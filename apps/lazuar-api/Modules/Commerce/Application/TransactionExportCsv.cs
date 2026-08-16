using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Modules.Commerce.Application;

public static class TransactionExportCsv
{
    public const int HardCap = 50_000;
    public const string Header =
        "id,created_at,status,amount,fee_amount,net_amount,currency,customer_name,customer_email,product_name,recorded_by,external_reference";

    public readonly record struct Row(
        Guid Id,
        DateTime CreatedAt,
        string Status,
        decimal Amount,
        decimal FeeAmount,
        decimal NetAmount,
        string Currency,
        string? CustomerName,
        string? CustomerEmail,
        string? ProductName,
        string? RecordedBy,
        string? ExternalReference);

    public static string Build(IEnumerable<Row> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Header);

        foreach (var t in rows)
        {
            sb.Append(Esc(t.Id.ToString())).Append(',')
              .Append(Esc(t.CreatedAt.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture))).Append(',')
              .Append(Esc(t.Status)).Append(',')
              .Append(t.Amount.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(t.FeeAmount.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(t.NetAmount.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(Esc(t.Currency)).Append(',')
              .Append(Esc(t.CustomerName)).Append(',')
              .Append(Esc(t.CustomerEmail)).Append(',')
              .Append(Esc(t.ProductName)).Append(',')
              .Append(Esc(t.RecordedBy)).Append(',')
              .Append(Esc(t.ExternalReference))
              .AppendLine();
        }

        return sb.ToString();
    }

    public static byte[] ToUtf8Bom(string csv)
    {
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
    }

    internal static string Esc(string? v)
    {
        if (string.IsNullOrEmpty(v)) return "";
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n'))
            return $"\"{v.Replace("\"", "\"\"")}\"";
        return v;
    }
}
