using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.One.Application.Queries;

public record GetPublicPricingQuery : IQuery<PublicPricingDto>;
