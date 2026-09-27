# BẢNG PHÂN RÃ CÔNG VIỆC KỸ THUẬT SIÊU CHI TIẾT (TECHNICAL WBS)
**Dự án:** An LLM-based Oral Examination  
**Tài liệu này ánh xạ trực tiếp từ 4 luồng Swimlane (MF-01 đến MF-04) xuống cấp độ Code (Component, API, Database) cho 4 Tuần.**  
**Đội hình (2 FE — 2 BE):** Hoàng (FE Lead), Hải (FE), Thành (BE Lead), Tốt (BE/AI/QA).  
**Quy chuẩn bắt buộc:** Buffer Screen = 30 giây (đồng bộ 100% Swimlane). Quota đếm trực tiếp bằng PostgreSQL (không dùng Redis).

---

## 🚀 TUẦN 1: MÓNG KIẾN TRÚC & MF-03 (NGÂN HÀNG CÂU HỎI & BAREM 10.0)

### 1. Lê Vũ Hoàng (FE Lead)
- **Code (Do):**
  - **FE-1.1:** Cấu hình chuẩn hóa Frontend trên source Vite React TypeScript có sẵn: Cài đặt và cấu hình Tailwind CSS, Shadcn/ui component primitives, Prettier, ESLint, và thiết lập Path Alias `@/` trong `tsconfig.json` cùng `vite.config.ts`.
  - **FE-1.2:** Xây dựng khung ứng dụng dùng `react-router-dom`: Dựng layout hoàn chỉnh gồm `Sidebar.tsx` (Menu: Luyện tập `/practice`, Thi thử `/mock-exam`, Ngân hàng đề `/question-bank`, Cổng hậu kiểm `/lecturer/audit`) và `Header.tsx` (thông tin tài khoản, avatar, nút Logout).
  - **FE-1.3:** Xây dựng component `AuthGuard.tsx` bảo vệ route dựa trên giải mã JWT Role: Phân chia 3 vai trò (`Student`, `Instructor`, `Admin`). Chặn các route quản trị đề thi và hậu kiểm nếu không phải vai trò `Instructor` hoặc `Admin`.
  - **FE-1.4:** Dựng trang `LoginPage.tsx` (Form nhập Email và Password), tích hợp gọi API `POST /api/v1/auth/login`. Lưu trữ `accessToken` vào Session Storage / Memory, tự động điều hướng người dùng tới dashboard tương ứng theo vai trò.
- **Báo cáo (Report):**
  - Khởi chạy `npm run build` đạt 0 lỗi, 0 cảnh báo.
  - Đăng nhập tài khoản `Student` điều hướng thành công vào `/practice`, cố truy cập `/question-bank` bị văng ra trang Unauthorized hoặc Login. Đăng nhập tài khoản `Instructor` truy cập đầy đủ các route quản trị.

### 2. Phạm Nguyễn Đăng Hải (FE Developer)
- **Code (Do):**
  - **FE-1.5:** Dựng trang danh sách câu hỏi `QuestionListPage.tsx`: Gọi API `GET /api/v1/questions`, render bảng dữ liệu phân trang gồm: Tên câu hỏi, Mã môn học, Cấp độ Bloom, Phạm vi áp dụng (`PRACTICE_ONLY`, `EXAM_ONLY`, `SHARED`), và ngày tạo.
  - **FE-1.6:** Dựng trang tạo câu hỏi `CreateQuestionPage.tsx`: Dropdown chọn Môn học (gọi `GET /api/v1/subjects`), chọn Chương (gọi `GET /api/v1/subjects/{id}/chapters`), chọn cấp độ nhận thức Bloom (6 mức) và trường nhập nội dung câu hỏi cùng câu trả lời mẫu của Giảng viên.
  - **FE-1.7:** Xây dựng component bảng tiêu chí Rubric động `RubricForm.tsx` sử dụng `react-hook-form` kết hợp `zod`:
    - Cho phép Giảng viên thêm/xóa từng dòng tiêu chí (Tên tiêu chí, Mô tả, Điểm tối đa `maxScore`).
    - Khóa cứng bất biến logic: `criteria.reduce((sum, item) => sum + item.maxScore, 0) === 10.0`. Nếu tổng khác 10.0, nút "Lưu câu hỏi" lập tức bị vô hiệu hóa (`disabled={true}`) kèm dòng cảnh báo đỏ hiển thị số điểm còn thiếu hoặc thừa.
