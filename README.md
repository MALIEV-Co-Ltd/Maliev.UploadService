# Maliev Upload Service

[![Build Status](https://img.shields.io/badge/Build-Passing-success)](https://github.com/ORGANIZATION/Maliev.UploadService)
[![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Database](https://img.shields.io/badge/Database-PostgreSQL%2018-blue)](https://www.postgresql.org/)

Enterprise-grade file orchestration and secure storage management service for the Maliev ecosystem.

**Role in MALIEV Architecture**: The centralized gateway for artifact persistence. It abstracts complex storage providers (Google Cloud Storage) while providing secure, validated file uploads, dynamic path resolution, and controlled access through signed URLs for all platform microservices.

---

## 🏗️ Architecture & Tech Stack

- **Framework**: ASP.NET Core 10.0 (C# 13)
- **Primary Storage**: Google Cloud Storage (Large scale persistence)
- **Database**: PostgreSQL 18 with Entity Framework Core 10.x (Metadata registry)
- **Distributed Cache**: Redis 7.x (Upload session & path metadata caching)
- **Messaging**: RabbitMQ via MassTransit
- **Security Logic**: ClamAV integration for automated malware scanning
- **Observability**: OpenTelemetry (Metrics, Traces, Logging)

---

## ⚖️ Constitution Rules

This service strictly adheres to the platform development mandates:

### Banned Libraries
To maintain high performance and low complexity, the following are **NOT** used:
- ❌ **AutoMapper**: Explicit manual mapping only.
- ❌ **FluentValidation**: Standard Data Annotations (`[Required]`, `[EmailAddress]`) only.
- ❌ **FluentAssertions**: Standard xUnit `Assert` methods only.
- ❌ **In-memory Test DB**: All integration tests use **Testcontainers** with real PostgreSQL 18.

### Mandatory Practices
- ✅ **TreatWarningsAsErrors**: Enabled in all `.csproj` files.
- ✅ **XML Documentation**: Required on all public methods and properties.
- ✅ **No Secrets in Code**: All sensitive configuration injected via environment variables.
- ✅ **No Test Config in Program.cs**: Test configuration in test fixtures only.
- ✅ **IAM Integration**: Self-registers permissions with the IAM Service using GCP-style naming: `{service}.{resource}.{action}`.

---

## ✨ Key Features

- **Validated High-Capacity Uploads**: Support for streaming huge files (up to 10GB) with strict content-type verification and malware scanning.
- **Dynamic Path Resolution**: Intelligent collision-aware organization system using dynamic path templates and metadata tagging.
- **Secure Signed Access**: Generation of temporary, expiring access URLs with granular permissions for controlled resource retrieval.
- **Automated Lifecycle Management**: Resource-specific retention policies that automatically handle storage tier transitions and archival.
- **Bulk Job Orchestration**: High-throughput background workers for large-scale cleanup and mass-deletion operations.

---

## 🚀 Quick Start

### Prerequisites
- .NET 10.0 SDK
- Docker Desktop (for infrastructure)
- PostgreSQL 18 (Alpine)

### Local Development Setup

1. **Clone the repository**
```bash
git clone https://github.com/ORGANIZATION/Maliev.UploadService.git
cd Maliev.UploadService
```

2. **Spin up Infrastructure**
```bash
docker run --name upload-db -e POSTGRES_PASSWORD=YOUR_PASSWORD -p 5432:5432 -d postgres:18-alpine
docker run --name upload-redis -p 6379:6379 -d redis:7-alpine
docker run --name upload-clamav -p 3310:3310 -d clamav/clamav:latest
```

3. **Configure Environment**
```powershell
# Windows PowerShell
$env:ConnectionStrings__PostgreSQL="YOUR_POSTGRES_CONNECTION_STRING"
$env:ConnectionStrings__Redis="YOUR_REDIS_CONNECTION_STRING"
$env:GoogleCloud__BucketName="YOUR_BUCKET_NAME"
```

4. **Apply Migrations & Run**
```bash
dotnet ef database update --project Maliev.UploadService.Api
dotnet run --project Maliev.UploadService.Api
```

The service will be available at `http://localhost:5000/api/v1/uploads`. Access the interactive documentation at `http://localhost:5000/scalar/v1`.

---

## 📡 API Endpoints

All endpoints are prefixed with `/api/v1/`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| POST | `/uploads` | Upload a new artifact with security validation |
| GET | `/files/{id}` | Retrieve comprehensive file metadata and provenance |
| POST | `/files/{id}/signed-url` | Generate a temporary expiring download link |
| POST | `/admin/bulk-delete` | Initiate a batch deletion background job |

---

## 🏥 Health & Monitoring

Standardized health probes for Kubernetes orchestration:
- **Liveness**: `GET /uploadservice/health`
- **Readiness**: `GET /uploadservice/readiness` (Checks DB, GCS, and Redis connectivity)
- **Metrics**: `GET /uploadservice/metrics` (Prometheus format)

---

## 🧪 Testing

We prioritize reliable tests over mock-heavy unit tests.

```bash
# Run all tests using Testcontainers
dotnet test --verbosity normal
```

- **Integration Tests**: Use real PostgreSQL 18 and Redis containers.
- **Contract Tests**: Ensure API stability for consumers.

---

## 📦 Deployment

Infrastructure management is handled via GitOps patterns.

- **Docker Image**: `REGION-docker.pkg.dev/PROJECT_ID/REPOSITORY/maliev-upload-service:{sha}`
- **Environments**: Development, Staging, Production

---

## 📄 License

Proprietary - © 2025 MALIEV Co., Ltd. All rights reserved.
