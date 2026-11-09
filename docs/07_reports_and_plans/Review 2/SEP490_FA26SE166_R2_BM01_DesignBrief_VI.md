# SEP490 · CAPSTONE PROJECT · REVIEW 2 · BM01 — DESIGN BRIEF

---
## 0. Thông tin chung (Header)

| Trường thông tin | Nội dung |
| :--- | :--- |
| **Project code** | `FA26SE166` |
| **Project name** | An LLM-based Oral Examination Practice and Assessment System for Software Engineering Major |
| **Team** | Nguyễn Quang Thành, Nguyễn Trọng Tốt, Phạm Nguyễn Đăng Hải, Lê Vũ Hoàng |
| **Supervisor** | TS. Nguyễn Thị Cẩm Hương |
| **Repository link (tag `review-2`)** | `https://github.com/FA26SE166/oral-exam-system/tree/review-2` |
| **Version** | `1.0` |
| **Date** | `28/10/2026` |

---

## 1. Phạm vi đề tài (Scope)

### 1.1. Tóm tắt thay đổi so với Phiếu đăng ký hiện hành (Registration Changes)

| Mục | Hiện tại (Phiếu đăng ký gốc) | Đề xuất điều chỉnh | Lý do |
| :--- | :--- | :--- | :--- |
| **Vai trò người dùng** | Chỉ chia 2 nhóm chung là Giảng viên/Admin và Sinh viên. | Tách thành 6 vai trò: Sinh viên, Giảng viên, Trưởng bộ môn, Cán bộ Khảo thí, Giám thị và Quản trị viên (kèm chuỗi phân cấp học thuật 4 cấp: Admin tạo học kỳ/môn & gán Trưởng BM → Trưởng BM phân công nhiều GV → GV tạo lớp, mật khẩu lớp & ca thi trong lớp có OTP xoay vòng 5 phút → SV tự tham gia). | Đảm bảo phân tách nhiệm vụ rõ ràng giữa quản trị hệ thống, quản lý học thuật/duyệt đề, tổ chức khảo thí, giảng dạy/hậu kiểm, coi thi phòng máy và phúc khảo độc lập. |
| **Ngân hàng câu hỏi** | Phân loại câu hỏi theo môn, chương và độ khó. | Phân loại theo môn học, chuẩn đầu ra (CLO) và mức độ nhận thức Bloom; tách độc lập luồng tạo câu hỏi luyện tập/thi thử (`practice_questions`) với luồng tạo câu hỏi thi thật (`exam_questions`) bám sát đề thi mẫu / ma trận cấu trúc đề của Trưởng bộ môn. | Phù hợp với cấu trúc đề cương môn học của trường, chuẩn hóa ma trận đề thi chính thức và ngăn việc lộ câu hỏi thi thật trong quá trình sinh viên tự luyện tập. |
| **Hình thức trả lời** | Cho phép trả lời bằng giọng nói hoặc gõ văn bản. | Câu trả lời phải được ghi âm qua microphone trước (thay vì nhập trực tiếp bằng văn bản), sau đó sinh viên chỉ chỉnh sửa lại bản phiên âm (như thuật ngữ tiếng Anh chuyên ngành bị nhận diện sai) trước khi gửi. | Giữ đúng bản chất kiểm tra vấn đáp nhưng vẫn khắc phục được lỗi nhận diện âm thanh đối với thuật ngữ chuyên ngành Kỹ thuật phần mềm. |
| **Quy trình thi thật & chấm điểm** | Giảng viên hậu kiểm, và nhập điểm so sánh giữa điểm AI và file nghe. | Cán bộ Khảo thí tạo kỳ/ca thi, xuất bản đề và quản lý DLQ thi thật; Trưởng bộ môn cấu hình câu hỏi phụ (1–5 câu, mặc định 2); Giám thị chỉ điểm danh (ghế 1–40, đối chiếu thẻ SV/CCCD) và báo cáo vi phạm; Giảng viên hậu kiểm toàn bộ bài nộp trước khi công bố điểm 1 lần (khóa một chiều) và hỗ trợ quy trình phúc khảo 5 bước qua Khảo thí & Trưởng bộ môn. | Chuẩn hóa pháp lý phòng thi tập trung, giảm tải chấm sơ bộ cho giảng viên nhưng vẫn giữ quyền kiểm soát đề thi, công bố điểm và phán quyết phúc khảo minh bạch. |

### 1.2. Ngoài phạm vi (Out of Scope)

