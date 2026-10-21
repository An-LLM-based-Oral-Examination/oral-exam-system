# 📚 TÀI LIỆU CHUẨN HÓA MỤC 5.1 (BUSINESS RULES) & 5.2 (COMMON REQUIREMENTS) — ĐỒ ÁN SEP490 (FA26SE166)

> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (*LLM-based Oral Examination System — OES*)  
> **Mã đề tài:** FA26SE166 — Học kỳ Fall 2026 (FA26) — Đại học FPT TP.HCM  
> **Giảng viên hướng dẫn:** ThS. Nguyễn Thị Cẩm Hương  
> **Mục đích tài liệu:** Cung cấp cơ sở lý thuyết chuẩn giáo trình Kỹ nghệ Yêu cầu Phần mềm (`SWR302`), dẫn chứng từ báo cáo Capstone mẫu của GVHD, và bộ danh mục chính thức **45 Business Rules (`BR-01` $\to$ `BR-45`)** cùng **12 Common Requirements (`CR-01` $\to$ `CR-12`)** (song ngữ Anh – Việt) để toàn nhóm đồng bộ 100% giữa **Report 2** và **Report 3 (Use Case Specifications)**.

---

## 🔎 PHẦN MỞ ĐẦU: CƠ SỞ LÝ THUYẾT & DẪN CHỨNG HỌC THUẬT VỀ BUSINESS RULES (BR)

### 1. Định nghĩa chuẩn theo giáo trình `SWR302` (Karl Wiegers — *Software Requirements*, 3rd Edition, Chapter 9: *"Playing by the Rules: Business Rules"*)
Trong Kỹ nghệ Phần mềm (Software Engineering) và khuôn mẫu báo cáo **SRS của Đại học FPT**, **Business Rule (BR — Quy tắc nghiệp vụ)** **KHÔNG PHẢI** là các bước thao tác của con người (đó là *Use Case* hoặc *User Requirement*), mà là:
> **Định nghĩa (Karl Wiegers, Ch. 9):** *"A business rule is a directive that defines or restricts some aspect of an organization's operations. Business rules are not software requirements in themselves, but they are the origin of many functional and data requirements that enforce the rules."*  
> *(Tạm dịch: Quy tắc nghiệp vụ là một chỉ thị định nghĩa hoặc giới hạn một khía cạnh vận hành của tổ chức/hệ thống. Nó là nguồn gốc sinh ra các điều kiện kiểm tra và hành vi tự động trong phần mềm).*

### 2. Phân biệt rõ 3 khái niệm trong SRS (Tránh nhầm lẫn khi viết Report 2 & Report 3)

| Khái niệm | Bản chất | Câu hỏi nhận diện | Ví dụ thực tế trong dự án OES (FA26SE166) |
| :--- | :--- | :--- | :--- |
| **Use Case (UC)** | **Hành động của Actor (con người)** tương tác với hệ thống để đạt một mục tiêu. | *"Ai bấm nút làm việc gì?"* | `UC-41`: Giảng viên tạo câu hỏi mới.<br>`UC-95`: Trưởng Bộ môn chạy lại (Replay) tác vụ chấm lỗi trong DLQ. |
| **Business Rule (BR)** | **Luật chơi, giới hạn, công thức hoặc cơ chế tự động (Trigger)** mà cả con người lẫn hệ thống bắt buộc phải tuân theo. | *"Luật nào cho phép/cấm việc đó? Khi điều kiện X xảy ra thì quy định bắt buộc là gì?"* | `BR-20`: Tổng trọng số các tiêu chí trong barem của một câu hỏi phải bằng chính xác 10.0 điểm.<br>`BR-12`: Phiên luyện tập tự động đóng sau 5 phút không tương tác nhưng vẫn giữ nguyên điểm các câu đã làm. |
| **Common Requirement (CR)** | **Chuẩn hành vi giao diện & kỹ thuật dùng chung** lặp lại ở mọi màn hình (UI/UX, điều hướng, múi giờ, phân trang). | *"Mọi màn hình trong phần mềm đều phải tuân thủ chuẩn kỹ thuật/giao diện chung nào?"* | `CR-03`: Truy cập sai vai trò chuyển về trang `403`, đường dẫn không tồn tại chuyển về trang `404`.<br>`CR-08`: Lưu giờ theo chuẩn `UTC` và hiển thị theo múi giờ người dùng (`UTC+7`). |

