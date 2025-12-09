# Maliev.UploadService

> A secure, scalable file upload microservice providing centralized file storage management for the MALIEV platform.

[![CI](https://github.com/MALIEV-Co-Ltd/Maliev.UploadService/actions/workflows/ci.yml/badge.svg)](https://github.com/MALIEV-Co-Ltd/Maliev.UploadService/actions/workflows/ci.yml)
[![License](https://img.shields.io/badge/license-Proprietary-red.svg)](LICENSE)

## Overview

The Upload Service is a core infrastructure microservice that abstracts Google Cloud Storage complexity and provides secure file upload capabilities to all MALIEV microservices. It enforces validation policies, manages file lifecycle, provides access control through signed URLs, and supports high-performance concurrent operations.

### Key Features

- **Secure File Upload**: JWT authentication, service-level authorization, malware scanning (ClamAV)
- **File Validation**: Content-type detection, size limits, MIME type whitelisting
- **Path Organization**: Dynamic path templates with collision detection
- **Lifecycle Management**: Automated retention policies and storage class transitions
- **Access Control**: Signed URL generation with configurable expiration
- **Large File Support**: Streaming uploads up to 10GB with resumable capability
- **Async Notifications**: RabbitMQ events for upload completion, failure, and deletion
- **Bulk Operations**: Background job processing for large-scale deletions
- **Observability**: OpenTelemetry metrics, structured logging, health checks

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                      Upload Service API                         │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │  Controllers │  │   Services   │  │  Background  │          │
│  │   (REST)     │──│ (Business    │──│   Workers    │          │
│  │              │  │   Logic)     │  │              │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└────────┬──────────────────┬─────────────────┬──────────────────┘
         │                  │                 │
         ↓                  ↓                 ↓
  ┌────────────┐   ┌──────────────┐   ┌────────────┐
  │ PostgreSQL │   │     Redis    │   │  RabbitMQ  │
  │ (Metadata) │   │   (Cache)    │   │  (Events)  │
  └────────────┘   └──────────────┘   └────────────┘
         │
         ↓
  ┌─────────────────────┐
  │  Google Cloud       │
  │  Storage (Files)    │
  └─────────────────────┘
```

## Technology Stack

- **.NET 10.0**: ASP.NET Core Web API
- **Entity Framework Core**: PostgreSQL provider
- **MassTransit**: RabbitMQ messaging
- **Google.Cloud.Storage.V1**: GCS integration
- **StackExchange.Redis**: Distributed caching
- **OpenTelemetry**: Observability and metrics
- **xUnit + Testcontainers**: Integration testing

## Getting Started

### Prerequisites

- .NET 10.0 SDK
- Docker Desktop (for local development)
- Google Cloud SDK (for production deployment)
- Access to GitHub Packages (for Maliev.Aspire.ServiceDefaults)

### Local Development Setup

1. **Clone the repository**:
   ```bash
   git clone https://github.com/MALIEV-Co-Ltd/Maliev.UploadService.git
   cd Maliev.UploadService
   ```

2. **Start infrastructure services**:
   ```bash
   docker-compose up -d
   ```
   This starts PostgreSQL, Redis, RabbitMQ, and ClamAV.

3. **Configure environment variables**:
   Create `appsettings.Development.json`:
   ```json
   {
     "ConnectionStrings": {
       "PostgreSQL": "Host=localhost;Port=5432;Database=uploadservice;Username=postgres;Password=postgres",
       "Redis": "localhost:6379",
       "RabbitMQ": "amqp://guest:guest@localhost:5672"
     },
     "GoogleCloud": {
       "BucketName": "maliev-uploads-dev"
     },
     "ClamAV": {
       "Host": "localhost",
       "Port": "3310"
     },
     "Authentication": {
       "Authority": "https://your-auth-server.com",
       "Audience": "upload-service"
     }
   }
   ```

4. **Restore dependencies**:
   ```bash
   dotnet restore
   ```

5. **Run database migrations**:
   ```bash
   dotnet ef database update --project Maliev.UploadService.Api
   ```

6. **Run the service**:
   ```bash
   dotnet run --project Maliev.UploadService.Api
   ```

   The API will be available at `https://localhost:5001`

7. **View API documentation**:
   Navigate to `https://localhost:5001/scalar/v1` in your browser

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test Maliev.UploadService.Tests
```

## API Endpoints

### Upload Operations

- `POST /api/v1/uploads` - Upload a file with validation
- `POST /api/v1/uploads/resumable` - Initiate resumable upload
- `PUT /api/v1/uploads/resumable/{uploadId}` - Continue resumable upload

### File Management

- `GET /api/v1/files/{uploadId}` - Get file metadata
- `GET /api/v1/files?pathPrefix={prefix}` - Query files by path
- `POST /api/v1/files/{uploadId}/signed-url` - Generate signed URL
- `DELETE /api/v1/files/{uploadId}` - Delete file

### Admin Operations

- `POST /api/v1/admin/bulk-delete` - Initiate bulk delete job
- `GET /api/v1/admin/bulk-delete/{jobId}` - Get bulk delete job status

### Health & Observability

- `GET /uploadservice/health` - Health check endpoint
- `GET /uploadservice/metrics` - Prometheus metrics

## Configuration

### Service Authorization Policies

Each microservice must have an authorization policy configured in the database:

```sql
INSERT INTO service_authorization_policies (
    policy_id, service_id, service_name,
    allowed_path_prefixes, allowed_content_types,
    max_file_size_bytes, storage_quota_bytes,
    allow_overwrite, allow_resumable_upload
) VALUES (
    gen_random_uuid(), 'quotation-service', 'Quotation Service',
    '["quotations/", "attachments/"]', '["application/pdf", "image/png", "image/jpeg"]',
    104857600, 10737418240,
    false, true
);
```

### Retention Policies

Configure lifecycle management rules:

```sql
INSERT INTO retention_policies (
    policy_id, policy_name, service_id,
    retention_days, storage_class_transitions,
    apply_to_path_prefix, is_active
) VALUES (
    gen_random_uuid(), '7-day-transient', 'quotation-service',
    7, '[{"days": 1, "storageClass": "NEARLINE"}]',
    'quotations/temp/', true
);
```

## Deployment

### Docker Build

```bash
# Build with BuildKit secrets
docker build --secret id=github_token,env=GITHUB_TOKEN \
  -t upload-service:latest .
```

### Kubernetes Deployment

The service is deployed to Google Kubernetes Engine (GKE) via GitOps workflow:

1. Push to `develop` branch triggers CI/CD
2. Docker image built and pushed to Artifact Registry
3. Pull request created in `maliev-gitops` repository
4. ArgoCD syncs deployment after PR approval

## Monitoring & Metrics

### Business Metrics

- Upload success/failure rates by service
- Upload duration distribution by file size
- File validation rejection rates
- Storage quota utilization
- Active upload count
- Signed URL generation frequency
- Bulk delete job progress

### Technical Metrics

- API request latency
- Database connection pool usage
- Redis cache hit/miss rates
- RabbitMQ message throughput
- GCS API call latency

Access metrics at `/uploadservice/metrics` (Prometheus format)

## Security

- **Authentication**: JWT tokens required for all endpoints
- **Authorization**: Service-level path-based access control
- **Malware Scanning**: ClamAV integration for all uploads
- **Input Validation**: Content-type detection, size limits, path sanitization
- **Secrets Management**: Google Secret Manager integration
- **Encryption**: TLS in transit, GCS encryption at rest

## Development Guidelines

### Code Style

- Follow .NET naming conventions
- Use Data Annotations for validation (no FluentValidation)
- Explicit mapping via extension methods (no AutoMapper)
- TreatWarningsAsErrors=true in all builds

### Testing Standards

- Test-first development (TDD) required
- Real infrastructure testing (Testcontainers, no InMemory databases)
- 80%+ code coverage for business logic
- Integration tests for all API endpoints

### Constitution Compliance

This service follows the [MALIEV Architecture Constitution](CLAUDE.md):
- ✅ Service autonomy with owned database
- ✅ Explicit REST and event contracts
- ✅ Test-first development
- ✅ Real infrastructure testing
- ✅ OpenTelemetry observability
- ✅ JWT authentication & authorization
- ✅ Google Secret Manager integration
- ✅ Zero warnings policy
- ✅ Docker best practices

## Troubleshooting

### Common Issues

**ClamAV connection fails**:
```bash
# Restart ClamAV container
docker-compose restart clamav

# Check ClamAV logs
docker-compose logs clamav
```

**Database migration fails**:
```bash
# Drop and recreate database
docker-compose down -v
docker-compose up -d postgres
dotnet ef database update --project Maliev.UploadService.Api
```

**GCS authentication fails**:
```bash
# Set up application default credentials
gcloud auth application-default login

# Or use service account key
export GOOGLE_APPLICATION_CREDENTIALS=/path/to/key.json
```

## Contributing

1. Create feature branch from `develop`
2. Write tests first (TDD)
3. Implement feature with tests passing
4. Run code formatter: `dotnet format`
5. Ensure zero warnings: `dotnet build /p:TreatWarningsAsErrors=true`
6. Create pull request to `develop`

## License

Proprietary - MALIEV Co., Ltd. All rights reserved.

## Support

- **Documentation**: See `specs/001-upload-service/` directory
- **Issues**: [GitHub Issues](https://github.com/MALIEV-Co-Ltd/Maliev.UploadService/issues)
- **Internal Support**: #upload-service Slack channel