| Hạng mục | Lý do |
| :--- | :--- |
| **Lưu trữ ghi âm cho chế độ Luyện tập và Thi thử** | Hai chế độ này dùng cho mục đích tự học hàng ngày và không yêu cầu lưu bằng chứng pháp lý như kỳ thi thật. |
| **Tích hợp gọi Video Call trực tuyến và giám sát Camera** | Kỳ thi chính thức được tổ chức tập trung tại phòng máy của trường dưới sự giám sát trực tiếp của Giám thị. |
| **Tự đăng ký tài khoản công khai** | Hệ thống sử dụng nội bộ trong trường; chỉ các tài khoản được nhà trường cấp phép mới được truy cập. |
| **Chấm điểm tức thì hoặc khiếu nại trực tiếp tại máy phòng thi** | Điểm thi chính thức cần được Giảng viên hậu kiểm toàn bộ ca thi trước khi công bố trên cổng thông tin sinh viên. |

---

## 2. Tổng quan hệ thống (System at a Glance)
*(Tối đa 150 từ)*

**Hệ thống Luyện thi và Đánh giá Vấn đáp bằng LLM (OES)** là nền tảng web nội bộ hỗ trợ sinh viên ngành Kỹ thuật phần mềm rèn luyện kỹ năng trả lời vấn đáp và giúp nhà trường tổ chức kỳ thi vấn đáp chuẩn hóa. Hệ thống phục vụ 6 vai trò (**Sinh viên, Giảng viên, Trưởng bộ môn, Cán bộ Khảo thí, Giám thị, Quản trị viên**) theo phân cấp học thuật 4 cấp, vận hành 4 luồng chính (luyện tập tự do, thi thử có hạn ngạch, xây dựng/duyệt ngân hàng câu hỏi kèm Rubric theo CLO, và thi thật phòng máy kết hợp hậu kiểm, phúc khảo 5 bước) cùng kiểm tra trong lớp có mật khẩu xoay vòng 5 phút. **Trưởng bộ môn** duyệt đề và cấu hình câu hỏi phụ; **Cán bộ Khảo thí** tổ chức ca thi, xuất bản đề và công bố điểm. Xem **Sơ đồ Kiến trúc Tổng quan (SDD, Mục 2.1)**.

---

## 3. Các Quyết Định Thiết Kế Chính (Key Design Decisions)

| ID | Quyết định (Decision) | Các phương án đã cân nhắc (Options considered) | Phương án chọn & Lý do (Chosen option and rationale) | Người phụ trách (Owner) | Liên kết FR / Sơ đồ (Related FR / diagram) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **D1** | **Kiến trúc ứng dụng và xử lý chấm điểm** | Chấm điểm trực tiếp trong lúc chờ phản hồi; máy chủ phân tầng kết hợp hàng đợi chấm điểm chạy ngầm | Chọn máy chủ phân tầng kết hợp hàng đợi chấm điểm chạy ngầm: lưu bài nộp vào cơ sở dữ liệu ngay lập tức để không làm treo giao diện phòng thi và xử lý chấm điểm bất đồng bộ. | **Nguyễn Quang Thành** | Luồng Luyện tập & Thi thật; Sơ đồ Kiến trúc |
| **D2** | **Vai trò hỗ trợ của AI và quyền quyết định của con người** | Để AI tự động phát hành câu hỏi và tự quyết định điểm số; dùng AI làm công cụ gợi ý có giải thích dưới sự kiểm soát của giảng viên | AI chỉ hỗ trợ sinh câu hỏi nháp theo CLO và chấm điểm sơ bộ kèm lập luận theo Rubric; nội dung AI sinh ra luôn cho phép chỉnh sửa và không bao giờ tự động duyệt câu hỏi hay tự chốt điểm thi chính thức. | **Nguyễn Trọng Tốt** | Sinh câu hỏi AI & Chấm điểm theo Rubric |
| **D3** | **Phân tách 6 vai trò RBAC, kiểm soát phòng thi và bảo vệ điểm số** | Phân quyền chung cho giảng viên; tách 6 vai trò chuyên trách giữa quản trị, học thuật, khảo thí, coi thi, hậu kiểm và phúc khảo 5 bước | Trưởng bộ môn duyệt câu hỏi và cấu hình câu hỏi phụ thi thật (1–5 câu, mặc định 2); Cán bộ Khảo thí tạo kỳ/ca thi (phân công đồng thời Giám khảo chấm thi & Giám thị), xuất bản đề, quản lý DLQ thi thật và tiếp nhận/thẩm tra đơn phúc khảo; Giám thị chỉ điểm danh (ghế 1–40, thẻ SV/CCCD) và báo cáo vi phạm; Giảng viên hậu kiểm và phối hợp công bố điểm 1 lần (khóa một chiều). Khi phúc khảo, Khảo thí và Trưởng bộ môn cùng nhận thông báo in-app; Khảo thí phân công Giảng viên độc lập chấm lại (định mức tối đa 24 bài/GV, chia đều nếu tổng bài $> \text{GV} \times 24$, cân bằng tải và điều chỉnh thủ công nếu $\le \text{GV} \times 24$) và phê duyệt quyết định cuối cùng. | **Nguyễn Trọng Tốt** | Quy trình Duyệt câu hỏi, Công bố điểm & Phúc khảo |
| **D4** | **Kiểm soát đầu vào giọng nói và lưu trữ bằng chứng thi** | Cho phép nhập trực tiếp câu trả lời bằng văn bản và lưu âm thanh mọi chế độ; yêu cầu ghi âm trước kèm bước chỉnh sửa bản phiên âm và chỉ lưu ghi âm cho kỳ thi thật | Yêu cầu câu trả lời phải được ghi âm qua microphone trước, sau đó cho phép sinh viên chỉnh sửa bản phiên âm (sửa thuật ngữ chuyên ngành bị nhận diện sai) trước khi gửi; chỉ niêm phong và lưu trữ tệp ghi âm cho kỳ thi chính thức tại phòng máy để làm bằng chứng hậu kiểm. | **Lê Vũ Hoàng** | Luồng Luyện tập, Thi thử & Phòng thi Kiosk |
| **D5** | **Tách biệt 2 luồng tạo ngân hàng câu hỏi và chụp tiêu chí Rubric** | Dùng chung một kho câu hỏi; tách độc lập luồng câu hỏi luyện tập/thi thử với luồng câu hỏi thi thật kèm bản chụp kết quả Rubric | Tách độc lập luồng tạo câu hỏi luyện tập/thi thử (`practice_questions` theo CLO) và luồng tạo câu hỏi thi thật (`exam_questions` bám sát đề thi mẫu / ma trận cấu trúc đề của Trưởng bộ môn) để bảo mật đề thi; lưu bản chụp tiêu chí Rubric trên từng bài nộp để việc sửa câu hỏi về sau không làm lệch điểm cũ. | **Phạm Nguyễn Đăng Hải** | Quản lý Ngân hàng câu hỏi; Sơ đồ ERD |

