# Log làm rõ và thay đổi Main Flow

File này ghi những chỗ đã chốt khi viết Report 1. Dòng có ❌ là chỗ tài liệu cũ đang lệch, cần sửa trước khi code theo bản cũ. Chưa chốt thì không được đem vào mục 6 của report.

Người chốt: Nguyễn Trọng Tốt. Ngày: 07/10/2026.

## Chung

Đăng nhập bằng Google được. Email không bắt buộc phải là `@fpt.edu.vn`. Sinh viên tự đăng nhập hoặc tự đăng ký.

❌ Đang lệch với [MASTER_ARCHITECTURE.md](MASTER_ARCHITECTURE.md) dòng 124, 151, 188, 207. Đang ghi bắt buộc `@fpt.edu.vn`.

❌ Đang lệch với [frontend/FE_TASK_BOARD.md](../frontend/FE_TASK_BOARD.md) dòng 82. Nút đăng nhập đang ghi Google FPT `@fpt.edu.vn`.

❌ Đang lệch với [KIEN_TRUC_HE_THONG.drawio](KIEN_TRUC_HE_THONG.drawio) ô xác thực, dòng 43. Đang ghi đuôi email bắt buộc `@fpt.edu.vn`.

❌ Đang lệch với [diagrams/KIEN_TRUC_HE_THONG.drawio](diagrams/KIEN_TRUC_HE_THONG.drawio) ô xác thực, dòng 65. Cùng câu bắt buộc `@fpt.edu.vn`.

Admin chỉ cấu hình MF-01. Admin không cấu hình quota thi thử, không cấu hình thi thật.

❌ Đang lệch với [MASTER_ARCHITECTURE.md](MASTER_ARCHITECTURE.md) dòng 80, 128, 140, 209. Quota đang khóa `K=3` và đang giao Admin.

Thi thử và thi thật do trưởng bộ môn cấu hình. Giảng viên đóng góp câu hỏi. Trưởng bộ môn duyệt.

## MF-01

Số câu hỏi phụ mặc định là 2. Admin cấu hình trong khoảng 1 đến 5. Chỉ áp dụng cho MF-01.

❌ Đang lệch với [MASTER_ARCHITECTURE.md](MASTER_ARCHITECTURE.md) dòng 290. Đang ghi MF-01 kích hoạt follow-up đúng một lần, và còn cổng `has_follow_up`.

❌ Đang lệch với [KIEN_TRUC_HE_THONG.drawio](KIEN_TRUC_HE_THONG.drawio) ô follow-up, dòng 91. Cùng câu kích hoạt đúng một lần.

❌ Đang lệch với [diagrams/KIEN_TRUC_HE_THONG.drawio](diagrams/KIEN_TRUC_HE_THONG.drawio) ô follow-up, dòng 138. Cùng câu kích hoạt đúng một lần.

Hỏi phụ chỉ khi sinh viên chọn Per-question và điểm từ 4.0 đến 8.0. Full-session không có câu hỏi phụ.

Cấu trúc đề luyện tập full-session: sinh viên chọn Practice, rồi Full-session. Với từng mức Dễ, Trung bình, Khó, sinh viên tự nhập số câu muốn làm. Admin chỉ cấu hình min và max cho phép của từng mức, không đặt sẵn 2 dễ, 2 trung bình, 2 khó. Không mượn mức khác nếu kho thiếu câu. Per-question vẫn làm từng câu, không dùng bộ số này. Cách chấm chọn một lần khi mở phiên. Không do giảng viên đặt.

❌ Đang lệch với [MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md](MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) dòng 54. Đang ghi sinh viên chọn một độ khó. Chưa có chỗ sinh viên nhập số câu từng mức, cũng chưa có min và max do Admin đặt.

Luyện tập không lưu audio. Chỉ lưu transcript đã sửa.

Sinh viên xem lại lịch sử luyện tập. Cùng một màn với lịch sử thi thử. Mục 6 ghi thêm `Get Practice History` trong FE-02.

Thời gian sửa transcript ở phần luyện tập do Admin cấu hình. Khoảng 10 đến 300 giây, mặc định 60. Giảng viên và trưởng bộ môn không sửa số này.

❌ Đang lệch với [API_CONTRACT_AND_INTEGRATION_GUIDE.md](API_CONTRACT_AND_INTEGRATION_GUIDE.md) dòng 217 đến 220. Đang ghi cả admin, giảng viên và trưởng bộ môn cùng sửa cấu hình môn, gồm thời gian đệm luyện tập.

