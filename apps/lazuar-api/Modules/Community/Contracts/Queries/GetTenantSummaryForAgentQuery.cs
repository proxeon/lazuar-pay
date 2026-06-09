using System;
using BuildingBlocks.Application;

namespace Modules.Community.Contracts.Queries;

[AgentTool("Retrieves a dense, high-level summary of the organization's community metrics including MRR, active subscribers, and available plans.", "SUPER_ADMIN", "ADMIN")]
public record GetTenantSummaryForAgentQuery(Guid OrganizationId) : IQuery<string>;
