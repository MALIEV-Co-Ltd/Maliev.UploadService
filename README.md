# Maliev Upload Service

Clean, business-agnostic file storage service for Google Cloud Storage using path-based architecture.

## 🏗️ Architecture

The Upload Service follows a **clean architecture** approach where business services own their storage patterns and the Upload Service provides pure infrastructure:

```
┌─────────────────┐    ┌─────────────────┐
│ Quotation       │    │ Order           │
│ Service         │    │ Service         │
│                 │    │                 │
│ Generates:      │    │ Generates:      │
│ "quotations/    │    │ "orders/        │
│  QUO-001/       │    │  ORD-001/       │
│  documents/"    │    │  production/"   │
└─────────┬───────┘    └─────────┬───────┘
          │                      │
          └──────────┬───────────┘
                     │
          ┌─────────────────────────┐
          │ Upload Service          │
          │ ✅ PURE INFRASTRUCTURE  │
          │                         │
          │ uploadToPath(           │
          │   objectPath,           │
          │   file,                 │
          │   options               │
          │ )                       │
          └─────────────────────────┘
```

## 🚀 API Endpoints

### Base URL
- **Development**: `http://localhost:5000/uploads/v1`
- **Production**: `https://api.maliev.com/uploads/v1`

### Core Operations

#### Upload File to Path
```http
POST /uploads/v1/path?objectPath={path}
Content-Type: multipart/form-data

# Example
POST /uploads/v1/path?objectPath=quotations/QUO-001/documents/contract.pdf
Content-Type: multipart/form-data

file: [binary data]
```

#### Upload with Request Body
```http
POST /uploads/v1
Content-Type: multipart/form-data

ObjectPath: quotations/QUO-001/documents/contract.pdf
File: [binary data]
StorageOptions.RetentionDays: 2555
ServiceMetadata[quotationId]: QUO-001
```

#### Download File
```http
GET /uploads/v1/path?objectPath={path}

# Example
GET /uploads/v1/path?objectPath=quotations/QUO-001/documents/contract.pdf
```

#### Delete File
```http
DELETE /uploads/v1/path?objectPath={path}
```

#### Check File Existence
```http
HEAD /uploads/v1/path?objectPath={path}
```

#### Generate Signed URL
```http
POST /uploads/v1/path/signed-url?objectPath={path}&expirationHours=1
```

### Health & Monitoring
```http
GET /uploads/liveness    # Health check
GET /uploads/readiness   # Readiness check
GET /uploads/metrics     # Prometheus metrics
```

### Swagger Documentation
- **Local**: `http://localhost:5000/uploads/swagger`
- **Production**: `https://api.maliev.com/uploads/swagger`

## 🔧 Service Integration

### HTTP Client Setup

```csharp
// Program.cs
builder.Services.AddHttpClient<IUploadServiceClient, UploadServiceClient>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Services:UploadService:BaseUrl"]);
    client.DefaultRequestHeaders.Add("X-Service-Name", "QuotationService");
});
```

### Client Implementation

```csharp
public interface IUploadServiceClient
{
    Task<FileUploadResponse> UploadFileToPathAsync(string objectPath, IFormFile file);
    Task<FileDownloadResponse?> DownloadFileByPathAsync(string objectPath);
    Task<bool> DeleteFileByPathAsync(string objectPath);
    Task<bool> FileExistsByPathAsync(string objectPath);
    Task<string> GenerateSignedUrlByPathAsync(string objectPath, TimeSpan expiration);
}

public class UploadServiceClient : IUploadServiceClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenService _tokenService;

    public async Task<FileUploadResponse> UploadFileToPathAsync(string objectPath, IFormFile file)
    {
        await SetAuthorizationHeader();

        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(file.OpenReadStream());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        form.Add(fileContent, "file", file.FileName);

        var response = await _httpClient.PostAsync($"path?objectPath={objectPath}", form);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<FileUploadResponse>(json);
    }

    private async Task SetAuthorizationHeader()
    {
        var token = await _tokenService.GetTokenAsync();
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
}
```

## 💡 Integration Examples

### Quotation Service
```csharp
public async Task<string> UploadQuotationDocumentAsync(
    string quotationId,
    IFormFile document,
    string documentType)
{
    // Business service generates its own storage path
    var objectPath = $"quotations/{quotationId}/documents/{documentType}/{DateTime.UtcNow:yyyyMMdd_HHmmss}_{document.FileName}";

    // Delegate to infrastructure service
    var result = await _uploadServiceClient.UploadFileToPathAsync(objectPath, document);

    // Store reference in business database
    await UpdateQuotationWithDocumentAsync(quotationId, objectPath, result);

    return result.ObjectName;
}
```