## MF-02

Cấu trúc đề thi thử, gồm phân bổ Bloom, do trưởng bộ môn cấu hình. Không dùng cách sinh viên tự nhập số câu dễ, trung bình, khó của MF-01.

❌ Đang lệch với [MASTER_ARCHITECTURE.md](MASTER_ARCHITECTURE.md) dòng 777. Đang ghi bốc đề cân đối cố định theo 6 mức Bloom.

❌ Đang lệch với [Task Daily/MVP_2_WEEKS_MASTER_PLAN.md](Task%20Daily/MVP_2_WEEKS_MASTER_PLAN.md) dòng 59. Đang ghi bốc đề theo tỷ lệ Bloom cố định.

❌ Đang lệch với [diagrams/KIEN_TRUC_HE_THONG.drawio](diagrams/KIEN_TRUC_HE_THONG.drawio) dòng 88 và [KIEN_TRUC_HE_THONG.drawio](KIEN_TRUC_HE_THONG.drawio) dòng 58. Đang ghi `ExamGeneratorService` bốc đề cân đối Bloom 6 mức.

Sinh viên chỉ chọn môn và có hoặc không follow-up. Không chọn topic. Không chọn độ khó.

Follow-up MF-02 do trưởng bộ môn cấu hình. AI hỏi theo nội dung, không theo điểm 4.0 đến 8.0.

❌ Đang lệch với [MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md](MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) dòng 202 và 214. Đang ghi giảng viên đặt tối đa 1–2 câu, mặc định 1.

Có follow-up thì thời lượng ca dài hơn. Thời lượng do trưởng bộ môn cấu hình.

❌ Chưa có câu này trong [MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md](MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md). Cần thêm ở giai đoạn thi thử, gần bước chọn follow-up.

Không lưu audio. Chỉ lưu transcript.

Số lượt thi thử do trưởng bộ môn cấu hình. Không khóa cứng 3. Không do Admin đặt.

❌ Đang lệch với [MASTER_ARCHITECTURE.md](MASTER_ARCHITECTURE.md) dòng 80.

❌ Đang lệch với [README.md](README.md) dòng 19. Đang ghi Quota Guard `K=3`.

❌ Đang lệch với [frontend/FE_TASK_BOARD.md](../frontend/FE_TASK_BOARD.md) dòng 13. Đang ghi modal chặn hạn ngạch `K=3`.

❌ Đang lệch với [Task Daily/MVP_2_WEEKS_MASTER_PLAN.md](Task%20Daily/MVP_2_WEEKS_MASTER_PLAN.md) dòng 26 và 82.

❌ Đang lệch với [KIEN_TRUC_HE_THONG.drawio](KIEN_TRUC_HE_THONG.drawio) dòng 13 và [diagrams/KIEN_TRUC_HE_THONG.drawio](diagrams/KIEN_TRUC_HE_THONG.drawio) dòng 21. Đang ghi quota `K=3`.

Hết giờ thì khóa, tự nộp. Câu chưa làm tính là bỏ trống, không chấm.

❌ Chưa ghi “câu chưa làm là bỏ trống, không chấm” trong [MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md](MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) dòng 222. Dòng này chỉ ghi tự nộp khi hết giờ.

## MF-03

AI sinh câu hỏi, rubric và sample answer từ CLO có sẵn trên syllabus. Giảng viên không phải chọn CLO1, CLO2. Bấm sinh là dùng CLO của syllabus. Muốn thu hẹp thì chọn topic trước, rồi chọn CLO.

❌ Đang lệch với [MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md](MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) dòng 25 và 259. Đang ghi giảng viên dùng AI sinh câu hỏi theo barem của mình.

❌ Đang lệch với [MASTER_ARCHITECTURE.md](MASTER_ARCHITECTURE.md) dòng 82, 799 và 1055. Dòng 82 cùng câu barem của giảng viên. Dòng 799 và 1055 đang bắt chọn từng CLO.

❌ Đang lệch với [README.md](README.md) dòng 19, 34 và 55. Cùng câu sinh câu hỏi theo barem của giảng viên.

Giảng viên vẫn soạn tay câu hỏi, rubric và sample answer. Trưởng bộ môn duyệt rồi câu mới vào kho. Trưởng bộ môn cũng tự sinh đề được.

