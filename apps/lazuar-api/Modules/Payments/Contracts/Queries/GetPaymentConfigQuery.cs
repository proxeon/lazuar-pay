using System;
using System.Collections.Generic;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Payments.Contracts.Queries;

public record GetPaymentConfigQuery(Guid OrganizationId) : IQuery<IEnumerable<PaymentConfigDto>>;