- **Báo cáo (Report):**
  - Trình diễn nhập điểm tiêu chí lẻ (ví dụ: 3.5 + 4.5 + 1.5 = 9.5) và chứng minh nút Lưu mờ đi với cảnh báo "Tổng điểm Rubric bắt buộc = 10.0 (Hiện tại: 9.5)". Nhập thêm 0.5 để tròn 10.0 thì nút Lưu lập tức sáng lên cho phép gửi form.

### 3. Nguyễn Quang Thành (BE Lead)
- **Code (Do):**
  - **BE-1.1:** Khởi tạo cấu trúc dự án .NET 8 Clean Architecture 4 tầng phân lập (`Domain`, `Application`, `Infrastructure`, `API`). Cài đặt `GlobalExceptionMiddleware.cs` trong `API/Middlewares/` bắt mọi lỗi ngoại lệ và format theo chuẩn RFC 7807 `ProblemDetails`.
  - **BE-1.2:** Thiết lập Authentication & Authorization: Cấu hình JWT Bearer trong `Program.cs`. Viết `AuthController.cs` với endpoint `POST /api/v1/auth/login`: Xác thực thông tin đăng nhập, sinh JWT Token được ký bằng khóa bí mật HMAC-SHA256, đóng gói Claims định danh gồm `UserId`, `Email`, `FullName` và `Role` thuộc 1 trong 3 vai trò: `Student`, `Instructor`, `Admin`.
  - **BE-1.3:** Khởi tạo `ApplicationDbContext.cs` với 11 thực thể cốt lõi (`Users`, `Roles`, `Subjects`, `Chapters`, `Questions`, `RubricCriteria`, `PracticeSessions`, `MockExamSessions`, `ExamSessions`, `ExamSubmissions`, `SystemAuditLogs`). Viết seed data khởi tạo: 5 Giảng viên, 50 Sinh viên và 1 Admin hệ thống.
  - **BE-1.4:** Chạy lệnh Migration `dotnet ef migrations add InitialCreate` tạo schema cơ sở dữ liệu trên PostgreSQL. Xuất bản đặc tả hợp đồng `API_CONTRACT_AND_INTEGRATION_GUIDE.md` và OpenAPI chuẩn mực cho toàn đội.
- **Báo cáo (Report):**
  - Chạy `dotnet build` đạt 0 lỗi, 0 cảnh báo.
  - Swagger UI hiển thị đầy đủ endpoint đăng nhập `POST /api/v1/auth/login`. Thử nghiệm trên Postman nhận về JWT Token hợp lệ chứa đúng vai trò. DBeaver hiển thị đầy đủ 11 bảng cùng dữ liệu seed ban đầu.

### 4. Nguyễn Trọng Tốt (BE, AI & QA)
- **Code (Do):**
  - **BE-1.5:** Xây dựng tính năng CRUD Môn học (Subject) và Chương (Chapter): Viết các Commands & Queries (`CreateSubjectCommand`, `GetSubjectsQuery`, `CreateChapterCommand`, `GetChaptersBySubjectQuery`) cùng `SubjectsController.cs` cung cấp endpoints: `GET /api/v1/subjects`, `POST /api/v1/subjects`, `GET /api/v1/subjects/{id}/chapters`, `POST /api/v1/subjects/{id}/chapters` để cung cấp dữ liệu danh mục cho FE.
  - **BE-1.6:** Viết các luồng CQRS cho Ngân hàng câu hỏi: `CreateQuestionCommand.cs`, `CreateQuestionCommandHandler.cs`, `GetQuestionsQuery.cs`.
  - **BE-1.7:** Thiết lập chốt chặn kiểm thực FluentValidation `CreateQuestionValidator.cs`: Ép buộc quy tắc tổng điểm các tiêu chí rubric phải bằng chính xác 10.0m. Xử lý lưu trữ trong `IDbContextTransaction` (ACID Transaction): Ghi đồng thời Question và các RubricCriterion trong cùng một giao dịch; nếu có bất kỳ lỗi nào xảy ra thì rollback toàn bộ dữ liệu.
  - **BE-1.8:** Khởi tạo project kiểm thử `OralExamination.UnitTests` dùng xUnit và FluentAssertions: Viết Unit Test gửi payload câu hỏi có tổng rubric 9.0m, kiểm chứng hệ thống ném `ValidationException` và Controller trả về HTTP 422 Unprocessable Entity kèm chi tiết lỗi.
