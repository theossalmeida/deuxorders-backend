using DeuxERP.Domain.Sales;
using DeuxERP.Domain.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace DeuxERP.API.Services
{
    public class ExportService
    {
        public byte[] GenerateCsv(IEnumerable<OrderExportRow> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("OrderId,Cliente,Data de Entrega,Status,Produto,Quantidade,Valor Unitário Pago,Total Pago");

            foreach (var row in rows)
            {
                sb.AppendLine(string.Join(",",
                    row.OrderId,
                    Escape(row.ClientName),
                    row.DeliveryDate.ToString("dd/MM/yyyy"),
                    StatusToString(row.Status),
                    Escape(row.ProductName),
                    row.Quantity,
                    FormatCents(row.PaidUnitPrice),
                    FormatCents(row.TotalPaid)
                ));
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        public async Task WriteCsvAsync(IAsyncEnumerable<OrderExportRow> rows, Stream output, CancellationToken ct = default)
        {
            await using var writer = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), leaveOpen: true);
            await writer.WriteLineAsync("OrderId,Cliente,Data de Entrega,Status,Produto,Quantidade,Valor Unitário Pago,Total Pago");

            await foreach (var row in rows.WithCancellation(ct))
            {
                await writer.WriteLineAsync(string.Join(",",
                    row.OrderId,
                    Escape(row.ClientName),
                    row.DeliveryDate.ToString("dd/MM/yyyy"),
                    StatusToString(row.Status),
                    Escape(row.ProductName),
                    row.Quantity,
                    FormatCents(row.PaidUnitPrice),
                    FormatCents(row.TotalPaid)
                ));
            }

            await writer.FlushAsync();
        }

        public byte[] GeneratePdf(IEnumerable<OrderExportRow> rows)
        {
            var rowList = rows.ToList();
            var headers = new[] { "Order ID", "Cliente", "Entrega", "Status", "Produto", "Qtd", "Vlr Unit.", "Total" };

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontSize(9));

                    page.Header()
                        .PaddingBottom(8)
                        .Text("Relatório de Pedidos")
                        .SemiBold().FontSize(14);

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(1);
                            cols.RelativeColumn(2);
                            cols.RelativeColumn(2);
                        });

                        table.Header(header =>
                        {
                            foreach (var col in headers)
                                header.Cell()
                                    .Background("#2d2d2d")
                                    .Padding(4)
                                    .Text(col)
                                    .FontColor("#ffffff")
                                    .SemiBold();
                        });

                        for (int i = 0; i < rowList.Count; i++)
                        {
                            var row = rowList[i];
                            var bg = i % 2 == 0 ? "#ffffff" : "#f5f5f5";

                            table.Cell().Background(bg).Padding(4).Text(row.OrderId.ToString()[..8] + "…");
                            table.Cell().Background(bg).Padding(4).Text(row.ClientName);
                            table.Cell().Background(bg).Padding(4).Text(row.DeliveryDate.ToString("dd/MM/yyyy"));
                            table.Cell().Background(bg).Padding(4).Text(StatusToString(row.Status));
                            table.Cell().Background(bg).Padding(4).Text(row.ProductName);
                            table.Cell().Background(bg).Padding(4).Text(row.Quantity.ToString());
                            table.Cell().Background(bg).Padding(4).Text(FormatCents(row.PaidUnitPrice));
                            table.Cell().Background(bg).Padding(4).Text(FormatCents(row.TotalPaid));
                        }
                    });

                    page.Footer().AlignRight().Text(x =>
                    {
                        x.Span("Página ");
                        x.CurrentPageNumber();
                        x.Span(" de ");
                        x.TotalPages();
                    });
                });
            }).GeneratePdf();
        }

        private static string FormatCents(long cents) => (cents / 100m).ToString("F2");

        private static string StatusToString(OrderStatus status) => status switch
        {
            OrderStatus.Pending => "Pendente",
            OrderStatus.Completed => "Concluído",
            OrderStatus.Canceled => "Cancelado",
            _ => status.ToString()
        };

        private static string Escape(string value)
        {
            var neutralized = NeutralizeFormula(value);
            return neutralized.Contains(',') || neutralized.Contains('"') || neutralized.Contains('\n') || neutralized.Contains('\r')
                ? $"\"{neutralized.Replace("\"", "\"\"")}\""
                : neutralized;
        }

        private static string NeutralizeFormula(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            return value[0] is '=' or '+' or '-' or '@' or '\t' or '\r'
                ? $"'{value}"
                : value;
        }
    }
}
