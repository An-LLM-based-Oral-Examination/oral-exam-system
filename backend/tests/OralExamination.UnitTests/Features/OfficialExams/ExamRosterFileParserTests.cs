using System.Text;
using FluentAssertions;
using OfficeOpenXml;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Features.OfficialExams;

public sealed class ExamRosterFileParserTests
{
    [Fact]
    public async Task ParseCsv_MapsRequiredHeadersAndQuotedCells()
    {
        var csv = "studentCode,seatNumber,ipAddress\n\"SE170001\",1,10.0.0.1\n";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var result = await new ExamRosterFileParser().ParseAsync(stream, "roster.csv");

        result.Errors.Should().BeEmpty();
        result.Rows.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new { SourceRow = 2, StudentCode = "SE170001", SeatNumber = 1, IpAddress = "10.0.0.1" });
    }

    [Fact]
    public async Task ParseXlsx_MapsRequiredHeaders()
    {
        ExcelPackage.License.SetNonCommercialOrganization("FPT University Oral Examination Project");
        await using var stream = new MemoryStream();
        using (var package = new ExcelPackage())
        {
            var sheet = package.Workbook.Worksheets.Add("Roster");
            sheet.Cells[1, 1].Value = "studentCode";
            sheet.Cells[1, 2].Value = "seatNumber";
            sheet.Cells[1, 3].Value = "ipAddress";
            sheet.Cells[2, 1].Value = "SE170002";
            sheet.Cells[2, 2].Value = 2;
            sheet.Cells[2, 3].Value = "10.0.0.2";
            await package.SaveAsAsync(stream);
        }
        stream.Position = 0;

        var result = await new ExamRosterFileParser().ParseAsync(stream, "roster.xlsx");

        result.Errors.Should().BeEmpty();
        result.Rows.Should().ContainSingle().Which.StudentCode.Should().Be("SE170002");
    }
}