- **Báo cáo (Report):**
  - Chạy `dotnet test`: 100% Unit Tests Passed.
  - Thao tác trên Swagger UI tạo thành công Môn học, Chương và Câu hỏi có Rubric chuẩn 10.0; xác nhận cơ chế rollback hoạt động khi cố ý gửi payload sai lệch.

---

## 🚀 TUẦN 2: MF-01 (LUYỆN TẬP TƯƠNG TÁC) & PHÒNG THỦ 4 TẦNG

### 1. Lê Vũ Hoàng (FE Lead)
- **Code (Do):**
  - **FE-2.1:** Viết custom hook `useWebSpeech.ts`:
    - Hàm `speak(text)`: Sử dụng `window.speechSynthesis` kích hoạt TTS đọc to nội dung câu hỏi mô phỏng giám khảo vấn đáp.
    - Hàm `startListening()` và `stopListening()`: Khởi tạo `webkitSpeechRecognition`, lắng nghe micro và stream chuỗi `transcript` dạng real-time hiển thị lên giao diện.
  - **FE-2.2:** Xây dựng giao diện trang luyện tập `PracticeSessionPage.tsx`:
    - Chọn chế độ luyện tập: "Chấm từng câu" (Per-question) hoặc "Làm hết cả bài rồi chấm" (Full-session batch).
    - Nút "Nghe câu hỏi" (kích hoạt TTS), nút "Bắt đầu nói" / "Dừng nói" có icon Micro nhấp nháy đỏ theo biên độ âm thanh.
    - Ô hiển thị văn bản bóc băng thời gian thực khi sinh viên đang phát biểu.
  - **FE-2.3:** Bổ sung nút **"Chấm thử bằng AI" (AI Calibration)** trên `CreateQuestionPage.tsx` (tại Tuần 2 khi Gemini Service của BE đã sẵn sàng): Cho phép Giảng viên gửi câu trả lời mẫu gọi `POST /api/v1/questions/{id}/calibrate` để AI chấm thử nghiệm trước khi lưu chính thức vào ngân hàng đề.
- **Báo cáo (Report):**
  - Trình duyệt phát âm tiếng đọc câu hỏi rõ ràng. Khi nói vào micro, văn bản nhận diện hiển thị tức thì từng từ trên màn hình. Nút "Chấm thử bằng AI" phản hồi kết quả phân tích rubric mẫu chính xác.

### 2. Phạm Nguyễn Đăng Hải (FE Developer)
- **Code (Do):**
  - **FE-2.4:** Xây dựng component màn hình đệm `BufferScreen.tsx`:
    - Đồng hồ đếm lùi **chính xác 30 giây** kết hợp thanh tiến trình (progress bar) chuyển động mượt bằng `requestAnimationFrame`.
    - Cho phép sinh viên gõ bàn phím rà soát và chỉnh sửa lỗi nhận diện thuật ngữ chuyên ngành tiếng Anh (Code-Switching).
    - Hết 30 giây (hoặc khi bấm nút "Xác nhận nộp"), component tự động đóng gói nội dung transcript hoàn chỉnh để gửi lên Backend.
  - **FE-2.5:** Viết hook `useSignalR.ts` quản lý kết nối thời gian thực tới Backend Hub `/hubs/practice`: Tự động tái kết nối khi mất mạng (`withAutomaticReconnect`), truyền JWT Bearer Token trong kết nối WebSocket.
  - **FE-2.6:** Xây dựng modal bảng điểm `ScorecardModal.tsx`: Đăng ký lắng nghe sự kiện `ReceiveScorecard` từ SignalR Hub, tự động bật pop-up hiển thị tổng điểm AI chấm, phân tích điểm chi tiết từng tiêu chí Rubric, nhận xét sư phạm và hai nút lựa chọn: "Luyện tập tiếp" hoặc "Kết thúc phiên".
- **Báo cáo (Report):**
  - Thanh đếm ngược 30 giây hoạt động chuẩn xác từ 30 về 0, cho phép chỉnh sửa văn bản mượt mà.
  - Khi Backend hoàn tất chấm điểm, modal `ScorecardModal` tự động bung lên ngay lập tức mà không cần tải lại trang.