Sau khi có câu, giảng viên tick kho. Hai ô: `practice_questions` và `exam_questions`. Tick một ô hoặc cả hai.

❌ Chưa có hai ô tick này trong [MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md](MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) mục MF-03. Cần thêm sau bước sinh hoặc soạn câu.

Hệ thống chỉ đọc syllabus từ FLM. Không ghi ngược lên FLM.

Giảng viên xem danh sách câu hỏi, xem danh sách rubric và tạo rubric. Rubric vẫn phải cộng đủ 10.0. Ba việc này nằm trong FE-04, không tách FE riêng. API đã có tạo rubric.

❌ Mục 6 trước đó thiếu ba dòng này. API đã có tại [API_CONTRACT_AND_INTEGRATION_GUIDE.md](API_CONTRACT_AND_INTEGRATION_GUIDE.md) mục 5.2, dòng 267.

## MF-04

Thi thật có lưu audio để hậu kiểm.

Số câu hỏi phụ mặc định là 2. Trưởng bộ môn cấu hình trong khoảng 1 đến 5.

❌ Đang lệch với [MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md](MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) dòng 26, 407, 441 và 507. Dòng 26, 407, 441 đang ghi 1–2 câu. Dòng 507 đang ghi tối đa 1 câu mỗi câu hỏi.

❌ Đang lệch với [MASTER_ARCHITECTURE.md](MASTER_ARCHITECTURE.md) dòng 88. Đang ghi follow-up MF-04 là 1–2 câu.

❌ Đang lệch với [frontend/FE_TASK_BOARD.md](../frontend/FE_TASK_BOARD.md) dòng 274. Đang ghi 1–2 câu.

❌ Đang lệch với [Task Daily/MVP_2_WEEKS_MASTER_PLAN.md](Task%20Daily/MVP_2_WEEKS_MASTER_PLAN.md) dòng 62. Đang ghi follow-up 1–2 câu.

Nộp Phòng Khảo thí làm cả file Excel và file PDF.

❌ Đang lệch với [MASTER_ARCHITECTURE.md](MASTER_ARCHITECTURE.md) dòng 156. Đang chỉ có export `.xlsx`, chưa có PDF.

Điểm danh bằng thẻ sinh viên và căn cước làm ở phòng lab. Giám thị tick trên hệ thống là đã đối chiếu.

AI chấm trên transcript sinh viên đã sửa. Hậu kiểm nghe file audio gốc.

Khi chấm, hệ thống tự tính điểm tin cậy của bài và tự xếp bài vào nhóm cần xem lại hoặc nhóm tin cậy cao. Giảng viên chỉ xem con số đó. Giảng viên không tự gắn bài là bình thường hay đáng nghi.

Thứ tự điểm thi thật: thi xong thì AI chấm, giảng viên kiểm tra, rồi công bố điểm. Sinh viên xem điểm. Đồng ý thì xác nhận nhận điểm. Không đồng ý thì gửi đơn phúc khảo.

Ca thi ghi phòng lab ngay trên ca, không có danh mục phòng riêng. Ca thi gán người coi thi. Người coi thi có thể là giảng viên hoặc giám thị. Cách làm bài của kỳ thi do trưởng bộ môn chọn, trong hai cách đã chốt.

Sinh viên và trưởng bộ môn xem danh sách đơn phúc khảo. Sinh viên chỉ thấy đơn của mình.

❌ Mục 6 cần thêm `Set Examination Room`, `Assign Proctor`, `Configure Examination Input Mode` trong FE-05. Cột phòng và giám thị đã có trong [ERD_DATABASE_DESIGN.md](ERD_DATABASE_DESIGN.md) dòng 420 và 423.

❌ Mục 6 cần thêm `Acknowledge Published Grade` và `Get Appeals` trong FE-07. API đã có xác nhận nhận điểm tại [API_CONTRACT_AND_INTEGRATION_GUIDE.md](API_CONTRACT_AND_INTEGRATION_GUIDE.md) dòng 1534, và danh sách đơn tại mục 5.10.

Sau khi có đơn, trưởng bộ môn giao một giảng viên chấm lại. Trưởng bộ môn tự chấm cũng được, nhưng đơn nhiều thì khó, nên tạm thời vẫn để trưởng bộ môn giao. Tự giao đơn để sau này, chưa làm bây giờ.