---

## 4. Các Rủi Ro Chính (Top Risks)

| ID | Rủi ro (Risk) | Tác động (Impact) | Cách kiểm chứng hoặc giảm thiểu (How we verify or mitigate) | Người phụ trách (Owner) |
| :--- | :--- | :--- | :--- | :--- |
| **R1** | **Dịch vụ AI hoặc nhận diện giọng nói bên thứ ba phản hồi chậm hoặc gián đoạn trong ca thi** | Kết quả chấm điểm sơ bộ bị trễ, có nguy cơ làm gián đoạn quá trình nộp bài của sinh viên. | Lưu câu trả lời của sinh viên vào cơ sở dữ liệu trước khi gọi AI; xử lý chấm điểm ở tiến trình nền có cơ chế tự động thử lại và lưu hàng đợi lỗi (DLQ) để chấm bù (Admin giám sát/replay DLQ luyện tập & thi thử; Cán bộ Khảo thí giám sát hàng đợi chấm thi thật). | **Nguyễn Trọng Tốt** |
| **R2** | **Nhận diện giọng nói ghi nhận sai các thuật ngữ tiếng Anh chuyên ngành Kỹ thuật phần mềm** | Nội dung văn bản gửi cho AI bị sai lệch so với ý trả lời thực tế của sinh viên, dẫn đến chấm điểm thiếu chính xác. | Cung cấp bước rà soát bản gỡ băng trong thời gian giới hạn để sinh viên chỉnh sửa các thuật ngữ chuyên ngành bị nhận diện sai trước khi xác nhận nộp, kết hợp bộ chuẩn hóa từ vựng kỹ thuật. | **Nguyễn Quang Thành** |
| **R3** | **Sinh viên chuyển cửa sổ trên máy thi hoặc điểm số bị thay đổi trái phép sau khi công bố** | Làm mất tính nghiêm túc của kỳ thi chính thức và độ tin cậy của kết quả đánh giá. | Kiểm tra máy trạm theo vị trí chỗ ngồi, khóa giao diện toàn màn hình, tự động đình chỉ bài thi và báo Giám thị khi thoát màn hình hoặc mất tiêu điểm vượt mức cho phép; khóa chỉnh sửa trực tiếp ngay khi công bố điểm. | **Lê Vũ Hoàng** |

---

## 5. Các Điểm Lệch Đã Biết (Known Gaps)

