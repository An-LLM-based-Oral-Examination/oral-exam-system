# SWP490 · CAPSTONE PROJECT · REVIEW 2 · BM03
## Registration Form – Record of Changes (Bản Tiếng Việt đã lược bỏ các con số chưa có bằng chứng)

### 1. Project Information
| Trường thông tin | Nội dung |
| :--- | :--- |
| **Project code** | FA26SE166 |
| **Project name** | An LLM-based Oral Examination Practice and Assessment System for Software Engineering Major |
| **Team** | Nguyễn Quang Thành, Nguyễn Trọng Tốt, Phạm Nguyễn Đăng Hải, Lê Vũ Hoàng |
| **Supervisor** | TS. Nguyễn Thị Cẩm Hương |
| **Current registration version** | 1.0 |
| **Proposed version** | 2.0 |
| **Version** | 1.0 |
| **Date** | 28/10/2026 |

### 2. Changes
☐ No changes to the current registration form

| No. | Section | Current content | Proposed content | Reason | Proposed by |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **1** | **Vai trò người dùng (Mục c - Yêu cầu chức năng)** | Chỉ chia thành hai nhóm người dùng chung là Giảng viên/Admin và Sinh viên. | Tách thành 6 vai trò riêng biệt: Sinh viên, Giảng viên, Trưởng bộ môn, Cán bộ Khảo thí, Giám thị và Quản trị viên (kèm chuỗi phân cấp học thuật 4 tầng: Admin tạo học kỳ/môn học và gán Trưởng bộ môn $\to$ Trưởng bộ môn phân công nhiều Giảng viên $\to$ Giảng viên tạo lớp học phần kèm mật khẩu lớp và ca thi trong lớp dùng mã OTP xoay vòng 5 phút $\to$ Sinh viên tự tham gia). | Đảm bảo phân tách nhiệm vụ rõ ràng giữa quản trị học vụ, soạn câu hỏi, phê duyệt ngân hàng đề/ma trận đề, tổ chức ca thi & công bố điểm (Cán bộ Khảo thí), điểm danh & ghi nhận vi phạm phòng thi (Giám thị), hậu kiểm điểm (Giảng viên) và xử lý phúc khảo 5 bước. | Nguyễn Trọng Tốt |
| **2** | **Ngân hàng câu hỏi & Quy trình duyệt (Mục c.1 - Giảng viên/Admin)** | Phân loại câu hỏi theo môn, chương và độ khó; dùng chung một ngân hàng câu hỏi và giảng viên tự lưu câu hỏi trực tiếp. | Phân loại câu hỏi theo môn học, chuẩn đầu ra (CLO) và mức độ nhận thức Bloom (bỏ phân loại theo chương); tách độc lập 2 luồng tạo câu hỏi: kho câu hỏi luyện tập/thi thử (`practice_questions` theo CLO) và kho câu hỏi thi thật (`exam_questions` bám sát đề thi mẫu / ma trận cấu trúc đề do Trưởng bộ môn ban hành); câu hỏi và bộ tiêu chí Rubric 10.0 điểm bắt buộc phải được Trưởng bộ môn phê duyệt trước khi phát hành. | Phù hợp với cấu trúc đề cương môn học của trường, đảm bảo đề thi chính thức tuân thủ chặt chẽ ma trận đề của Trưởng bộ môn, ngăn việc lộ câu hỏi thi chính thức trong quá trình sinh viên luyện tập và kiểm soát chất lượng câu hỏi trước khi đưa vào sử dụng. | Phạm Nguyễn Đăng Hải |
| **3** | **Hình thức trả lời & Lưu trữ ghi âm (Mục b & c.2 - Luyện tập, Thi thử)** | Cho phép trả lời bằng giọng nói hoặc nhập trực tiếp câu trả lời bằng văn bản; chưa tách biệt chính sách lưu trữ tệp ghi âm giữa chế độ tự luyện tập và kỳ thi thật. | Câu trả lời bắt buộc phải được ghi âm qua microphone trước (thay vì nhập trực tiếp câu trả lời bằng văn bản); sau khi hệ thống chuyển giọng nói thành văn bản, sinh viên được phép chỉnh sửa bản phiên âm (như các thuật ngữ tiếng Anh chuyên ngành bị nhận diện sai) trước khi gửi chấm. Chỉ lưu trữ tệp ghi âm cho kỳ thi thật tại phòng máy để làm bằng chứng hậu kiểm; chế độ luyện tập và thi thử chỉ lưu văn bản phiên âm. | Giữ đúng bản chất kiểm tra vấn đáp, khắc phục lỗi nhận diện âm thanh đối với thuật ngữ chuyên ngành Kỹ thuật phần mềm và tối ưu dung lượng lưu trữ cho các hoạt động tự học hàng ngày. | Nguyễn Quang Thành |
| **4** | **Quy trình thi thật, Hậu kiểm & Phúc khảo (Mục c.3 - Thi thật)** | Giảng viên hậu kiểm và nhập điểm so sánh giữa điểm AI và file nghe; ghi lại buổi thi để phục vụ phúc khảo. | Trưởng bộ môn cấu hình Follow-up cho đề/ca thi thật (1–5 câu, mặc định 2); Cán bộ Khảo thí tạo kỳ thi/ca thi phòng máy (phân công đồng thời Giám khảo chấm thi `examiner_user_id` & Giám thị `proctor_user_id`), phân bổ đề và mở xuất bản đề thi (`is_exam_published = true`); Giám thị chỉ điểm danh theo chỗ ngồi (Seat 1–40, đối chiếu thẻ SV/CCCD) và ghi log vi phạm an ninh phòng thi (`exam_anti_cheat_logs`). Sinh viên làm bài trên Kiosk khóa màn hình; AI chấm sơ bộ ở tiến trình nền; Giảng viên hậu kiểm toàn bộ bài nộp trước khi công bố điểm 1 lần duy nhất (`One-Way Lock`). Quy trình phúc khảo 5 bước: Sinh viên nộp đơn lên Cán bộ Khảo thí $\to$ Khảo thí và Trưởng bộ môn cùng nhận thông báo, Khảo thí thẩm tra hợp lệ $\to$ Khảo thí phân công Giảng viên độc lập chấm lại (tối đa 24 bài/GV; chia đều nếu tổng bài $> \text{GV} \times 24$, cân bằng tải và điều chỉnh thủ công nếu $\le \text{GV} \times 24$) $\to$ Giảng viên chấm lại $\to$ Cán bộ Khảo thí phê duyệt quyết định cuối cùng. | Chuẩn hóa quy trình khảo thí tập trung tại phòng máy FPTU, phân định rạch ròi quyền hạn giữa Cán bộ Khảo thí, Giám thị, Trưởng bộ môn và Giảng viên, đồng thời đảm bảo tính minh bạch và khách quan qua quy trình phúc khảo 5 bước. | Lê Vũ Hoàng |
| **5** | **Phạm vi tính năng nâng cao & Quản trị tài khoản (Mục c.4 - Tính năng nâng cao)** | Đề cập các hướng mở rộng: chấm phát âm/độ lưu loát tiếng Anh, AI tạo câu hỏi phụ, phát hiện chuyển tab/yêu cầu camera, tích hợp thi qua video call. | Giữ lại và triển khai chính thức: AI sinh câu hỏi phụ theo ngữ cảnh, kiểm soát mất tiêu điểm/thoát toàn màn hình tại phòng máy và thống kê kết quả theo chuẩn đầu ra (CLO). Đưa ra ngoài phạm vi (Out of Scope): gọi Video Call trực tuyến, giám sát Camera, chấm phát âm tiếng Anh và tự đăng ký tài khoản công khai (chỉ dùng tài khoản do nhà trường cấp). | Kỳ thi chính thức được tổ chức trực tiếp tại phòng máy của trường dưới sự giám sát của Giám thị nên không dùng Video Call từ xa; môn học đánh giá kiến thức chuyên ngành bằng tiếng Việt kèm thuật ngữ tiếng Anh nên không chấm phát âm ngoại ngữ. | Nguyễn Quang Thành |
| **6** | **Cơ chế xử lý chấm điểm & Độ tin cậy (Mục d - Yêu cầu phi chức năng)** | Gọi LLM chấm điểm trực tiếp trong lúc chờ phản hồi; lưu câu trả lời khi LLM lỗi hoặc hết thời gian chờ để chấm lại sau. | Tách bước lưu bài nộp khỏi bước chấm điểm tự động: máy chủ ghi nhận và lưu bài nộp của sinh viên vào cơ sở dữ liệu ngay lập tức trước khi chuyển sang hàng đợi chấm ngầm ở tiến trình nền, kết hợp cơ chế tự động thử lại 3 lần và hàng đợi lỗi (DLQ), trong đó Admin giám sát/chạy lại DLQ luồng luyện tập & thi thử còn Cán bộ Khảo thí giám sát/chạy lại DLQ luồng thi thật. | Đảm bảo giao diện làm bài tại phòng thi phản hồi tức thì, không mất dữ liệu bài nộp khi dịch vụ AI quá tải và phân tách đúng thẩm quyền vận hành hàng đợi lỗi giữa Admin và Cán bộ Khảo thí. | Nguyễn Trọng Tốt |

### 3. Decision at Review 2 (completed by reviewers)
| No. | Decision (Accepted / Adjusted / Rejected) | Reviewer note |
| :---: | :--- | :--- |
| **1** | | |
| **2** | | |
| **3** | | |
| **4** | | |
| **5** | | |
| **6** | | |
