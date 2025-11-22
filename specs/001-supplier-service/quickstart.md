# Quickstart: Supplier Service

**Branch**: `001-supplier-service` | **Date**: 2025-11-22

## Prerequisites

### Required Software

| Software | Version | Purpose |
|----------|---------|---------|
| .NET SDK | 10.0+ | Build and run |
| Docker Desktop | 4.x+ | Testcontainers, local dev |
| PostgreSQL | 18 | Database (via container) |
| Git | 2.x+ | Version control |

### Required Access

- GitHub PAT with `read:packages` scope for `Maliev.Aspire.ServiceDefaults`
- Access to MALIEV GitHub organization

## Quick Setup

### 1. Clone and Navigate

```bash
git clone https://github.com/MALIEV-Co-Ltd/Maliev.SupplierService.git
cd Maliev.SupplierService
git checkout 001-supplier-service
```

### 2. Configure NuGet Authentication

Set environment variables for GitHub Packages access:

```bash
# Linux/macOS
export NUGET_USERNAME="your-github-username"
export NUGET_PASSWORD="your-github-pat"

# Windows PowerShell
$env:NUGET_USERNAME = "your-github-username"
$env:NUGET_PASSWORD = "your-github-pat"
```

### 3. Restore and Build

```bash
dotnet restore Maliev.SupplierService.sln
dotnet build Maliev.SupplierService.sln --no-restore
```

### 4. Run Tests

Tests use Testcontainers - Docker must be running:

```bash
dotnet test Maliev.SupplierService.Tests/Maliev.SupplierService.Tests.csproj
```

### 5. Run Locally

For local development with all dependencies:

```bash
# Start infrastructure via docker-compose (for local dev only)
docker-compose -f docker-compose.dev.yml up -d

# Run the API
dotnet run --project Maliev.SupplierService.Api/Maliev.SupplierService.Api.csproj
```

## Local Development Configuration

### appsettings.Development.json

```json
{
  "ConnectionStrings": {
    "ServiceDbContext": "Host=localhost;Port=5432;Database=supplier_app_db;Username=postgres;Password=postgres"
  },
  "Jwt": {
    "PublicKey": "<base64-encoded-public-key>",
    "Issuer": "https://auth.maliev.local",
    "Audience": "https://api.maliev.local"
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  },
  "Redis": {
    "ConnectionString": "localhost:6379",
    "Enabled": true
  },
  "ExternalServices": {
    "PurchaseOrderService": {
      "BaseUrl": "http://localhost:5001",
      "TimeoutInSeconds": 30
    },
    "InvoiceService": {
      "BaseUrl": "http://localhost:5002",
      "TimeoutInSeconds": 30
    },
    "StockService": {
      "BaseUrl": "http://localhost:5003",
      "TimeoutInSeconds": 30
    }
  },
  "CORS": {
    "AllowedOrigins": ["http://localhost:3000"]
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug"
    }
  }
}
```

### docker-compose.dev.yml

```yaml
version: '3.8'

services:
  postgres:
    image: postgres:18
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: supplier_app_db
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data

  rabbitmq:
    image: rabbitmq:3-management
    environment:
      RABBITMQ_DEFAULT_USER: guest
      RABBITMQ_DEFAULT_PASS: guest
    ports:
      - "5672:5672"
      - "15672:15672"

  redis:
    image: redis:7.0
    ports:
      - "6379:6379"

volumes:
  postgres_data:
```

## Database Migrations

### Create Migration

```bash
cd Maliev.SupplierService.Data
dotnet ef migrations add InitialCreate --startup-project ../Maliev.SupplierService.Api
```

### Apply Migration (Development Only)

```bash
dotnet ef database update --startup-project ../Maliev.SupplierService.Api
```

**Note**: Migrations are NOT auto-applied in production. Use manual migration scripts.

## API Endpoints

Once running, access:

| Endpoint | Description |
|----------|-------------|
| `http://localhost:8080/suppliers/scalar/v1` | API Documentation (Scalar UI) |
| `http://localhost:8080/suppliers/openapi/v1.json` | OpenAPI Spec |
| `http://localhost:8080/suppliers/liveness` | Liveness probe |
| `http://localhost:8080/suppliers/readiness` | Readiness probe |
| `http://localhost:8080/suppliers/metrics` | Prometheus metrics |

## Testing Strategy

### Test Categories

| Category | Command | Description |
|----------|---------|-------------|
| All | `dotnet test` | Run all tests |
| Unit | `dotnet test --filter Category=Unit` | Unit tests only |
| Integration | `dotnet test --filter Category=Integration` | Integration tests (requires Docker) |
| Contract | `dotnet test --filter Category=Contract` | API contract tests |

### Infrastructure Requirements

Integration tests use Testcontainers to spin up:
- PostgreSQL 18 container
- RabbitMQ 3-management container
- Redis 7.0 container

No external infrastructure needed - Docker handles everything.

## Build Docker Image

```bash
# With BuildKit secrets for NuGet
NUGET_USERNAME=your-username NUGET_PASSWORD=your-pat \
docker build \
  --secret id=nuget_username,env=NUGET_USERNAME \
  --secret id=nuget_password,env=NUGET_PASSWORD \
  -t maliev-supplier-service:latest \
  -f Maliev.SupplierService.Api/Dockerfile .
```

## Common Issues

### Issue: NuGet restore fails for ServiceDefaults

**Solution**: Ensure `NUGET_USERNAME` and `NUGET_PASSWORD` environment variables are set with valid GitHub credentials.

### Issue: Tests fail with "Docker not available"

**Solution**: Ensure Docker Desktop is running. Testcontainers requires Docker.

### Issue: Database connection refused

**Solution**: Ensure PostgreSQL container is running via `docker-compose.dev.yml`.

### Issue: Build warnings treated as errors

**Solution**: Fix all warnings before building. Zero warnings policy is enforced.

## Key Files

| File | Purpose |
|------|---------|
| `Maliev.SupplierService.Api/Program.cs` | Application entry point |
| `Maliev.SupplierService.Data/SupplierDbContext.cs` | EF Core DbContext |
| `Maliev.SupplierService.Api/Dockerfile` | Production container build |
| `nuget.config` | NuGet package sources including GitHub Packages |
| `specs/001-supplier-service/contracts/openapi-v1.yaml` | API contract specification |

## Related Documentation

- [Feature Specification](./spec.md)
- [Implementation Plan](./plan.md)
- [Research Decisions](./research.md)
- [Data Model](./data-model.md)
- [OpenAPI Contract](./contracts/openapi-v1.yaml)