| ID | Điểm chưa khớp (Gap) | Vị trí (Location) | Kế hoạch xử lý (Plan) | Người phụ trách (Owner) | Hạn (Due) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **G1** | Một số sơ đồ trong tài liệu thiết kế (SDD) đang ở dạng tệp vẽ rời, cần lưu kèm dạng mã nguồn sơ đồ trong kho mã nguồn theo quy định nộp hồ sơ. | Report 4 (SDD) & thư mục `docs/` | Cập nhật và bổ sung đầy đủ mã nguồn sơ đồ (Mermaid & Draw.io) cho sơ đồ kiến trúc, ERD, trạng thái và luồng nghiệp vụ vào kho mã nguồn. | **Nguyễn Quang Thành** | Trước hạn nộp Review 2 |
| **G2** | Nội dung chi tiết hóa về 6 vai trò người dùng, tách 2 luồng tạo câu hỏi và quy trình hậu kiểm/phúc khảo 5 bước trong SRS cần được cập nhật đồng bộ sang Phiếu đăng ký bản chốt. | Phiếu đăng ký & Biểu mẫu `BM03` | Kê khai đầy đủ các mục làm rõ phạm vi (6 vai trò RBAC, phân cấp học thuật 4 cấp, ca thi trong lớp và phúc khảo 5 bước) vào biểu mẫu `BM03 (Record of Changes)` đính kèm Phiếu đăng ký bản đề xuất chốt. | **Nguyễn Trọng Tốt** | Trước hạn nộp Review 2 |
| **G3** | Môi trường chạy demo tập trung (Staging) và kịch bản nạp dữ liệu mẫu tự động cần được rà soát đồng bộ lần cuối trước buổi bảo vệ. | Cấu hình triển khai & Database Seeder | Kiểm tra quy trình khởi tạo dữ liệu mẫu tự động và chạy thử toàn bộ kịch bản demo trên môi trường triển khai. | **Phạm Nguyễn Đăng Hải** | Trước buổi Review 2 |

---

## 6. Tính Năng Bắt Buộc Đề Xuất (Proposed Mandatory Features)

