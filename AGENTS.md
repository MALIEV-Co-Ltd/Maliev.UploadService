# Maliev.UploadService Developer Guidelines for Agents

This document provides essential instructions for AI agents working on the Maliev.UploadService repository.

## 1. Environment & Build

- **Platform:** .NET 10.0 (C#)
- **Framework:** ASP.NET Core Web API
- **Solution File:** `Maliev.UploadService.slnx` (Treat as `.sln`)

### Commands

| Action | Command |
|--------|---------|
| **Build Solution** | `dotnet build` |
| **Run All Tests** | `dotnet test` |
| **Run Specific Test** | `dotnet test --filter "FullyQualifiedName~Namespace.ClassName.MethodName"` |
| **Run Tests in File** | `dotnet test --filter "FullyQualifiedName~Namespace.ClassName"` |
| **Clean** | `dotnet clean` |
| **Restore** | `dotnet restore` |
| **Format Code** | `dotnet format` |

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

## 3. Code Style & Conventions

### General
- **Formatting:** Follow standard C# conventions (K&R braces, 4-space indentation).
- **Naming:**
  - Classes/Methods/Properties: `PascalCase`
  - Local Variables/Parameters: `camelCase`
  - Private Fields: `_camelCase` (e.g., `_storageService`)
- **Async/Await:** Use `async/await` for all I/O-bound operations. Avoid `.Result` or `.Wait()`.
- **Var:** Use `var` when the type is obvious from the right-hand side.

### API Controllers
- Use `[ApiController]` and `[Route("upload/v{version:apiVersion}/[controller]")]`.
- Inherit from `ControllerBase`.
- Use `[HttpGet]`, `[HttpPost]`, etc., with explicit routes if needed.
- Return `IActionResult` (e.g., `Ok()`, `NotFound()`, `Forbid()`).
- Use `[ProducesResponseType]` for documentation.
- **Authorization:** Use `[RequirePermission(UploadPermissions.X, RequireLiveCheck = true)]`.

### Dependency Injection
- Use constructor injection.
- Register services in `Program.cs`.
- Use scoped lifetime for services by default (`builder.Services.AddScoped<I..., ...>`).

### Database (EF Core)
- Entities in `Maliev.UploadService.Data/Entities`.
- Use `UploadDbContext`.
- Always use `await _dbContext.SaveChangesAsync(cancellationToken)`.

### Logging & Observability
- Inject `ILogger<T>`.
- Use structured logging (e.g., `_logger.LogInformation("Processing {UploadId}", uploadId)`).
- Use `LogFileEventAsync` for audit trails in controllers.

### Error Handling
- Use `try-catch` in services/controllers if specific handling is needed.
- Global exception handling is configured via middleware.
- Throw specific exceptions (e.g., `InvalidOperationException`, `ArgumentException`) which middleware maps to HTTP status codes.

## 4. Testing Guidelines

- **Framework:** xUnit
- **Mocking:** Moq
- **Integration Tests:** Use `Testcontainers` (Postgres, RabbitMQ, Redis).
- **Naming:** `MethodName_Condition_ExpectedResult` (e.g., `GetFileMetadata_WhenFileExists_ReturnsMetadata`).
- **Structure:** AAA (Arrange, Act, Assert).

### Example: Running a Single Test
```bash
dotnet test --filter "FullyQualifiedName~Maliev.UploadService.Tests.Integration.FilesControllerTests.GetFileMetadata_Success"
```

## 5. Agent Behavior Rules

- **Safety First:** verify `Directory.Build.props` or `csproj` settings before changing build configurations.
- **Pathing:** Always use **absolute paths** for file operations.
- **Verification:** ALWAYS run `dotnet build` after making changes to ensure no compilation errors.
- **Testing:** If modifying logic, run relevant tests to ensure no regression.
- **No Hallucination:** Do not invent libraries or helper methods. Check existing `Extensions/` or `Services/` first.


## Git & Version Control — Mandatory Rules

### 🚨 CRITICAL: Always Commit Code Changes (Non-Negotiable)
- **You MUST commit your changes to the local repository after completing any meaningful unit of work.**
- **Never accumulate uncommitted changes.** Do not wait until end of session or until something breaks.
- **Commit early and often** — if a change is meaningful (even a small fix or refactor), commit it.
- **You do NOT need to push to remote** — local commits are sufficient to protect against accidental loss.
- **If you are unsure whether to commit, commit anyway.** Extra commits are harmless; lost work is irreversible.
- This rule applies even if you are just "testing" or "exploring" — use git branches to isolate experimental work and commit those changes too.

### 🚨 CRITICAL: Never Use `git checkout` to Restore Broken Files
- **NEVER use `git checkout` to restore or recover files.** This operation discards uncommitted changes permanently and will result in data loss.
- **To undo/recover from broken files: first commit your current changes, then use `git revert` or `git reset --soft` to safely undo.**

## Database & EF Core — Mandatory Rules

### EF Core Design Package
- ❌ `Microsoft.EntityFrameworkCore.Design` MUST NOT be in Api projects
- ✅ It belongs ONLY in the Infrastructure (or Data) project where migrations live
- Migration commands must target Infrastructure as both project and startup-project (since EF Core Design package is in Infrastructure):
  ```
  dotnet ef migrations add <Name> --project Maliev.<Domain>Service.Infrastructure --startup-project Maliev.<Domain>Service.Infrastructure
  ```

### PostgreSQL xmin Concurrency — Mandatory Pattern
Use shadow property ONLY. Never add a Xmin/xmin property to domain entities.
```csharp
entity.Property<uint>("xmin").HasColumnType("xid").IsRowVersion();
```
- ❌ Never use `UseXminAsConcurrencyToken()` (removed in Npgsql EF v7)
- ❌ Never use entity property `public uint Xmin { get; set; }` or `public uint xmin { get; set; }`
- ❌ Never use `.Ignore(e => e.Xmin)` — remove the entity property instead