❌ Đang lệch với [MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md](MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) dòng 415, 419, 421, 572 và 573. Đang ghi hệ thống tự gán đơn cho trưởng bộ môn, và trưởng bộ môn tự chấm, duyệt hoặc bác.

❌ Đang lệch với [MASTER_ARCHITECTURE.md](MASTER_ARCHITECTURE.md) dòng 91, 162, 211, 349, 847, 1234 và 1376. Đang ghi đơn gán thẳng cho trưởng bộ môn thẩm định.

❌ Đang lệch với [README.md](README.md) dòng 19 và 56. Đang ghi trưởng bộ môn thẩm định đơn phúc khảo.

❌ Đang lệch với [frontend/FE_TASK_BOARD.md](../frontend/FE_TASK_BOARD.md) dòng 190. Đang ghi đơn chuyển thẳng cho trưởng bộ môn và quyết định của trưởng bộ môn là quyết định cuối.

❌ Đang lệch với [Task Daily/MVP_2_WEEKS_MASTER_PLAN.md](Task%20Daily/MVP_2_WEEKS_MASTER_PLAN.md) dòng 65 và 69. Đang ghi trưởng bộ môn chấm lại.

❌ Đang lệch với [diagrams/MF04_Lab_Exam_Audit.drawio](diagrams/MF04_Lab_Exam_Audit.drawio) dòng 169. Đang ghi appeal giao trưởng bộ môn.

❌ Đang lệch với [MASTER_ARCHITECTURE.md](MASTER_ARCHITECTURE.md) dòng 81 và 405, và [MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md](MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) dòng 560 đến 562. Các dòng này khóa điểm vĩnh viễn ngay lúc công bố, chặn mọi sửa điểm. Phúc khảo lại xảy ra sau công bố, nên chỗ khóa phải chừa đường cho giảng viên được giao chấm lại.

Cách làm bài trong scope: `VoiceOnly` và `VoiceWithTranscriptEdit`. `VoiceAndTextInput` không làm bây giờ, để phát triển sau.

❌ Đang lệch với [MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md](MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) dòng 407, 442 và 485. Đang còn `VoiceAndTextInput`.

❌ Đang lệch với [MASTER_ARCHITECTURE.md](MASTER_ARCHITECTURE.md) dòng 90. Đang còn `VoiceAndTextInput`.

❌ Đang lệch với [frontend/FE_TASK_BOARD.md](../frontend/FE_TASK_BOARD.md) dòng 275. Đang còn `VoiceAndTextInput`.

❌ Đang lệch với [diagrams/MF04_Lab_Exam_Audit.drawio](diagrams/MF04_Lab_Exam_Audit.drawio) dòng 222. Đang còn nhánh `VoiceAndTextInput`.

Danh sách thí sinh của ca thi lấy từ lớp đã có trong hệ thống. Trưởng bộ môn chọn môn và lớp. Excel chỉ là đường dự phòng khi lớp chưa có.

❌ Đang lệch với [MASTER_ARCHITECTURE.md](MASTER_ARCHITECTURE.md) dòng 156. Import Excel đang là đường chính, chưa có bước lấy danh sách từ lớp.

## Mục 6 đã bổ sung

Người chốt: Nguyễn Trọng Tốt. Ngày: 07/10/2026. FE-09, FE-10 và FE-11 đã có trong report. Report còn thiếu các dòng đã ghi ở MF-01, MF-03, MF-04 và phần học kỳ, nhật ký bên dưới.

### FE-09 User Management

Admin xem danh sách người dùng, xem chi tiết, khóa hoặc mở tài khoản, gán vai trò. Năm vai trò cố định: sinh viên, giảng viên, trưởng bộ môn, giám thị, admin. Sinh viên tự đăng ký bằng Google. Không tạo user bằng email và mật khẩu.

❌ Đang lệch với [ERD_DATABASE_DESIGN.md](ERD_DATABASE_DESIGN.md) dòng 141. Bảng `users` còn `password_hash`. Sản phẩm không dùng mật khẩu.

### FE-10 Dashboard

Có trang chủ của sinh viên, giảng viên và trưởng bộ môn. Giám thị không có trang chủ riêng. Giám thị dùng màn phòng thi. Admin dùng màn cấu hình và màn hàng đợi chấm lỗi.

### FE-11 Notification

Hộp thư trong web. Người dùng xem danh sách và đánh dấu đã đọc. Không gửi email.