### 3. Nguyễn Quang Thành (BE Lead)
- **Code (Do):**
  - **BE-2.1:** Xây dựng tầng hàng đợi chịu tải `BoundedGradingQueueChannel.cs` dựa trên `System.Threading.Channels.Channel.CreateBounded<GradingTask>` với sức chứa cố định 1,000 slots (`BoundedChannelFullMode.Wait`) nhằm làm phẳng lưu lượng (Traffic Smoothing).
  - **BE-2.2:** Xây dựng `GradingQueueWorker.cs` kế thừa `BackgroundService`:
    - Rút tuần tự các tác vụ chấm điểm từ Channel và chuyển tiếp cho `GeminiService`.
    - Bọc logic gọi AI bằng chính sách thử lại lũy thừa `Polly RetryPolicy`: Thử lại 3 lần với khoảng cách thời gian `2s -> 4s -> 8s`.
    - Nếu thất bại cả 3 lần, chuyển bản ghi vào bảng `dead_letter_queues` (DLQ) với trạng thái `PENDING_RETRY` nhằm đảm bảo tuyệt đối không làm mất dữ liệu của sinh viên (Zero Data Loss).
  - **BE-2.3:** Thiết lập SignalR Hub `PracticeHub.cs` tại endpoint `/hubs/practice`: Sau khi Worker xử lý xong, gọi `Clients.User(userId).SendAsync("ReceiveScorecard", scorecardPayload)` để bắn kết quả trực tiếp về client của sinh viên.
  - **BE-2.4:** Viết API tiếp nhận bài nộp `POST /api/v1/practice/submit`: Ghi nhận tức thì câu trả lời xuống bảng `practice_answers` với trạng thái `PENDING` (Tầng Persist-First < 100ms), đẩy task vào Bounded Channel và phản hồi ngay `HTTP 202 Accepted` cho Frontend.
- **Báo cáo (Report):**
  - Sử dụng kịch bản gửi đồng loạt 100 request vào `/api/v1/practice/submit`: Toàn bộ phản hồi 202 Accepted trong dưới 80ms. Worker rút task xử lý ổn định, SignalR phát tin thành công tới client đúng User ID.

### 4. Nguyễn Trọng Tốt (BE, AI & QA)
- **Code (Do):**
  - **BE-2.5:** Xây dựng `GeminiService.cs` kết nối trực tiếp với Google Gemini 1.5 Flash API qua thư viện chính thức hoặc HttpClient chuyên dụng, nạp khóa API an toàn từ cấu hình.
  - **BE-2.6:** Thiết kế Prompt hệ thống theo kỹ thuật Chain-of-Thought (CoT) 3 bước chuẩn mực:
    - Bước 1: Trích xuất các sự thật then chốt (Fact-anchoring) từ bản gỡ băng của sinh viên.
    - Bước 2: So khớp chi tiết từng sự thật với Barem Rubric 10.0 của Giảng viên.
    - Bước 3: Phân bổ điểm thành phần từng tiêu chí và tổng hợp nhận xét định tính có tính xây dựng.
  - **BE-2.7:** Áp dụng ràng buộc cấu trúc đầu ra (Structured Outputs) qua `response_mime_type="application/json"` và JSON Schema định nghĩa rõ ràng cấu trúc: `{ "totalScore": number, "criteriaScores": [{ "criterionId": string, "score": number, "comment": string }], "feedback": string }`.
  - **BE-2.8:** **Sở hữu duy nhất việc ghi nhận kết quả AI xuống DB:** Viết phương thức cập nhật điểm số chính thức, mảng tiêu chí và phản hồi của Gemini vào bảng `practice_answers` (cập nhật trạng thái sang `GRADED`) trong cùng luồng xử lý trước khi kích hoạt thông báo SignalR.
- **Báo cáo (Report):**
  - Log console hiển thị 100% phản hồi từ Gemini 1.5 Flash là chuỗi JSON thuần khiết, parse thành công vào C# DTO mà không bị lỗi Markdown wrapper (` ```json `). Dữ liệu điểm và nhận xét được lưu toàn vẹn vào DB.

---

