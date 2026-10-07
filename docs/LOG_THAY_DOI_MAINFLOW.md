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