### Order Service
```csharp
public async Task<string> UploadOrderFileAsync(
    string orderId,
    IFormFile file,
    string stage,
    string fileType)
{
    // Different path pattern for orders
    var objectPath = $"orders/{orderId}/{stage}/{fileType}/{DateTime.UtcNow:yyyyMMdd_HHmmss}_{file.FileName}";

    var result = await _uploadServiceClient.UploadFileToPathAsync(objectPath, file);
    return result.ObjectName;
}
```

### Customer Service
```csharp
public async Task<string> UploadCustomerDocumentAsync(
    string customerId,
    IFormFile document,
    string category)
{
    var objectPath = $"customers/{customerId}/{category}/{DateTime.UtcNow:yyyyMMdd_HHmmss}_{document.FileName}";

    var result = await _uploadServiceClient.UploadFileToPathAsync(objectPath, document);
    return result.ObjectName;
}
```

## ⚙️ Configuration

### appsettings.json
```json
{
  "StorageService": {
    "DefaultBucketName": "maliev-upload-service",
    "MaxFileSizeBytes": 104857600,
    "DefaultRetentionDays": 365,
    "EnableVersioning": false,
    "EnableEncryption": true,
    "BlockedFileExtensions": [".exe", ".scr", ".bat", ".cmd"]
  },
  "Services": {
    "UploadService": {
      "BaseUrl": "https://api.maliev.com/uploads/v1",
      "Timeout": "00:05:00",
      "RetryAttempts": 3
    }
  }
}
```

### Authentication
All API calls require JWT authentication:
```http
Authorization: Bearer {your-jwt-token}
```

## 🛠️ Development

### Prerequisites
- .NET 9.0 SDK
- Google Cloud credentials (for GCS access)
- PostgreSQL (for file metadata)

### Run Locally
```bash
# Build
dotnet build Maliev.UploadService.sln

# Run API
cd Maliev.UploadService.Api
dotnet run

# Access Swagger UI
# Automatically opens at: http://localhost:5000/uploads/swagger
```

### Database Setup
```bash
# Run migrations
dotnet ef database update --project Maliev.UploadService.Data
```

### Testing
```bash
dotnet test Maliev.UploadService.sln --verbosity normal
```

## 📂 Storage Patterns

Business services own their storage organization:

### Quotation Service Patterns
```
quotations/
├── QUO-001/
│   ├── documents/
│   │   ├── draft/
│   │   └── final/
│   └── attachments/
└── QUO-002/
```

### Order Service Patterns
```
orders/
├── ORD-001/
│   ├── design/
│   ├── production/
│   ├── quality-control/
│   └── shipping/
└── ORD-002/
```

### Customer Service Patterns
```
customers/
├── CUST-001/
│   ├── profile/
│   ├── contracts/
│   └── communications/
└── CUST-002/
```

## 🔒 Security

### Path Validation
- Invalid characters blocked
- Path traversal attacks prevented (`../` blocked)
- Maximum path length enforced (500 characters)

### File Validation
- File type restrictions by extension
- Maximum file size limits
- Content type validation

### Access Control
- JWT token required for all operations
- Service-specific access patterns
- Audit logging for all operations

## 🎯 Benefits

### ✅ Clean Architecture
- **Separation of Concerns**: Upload Service handles only storage infrastructure
- **Domain Ownership**: Business services control their storage patterns
- **Scalability**: New services don't require Upload Service changes
- **Flexibility**: Services optimize storage for their specific needs

### ✅ Business Value
- **Service Autonomy**: Teams evolve storage independently
- **Domain Expertise**: Business logic stays in domain services
- **Compliance**: Domain-specific retention policies
- **Performance**: Optimized storage patterns per business area

## 📊 Monitoring

The service provides Prometheus metrics at `/uploads/metrics` including:
- Upload success/failure rates
- Storage utilization
- Request latency
- Error rates

## 🔄 Deployment

### Docker
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY . .
EXPOSE 80
ENTRYPOINT ["dotnet", "Maliev.UploadService.Api.dll"]
```

### Kubernetes
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: maliev-upload-service
spec:
  template:
    spec:
      containers:
      - name: upload-service
        image: maliev-upload-service:latest
        ports:
        - containerPort: 80
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
```

---

**Upload Service v1.0** - Clean architecture for scalable file storage in microservices environments.