using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Lazuar.ApiTypes;

namespace Modules.Commerce.Application.Queries;

public interface ICommerceQueryService
{
    Task<IEnumerable<ProductDto>> GetProductsAsync(Guid organizationId);
    Task<ProductDto?> GetProductByIdAsync(Guid organizationId, Guid productId);
}