---

### 3. Năm (05) Phân loại Business Rules chính thống theo Karl Wiegers (Taxonomy of Business Rules)
Một bảng Business Rules chuẩn mực trong SRS bao gồm **5 nhóm quy tắc** dưới đây (chứ không chỉ nói về hành động của con người):

1. **Constraints (Ràng buộc / Giới hạn — *"Ai được phép / không được phép làm gì, giới hạn bao nhiêu"*):**
   - Luôn chứa các từ khóa: *Only... can (Chỉ... mới được)*, *must (bắt buộc)*, *cannot / must not (không được phép)*, *at least / at most (tối thiểu / tối đa)*.
   - *Ví dụ trong OES:* `BR-04` (Chỉ Admin mới được khóa tài khoản và không thể tự khóa mình), `BR-14` (Sinh viên không được vượt quá quota thi thử mỗi ngày do Admin cấu hình), `BR-33` (Khóa điểm 1 chiều sau khi công bố).
2. **Action Enablers (Quy tắc kích hoạt hành động tự động của Hệ thống — *"Nếu điều kiện X xảy ra $\to$ Hệ thống tự động thực thi Y"*):**
   - **Lý giải vì sao quy tắc tự động của hệ thống vẫn là BR chuẩn:** Theo Karl Wiegers, *Action Enabler* là quy tắc kích hoạt một hành động nghiệp vụ tự động khi một điều kiện đúng (ngay cả khi không có người bấm nút tại thời điểm đó).
   - *Ví dụ kinh điển của Wiegers:* *"If the cache of insulin in a hospital pharmacy falls below a minimum threshold, the system automatically places an order."*
   - *Ví dụ trong OES:*
     - `BR-08`: Khi điểm câu chính đạt từ $4.0$ đến $8.0$ $\to$ hệ thống tự động sinh câu hỏi phụ AI (1–5 câu, mặc định 2).
     - `BR-12`: Quá 5 phút không tương tác $\to$ hệ thống tự động đóng phiên luyện tập nhưng giữ nguyên điểm các câu đã làm.
     - `BR-27`: Thoát toàn màn hình hoặc mất tiêu điểm quá 3 lần $\to$ hệ thống tự động phát cảnh báo cho Giám thị để đình chỉ.
     - `BR-41` & `BR-42`: Nếu chấm AI tự động thất bại sau 3 lần thử $\to$ bài nộp của sinh viên vẫn được bảo toàn và chuyển vào hàng đợi lỗi (DLQ) để chấm bù sau 5 phút hoặc do Trưởng Bộ môn kích hoạt chấm lại.
3. **Computations (Quy tắc tính toán / Công thức điểm số):**
   - *Ví dụ trong OES:* `BR-20` (Tổng trọng số tiêu chí barem của một câu hỏi phải bằng chính xác `10.0` điểm).
4. **Inferences (Quy tắc suy diễn trạng thái — *"Nếu điều kiện A đúng $\to$ suy ra phân loại/trạng thái B"*):**
   - *Ví dụ trong OES:* `BR-30` (Nếu bài thi có dấu hiệu bất thường hoặc điểm tự tin AI $< 0.70$ $\to$ xếp vào Nhóm 1 cần Giảng viên hậu kiểm kỹ).
