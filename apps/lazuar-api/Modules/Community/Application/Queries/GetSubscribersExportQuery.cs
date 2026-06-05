using System;
using BuildingBlocks.Application;

namespace Modules.Community.Application.Queries;

public record GetSubscribersExportQuery(Guid OrganizationId) : IQuery<byte[]>;
