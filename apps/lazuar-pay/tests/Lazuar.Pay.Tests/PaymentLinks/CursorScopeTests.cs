using System.Net;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

/// <summary>
/// Issue 015 (issues/001): the ?after= cursor row used to resolve by primary key across all
/// orgs, so a foreign org's id produced a different page boundary than an unknown id — a
/// cross-org existence + timestamp oracle. A foreign cursor must now behave exactly like a
/// bogus one (start of list).
/// </summary>
public class CursorScopeTests
{
    [Test]
    public async Task Foreign_org_cursor_id_is_treated_as_unknown()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
            {
                return FakeOneHandler.Json(HttpStatusCode.OK,
                    """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"owner","status":"active"},{"id":"t2","role":"owner","status":"active"}]}""");
            }

            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        };
        var client = factory.CreateClient();

        string linkId = "";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            var baseTime = DateTimeOffset.UtcNow;
            // Org t1: two links, one newer, one older than the foreign cursor row.
            db.PaymentLinks.AddRange(
                new PaymentLinkRow
                {
                    Id = "lk_t1_new", OrgId = "t1", PublicToken = "tok_new", Provider = "test",
                    Amount = 10m, Currency = "MYR", MaxPayers = 1, CreatedAt = baseTime
                },
                new PaymentLinkRow
                {
                    Id = "lk_t1_old", OrgId = "t1", PublicToken = "tok_old", Provider = "test",
                    Amount = 10m, Currency = "MYR", MaxPayers = 1, CreatedAt = baseTime.AddMinutes(-5)
                },
                // Org t2: the foreign row sitting strictly between t1's two rows.
                new PaymentLinkRow
                {
                    Id = "lk_t2_mid", OrgId = "t2", PublicToken = "tok_mid", Provider = "test",
                    Amount = 10m, Currency = "MYR", MaxPayers = 1, CreatedAt = baseTime.AddMinutes(-2)
                });
            await db.SaveChangesAsync();
            linkId = "lk_t2_mid";
        }

        using var foreign = new HttpRequestMessage(HttpMethod.Get, $"/v1/orgs/t1/payment-links?after={linkId}");
        foreign.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var foreignResponse = await client.SendAsync(foreign);
        Assert.That(foreignResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var foreignDoc = JsonDocument.Parse(await foreignResponse.Content.ReadAsStringAsync());

        using var bogus = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/payment-links?after=does-not-exist");
        bogus.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var bogusResponse = await client.SendAsync(bogus);
        using var bogusDoc = JsonDocument.Parse(await bogusResponse.Content.ReadAsStringAsync());

        Assert.That(foreignDoc.RootElement.GetRawText(), Is.EqualTo(bogusDoc.RootElement.GetRawText()),
            "a foreign org's cursor must behave exactly like an unknown one: page 1, both rows");
        Assert.That(PayTest.Items(foreignDoc.RootElement).GetArrayLength(), Is.EqualTo(2),
            "the foreign row's timestamp must not truncate t1's list");
    }
}
