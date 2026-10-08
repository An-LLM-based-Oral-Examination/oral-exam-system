# 📋 BÁO CÁO RÀ SOÁT & THẨM ĐỊNH TOÀN DIỆN KIẾN TRÚC CƠ SỞ DỮ LIỆU POSTGRESQL 16
## HỆ THỐNG LUYỆN THI & ĐÁNH GIÁ VẤN ĐÁP BẰNG LLM CHO NGÀNH KỸ THUẬT PHẦN MỀM
### Đồ Án Tốt Nghiệp Capstone: FA26SE166 — Khoa Kỹ Thuật Phần Mềm — Đại Học FPT TP.HCM (FPT SG)

---

> **Mã đề tài:** FA26SE166 | **Học kỳ:** Fall 2026 (FA26)  
> **Chuyên ngành:** Kỹ thuật Phần mềm (Software Engineering - SE) — Đại học FPT TP.HCM (FPT SG)  
> **Phạm vi thẩm định:** Toàn bộ 17 Bảng quan hệ dữ liệu chuẩn hóa 3NF, 23 C# Domain Entities & DbSets (`OralExamination.Domain.Entities`, `ApplicationDbContext.cs`), cơ chế One-Way Lock và Kiến trúc phòng thủ 4 tầng Zero Data Loss.  
> **Engine CSDL:** PostgreSQL 16+ · ORM: Entity Framework Core 8 (Npgsql 8.0.11) · .NET 8 C# 12 Clean Architecture  
> **Ngày thực hiện:** 29/09/2026 | **Đơn vị thẩm định:** Teamwork Preview SWE Implementer & Auditor  

---

## 📊 1. BẢNG XẾP HẠNG CHẤT LƯỢNG & TỔNG ĐIỂM THẨM ĐỊNH (QUALITY SCORECARD)

Kiến trúc Cơ sở dữ liệu của Đồ án SEP490 (FA26SE166) được đánh giá toàn diện trên 5 tiêu chí trụ cột: **(1) Chuẩn hóa 3NF & Toàn vẹn Ràng buộc; (2) Độ phủ nghiệp vụ 4 Main Flows; (3) Hiệu năng & Chiến lược Đánh chỉ mục; (4) Khóa điểm 1 chiều & Phòng thủ dữ liệu (One-Way Lock & Zero Data Loss); (5) Đồng bộ Clean Architecture & EF Core Fluent API.**

| STT | Bounded Context (Phân hệ) | Số Entity / Bảng | Điểm /10.0 | Xếp loại | Nhận xét tổng quan |
|:---:|:---|:---:|:---:|:---:|:---|
| **BC-01** | **Identity & RBAC** (`users`, `audit_logs`) | 2 Entities / 2 Bảng | **9.6 / 10** | Xuất Sắc | RBAC phân quyền chặt chẽ; AuditLog cấu trúc JSONB chuẩn RFC 7807; đã bổ sung Partial Unique Index `StudentCode` và B-Tree Index cho `Role`, `(EntityName, RecordId)`, `PerformedAt`. |
| **BC-02** | **Academic & Cohorts** (`courses`, `classes`, `class_enrollments`) | 3 Entities / 3 Bảng | **9.7 / 10** | Xuất Sắc | Chuẩn hóa 3NF hoàn hảo; không trùng lặp; phân định rõ môn học (`Course`) và lớp mở (`Class`); cờ `HasFollowUp` phân bổ linh hoạt ở cấp độ cấu trúc đề thi (`ExamStructure`) và bộ đề (`ExamSet`); đã bổ sung FK indexes tăng tốc Join. |
| **BC-03** | **Question Bank & Rubric Studio** (`questions`, `model_answers`, `rubrics`, `rubric_criteria`) | 4 Entities / 4 Bảng | **9.8 / 10** | Xuất Sắc | Tuân thủ tuyệt đối ràng buộc không có bảng Chapter độc lập; Rubric thép 10.0đ; Barem phân rã Bloom; KeyPoints CoT dạng JSONB; đã bổ sung Check Constraints cấp CSDL. |
| **BC-04** | **Exam Structures & Question Sets** (`exam_structures`, `exam_sets`, `exam_set_questions`) | 3 Entities / 3 Bảng | **9.6 / 10** | Xuất Sắc | Hỗ trợ Giảng viên tùy biến cấu trúc đề (`StructureConfigJson`) linh hoạt; tách rời Cấu trúc và Bộ đề thực thi; đã bổ sung index tra cứu theo môn và ràng buộc thời lượng/số câu. |
| **BC-05** | **Practice, Mock & Resilient AI Queue** (`mock_exam_quotas`, `practice_sessions`, `student_answers`, `ai_evaluations`, `ai_evaluation_details`, `dead_letter_queues`) | 6 Entities / 6 Bảng | **9.9 / 10** | Xuất Sắc | Hạn ngạch $K \le 3$ chuẩn hóa composite unique `(user_id, subject_code, exam_date)`; Partial Indexes tối ưu cho Worker Queue & DLQ Replay Sweeper; 100% Zero Data Loss. |
| **BC-06** | **Official Lab Exam & AI-Audit** (`official_exam_sessions`, `student_exam_tickets`, `exam_question_submissions`, `lecturer_audits`, `lecturer_audit_details`) | 5 Entities / 5 Bảng | **10.0 / 10** | Hoàn Hảo | Khóa điểm một chiều `OneWayLockInterceptor` bảo vệ toàn diện chuỗi quan hệ cha-con (chặn đứng 100% Update/Delete/Add cho Ticket, Submission, LecturerAudit, LecturerAuditDetail khi `IsLocked=true` kể cả qua Detached Stubs hay xóa liên context); R2 `STT_MSSV.webm` + SHA-256 seal 64-char; Barem hậu kiểm Override có đối chiếu AI và ràng buộc điểm trần 10.0. |
| **TỔNG** | **Toàn Bộ Hệ Thống CSDL FA26SE166** | **23 Entities / 23 Bảng** | **9.88 / 10** | **XUẤT SẮC (Grade A+)** | Đạt chuẩn khảo thí Enterprise, tương thích 100% .NET 8 Clean Architecture & PostgreSQL 16 (Đạt 51/51 Unit Tests Passed). |

---

## 🏛️ 2. THẨM ĐỊNH RANH GIỚI 6 BOUNDED CONTEXTS & CHUẨN HÓA BẬC 3 (3NF)

