namespace BuildingBlocks.Application;

public interface IEmailService
{
    /// <param name="organizationId">When provided, the provider tags the email so inbound
    /// bounce/complaint webhooks can be attributed back to the tenant.</param>
    /// <param name="tenantApiKey">The BYOK Resend API key for the specific tenant.</param>
    /// <param name="tenantSenderEmail">The verified sender email address for the specific tenant.</param>
    Task SendEmailAsync(string to, string subject, string body, Guid? organizationId = null, string? tenantApiKey = null, string? tenantSenderEmail = null);
}
