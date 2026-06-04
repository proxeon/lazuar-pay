using System;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;

namespace Modules.Payments.Application.Queries;

public record GetPaymentConfigQuery(Guid OrganizationId) : IQuery<PaymentConfigDto?>;