| ID | Tính năng (Feature) | Actor | Tiêu chí chấp nhận có thể kiểm thử (Acceptance criteria) | Mã Test | Trạng thái |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **F-01** | **Đăng nhập, phân quyền 6 vai trò và phân cấp học thuật** | Tất cả người dùng, Admin, Trưởng bộ môn, Giảng viên, Sinh viên | Chỉ tài khoản do trường cấp phép mới đăng nhập được và truy cập đúng chức năng theo 6 vai trò; hỗ trợ chuỗi phân cấp đào tạo chuẩn (Admin quản lý học kỳ, môn học & gán Trưởng BM → Trưởng BM phân công nhiều GV vào môn → GV tạo lớp, mật khẩu lớp & ca thi trong lớp cấu hình tên ca, 2 chế độ bốc đề chống trùng ALL_COURSE_BANK/SELECTED_POOL, tổng số câu 1–10, bật/tắt Follow-up 1–5 câu, mã OTP xoay vòng 5 phút, chấm song song đa luồng x12 đưa lên Dashboard Giảng viên; điểm chính thức của sinh viên được bảo mật ẩn cho đến khi Giảng viên hậu kiểm và bấm chốt sổ/publish điểm). | TC-F01 | Done |
| **F-02** | **Cấu hình tham số luyện tập và hạn ngạch thi thử** | Admin, Trưởng bộ môn | Admin thiết lập được các thông số phiên luyện tập và giới hạn số lượt thi thử mỗi ngày; Trưởng bộ môn cấu hình được cấu trúc câu hỏi và thời lượng thi thử cho môn học. | `TC-F02` | Done |
| **F-03** | **Luyện tập vấn đáp đa phương thức và chỉnh sửa thuật ngữ** | Sinh viên | Sinh viên bắt đầu được phiên luyện tập với câu hỏi chính lấy từ kho đã duyệt; hỗ trợ linh hoạt 2 chế độ trả lời gồm nhập văn bản trực tiếp hoặc ghi âm qua microphone kèm chỉnh sửa thuật ngữ chuyên ngành trên bản gỡ băng trước khi nộp; phiên không tương tác quá thời gian chờ tự động kết thúc. | TC-F03 | Done |
| **F-04** | **Chấm điểm luyện tập, hỏi phụ và xem lịch sử** | Sinh viên | Hệ thống trả kết quả chấm điểm theo tiêu chí Rubric, sinh câu hỏi phụ theo ngữ cảnh (chỉ dùng trong phiên, không lưu vào ngân hàng câu hỏi) khi câu trả lời cần làm rõ thêm và lưu lại lịch sử luyện tập. | `TC-F04` | Done |
| **F-05** | **Thi thử vấn đáp tính giờ có hạn ngạch** | Sinh viên | Hệ thống từ chối lượt thi mới nếu sinh viên đã dùng hết hạn ngạch trong ngày; câu hỏi chính lấy từ kho luyện tập đã duyệt, bắt buộc ghi âm câu trả lời qua microphone trước khi rà soát bản phiên âm và tự động thu bài khi hết giờ (Zero-R2: không lưu tệp âm thanh). | TC-F05 | Done |
| **F-06** | **Xem bảng điểm thi thử theo chuẩn đầu ra (CLO)** | Sinh viên | Sau khi nộp bài và hoàn tất tiến trình chấm tự động, hệ thống hiển thị bảng điểm tổng hợp kèm mức độ đạt được theo từng chuẩn đầu ra (CLO) của môn học. | `TC-F06` | Done |
| **F-07** | **Tạo câu hỏi vấn đáp (tách 2 luồng) và thiết kế Rubric** | Giảng viên, Trưởng bộ môn | Giảng viên tạo câu hỏi theo 2 luồng độc lập: luồng luyện tập/thi thử (`practice_questions` theo CLO) và luồng thi thật (`exam_questions` bám sát đề thi mẫu / ma trận cấu trúc đề của Trưởng bộ môn); chỉ cho phép lưu khi có đáp án mẫu hợp lệ và bộ tiêu chí Rubric có tổng trọng số bằng đúng 10.0 điểm. | `TC-F07` | In progress |
| **F-08** | **Gửi duyệt và phê duyệt câu hỏi vào ngân hàng** | Giảng viên, Trưởng bộ môn | Câu hỏi do Giảng viên tạo ở cả hai luồng phải gửi chờ duyệt; chỉ những câu hỏi đã được Trưởng bộ môn phê duyệt mới được phát hành vào kho câu hỏi luyện tập (`practice_questions`) hoặc kho câu hỏi thi thật (`exam_questions`). | `TC-F08` | In progress |
| **F-09** | **Tổ chức kỳ thi, ca thi phòng máy, xuất bản đề và điểm danh** | Cán bộ Khảo thí, Trưởng bộ môn, Giám thị | Trưởng bộ môn cấu hình câu hỏi phụ cho đề/ca thi thật (1–5 câu, mặc định 2); Cán bộ Khảo thí tạo kỳ thi, ca thi phòng máy, phân công đồng thời Giảng viên chấm thi (examiner_user_id) & Giám thị (proctor_user_id), bốc bộ đề thi chính thức (HANDED_OVER, đúng môn/cấu trúc và không trùng trong ca) và mở xuất bản đề thi ca thi; Giám thị chỉ điểm danh sinh viên theo ghế ngồi (1–40, đối chiếu thẻ SV/CCCD) và báo cáo vi phạm phòng thi. | TC-F09 | In progress |
| **F-10** | **Làm bài thi thật trên giao diện phòng thi khóa màn hình** | Sinh viên, Giám thị | Sinh viên chỉ vào được bài thi khi đã điểm danh, đề thi ca thi đã xuất bản và kiểm tra microphone đạt yêu cầu; giao diện khóa toàn màn hình, tự động đình chỉ bài thi và báo Giám thị nếu thoát màn hình hoặc mất tiêu điểm quá 3 lần, đồng thời lưu tệp ghi âm kèm mã băm SHA-256 để kiểm tra tính toàn vẹn khi nộp bài. | TC-F10 | In progress |
| **F-11** | **Hậu kiểm kết quả chấm và công bố điểm ca thi** | Giảng viên chấm thi, Cán bộ Khảo thí | Giảng viên chấm thi nghe lại tệp ghi âm, đối chiếu nhận xét của AI và điều chỉnh điểm kèm lý do; khi đạt cổng kiểm soát xuất bản Hard Gate (100% bài nộp đã hoàn tất chấm/xử lý vắng/đình chỉ, không còn retry/DLQ và hoàn tất hậu kiểm bài nghi ngờ/fail/<0.70 confidence), Giảng viên chấm thi hoặc Cán bộ Khảo thí công bố điểm một lần duy nhất (kích hoạt One-Way Lock is_locked = true), đồng thời Cán bộ Khảo thí giám sát hàng đợi DLQ thi thật. | TC-F11 | In progress |
| **F-12** | **Xác nhận điểm và xử lý phúc khảo nội bộ 5 bước** | Sinh viên, Cán bộ Khảo thí, Trưởng bộ môn, Giảng viên | Sinh viên xem điểm đã công bố để xác nhận (ACKNOWLEDGED) hoặc nộp đơn phúc khảo (AppealRequest); Phòng Khảo thí và Trưởng bộ môn cùng nhận thông báo in-app; Cán bộ Khảo thí thẩm tra đơn hợp lệ, phân công Giảng viên độc lập chấm lại (loại trừ Giám khảo ban đầu, định mức tối đa 24 bài/GV) và phê duyệt quyết định cuối cùng (APPROVED/REJECTED) mở ngoại lệ One-Way Lock cập nhật điểm chính thức. | TC-F12 | In progress |

---

## 7. Yêu Cầu Phi Chức Năng (Non-Functional Requirements)