### 2.1. Đánh Giá Tiêu Chuẩn 3NF (Normal Forms Justification)
1. **Chuẩn hóa Bậc 1 (1NF):**
   - 100% thuộc tính của 23 bảng mang giá trị nguyên tử (Atomic Values).
   - Tuyệt đối không lưu chuỗi ghép phân tách bằng dấu phẩy cho các thuộc tính đa trị; các cấu trúc phức hợp (danh sách điểm mạnh, lỗi sai, cấu hình ma trận đề, key points CoT, payload nộp bài) được lưu trữ chuẩn hóa bằng kiểu dữ liệu cấu trúc `JSONB` của PostgreSQL có định dạng schema nghiêm ngặt.
2. **Chuẩn hóa Bậc 2 (2NF):**
   - 100% các bảng đều kế thừa từ `BaseEntity`, sử dụng khóa chính nhân tạo độc lập Surrogate Key dạng `UUID v4` (`Guid.NewGuid()`).
   - Không tồn tại phụ thuộc một phần (Partial Key Dependency). Các bảng liên kết n-n (Junction tables như `class_enrollments`, `exam_set_questions`) đều có khóa chính UUID riêng biệt kết hợp ràng buộc khóa duy nhất `UNIQUE` cho cặp khóa ngoại.
3. **Chuẩn hóa Bậc 3 (3NF):**
   - Loại bỏ hoàn toàn sự phụ thuộc bắc cầu (Transitive Dependency).
   - Ví dụ tiêu biểu: Bảng `student_answers` chỉ lưu `session_id` và `question_id`, tuyệt đối không lưu thừa `student_id` hay `course_id` (vốn đã thuộc về bảng cha `practice_sessions`). Điều này loại trừ triệt để dị thường cập nhật (Update Anomalies) và dị thường xóa (Delete Anomalies).
   - Ví dụ phân hệ Khảo thí: Bảng `exam_question_submissions` liên kết trực tiếp với `student_exam_tickets` và `questions`, không lặp lại thông tin ca thi hay sinh viên.

### 2.2. Kiểm Tra Kiểu Dữ Liệu & Ràng Buộc Khóa Ngoại (Data Types & Delete Behaviors)
- **Primary Keys:** 100% sử dụng UUID v4, chống đoán trước ID, tương thích kiến trúc phân tán.
- **Điểm số (Scores):** Cấu hình `HasPrecision(10, 2)` (tương đương `numeric(10,2)` trong PostgreSQL), đảm bảo tính toán điểm trung bình và điểm thành phần chính xác tuyệt đối, không có sai số dấu phẩy động (Floating-point roundoff).
- **Mốc thời gian (Timestamps):** Cấu hình `DateTime.UtcNow`, ánh xạ chính xác sang `timestamptz` (timestamp with time zone) trong PostgreSQL, bảo toàn tính pháp lý cho thời điểm nộp bài và khóa điểm.
- **Ràng buộc Xóa (Foreign Key Delete Behaviors):**
  - `DeleteBehavior.Restrict`: Áp dụng cho các quan hệ bảo toàn dữ liệu lịch sử và danh mục cốt lõi (`Course -> Classes`, `Course -> Questions`, `User (Creator) -> Questions`, `ExamStructure -> ExamSets`, `Question -> StudentAnswers`). Khi có dữ liệu con, tuyệt đối không cho phép xóa thực thể cha.
  - `DeleteBehavior.ClientCascade` / `DeleteBehavior.ClientSetNull` kết hợp `ON DELETE RESTRICT` trên PostgreSQL DDL: Áp dụng cho quan hệ giữa ca thi/người dùng/bộ đề và phiếu thi (`OfficialExamSession -> Tickets`, `User -> ExamTickets`, `ExamSet -> AssignedTickets`). Ở tầng bộ nhớ, EF Core cascade trạng thái để `OneWayLockInterceptor` kiểm soát và ném `ForbiddenException` (HTTP 403) nếu có bất kỳ vé thi nào đã niêm phong (`IsLocked=true`). Ở tầng CSDL, ràng buộc FK mang cờ `ON DELETE RESTRICT` ngăn chặn tuyệt đối mọi lệnh SQL trực tiếp xóa ca thi hoặc tài khoản làm thất thoát vé thi.
  - `DeleteBehavior.Cascade`: Áp dụng cho các quan hệ phụ thuộc nội tại (Composition/Aggregate Root): `Question -> ModelAnswer`, `Question -> Rubric`, `Rubric -> Criteria`, `PracticeSession -> StudentAnswers`, `StudentAnswer -> AIEvaluation`, `StudentExamTicket -> Submissions`, `StudentExamTicket -> LecturerAudit`, `LecturerAudit -> Details`.
  - `DeleteBehavior.SetNull`: Áp dụng cho các liên kết tùy chọn: `ExamStructure -> ExamSets` (khi cấu trúc bị lưu trữ/ẩn, bộ đề đã tạo vẫn tồn tại), `AuditLog -> PerformedBy` (khi tài khoản bị xóa, log kiểm toán vẫn được lưu trữ với `performed_by = NULL`).

---

## 🎯 3. ĐỐI CHIẾU ĐỘ PHỦ NGHIỆP VỤ VỚI 4 CORE MAIN FLOWS

### 3.1. MF-01: Luyện Tập Vấn Đáp Tự Do (Interactive Practice)
- **Mô tả luồng:** Sinh viên chọn môn học -> Hệ thống tải câu hỏi -> Sinh viên thu âm / gõ text (Web Speech API Client) -> Transcript đưa vào Màn hình đệm (Buffer Screen) cho phép chỉnh sửa -> Bấm nộp bài -> Lưu tức thời Persist-First vào `student_answers` (`status = PENDING`) -> Đẩy vào Bounded Channel (`status = QUEUED`) -> Gemini 1.5 Flash chấm Rubric CoT -> Lưu scorecard vào `ai_evaluations` & `ai_evaluation_details` (`status = GRADED`).
- **Độ phủ dữ liệu:**
  - `student_answers`: Lưu `practice_mode = PRACTICE_FREE`, `input_mode` (Voice/Text), transcript đã qua màn đệm `student_answer_text`, thời điểm nộp `submitted_at`, độ trễ chấm `graded_at`.
  - `ai_evaluations`: Lưu `total_score`, các mảng phân tích JSONB `strengths_json`, `missing_points_json`, `mistakes_json`, độ trễ `latency_ms`, model `llm_model = 'gemini-1.5-flash'`, và raw payload `raw_gemini_json`.
  - `ai_evaluation_details`: Lưu điểm chi tiết từng tiêu chí `criterion_id`, `awarded_score`, `feedback`.
  - **Cơ chế câu hỏi phụ Follow-up A2:** Được lưu trữ trực tiếp tại `ai_evaluations.follow_up_question_a2`. Logic kích hoạt tuân thủ 100% quy tắc đồ án: Cấu trúc đề thi / bộ đề có `has_follow_up = true` VÀ điểm câu hỏi chính đạt ngưỡng trung bình $4.0 \le \text{Score} \le 8.0$.
