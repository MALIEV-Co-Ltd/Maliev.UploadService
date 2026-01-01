using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Maliev.UploadService.Tests.Testing;

/// <summary>
/// Singleton-like fixture for managing shared Testcontainers.
/// </summary>
public static class TestContainerFixture
{
    public static readonly PostgreSqlContainer PostgresContainer = new PostgreSqlBuilder()
        .WithImage("postgres:18-alpine")
        .WithCommand("-c", "max_connections=1000")
        .Build();

    public static readonly RedisContainer RedisContainer = new RedisBuilder()
        .WithImage("redis:8.4-alpine")
        .Build();

    public static readonly RabbitMqContainer RabbitMqContainer = new RabbitMqBuilder()
        .WithImage("rabbitmq:4.2-alpine")
        .Build();

    private static bool _containersStarted;
    private static readonly SemaphoreSlim _startupLock = new(1, 1);

    public static async Task InitializeAsync()
    {
        if (_containersStarted) return;

        await _startupLock.WaitAsync();
        try
        {
            if (_containersStarted) return;

            await Task.WhenAll(
                PostgresContainer.StartAsync(),
                RedisContainer.StartAsync(),
                RabbitMqContainer.StartAsync()
            );

            _containersStarted = true;
        }
        finally
        {
            _startupLock.Release();
        }
    }
}
