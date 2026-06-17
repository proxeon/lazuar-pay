namespace BuildingBlocks.Application;

public interface IExecutionContextAccessor
{
    Guid TenantId { get; }
    Guid UserId { get; }
    string UserRole { get; }
    bool IsSystemAdmin { get; }
    
    /// <summary>
    /// Indicates if the current execution context was authenticated using a sandbox API Key (sk_test_).
    /// </summary>
    bool IsTestMode { get; }
    
    string AuditSignature { get; }
}
