using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Tenant.Api.Data;
using Tenant.Api.Models;

namespace Tenant.Api.Services
{
    public class ReceiptService : IReceiptService
    {
        private readonly AppDbContext _context;
        private readonly ICurrentUserService _currentUserService;

        public ReceiptService(AppDbContext context, ICurrentUserService currentUserService)
        {
            _context = context;
            _currentUserService = currentUserService;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<(byte[] PdfBytes, string FileName)> GenerateReceiptAsync(Guid recordId, string? publicId = null)
        {
            // 1. Fetch record and entry
            var query = _context.Records
                .Include(r => r.Entry)
                .Where(r => r.PublicId == recordId);

            if (!string.IsNullOrEmpty(publicId))
            {
                // Access via shared link
                if (Guid.TryParse(publicId, out Guid parsedId))
                {
                    query = query.Where(r => r.Entry!.PublicId == parsedId);
                }
                else
                {
                    throw new UnauthorizedAccessException("Invalid public ID");
                }
            }
            else
            {
                // Authenticated access
                var userId = await _currentUserService.GetCurrentUserIdAsync();
                if (userId == null) throw new UnauthorizedAccessException();
                query = query.Where(r => r.Entry!.UserId == userId.Value);
            }

            var record = await query.FirstOrDefaultAsync();
            if (record == null)
            {
                throw new Exception("Record not found or access denied");
            }

            // 2. Generate Receipt Number if missing
            if (string.IsNullOrEmpty(record.ReceiptNumber))
            {
                record.ReceiptNumber = $"REC-{record.Id:D6}-{DateTime.UtcNow:yyMMdd}";
                await _context.SaveChangesAsync();
            }

            var entry = record.Entry!;

            // 3. Generate PDF using QuestPDF
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Element(compose => ComposeHeader(compose, record, entry));
                    page.Content().Element(compose => ComposeContent(compose, record, entry));
                    page.Footer().Element(ComposeFooter);
                });
            });

            var pdfBytes = document.GeneratePdf();
            
            // 4. Create Filename
            var sanitizedTenantName = string.Join("_", entry.Name.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"Receipt_{record.RentPeriod:MMMMyyyy}_{sanitizedTenantName}.pdf";

            return (pdfBytes, fileName);
        }

        private void ComposeHeader(IContainer container, Record record, Entry entry)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("RENT RECEIPT").FontSize(24).SemiBold().FontColor(Colors.Blue.Darken2);
                    column.Item().PaddingTop(5).Text(text =>
                    {
                        text.Span("Receipt No: ").SemiBold();
                        text.Span(record.ReceiptNumber);
                    });
                    column.Item().Text(text =>
                    {
                        text.Span("Date: ").SemiBold();
                        text.Span(DateTime.UtcNow.ToString("dd MMM yyyy"));
                    });
                });
            });
        }

        private void ComposeContent(IContainer container, Record record, Entry entry)
        {
            container.PaddingVertical(1, Unit.Centimetre).Column(column =>
            {
                column.Spacing(20);

                // Parties section
                column.Item().Row(row =>
                {
                    row.RelativeItem().Component(new AddressComponent("Landlord Details", "Owner", "Contact the owner for queries"));
                    row.ConstantItem(50);
                    row.RelativeItem().Component(new AddressComponent("Tenant Details", entry.Name, entry.Address ?? "N/A", MaskAadhaar(entry.AadhaarNumber)));
                });

                // Property Details
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Property Details").SemiBold().FontSize(14).FontColor(Colors.Grey.Darken3);
                column.Item().PaddingBottom(10).Text(entry.PropertyName ?? "N/A");

                // Payment Details
                column.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(5).Text("Payment Summary").SemiBold().FontSize(14).FontColor(Colors.Grey.Darken3);
                
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Rent Period").SemiBold();
                        header.Cell().Text("Received Date").SemiBold();
                        header.Cell().Text("Payment Method").SemiBold();
                        header.Cell().AlignRight().Text("Amount").SemiBold();
                    });

                    table.Cell().Text(record.RentPeriod.ToString("MMMM yyyy"));
                    table.Cell().Text(record.ReceivedDate.ToString("dd MMM yyyy"));
                    table.Cell().Text("Online/Cash"); 
                    table.Cell().AlignRight().Text($"Rs. {record.Amount:F2}").SemiBold();
                });

                // Total
                column.Item().AlignRight().PaddingTop(10).Text($"Total Paid: Rs. {record.Amount:F2}").FontSize(14).SemiBold();

                // Signature
                column.Item().PaddingTop(40).Row(row =>
                {
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(5).Text("Landlord Signature").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                    row.ConstantItem(100);
                    row.RelativeItem().Column(col =>
                    {
                        col.Item().LineHorizontal(1).LineColor(Colors.Grey.Darken1);
                        col.Item().PaddingTop(5).Text("Tenant Signature").FontSize(10).FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text("This is a system generated receipt and does not require a physical signature.").FontSize(9).FontColor(Colors.Grey.Medium);
        }

        private static string MaskAadhaar(string? aadhaar)
        {
            if (string.IsNullOrWhiteSpace(aadhaar) || aadhaar.Length < 4)
                return "Not Provided";

            var trimmed = aadhaar.Trim();
            if (trimmed.Length <= 4)
            {
                return new string('*', trimmed.Length);
            }
            
            var firstTwo = trimmed.Substring(0, 2);
            var lastTwo = trimmed.Substring(trimmed.Length - 2);
            var middleChars = trimmed.Length - 4;
            
            // Format like: 12**** **** **90
            var maskedMiddle = new string('*', middleChars);
            var middleWithSpaces = InsertSpaces(maskedMiddle, 4);

            return $"{firstTwo}{middleWithSpaces}{lastTwo}".Trim();
        }

        private static string InsertSpaces(string text, int groupSize)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var result = "";
            for (var i = 0; i < text.Length; i++)
            {
                if (i > 0 && i % groupSize == 0)
                    result += " ";
                result += text[i];
            }
            return result;
        }

        public class AddressComponent : IComponent
        {
            private string Title { get; }
            private string Name { get; }
            private string Address { get; }
            private string? Aadhaar { get; }

            public AddressComponent(string title, string name, string address, string? aadhaar = null)
            {
                Title = title;
                Name = name;
                Address = address;
                Aadhaar = aadhaar;
            }

            public void Compose(IContainer container)
            {
                container.Column(column =>
                {
                    column.Spacing(2);
                    column.Item().PaddingBottom(5).Text(Title).SemiBold().FontSize(14).FontColor(Colors.Grey.Darken3);
                    column.Item().Text(Name).SemiBold();
                    column.Item().Text(Address);
                    if (!string.IsNullOrEmpty(Aadhaar))
                    {
                        column.Item().Text($"Aadhaar: {Aadhaar}");
                    }
                });
            }
        }
    }
}
