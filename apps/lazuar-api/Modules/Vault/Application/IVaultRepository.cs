using System.Threading;
using System.Threading.Tasks;
using Modules.Vault.Domain.Aggregates;

namespace Modules.Vault.Application;

public interface IVaultRepository
{
    void Add(VaultAsset asset);
    Task SaveChangesAsync(CancellationToken ct = default);
}