## 🚀 TUẦN 3: MF-02 (THI THỬ BẤM GIỜ & VOICE-FIRST) & TIỀN ĐỀ MF-04

### 1. Lê Vũ Hoàng (FE Lead)
- **Code (Do):**
  - **FE-3.1:** Xây dựng giao diện trang thi thử có giám sát `MockExamPage.tsx`: Hiển thị thông tin đề thi, môn thi, tiến độ câu hỏi (ví dụ: Câu 3/10) và tích hợp các chốt chặn Voice-First.
  - **FE-3.2:** Xây dựng component chốt chặn âm thanh `VoiceFirstGate.tsx`:
    - Ô nhập liệu văn bản `<textarea>` bị khóa cứng (`disabled={!isRecordingFinished}`) kèm icon ổ khóa bảo vệ.
    - Nếu sinh viên cố tình nhấp chuột hoặc bấm phím khi chưa thu âm xong, hệ thống chặn lại và bắn Toast cảnh báo màu cam: *"Bạn bắt buộc phải trả lời bằng giọng nói qua Micro trước khi được phép chỉnh sửa!"*.
    - Chỉ sau khi sinh viên bấm "Dừng ghi âm" và micro hoàn tất bóc băng thì ổ khóa mới mở ra cho phép rà soát văn bản.
- **Báo cáo (Report):**
  - Thực nghiệm thao tác gian lận (cố gõ phím trực tiếp) trên trang thi thử: Bị chặn đứng 100% kèm Toast thông báo. Khi nói đủ nội dung và bấm dừng nói, ô nhập liệu tự động kích hoạt cho phép chỉnh sửa.

### 2. Phạm Nguyễn Đăng Hải (FE Developer)
- **Code (Do):**
  - **FE-3.3:** Xây dựng component đồng hồ đếm ngược `CountdownTimer.tsx`:
    - Đồng bộ mốc thời gian chặt chẽ với máy chủ Backend (nhận `startTime` và `maxDurationSeconds` từ payload phiên thi, không dùng `Date.now()` cục bộ tránh sinh viên chỉnh giờ máy tính).
    - Hiển thị song song đồng hồ đếm lùi toàn bài thi và đồng hồ từng câu hỏi.
    - Khi thời gian chạm mốc `00:00`, tự động kích hoạt hàm nộp bài khẩn cấp `handleSubmitExam()` và khóa toàn bộ tương tác.
  - **FE-3.4:** Xây dựng component biểu đồ mạng nhện đánh giá năng lực `BloomRadar.tsx` sử dụng thư viện `Recharts`:
    - Hiển thị 6 trục kỹ năng nhận thức chuẩn Bloom: Ghi nhớ, Thông hiểu, Vận dụng, Phân tích, Đánh giá, Sáng tạo.
    - Nhận dữ liệu phân tích từ payload kết quả thi thử do Backend trả về để vẽ biểu đồ trực quan giúp sinh viên nhận diện điểm yếu.
- **Báo cáo (Report):**
  - Đồng hồ đếm lùi chính xác theo từng giây; khi hết giờ bài thi tự động nộp thành công lên server. Biểu đồ Radar render sắc nét, phản ánh trực quan điểm số theo 6 cấp độ nhận thức.