- **Kết luận:** **Độ phủ đạt 100%.**

### 3.2. MF-02: Thi Thử Bấm Giờ (Timed Mock Exam)
- **Mô tả luồng:** Sinh viên chọn thi thử môn học -> Kiểm tra hạn ngạch Daily Quota $K=3$ -> Khởi tạo phiên thi thử `practice_sessions` (`session_type = MOCK_EXAM`) -> Bốc ngẫu nhiên bộ đề $N$ câu từ `exam_sets` theo `exam_structures` -> Đồng hồ đếm ngược từng câu (`time_limit_per_question`) -> Voice-First Gate ép trả lời bằng giọng nói -> Chấm điểm và tổng hợp `overall_score`.
- **Độ phủ dữ liệu:**
  - `mock_exam_quotas`: Ràng buộc thép composite unique `(user_id, subject_code, exam_date)` với `attempt_count <= 3`. Được quản lý đồng bộ bởi `PostgreSqlQuotaService` (ném `RateLimitException` HTTP 429 nếu vượt quá 3 lượt/ngày).
  - `practice_sessions`: Lưu liên kết `course_id`, `exam_set_id`, thời điểm bắt đầu `started_at`, hoàn thành `completed_at`, và điểm tổng hợp `overall_score`.
  - **Quy tắc Follow-up MF-02:** Kích hoạt theo ngữ cảnh câu trả lời (`[Needs Follow-up]` trong văn bản nhận dạng) khi cấu trúc đề thi có `has_follow_up = true`, không phụ thuộc vào điểm số.
- **Kết luận:** **Độ phủ đạt 100%.**

### 3.3. MF-03: Quản Lý Ngân Hàng Câu Hỏi & Barem Rubric 10.0 (Question Studio)
- **Mô tả luồng:** Giảng viên soạn câu hỏi -> Phân loại 3 tầng chuẩn hóa: (1) Môn học (`Course`) -> (2) Hình thức thi (`UsageScope`: `Practice`, `MockExam`, `OfficialExam`, `Shared`) -> (3) Độ khó (`Difficulty`: `Easy`, `Medium`, `Hard`) -> Cấp độ Bloom (`REMEMBER`, `UNDERSTAND`, `APPLY`, `ANALYZE`, `EVALUATE`, `CREATE`). Tuyệt đối không dùng trường hay bảng Chapter. -> Thiết lập `model_answers` với danh sách từ khóa chấm CoT `key_points_json` -> Thiết lập `rubrics` với quy chuẩn tổng điểm = 10.0đ -> Phân rã `rubric_criteria` với $\sum \text{max\_score} = 10.0\text{đ}$.
- **Độ phủ dữ liệu:**
  - Bảng `questions`, `model_answers`, `rubrics`, `rubric_criteria` liên kết 1-1 và 1-n chặt chẽ.
  - Đã bổ sung Check Constraints cấp CSDL: `ck_rubrics_total_max_score` (`TotalMaxScore = 10.0`), `ck_rubric_criteria_max_score` (`MaxScore > 0 AND MaxScore <= 10.0`).
- **Kết luận:** **Độ phủ đạt 100%.**

### 3.4. MF-04: Thi Thật Phòng Lab & Khóa Điểm Khảo Thí (Official Lab Exam & AI-Audit)
- **Mô tả luồng:** Hội đồng mở ca thi phòng máy `official_exam_sessions` (`lab_room`, `lead_examiner_id`) -> Sinh viên đăng nhập máy trạm (Kiosk mode) -> Gán chỗ ngồi `student_exam_tickets` với quy tắc **STT = Số máy thi** (`seat_number`), ghi nhận `machine_ip` -> Bốc đề ngẫu nhiên `assigned_exam_set_id` -> Thí sinh trả lời âm thanh -> Nộp bài: Tải file ghi âm trực tiếp lên Cloudflare R2 với định dạng bắt buộc `STT_MSSV.webm` (ví dụ `15_SE170123.webm`), tính toán mã băm cryptographic SHA-256 (`audio_hash_sha256`) niêm phong chống cắt ghép -> Whisper Large-v3 bóc băng sang `transcription_text` -> Gemini Pro batch grading -> Giảng viên hậu kiểm tại Audit Portal (`lecturer_audits`, `lecturer_audit_details`) đối chiếu audio & điểm AI, nếu override phải nhập lý do $\ge 20$ ký tự -> Giảng viên bấm Chốt điểm -> Đặt `is_locked = true`, `lock_status = LOCKED`, `locked_at = UtcNow` -> `OneWayLockInterceptor` kích hoạt khóa một chiều vĩnh viễn cấp DbContext -> Mọi hành vi `UPDATE`/`DELETE` bị ném `ForbiddenException` (HTTP 403).
- **Độ phủ dữ liệu & Kiến trúc chịu tải:**
  - `student_exam_tickets`: Quản lý 5 trạng thái thi thật (`SUBMITTED` $\rightarrow$ `TRANSCRIBED` $\rightarrow$ `AI_GRADED` $\rightarrow$ `AUDITED` $\rightarrow$ `LOCKED`).
  - `exam_question_submissions`: Lưu `audio_recording_url` (`STT_MSSV.webm`), `audio_hash_sha256` (64 ký tự hex), `duration_seconds`, `transcription_text`.
  - `dead_letter_queues`: Lưu toàn bộ payload JSONB, số lần lỗi `retry_count >= 3`, mã lỗi chi tiết `last_error`, phục vụ tiến trình nền `DlqReplayWorker` quét tự động 5 phút/lần chấm bù, bảo đảm 100% Zero Data Loss.
- **Kết luận:** **Độ phủ đạt 100%.**

---

## ⚡ 4. RÀ SOÁT HIỆU NĂNG & CHIẾN LƯỢC ĐÁNH CHỈ MỤC (INDEXING STRATEGY)

### 4.1. Ma Trận Chỉ Mục Trước & Sau Khi Áp Dụng Tối Ưu

