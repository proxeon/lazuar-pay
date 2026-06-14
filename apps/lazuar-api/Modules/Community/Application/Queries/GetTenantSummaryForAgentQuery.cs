using System;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Queries;

[AgentTool("Retrieves a dense, high-level summary of the organization's community metrics including MRR, active subscribers, and available plans. Execute this to get an overview of the business.", "COMMUNITY", "low", "SUPER_ADMIN", "ADMIN")]
public record GetTenantSummaryForAgentQuery(Guid OrganizationId) : IQuery<string>;