| ID | Thuộc tính chất lượng | Chỉ số đo (Metric) | Ngưỡng đạt (Threshold) | Cách đo lường (Measurement method) |
| :--- | :--- | :--- | :--- | :--- |
| **NFR-01** | **Thời gian ghi nhận nộp bài** | Thời gian máy chủ lưu trạng thái nộp bài của sinh viên trước khi chuyển sang hàng đợi chấm ngầm | $\le 1\text{ giây}$ đối với các thí sinh nộp bài trong cùng một ca thi phòng máy | Chạy kiểm thử tải đồng thời trên API nộp bài phòng thi |
| **NFR-02** | **Thời gian phản hồi tra cứu** | Thời gian phản hồi của các API lấy danh sách, cấu hình và xem kết quả đã chấm | $\le 2\text{ giây}$ trong điều kiện hoạt động thông thường | Đo thời gian phản hồi qua bộ kiểm thử tích hợp API tự động |
| **NFR-03** | **Độ tin cậy khi chấm điểm** | Tỷ lệ bài nộp được bảo toàn và chuyển vào hàng đợi chấm bù khi dịch vụ AI gặp lỗi tạm thời | Không mất bài nộp; tự động thử lại tối đa 3 lần trước khi chuyển vào hàng đợi chờ chấm bù (Admin quản lý DLQ luyện tập/thi thử; Cán bộ Khảo thí quản lý DLQ thi thật) | Kiểm thử giả lập lỗi ngắt kết nối dịch vụ AI và kiểm tra bản ghi trong hàng đợi lỗi (DLQ) |
| **NFR-04** | **Tính toàn vẹn điểm số** | Tỷ lệ chặn các thao tác chỉnh sửa hoặc xóa điểm trực tiếp sau khi ca thi đã công bố điểm | Tất cả yêu cầu sửa/xóa điểm ngoài luồng phúc khảo bị từ chối (`HTTP 403`) | Kiểm thử phân quyền và kiểm thử chặn cập nhật trên bản ghi điểm đã khóa |
| **NFR-05** | **An toàn bằng chứng và phòng thi** | Tỷ lệ khớp mã băm tệp ghi âm và thực thi tự động khóa bài thi khi vi phạm màn hình | Tệp ghi âm lưu trữ khớp mã băm `SHA-256`; tự động đình chỉ ngay khi thoát màn hình/mất tiêu điểm $> 3$ lần | Kiểm thử đối chiếu mã băm tệp ghi âm và kiểm thử bộ đếm vi phạm trên giao diện phòng thi |

---

## 8. Bản Đồ Phân Công Sở Hữu (Ownership Map)

| Thành viên (Member) | Vai trò (Role) | Các phần đảm nhiệm chính (Owned parts: 2–3) | Luồng chính sở hữu (Main workflow owned) | Quyết định phụ trách |
| :--- | :--- | :--- | :--- | :--- |
| **Nguyễn Quang Thành** | Team Leader & Backend | Phân hệ luyện tập tương tác; xử lý thông báo kết quả thời gian thực và xuất báo cáo điểm | **`WF-01`** — Luồng Luyện tập vấn đáp tương tác | `D1`, `R2` |
| **Phạm Nguyễn Đăng Hải** | Database & Frontend | Thiết kế cơ sở dữ liệu và dữ liệu mẫu; giao diện thi thử tính giờ và cấu hình phân cấp học thuật | **`WF-02`** — Luồng Thi thử vấn đáp tính giờ | `D5`, `G3` |
| **Nguyễn Trọng Tốt** | Backend, AI & QA | Ngân hàng câu hỏi (2 luồng) và tích hợp AI sinh đề/chấm điểm; xử lý khóa điểm, phúc khảo 5 bước và kiểm thử | **`WF-03`** — Luồng Xây dựng và phê duyệt câu hỏi | `D2`, `D3`, `R1` |
| **Lê Vũ Hoàng** | Lead Frontend | Giao diện nền tảng và phân quyền 6 vai trò; giao diện Kiosk phòng thi, màn hình Khảo thí và hậu kiểm Giảng viên | **`WF-04`** — Luồng Thi thật phòng máy, hậu kiểm và phúc khảo | `D4`, `R3` |

---

## Phụ lục A — Mục Tiêu Defense 1.1 (Appendix A – Defense 1.1 Targets)

### A1. Danh sách Main Workflows

