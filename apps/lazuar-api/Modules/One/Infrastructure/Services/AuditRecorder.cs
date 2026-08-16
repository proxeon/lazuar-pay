using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Modules.One.Contracts;
using Modules.One.Domain;

namespace Modules.One.Infrastructure.Services;

public sealed class AuditRecorder : IAuditRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly OneDbContext _db;
    private readonly IExecutionContextAccessor _ctx;
    private readonly ILogger<AuditRecorder> _logger;

    public AuditRecorder(
        OneDbContext db,
        IExecutionContextAccessor ctx,
        ILogger<AuditRecorder> logger)
    {
        _db = db;
        _ctx = ctx;
        _logger = logger;
    }

    public async Task RecordAsync(
        Guid organizationId,
        string action,
        string entityType,
        string entityId,
        object? metadata = null,
        Guid? actorUserId = null,
        string? actorEmail = null,
        CancellationToken ct = default)
    {
        try
        {
            var userId = actorUserId ?? (_ctx.UserId == Guid.Empty ? null : _ctx.UserId);
            var email = actorEmail;
            if (string.IsNullOrWhiteSpace(email) && userId.HasValue)
            {
                email = await _db.GlobalUsers
                    .AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(u => u.Id == userId.Value)
                    .Select(u => u.Email)
                    .FirstOrDefaultAsync(ct);
            }

            string? metadataJson = null;
            if (metadata != null)
            {
                metadataJson = metadata is string s
                    ? s
                    : JsonSerializer.Serialize(metadata, JsonOptions);
            }

            _db.AuditEvents.Add(new AuditEvent(
                organizationId,
                action,
                entityType,
                entityId,
                userId,
                email,
                metadataJson));
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to record audit action {Action} for org {OrgId} entity {EntityType}/{EntityId}.",
                action, organizationId, entityType, entityId);
        }
    }
}