5. **Facts (Sự thật / Định nghĩa bất biến của miền bài toán):**
   - *Ví dụ trong OES:* `BR-18` (Đề cương và CLO được đọc từ hệ thống FLM của trường và không được phép sửa ngược từ OES), `BR-38` (Mỗi sinh viên chỉ thuộc tối đa 1 lớp học phần của cùng 1 môn trong 1 học kỳ).

---

### 4. Dẫn chứng thực tế từ Báo cáo Đồ án mẫu của GVHD (ThS. Nguyễn Thị Cẩm Hương — Đề tài `SP25SE126`)
Trong báo cáo tốt nghiệp mẫu do cô **ThS. Nguyễn Thị Cẩm Hương** hướng dẫn (`SP25SE126_NguyenThiCamHuong_FinalReport`, *Table 16: Business Rules*), các quy tắc tự động xử lý của hệ thống, thời gian chờ (timeout/waiting time), và bảo toàn trạng thái khi sự cố đều được đặt trang trọng trong bảng **Business Rules (`BR`)**:
- **`BR-05` (Đồ án mẫu `SP25SE126`):** *"Automatic Call Termination: Calls will automatically disconnect after the maximum waiting time is exceeded if the interpreter fails to pick up."* $\longrightarrow$ Tương đương trực tiếp với **`BR-12`** (Tự động đóng phiên luyện tập sau 5 phút không tương tác) và **`BR-17`** (Tự động nộp bài thi thử khi hết giờ) của nhóm mình.
- **`BR-06` (Đồ án mẫu `SP25SE126`):** *"Connection Loss Handling: If the video call is disconnected due to network issues, the system will allow a 1-minute reconnection window before terminating the session."* $\longrightarrow$ Tương đương trực tiếp với **`BR-41` & `BR-42`** (Bảo toàn bài nộp của sinh viên khi AI lỗi sau 3 lần thử, chuyển vào DLQ để retry sau 5 phút hoặc Trưởng Bộ môn chạy lại).
- **`BR-16` (Đồ án mẫu `SP25SE126`):** *"Review Immutability: Submitted reviews cannot be edited or deleted by users to maintain transparency."* $\longrightarrow$ Tương đương trực tiếp với **`BR-33`** (Khóa điểm 1 chiều sau khi công bố) và **`BR-45`** (Audit Logs chỉ đọc, không ai kể cả Admin được sửa/xóa).

---
---

# 🇬🇧 PHẦN 1: BẢN TIẾNG ANH CHUẨN NỘP HỘI ĐỒNG (ENGLISH VERSION)

### 5. Requirement Appendix

#### 5.1 Business Rules

**Table 5.1 : Business Rules**

