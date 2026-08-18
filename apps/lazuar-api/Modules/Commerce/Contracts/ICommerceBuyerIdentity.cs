using System;
using System.Threading;
using System.Threading.Tasks;

namespace Modules.Commerce.Contracts;

public interface ICommerceBuyerIdentity
{
    Task AttachTinAsync(
        Guid organizationId,
        string fullName,
        string email,
        string tin,
        string idType,
        string idValue,
        string companyName,
        CancellationToken ct = default);
}
