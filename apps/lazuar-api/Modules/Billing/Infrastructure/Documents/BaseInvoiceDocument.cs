using System;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QRCoder;

namespace Modules.Billing.Infrastructure.Documents;

public class BaseInvoiceDocument : IDocument
{
    private readonly InvoiceDocumentModel _model;

    public BaseInvoiceDocument(InvoiceDocumentModel model)
    {
        _model = model;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    public void Compose(IDocumentContainer container)
    {
        container
            .Page(page =>
            {
                page.Margin(50);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica").FontColor(Colors.Black));

                page.Header().Element(ComposeHeader);
                page.Content().Element(ComposeContent);
                page.Footer().Element(ComposeFooter);
            });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                if (_model.CompanyLogo != null)
                {
                    column.Item().Height(50).Image(_model.CompanyLogo);
                    column.Item().PaddingTop(10);
                }
                
                column.Item().Text(_model.CompanyName).FontSize(14).SemiBold();
                if (!string.IsNullOrWhiteSpace(_model.CompanyTin))
                    column.Item().Text($"TIN: {_model.CompanyTin}").FontSize(10).FontColor(Colors.Grey.Darken2);
                if (!string.IsNullOrWhiteSpace(_model.CompanyAddress))
                    column.Item().Text(_model.CompanyAddress).FontSize(10).FontColor(Colors.Grey.Darken2);
            });

            row.ConstantItem(200).AlignRight().Column(column =>
            {
                column.Item().Text(_model.DocumentType).FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                column.Item().Text(text =>
                {
                    text.Span("No: ").SemiBold();
                    text.Span(_model.InvoiceNumber);
                });
                column.Item().Text(text =>
                {
                    text.Span("Date: ").SemiBold();
                    text.Span(_model.IssueDate.ToString("d MMM yyyy"));
                });
            });
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.PaddingVertical(30).Column(column =>
        {
            column.Item().PaddingBottom(20).Row(row =>
            {
                row.RelativeItem().Column(col =>
                {
                    col.Item().Text("Billed To:").SemiBold().FontColor(Colors.Grey.Darken2);
                    col.Item().Text(_model.CustomerName).FontSize(12).SemiBold();
                    col.Item().Text(_model.CustomerEmail);
                });
            });

            column.Item().Element(ComposeTable);

            if (!string.IsNullOrWhiteSpace(_model.Notes))
            {
                column.Item().PaddingTop(16).Text(_model.Notes).FontSize(9).FontColor(Colors.Grey.Darken2);
            }

            if (!string.IsNullOrEmpty(_model.LhdnUuid))
            {
                column.Item().PaddingTop(30).Text(text =>
                {
                    text.Span("LHDN Validation UUID: ").SemiBold();
                    text.Span(_model.LhdnUuid).FontColor(Colors.Grey.Darken2);
                });
            }
        });
    }

    private void ComposeTable(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(3);
                columns.RelativeColumn();
            });

            table.Header(header =>
            {
                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Description").SemiBold();
                header.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).AlignRight().Text("Amount").SemiBold();
            });

            foreach (var item in _model.LineItems)
            {
                table.Cell().PaddingVertical(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Text(item.Description);
                table.Cell().PaddingVertical(8).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).AlignRight().Text($"{_model.Currency} {item.Amount:F2}");
            }

            table.Cell().ColumnSpan(2).PaddingTop(15).AlignRight().Column(column =>
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem().AlignRight().Text("Subtotal:").SemiBold();
                    row.ConstantItem(80).AlignRight().Text($"{_model.Currency} {_model.Subtotal:F2}");
                });

                if (_model.Discount > 0)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().AlignRight().Text("Discount:").SemiBold();
                        row.ConstantItem(80).AlignRight().Text($"- {_model.Currency} {_model.Discount:F2}").FontColor(Colors.Red.Medium);
                    });
                }

                if (_model.Tax > 0 || _model.ShowZeroTax)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().AlignRight().Text(_model.TaxLabel).SemiBold();
                        row.ConstantItem(80).AlignRight().Text($"{_model.Currency} {_model.Tax:F2}");
                    });
                }

                column.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().AlignRight().Text("Total:").FontSize(14).SemiBold();
                    row.ConstantItem(80).AlignRight().Text($"{_model.Currency} {_model.Total:F2}").FontSize(14).SemiBold();
                });
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignBottom().Text(x =>
            {
                x.Span("Page ");
                x.CurrentPageNumber();
                x.Span(" of ");
                x.TotalPages();
            });

            if (!string.IsNullOrEmpty(_model.LhdnQrLink))
            {
                using var qrGenerator = new QRCodeGenerator();
                using var qrCodeData = qrGenerator.CreateQrCode(_model.LhdnQrLink, QRCodeGenerator.ECCLevel.M);
                using var qrCode = new PngByteQRCode(qrCodeData);
                
                var qrBytes = qrCode.GetGraphic(5);
                row.ConstantItem(80).AlignRight().Image(qrBytes);
            }
        });
    }
}
