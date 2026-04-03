# Maliev.UploadService Developer Guidelines for Agents

This document provides essential instructions for AI agents working on the Maliev.UploadService repository.

> **Workspace root** `B:\maliev` contains **41 independent git repos**. Each `Maliev.*` folder and `maliev-gitops` is its own repo. There is no single repo at the workspace root. Always work within the target service directory.

---

## 1. Environment & Build

- **Platform:** .NET 10.0 (C#)
- **Framework:** ASP.NET Core Web API
- **Solution File:** `Maliev.UploadService.slnx` (preferred over `.sln`)

### Commands

All commands run from within this service directory (`B:\maliev\Maliev.UploadService`).

```powershell
# Build (treats warnings as errors — all must be fixed)
dotnet build Maliev.UploadService.slnx

# Run all tests
dotnet test Maliev.UploadService.slnx --verbosity normal

# Run a single test method
dotnet test --filter "FullyQualifiedName~Namespace.ClassName.MethodName"

# Run all tests in a class
dotnet test --filter "FullyQualifiedName~Namespace.ClassName"

# Run with code coverage
dotnet test Maliev.UploadService.slnx --collect:"XPlat Code Coverage"

# Format check
dotnet format Maliev.UploadService.slnx

# Clean
dotnet clean Maliev.UploadService.slnx

# Restore
dotnet restore Maliev.UploadService.slnx
```

**Important:** `TreatWarningsAsErrors` is enabled. All warnings must be resolved for the build to pass.

## 2. Project Structure

- **`Maliev.UploadService.Api`**: Main Web API project.
  - `Controllers/`: API Endpoints (v1/).
  - `Services/`: Business logic (Storage, Auth, Validation).
  - `Data/`: Seed data.
  - `Models/`: DTOs (Requests/Responses).
- **`Maliev.UploadService.Data`**: Data Access Layer.
  - `Entities/`: EF Core entities.
  - `UploadDbContext.cs`: DB Context.
- **`Maliev.UploadService.Tests`**: Testing suite (xUnit).
  - `Unit/`: Unit tests.
  - `Integration/`: Integration tests using Testcontainers.

---

## 3. Code Style & Conventions

### C# Naming & Formatting
- **Namespaces**: File-scoped (`namespace Maliev.UploadService.Api.Controllers;`)
- **Classes/Methods/Properties**: `PascalCase`
- **Private fields**: `_camelCase` (underscore prefix)
- **Parameters/locals**: `camelCase`
- **Async methods**: Suffix with `Async` (e.g., `UploadFileAsync`)
- **Interfaces**: Prefix with `I` (e.g., `IStorageService`)
- **Permissions**: GCP-style `{domain}.{plural-resource}.{action}` as `public const string` in a `Permissions` static class
  - Valid: `upload.uploads.create`, `upload.files.delete`
  - Invalid: `upload.upload.create` (singular), `upload.create` (missing resource)
- **XML docs**: Required on ALL public methods and properties
- **Nullable**: Enabled (`<Nullable>enable</Nullable>`). Use `?` explicitly
- **Imports**: System first, then third-party, then local. Alphabetize within groups. Remove unused `using`
- **Braces**: Allman style (new line) for methods and control structures. Expression-bodied for properties/accessors
- **Indentation**: 4 spaces, LF line endings, UTF-8, trim trailing whitespace

### C# Patterns
- **DI**: Constructor injection with `private readonly` fields
- **Controllers**: `[ApiController]`, `[ApiVersion("1")]`, `[Route("upload/v{version:apiVersion}/[controller]")]`
- **Logging**: `ILogger<T>` with structured placeholders (never interpolate): `_logger.LogInformation("Processing {UploadId}", uploadId)`
- **Error handling**: Global exception middleware. Return `ProblemDetails` / `ErrorResponse` DTOs. Never expose stack traces
- **Manual mapping**: Static extension methods (`ToDto()`, `ToEntity()`). AutoMapper is banned
- **Validation**: `System.ComponentModel.DataAnnotations` on DTOs. FluentValidation is banned
- **Authorization**: Use `[RequirePermission(UploadPermissions.X, RequireLiveCheck = true)]`

### Logging & Observability
- Use `LogFileEventAsync` for audit trails in controllers.

### Database (EF Core)
- Entities in `Maliev.UploadService.Data/Entities`.
- Use `UploadDbContext`.
- Always use `await _dbContext.SaveChangesAsync(cancellationToken)`.

---

## Banned Libraries (Build Will Fail)

| Banned | Use Instead |
|--------|-------------|
| AutoMapper | Manual mapping extensions |
| FluentValidation | DataAnnotations or manual validation |
| FluentAssertions | Standard xUnit `Assert.*` |
| Swashbuckle/Swagger | Scalar (at `/upload/scalar`) |
| InMemoryDatabase (EF Core) | Testcontainers with real PostgreSQL |

---

## 4. Testing Guidelines

- **Framework**: xUnit with standard `Assert` (`Assert.Equal`, `Assert.NotNull`, etc.)
- **Mocking:** Moq
- **Naming**: `MethodName_StateUnderTest_ExpectedBehavior` or `HTTP_METHOD_Path_Scenario_ExpectedStatus`
- **Coverage**: Minimum 80% per service
- **Integration tests**: `BaseIntegrationTestFactory<TProgram, TDbContext>` with Testcontainers (PostgreSQL, Redis, RabbitMQ). Never InMemoryDatabase
- **Eventual consistency**: Use `TestHelpers.WaitForAsync`. Never `Task.Delay`
- **MassTransit consumers**: Must have consumer tests using `AddMassTransitTestHarness()`
- Use `[Fact]` for single cases, `[Theory]` for parameterized tests

### Testing Strategy (4-Tier Pyramid Context)

This service's tests cover **Tier 1 (Unit)** and **Tier 2 (Service Integration)** of the Maliev testing pyramid:

| Tier | What to Test | Infrastructure |
|------|-------------|---------------|
| **Unit** | Business logic, domain models, service methods with mocked dependencies | None (mocks only) |
| **Service Integration** | API endpoints, database persistence, permission enforcement, input validation | `BaseIntegrationTestFactory` + Testcontainers (Postgres/Redis/RabbitMQ) |

**Tier 3 (System Integration)** — cross-service workflows and event chains — is tested in `Maliev.Aspire.Tests/`.

> Full ecosystem test strategy: `Maliev.Aspire.Tests/TEST_PLAN.md`

---

## 5. Mandatory Rules

- **`TreatWarningsAsErrors = true`**: Zero warnings allowed. No suppression
- **`[RequirePermission("upload.resources.action")]`**: On all endpoints, not plain `[Authorize]`
- **API versioning**: All routes versioned (`v1/`)
- **Service prefix**: Routes prefixed with service domain (`/upload`)
- **Scalar docs**: Configured at `/upload/scalar`
- **Secrets**: Never hardcoded. Use GCP Secret Manager or environment variables
- **Async/await**: All the way down. Pass `CancellationToken`
- **EF Core Design package**: Only in Data project, never in Api
- **PostgreSQL xmin**: Shadow property only — `entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion()`. Never add entity property
- **Temporary files**: Generate in `/temp` folder, clean up afterwards

### Agent Behavior Rules

- **Safety First:** verify `Directory.Build.props` or `csproj` settings before changing build configurations.
- **Pathing:** Always use **absolute paths** for file operations.
- **Verification:** ALWAYS run `dotnet build` after making changes to ensure no compilation errors.
- **Testing:** If modifying logic, run relevant tests to ensure no regression.
- **No Hallucination:** Do not invent libraries or helper methods. Check existing `Extensions/` or `Services/` first.

---

## Git Rules

- Each `Maliev.*` folder is an independent git repo. `cd` into it before git commands
- **Commit early and often** after every meaningful unit of work. Do not accumulate changes
- **Never use `git checkout` to restore files** — commit first, then `git revert` or `git reset --soft`
- Feature branches merged to `develop` via PR. Do not push without being asked

---

## Database & EF Core — Mandatory Rules

### EF Core Design Package
- ❌ `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- ✅ It belongs ONLY in the Infrastructure (or Data) project where migrations live
- Migration commands must target Data as both project and startup-project:
  ```
  dotnet ef migrations add <Name> --project Maliev.UploadService.Data --startup-project Maliev.UploadService.Data
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- ❌ Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- ❌ Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- ❌ Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
