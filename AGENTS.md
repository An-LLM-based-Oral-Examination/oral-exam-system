# 🤖 AGENTS.md — BẢN THAM CHIẾU NGUỒN MÃ NGUỒN (05_Source_Code)

> **Nguồn Sự Thật Duy Nhất (Single Source of Truth):** Toàn bộ quy chuẩn chi tiết của Đồ Án SEP490 (`FA26SE166`) được nạp trực tiếp từ tệp gốc [`D:\Đồ Án\AGENTS.md`](../AGENTS.md) và skill [`oral-exam-pipeline`](../.agents/skills/oral-exam-pipeline/SKILL.md). Tệp này được giữ tinh gọn để tránh trùng lặp và tiết kiệm Rules Token Budget khi mở đa Workspace.

---

## ⚡ TÓM TẮT NHANH CHO WORKSPACE `05_Source_Code`

1. **Tư duy Phản biện, Truy vết Bằng chứng & Cấm Nịnh Bợ (4 Bước Bắt Buộc):**
   - **Search-Before-Speak:** Bắt buộc dùng tool (`codebase-memory`, `view_file`, `search_web`, `dotnet test`) tìm kiếm sự thật trước khi trả lời bất kỳ nhận định/câu hỏi nào của User.
   - **Zero-Flattery:** Cấm tuyệt đối khen sáo rỗng hay hùa theo ("Yes-Man").
   - **Evidence-Based Verdict:** Mở đầu bằng phán quyết `[ĐÚNG]` / `[CHƯA CHÍNH XÁC]` / `[ĐÚNG MỘT PHẦN]` kèm dẫn chứng `file:///...#L...` và lý luận nhân quả rõ ràng.
   - **Agreement Gate:** Chờ User xác nhận đồng ý trước khi sửa code (Mức M/L). Mức S ($\le 3$ files) chạy Fast-Path SOLO.
2. **12 Cam Kết Bất Biến Cốt Lõi:**
   - **Zero Placeholder, Anti-Mocking & Zero-Bypass (Chống Gian Lận Tuyệt Đối):** Cấm `// TODO`, cấm dummy/fake data trong production, cấm bypass logic nghiệp vụ/Voice-First Gate/duyệt đề/AI/STT/DB thật và cấm mọi hình thức gian lận để làm cho xong dự án.
   - **Holistic DB-Code 3NF & Anti-Column-Stuffing (Đồng Bộ Tối Ưu DB & Code):** Luôn đánh giá đồng bộ cả DB Schema và Code. Cấm nhồi cột (nối chuỗi vào `cot_trace`, trộn `"qid:"` vào mảng độ khó, giấu `OrderIndex` vào `Guid`) hoặc dùng `[NotMapped]` + static RAM để né tạo cột DB; truy xuất trực diện bằng khóa $O(1)$ (`Id`/`ParentId`) và đồng bộ 100% `Entity` $\leftrightarrow$ `DbContext` $\leftrightarrow$ `Migration`.
   - **Ports:** FE `3000`, BE `5000`, PostgreSQL `5432` (nội bộ).
   - **MF-01 (Practice):** Admin cấu hình (`TranscriptBufferSeconds` 10–300s mặc định 60s, follow-up 1–5 mặc định 2, progressive 3–10, timeout `SessionInactivityTimeoutMinutes = 5` phút trả `HTTP 410 Gone`). Không lưu audio R2.
   - **MF-02 (Mock Exam):** Quota do Admin cấu hình theo môn (`max_mock_exams_per_day`), vượt trả `HTTP 429`. Trưởng BM cấu hình cấu trúc đề & Bloom. Kho `practice_questions`. Không lưu audio R2.
   - **MF-03 (Question Bank):** Barem Rubric $\sum \equiv 10.0$đ (`HTTP 422`), Model Answer $\ge 50$ ký tự, hỗ trợ đính kèm ảnh/sơ đồ kỹ thuật (`PNG/JPG/WEBP <= 5MB`, Magic Bytes `HTTP 413`/`415`, `image_url` + `image_description` chấm Multimodal AI) cho câu hỏi soạn thủ công (`source == "manual"`), gửi Trưởng BM duyệt (`APPROVED`, `NEEDS_REVISION`, `REJECTED`).
   - **MF-04 (Official Kiosk Exam):** Cột IP là `ip_address` (cấm `workstation_ip`), cấm thực thể `chapter`, trạng thái vé viết HOA (`SCHEDULED` $\to$ `LOCKED`), Persist First $< 100$ms (`SUBMITTED`), Audio R2 `STT_MSSV.webm` + SHA-256, `BoundedChannel` 1000 slots RAM + Polly Retry (2-4-8s) + DLQ 5m (Trưởng BM giám sát & replay DLQ), `OneWayLockInterceptor` (`is_locked = true` $\to$ `HTTP 403`), phúc khảo `AppealRequest` trên Student Portal.
3. **Cổng Kiểm Chứng Cứng (Hard Verification Gate — Exit Code = 0):**
   ```powershell
   dotnet build "D:\Đồ Án\05_Source_Code\backend"
   dotnet test "D:\Đồ Án\05_Source_Code\backend" --verbosity normal
   cd "D:\Đồ Án\05_Source_Code\frontend"; npm run build; cd ..
   ```