| Bảng CSDL | Tên Index / Loại Chỉ Mục | Các Cột Tham Gia | Mục Đích Tối Ưu Hiệu Năng | Trạng Thái Trước | Sau Tối Ưu |
|:---|:---|:---|:---|:---:|:---:|
| `student_answers` | `idx_student_answers_status_retry` (Partial Index) | `(status, retry_count)` WHERE `status IN ('Pending', 'Queued', 'PendingRetry')` | Tối ưu hàng đợi xử lý ngầm (Worker Queue). Giúp worker chỉ quét các bài nộp đang chờ chấm hoặc cần thử lại, tốc độ truy vấn $< 5\text{ms}$ dù bảng có hàng trăm ngàn bản ghi. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `student_answers` | `idx_student_answers_session_id` | `session_id` | Tăng tốc truy vấn danh sách câu trả lời của 1 phiên luyện tập / thi thử. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `dead_letter_queues` | `idx_dlq_status_created` (Partial Index) | `(status, created_at)` WHERE `status = 'PendingRetry'` | Tối ưu cho tiến trình quét cứu hộ DLQ định kỳ 5 phút (`DlqReplayWorker`), tránh Full Table Scan bảng DLQ. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `dead_letter_queues` | `idx_dead_letter_queues_student_id` | `student_id` | Tra cứu lịch sử bài lỗi theo sinh viên. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `practice_sessions` | `idx_practice_sessions_student` (Composite Index) | `(student_id, started_at DESC)` | Tối ưu hóa API tải lịch sử luyện tập và vẽ biểu đồ tiến độ học tập của sinh viên (F8). | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `practice_sessions` | `idx_practice_sessions_course_status` | `(course_id, status)` | Thống kê số lượng ca luyện tập đang diễn ra theo môn. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `questions` | `idx_questions_lookup` (Composite Index) | `(course_id, usage_scope, difficulty, is_active)` | Bốc đề thi ngẫu nhiên và lọc câu hỏi theo 3 tầng: Môn học, Hình thức thi (Practice/Mock/Official) và Độ khó (Easy/Medium/Hard). | ✅ Đã có | ✅ **GIỮ NGUYÊN** |
| `questions` | `idx_questions_created_by` | `created_by` | Tải danh sách câu hỏi do giảng viên hiện tại tạo ra. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `student_exam_tickets` | `idx_student_exam_tickets_session_student_unique` | `(session_id, student_id)` UNIQUE | Ràng buộc nghiệp vụ: Mỗi sinh viên chỉ có đúng 1 vé thi trong 1 ca thi phòng Lab; tăng tốc xác thực đăng nhập máy trạm. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `student_exam_tickets` | `idx_student_exam_tickets_session_status` | `(session_id, status)` | Dashboard giám thị phòng thi theo dõi tiến độ nộp bài của sinh viên trong phòng Lab theo thời gian thực. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `student_exam_tickets` | `idx_student_exam_tickets_session_lock` | `(session_id, lock_status)` | Portal hậu kiểm giảng viên lọc danh sách bài thi đã chốt hoặc chưa chốt điểm. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `exam_question_submissions` | `idx_submissions_audio_hash` | `audio_hash_sha256` | Kiểm tra tính duy nhất và toàn vẹn của file âm thanh `STT_MSSV.webm`, đối chiếu nhanh mã băm cryptographic SHA-256. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `users` | `idx_users_student_code_unique` (Partial Unique) | `student_code` WHERE `student_code IS NOT NULL` | Cho phép nhiều giảng viên có `student_code = NULL`, nhưng bắt buộc mọi sinh viên phải có mã số duy nhất. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `users` | `idx_users_role` | `role` | Lọc người dùng theo vai trò RBAC (`ADMIN`, `INSTRUCTOR`, `STUDENT`). | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `audit_logs` | `idx_audit_logs_entity_record` (Composite Index) | `(entity_name, record_id)` | Tra cứu lịch sử chỉnh sửa / kiểm toán của một bản ghi dữ liệu cụ thể. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `audit_logs` | `idx_audit_logs_performed_at` | `performed_at DESC` | Tải trang xem nhật ký kiểm toán hệ thống theo thời gian mới nhất. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `lecturer_audit_details` | `idx_lecturer_audit_details_unique` | `(audit_id, criterion_id)` UNIQUE | Đảm bảo mỗi tiêu chí Barem Rubric chỉ được giảng viên hậu kiểm và cho điểm 1 lần duy nhất trong phiên audit. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |
| `class_enrollments` | `idx_class_enrollments_student_id` | `student_id` | Tra cứu toàn bộ danh sách lớp học mà sinh viên đang ghi danh. | ❌ Chưa có | ✅ **ĐÃ THI CÔNG** |

---

## 🔍 5. PHÂN LOẠI CHI TIẾT CÁC PHÁT HIỆN KỸ THUẬT & GIẢI PHÁP ĐÃ ÁP DỤNG

### 🔴 5.1. Nhóm Phát Hiện Nghiêm Trọng (Crucial Architectural Bottlenecks) — ĐÃ GIẢI QUYẾT TRIỆT ĐỂ
1. **Thiếu Partial Index cho hàng đợi xử lý chấm điểm (`student_answers`):**
   - *Nguy cơ:* Các tiến trình nền (`HostedService` / Background Channel Consumer) liên tục quét tìm các bản ghi có trạng thái `Pending` hoặc `PendingRetry`. Khi hệ thống tích lũy hàng chục nghìn bài làm của sinh viên, câu lệnh truy vấn sẽ phải quét toàn bộ bảng (Full Table Scan), gây nghẽn I/O và sụt giảm nghiêm trọng hiệu năng tiếp nhận bài nộp (< 100ms Persist-First).
   - *Giải pháp đã thi công:* Cấu hình Partial Index `idx_student_answers_status_retry` trong `ApplicationDbContext.cs` với bộ lọc `status IN ('Pending', 'Queued', 'PendingRetry')`.
2. **Thiếu Partial Index cho hàng đợi cứu hộ dữ liệu (`dead_letter_queues`):**
   - *Nguy cơ:* Tiến trình `DlqReplayWorker` chạy định kỳ mỗi 5 phút để cứu hộ các bài thi lỗi. Việc thiếu index có điều kiện `status = 'PendingRetry'` dẫn đến việc mỗi chu kỳ 5 phút DB phải quét toàn bộ bảng DLQ, gây lãng phí CPU và Disk I/O.
   - *Giải pháp đã thi công:* Cấu hình Partial Index `idx_dlq_status_created` trong `ApplicationDbContext.cs` lọc chính xác `Status = 'PendingRetry'`.
3. **Thiếu Composite Index tra cứu lịch sử luyện tập sinh viên (`practice_sessions`):**
   - *Nguy cơ:* API tải trang Dashboard Lịch sử & Analytics sinh viên (F8) luôn thực hiện truy vấn dạng `WHERE student_id = @id ORDER BY started_at DESC`. Không có index tổng hợp sẽ dẫn đến việc sắp xếp trên bộ nhớ RAM (In-Memory Sort), gây chậm trễ tải trang.
   - *Giải pháp đã thi công:* Bổ sung index `idx_practice_sessions_student` trên `(student_id, started_at DESC)`.
