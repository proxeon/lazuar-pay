using FluentAssertions;
using Modules.Communications.Infrastructure.Security;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class SvixWebhookSignatureTests
{
    // Resend dashboard sample from Svix docs.
    private const string SampleSecret = "whsec_plJ3nmyCDGBKInavdOK15jsl";

    [Test]
    public void ResolveKey_StripsWhsecPrefixAndBase64Decodes()
    {
        var key = SvixWebhookSignature.ResolveKey(SampleSecret);
        key.Should().NotBeEmpty();
        key.Should().NotEqual(System.Text.Encoding.UTF8.GetBytes(SampleSecret));
    }

    [Test]
    public void IsValid_AcceptsSignatureForSampleWhsecSecret()
    {
        const string id = "msg_test";
        const string timestamp = "1710000000";
        const string body = "{\"type\":\"email.bounced\",\"data\":{\"to\":[\"a@b.com\"]}}";
        var signature = SvixWebhookSignature.Sign(SampleSecret, id, timestamp, body);

        SvixWebhookSignature.IsValid(SampleSecret, id, timestamp, body, $"v1={signature}")
            .Should().BeTrue();
    }

    [Test]
    public void IsValid_RejectsUtf8OfWholeSecret()
    {
        const string id = "msg_test";
        const string timestamp = "1710000000";
        const string body = "{\"type\":\"email.bounced\"}";
        var wrong = System.Convert.ToBase64String(
            System.Security.Cryptography.HMACSHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(SampleSecret),
                System.Text.Encoding.UTF8.GetBytes($"{id}.{timestamp}.{body}")));

        SvixWebhookSignature.IsValid(SampleSecret, id, timestamp, body, $"v1={wrong}")
            .Should().BeFalse();
    }
}
