namespace BuildingBlocks.Application;

public static class EmailTemplateBuilder
{
    public static string WrapWithBrandHtml(string rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return string.Empty;
        }

        var formattedBody = rawBody.Replace("\n", "<br/>");

        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
</head>
<body style=""margin: 0; padding: 0; background-color: #f4f4f5; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; -webkit-font-smoothing: antialiased; line-height: 1.6;"">
    <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""background-color: #f4f4f5; padding: 40px 20px;"">
        <tr>
            <td align=""center"">
                <table border=""0"" cellpadding=""0"" cellspacing=""0"" width=""100%"" style=""max-width: 600px; background-color: #ffffff; border: 1px solid #e5e5e5; border-radius: 8px; overflow: hidden;"">
                    <tr>
                        <td style=""padding: 40px; color: #09090b; font-size: 15px;"">
                            {formattedBody}
                        </td>
                    </tr>
                    <tr>
                        <td style=""background-color: #fafafa; border-top: 1px solid #e5e5e5; padding: 20px 40px; text-align: center;"">
                            <p style=""margin: 0; font-size: 12px; color: #71717a;"">
                                Powered by <strong>Lazuar</strong>
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }
}
