using System.Linq;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Messaging.Infrastructure;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Messaging;

[TestFixture]
public class MessagingEndpointsAuthorizationTests
{
    [Test]
    public void MapMessagingEndpoints_Notify_Requires_OrgAdmin()
    {
        var builder = WebApplication.CreateBuilder();
        // MapPost handler DI-resolves IMediator when endpoint metadata is materialized.
        builder.Services.AddSingleton(Substitute.For<IMediator>());

        var app = builder.Build();
        app.MapMessagingEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        var notify = endpoints.SingleOrDefault(e =>
            e.RoutePattern.RawText is { } raw &&
            raw.Contains("notify", System.StringComparison.OrdinalIgnoreCase));

        Assert.That(notify, Is.Not.Null,
            $"Expected POST /messaging/notify route. Found: {string.Join(", ", endpoints.Select(e => e.RoutePattern.RawText))}");

        var authorizeData = notify!.Metadata.GetOrderedMetadata<IAuthorizeData>();
        Assert.That(authorizeData, Is.Not.Empty,
            "POST /messaging/notify must not be anonymously callable.");
        Assert.That(authorizeData.Any(a => a.Policy == "OrgAdmin"), Is.True,
            "POST /messaging/notify must require OrgAdmin policy.");
    }
}