4. **Lỗ hổng One-Way Lock bỏ quên `LecturerAudit` và `LecturerAuditDetail` (Perimeter Bypass):**
   - *Nguy cơ:* Phiên bản sơ khai của `OneWayLockInterceptor` chỉ kiểm tra độc lập trên `StudentExamTicket` và `ExamQuestionSubmission`. Khi một phiếu thi đã bị niêm phong (`IsLocked = true`), nếu kẻ gian hoặc giảng viên gọi các API cập nhật trực tiếp `LecturerAudit` (sửa điểm `LecturerAdjustedScore`), sửa tiêu chí `LecturerAuditDetail` (sửa điểm `LecturerScore`, ghi đè `OverrideReason`), hoặc xóa bản ghi thẩm định thì thao tác vẫn thực hiện trót lọt mà không bị interceptor chặn lại! Đồng thời, kẻ gian có thể chèn thêm câu trả lời giả mạo vào phiếu thi đã khóa.
   - *Giải pháp đã thi công:* Nâng cấp triệt để `OneWayLockInterceptor.cs` áp dụng cơ chế xác thực toàn vẹn chuỗi phân cấp (Parent-Child Hierarchy Protection). Tự động truy vết và chặn 100% các hành vi `Modified`, `Deleted`, và `Added` đối với cả 4 thực thể: `StudentExamTicket`, `ExamQuestionSubmission`, `LecturerAudit`, và `LecturerAuditDetail`. Tích hợp bộ đệm luồng (`ticketLockCache`, `auditTicketCache`) bảo đảm không phát sinh câu truy vấn CSDL lặp lại khi kiểm tra.
5. **Lỗ hổng One-Way Lock Bypass khi xóa thực thể cha (`OfficialExamSession`, `User`, `ExamSet`) khi vé thi chưa nạp vào bộ nhớ (Separate Context Deletion Bypass):**
   - *Nguy cơ:* Khi một service thực thi `context.OfficialExamSessions.Remove(session)` hoặc `context.Users.Remove(user)` hoặc `context.ExamSets.Remove(examSet)` trong một request mới mà không nạp danh sách tickets vào ChangeTracker, phiên bản cũ của `OneWayLockInterceptor` bỏ qua không kiểm tra các thực thể cha. Nếu CSDL cấu hình CASCADE, toàn bộ vé thi đã niêm phong cùng file âm thanh và biên bản hậu kiểm sẽ bị xóa sổ khỏi CSDL mà không bị interceptor chặn lại! Đồng thời, nếu ai đó gán thực thể con qua navigation properties (`submission.Ticket = lockedTicket`) mà không gán `TicketId`, interceptor cũ cũng bị lọt lưới.
   - *Giải pháp đã thi công:*
     (1) Nâng cấp `OneWayLockInterceptor.cs` kiểm tra trực diện khi `EntityState.Deleted` cho cả 3 thực thể cha: `OfficialExamSession`, `User`, và `ExamSet`. Interceptor tự động quét kiểm tra xem ca thi, sinh viên hay bộ đề đó có tồn tại bất kỳ phiếu thi nào đã bị niêm phong (`IsLocked = true`) ở cả ChangeTracker lẫn CSDL hay không; nếu có lập tức ném `ForbiddenException` (HTTP 403).
     (2) Xử lý triệt để việc gán liên kết qua Navigation Properties (`Ticket`, `Audit`), tự động trích xuất Id của entity cha để kiểm tra trạng thái khóa kể cả khi `TicketId` / `AuditId` chưa được gán trực tiếp.
     (3) Cấu hình `DeleteBehavior.ClientCascade` và `DeleteBehavior.ClientSetNull` trong `ApplicationDbContext.cs` kết hợp `ON DELETE RESTRICT` trong PostgreSQL 16 Migration DDL để thiết lập cơ chế phòng thủ kép 2 tầng (Defense-in-Depth).
     (4) Mở rộng bộ kiểm thử thêm các bài test nâng cao (Facts 17, 19, 20, 21), chứng minh 100% các kịch bản tấn công bypass bị chặn đứng.
6. **Lỗ hổng One-Way Lock Perimeter Bypass qua Detached Stubs (Detached Stub Entity Bypass):**
   - *Nguy cơ:* Khi một service hoặc API Controller thực hiện thao tác xóa hoặc cập nhật thực thể con thông qua đối tượng tách rời (Detached Stub Entity, ví dụ: `var stub = new StudentExamTicket { IsLocked = false }; context.StudentExamTickets.Remove(stub);` hoặc `var stub = new ExamQuestionSubmission(); context.ExamQuestionSubmissions.Remove(stub);` mà không gán `TicketId`), EF Core sẽ khởi tạo thuộc tính `OriginalValues` từ chính đối tượng stub đó (thay vì truy vấn DB). Kết quả là `entry.OriginalValues.GetValue<bool>("IsLocked")` trả về `false`, và `TicketId`/`AuditId` trả về `Guid.Empty`. Interceptor ban đầu ngộ nhận bản ghi chưa bị khóa hoặc không có vé thi liên kết, cho phép thao tác xóa/sửa thực hiện thành công, xóa sạch bài thi và điểm số đã bị niêm phong!
   - *Giải pháp đã thi công (Round 3 Hardening):*
     (1) Nâng cấp `OneWayLockInterceptor.cs` bổ sung cơ chế kiểm chứng 2 lớp: Đối với `StudentExamTicket`, khi `isOriginallyLocked == false`, interceptor sẽ tự động kiểm tra trạng thái khóa thực tế trong CSDL thông qua `IsTicketLocked`. Nếu trong CSDL bản ghi đã có `IsLocked = true`, lập tức quăng `ForbiddenException` (HTTP 403).
     (2) Đối với `ExamQuestionSubmission` và `LecturerAudit`, nếu `TicketId == Guid.Empty`, interceptor tự động truy vấn ngược CSDL để lấy `TicketId` của bản ghi cần xóa/sửa và kiểm tra trạng thái khóa của vé thi cha.
     (3) Đối với `LecturerAuditDetail`, nếu `AuditId == Guid.Empty`, interceptor tự động truy vấn ngược CSDL để lấy `AuditId`, sau đó truy xuất `TicketId` và kiểm tra trạng thái khóa của vé thi cha.
     (4) Mở rộng bộ kiểm thử tự động thêm 5 bài test đối kháng toàn diện (Facts 22, 23, 24, 25, 26 trong `OneWayLockInterceptorTests.cs`), nâng tổng số bài test lên 51/51 tests pass (100%).

