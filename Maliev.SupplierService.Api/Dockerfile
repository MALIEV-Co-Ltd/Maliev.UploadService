# syntax=docker/dockerfile:1.4
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy NuGet config and solution files
COPY nuget.config ./
COPY Maliev.SupplierService.sln ./

# Copy project files for restore
COPY Maliev.SupplierService.Api/Maliev.SupplierService.Api.csproj Maliev.SupplierService.Api/
COPY Maliev.SupplierService.Data/Maliev.SupplierService.Data.csproj Maliev.SupplierService.Data/

# Restore with BuildKit secrets for private NuGet feeds
RUN --mount=type=secret,id=nuget_github_token \
    GITHUB_TOKEN=$(cat /run/secrets/nuget_github_token 2>/dev/null || echo "") && \
    if [ -n "$GITHUB_TOKEN" ]; then \
        dotnet nuget update source github --username docker --password "$GITHUB_TOKEN" --store-password-in-clear-text --configfile nuget.config 2>/dev/null || true; \
    fi && \
    dotnet restore Maliev.SupplierService.Api/Maliev.SupplierService.Api.csproj

# Copy source code
COPY Maliev.SupplierService.Api/ Maliev.SupplierService.Api/
COPY Maliev.SupplierService.Data/ Maliev.SupplierService.Data/

# Build and publish
WORKDIR /src/Maliev.SupplierService.Api
RUN dotnet publish -c Release -o /app/publish --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app

# Create non-root user (use different IDs to avoid conflicts with base image)
RUN groupadd --gid 1001 appgroup && \
    useradd --uid 1001 --gid appgroup --shell /bin/bash --create-home appuser

# Copy published application
COPY --from=build /app/publish .

# Set ownership and switch to non-root user
RUN chown -R appuser:appgroup /app
USER appuser

# Configure ASP.NET Core
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_EnableDiagnostics=0

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/suppliers/health/ready || exit 1

ENTRYPOINT ["dotnet", "Maliev.SupplierService.Api.dll"]
