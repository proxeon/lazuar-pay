namespace BuildingBlocks.Application;

public interface IExecutionContextAccessor
{
    Guid TenantId { get; }
    Guid UserId { get; }
    string UserRole { get; }
    bool IsSystemAdmin { get; }
    
    // Identifies whether an action was triggered by a human directly or the AI agent
    string AuditSignature { get; }
}
