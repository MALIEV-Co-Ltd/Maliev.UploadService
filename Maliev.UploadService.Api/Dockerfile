# Use the official .NET 9.0 runtime as base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Use .NET 9.0 SDK for building
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy project files
COPY ["Maliev.UploadService.Api/Maliev.UploadService.Api.csproj", "Maliev.UploadService.Api/"]
COPY ["Maliev.UploadService.Data/Maliev.UploadService.Data.csproj", "Maliev.UploadService.Data/"]

# Restore dependencies
RUN dotnet restore "./Maliev.UploadService.Api/Maliev.UploadService.Api.csproj"

# Copy all source code
COPY . .

# Build the application
WORKDIR "/src/Maliev.UploadService.Api"
RUN dotnet build "./Maliev.UploadService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

# Publish the application
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Maliev.UploadService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# Final stage - runtime image
FROM base AS final
WORKDIR /app

# Copy the published application
COPY --from=publish /app/publish .

# Create non-root user for security
RUN adduser --disabled-password --gecos "" --uid 1001 appuser && chown -R appuser /app
USER appuser

# Set environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/uploads/liveness || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Maliev.UploadService.Api.dll"]