| ID | Tên Workflow | Actors | Sự kiện bắt đầu (Trigger) | Kết quả nghiệp vụ (Business outcome) | Tính năng | Trạng thái thực thể chính | Người sở hữu (Owner) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-01** | **Luyện tập vấn đáp tương tác** | Admin, Sinh viên | Sinh viên chọn môn học và bắt đầu phiên luyện tập | Câu trả lời nói cho câu hỏi lấy từ kho đã duyệt được gỡ băng, rà soát thuật ngữ, chấm điểm theo Rubric kèm câu hỏi phụ và lưu lịch sử | `F-01`, `F-03`, `F-04` | `PracticeSession`: `in_progress` → `completed` *(1 chuyển trạng thái)* | **Nguyễn Quang Thành** |
| **WF-02** | **Thi thử vấn đáp tính giờ** | Admin, Trưởng bộ môn, Sinh viên | Sinh viên đăng ký bắt đầu một lượt thi thử trong hạn ngạch ngày | Hệ thống ghi nhận lượt thi, thu câu trả lời qua microphone trong thời gian quy định và trả bảng điểm theo chuẩn đầu ra (CLO) sau khi chấm xong | `F-02`, `F-05`, `F-06` | `MockExamSession`: `in_progress` → `submitted` → `graded` *(2 chuyển trạng thái)* | **Phạm Nguyễn Đăng Hải** |
| **WF-03** | **Xây dựng và phê duyệt ngân hàng câu hỏi** | Giảng viên, Trưởng bộ môn | Giảng viên tạo câu hỏi mới theo luồng luyện tập/thi thử (`practice_questions`) hoặc luồng thi thật (`exam_questions` theo ma trận đề mẫu) và gửi duyệt | Câu hỏi và bộ tiêu chí Rubric đạt chuẩn 10.0 điểm được Trưởng bộ môn phê duyệt đưa vào kho câu hỏi luyện tập hoặc kho câu hỏi thi thật tương ứng | `F-07`, `F-08` | `Question`: `DRAFT` → `SUBMITTED_FOR_REVIEW` → `APPROVED` \| `NEEDS_REVISION` \| `REJECTED` *(4 chuyển trạng thái)* | **Nguyễn Trọng Tốt** |
| **WF-04** | **Thi thật phòng máy, hậu kiểm và phúc khảo** | Cán bộ Khảo thí, Trưởng bộ môn, Giám thị, Sinh viên, Giảng viên | Cán bộ Khảo thí mở xuất bản đề thi ca thi và Giám thị điểm danh từng sinh viên theo ghế ngồi (1–40) | Bài thi và tệp ghi âm được niêm phong lưu trữ, AI chấm sơ bộ, Giảng viên hậu kiểm toàn bộ bài nộp, công bố điểm 1 lần (khóa một chiều) và hỗ trợ phúc khảo 5 bước do Cán bộ Khảo thí phân công ($\le 24$ bài/GV) & phê duyệt (Trưởng bộ môn giám sát chuyên môn) | `F-09`, `F-10`, `F-11`, `F-12` | `StudentExamTicket`: `SCHEDULED` → `IN_PROGRESS` → `SUBMITTED` → `AI_GRADED` → `AUDITED` → `PUBLISHED / LOCKED` *(5)*;<br>`AppealRequest`: `SUBMITTED_TO_EXAM_OFFICE` → `ASSIGNED_TO_LECTURER` → `REGRADED_BY_LECTURER` → `APPROVED` \| `REJECTED` *(3)* | **Lê Vũ Hoàng** |

### A2. Kịch bản Demo (Demo Scenarios)

