namespace BuildingBlocks.Application;

public interface IExecutionContextAccessor
{
    Guid TenantId { get; }
    Guid UserId { get; }
    string UserRole { get; }
}