| ID | Rule Definition |
| :-: | :--- |
| BR-01 | Users across all five roles can sign in via Google OAuth 2.0 or with a username and password provisioned by the university. |
| BR-02 | Each account's role is read from the database, and no public self-registration with username and password is allowed. |
| BR-03 | Users with a university-provisioned account can change their password only after verifying their current password. |
| BR-04 | Only Admin can change user roles or lock accounts, and Admin cannot lock their own account. |
| BR-05 | A locked account cannot sign in or access any system features until unlocked by Admin. |
| BR-06 | Practice sessions draw questions only from the approved practice question pool. |
| BR-07 | Per-Question practice mode does not allow more than two consecutive questions of the same difficulty. |
| BR-08 | Per-Question practice triggers AI follow-up questions (1–5, default 2) only when the main score is between 4.0 and 8.0. |
| BR-09 | Full-Session practice requires 3 to 10 questions ordered from Easy to Hard without follow-up questions. |
| BR-10 | Students have a countdown buffer window (10–300s, default 60s) to review and fix recognized English technical terms. |
| BR-11 | Practice and mock exams only save text transcripts and never store audio files. |
| BR-12 | Practice sessions automatically close after 5 minutes of inactivity while keeping graded scores. |
| BR-13 | Mock exams draw questions from the practice pool based on the course structure and Bloom matrix set by the Department Head. |
| BR-14 | Students cannot exceed the daily mock exam quota configured for each course by the Admin. |
| BR-15 | Students can choose whether to enable contextual AI follow-up questions before starting a mock exam. |
| BR-16 | Mock exams lock keyboard typing for primary answers and require voice recording via microphone. |
| BR-17 | Mock exams auto-submit when the timer expires and immediately display a CLO-mapped scorecard. |
| BR-18 | Course syllabi and CLOs are read from the FLM system and cannot be modified by OES. |
| BR-19 | Every question must have a model answer of at least 50 characters and be assigned to at least one question pool. |
| BR-20 | The total weight of all rubric criteria for a question must equal exactly 10.0 points. |
| BR-21 | Questions created by a Lecturer start as drafts and must be submitted to the Department Head for approval. |
| BR-22 | Only the Department Head can approve, request revision for, or reject submitted questions. |
| BR-23 | Only the Department Head can create official exam sessions, lab shifts, and official exam rules. |
| BR-24 | Each candidate is assigned a seat number (1–40) bound to a lab workstation IP and must be checked in by the Proctor. |
| BR-25 | Official exam tickets must follow seven states: SCHEDULED, IN_PROGRESS, SUBMITTED, AI_GRADED, AUDITED, PUBLISHED, and LOCKED. |
| BR-26 | Candidates must pass a 30-second microphone check reaching at least 60 dB before entering the Kiosk exam room. |
| BR-27 | Exiting fullscreen or losing window focus more than 3 times alerts the Proctor for candidate suspension. |
| BR-28 | Official exam audio is saved to Cloudflare R2 as STT_MSSV.webm and sealed with a SHA-256 hash. |
| BR-29 | Kiosk terminals lock immediately after submission and never show immediate scores or appeal options. |
| BR-30 | AI-graded official exams are split into Group 1 (suspicious or confidence below 0.70) and Group 2 (high confidence) for auditing. |
| BR-31 | Lecturers must enter a written reason whenever they override an AI-generated score. |
| BR-32 | Lecturers can publish shift grades only once after 100% of submitted exams in the shift are audited. |
| BR-33 | Publishing shift grades activates a one-way lock that prevents any unauthorized score edits or deletions. |
| BR-34 | Students can submit an appeal on the Student Portal, and the Department Head must assign a different Lecturer to re-grade it. |
| BR-35 | Exported Excel and digitally signed PDF grade reports include a public hash for authenticity verification. |
| BR-36 | Academic semesters must have valid start/end dates and can be updated by Admin, but can never be deleted. |
| BR-37 | Admin can update general course details and configure daily mock exam quotas, but cannot configure official exam rules or exam session structures. |
| BR-38 | A student can enroll in at most one active class per course in the same semester. |
| BR-39 | Only Admin can update global practice settings (follow-up 1–5, progressive 3–10, buffer 10–300s, timeout 5m). |
| BR-40 | Updated global practice settings apply only to new practice sessions and do not affect active sessions. |
| BR-41 | If automatic AI grading fails after 3 attempts, the student's submission is preserved and moved to the Dead-Letter Queue for recovery. |
| BR-42 | Tasks in the Dead-Letter Queue are retried after 5 minutes or replayed by the Department Head so no submission is lost. |
| BR-43 | Replaying a Dead-Letter Queue task will not overwrite a submission that has already been graded. |
| BR-44 | All sensitive actions (login, role changes, score overrides, grade publishing, config updates) are recorded in audit logs. |
| BR-45 | System audit logs are read-only and cannot be edited or deleted by any user, including Admin. |

---

#### 5.2 Common Requirements

**Table 5.2 : Common Requirements**

