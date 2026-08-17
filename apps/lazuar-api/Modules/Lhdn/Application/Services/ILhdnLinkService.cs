namespace Modules.Lhdn.Application.Services;

public interface ILhdnLinkService
{
    /// <summary>
    /// MyInvois share-portal host for the tenant's Environment (PROD vs SANDBOX).
    /// </summary>
    string GetPortalUrl(string? environment = null);
}
