using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OralExamination.Application.Common.Interfaces;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Admin.Commands.ImportUsers;
using OralExamination.Application.Features.Admin.DTOs;
using OralExamination.Application.Features.Auth.Commands.PasswordLogin;
using OralExamination.Domain.Entities;
using OralExamination.Domain.Enums;
using OralExamination.Infrastructure.Authentication;
using OralExamination.Infrastructure.Persistence;
using OralExamination.Infrastructure.Services;
using Xunit;

namespace OralExamination.UnitTests.Features.Admin;

public class AdminImportUsersTests
{
    private readonly AdminUserSpreadsheetService _spreadsheetService = new();
    private readonly Pbkdf2PasswordHasher _passwordHasher = new();

    private static OralExamDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OralExamDbContext(options);
    }

    private static MemoryStream CsvStream(params string[] lines)
    {
        var content = string.Join("\r\n", lines) + "\r\n";
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    [Theory(DisplayName = "IMPORT-01: File mẫu .xlsx và .csv tải về đọc ngược lại thành công qua ParseAsync")]
    [InlineData("xlsx")]
    [InlineData("csv")]
    public async Task GenerateTemplate_ShouldRoundTripThroughParseAsync(string format)
    {
        var queryHandler = new GetAdminUsersImportTemplateQueryHandler(_spreadsheetService);
        var templateResult = await queryHandler.Handle(new GetAdminUsersImportTemplateQuery(format), CancellationToken.None);

        templateResult.IsSuccess.Should().BeTrue();
        templateResult.Value.Content.Should().NotBeEmpty();
        templateResult.Value.FileName.Should().EndWith($".{format}");

        using var stream = new MemoryStream(templateResult.Value.Content);
        var parsed = await _spreadsheetService.ParseAsync(stream, templateResult.Value.FileName, stream.Length);

        parsed.IsSuccess.Should().BeTrue();
        parsed.Rows.Should().HaveCount(3);
        parsed.Rows[0].Email.Should().Be("thanhnqse184236@fpt.edu.vn");
        parsed.Rows[0].FullName.Should().Be("Nguyễn Quang Thành");
        parsed.Rows[0].Role.Should().Be("student");
        parsed.Rows[0].StudentCode.Should().Be("SE184236");
        parsed.Rows[1].Password.Should().Be("Fpt@2026");
        parsed.Rows[2].Username.Should().Be("khoilm");
    }

    [Fact(DisplayName = "IMPORT-02: ParseAsync chặn file sai định dạng, sai Magic Bytes, quá 2MB, thiếu cột bắt buộc và quá 500 dòng")]
    public async Task ParseAsync_ShouldRejectInvalidFilesAndBoundaries()
    {
        // Unsupported extension
        using (var txtStream = new MemoryStream(Encoding.UTF8.GetBytes("email,full_name,role\na@b.com,Test,student")))
        {
            var res = await _spreadsheetService.ParseAsync(txtStream, "users.txt", txtStream.Length);
            res.IsSuccess.Should().BeFalse();
            res.ErrorKind.Should().Be(AdminUserImportFileErrorKind.UnsupportedMediaType);
        }

        // Fake .xlsx without ZIP Magic Bytes
        using (var fakeXlsx = new MemoryStream(Encoding.UTF8.GetBytes("not a zip archive")))
        {
            var res = await _spreadsheetService.ParseAsync(fakeXlsx, "fake.xlsx", fakeXlsx.Length);
            res.IsSuccess.Should().BeFalse();
            res.ErrorKind.Should().Be(AdminUserImportFileErrorKind.UnsupportedMediaType);
        }

        // Exceeds 2MB
        using (var oversized = new MemoryStream(new byte[AdminUserImportRules.MaxFileBytes + 1]))
        {
            var res = await _spreadsheetService.ParseAsync(oversized, "big.csv", oversized.Length);
            res.IsSuccess.Should().BeFalse();
            res.ErrorKind.Should().Be(AdminUserImportFileErrorKind.PayloadTooLarge);
        }

        // Missing required columns
        using (var missingCols = CsvStream("email,student_code", "a@fpt.edu.vn,SE180001"))
        {
            var res = await _spreadsheetService.ParseAsync(missingCols, "missing.csv", missingCols.Length);
            res.IsSuccess.Should().BeFalse();
            res.ErrorKind.Should().Be(AdminUserImportFileErrorKind.InvalidFormat);
        }

        // > 500 rows
        var lines = new string[AdminUserImportRules.MaxRows + 2];
        lines[0] = "email,full_name,role,student_code";
        for (var i = 1; i <= AdminUserImportRules.MaxRows + 1; i++)
        {
            lines[i] = $"sv{i}@fpt.edu.vn,Nguyen Van A,student,SE{180000 + i}";
        }

        using (var tooMany = CsvStream(lines))
        {
            var res = await _spreadsheetService.ParseAsync(tooMany, "many.csv", tooMany.Length);
            res.IsSuccess.Should().BeFalse();
            res.ErrorKind.Should().Be(AdminUserImportFileErrorKind.TooManyRows);
        }
    }

    [Fact(DisplayName = "IMPORT-03: Preview kiểm tra quy tắc, sinh Username chuẩn FPT, chặn role admin, phát hiện trùng DB và trùng nội bộ không ghi DB")]
    public async Task Preview_ShouldEvaluateAllRules_WithoutPersistingUsers()
    {
        await using var context = CreateDbContext();
        context.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Email = "existing@fpt.edu.vn",
            FullName = "Existing User",
            Role = UserRole.Student,
            StudentCode = "SE180001",
            Username = "ExistingUserSE180001",
            IsActive = true
        });
        await context.SaveChangesAsync();

        using var csv = CsvStream(
            "Email,Họ và tên,Vai trò,MSSV,Username,Mật khẩu",
            "thanhnq@fpt.edu.vn,Nguyễn Quang Thành,student,SE184236,,",
            "huongntc@fpt.edu.vn,Nguyễn Thị Cẩm Hương,student,se181863,,Custom#2026",
            "dup.infile@fpt.edu.vn,Nguyễn Quang Thành 2,student,SE184236,,",
            "EXISTING@fpt.edu.vn,Trùng Email DB,lecturer,,lec.dup,",
            "admin.try@fpt.edu.vn,Admin Hack,admin,,,",
            "student.manual@fpt.edu.vn,Lê Minh Anh,student,SE189999,manual.user,",
            "student.oneword@fpt.edu.vn,Thành,student,SE189998,,",
            "lec.valid@fpt.edu.vn,Trần Minh Khôi,lecturer,,Khoi.LM,");

        var handler = new PreviewAdminUsersImportQueryHandler(context, _spreadsheetService);
        var result = await handler.Handle(
            new PreviewAdminUsersImportQuery(csv, "users.csv", csv.Length, DefaultPassword: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRows.Should().Be(8);
        result.Value.ValidCount.Should().Be(3);
        result.Value.DuplicateCount.Should().Be(2);
        result.Value.InvalidCount.Should().Be(3);
        result.Value.SkippedCount.Should().Be(5);

        // Check generated FPT usernames
        result.Value.Rows[0].IsValid.Should().BeTrue();
        result.Value.Rows[0].Username.Should().Be("ThanhNQSE184236");
        result.Value.Rows[0].PasswordSource.Should().Be("AUTO_GENERATED");

        result.Value.Rows[1].IsValid.Should().BeTrue();
        result.Value.Rows[1].Username.Should().Be("HuongNTCSE181863");
        result.Value.Rows[1].PasswordSource.Should().Be("ROW");

        result.Value.Rows[2].Status.Should().Be("DUPLICATE");
        result.Value.Rows[3].Status.Should().Be("DUPLICATE");
        result.Value.Rows[4].Status.Should().Be("INVALID");
        result.Value.Rows[4].ErrorMessage.Should().Contain("admin");
        result.Value.Rows[5].Status.Should().Be("INVALID");
        result.Value.Rows[6].Status.Should().Be("INVALID");

        result.Value.Rows[7].IsValid.Should().BeTrue();
        result.Value.Rows[7].Username.Should().Be("khoi.lm");

        // Ensure Preview did NOT write to DB
        (await context.Users.CountAsync()).Should().Be(1);
        (await context.AuditLogs.CountAsync()).Should().Be(0);
    }

    [Fact(DisplayName = "IMPORT-04: Commit tạo các dòng hợp lệ, bỏ qua dòng lỗi, áp dụng ưu tiên mật khẩu, trả file Excel 2 sheet, không log mật khẩu và đăng nhập không phân biệt hoa thường")]
    public async Task Commit_ShouldCreateValidUsers_ReturnTwoSheetResultWorkbook_AndSupportCaseInsensitiveLogin()
    {
        await using var context = CreateDbContext();
        var adminId = Guid.NewGuid();

        using var csv = CsvStream(
            "email,full_name,role,student_code,username,password",
            "thanhnq@fpt.edu.vn,Nguyễn Quang Thành,student,SE184236,,",
            "huongntc@fpt.edu.vn,Nguyễn Thị Cẩm Hương,student,SE181863,,RowPass#2026",
            "invalid.admin@fpt.edu.vn,Admin Blocked,admin,,,");

        var commitHandler = new CommitAdminUsersImportCommandHandler(context, _passwordHasher, _spreadsheetService);
        var result = await commitHandler.Handle(
            new CommitAdminUsersImportCommand(adminId, csv, "batch-fa26.csv", csv.Length, DefaultPassword: null),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.TotalRows.Should().Be(3);
        result.Value.CreatedCount.Should().Be(2);
        result.Value.SkippedCount.Should().Be(1);
        result.Value.ResultFileName.Should().EndWith(".xlsx");
        result.Value.ResultFileBase64.Should().NotBeNullOrWhiteSpace();

        var createdThanh = result.Value.CreatedRows.Single(r => r.Email == "thanhnq@fpt.edu.vn");
        createdThanh.Username.Should().Be("ThanhNQSE184236");
        createdThanh.PasswordSource.Should().Be("AUTO_GENERATED");
        createdThanh.TemporaryPassword.Should().HaveLength(AdminUserImportRules.GeneratedPasswordLength);
        char.IsAsciiLetter(createdThanh.TemporaryPassword[0]).Should().BeTrue();

        var createdHuong = result.Value.CreatedRows.Single(r => r.Email == "huongntc@fpt.edu.vn");
        createdHuong.Username.Should().Be("HuongNTCSE181863");
        createdHuong.PasswordSource.Should().Be("ROW");
        createdHuong.TemporaryPassword.Should().Be("RowPass#2026");

        // Verify DB entities & password hashes
        var dbUsers = await context.Users.OrderBy(u => u.Email).ToListAsync();
        dbUsers.Should().HaveCount(2);
        dbUsers.Should().OnlyContain(u => u.MustChangePassword && u.IsActive && u.AuthVersion == 1);
        _passwordHasher.Verify(createdThanh.TemporaryPassword, dbUsers.Single(u => u.Email == "thanhnq@fpt.edu.vn").PasswordHash!).Should().BeTrue();
        _passwordHasher.Verify("RowPass#2026", dbUsers.Single(u => u.Email == "huongntc@fpt.edu.vn").PasswordHash!).Should().BeTrue();

        // Verify AuditLog has ZERO plaintext passwords
        var audit = await context.AuditLogs.SingleAsync();
        audit.Action.Should().Be("USERS_BULK_IMPORTED");
        audit.UserId.Should().Be(adminId);
        audit.NewValues.Should().Contain("createdCount=2").And.Contain("skippedCount=1");
        audit.NewValues.Should().NotContain(createdThanh.TemporaryPassword);
        audit.NewValues.Should().NotContain("RowPass#2026");

        // Verify generated .xlsx result workbook contains both sheets & exact values
        var workbookBytes = Convert.FromBase64String(result.Value.ResultFileBase64);
        using (var wbStream = new MemoryStream(workbookBytes))
        using (var package = new ExcelPackage(wbStream))
        {
            package.Workbook.Worksheets.Select(s => s.Name).Should().ContainInOrder("TaiKhoanDaTao", "DongBoQua");
            var createdSheet = package.Workbook.Worksheets["TaiKhoanDaTao"]!;
            createdSheet.Cells[2, 2].Text.Should().Be("thanhnq@fpt.edu.vn");
            createdSheet.Cells[2, 6].Text.Should().Be("ThanhNQSE184236");
            createdSheet.Cells[2, 7].Text.Should().Be(createdThanh.TemporaryPassword);
            createdSheet.Cells[2, 9].Text.Should().Be("Có");

            var skippedSheet = package.Workbook.Worksheets["DongBoQua"]!;
            skippedSheet.Cells[2, 2].Text.Should().Be("invalid.admin@fpt.edu.vn");
            skippedSheet.Cells[2, 6].Text.Should().Contain("admin");
        }

        // Verify newly imported student can log in with UPPER, lower, or Mixed case username
        var loginHandler = new PasswordLoginCommandHandler(
            context,
            _passwordHasher,
            new StubAuthTokenIssuer(),
            new PasswordLoginCommandValidator());

        foreach (var variant in new[] { "ThanhNQSE184236", "thanhnqse184236", "THANHNQSE184236" })
        {
            var loginResult = await loginHandler.Handle(
                new PasswordLoginCommand(variant, createdThanh.TemporaryPassword),
                CancellationToken.None);
            loginResult.IsSuccess.Should().BeTrue($"Login should succeed for username variant '{variant}'");
            loginResult.Value.MustChangePassword.Should().BeTrue();
        }
    }

    [Fact(DisplayName = "IMPORT-05: Commit áp dụng defaultPassword cho dòng trống cột password và từ chối khi không có dòng hợp lệ hoặc defaultPassword < 8 ký tự")]
    public async Task Commit_ShouldApplyDefaultPassword_AndRejectZeroValidOrShortDefaultPassword()
    {
        await using var context = CreateDbContext();
        var handler = new CommitAdminUsersImportCommandHandler(context, _passwordHasher, _spreadsheetService);

        using (var csv = CsvStream("email,full_name,role,student_code,username,password", "an@fpt.edu.vn,Nguyễn An,student,SE180001,,"))
        {
            var shortDefault = await handler.Handle(
                new CommitAdminUsersImportCommand(Guid.NewGuid(), csv, "users.csv", csv.Length, DefaultPassword: "short"),
                CancellationToken.None);
            shortDefault.IsSuccess.Should().BeFalse();
            shortDefault.Error.Should().StartWith("VALIDATION:");
        }

        using (var csv = CsvStream("email,full_name,role,student_code,username,password", "bad-email,Nguyễn An,student,SE180001,,"))
        {
            var noValid = await handler.Handle(
                new CommitAdminUsersImportCommand(Guid.NewGuid(), csv, "users.csv", csv.Length, DefaultPassword: "Default#2026"),
                CancellationToken.None);
            noValid.IsSuccess.Should().BeFalse();
            noValid.Error.Should().StartWith("UNPROCESSABLE:");
        }

        using (var csv = CsvStream("email,full_name,role,student_code,username,password", "an@fpt.edu.vn,Nguyễn An,student,SE180001,,"))
        {
            var ok = await handler.Handle(
                new CommitAdminUsersImportCommand(Guid.NewGuid(), csv, "users.csv", csv.Length, DefaultPassword: "Default#2026"),
                CancellationToken.None);
            ok.IsSuccess.Should().BeTrue();
            ok.Value.CreatedRows.Single().Username.Should().Be("AnNSE180001");
            ok.Value.CreatedRows.Single().PasswordSource.Should().Be("DEFAULT");
            ok.Value.CreatedRows.Single().TemporaryPassword.Should().Be("Default#2026");
        }
    }

    [Fact(DisplayName = "IMPORT-06: GenerateResultWorkbook trung hòa Excel Formula Injection (=, +, -, @) trên mọi cột văn bản")]
    public void GenerateResultWorkbook_ShouldNeutralizeFormulaInjection()
    {
        var created = new[]
        {
            new AdminUserImportCreatedRowDto
            {
                RowNumber = 2,
                UserId = Guid.NewGuid(),
                Email = "safe@fpt.edu.vn",
                FullName = "=CMD|' /C calc'!A0",
                Role = UserRole.Lecturer,
                Username = "lec.test",
                TemporaryPassword = "+FormulaPass123",
                PasswordSource = "ROW",
                MustChangePassword = true
            }
        };
        var skipped = new[]
        {
            new AdminUserImportSkippedRowDto
            {
                RowNumber = 3,
                Email = "@evil@fpt.edu.vn",
                FullName = "-2+3",
                Role = UserRole.Student,
                StudentCode = "SE180001",
                Reason = "=HYPERLINK(\"http://evil\")"
            }
        };

        var file = _spreadsheetService.GenerateResultWorkbook(created, skipped, DateTime.UtcNow);
        using var stream = new MemoryStream(file.Content);
        using var package = new ExcelPackage(stream);

        var createdSheet = package.Workbook.Worksheets["TaiKhoanDaTao"]!;
        createdSheet.Cells[2, 3].Text.Should().StartWith("'=");
        createdSheet.Cells[2, 7].Text.Should().StartWith("'+");

        var skippedSheet = package.Workbook.Worksheets["DongBoQua"]!;
        skippedSheet.Cells[2, 2].Text.Should().StartWith("'@");
        skippedSheet.Cells[2, 3].Text.Should().StartWith("'-");
        skippedSheet.Cells[2, 6].Text.Should().StartWith("'=");
    }

    [Fact(DisplayName = "IMPORT-07: ParseXlsx chặn Zip-Bomb (>20MB giải nén), quá 100 entry và xử lý an toàn Sparse Dimension Bomb (ô ở cột xa XFD / dòng xa)")]
    public async Task ParseXlsx_ShouldBlockZipBombs_AndHandleSparseHighCoordinatesSafely()
    {
        // 1. Zip-bomb: highly compressible 21MB zeroed XML entry inside a tiny < 50KB zip
        using (var zipBombStream = new MemoryStream())
        {
            using (var archive = new ZipArchive(zipBombStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                var entry = archive.CreateEntry("xl/worksheets/sheet1.xml", System.IO.Compression.CompressionLevel.SmallestSize);
                using var writer = entry.Open();
                var chunk = new byte[1024 * 1024];
                for (var i = 0; i < 21; i++)
                {
                    await writer.WriteAsync(chunk);
                }
            }

            zipBombStream.Position = 0;
            zipBombStream.Length.Should().BeLessThan(AdminUserImportRules.MaxFileBytes);
            var bombRes = await _spreadsheetService.ParseAsync(zipBombStream, "bomb.xlsx", zipBombStream.Length);
            bombRes.IsSuccess.Should().BeFalse();
            bombRes.ErrorKind.Should().Be(AdminUserImportFileErrorKind.PayloadTooLarge);
        }

        // 2. Too many zip entries (> 100)
        using (var manyEntriesStream = new MemoryStream())
        {
            using (var archive = new ZipArchive(manyEntriesStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                for (var i = 0; i < 105; i++)
                {
                    var entry = archive.CreateEntry($"entry-{i}.xml");
                    using var sw = new StreamWriter(entry.Open());
                    await sw.WriteAsync("x");
                }
            }

            manyEntriesStream.Position = 0;
            var entriesRes = await _spreadsheetService.ParseAsync(manyEntriesStream, "many-entries.xlsx", manyEntriesStream.Length);
            entriesRes.IsSuccess.Should().BeFalse();
            entriesRes.ErrorKind.Should().Be(AdminUserImportFileErrorKind.InvalidFormat);
        }

        // 3. Real .xlsx with valid rows plus a stray cell far out at column 10000 & row 100000 (sparse dimension bomb)
        ExcelPackage.License.SetNonCommercialOrganization("FPT University Oral Examination Project");
        byte[] sparseBytes;
        using (var pkg = new ExcelPackage())
        {
            var sheet = pkg.Workbook.Worksheets.Add("Users");
            sheet.Cells[1, 1].Value = "email";
            sheet.Cells[1, 2].Value = "full_name";
            sheet.Cells[1, 3].Value = "role";
            sheet.Cells[1, 4].Value = "student_code";
            sheet.Cells[2, 1].Value = "sparse@fpt.edu.vn";
            sheet.Cells[2, 2].Value = "Nguyễn Quang Thành";
            sheet.Cells[2, 3].Value = "student";
            sheet.Cells[2, 4].Value = "SE184236";
            sheet.Cells[100000, 10000].Value = "stray";
            sparseBytes = pkg.GetAsByteArray();
        }

        using var sparseStream = new MemoryStream(sparseBytes);
        var sparseRes = await _spreadsheetService.ParseAsync(sparseStream, "sparse.xlsx", sparseStream.Length);
        sparseRes.IsSuccess.Should().BeTrue();
        sparseRes.Rows.Should().ContainSingle(r => r.Email == "sparse@fpt.edu.vn" && r.StudentCode == "SE184236");
    }

    [Fact(DisplayName = "IMPORT-08: Commit qua tệp .xlsx thật từ chối mật khẩu bắt đầu bằng ký tự công thức (=,+,-,@), MSSV bẩn (SE-184236) và xử lý concurrency PostgreSQL 23505 -> CONFLICT")]
    public async Task Commit_WithRealXlsx_ShouldRejectFormulaPasswordsDirtyStudentCodes_AndMap23505ToConflict()
    {
        ExcelPackage.License.SetNonCommercialOrganization("FPT University Oral Examination Project");
        byte[] xlsxBytes;
        using (var pkg = new ExcelPackage())
        {
            var sheet = pkg.Workbook.Worksheets.Add("TaiKhoan");
            sheet.Cells[1, 1].Value = "email";
            sheet.Cells[1, 2].Value = "full_name";
            sheet.Cells[1, 3].Value = "role";
            sheet.Cells[1, 4].Value = "student_code";
            sheet.Cells[1, 5].Value = "username";
            sheet.Cells[1, 6].Value = "password";

            sheet.Cells[2, 1].Value = "ok@fpt.edu.vn";
            sheet.Cells[2, 2].Value = "Nguyễn Quang Thành";
            sheet.Cells[2, 3].Value = "student";
            sheet.Cells[2, 4].Value = "SE184236";

            sheet.Cells[3, 1].Value = "dirtycode@fpt.edu.vn";
            sheet.Cells[3, 2].Value = "Nguyễn Văn B";
            sheet.Cells[3, 3].Value = "student";
            sheet.Cells[3, 4].Value = "SE-184236";

            sheet.Cells[4, 1].Value = "formulapwd@fpt.edu.vn";
            sheet.Cells[4, 2].Value = "Lê Minh Khôi";
            sheet.Cells[4, 3].Value = "lecturer";
            sheet.Cells[4, 6].Value = "@Fpt2026!";

            xlsxBytes = pkg.GetAsByteArray();
        }

        // Default password starting with formula trigger char is rejected
        await using (var context = CreateDbContext())
        {
            using var stream = new MemoryStream(xlsxBytes);
            var handler = new CommitAdminUsersImportCommandHandler(context, _passwordHasher, _spreadsheetService);
            var badDefault = await handler.Handle(
                new CommitAdminUsersImportCommand(Guid.NewGuid(), stream, "users.xlsx", stream.Length, DefaultPassword: "=BadPass123"),
                CancellationToken.None);
            badDefault.IsSuccess.Should().BeFalse();
            badDefault.Error.Should().StartWith("VALIDATION:");
        }

        // Real .xlsx commit: row 2 succeeds, row 3 (dirty MSSV) and row 4 (formula password) are skipped
        await using (var context = CreateDbContext())
        {
            using var stream = new MemoryStream(xlsxBytes);
            var handler = new CommitAdminUsersImportCommandHandler(context, _passwordHasher, _spreadsheetService);
            var ok = await handler.Handle(
                new CommitAdminUsersImportCommand(Guid.NewGuid(), stream, "users.xlsx", stream.Length, DefaultPassword: null),
                CancellationToken.None);
            ok.IsSuccess.Should().BeTrue();
            ok.Value.CreatedCount.Should().Be(1);
            ok.Value.SkippedCount.Should().Be(2);
            ok.Value.CreatedRows.Single().Username.Should().Be("ThanhNQSE184236");
            ok.Value.SkippedRows.Should().Contain(r => r.Email == "dirtycode@fpt.edu.vn");
            ok.Value.SkippedRows.Should().Contain(r => r.Email == "formulapwd@fpt.edu.vn");
        }

        // Concurrent PostgreSQL 23505 -> CONFLICT
        var conflictOptions = new DbContextOptionsBuilder<OralExamDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using (var conflictContext = new Conflict23505DbContext(conflictOptions))
        {
            conflictContext.Throw23505OnNextSave = true;
            using var stream = new MemoryStream(xlsxBytes);
            var handler = new CommitAdminUsersImportCommandHandler(conflictContext, _passwordHasher, _spreadsheetService);
            var conflict = await handler.Handle(
                new CommitAdminUsersImportCommand(Guid.NewGuid(), stream, "users.xlsx", stream.Length, DefaultPassword: null),
                CancellationToken.None);
            conflict.IsSuccess.Should().BeFalse();
            conflict.Error.Should().StartWith("CONFLICT:");
        }
    }

    private sealed class Conflict23505DbContext(DbContextOptions<OralExamDbContext> options) : OralExamDbContext(options)
    {
        public bool Throw23505OnNextSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (Throw23505OnNextSave)
            {
                Throw23505OnNextSave = false;
                throw new DbUpdateException(
                    "duplicate key value violates unique constraint",
                    new FakeSqlStateException("23505"));
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class FakeSqlStateException(string sqlState) : Exception($"Simulated Postgres {sqlState}")
    {
        public string SqlState { get; } = sqlState;
    }

    private sealed class StubAuthTokenIssuer : IAccessTokenIssuer
    {
        public IssuedAccessToken Issue(
            Guid userId,
            string email,
            string fullName,
            string role,
            bool mustChangePassword = false,
            int authVersion = 1) =>
            new("stub-access-token", 900);
    }
}