Hệ thống tạo thông báo khi: lịch thi được công bố, gửi sinh viên; điểm thi thật được công bố, gửi sinh viên; trưởng bộ môn giao chấm lại một đơn, gửi giảng viên được giao; câu hỏi chờ duyệt, gửi trưởng bộ môn; câu hỏi bị trả về sửa, gửi giảng viên.

Những câu hiện ngay trên màn đang mở thì không vào hộp thư: sai máy thi, micro chưa đạt, bài đã lưu an toàn, hết lượt thi thử, điểm luyện tập hoặc thi thử trả về trong phiên.

❌ Chưa có bảng thông báo trong [ERD_DATABASE_DESIGN.md](ERD_DATABASE_DESIGN.md). Chưa có API thông báo trong [API_CONTRACT_AND_INTEGRATION_GUIDE.md](API_CONTRACT_AND_INTEGRATION_GUIDE.md). [frontend/FE_TASK_BOARD.md](../frontend/FE_TASK_BOARD.md) chưa có màn hộp thư.

### FE-08 thêm học kỳ và nhật ký

Admin tạo và sửa học kỳ. Học kỳ có mã, tên, ngày bắt đầu, ngày kết thúc, và trạng thái đang dùng. Môn học và lớp trỏ tới học kỳ đó. Mục 6 ghi `Get Semesters`, `Create Semester`, `Update Semester` trong FE-08. Không xóa học kỳ.

❌ Đang lệch với [API_CONTRACT_AND_INTEGRATION_GUIDE.md](API_CONTRACT_AND_INTEGRATION_GUIDE.md) mục 5.1, dòng 181. API chỉ có lấy danh sách học kỳ. Chưa có tạo và sửa.

❌ [frontend/FE_TASK_BOARD.md](../frontend/FE_TASK_BOARD.md) chưa có màn tạo và sửa học kỳ.

Admin xem nhật ký kiểm toán. Mục 6 thêm `Get Audit Logs`, cùng FE-08 với hàng đợi chấm lỗi.
❌ [frontend/FE_TASK_BOARD.md](../frontend/FE_TASK_BOARD.md) chưa có màn nhật ký. API đã có `GET /api/v1/admin/audit-logs` tại [API_CONTRACT_AND_INTEGRATION_GUIDE.md](API_CONTRACT_AND_INTEGRATION_GUIDE.md) mục 5.9, dòng 1777.

---

# Đợt cập nhật & Chốt kỹ thuật lần 2 (Triển khai & Chuẩn hóa Hệ thống)

Người chốt: Nguyễn Quang Thành (Team Leader & Lead Backend Architect). Ngày: 08/10/2026.

## 1. Phân công nhân sự & Ranh giới trách nhiệm (Teamwork Alignment)

- **Nguyễn Quang Thành:** Chủ trì toàn diện Backend MF-01 (Interactive Practice) từ A-Z, Backend MF-02 (Timed Mock Exam) và Module Báo cáo Khảo thí Phòng thi (Excel `.xlsx` + PDF chữ ký số).
- **Nguyễn Trọng Tốt:** Phụ trách Auth Google, toàn bộ Backend MF-03 (FLM Syllabus AI Generator, Rubric Studio), toàn bộ Backend MF-04 (Thi thật Lab Kiosk, Audio Stream R2, Background Grading Worker, One-Way Lock, Phúc khảo Internal Appeals), và các API quản trị phụ (FE-08 Học kỳ CRUD, FE-09 Quản lý User, FE-11 Hộp thư thông báo).
- **Phân định rõ với Frontend:** Team Backend (Thành & Tốt) **tuyệt đối KHÔNG code trước giao diện React 19 trong `frontend/src/`**. Giao diện để toàn quyền cho Hoàng & Hải tự thiết kế và code sau. Backend chỉ cung cấp tài liệu đặc tả API chuẩn (`MF01_Frontend_Integration.md`) và file công cụ kiểm thử chạy trực tiếp (`MF01_Mini_Tester.html`) để Frontend tự nối vào.
- **Nguyễn Đăng Hải:** DB Specialist & Frontend Developer. Hải 100% không code C# Backend. Hải chuyên trách Database Schema, EF Core Migrations, và phụ trách Frontend các module: MF-02 (Thi thử Voice-First), Admin (FE-08 Học kỳ, FE-09 Người dùng), và Trưởng Bộ Môn (Duyệt đề MF-03, Quản lý ca thi & Giao đơn phúc khảo MF-04).
- **Lê Vũ Hoàng:** Lead Frontend Architect & Fullstack Coordinator. Phụ trách điều phối kiến trúc Frontend, phụ trách trực tiếp UI/UX MF-01 (Luyện tập, Buffer Screen 60s, Web Speech API, SignalR Hook), UI Kiosk phòng Lab MF-04, Cổng Hậu kiểm Evidence Panel & Waveform Audio Player, Dashboard Sinh viên / Giảng viên (FE-10), và Hộp thư Thông báo (FE-11).

