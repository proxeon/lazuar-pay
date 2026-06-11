using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Modules.Community.Application.Queries;

namespace Modules.Community.Infrastructure.Queries;

public class GetSubscribersExportQueryHandler : IQueryHandler<GetSubscribersExportQuery, byte[]>
{
    private readonly ISqlConnectionFactory _connectionFactory;

    // Secure local DTO to prevent dynamic/casing binder exceptions.
    private record ExportRow(string Name, string Email, string Phone, string Plan, string Status, DateTime Joined, DateTime? NextDue, string Source, bool IsReminderOnly);

    public GetSubscribersExportQueryHandler(
        [FromKeyedServices("CommunitySqlConnectionFactory")] ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<byte[]> Handle(GetSubscribersExportQuery request, CancellationToken ct)
    {
        using var connection = _connectionFactory.CreateConnection();
        if (connection.State != ConnectionState.Open) connection.Open();

        const string sql = @"
            SELECT 
                cp.""FullName"" as Name,
                cp.""Email"" as Email,
                cp.""Phone"" as Phone,
                p.""Name"" as Plan,
                s.""Status"" as Status,
                s.""CreatedAt"" as Joined,
                s.""NextRenewalDate"" as NextDue,
                s.""Source"" as Source,
                s.""IsReminderOnly"" as IsReminderOnly
            FROM community.""Subscriptions"" s
            JOIN community.""Plans"" p ON s.""PlanId"" = p.""Id""
            JOIN crm.""ClientProfiles"" cp ON s.""ClientProfileId"" = cp.""Id""
            WHERE s.""OrganizationId"" = @OrgId AND s.""Status"" != 'PENDING'
            ORDER BY s.""CreatedAt"" DESC";

        var rows = await connection.QueryAsync<ExportRow>(sql, new { OrgId = request.OrganizationId });

        var sb = new StringBuilder();
        sb.AppendLine("Name,Email,Phone,Plan,Status,Joined,Next Due,Source,Reminder Only");

        foreach (var r in rows)
        {
            var name = (r.Name ?? "").Replace("\"", "\"\"");
            var email = r.Email ?? "";
            var phone = string.IsNullOrWhiteSpace(r.Phone) ? "" : $"=\"{r.Phone}\"";
            var plan = (r.Plan ?? "").Replace("\"", "\"\"");
            var status = r.Status ?? "";
            var joined = r.Joined.ToString("yyyy-MM-dd");
            var nextDue = r.NextDue != null ? r.NextDue.Value.ToString("yyyy-MM-dd") : "";
            var source = r.Source ?? "";
            var reminderOnly = r.IsReminderOnly ? "Yes" : "No";

            sb.AppendLine($"\"{name}\",\"{email}\",{phone},\"{plan}\",\"{status}\",\"{joined}\",\"{nextDue}\",\"{source}\",\"{reminderOnly}\"");
        }

        var preamble = Encoding.UTF8.GetPreamble();
        var content = Encoding.UTF8.GetBytes(sb.ToString());

        var result = new byte[preamble.Length + content.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(content, 0, result, preamble.Length, content.Length);

        return result;
    }
}