| ID | Rule Definition |
| :-: | :--- |
| CR-01 | All user input is validated on the client and re-validated on the server before processing. |
| CR-02 | Protected routes verify the user session before rendering and show a loading state while checking. |
| CR-03 | Unauthorized role access redirects to the 403 page, and unknown URLs redirect to the 404 page. |
| CR-04 | Real-time updates (practice scores, lab room status, alerts, notifications) are pushed live via SignalR without page refresh. |
| CR-05 | Multi-step exam flows (practice, mock exam, Kiosk check-in) run on a single route to prevent skipping steps via URL. |
| CR-06 | Simple catalog and configuration records are created and edited in modal dialogs over the list screen. |
| CR-07 | All large data lists support server-side pagination, keyword search, and filtering. |
| CR-08 | All system timestamps are stored in UTC and displayed in the user's local time zone (UTC+7). |
| CR-09 | Student submissions are saved to the database first before queuing for background AI grading. |
| CR-10 | English technical terms spoken inside Vietnamese answers are kept intact and editable during the buffer countdown. |
| CR-11 | Irreversible actions (exam submission, candidate suspension, grade publishing, account locking) require a confirmation dialog. |
| CR-12 | All user-facing notifications and error messages use the standard application messages list in Section 5.3. |

---
---

# 🇻🇳 PHẦN 2: BẢN TIẾNG VIỆT (ĐỐI SOÁT NỘI BỘ & GIẢI TRÌNH HỘI ĐỒNG)

### 5. Phụ Lục Yêu Cầu (Requirement Appendix)

#### 5.1 Quy Tắc Nghiệp Vụ (Business Rules)

**Bảng 5.1 : Quy tắc nghiệp vụ (Business Rules)**