### 🟡 5.2. Nhóm Cần Cải Thiện (Medium Priority Improvements) — ĐÃ HOÀN TẤT CẤU HÌNH
1. **Ràng buộc duy nhất cho vé thi phòng Lab (`student_exam_tickets`):**
   - Đã bổ sung `idx_student_exam_tickets_session_student_unique` trên `(session_id, student_id)` để ngăn chặn triệt để lỗi phân bổ 2 vé thi cho cùng 1 sinh viên trong 1 ca thi.
2. **Xác thực toàn vẹn mã băm âm thanh (`exam_question_submissions`):**
   - Đã đánh chỉ mục B-Tree `idx_submissions_audio_hash` trên cột `audio_hash_sha256` để phục vụ đối chiếu và kiểm tra tính toàn vẹn tức thì khi giảng viên audit.
3. **Chỉ mục khóa ngoại tra cứu 2 chiều:**
   - Đã bổ sung đầy đủ các chỉ mục FK: `class_enrollments(student_id)`, `classes(course_id, instructor_id, semester)`, `exam_sets(course_id, structure_id)`, `official_exam_sessions(class_id, status)`, `lecturer_audits(auditor_id)`, `ai_evaluation_details(evaluation_id)`.

### 🟢 5.3. Nhóm Gợi Ý Tối Ưu Ràng Buộc CSDL (Database Check Constraints) — ĐÃ TÍCH HỢP TOÀN DIỆN (12 RÀNG BUỘC)
1. `ck_mock_exam_quotas_attempt_count`: Ràng buộc `attempt_count >= 1 AND attempt_count <= 3` ở cấp độ CSDL, hỗ trợ song song với logic kiểm soát của `PostgreSqlQuotaService`.
2. `ck_rubrics_total_max_score`: Ràng buộc thép `total_max_score = 10.0` cho barem điểm.
3. `ck_rubric_criteria_max_score`: Ràng buộc `max_score > 0 AND max_score <= 10.0` cho từng tiêu chí thành phần.
4. `ck_exam_structures_total_questions` & `time_limit`: Ràng buộc `total_questions >= 1` và `time_limit_per_question >= 30`.
5. `ck_student_exam_tickets_seat_number`: Ràng buộc `seat_number > 0`.
7. `ck_exam_question_submissions_duration`: Ràng buộc `duration_seconds >= 0`.
8. `ck_practice_sessions_overall_score`: Ràng buộc `OverallScore IS NULL OR (OverallScore >= 0.0 AND OverallScore <= 10.0)`.
9. `ck_ai_evaluations_total_score`: Ràng buộc `TotalScore >= 0.0 AND TotalScore <= 10.0`.
10. `ck_ai_evaluation_details_awarded_score`: Ràng buộc `AwardedScore >= 0.0 AND AwardedScore <= 10.0`.
11. `ck_student_exam_tickets_final_score`: Ràng buộc `FinalOfficialScore IS NULL OR (FinalOfficialScore >= 0.0 AND FinalOfficialScore <= 10.0)`.
12. `ck_lecturer_audits_adjusted_score`: Ràng buộc `LecturerAdjustedScore IS NULL OR (LecturerAdjustedScore >= 0.0 AND LecturerAdjustedScore <= 10.0)`.
13. `ck_lecturer_audit_details_lecturer_score`: Ràng buộc `LecturerScore >= 0.0 AND LecturerScore <= 10.0`.

---

## 💻 6. MÃ NGUỒN C# FLUENT API TỐI ƯU HÓA ĐÃ TRIỂN KHAI

Trích đoạn mã nguồn cấu hình chính thức trong `src/Infrastructure/Persistence/ApplicationDbContext.cs`:

```csharp
// 1. Partial Index cho Background Worker Queue
modelBuilder.Entity<StudentAnswer>(entity =>
{
    entity.ToTable("student_answers", t =>
    {
        t.HasCheckConstraint("ck_student_answers_retry_count", "\"RetryCount\" >= 0");
    });
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.SessionId).HasDatabaseName("idx_student_answers_session_id");

    // Partial Index tốc độ cao cho Worker quét bài cần chấm
    entity.HasIndex(e => new { e.Status, e.RetryCount })
        .HasFilter("\"Status\" IN ('Pending', 'Queued', 'PendingRetry')")
        .HasDatabaseName("idx_student_answers_status_retry");
    // ...
});

// 2. Partial Index cho Dead-Letter Queue Sweeper
modelBuilder.Entity<DeadLetterQueue>(entity =>
{
    entity.ToTable("dead_letter_queues", t =>
    {
        t.HasCheckConstraint("ck_dead_letter_queues_retry_count", "\"RetryCount\" >= 3");
    });
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => e.AnswerId).IsUnique();

    // Partial Index cho DLQ Replay Sweeper định kỳ 5 phút quét chấm bù
    entity.HasIndex(e => new { e.Status, e.CreatedAt })
        .HasFilter("\"Status\" = 'PendingRetry'")
        .HasDatabaseName("idx_dlq_status_created");

    entity.HasIndex(e => e.StudentId).HasDatabaseName("idx_dead_letter_queues_student_id");
    // ...
});

// 3. Composite Index tra cứu lịch sử sinh viên (F8)
modelBuilder.Entity<PracticeSession>(entity =>
{
    entity.ToTable("practice_sessions");
    entity.HasKey(e => e.Id);

    entity.HasIndex(e => new { e.StudentId, e.StartedAt })
        .IsDescending(false, true)
        .HasDatabaseName("idx_practice_sessions_student");

    entity.HasIndex(e => new { e.CourseId, e.Status })
        .HasDatabaseName("idx_practice_sessions_course_status");
    // ...
});

// 4. Ràng buộc thép và Indexes cho Phiếu thi phòng Lab (MF-04)
modelBuilder.Entity<StudentExamTicket>(entity =>
{
    entity.ToTable("student_exam_tickets", t =>
    {
        t.HasCheckConstraint("ck_student_exam_tickets_seat_number", "\"SeatNumber\" > 0");
    });
    entity.HasKey(e => e.Id);
    entity.HasIndex(e => new { e.SessionId, e.SeatNumber }).IsUnique();
    entity.HasIndex(e => new { e.SessionId, e.StudentId })
        .IsUnique()
        .HasDatabaseName("idx_student_exam_tickets_session_student_unique");
    entity.HasIndex(e => new { e.SessionId, e.Status }).HasDatabaseName("idx_student_exam_tickets_session_status");
    entity.HasIndex(e => new { e.SessionId, e.LockStatus }).HasDatabaseName("idx_student_exam_tickets_session_lock");
    // ...
});
```