## 2. Thay đổi nghiệp vụ & Bổ sung chức năng MF-01 (Interactive Practice)

- **Số lượng câu hỏi cho CẢ 2 CHẾ ĐỘ:** Cho phép sinh viên tự nhập số lượng câu hỏi luyện tập cho **cả chế độ `[Per-Question]` (luyện từng câu) và `[Full-Session]` (luyện trọn gói)**, từ 1 đến 10 câu (giới hạn tối đa đọc động từ cấu hình `MaxPracticeQuestionsPerSession` trong bảng `system_configs`).
- **Tùy chọn độ khó "progressive" ("Ngẫu nhiên từ dễ đến khó"):**
  - Bổ sung tùy chọn `progressive` áp dụng cho cả Per-Question và Full-Session.
  - Sinh viên bắt buộc nhập từ **3 đến 10 câu** (ràng buộc bởi `MinMixedPracticeQuestions = 3` và `MaxMixedPracticeQuestions = 10` do Admin cấu hình trong `system_configs`).
  - Thuật toán bốc đề: Lấy câu hỏi chia đều các mức (Dễ, Trung bình, Khó) và sắp xếp thứ tự phát vấn tăng dần từ Dễ $\to$ Trung bình $\to$ Khó.
  - Nếu kho đề không đủ câu cho bất kỳ mức nào, hệ thống trả về mã lỗi `HTTP 400 Bad Request` tiếng Việt rõ ràng, không tạo phiên rác.
- **Loại bỏ hoàn toàn lưu Audio cho MF-01 và MF-02:**
  - File ghi âm của sinh viên chỉ stream trực tiếp qua Cloudflare Whisper STT để lấy transcript tức thì phục vụ Buffer Screen.
  - Không upload lên Cloudflare R2, không băm SHA-256.
  - Xóa bỏ cột `AudioUrl` khỏi bảng `practice_answers` (chỉ lưu audio cho thi thật MF-04 để phục vụ hậu kiểm pháp lý).
- **Bổ sung API Hoàn thành phiên (`POST /api/v1/practice/sessions/{id}/complete`):**
  - Cập nhật trạng thái phiên sang `status = "completed"`, `completed_at = DateTime.UtcNow`.
  - Có tính chất Idempotent (gọi nhiều lần không lỗi, không ghi đè trùng lặp).
  - Hỗ trợ xác thực sinh viên qua cả `X-User-Id` header lẫn query param `?studentId=...` linh hoạt cho Frontend và công cụ test.
- **Bổ sung API Lịch sử luyện tập (`GET /api/v1/practice/student/history`):**
  - Trả về danh sách các phiên luyện tập của sinh viên kèm điểm trung bình, số câu đã trả lời để hiển thị chung với lịch sử thi thử tại Student Portal (FE-02).
- **Follow-up Engine đa nấc trong Background Worker:**
  - `GradingQueueWorker` kiểm tra số lượng câu hỏi phụ thực tế đã tạo so với `MaxPracticeFollowUpQuestions` (1–5 câu do Admin cấu hình trong `system_configs`, mặc định 2 câu).
  - Chỉ kích hoạt khi điểm số rơi vào ranh giới $4.0 \le \text{Score} \le 8.0$ và chưa vượt quá số câu phụ tối đa.
- **Tối ưu AsNoTracking:** Toàn bộ truy vấn đọc câu hỏi từ DB trong `StartPracticeSessionCommandHandler` được gắn `.AsNoTracking()` để tối ưu hiệu năng.

## 3. Công cụ & Tài liệu Bàn giao Tích hợp Frontend