### 3. Nguyễn Quang Thành (BE Lead)
- **Code (Do):**
  - **BE-3.1:** Xây dựng cơ chế chốt chặn lượt thi `QuotaGuard` bằng **PostgreSQL** (hoàn toàn không dùng Redis):
    - Đếm trực tiếp số lượt thi thử trong ngày bằng truy vấn tối ưu: `SELECT COUNT(*) FROM mock_exam_sessions WHERE user_id = @userId AND subject_id = @subjectId AND DATE(created_at AT TIME ZONE 'UTC') = CURRENT_DATE`.
    - Nếu số lượt đạt ngưỡng >= 3 lượt/môn/ngày, ném ngoại lệ trả về HTTP 429 Too Many Requests kèm thông báo rõ ràng trong RFC 7807: *"Bạn đã sử dụng hết hạn mức 3 lượt thi thử trong ngày cho môn học này. Vui lòng quay lại vào ngày mai!"*.
  - **BE-3.2:** Xây dựng đồng hồ giám định máy chủ (Server-side Master Timer):
    - Khi sinh viên bắt đầu thi (`POST /api/v1/mock-exam/start`), ghi nhận `StartTime` và `MaxDurationSeconds` vào cơ sở dữ liệu.
    - Khi nhận lệnh nộp bài (`POST /api/v1/mock-exam/submit`), so sánh thời điểm nhận request với hạn chót cho phép: Cho phép biên độ trễ mạng tối đa 10 giây. Nếu vượt quá `EndTime + 10s`, từ chối tính điểm câu nộp trễ.
  - **BE-3.3:** **Xây dựng API tạo ca thi phòng Lab (`POST /api/v1/exam-sessions`):**
    - Thiết kế thực thể `ExamSession` và `ExamSessionStudent` liên kết: Mã ca thi (`sessionCode`), Mã phòng Lab (`roomCode`), Môn học, Thời gian bắt đầu, Thời gian kết thúc.
    - Cho phép gán danh sách sinh viên tham gia kỳ thi kèm số máy trạm (`pcNumber`) cố định trong phòng máy.
    - Cung cấp API `GET /api/v1/exam-sessions/{id}` phục vụ cho các trạm thi Kiosk truy vấn xác thực ở Tuần 4.
- **Báo cáo (Report):**
  - Gọi API thi thử lần thứ 4 cho cùng môn học trong ngày: Hệ thống lập tức trả về HTTP 429 kèm ProblemDetails chuẩn. Giả lập nộp bài trễ 15s bị hệ thống từ chối chấm. API tạo ca thi Lab hoạt động trơn tru trên Swagger, gán thành công 40 sinh viên vào 40 máy phòng Lab.

### 4. Nguyễn Trọng Tốt (BE, AI & QA)
- **Code (Do):**
  - **BE-3.4:** Xây dựng thuật toán câu hỏi phản biện mở rộng A2 (Adaptive Deep-Dive Questioning):
    - Sau khi chấm câu hỏi chính, nếu điểm số nằm trong khoảng trung bình `4.0 <= totalScore <= 8.0`, kích hoạt prompt chuyên sâu: Yêu cầu Gemini phân tích kẽ hở lập luận của sinh viên và sinh ngay 1 câu hỏi đào sâu A2 tương ứng để thử thách tư duy phản biện.
  - **BE-3.5:** **Thực thi Load Test hàng đợi chịu tải 4 tầng:** Sau khi hệ thống hàng đợi Tuần 2 đã vận hành ổn định, thực hiện kịch bản kiểm thử tải trọng (Load Testing) bằng công cụ chuyên dụng giả lập 500 người dùng đồng thời nộp bài luyện tập, đánh giá độ ổn định của Bounded Channel và tài nguyên CPU/RAM.
  - **BE-3.6:** Viết bộ kịch bản kiểm thử hồi quy (Regression Test Suite) bao phủ toàn diện luồng MF-01 và MF-02: Đảm bảo các logic tính năng mới của thi thử không làm phát sinh lỗi trên luồng luyện tập tương tác.
- **Báo cáo (Report):**
  - Demo thực nghiệm: Sinh viên trả lời chung chung đạt 6.5 điểm, Gemini lập tức sinh câu hỏi xoáy sâu A2 chuẩn xác vào khái niệm bị bỏ sót.
  - Báo cáo Load Test: Hàng đợi hấp thụ trọn vẹn 500 requests không rơi rớt tác vụ nào (0% drop), thời gian phản hồi HTTP 202 trung bình < 90ms. Kịch bản Regression Test đạt 100% Pass.

---

## 🚀 TUẦN 4: MF-04 (THI THẬT PHÒNG LAB, CỔNG HẬU KIỂM & NIÊM PHONG)

### 1. Lê Vũ Hoàng (FE Lead)
- **Code (Do):**
  - **FE-4.1:** Viết custom hook kiểm soát môi trường thi Kiosk `useKiosk.ts` trên trang `/lab-exam`:
    - Bắt sự kiện chuyển cửa sổ hoặc đổi tab: `window.addEventListener('blur', ...)`.
    - Chặn menu chuột phải: `document.addEventListener('contextmenu', e => e.preventDefault())`.
    - Chặn toàn bộ phím tắt gian lận và mở công cụ nhà phát triển: F12, Ctrl+C, Ctrl+V, Ctrl+Shift+I, Alt+Tab.
  - **FE-4.2:** Xây dựng giao diện cảnh báo vi phạm Kiosk 3 cấp độ:
    - Vi phạm lần 1 & lần 2: Hiển thị Banner đỏ toàn màn hình cảnh báo hành vi bất thường và ghi nhận số lần vi phạm.
    - Vi phạm lần 3: Tự động khóa màn hình thi, hiển thị thông báo đình chỉ thi và kích hoạt API gọi khẩn cấp `POST /api/v1/exam/seal` để niêm phong bài làm hiện tại.