| Mã | Định nghĩa quy tắc (Rule Definition) |
| :-: | :--- |
| BR-01 | Người dùng thuộc cả 5 vai trò có thể đăng nhập qua Google OAuth 2.0 hoặc bằng tên đăng nhập và mật khẩu do nhà trường cấp. |
| BR-02 | Vai trò của mỗi tài khoản được đọc từ cơ sở dữ liệu và hệ thống không cho phép tự đăng ký công khai bằng tên đăng nhập và mật khẩu. |
| BR-03 | Người dùng có tài khoản do nhà trường cấp chỉ được đổi mật khẩu mới sau khi xác nhận đúng mật khẩu hiện tại. |
| BR-04 | Chỉ Admin mới có quyền đổi vai trò hoặc khóa tài khoản, và không thể tự khóa tài khoản của chính mình. |
| BR-05 | Tài khoản bị khóa không thể đăng nhập hoặc truy cập chức năng hệ thống cho đến khi được Admin mở khóa. |
| BR-06 | Các phiên luyện tập chỉ rút câu hỏi từ kho câu hỏi luyện tập đã được phê duyệt. |
| BR-07 | Chế độ luyện Từng câu không cho phép xuất hiện quá 2 câu hỏi liên tiếp có cùng mức độ khó. |
| BR-08 | Chế độ luyện Từng câu chỉ sinh câu hỏi phụ AI (1–5 câu, mặc định 2) khi điểm câu chính đạt từ 4.0 đến 8.0. |
| BR-09 | Chế độ luyện Toàn phiên yêu cầu từ 3 đến 10 câu hỏi sắp xếp từ Dễ đến Khó và không có câu hỏi phụ. |
| BR-10 | Sinh viên có màn hình đệm đếm ngược (10–300s, mặc định 60s) để rà soát và sửa các thuật ngữ tiếng Anh chuyên ngành. |
| BR-11 | Các phiên luyện tập và thi thử chỉ lưu văn bản trả lời và không lưu trữ tệp âm thanh. |
| BR-12 | Phiên luyện tập tự động đóng sau 5 phút không tương tác nhưng vẫn giữ nguyên điểm các câu đã làm. |
| BR-13 | Bài thi thử rút câu hỏi từ kho luyện tập theo cấu trúc đề và ma trận Bloom do Trưởng Bộ môn thiết lập. |
| BR-14 | Sinh viên không được vượt quá số lượt thi thử tối đa mỗi ngày theo môn do Admin cấu hình. |
| BR-15 | Sinh viên có thể chọn bật hoặc tắt câu hỏi phụ AI theo ngữ cảnh trước khi bắt đầu bài thi thử. |
| BR-16 | Bài thi thử khóa nhập liệu bàn phím ở câu trả lời chính và bắt buộc thu âm qua micro. |
| BR-17 | Bài thi thử tự động nộp khi hết giờ và hiển thị ngay bảng điểm chi tiết theo từng CLO. |
| BR-18 | Đề cương môn học và CLO được đọc từ hệ thống FLM và không thể chỉnh sửa ngược lại từ OES. |
| BR-19 | Mỗi câu hỏi phải có đáp án mẫu tối thiểu 50 ký tự và được gán vào ít nhất một kho câu hỏi; câu hỏi biên soạn thủ công được phép đính kèm hình ảnh/sơ đồ kỹ thuật hợp lệ (`PNG/JPG/WEBP <= 5MB`) kèm mô tả để AI chấm đa phương thức. |
| BR-20 | Tổng trọng số các tiêu chí trong barem của một câu hỏi phải bằng chính xác 10.0 điểm. |
| BR-21 | Câu hỏi do Giảng viên tạo khởi đầu ở trạng thái nháp và phải gửi Trưởng Bộ môn phê duyệt. |
| BR-22 | Chỉ Trưởng Bộ môn mới có quyền phê duyệt, yêu cầu chỉnh sửa hoặc từ chối câu hỏi. |
| BR-23 | Chỉ Trưởng Bộ môn mới có quyền tạo kỳ thi chính thức, ca thi phòng Lab và quy tắc thi chính thức. |
| BR-24 | Mỗi thí sinh được xếp một số ghế (1–40) gắn với IP máy trạm phòng Lab và phải được Giám thị điểm danh. |
| BR-25 | Vé thi chính thức chuyển tuần tự qua 7 trạng thái: SCHEDULED, IN_PROGRESS, SUBMITTED, AI_GRADED, AUDITED, PUBLISHED, và LOCKED. |
| BR-26 | Thí sinh phải vượt qua bước kiểm tra micro 30 giây đạt tối thiểu 60 dB trước khi vào phòng thi Kiosk. |
| BR-27 | Thoát toàn màn hình hoặc chuyển cửa sổ quá 3 lần sẽ báo động cho Giám thị để đình chỉ thi. |
| BR-28 | Âm thanh bài thi chính thức được lưu lên Cloudflare R2 theo tên STT_MSSV.webm kèm mã băm SHA-256. |
| BR-29 | Máy trạm Kiosk khóa màn hình ngay sau khi nộp bài và tuyệt đối không hiển thị điểm liền hay khiếu nại tại chỗ. |
| BR-30 | Bài thi chính thức chấm AI xong được chia thành Nhóm 1 (nghi ngờ hoặc độ tin cậy dưới 0.70) và Nhóm 2 (tin cậy cao) để hậu kiểm. |
| BR-31 | Giảng viên bắt buộc nhập lý do giải trình mỗi khi chỉnh sửa điểm do AI chấm. |
| BR-32 | Giảng viên chỉ được công bố điểm ca thi một lần duy nhất sau khi 100% bài nộp trong ca đã được hậu kiểm. |
| BR-33 | Công bố điểm ca thi sẽ kích hoạt khóa một chiều, chặn mọi thao tác sửa hoặc xóa điểm trái phép. |
| BR-34 | Sinh viên có thể gửi đơn phúc khảo trên Student Portal và Trưởng Bộ môn phải giao cho một Giảng viên khác chấm lại. |
| BR-35 | Bảng điểm xuất ra Excel và PDF có chữ ký số luôn kèm mã băm công khai để xác thực tính toàn vẹn. |
| BR-36 | Học kỳ phải có ngày bắt đầu/kết thúc hợp lệ và chỉ được Admin cập nhật, tuyệt đối không được xóa. |
| BR-37 | Admin được cập nhật thông tin môn học và cấu hình hạn ngạch thi thử hàng ngày theo môn, nhưng không được cấu hình quy chế hoặc ca thi chính thức. |
| BR-38 | Mỗi sinh viên chỉ được ghi danh vào tối đa một lớp học phần của cùng một môn trong một học kỳ. |
| BR-39 | Chỉ Admin mới được chỉnh tham số luyện tập toàn cục (hỏi phụ 1–5, toàn phiên 3–10, đệm 10–300s, timeout 5p). |
| BR-40 | Tham số luyện tập mới chỉ áp dụng cho các phiên tạo mới và không ảnh hưởng tới phiên đang diễn ra. |
| BR-41 | Nếu việc chấm AI tự động thất bại sau 3 lần thử, bài nộp của sinh viên vẫn được giữ nguyên và chuyển vào hàng đợi lỗi (DLQ) để xử lý chấm bù. |
| BR-42 | Tác vụ trong DLQ được tự động thử lại sau 5 phút hoặc do Trưởng Bộ môn chạy lại để không mất bài thi. |
| BR-43 | Việc chạy lại tác vụ từ DLQ sẽ không ghi đè nếu bài thi đó đã được chấm điểm thành công. |
| BR-44 | Mọi thao tác nhạy cảm (đăng nhập, đổi vai trò, sửa điểm, công bố điểm, đổi cấu hình) đều được ghi vào nhật ký kiểm toán. |
| BR-45 | Nhật ký kiểm toán chỉ được phép đọc, không một ai kể cả Admin được quyền sửa hoặc xóa. |

