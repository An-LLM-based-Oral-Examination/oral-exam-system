# BẢNG PHÂN RÃ CÔNG VIỆC KỸ THUẬT SIÊU CHI TIẾT (TECHNICAL WBS)
**Dự án:** An LLM-based Oral Examination  
**Tài liệu này ánh xạ trực tiếp từ 4 luồng Swimlane (MF-01 đến MF-04) xuống cấp độ Code (Component, API, Database) cho 4 Tuần.**
**Đội hình (2 FE - 2 BE):** Hoàng (FE Lead), Hải (FE), Thành (BE Lead), Tốt (BE/AI/QA).

---

## 🚀 TUẦN 1: MÓNG KIẾN TRÚC & MF-03 (NGÂN HÀNG CÂU HỎI & BAREM 10.0)

### 1. Lê Vũ Hoàng (FE Lead)
- **Code (Do):**
  - Khởi tạo thư mục Frontend: `npm create vite@latest`, setup Tailwind, Shadcn.
  - Setup routing: `react-router-dom` (các route `/practice`, `/mock-exam`, `/lab-exam`, `/audit`).
  - Dựng Component `AuthGuard` bảo vệ route dựa trên JWT Role (Student vs Lecturer).
- **Báo cáo (Report):** Code base sạch 0 cảnh báo. Đăng nhập thành công và bị văng ra nếu sai quyền.

### 2. Phạm Nguyễn Đăng Hải (FE Developer)
- **Code (Do):** 
  - Code trang `/question-bank` (MF-03).
  - Viết Component `<RubricForm>` sử dụng `react-hook-form` + `zod`.
  - Logic bắt buộc: Validate mảng `criteria[]`, nếu `.reduce((sum, item) => sum + item.score) !== 10.0` thì Disable nút `Save`.
- **Báo cáo (Report):** Trình diễn Form tạt các số điểm lẻ (vd: 3.5 + 4.5 + 1.5 = 9.5) và chứng minh nút Lưu bị mờ đi.

### 3. Nguyễn Quang Thành (Lead, BE)
- **Code (Do):** 
  - Khởi tạo 4 Layer Clean Architecture (Domain, Application, Infrastructure, API).
  - Thiết lập EF Core `DbContext` cho 11 bảng. Chạy `dotnet ef migrations add Initial`.
  - Viết file `API.yaml` (OpenAPI) chốt Input/Output cho toàn bộ dự án.
- **Báo cáo (Report):** Swagger UI hiển thị rõ ràng các Endpoint, DB trên DBeaver hiện đủ 11 bảng.

### 4. Nguyễn Trọng Tốt (QA, AI/BE)
- **Code (Do):** 
  - Viết luồng CQRS (Command/Query): `CreateQuestionCommand`, `CreateRubricCommand`.
  - Dùng `TransactionScope` trong EF Core: Đảm bảo lưu Question và các Rubric Criteria cùng 1 transaction (ACID).
  - Viết Unit Test: Test thử gửi request POST `/api/questions` với tổng rubric là 8.0, assert HTTP 422 Unprocessable Entity.
- **Báo cáo (Report):** Test Runner hiển thị 100% Pass.

---

## 🚀 TUẦN 2: MF-01 (LUYỆN TẬP TƯƠNG TÁC) & PHÒNG THỦ 4 TẦNG

### 1. Lê Vũ Hoàng (FE Lead)
- **Code (Do):** 
  - Dựng trang `/practice` (MF-01).
  - Viết custom hook `useWebSpeechAPI()`: Gọi hàm Text-to-Speech đọc câu hỏi, gọi Speech-to-Text để nhận diện giọng nói SV.
  - Xử lý việc thu âm đẩy ra `transcript` text realtime lên màn hình.
- **Báo cáo (Report):** FE nhận diện đúng giọng nói tiếng Việt/Anh và web tự động đọc tiếng (AI Voice).

### 2. Phạm Nguyễn Đăng Hải (FE Developer)
- **Code (Do):** 
  - Component `<BufferScreen>` (Màn hình đệm 60s): Có thanh tiến trình (Progress bar) tụt lùi từ 60 về 0 để SV sửa lỗi nhận diện chuyên ngành (Code-Switching).
  - Setup SignalR Client. Component `<ScorecardModal>` tự động Pop-up khi nhận sự kiện `ReceiveScorecard` từ Server.
- **Báo cáo (Report):** Thanh chạy thời gian siêu mượt (requestAnimationFrame), Pop-up điểm bật lên ngay khi có tín hiệu mạng.

### 3. Nguyễn Quang Thành (Lead, BE)
- **Code (Do):** 
  - Khởi tạo Background Service: Dùng `System.Threading.Channels` tạo Bounded Channel 1000 slots.
  - Viết `Polly RetryPolicy`: Nếu gọi AI xịt, tự động thử lại 2s -> 4s -> 8s. Lỗi quá 3 lần ném vào `dead_letter_queues` (DLQ).
  - Setup `PracticeHub` (SignalR) để bắn method `ReceiveScorecard` về Client.
- **Báo cáo (Report):** Gửi 1000 request ảo cùng lúc vào `/api/practice/submit` và show log hàng đợi từ từ nhả task.

### 4. Nguyễn Trọng Tốt (QA, AI/BE)
- **Code (Do):** 
  - Viết Service gọi Google Gemini 1.5 Flash.
  - Viết System Prompt (CoT 3 bước): 1. Đọc Rubric -> 2. So khớp Transcript -> 3. Phân rã điểm.
  - Dùng JSON Schema (`response_mime_type="application/json"`) ép Gemini trả về `{"score": X, "feedback": "Y"}`.