- **Báo cáo (Report):**
  - Mở chế độ ẩn danh, bấm F12 hoặc chuyển tab 3 lần: Giao diện lập tức khóa cứng, đổi nền đỏ cảnh báo đình chỉ thi và bài làm được niêm phong gửi về máy chủ.

### 2. Phạm Nguyễn Đăng Hải (FE Developer)
- **Code (Do):**
  - **FE-4.3:** Xây dựng giao diện Cổng hậu kiểm dành riêng cho Giảng viên `AuditPortalPage.tsx` (`/lecturer/audit`):
    - Bộ lọc danh sách bài thi sinh viên theo Ca thi, Phòng Lab và hiển thị trực quan theo sơ đồ Số máy trạm (PC Number).
    - Bảng thông tin: Họ tên sinh viên, MSSV, Số máy, Điểm AI sơ bộ, Điểm chính thức sau thẩm định, Trạng thái niêm phong và cờ khóa điểm `is_locked`.
  - **FE-4.4:** Xây dựng component trình phát âm thanh bài thi `AudioPlayer.tsx`:
    - Tải và phát bản ghi âm giọng nói WebM lưu trữ tại Cloudflare R2 qua Presigned URL.
    - Tích hợp thanh Timeline tương tác hai chiều: Khi bấm vào bất kỳ mốc thời gian (timestamp) nào trên bản gỡ băng, âm thanh tự động nhảy đến đúng đoạn đó và ngược lại, đoạn văn bản đang phát được bôi sáng màu vàng (yellow highlight).
  - **FE-4.5:** Xây dựng form thẩm định và ghi đè điểm số `ScoreOverrideForm.tsx`:
    - Hiển thị điểm số AI đề xuất cho từng tiêu chí Rubric kèm ô cho phép Giảng viên điều chỉnh điểm.
    - **Ô nhập lý do giải trình bắt buộc (`overrideReason`):** Trường Textarea bắt buộc nhập nếu có thay đổi điểm số, không cho phép để trống.
    - Nút "Lưu điểm thẩm định": Gọi API `PUT /api/v1/audit/override` cập nhật điểm và lý do.
    - Nút "Khóa điểm vĩnh viễn (One-Way Lock)": Mở hộp thoại xác nhận cảnh báo một chiều, khi Giảng viên bấm duyệt sẽ gọi API `POST /api/v1/audit/lock`. Sau khi khóa, toàn bộ các ô nhập điểm bị vô hiệu hóa vĩnh viễn.
- **Báo cáo (Report):**
  - Tua thanh âm thanh, đoạn transcript tương ứng đổi màu bôi sáng tức thì. Giảng viên nhập sửa điểm kèm lý do giải trình lưu thành công; bấm khóa điểm làm nút bấm chuyển xám và không thể chỉnh sửa thêm.

### 3. Nguyễn Quang Thành (BE Lead)
- **Code (Do):**
  - **BE-4.1:** Xây dựng API Khóa điểm một chiều `POST /api/v1/audit/lock`:
    - Phân quyền nghiêm ngặt: Bắt buộc vai trò `Instructor` hoặc `Admin`.
    - Cập nhật cờ `is_locked = true` và thời điểm khóa `locked_at` trên bản ghi bài thi `exam_submissions`.
    - Cài đặt `OneWayLockInterceptor` trong EF Core: Chặn đứng ở mức hạ tầng cơ sở dữ liệu mọi câu lệnh `UPDATE` hay `DELETE` tác động lên bất kỳ bản ghi nào có cờ `is_locked == true`, ném lỗi `DomainValidationException` (HTTP 403 Forbidden) nếu có hành vi can thiệp trái phép.
  - **BE-4.2:** Xây dựng API xuất bảng điểm khảo thí `GET /api/v1/audit/export-excel`:
    - Sử dụng thư viện `EPPlus` kết xuất file bảng điểm định dạng chuẩn `.xlsx` theo mẫu Khảo thí của nhà trường (FAP).
    - Cấu trúc các cột bắt buộc: STT, Mã số sinh viên, Họ và tên, Phòng Lab, Số máy, Điểm AI đề xuất, Điểm chính thức của Giảng viên, Lý do điều chỉnh, Giảng viên thẩm định, Thời gian khóa điểm.