---

#### 5.2 Yêu Cầu Chung Toàn Hệ Thống (Common Requirements)

**Bảng 5.2 : Yêu cầu chung (Common Requirements)**

| Mã | Định nghĩa quy tắc (Rule Definition) |
| :-: | :--- |
| CR-01 | Mọi dữ liệu nhập đều được kiểm tra ở trình duyệt và xác thực lại ở máy chủ trước khi xử lý. |
| CR-02 | Các trang bảo vệ luôn kiểm tra phiên đăng nhập trước khi hiển thị và hiện trạng thái chờ trong lúc kiểm tra. |
| CR-03 | Truy cập sai vai trò sẽ chuyển về trang 403, và đường dẫn không tồn tại sẽ chuyển về trang 404. |
| CR-04 | Dữ liệu thời gian thực (điểm luyện tập, phòng máy Lab, cảnh báo, thông báo) được đẩy trực tiếp qua SignalR không cần tải lại trang. |
| CR-05 | Các luồng làm bài nhiều bước (luyện tập, thi thử, check-in Kiosk) chạy trên một đường dẫn duy nhất để tránh nhảy cóc qua URL. |
| CR-06 | Các danh mục và cấu hình đơn giản được thêm và sửa ngay trên hộp thoại nổi (modal) đè lên danh sách. |
| CR-07 | Mọi danh sách dữ liệu lớn đều hỗ trợ phân trang, tìm kiếm và lọc từ phía máy chủ. |
| CR-08 | Toàn bộ mốc thời gian được lưu theo chuẩn UTC và hiển thị theo múi giờ địa phương của người dùng (UTC+7). |
| CR-09 | Bài nộp của sinh viên luôn được lưu vào cơ sở dữ liệu trước khi đưa vào hàng đợi chấm AI ngầm. |
| CR-10 | Thuật ngữ tiếng Anh chuyên ngành nói chêm trong tiếng Việt được giữ nguyên và cho phép sửa ở màn hình đệm. |
| CR-11 | Các thao tác không thể đảo ngược (nộp bài thi, đình chỉ thi, công bố điểm, khóa tài khoản) bắt buộc có hộp thoại xác nhận. |
| CR-12 | Toàn bộ thông báo và báo lỗi hiển thị cho người dùng đều lấy từ danh mục thông báo chuẩn tại Mục 5.3. |