- **Báo cáo (Report):** Log hệ thống in ra chuỗi JSON thuần khiết của AI, không bị kẹp trong dấu ````json`.

---

## 🚀 TUẦN 3: MF-02 (THI THỬ BẤM GIỜ) & THUẬT TOÁN ĐÀO SÂU A2

### 1. Lê Vũ Hoàng (FE Lead)
- **Code (Do):** 
  - Component "Voice-First Gate" ở trang `/mock-exam`: Thuộc tính `disabled={true}` ở ô `<textarea>` cho tới khi state `isRecordingFinished == true`.
  - Nếu sinh viên chưa thu âm xong mà cố ấn gõ phím -> văng Toast Error cảnh báo.
- **Báo cáo (Report):** Biểu diễn việc cố tình gian lận không nói mà gõ phím và bị chặn đứng.

### 2. Phạm Nguyễn Đăng Hải (FE Developer)
- **Code (Do):** 
  - Tích hợp `<CountdownTimer>` (Đếm ngược) móc nối với Server time. Tự động Auto-submit khi hết giờ.
  - Cài đặt `Recharts`. Viết Component `<BloomRadar>` hứng 6 thông số (Nhớ, Hiểu, Vận dụng, Phân tích, Đánh giá, Sáng tạo) từ AI trả về để vẽ biểu đồ mạng nhện.
- **Báo cáo (Report):** Để đồng hồ chạy về 0 và chứng minh Web tự động gửi bài. Biểu đồ Radar hiện đẹp mắt.

### 3. Nguyễn Quang Thành (Lead, BE)
- **Code (Do):** 
  - Viết API "Quota Guard": Dùng Redis đếm số lần gọi `/api/mock-exam/start` theo `UserId:Date`. Nếu >= 3 -> ném lỗi HTTP 429 Too Many Requests.
  - Viết Server-side Timer ở DB (lưu `StartTime` và `EndTime`), từ chối lưu bài nếu `SubmitTime > EndTime + 10s` (Độ trễ mạng).
- **Báo cáo (Report):** Bắn request nộp bài trễ giờ qua Postman và chứng minh BE từ chối lưu bài.

### 4. Nguyễn Trọng Tốt (QA, AI/BE)
- **Code (Do):** 
  - Nâng cấp Prompt: Nếu điểm trả về >= 4.0 và <= 8.0 -> Kích hoạt nhánh AI thứ 2 sinh thêm "Câu hỏi đào sâu A2" (Deep-dive question) chọc ngoáy vào kẽ hở câu trả lời trước.
  - Viết Regression Test đảm bảo MF-02 chạy độc lập với MF-01.
- **Báo cáo (Report):** Demo 1 câu trả lời nửa vời và cho thấy AI đẻ ra câu hỏi xoáy sâu cực kỳ khó.

---

## 🚀 TUẦN 4: MF-04 (THI THẬT PHÒNG LAB), BẢO MẬT BĂM & KHÓA ĐIỂM

### 1. Lê Vũ Hoàng (FE Lead)
- **Code (Do):** 
  - Code "Kiosk Lockdown" ở trang `/lab-exam`. Dùng JS bắt sự kiện `window.onblur`, `contextmenu`, phím F12.
  - Khởi tạo biến đếm số lần vi phạm `violationCount`. Nếu > 3 lần, tự gọi API `/api/exam/seal` (Đình chỉ thi).
- **Báo cáo (Report):** Mở trình duyệt ẩn danh, ấn F12 3 lần và chứng minh màn hình đỏ lòm báo đình chỉ thi.

### 2. Phạm Nguyễn Đăng Hải (FE Developer)
- **Code (Do):** 
  - Dựng trang `/lecturer/audit` (Cổng Hậu Kiểm).
  - Code Audio Player có nút Tua (Seek). 
  - Code giao diện form cho giảng viên ghi đè điểm (Override Score) của AI và bắt buộc nhập lý do giải trình.
- **Báo cáo (Report):** Tua thanh âm thanh và bấm nút "Ghi đè điểm", form bật lên yêu cầu nhập giải trình.

### 3. Nguyễn Quang Thành (Lead, BE)
- **Code (Do):** 
  - Viết API `POST /api/audit/lock`: Kiểm tra phân quyền (`Role = Lecturer`), update `is_locked = true` trên Row của bài thi. Chặn toàn bộ lệnh UPDATE tiếp theo lên Row này.
  - Dùng thư viện `EPPlus` viết API tải xuống File Excel danh sách bảng điểm (Export) format theo chuẩn nhà trường (FAP).
- **Báo cáo (Report):** Cố gắng dùng Postman gửi lệnh sửa điểm lên một bài thi đã Lock và nhận lỗi HTTP 403 Forbidden.

### 4. Nguyễn Trọng Tốt (QA, AI/BE)
- **Code (Do):** 
  - Nhận luồng Audio dạng `Blob` từ FE, đẩy thẳng lên Cloudflare R2 / AWS S3 qua Presigned URL (Đặt tên file `STT_MSSV.webm`).
  - Lấy URI của file, sinh băm SHA-256 (`ComputeHash(audioBytes)`) lưu vào DB để niêm phong.
  - Rà soát UAT, xuất Test Report chốt.
- **Báo cáo (Report):** Sửa 1 byte của file âm thanh trên Cloudflare, viết API quét lại mã băm và chứng minh hệ thống phát hiện bài thi đã bị giả mạo.

---
*(Bảng WBS Kỹ thuật được đóng băng dựa trên nghiên cứu Swimlane MF-01 -> MF-04 và các chốt chặn Voice-First Gate, 4-Tier Queue, Bloom Radar).*
