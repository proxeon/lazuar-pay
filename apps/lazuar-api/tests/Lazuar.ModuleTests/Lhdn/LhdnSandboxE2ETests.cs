using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Lhdn.Infrastructure.Gateways;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Lhdn;

/// <summary>
/// Executes a live end-to-end integration test against the LHDN Pre-Prod Sandbox.
/// Proves that the credentials, token generation, and document status polling are fully functional.
/// </summary>
[TestFixture]
[Ignore("Requires active Sandbox credentials in environment variables to run.")]
public class LhdnSandboxE2ETests
{
    private LhdnGatewayAdapter _adapter = null!;
    private string _clientId = string.Empty;
    private string _clientSecret = string.Empty;

    [SetUp]
    public void Setup()
    {
        _clientId = Environment.GetEnvironmentVariable("LHDN_SANDBOX_CLIENT_ID") ?? throw new Exception("LHDN_SANDBOX_CLIENT_ID missing");
        _clientSecret = Environment.GetEnvironmentVariable("LHDN_SANDBOX_CLIENT_SECRET") ?? throw new Exception("LHDN_SANDBOX_CLIENT_SECRET missing");

        var httpClientFactory = Substitute.For<IHttpClientFactory>();
        httpClientFactory.CreateClient(Arg.Any<string>()).Returns(new HttpClient());

        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = Substitute.For<IConfiguration>();
        config["Lhdn:BaseUrl"].Returns("https://preprod-api.myinvois.hasil.gov.my");

        _adapter = new LhdnGatewayAdapter(httpClientFactory, cache, config, NullLogger<LhdnGatewayAdapter>.Instance);
    }

    [Test]
    public async Task GetTokenAsync_ShouldReturnValidJwt_FromLhdnSandbox()
    {
        var token = await _adapter.GetTokenAsync(Guid.NewGuid(), _clientId, _clientSecret, false, null);

        token.Should().NotBeNullOrWhiteSpace();
        token.Split('.').Length.Should().Be(3); 
    }

    [Test]
    public async Task GetDocumentStatusAsync_ShouldReturnStatus_ForKnownSubmission()
    {
        var token = await _adapter.GetTokenAsync(Guid.NewGuid(), _clientId, _clientSecret, false, null);
        var knownSubmissionUid = Environment.GetEnvironmentVariable("LHDN_KNOWN_SUBMISSION_UID") ?? "mock_uid";

        var result = await _adapter.GetDocumentStatusAsync(token, knownSubmissionUid);

        result.Success.Should().BeTrue();
        result.Status.Should().NotBeNullOrWhiteSpace();
    }
}
