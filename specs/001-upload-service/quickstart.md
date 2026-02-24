# Quickstart Guide: Upload Service

**Date**: 2025-12-05
**Branch**: 001-upload-service

## Overview

This guide helps developers quickly set up and test the Upload Service locally. By the end, you'll be able to upload files, validate security policies, and monitor metrics.

---

## Prerequisites

### Required Software

- **.NET 10 SDK**: [Download](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Docker Desktop**: [Download](https://www.docker.com/products/docker-desktop)
- **Git**: [Download](https://git-scm.com/downloads)
- **PowerShell 7+** (Windows/macOS/Linux): [Download](https://github.com/PowerShell/PowerShell)

### Optional Tools

- **Postman** or **curl** for API testing
- **Visual Studio 2022** or **VS Code** with C# extension
- **Azure Data Studio** or **pgAdmin** for PostgreSQL access

---

## Quick Start (5 Minutes)

### 1. Clone Repository

```bash
git clone https://github.com/MALIEV-Co-Ltd/Maliev.UploadService.git
cd Maliev.UploadService
git checkout 001-upload-service
```

### 2. Start Infrastructure

Start PostgreSQL, Redis, RabbitMQ, and ClamAV using Docker Compose:

```bash
docker-compose up -d
```

**Services Started**:
- PostgreSQL: `localhost:5432`
- Redis: `localhost:6379`
- RabbitMQ: `localhost:5672` (Management UI: `localhost:15672`)
- ClamAV: `localhost:3310`

**Verify Services**:

```bash
docker-compose ps
```

All services should show `Up` status.

### 3. Configure Secrets (Development)

For local development, create `appsettings.Development.json`:

```bash
cd Maliev.UploadService.Api
```

Create `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "UploadServiceDbContext": "Host=localhost;Port=5432;Database=uploadservice;Username=postgres;Password=devpassword",
    "redis": "localhost:6379",
    "rabbitmq": "amqp://guest:guest@localhost:5672"
  },
  "GCS": {
    "BucketName": "maliev-dev-uploads",
    "ProjectId": "maliev-dev",
    "CredentialsPath": "/path/to/service-account-key.json"
  },
  "ClamAV": {
    "Server": "localhost",
    "Port": 3310
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**Note**: For GCS, use a development service account or mock GCS storage for local testing.

### 4. Run Database Migrations

```bash
dotnet ef database update
```

### 5. Seed Test Data (Optional)

```bash
dotnet run --seed-data
```

This creates:
- Test service authorization policies (pdf-service, quotation-service, scan-service)
- Sample retention policies (7-day-transient, 30-day-standard, indefinite)

### 6. Start Upload Service

```bash
dotnet run
```

**Output**:

```
info: Maliev.UploadService.Api[0]
      Now listening on: http://localhost:8080
info: Maliev.UploadService.Api[0]
      Application started. Press Ctrl+C to shut down.
```

### 7. Verify Health

```bash
curl http://localhost:8080/uploadservice/liveness
```

**Expected Response**:

```json
{
  "status": "Healthy"
}
```

---

## Testing the API

### Generate Test JWT

For local testing, generate a JWT token using the test script:

```bash
pwsh .specify/scripts/powershell/generate-test-token.ps1 -ServiceId "pdf-service" -UserId "test-user-001"
```

**Output**:

```
JWT Token (valid for 1 hour):
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...
```

Save this token for API requests.

### Upload a File

**Using curl**:

```bash
export TOKEN="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."

curl -X POST http://localhost:8080/api/v1/uploads \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@test-receipt.pdf" \
  -F "targetPath=pdf/receipts/12345.pdf" \
  -F "checksum=d41d8cd98f00b204e9800998ecf8427e"
```

**Expected Response** (200 OK):

```json
{
  "uploadId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "storagePath": "pdf/receipts/12345.pdf",
  "versionETag": "\"33a64df551425fcc55e4d42a148795d9f25f89d4\"",
  "fileSize": 1048576,
  "checksum": "d41d8cd98f00b204e9800998ecf8427e",
  "uploadedAt": "2025-12-05T10:30:00Z",
  "signedUrl": "https://storage.googleapis.com/maliev-dev-uploads/pdf/receipts/12345.pdf?X-Goog-Algorithm=...",
  "expiresAt": null
}
```

### Retrieve File Metadata

```bash
curl -X GET "http://localhost:8080/api/v1/files/7c9e6679-7425-40de-944b-e07fc1f90ae7" \
  -H "Authorization: Bearer $TOKEN"
```

**Expected Response** (200 OK):

```json
{
  "fileId": "9b1deb4d-3b7d-4bad-9bdd-2b0d7b3dcb6d",
  "uploadId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "storagePath": "pdf/receipts/12345.pdf",
  "versionETag": "\"33a64df551425fcc55e4d42a148795d9f25f89d4\"",
  "fileSize": 1048576,
  "contentType": "application/pdf",
  "checksum": "d41d8cd98f00b204e9800998ecf8427e",
  "uploadedAt": "2025-12-05T10:30:00Z",
  "lastAccessedAt": null,
  "expiresAt": null,
  "storageClass": "STANDARD",
  "metadata": {}
}
```

### Generate Signed URL

```bash
curl -X POST "http://localhost:8080/api/v1/files/7c9e6679-7425-40de-944b-e07fc1f90ae7/signed-url" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "expirationMinutes": 60
  }'
```

**Expected Response** (200 OK):

```json
{
  "signedUrl": "https://storage.googleapis.com/maliev-dev-uploads/pdf/receipts/12345.pdf?X-Goog-Algorithm=...",
  "expiresAt": "2025-12-05T11:30:00Z"
}
```

### Delete File

```bash
curl -X DELETE "http://localhost:8080/api/v1/files/7c9e6679-7425-40de-944b-e07fc1f90ae7" \
  -H "Authorization: Bearer $TOKEN"
```

**Expected Response** (204 No Content)

---

## Testing Validation

### Test File Size Limit

Upload a file exceeding the service quota (default: 100MB for pdf-service):

```bash
# Create 101MB file
dd if=/dev/zero of=large-file.bin bs=1M count=101

curl -X POST http://localhost:8080/api/v1/uploads \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@large-file.bin" \
  -F "targetPath=pdf/receipts/large.pdf"
```

**Expected Response** (413 Payload Too Large):

```json
{
  "error": "FILE_SIZE_EXCEEDS_LIMIT",
  "message": "File size (105906176 bytes) exceeds service quota (104857600 bytes)"
}
```

### Test Invalid Content Type

Upload an executable file (not allowed for pdf-service):

```bash
curl -X POST http://localhost:8080/api/v1/uploads \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@malware.exe" \
  -F "targetPath=pdf/receipts/malware.pdf"
```

**Expected Response** (400 Bad Request):

```json
{
  "error": "INVALID_CONTENT_TYPE",
  "message": "Content type 'application/x-msdownload' not allowed for service pdf-service"
}
```

### Test Path Traversal Attack

Attempt to upload to unauthorized path:

```bash
curl -X POST http://localhost:8080/api/v1/uploads \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@test.pdf" \
  -F "targetPath=../../etc/passwd"
```

**Expected Response** (400 Bad Request):

```json
{
  "error": "INVALID_PATH",
  "message": "Path traversal detected: .."
}
```

---

## Monitoring & Metrics

### View Metrics

```bash
curl http://localhost:8080/uploadservice/metrics
```

**Sample Output** (Prometheus format):

```
# HELP upload_success Number of successful uploads
# TYPE upload_success counter
upload_success{service_name="upload-service",service_id="pdf-service",file_type="application/pdf",file_size_bucket="1-10MB",environment="Development"} 42

# HELP upload_duration Upload duration distribution
# TYPE upload_duration histogram
upload_duration_bucket{service_name="upload-service",service_id="pdf-service",file_type="application/pdf",file_size_bucket="1-10MB",environment="Development",le="100"} 5
upload_duration_bucket{service_name="upload-service",service_id="pdf-service",file_type="application/pdf",file_size_bucket="1-10MB",environment="Development",le="500"} 35
upload_duration_bucket{service_name="upload-service",service_id="pdf-service",file_type="application/pdf",file_size_bucket="1-10MB",environment="Development",le="1000"} 42
```

### View Health Checks

```bash
curl http://localhost:8080/uploadservice/health
```

**Sample Output**:

```json
{
  "status": "Healthy",
  "totalDuration": "00:00:00.0234567",
  "entries": {
    "db": {
      "status": "Healthy",
      "duration": "00:00:00.0123456"
    },
    "redis": {
      "status": "Healthy",
      "duration": "00:00:00.0098765"
    },
    "rabbitmq": {
      "status": "Healthy",
      "duration": "00:00:00.0012345"
    }
  }
}
```

### View API Documentation

Open browser to: `http://localhost:8080/uploadservice/scalar/v1`

This loads the Scalar API documentation UI with interactive request testing.

---

## Common Issues & Troubleshooting

### Issue: Database Connection Failed

**Symptom**:

```
Unable to connect to PostgreSQL server
```

**Solution**:

1. Verify PostgreSQL is running:

   ```bash
   docker-compose ps postgres
   ```

2. Check connection string in `appsettings.Development.json`

3. Restart PostgreSQL:

   ```bash
   docker-compose restart postgres
   ```

### Issue: ClamAV Not Ready

**Symptom**:

```
Malware scan failed: Connection refused
```

**Solution**:

ClamAV takes 1-2 minutes to initialize virus database. Wait and retry, or check logs:

```bash
docker-compose logs clamav
```

Look for: `Daemon started` message.

### Issue: JWT Token Invalid

**Symptom**:

```json
{
  "error": "UNAUTHORIZED",
  "message": "Invalid or expired token"
}
```

**Solution**:

1. Regenerate token using `generate-test-token.ps1`
2. Ensure token is passed in `Authorization: Bearer {token}` header
3. Check token expiration (default: 1 hour)

### Issue: GCS Credentials Not Found

**Symptom**:

```
Google.Apis.Auth.OAuth2.GoogleApplicationDefaultCredentials not found
```

**Solution**:

For local testing, use one of:

1. **Service Account Key**: Set `GOOGLE_APPLICATION_CREDENTIALS` environment variable:

   ```bash
   export GOOGLE_APPLICATION_CREDENTIALS="/path/to/service-account-key.json"
   ```

2. **Mock GCS** (recommended for local dev): Use Testcontainers GCS emulator in tests

---

## Running Tests

### Unit Tests

```bash
cd Maliev.UploadService.Tests
dotnet test --filter "Category=Unit"
```

### Integration Tests

```bash
dotnet test --filter "Category=Integration"
```

**Note**: Integration tests use Testcontainers to spin up real PostgreSQL, Redis, and RabbitMQ instances.

### Test Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html
```

Open `coverage-report/index.html` in browser.

---

## Docker Build (Local)

### Build Image

```bash
docker build -t maliev-upload-service:local .
```

### Run Container

```bash
docker run -p 8080:8080 \
  -e ConnectionStrings__UploadServiceDbContext="Host=host.docker.internal;Port=5432;Database=uploadservice;Username=postgres;Password=devpassword" \
  -e ConnectionStrings__redis="host.docker.internal:6379" \
  -e ConnectionStrings__rabbitmq="amqp://guest:guest@host.docker.internal:5672" \
  -e GCS__BucketName="maliev-dev-uploads" \
  -e GOOGLE_APPLICATION_CREDENTIALS="/secrets/gcs-key.json" \
  -v /path/to/gcs-key.json:/secrets/gcs-key.json \
  maliev-upload-service:local
```

---

## Next Steps

1. **Read Contracts**: Review `contracts/openapi.yaml` and `contracts/rabbitmq-events.md`
2. **Explore Code**: Navigate source in `Maliev.UploadService.Api/`
3. **Write Tests**: Add tests in `Maliev.UploadService.Tests/`
4. **Implement Features**: Follow `tasks.md` (generated by `/speckit.tasks`)

---

## Useful Commands

| Task | Command |
|------|---------|
| Start infrastructure | `docker-compose up -d` |
| Stop infrastructure | `docker-compose down` |
| View logs | `docker-compose logs -f <service>` |
| Reset database | `dotnet ef database drop && dotnet ef database update` |
| Generate migration | `dotnet ef migrations add <MigrationName>` |
| Run service | `dotnet run` |
| Run tests | `dotnet test` |
| View metrics | `curl http://localhost:8080/uploadservice/metrics` |
| View health | `curl http://localhost:8080/uploadservice/health` |
| Generate JWT | `pwsh generate-test-token.ps1 -ServiceId "pdf-service"` |

---

## Support

For questions or issues:

- **Slack**: #maliev-upload-service
- **Email**: platform-eng@maliev.com
- **Documentation**: `specs/001-upload-service/`