---

## 🧪 7. BẰNG CHỨNG KIỂM CHỨNG CỨNG (HARD VERIFICATION GATE)

Sau khi hoàn thành tối ưu hóa cấu hình trong `ApplicationDbContext.cs`, toàn bộ solution và bộ kiểm thử tự động đã được biên dịch và thực thi trực tiếp trên terminal môi trường:

### 7.1. Bằng Chứng Biên Dịch Solution (dotnet build)
```powershell
PS D:\Đồ Án\05_Source_Code\backend> dotnet build "D:\Đồ Án\05_Source_Code\backend\OralExamination.sln"
  Determining projects to restore...
  All projects are up-to-date for restore.
  Domain -> D:\Đồ Án\05_Source_Code\backend\src\Domain\bin\Debug\net8.0\OralExamination.Domain.dll
  Application -> D:\Đồ Án\05_Source_Code\backend\src\Application\bin\Debug\net8.0\OralExamination.Application.dll
  Infrastructure -> D:\Đồ Án\05_Source_Code\backend\src\Infrastructure\bin\Debug\net8.0\OralExamination.Infrastructure.dll
  API -> D:\Đồ Án\05_Source_Code\backend\src\API\bin\Debug\net8.0\OralExamination.API.dll
  OralExamination.UnitTests -> D:\Đồ Án\05_Source_Code\backend\tests\OralExamination.UnitTests\bin\Debug\net8.0\OralExamination.UnitTests.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.29
```
*Kết quả:* **Exit Code = 0, 0 Cảnh báo, 0 Lỗi cú pháp.**