| ID | Workflow | Loại | Tiền điều kiện & Dữ liệu | Các bước chính | Kết quả mong đợi | Người trình bày |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **DS-01** | `WF-01` | Chính | Tài khoản sinh viên; môn học đã có sẵn câu hỏi đã duyệt trong kho luyện tập | Chọn môn và bắt đầu luyện tập → Ghi âm trả lời và sửa thuật ngữ trên bản gỡ băng → Nhận kết quả chấm, trả lời câu hỏi phụ và kết thúc phiên | Hệ thống hiển thị bảng điểm chi tiết theo tiêu chí và lưu phiên luyện tập ở trạng thái hoàn tất | **Nguyễn Quang Thành** |
| **DS-02** | `WF-01` | Ngoại lệ | Phiên luyện tập đang mở nhưng sinh viên bỏ trống không thao tác quá thời gian chờ | Sinh viên quay lại phiên luyện tập đã hết thời gian chờ và bấm gửi câu trả lời hoặc lấy câu hỏi tiếp theo | Hệ thống tự động đóng phiên, giữ nguyên kết quả các câu đã làm trước đó và từ chối thao tác mới | **Nguyễn Quang Thành** |
| **DS-03** | `WF-02` | Chính | Môn học đã cấu hình cấu trúc thi thử; sinh viên còn lượt thi thử trong ngày | Bắt đầu bài thi thử tính giờ → Ghi âm trả lời các câu hỏi qua microphone → Nộp bài và chờ tiến trình chấm tự động hoàn tất | Hệ thống ghi nhận nộp bài, hoàn tất chấm tự động và hiển thị bảng kết quả đánh giá theo từng chuẩn đầu ra (CLO), trừ lượt thi trong ngày | **Phạm Nguyễn Đăng Hải** |
| **DS-04** | `WF-02` | Ngoại lệ | Sinh viên đã sử dụng hết số lượt thi thử tối đa trong ngày của môn học | Sinh viên tiếp tục bấm bắt đầu thêm một lượt thi thử mới cho môn học đó trong cùng ngày | Hệ thống từ chối khởi tạo phiên thi mới và thông báo đã hết hạn ngạch thi thử trong ngày | **Phạm Nguyễn Đăng Hải** |
| **DS-05** | `WF-03` | Chính | Tài khoản Giảng viên và Trưởng bộ môn; môn học đã có danh sách chuẩn đầu ra (CLO) và ma trận cấu trúc đề thi mẫu | Giảng viên tạo câu hỏi kèm đáp án mẫu và Rubric 10.0 điểm theo luồng luyện tập (`practice_questions`) hoặc luồng thi thật (`exam_questions` bám sát ma trận đề mẫu) rồi gửi duyệt → Trưởng bộ môn xem xét và bấm Phê duyệt | Câu hỏi chuyển sang trạng thái đã duyệt và được phát hành vào đúng kho câu hỏi độc lập tương ứng | **Nguyễn Trọng Tốt** |
| **DS-06** | `WF-03` | Ngoại lệ | Giảng viên nhập bộ tiêu chí Rubric có tổng điểm các thành phần khác 10.0 điểm | Giảng viên bấm lưu hoặc gửi duyệt câu hỏi với bộ Rubric chưa hợp lệ | Hệ thống từ chối lưu và hiển thị thông báo yêu cầu điều chỉnh lại trọng số các tiêu chí Rubric bằng đúng 10.0 điểm | **Nguyễn Trọng Tốt** |
| **DS-07** | `WF-04` | Chính | Ca thi phòng máy đã được Cán bộ Khảo thí tạo, gán danh sách sinh viên, Giám thị, Giảng viên và cấu hình Follow-up của Trưởng bộ môn | Cán bộ Khảo thí xuất bản đề thi ca thi & Giám thị điểm danh từng sinh viên (ghế 1–40) → Sinh viên kiểm tra mic, làm bài trên màn hình khóa và nộp bài → Giảng viên nghe lại ghi âm, xác minh 100% bài nộp và công bố điểm 1 lần | Bài thi được lưu kèm tệp ghi âm, chấm sơ bộ, xác minh hoàn tất và công bố thành công (đồng thời kích hoạt khóa chỉnh sửa điểm `khóa một chiều`) | **Lê Vũ Hoàng** |
| **DS-08** | `WF-04` | Ngoại lệ | Bảng điểm ca thi đã được công bố và khóa chỉnh sửa trực tiếp | Thử chỉnh sửa trực tiếp điểm đã công bố (bị chặn `HTTP 403`) → Sinh viên nộp đơn phúc khảo → Cán bộ Khảo thí & Trưởng bộ môn nhận thông báo; Khảo thí thẩm tra hợp lệ và phân công Giảng viên độc lập chấm lại ($\le 24$ bài/GV) → Giảng viên chấm lại và Cán bộ Khảo thí phê duyệt kết quả phúc khảo cuối cùng | Thao tác sửa điểm trực tiếp bị từ chối; điểm số chỉ được cập nhật qua quy trình phúc khảo 5 bước sau khi Giảng viên độc lập chấm lại và được Cán bộ Khảo thí phê duyệt | **Lê Vũ Hoàng** |

### A3. Mục tiêu Số lượng Test Case Chức năng (Functional Test Case Target)

| Nguồn (Source) | Số lượng (Count) | Tối thiểu mỗi mục (Minimum per item) | Tổng phụ (Subtotal) |
| :--- | :---: | :---: | :---: |
| **Tính năng bắt buộc (Mandatory features: `F-01`..`F-12`)** | `12` | `3` | `36` |
| **Luồng nghiệp vụ chính (Main workflows end-to-end: `WF-01`..`WF-04`)** | `4` | `1` | `4` |
| **Chuyển trạng thái của thực thể chính (State transitions: 1 + 2 + 4 + 5 + 3 = 15 transitions ở bảng A1)** | `15` | `1` | `15` |
| **Yêu cầu phi chức năng đã chốt (Agreed NFRs: `NFR-01`..`NFR-05`)** | `5` | `1` | `5` |
| **Tổng cam kết (Committed total)** | — | — | **`60`** |

- **Tỷ lệ chạy tự động trong CI (Automated in CI):** Ít nhất **50%** tổng số test case cam kết ($\ge 30$ test cases).
