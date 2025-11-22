using System.Text.Json;
using System.Text.Json.Serialization;
using Maliev.SupplierService.Data;
using Maliev.SupplierService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Maliev.SupplierService.Api.Services;

public class AuditService : IAuditService
{
    private readonly IDbContextFactory<SupplierDbContext> _contextFactory;
    private readonly ILogger<AuditService> _logger;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public AuditService(IDbContextFactory<SupplierDbContext> contextFactory, ILogger<AuditService> logger)
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task LogChangeAsync(
        Guid supplierId,
        string changeType,
        string entityType,
        Guid entityId,
        object? oldValues,
        object? newValues,
        string userId,
        string userName,
        CancellationToken cancellationToken = default)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        var auditLog = new SupplierAuditLog
        {
            Id = Guid.NewGuid(),
            SupplierId = supplierId,
            ChangeType = changeType,
            EntityType = entityType,
            EntityId = entityId,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, SerializerOptions),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, SerializerOptions),
            ChangedBy = userId,
            ChangedByName = userName,
            Timestamp = DateTime.UtcNow
        };

        context.SupplierAuditLogs.Add(auditLog);
        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Audit log created: {ChangeType} {EntityType} {EntityId} by {UserName}",
            changeType, entityType, entityId, userName);
    }
}