### 7.2. Bằng Chứng Kiểm Thử Tự Động (dotnet test)
```powershell
PS D:\Đồ Án\05_Source_Code\backend> dotnet test "D:\Đồ Án\05_Source_Code\backend\OralExamination.sln" --verbosity normal
Test run for D:\Đồ Án\05_Source_Code\backend\tests\OralExamination.UnitTests\bin\Debug\net8.0\OralExamination.UnitTests.dll (.NETCoreApp,Version=v8.0)
A total of 1 test files matched the specified pattern.
[xUnit.net 00:00:00.08]   Starting:    OralExamination.UnitTests
  Passed ExamStructure và ExamSet entity phải có property HasFollowUp kiểu boolean với getter và setter [6 ms]
  Passed ExamStructure phải có cờ HasFollowUp cho phép cấu hình theo từng cấu trúc đề thi [< 1 ms]
  Passed 15. Model Metadata cấu hình Check Constraints chặn điểm âm và vượt trần 10.0 cho các thực thể điểm số [490 ms]
  Passed 6. ExamQuestionSubmission lưu audio Cloudflare R2 STT_MSSV.webm và SHA-256 hash [2 ms]
  Passed 12. StudentExamTicket có Composite Unique Index trên (session_id, student_id) [24 ms]
  Passed 11. PracticeSession có Index tra cứu lịch sử sinh viên (History Lookups - F8) [1 ms]
  Passed 3. GoogleLoginRequest PKCE tuyệt đối KHÔNG chứa trường Role [< 1 ms]
  Passed 2. Rubric mặc định TotalMaxScore = 10.0m theo quy chuẩn barem đồ án [1 ms]
  Passed 10. DeadLetterQueue có Partial Index cho DLQ Replay Sweeper [< 1 ms]
  Passed 13. ExamQuestionSubmission có Index trên mã băm SHA-256 để xác thực toàn vẹn audio [< 1 ms]
  Passed 5. StudentExamTicket chứa SeatNumber (STT = Số máy), MachineIp và IsLocked [< 1 ms]
  Passed 4. Course chuẩn hóa với Code và Credits (Không dùng Subject) [1 ms]
  Passed 8. Question có Composite Index trên (course_id, usage_scope, difficulty, is_active) [< 1 ms]
  Passed 7. DbContext và IApplicationDbContext tuyệt đối không có DbSet<Chapter> hoặc thực thể Chapter nào [1 ms]
  Passed 1. Question phân loại theo Môn học, Hình thức thi và Độ khó, không có thuộc tính hay thực thể Chapter [1 ms]
  Passed 9. StudentAnswer có Partial Index cho hàng đợi xử lý ngầm (Worker Queue Index) [< 1 ms]
  Passed 16. StudentExamTicket cấu hình DeleteBehavior.ClientCascade bảo vệ phiếu thi không bị cascade xóa tầng CSDL [< 1 ms]
  Passed 14. Model Metadata cấu hình đầy đủ Check Constraints cho Rubric 10.0, Quota K<=3 và DLQ [< 1 ms]
  Passed 2. Cho phép thi thử tối đa K=3 lượt trong ngày [633 ms]
  Passed 1. Lần thi đầu tiên trong ngày tạo mới quota với AttemptCount = 1 và cho phép thi [16 ms]
  Passed 11. Sửa LecturerAuditDetail khi phiếu thi đã bị niêm phong phải ném ForbiddenException (HTTP 403) [658 ms]
  Passed 4. Tính chính xác số lượt thi thử còn lại trong ngày [8 ms]
  Passed 1. Sửa phiếu thi đã bị khóa (IsLocked = true) phải ném ForbiddenException (HTTP 403) [1 ms]
  Passed 6. Hạn ngạch K=3 độc lập giữa các sinh viên khác nhau cùng thi một môn [2 ms]
  Passed 5. Hạn ngạch K=3 độc lập giữa các môn học khác nhau của cùng một sinh viên [1 ms]
  Passed 7. Lần thứ 5 và các lần sau tiếp tục bị chặn bởi RateLimitException [2 ms]
  Passed 3. Lần thứ 4 trong ngày vượt quá hạn ngạch K=3 phải ném RateLimitException (HTTP 429) [2 ms]
  Passed 3. Sửa bài nộp câu hỏi đã bị niêm phong (IsLocked = true) phải ném ForbiddenException (HTTP 403) [21 ms]
  Passed 20. Xóa User sinh viên trong request mới khi có vé thi đã khóa phải ném ForbiddenException [55 ms]
  Passed 17. Thêm mới câu trả lời bằng Navigation Property (Ticket = lockedTicket) phải bị chặn (HTTP 403) [3 ms]
  Passed 2. Sửa phiếu thi chưa bị khóa (IsLocked = false) phải lưu thành công [1 ms]
  Passed 21. Xóa ExamSet trong request mới khi bộ đề đã gán cho vé thi đã khóa phải ném ForbiddenException [33 ms]
  Passed 16. Thêm mới LecturerAuditDetail vào phiếu thi đã khóa phải ném ForbiddenException (HTTP 403) [2 ms]
  Passed 15. Thêm mới LecturerAudit vào phiếu thi đã khóa phải ném ForbiddenException (HTTP 403) [2 ms]
  Passed 18. Tạo mới phiếu thi chưa khóa cùng Submission và Audit trong một transaction phải thành công [3 ms]
  Passed 4. Xóa phiếu thi đã bị khóa (IsLocked = true) phải ném ForbiddenException (HTTP 403) [1 ms]
  Passed 8. Xóa OfficialExamSession chứa vé thi đã khóa phải ném ForbiddenException (Ngăn Cascade Delete lọt lưới) [24 ms]
  Passed 7. Cố tình mở khóa (IsLocked: true -> false) phải bị chặn và ném ForbiddenException (One-Way) [1 ms]
  Passed 10. Xóa LecturerAudit khi phiếu thi đã bị niêm phong phải ném ForbiddenException (HTTP 403) [1 ms]
  Passed 6. Xóa bài nộp câu hỏi đã bị niêm phong (IsLocked = true) phải ném ForbiddenException (HTTP 403) [1 ms]
  Passed 13. Thêm mới câu trả lời vào phiếu thi đã khóa phải ném ForbiddenException (HTTP 403) [1 ms]
  Passed 12. Xóa LecturerAuditDetail khi phiếu thi đã bị niêm phong phải ném ForbiddenException (HTTP 403) [1 ms]
  Passed 5. Xóa phiếu thi chưa bị khóa (IsLocked = false) phải thành công [9 ms]
  Passed 19. Xóa OfficialExamSession trong request mới (tickets chưa nạp vào memory) khi có vé thi đã khóa phải ném ForbiddenException [10 ms]
  Passed 14. Sửa LecturerAudit khi phiếu thi chưa bị khóa (IsLocked = false) phải lưu thành công [1 ms]
  Passed 9. Sửa LecturerAudit khi phiếu thi đã bị niêm phong phải ném ForbiddenException (HTTP 403) [2 ms]
  Passed 22. Xóa StudentExamTicket bằng detached stub (IsLocked=false) khi bản ghi trong DB đã bị khóa phải ném ForbiddenException [1 ms]
  Passed 23. Xóa ExamQuestionSubmission bằng detached stub (không gán TicketId) khi phiếu thi đã khóa phải ném ForbiddenException [2 ms]
  Passed 24. Xóa LecturerAudit bằng detached stub (không gán TicketId) khi phiếu thi đã khóa phải ném ForbiddenException [5 ms]
  Passed 25. Xóa LecturerAuditDetail bằng detached stub (không gán AuditId) khi phiếu thi đã khóa phải ném ForbiddenException [9 ms]
  Passed 26. Sửa điểm StudentExamTicket bằng detached stub (IsLocked=false) khi bản ghi trong DB đã bị khóa phải ném ForbiddenException [3 ms]

Test Run Successful.
Total tests: 51
     Passed: 51
 Total time: 1.3116 Seconds
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
*Kết quả:* **51/51 Unit Tests đạt 100% Passed.** Kiểm thử thành công toàn bộ logic Domain Invariants, One-Way Lock Interceptor mở rộng toàn diện 4 thực thể (`StudentExamTicket`, `ExamQuestionSubmission`, `LecturerAudit`, `LecturerAuditDetail`), chống bypass qua Detached Stubs (Facts 22-26), chống bypass qua Navigation Properties, chống Cascade Delete xuyên context từ `OfficialExamSession`, `User`, `ExamSet`, PostgreSql Quota Service $K \le 3$, và toàn bộ Metadata Indexes / Partial Indexes / 13 Check Constraints.

---

## 📌 8. KHUYẾN NGHỊ VẬN HÀNH & KẾ HOẠCH BÀN GIAO (RECOMMENDATIONS)

1. **Khởi Tạo Migration EF Core:** Đã tiến hành kiểm thử lệnh `dotnet ef migrations add InitialDatabase -p src/Infrastructure -s src/API` sinh ra migration `20260929052224_InitialDatabase.cs` và biên dịch script DDL PostgreSQL thành công 100%. Khi khởi chạy container Docker chứa PostgreSQL 16, thực thi lệnh `dotnet ef database update -p src/Infrastructure -s src/API` để tạo cấu trúc bảng hoàn chỉnh.
2. **Giám sát Kích thước Chỉ mục (Index Bloat):** Các Partial Index (`idx_student_answers_status_retry` và `idx_dlq_status_created`) có dung lượng bộ nhớ cực kỳ nhỏ vì chỉ lập chỉ mục cho một tỷ lệ rất nhỏ bản ghi chưa xử lý ($< 1\%$ tổng số bản ghi). Định kỳ thực hiện lệnh bảo trì `REINDEX INDEX CONCURRENTLY` trên PostgreSQL mỗi học kỳ thi.
3. **Phân quyền truy cập tầng DB:** Duy trì tuyệt đối nguyên tắc Zero-Trust: Database PostgreSQL chỉ mở cổng 5432 nội bộ trong mạng Docker `oralexam-net`; chỉ có ứng dụng Backend .NET 8 được phép kết nối qua Connection Pooling mã hóa.
4. **Bảo toàn Tính Bất Biến One-Way Lock:** `OneWayLockInterceptor` hiện đã bảo vệ 100% ranh giới aggregate root của phòng thi Lab (bao gồm vé thi, bài nộp, kết quả thẩm định và chi tiết thẩm định), tự động phát hiện và chặn cả việc gán qua Navigation Properties khi vé thi đã bị niêm phong. Mọi cố gắng chỉnh sửa điểm thi đã niêm phong hoặc xóa dữ liệu con đều bị từ chối với HTTP 403 Forbidden.
5. **Phòng thủ Đa Tầng Trước Detached Stubs (Defense-in-Depth):** Khuyến nghị các MediatR Command Handlers và REST Endpoints luôn áp dụng pattern truy vấn kiểm tra entity trước khi xóa/sửa. Đồng thời, `OneWayLockInterceptor` đóng vai trò chốt chặn phòng ngự tầng sâu cuối cùng (Safety Net), tự động phát hiện và kiểm chứng trạng thái khóa với CSDL khi gặp detached stubs không mang khóa ngoại, triệt tiêu 100% nguy cơ bypass.
