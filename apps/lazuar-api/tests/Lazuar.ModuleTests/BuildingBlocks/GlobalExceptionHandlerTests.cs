using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Domain;
using BuildingBlocks.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.BuildingBlocks;

[TestFixture]
public class GlobalExceptionHandlerTests
{
    [Test]
    public async Task InvalidOperation_Is_400_With_Domain_Message()
    {
        var (status, json) = await Handle(new InvalidOperationException("You are already a member of this workspace."));

        Assert.That(status, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(json.GetProperty("detail").GetString(), Is.EqualTo("You are already a member of this workspace."));
        Assert.That(json.GetProperty("code").GetString(), Is.EqualTo("invalid_operation"));
    }

    [Test]
    public async Task BusinessRule_Is_400_With_Domain_Message()
    {
        var (status, json) = await Handle(new BusinessRuleValidationException(new StubRule("Last admin cannot be removed.")));

        Assert.That(status, Is.EqualTo(StatusCodes.Status400BadRequest));
        Assert.That(json.GetProperty("detail").GetString(), Is.EqualTo("Last admin cannot be removed."));
        Assert.That(json.GetProperty("code").GetString(), Is.EqualTo("business_rule_violation"));
    }

    [Test]
    public async Task Unhandled_Exception_Is_500_Without_Provider_Text()
    {
        const string leak = "23505: duplicate key value violates unique constraint \"ux_members_org_user\"";
        var (status, json) = await Handle(new DbUpdateException(leak, new Exception(leak)));

        Assert.That(status, Is.EqualTo(StatusCodes.Status500InternalServerError));
        Assert.That(json.GetProperty("detail").GetString(), Is.EqualTo("An unexpected error occurred."));
        Assert.That(json.GetProperty("title").GetString(), Is.EqualTo("An unexpected error occurred"));
        Assert.That(json.GetProperty("code").GetString(), Is.EqualTo("internal_error"));
        Assert.That(json.ToString(), Does.Not.Contain("23505"));
        Assert.That(json.ToString(), Does.Not.Contain("ux_members"));
    }

    private static async Task<(int Status, JsonElement Body)> Handle(Exception exception)
    {
        var logger = Substitute.For<ILogger<GlobalExceptionHandler>>();
        var handler = new GlobalExceptionHandler(logger);
        var ctx = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await handler.TryHandleAsync(ctx, exception, CancellationToken.None);

        ctx.Response.Body.Position = 0;
        var json = await JsonDocument.ParseAsync(ctx.Response.Body);
        return (ctx.Response.StatusCode, json.RootElement.Clone());
    }

    private sealed class StubRule : IBusinessRule
    {
        public StubRule(string message) => Message = message;
        public string Message { get; }
        public bool IsBroken() => true;
    }
}
