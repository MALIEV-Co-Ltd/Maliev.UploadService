# T177: Dockerfile following Constitution Principle X best practices
# Multi-stage build using .NET 10, built-in app user, BuildKit secrets, health check

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project file and nuget.config
COPY ["Maliev.UploadService.Api/Maliev.UploadService.Api.csproj", "Maliev.UploadService.Api/"]
COPY ["nuget.config", "./"]

# Restore using BuildKit secrets for GitHub Packages authentication
RUN --mount=type=secret,id=github_token \
    export GITHUB_TOKEN=$(cat /run/secrets/github_token) && \
    dotnet restore "Maliev.UploadService.Api/Maliev.UploadService.Api.csproj"

# Copy source code
COPY . .
WORKDIR "/src/Maliev.UploadService.Api"

# Build
RUN dotnet build "Maliev.UploadService.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/build \
    --no-restore \
    /p:TreatWarningsAsErrors=true

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "Maliev.UploadService.Api.csproj" \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-build \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Health check endpoint as per Constitution Principle X
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/uploadservice/liveness || exit 1

ENTRYPOINT ["dotnet", "Maliev.UploadService.Api.dll"]
