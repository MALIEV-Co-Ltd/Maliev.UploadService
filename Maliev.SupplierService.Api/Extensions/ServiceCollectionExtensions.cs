using System.Text;
using FluentValidation;
using Maliev.SupplierService.Api.Configuration;
using Maliev.SupplierService.Api.Services;
using Maliev.SupplierService.Api.Services.ExternalServices;
using Maliev.SupplierService.Data;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Polly;
using StackExchange.Redis;

namespace Maliev.SupplierService.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSupplierServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configuration
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<RedisSettings>(configuration.GetSection(RedisSettings.SectionName));
        services.Configure<RabbitMQSettings>(configuration.GetSection(RabbitMQSettings.SectionName));
        services.Configure<ExternalServicesSettings>(configuration.GetSection(ExternalServicesSettings.SectionName));

        // Database - with DbContextFactory for scenarios needing separate instances
        services.AddDbContextFactory<SupplierDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("ServiceDbContext"));
        });
        services.AddDbContext<SupplierDbContext>(options =>
        {
            options.UseNpgsql(configuration.GetConnectionString("ServiceDbContext"));
        });

        // FluentValidation
        services.AddValidatorsFromAssemblyContaining<Program>();

        // Services
        services.AddScoped<ISupplierService, Services.SupplierService>();
        services.AddScoped<ICacheService, CacheService>();
        services.AddScoped<IAuditService, AuditService>();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? new JwtSettings();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtSettings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.PublicKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var redisSettings = configuration.GetSection(RedisSettings.SectionName).Get<RedisSettings>()
            ?? new RedisSettings();

        if (redisSettings.Enabled)
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(redisSettings.ConnectionString));

            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisSettings.ConnectionString;
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        return services;
    }

    public static IServiceCollection AddMassTransitWithRabbitMq(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var rabbitMqSettings = configuration.GetSection(RabbitMQSettings.SectionName).Get<RabbitMQSettings>()
            ?? new RabbitMQSettings();

        services.AddMassTransit(config =>
        {
            config.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitMqSettings.Host, rabbitMqSettings.VirtualHost, h =>
                {
                    h.Username(rabbitMqSettings.Username);
                    h.Password(rabbitMqSettings.Password);
                });

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    public static IServiceCollection AddApiVersioningConfiguration(this IServiceCollection services)
    {
        services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
        })
        .AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'V";
            options.SubstituteApiVersionInUrl = true;
        });

        return services;
    }

    public static IServiceCollection AddExternalServiceClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration.GetSection(ExternalServicesSettings.SectionName).Get<ExternalServicesSettings>()
            ?? new ExternalServicesSettings();

        // PurchaseOrder Service Client
        services.AddHttpClient<IPurchaseOrderServiceClient, PurchaseOrderServiceClient>(client =>
        {
            client.BaseAddress = new Uri(settings.PurchaseOrderService.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.PurchaseOrderService.TimeoutInSeconds);
        })
        .AddStandardResilienceHandler();

        // Invoice Service Client
        services.AddHttpClient<IInvoiceServiceClient, InvoiceServiceClient>(client =>
        {
            client.BaseAddress = new Uri(settings.InvoiceService.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.InvoiceService.TimeoutInSeconds);
        })
        .AddStandardResilienceHandler();

        // Stock Service Client
        services.AddHttpClient<IStockServiceClient, StockServiceClient>(client =>
        {
            client.BaseAddress = new Uri(settings.StockService.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.StockService.TimeoutInSeconds);
        })
        .AddStandardResilienceHandler();

        return services;
    }
}
