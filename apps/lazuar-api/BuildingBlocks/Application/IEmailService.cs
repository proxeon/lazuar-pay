namespace BuildingBlocks.Application;

public interface IEmailService
{
    /// <param name="organizationId">When provided, the provider tags the email so inbound
    /// bounce/complaint webhooks can be attributed back to the tenant.</param>
    Task SendEmailAsync(string to, string subject, string body, Guid? organizationId = null);
}