- **Công cụ kiểm thử chạy trực tiếp `MF01_Mini_Tester.html`:** File HTML độc lập, mở thẳng trên Chrome/Edge có tích hợp sẵn thư viện SignalR, cho phép test toàn bộ luồng: Khởi tạo phiên (Progressive 3-10 câu), Upload audio Whisper STT, Sửa transcript, Nộp câu trả lời Persist First, AI chấm ngầm, Bắt câu hỏi phụ Follow-up realtime, Hoàn thành phiên và Tra cứu lịch sử.
- **Tài liệu bàn giao `MF01_Frontend_Integration.md`:** Đặc tả đầy đủ 7 endpoints, chuẩn lỗi RFC 7807 Problem Details, hook `usePracticeHub.ts` React 19, đặc tả logic Buffer Screen 60s và Follow-up Engine.

## 4. Cơ sở dữ liệu & Cấu hình Hệ thống (System Configs)

- **Tạo bảng `system_configs`:** Lưu trữ dạng Key-Value chuẩn Enterprise để Admin cấu hình:
  - `MinMixedPracticeQuestions`: `3`
  - `MaxMixedPracticeQuestions`: `10`
  - `TranscriptBufferSeconds`: `60`
  - `MaxPracticeQuestionsPerSession`: `10`
  - `MaxPracticeFollowUpQuestions`: `2`
- **Xóa cột `password_hash` trong bảng `users`:** Chuyển hoàn toàn sang Google OAuth2, không hỗ trợ đăng nhập mật khẩu truyền thống.
- **Bảng `notifications`:** Đã chuẩn hóa phục vụ Hộp thư thông báo in-app FE-11.
- **Cột IP Kiosk phòng thi:** Thống nhất dùng duy nhất tên cột `ip_address` (loại bỏ hoàn toàn tên cũ `workstation_ip`).
## 5. Khắc phục triệt để các phát hiện từ Báo cáo Kiểm toán Kỹ thuật (Audit Report Resolutions)

- **Loại bỏ hoàn toàn `VoiceAndTextInput` (Cấp độ 2 - Warning resolved):**
  - Xóa bỏ hằng số `VoiceAndTextInput` khỏi `DomainEnums.ExamInputMode`. Mảng `All` hiện chỉ gồm 2 giá trị chuẩn hóa: `VoiceOnly` và `VoiceWithTranscriptEdit`.
  - Cập nhật giá trị mặc định của `ExamInputMode` trong các Entity (`Course.cs`, `OfficialExamSession.cs`), DTO (`CourseConfigurationDto.cs`), Fluent API mapping (`OralExamDbContext.cs`) và EF Core ModelSnapshot thành `ExamInputMode.VoiceWithTranscriptEdit`.
  - Cập nhật `UpdateCourseConfigurationCommandValidator` chỉ chấp nhận `VoiceOnly` hoặc `VoiceWithTranscriptEdit`.
  - Cập nhật các bài test đối kháng trong `AdversarialMilestone1ChallengerTests.cs` và `AdversarialMilestone1ModelIntegrityChallengerTests.cs`: chuyển `VoiceAndTextInput` thành ca kiểm thử bị REJECT (`HTTP 400` / Validation Failure).
- **Seed đầy đủ 5 khóa cấu hình Admin cho `system_configs` (Cấp độ 2 - Warning resolved):**
  - Cấu hình `HasData` trong `OralExamDbContext.cs`, `OralExamDbContextModelSnapshot.cs` và `02_seed.sql` với đầy đủ 5 cấu hình:
    1. `MaxPracticeQuestionsPerSession`: `10`
    2. `MinMixedPracticeQuestions`: `3`
    3. `MaxMixedPracticeQuestions`: `10`
    4. `TranscriptBufferSeconds`: `60`
    5. `MaxPracticeFollowUpQuestions`: `2`
  - Bổ sung test kiểm thử `ADV-M1-09` xác thực 100% metadata Seed Data của EF Core.
- **Thống nhất quyền cấu hình `TranscriptBufferSeconds` (Cấp độ 3 - Code Hygiene resolved):**
  - `StartPracticeSessionCommandHandler` ưu tiên đọc cấu hình `TranscriptBufferSeconds` từ bảng `system_configs` do Admin quản trị (fallback về cấu hình của Course hoặc 60s).
  - Bổ sung 2 bài unit tests kiểm chứng cơ chế ưu tiên đọc cấu hình từ Admin và cơ chế fallback an toàn.
- **Kiểm chứng kỹ thuật:** 516/516 unit tests passed 100% (Exit Code 0), .NET 8 build hoàn toàn sạch 0 Error.