- **Báo cáo (Report):**
  - Dùng Postman cố tình gửi request sửa điểm một bài thi đã bị khóa: Hệ thống chặn đứng và trả về HTTP 403 Forbidden. Gọi API xuất Excel tải về file `.xlsx` định dạng đẹp mắt, đầy đủ dữ liệu và đúng chuẩn biểu mẫu.

### 4. Nguyễn Trọng Tốt (BE, AI & QA)
- **Code (Do):**
  - **BE-4.3:** Tích hợp dịch vụ Whisper STT Server-side cho kỳ thi Lab MF-04 (`WhisperTranscriptionService.cs`):
    - Sử dụng Whisper xử lý âm thanh phía máy chủ (Server-side transcription) để bóc băng toàn bộ file ghi âm bài thi của sinh viên; đảm bảo độ chính xác cao và sinh kèm mốc thời gian chi tiết từng từ (word-level timestamps) cho Cổng hậu kiểm. *(Lưu ý: Web Speech API client-side chỉ dùng cho luyện tập MF-01/MF-02, thi thật MF-04 bắt buộc dùng Whisper server-side).*
  - **BE-4.4:** Tích hợp Cloudflare R2 Storage & Cơ chế niêm phong mật mã SHA-256:
    - Tiếp nhận luồng audio WebM đặt tên theo quy chuẩn định danh `STT_MSSV.webm`, truyền tải trực tiếp lên Cloudflare R2 qua Presigned URL an toàn.
    - Tính toán mã băm mật mã SHA-256 trên luồng byte của file âm thanh:
      ```csharp
      using var sha256 = SHA256.Create();
      byte[] hashBytes = sha256.ComputeHash(audioStream);
      string cryptographicSeal = Convert.ToHexString(hashBytes);
      ```
    - Lưu trữ chuỗi mã băm vào trường `audio_sha256_hash` của bản ghi để niêm phong bài thi chống chối bỏ và chống can thiệp tệp âm thanh.
  - **BE-4.5:** **Xây dựng API sửa điểm thẩm định của Giảng viên (`PUT /api/v1/audit/override`):**
    - Tiếp nhận payload điều chỉnh điểm số các tiêu chí Rubric và chuỗi giải trình `overrideReason` (bắt buộc).
    - Kiểm tra tính hợp lệ: Chỉ cho phép điều chỉnh khi `is_locked == false`.
    - Cập nhật điểm số mới và tự động ghi vết toàn vẹn vào bảng `system_audit_logs` (Ghi nhận ID Giảng viên, Điểm cũ, Điểm mới, Lý do giải trình, Timestamp).
  - **BE-4.6:** Thực thi nghiệm thu chất lượng toàn diện (UAT): Thực hiện rà soát các tiêu chí bảo mật, tính toàn vẹn dữ liệu và xuất bản tài liệu tổng kết kiểm thử `FINAL_TEST_REPORT.md` chứng minh 100% ca kiểm thử then chốt đều PASS.
- **Báo cáo (Report):**
  - Whisper STT bóc băng tiếng Việt/Anh chuẩn xác kèm timestamp.
  - Thử nghiệm sửa 1 byte trong file audio trên R2: Script kiểm tra băm SHA-256 lập tức phát hiện mã băm không khớp và báo động bài thi bị giả mạo.
  - API `PUT /api/v1/audit/override` cập nhật điểm trơn tru, bảng nhật ký kiểm toán ghi vết minh bạch. Tài liệu UAT Report hoàn tất nghiệm thu.

---
*(Bảng WBS Kỹ thuật được cập nhật hoàn chỉnh, đồng bộ 100% với 12 điểm phản biện kỹ thuật, phân làn Swimlane MF-01 đến MF-04 và bảo đảm tính nhất quán trên toàn hệ thống).*
