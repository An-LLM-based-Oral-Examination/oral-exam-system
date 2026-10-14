using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace OralExamination.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SeedSampleData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "semesters",
                columns: new[] { "id", "code", "created_at", "end_date", "is_active", "name", "start_date" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000007"), "FA26", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateOnly(2026, 12, 31), true, "Fall 2026", new DateOnly(2026, 9, 1) });

            migrationBuilder.InsertData(
                table: "users",
                columns: new[] { "id", "created_at", "email", "full_name", "is_active", "role", "student_code", "updated_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@fpt.edu.vn", "Nguyen Quang Thanh", true, "admin", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000002"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "headse@fe.edu.vn", "Tran Van Truong", true, "department_head", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "totnt@fe.edu.vn", "Nguyen Trong Tot", true, "lecturer", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000004"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "haind@fe.edu.vn", "Nguyen Dang Hai", true, "proctor", null, new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000005"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hoanglvse170001@fpt.edu.vn", "Le Vu Hoang", true, "student", "SE170001", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("00000000-0000-0000-0000-000000000006"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "maittse170002@fpt.edu.vn", "Tran Thi Mai", true, "student", "SE170002", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.InsertData(
                table: "courses",
                columns: new[] { "id", "code", "created_at", "credits", "exam_input_mode", "has_follow_up", "is_active", "max_follow_up_questions", "name", "semester_id", "transcript_buffer_seconds" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000008"), "PRN231", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, "VoiceWithTranscriptEdit", true, true, 2, "Building Cross-Platform Back-End Applications with .NET", new Guid("00000000-0000-0000-0000-000000000007"), 60 },
                    { new Guid("00000000-0000-0000-0000-000000000009"), "SWD392", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), 3, "VoiceWithTranscriptEdit", true, true, 2, "Software Architecture and Design", new Guid("00000000-0000-0000-0000-000000000007"), 60 }
                });

            migrationBuilder.InsertData(
                table: "classes",
                columns: new[] { "id", "code", "course_id", "created_at", "is_active", "lecturer_id", "max_students", "semester_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000010"), "SE1801-NET", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, new Guid("00000000-0000-0000-0000-000000000003"), 35, new Guid("00000000-0000-0000-0000-000000000007") });

            migrationBuilder.InsertData(
                table: "exam_structures",
                columns: new[] { "id", "bloom_distribution", "course_id", "created_at", "created_by", "duration_minutes", "is_active", "name", "total_questions" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000018"), "{\"Understand\": 2, \"Apply\": 2, \"Analyze\": 1}", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000003"), 30, true, "Cấu trúc Đề thi Vấn đáp Cuối kỳ PRN231 (FA26)", 5 });

            migrationBuilder.InsertData(
                table: "rubrics",
                columns: new[] { "id", "course_id", "created_at", "description", "is_active", "name", "total_max_score" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000013"), new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Đánh giá Clean Architecture", true, "Barem Đánh Giá Kiến Trúc .NET 8", 10.00m });

            migrationBuilder.InsertData(
                table: "class_enrollments",
                columns: new[] { "id", "class_id", "enrolled_at", "status", "student_id" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000011"), new Guid("00000000-0000-0000-0000-000000000010"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "enrolled", new Guid("00000000-0000-0000-0000-000000000005") },
                    { new Guid("00000000-0000-0000-0000-000000000012"), new Guid("00000000-0000-0000-0000-000000000010"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "enrolled", new Guid("00000000-0000-0000-0000-000000000006") }
                });

            migrationBuilder.InsertData(
                table: "exam_questions",
                columns: new[] { "id", "approval_status", "approved_by", "bloom_level", "content", "course_id", "created_at", "difficulty", "is_active", "key_points", "review_notes", "rubric_id", "sample_answer", "source", "submitted_by", "title" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000020"), "APPROVED", new Guid("00000000-0000-0000-0000-000000000002"), "Analyze", "Phân biệt sự khác nhau giữa BackgroundService và IHostedService trong .NET 8 khi xây dựng hàng đợi xử lý chấm điểm ngầm?", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hard", true, "[\"IHostedService là interface cốt lõi\",\"BackgroundService là abstract class kế thừa IHostedService\",\"ExecuteAsync chạy Task bất đồng bộ\"]", null, new Guid("00000000-0000-0000-0000-000000000013"), "IHostedService là interface nền tảng định nghĩa StartAsync và StopAsync. BackgroundService là một abstract class kế thừa IHostedService và triển khai sẵn ExecuteAsync chạy nền vòng lặp an toàn.", "manual", null, "Phân biệt BackgroundService và IHostedService trong .NET 8" });

            migrationBuilder.InsertData(
                table: "exam_sets",
                columns: new[] { "id", "course_id", "generated_at", "is_active", "set_code", "structure_id" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000019"), new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, "PRN231-SET01", new Guid("00000000-0000-0000-0000-000000000018") });

            migrationBuilder.InsertData(
                table: "official_exam_sessions",
                columns: new[] { "id", "course_id", "created_at", "created_by", "exam_date", "exam_input_mode", "exam_structure_id", "has_follow_up", "max_follow_up_questions", "status", "title", "transcript_buffer_seconds" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000022"), new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, new DateOnly(2026, 10, 15), "VoiceWithTranscriptEdit", new Guid("00000000-0000-0000-0000-000000000018"), true, 2, "scheduled", "Vấn đáp Cuối kỳ PRN231 — Ca 1 Sáng", 60 });

            migrationBuilder.InsertData(
                table: "practice_questions",
                columns: new[] { "id", "bloom_level", "content", "course_id", "created_at", "difficulty", "follow_up_prompt", "has_follow_up", "is_active", "key_points", "rubric_id", "sample_answer", "title" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000017"), "Apply", "Hãy giải thích nguyên lý Dependency Inversion trong Clean Architecture .NET 8.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "medium", "Nếu muốn thay đổi cơ sở dữ liệu từ PostgreSQL sang SQL Server, bạn cần thay đổi những tầng nào và tại sao?", true, true, "[\"Module cấp cao không phụ thuộc module cấp thấp\",\"Phụ thuộc vào Abstraction/Interface\"]", new Guid("00000000-0000-0000-0000-000000000013"), "Module cấp cao phụ thuộc abstraction. Application khai báo interface, Infrastructure hiện thực.", "Nguyên lý Dependency Inversion" },
                    { new Guid("00000000-0000-0000-0000-000000000101"), "Understand", "Tầng Domain trong Clean Architecture chứa những thành phần nào và tại sao không được tham chiếu tới các tầng khác?", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "easy", "Làm thế nào để tầng Domain giao tiếp với các dịch vụ bên ngoài mà không phá vỡ tính độc lập?", true, true, "[\"Chứa Entities và quy tắc cốt lõi\",\"Không phụ thuộc Infrastructure hay UI\"]", new Guid("00000000-0000-0000-0000-000000000013"), "Tầng Domain chứa Entities, Value Objects, Domain Events và Enums cốt lõi. Không phụ thuộc vào framework hay database để bảo toàn quy tắc nghiệp vụ.", "Vai trò tầng Domain trong Clean Architecture" },
                    { new Guid("00000000-0000-0000-0000-000000000102"), "Understand", "Hãy trình bày sự khác biệt giữa Command và Query khi triển khai mẫu CQRS với thư viện MediatR trong .NET 8.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "easy", "Khi nào nên sử dụng AsNoTracking() trong các Query Handler?", true, true, "[\"Command thay đổi trạng thái\",\"Query chỉ đọc dữ liệu\",\"Tách biệt mô hình đọc và ghi\"]", new Guid("00000000-0000-0000-0000-000000000013"), "Command dùng để thay đổi trạng thái dữ liệu (Create, Update, Delete), trong khi Query chỉ dùng để truy vấn đọc dữ liệu và không làm biến đổi trạng thái.", "Phân biệt Command và Query trong mẫu CQRS" },
                    { new Guid("00000000-0000-0000-0000-000000000103"), "Apply", "Cách thức hoạt động của IPipelineBehavior trong MediatR kết hợp với FluentValidation để kiểm tra dữ liệu đầu vào trước khi vào Handler?", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "medium", "Ưu điểm của việc đặt validation ở tầng Application thay vì chỉ đặt ở Controller là gì?", true, true, "[\"Chặn trước khi vào Handler\",\"Chạy tất cả IValidator\",\"Trả lỗi chuẩn RFC 7807\"]", new Guid("00000000-0000-0000-0000-000000000013"), "ValidationBehavior chặn request trước khi tới Handler, chạy toàn bộ IValidator<TRequest> đã đăng ký và ném ValidationException nếu có lỗi.", "Cơ chế Pipeline Behavior với FluentValidation" },
                    { new Guid("00000000-0000-0000-0000-000000000104"), "Analyze", "Phân tích cách sử dụng BoundedChannel kết hợp BackgroundService và Polly Retry để xử lý chấm điểm bất đồng bộ chịu tải cao.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hard", "Tại sao phải tạo IServiceScope mới bên trong BackgroundService mỗi khi xử lý một item?", true, true, "[\"BoundedChannel chống tràn RAM\",\"BackgroundService xử lý nền\",\"Polly Retry và DLQ bảo toàn dữ liệu\"]", new Guid("00000000-0000-0000-0000-000000000013"), "BoundedChannel giới hạn số lượng slot trong RAM để áp dụng backpressure (FullMode.Wait). BackgroundService đọc tuần tự và dùng Polly retry, nếu vẫn lỗi thì lưu vào bảng dead_letter_queues.", "Thiết kế hàng đợi BoundedChannel và xử lý Dead-Letter Queue" },
                    { new Guid("00000000-0000-0000-0000-000000000105"), "Analyze", "Phân tích nguyên nhân gây ra lỗi N+1 Query trong Entity Framework Core và các giải pháp khắc phục hiệu quả.", new Guid("00000000-0000-0000-0000-000000000008"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "hard", "Khi nào nên dùng AsSplitQuery() khi sử dụng nhiều lệnh Include?", true, true, "[\"Nguyên nhân do truy vấn trong vòng lặp\",\"Dùng Include/ThenInclude\",\"Dùng Select Projection và AsNoTracking\"]", new Guid("00000000-0000-0000-0000-000000000013"), "Lỗi N+1 xảy ra khi truy vấn danh sách thực thể cha rồi lặp qua từng phần tử để truy vấn thực thể con. Khắc phục bằng Eager Loading (Include/ThenInclude) hoặc Projection (Select).", "Tối ưu hóa truy vấn EF Core 8 và phòng chống lỗi N+1 Query" }
                });

            migrationBuilder.InsertData(
                table: "rubric_criteria",
                columns: new[] { "id", "bloom_level", "created_at", "criterion_name", "description", "max_score", "order_index", "rubric_id", "weight" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000014"), "Understand", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Khái niệm & Ranh giới 4 tầng Clean Architecture", "Hiểu rõ vai trò Domain, Application, Infrastructure, API. Domain không phụ thuộc tầng nào.", 3.00m, 1, new Guid("00000000-0000-0000-0000-000000000013"), 0.30m },
                    { new Guid("00000000-0000-0000-0000-000000000015"), "Apply", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Nguyên lý Dependency Inversion & CQRS MediatR", "Áp dụng tách biệt Command và Query, sử dụng Interface và Dependency Injection Container.", 4.00m, 2, new Guid("00000000-0000-0000-0000-000000000013"), 0.40m },
                    { new Guid("00000000-0000-0000-0000-000000000016"), "Analyze", new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Xử lý Ngoại lệ & Kiến trúc chịu lỗi (Resilience)", "Trình bày cơ chế Global Exception RFC 7807, Bounded Channel và xử lý retry Polly.", 3.00m, 3, new Guid("00000000-0000-0000-0000-000000000013"), 0.30m }
                });

            migrationBuilder.InsertData(
                table: "exam_set_questions",
                columns: new[] { "id", "exam_question_id", "exam_set_id", "order_index" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000021"), new Guid("00000000-0000-0000-0000-000000000020"), new Guid("00000000-0000-0000-0000-000000000019"), 1 });

            migrationBuilder.InsertData(
                table: "real_exam_session_shifts",
                columns: new[] { "id", "created_at", "end_time", "proctor_user_id", "room_lab", "session_id", "shift_name", "start_time", "status" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000023"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 15, 2, 30, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000004"), "Lab 301", new Guid("00000000-0000-0000-0000-000000000022"), "Kíp 1 (08h00 - 09h30)", new DateTime(2026, 10, 15, 1, 0, 0, 0, DateTimeKind.Utc), "scheduled" });

            migrationBuilder.InsertData(
                table: "student_exam_tickets",
                columns: new[] { "id", "checked_in_at", "exam_set_id", "ip_address", "seat_number", "shift_id", "status", "student_id", "submitted_at" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000024"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000019"), "192.168.1.101", 1, new Guid("00000000-0000-0000-0000-000000000023"), "SCHEDULED", new Guid("00000000-0000-0000-0000-000000000005"), null },
                    { new Guid("00000000-0000-0000-0000-000000000025"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), new Guid("00000000-0000-0000-0000-000000000019"), "192.168.1.102", 2, new Guid("00000000-0000-0000-0000-000000000023"), "SCHEDULED", new Guid("00000000-0000-0000-0000-000000000006"), null }
                });

            migrationBuilder.InsertData(
                table: "appeal_requests",
                columns: new[] { "id", "assigned_to", "created_at", "decision", "original_score", "proposed_score", "reason", "resolved_at", "review_notes", "reviewed_by", "session_id", "status", "student_id", "submission_id", "ticket_id", "updated_at" },
                values: new object[] { new Guid("00000000-0000-0000-0000-000000000026"), new Guid("00000000-0000-0000-0000-000000000003"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc), null, 7.50m, null, "Em xin phúc khảo phần phát biểu về nguyên lý Dependency Inversion bị ồn mic", null, null, null, new Guid("00000000-0000-0000-0000-000000000022"), "PENDING", new Guid("00000000-0000-0000-0000-000000000005"), null, new Guid("00000000-0000-0000-0000-000000000024"), new DateTime(2026, 9, 1, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "appeal_requests",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000026"));

            migrationBuilder.DeleteData(
                table: "class_enrollments",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000011"));

            migrationBuilder.DeleteData(
                table: "class_enrollments",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000012"));

            migrationBuilder.DeleteData(
                table: "courses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000009"));

            migrationBuilder.DeleteData(
                table: "exam_set_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000021"));

            migrationBuilder.DeleteData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000017"));

            migrationBuilder.DeleteData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000101"));

            migrationBuilder.DeleteData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000102"));

            migrationBuilder.DeleteData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000103"));

            migrationBuilder.DeleteData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000104"));

            migrationBuilder.DeleteData(
                table: "practice_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000105"));

            migrationBuilder.DeleteData(
                table: "rubric_criteria",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "rubric_criteria",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000015"));

            migrationBuilder.DeleteData(
                table: "rubric_criteria",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000016"));

            migrationBuilder.DeleteData(
                table: "student_exam_tickets",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000025"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"));

            migrationBuilder.DeleteData(
                table: "classes",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000010"));

            migrationBuilder.DeleteData(
                table: "exam_questions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000020"));

            migrationBuilder.DeleteData(
                table: "student_exam_tickets",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000024"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000006"));

            migrationBuilder.DeleteData(
                table: "exam_sets",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000019"));

            migrationBuilder.DeleteData(
                table: "real_exam_session_shifts",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000023"));

            migrationBuilder.DeleteData(
                table: "rubrics",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000013"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000002"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000005"));

            migrationBuilder.DeleteData(
                table: "official_exam_sessions",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000022"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000004"));

            migrationBuilder.DeleteData(
                table: "exam_structures",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000018"));

            migrationBuilder.DeleteData(
                table: "courses",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000008"));

            migrationBuilder.DeleteData(
                table: "users",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000003"));

            migrationBuilder.DeleteData(
                table: "semesters",
                keyColumn: "id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000007"));
        }
    }
}
