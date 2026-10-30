# BẢN NHÁP TIẾNG VIỆT (ĐÃ TINH CHỈNH THEO GÓP Ý TEAM) — BM01: DESIGN BRIEF

> **Nguyên tắc biên soạn:**
> - Giữ văn phong **ngắn gọn, khái quát ở tầm hệ thống** (1–2 câu/ô), không sa đà vào chi tiết kỹ thuật vụn vặt.
> - Khắc phục chính xác các điểm nghiệp vụ cốt lõi mà team góp ý: tách rõ quyền **Trưởng bộ môn duyệt câu hỏi** vs **Giảng viên hậu kiểm/công bố điểm**, quy trình **phúc khảo bởi Giảng viên độc lập & Trưởng bộ môn xác minh/công bố**, điều kiện **xác minh 100% bài thi trước khi công bố**, ngưỡng **thoát màn hình quá 3 lần**, phân biệt **câu hỏi chính (từ kho đã duyệt) với câu hỏi phụ (AI sinh theo ngữ cảnh)**, và bổ sung **ngưỡng đo ngắn gọn cho NFR**.

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
| **Vai trò người dùng** | Chỉ chia 2 nhóm chung là Giảng viên/Admin và Sinh viên. | Tách thành 5 vai trò: Sinh viên, Giảng viên, Trưởng bộ môn, Giám thị và Quản trị viên. | Đảm bảo phân tách nhiệm vụ giữa người soạn câu hỏi, người duyệt ngân hàng câu hỏi, người coi thi phòng máy và người hậu kiểm điểm. |
| **Ngân hàng câu hỏi** | Phân loại câu hỏi theo môn, chương và độ khó. | Phân loại theo môn học, chuẩn đầu ra (CLO) và mức độ nhận thức Bloom; tách riêng kho câu hỏi luyện tập và kho câu hỏi thi thật. | Phù hợp với cấu trúc đề cương môn học của trường và ngăn việc lộ câu hỏi thi chính thức trong quá trình sinh viên luyện tập. |
| **Hình thức trả lời** | Cho phép trả lời bằng giọng nói hoặc gõ văn bản. | Bắt buộc trả lời bằng giọng nói qua microphone, kết hợp bước rà soát để sửa thuật ngữ tiếng Anh chuyên ngành bị nhận diện sai trước khi nộp. | Giữ đúng bản chất kiểm tra vấn đáp nhưng vẫn khắc phục được lỗi nhận diện âm thanh đối với thuật ngữ chuyên ngành Kỹ thuật phần mềm. |
| **Quy trình thi thật & chấm điểm** | Giảng viên hậu kiểm, và nhập điểm so sánh giữa điểm AI và file nghe. | Sinh viên làm bài trên giao diện phòng thi khóa màn hình; AI chấm sơ bộ ngầm; Giảng viên hậu kiểm 100% bài nộp trước khi công bố điểm và hỗ trợ luồng phúc khảo độc lập. | Giảm tải thời gian chấm điểm cho giảng viên mà vẫn giữ quyền xác minh và quyết định điểm số chính thức thuộc về con người. |

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

**Hệ thống Luyện thi và Đánh giá Vấn đáp bằng LLM (OES)** là nền tảng web nội bộ hỗ trợ sinh viên ngành Kỹ thuật phần mềm rèn luyện kỹ năng trả lời vấn đáp và giúp bộ môn tổ chức các kỳ thi vấn đáp chuẩn hóa. Hệ thống phục vụ 5 vai trò (**Sinh viên, Giảng viên, Trưởng bộ môn, Giám thị, Quản trị viên**) qua 4 luồng nghiệp vụ chính: luyện tập tương tác tự do, thi thử tính giờ có hạn ngạch, xây dựng và phê duyệt ngân hàng câu hỏi kèm Rubric theo chuẩn đầu ra (CLO), và tổ chức thi thật tại phòng máy kết hợp hậu kiểm và phúc khảo nội bộ. Hệ thống sử dụng nhận diện giọng nói và mô hình ngôn ngữ lớn để chấm điểm sơ bộ theo Rubric, trong khi **quyền phê duyệt câu hỏi thuộc về Trưởng bộ môn** và **quyền hậu kiểm, công bố điểm thi ban đầu thuộc về Giảng viên**. Xem **Sơ đồ Kiến trúc Tổng quan (SDD, Mục 2.1)**.

---

## 3. Các Quyết Định Thiết Kế Chính (Key Design Decisions)

| ID | Quyết định (Decision) | Các phương án đã cân nhắc (Options considered) | Phương án chọn & Lý do (Chosen option and rationale) | Người phụ trách (Owner) | Liên kết FR / Sơ đồ (Related FR / diagram) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **D1** | **Kiến trúc ứng dụng và xử lý chấm điểm** | Chấm điểm trực tiếp trong lúc chờ phản hồi; máy chủ phân tầng kết hợp hàng đợi chấm điểm chạy ngầm | Chọn máy chủ phân tầng kết hợp hàng đợi chấm điểm chạy ngầm: lưu bài nộp vào cơ sở dữ liệu ngay lập tức để không làm treo giao diện phòng thi và xử lý chấm điểm bất đồng bộ. | **Nguyễn Quang Thành** | Luồng Luyện tập & Thi thật; Sơ đồ Kiến trúc |
| **D2** | **Vai trò hỗ trợ của AI và quyền quyết định của con người** | Để AI tự động phát hành câu hỏi và tự quyết định điểm số; dùng AI làm công cụ gợi ý có giải thích dưới sự kiểm soát của giảng viên | AI chỉ hỗ trợ sinh câu hỏi nháp theo CLO và chấm điểm sơ bộ kèm lập luận theo Rubric; nội dung AI sinh ra luôn cho phép chỉnh sửa và không bao giờ tự động duyệt câu hỏi hay tự chốt điểm thi chính thức. | **Nguyễn Trọng Tốt** | Sinh câu hỏi AI & Chấm điểm theo Rubric |
| **D3** | **Phân tách quyền hạn nghiệp vụ và bảo vệ điểm số** | Phân quyền chung cho giảng viên; tách quyền duyệt câu hỏi, hậu kiểm điểm và chấm phúc khảo độc lập | Trưởng bộ môn duyệt câu hỏi và cấu hình kỳ thi; Giám thị coi thi; Giảng viên hậu kiểm và công bố điểm (đồng thời khóa chỉnh sửa trực tiếp). Khi có phúc khảo, Trưởng bộ môn giao một Giảng viên khác chấm lại và xác minh kết quả cuối cùng. | **Nguyễn Trọng Tốt** | Quy trình Duyệt câu hỏi, Công bố điểm & Phúc khảo |
| **D4** | **Kiểm soát đầu vào giọng nói và lưu trữ bằng chứng thi** | Cho phép nhập văn bản tự do và lưu âm thanh mọi chế độ; bắt buộc trả lời qua giọng nói và chỉ lưu ghi âm cho kỳ thi thật | Yêu cầu thu âm qua microphone kèm bước rà soát sửa thuật ngữ chuyên ngành trên bản gỡ băng; chỉ niêm phong và lưu trữ tệp ghi âm cho kỳ thi chính thức tại phòng máy để làm bằng chứng hậu kiểm. | **Lê Vũ Hoàng** | Luồng Luyện tập, Thi thử & Phòng thi Kiosk |
| **D5** | **Tổ chức ngân hàng câu hỏi và dữ liệu tiêu chí chấm** | Dùng chung một kho câu hỏi; tách riêng kho câu hỏi luyện tập với kho câu hỏi thi thật kèm bản chụp kết quả Rubric | Tách biệt kho câu hỏi luyện tập/thi thử và kho câu hỏi thi thật để bảo mật đề thi; lưu kèm bản chụp chi tiết tiêu chí Rubric ngay trên từng bản ghi bài nộp để việc sửa câu hỏi về sau không làm lệch điểm cũ. | **Phạm Nguyễn Đăng Hải** | Quản lý Ngân hàng câu hỏi; Sơ đồ ERD |

---

## 4. Các Rủi Ro Chính (Top Risks)

| ID | Rủi ro (Risk) | Tác động (Impact) | Cách kiểm chứng hoặc giảm thiểu (How we verify or mitigate) | Người phụ trách (Owner) |
| :--- | :--- | :--- | :--- | :--- |
| **R1** | **Dịch vụ AI hoặc nhận diện giọng nói bên thứ ba phản hồi chậm hoặc gián đoạn trong ca thi** | Kết quả chấm điểm sơ bộ bị trễ, có nguy cơ làm gián đoạn quá trình nộp bài của sinh viên. | Lưu câu trả lời của sinh viên vào cơ sở dữ liệu trước khi gọi AI; xử lý chấm điểm ở tiến trình nền có cơ chế tự động thử lại 3 lần và lưu hàng đợi lỗi để chấm bù. | **Nguyễn Trọng Tốt** |
| **R2** | **Nhận diện giọng nói ghi nhận sai các thuật ngữ tiếng Anh chuyên ngành Kỹ thuật phần mềm** | Nội dung văn bản gửi cho AI bị sai lệch so với ý trả lời thực tế của sinh viên, dẫn đến chấm điểm thiếu chính xác. | Cung cấp bước rà soát bản gỡ băng trong thời gian giới hạn để sinh viên chỉnh sửa các thuật ngữ chuyên ngành bị nhận diện sai trước khi xác nhận nộp, kết hợp bộ chuẩn hóa từ vựng kỹ thuật. | **Nguyễn Quang Thành** |
| **R3** | **Sinh viên chuyển cửa sổ trên máy thi hoặc điểm số bị thay đổi trái phép sau khi công bố** | Làm mất tính nghiêm túc của kỳ thi chính thức và độ tin cậy của kết quả đánh giá. | Kiểm tra máy trạm theo vị trí chỗ ngồi, khóa giao diện toàn màn hình, tự động đình chỉ bài thi và báo Giám thị khi thoát màn hình hoặc mất tiêu điểm quá 3 lần; khóa chỉnh sửa trực tiếp ngay khi công bố điểm. | **Lê Vũ Hoàng** |

---

## 5. Các Điểm Lệch Đã Biết (Known Gaps)

| ID | Điểm chưa khớp (Gap) | Vị trí (Location) | Kế hoạch xử lý (Plan) | Người phụ trách (Owner) | Hạn (Due) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **G1** | Một số sơ đồ trong tài liệu thiết kế (SDD) đang ở dạng tệp vẽ rời, cần lưu kèm dạng mã nguồn sơ đồ trong kho mã nguồn theo quy định nộp hồ sơ. | Report 4 (SDD) & thư mục `docs/` | Cập nhật và bổ sung đầy đủ mã nguồn sơ đồ (Mermaid) cho sơ đồ kiến trúc, ERD, trạng thái và luồng nghiệp vụ vào kho mã nguồn. | **Nguyễn Quang Thành** | Trước hạn nộp Review 2 |
| **G2** | Nội dung chi tiết hóa về 5 vai trò người dùng và quy trình hậu kiểm/phúc khảo trong SRS cần được cập nhật đồng bộ sang Phiếu đăng ký bản chốt. | Phiếu đăng ký & Biểu mẫu `BM03` | Kê khai đầy đủ các mục làm rõ phạm vi vào biểu mẫu `BM03 (Record of Changes)` đính kèm Phiếu đăng ký bản đề xuất chốt. | **Nguyễn Trọng Tốt** | Trước hạn nộp Review 2 |
| **G3** | Môi trường chạy demo tập trung (Staging) và kịch bản nạp dữ liệu mẫu tự động cần được rà soát đồng bộ lần cuối trước buổi bảo vệ. | Cấu hình triển khai & Database Seeder | Kiểm tra quy trình khởi tạo dữ liệu mẫu tự động và chạy thử toàn bộ kịch bản demo trên môi trường triển khai. | **Phạm Nguyễn Đăng Hải** | Trước buổi Review 2 |

---

## 6. Tính Năng Bắt Buộc Đề Xuất (Proposed Mandatory Features)

| ID | Tính năng (Feature) | Actor | Tiêu chí chấp nhận có thể kiểm thử (Acceptance criteria) | Mã Test | Trạng thái |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **F-01** | **Đăng nhập và phân quyền theo vai trò** | Tất cả người dùng, Admin | Chỉ tài khoản do trường cấp phép mới đăng nhập được và truy cập đúng chức năng theo vai trò; tài khoản bị khóa hoặc sai thông tin bị hệ thống từ chối. | `TC-F01` | Done |
| **F-02** | **Cấu hình tham số luyện tập và hạn ngạch thi thử** | Admin, Trưởng bộ môn | Admin thiết lập được các thông số phiên luyện tập và giới hạn số lượt thi thử mỗi ngày; Trưởng bộ môn cấu hình được cấu trúc câu hỏi và thời lượng thi thử cho môn học. | `TC-F02` | Done |
| **F-03** | **Luyện tập vấn đáp và chỉnh sửa thuật ngữ** | Sinh viên | Sinh viên bắt đầu được phiên luyện tập với câu hỏi chính lấy từ kho đã duyệt, ghi âm trả lời và sửa thuật ngữ chuyên ngành trên bản gỡ băng trước khi nộp; phiên không tương tác quá 5 phút tự động kết thúc. | `TC-F03` | Done |
| **F-04** | **Chấm điểm luyện tập, hỏi phụ và xem lịch sử** | Sinh viên | Hệ thống trả kết quả chấm điểm theo tiêu chí Rubric, sinh câu hỏi phụ theo ngữ cảnh (chỉ dùng trong phiên, không lưu vào ngân hàng câu hỏi) khi câu trả lời cần làm rõ thêm và lưu lại lịch sử luyện tập. | `TC-F04` | Done |
| **F-05** | **Thi thử tính giờ dưới cổng bắt buộc giọng nói** | Sinh viên | Hệ thống từ chối lượt thi mới nếu sinh viên đã dùng hết hạn ngạch trong ngày; câu hỏi chính lấy từ kho luyện tập đã duyệt, bắt buộc thu âm qua microphone và tự động thu bài khi hết giờ. | `TC-F05` | Done |
| **F-06** | **Xem bảng điểm thi thử theo chuẩn đầu ra (CLO)** | Sinh viên | Sau khi nộp bài và hoàn tất tiến trình chấm tự động, hệ thống hiển thị bảng điểm tổng hợp kèm mức độ đạt được theo từng chuẩn đầu ra (CLO) của môn học. | `TC-F06` | Done |
| **F-07** | **Tạo câu hỏi vấn đáp và thiết kế Rubric** | Giảng viên, Trưởng bộ môn | Giảng viên tạo câu hỏi thủ công hoặc dùng AI hỗ trợ sinh câu hỏi nháp theo CLO môn học; hệ thống chỉ cho phép lưu khi có đáp án mẫu hợp lệ và bộ tiêu chí Rubric có tổng trọng số bằng đúng 10.0 điểm. | `TC-F07` | In progress |
| **F-08** | **Gửi duyệt và phê duyệt câu hỏi vào ngân hàng** | Giảng viên, Trưởng bộ môn | Câu hỏi do Giảng viên tạo phải gửi chờ duyệt; chỉ những câu hỏi đã được Trưởng bộ môn phê duyệt mới được phát hành vào kho câu hỏi luyện tập hoặc kho câu hỏi thi thật. | `TC-F08` | In progress |
| **F-09** | **Tổ chức kỳ thi, ca thi phòng máy và điểm danh** | Trưởng bộ môn, Giám thị | Trưởng bộ môn tạo kỳ thi, ca thi và phân công nhân sự; Giám thị điểm danh xác nhận từng sinh viên theo vị trí chỗ ngồi và mở ca thi để các sinh viên đã điểm danh bắt đầu làm bài. | `TC-F09` | In progress |
| **F-10** | **Làm bài thi thật trên giao diện phòng thi khóa màn hình** | Sinh viên, Giám thị | Sinh viên chỉ vào được bài thi khi đã điểm danh và kiểm tra microphone đạt yêu cầu; giao diện khóa toàn màn hình, tự động đình chỉ bài thi và báo Giám thị nếu thoát màn hình hoặc mất tiêu điểm quá 3 lần, đồng thời lưu tệp ghi âm kèm mã băm khi nộp bài. | `TC-F10` | Done |
| **F-11** | **Hậu kiểm kết quả chấm và công bố điểm ca thi** | Giảng viên | Giảng viên nghe lại tệp ghi âm, đối chiếu nhận xét của AI và điều chỉnh điểm kèm lý do nếu cần; chỉ được công bố điểm một lần duy nhất sau khi 100% bài nộp trong ca thi đã được xác minh, đồng thời tự động khóa chỉnh sửa trực tiếp. | `TC-F11` | In progress |
| **F-12** | **Xác nhận điểm và xử lý phúc khảo nội bộ** | Sinh viên, Trưởng bộ môn, Giảng viên | Sinh viên xem điểm đã công bố để xác nhận hoặc gửi đơn phúc khảo; Trưởng bộ môn phân công một Giảng viên độc lập (khác người hậu kiểm ban đầu) chấm lại, sau đó Trưởng bộ môn xác minh và công bố kết quả phúc khảo chính thức. | `TC-F12` | In progress |

---

## 7. Yêu Cầu Phi Chức Năng (Non-Functional Requirements)

| ID | Thuộc tính chất lượng | Chỉ số đo (Metric) | Ngưỡng đạt (Threshold) | Cách đo lường (Measurement method) |
| :--- | :--- | :--- | :--- | :--- |
| **NFR-01** | **Thời gian ghi nhận nộp bài** | Thời gian máy chủ lưu trạng thái nộp bài của sinh viên trước khi chuyển sang hàng đợi chấm ngầm | $\le 1\text{ giây}$ ($p95$) với 40 thí sinh trong cùng một ca thi phòng máy | Chạy kiểm thử tải đồng thời trên API nộp bài phòng thi |
| **NFR-02** | **Thời gian phản hồi tra cứu** | Thời gian phản hồi của các API lấy danh sách, cấu hình và xem kết quả đã chấm | $\le 2\text{ giây}$ ($p95$) trong điều kiện hoạt động thông thường | Đo thời gian phản hồi qua bộ kiểm thử tích hợp API tự động |
| **NFR-03** | **Độ tin cậy khi chấm điểm** | Tỷ lệ bài nộp được bảo toàn và chuyển vào hàng đợi chấm bù khi dịch vụ AI gặp lỗi tạm thời | $0\%$ mất bài nộp; tự động thử lại tối đa 3 lần và chờ chấm bù sau 5 phút | Kiểm thử giả lập lỗi ngắt kết nối dịch vụ AI và kiểm tra bản ghi trong hàng đợi lỗi (DLQ) |
| **NFR-04** | **Tính toàn vẹn điểm số** | Tỷ lệ chặn các thao tác chỉnh sửa hoặc xóa điểm trực tiếp sau khi ca thi đã công bố điểm | $100\%$ yêu cầu sửa/xóa điểm ngoài luồng phúc khảo bị từ chối (`HTTP 403`) | Kiểm thử phân quyền và kiểm thử chặn cập nhật trên bản ghi điểm đã khóa |
| **NFR-05** | **An toàn bằng chứng và phòng thi** | Tỷ lệ khớp mã băm tệp ghi âm và thực thi tự động khóa bài thi khi vi phạm màn hình | $100\%$ tệp ghi âm khớp mã băm `SHA-256`; tự động đình chỉ ngay khi thoát màn hình/mất tiêu điểm $> 3$ lần | Kiểm thử đối chiếu mã băm tệp ghi âm và kiểm thử bộ đếm vi phạm trên giao diện phòng thi |

---

## 8. Bản Đồ Phân Công Sở Hữu (Ownership Map)

| Thành viên (Member) | Vai trò (Role) | Các phần đảm nhiệm chính (Owned parts: 2–3) | Luồng chính sở hữu (Main workflow owned) | Quyết định phụ trách |
| :--- | :--- | :--- | :--- | :--- |
| **Nguyễn Quang Thành** | Team Leader & Backend | Phân hệ luyện tập tương tác; xử lý thông báo kết quả thời gian thực và xuất báo cáo điểm | **`WF-01`** — Luồng Luyện tập vấn đáp tương tác | `D1`, `R2` |
| **Phạm Nguyễn Đăng Hải** | Database & Frontend | Thiết kế cơ sở dữ liệu và dữ liệu mẫu; giao diện thi thử tính giờ và cấu hình học vụ | **`WF-02`** — Luồng Thi thử vấn đáp tính giờ | `D5`, `G3` |
| **Nguyễn Trọng Tốt** | Backend, AI & QA | Ngân hàng câu hỏi và tích hợp AI hỗ trợ sinh câu hỏi/chấm điểm; xử lý khóa điểm, phúc khảo và kiểm thử | **`WF-03`** — Luồng Xây dựng và phê duyệt câu hỏi | `D2`, `D3`, `R1` |
| **Lê Vũ Hoàng** | Lead Frontend | Giao diện nền tảng và phân quyền; giao diện phòng thi khóa màn hình và màn hình hậu kiểm của giảng viên | **`WF-04`** — Luồng Thi thật phòng máy, hậu kiểm và phúc khảo | `D4`, `R3` |

---

## Phụ lục A — Mục Tiêu Defense 1.1 (Appendix A – Defense 1.1 Targets)

### A1. Danh sách Main Workflows

| ID | Tên Workflow | Actors | Sự kiện bắt đầu (Trigger) | Kết quả nghiệp vụ (Business outcome) | Tính năng | Trạng thái thực thể chính | Người sở hữu (Owner) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **WF-01** | **Luyện tập vấn đáp tương tác** | Admin, Sinh viên | Sinh viên chọn môn học và bắt đầu phiên luyện tập | Câu trả lời nói cho câu hỏi lấy từ kho đã duyệt được gỡ băng, rà soát thuật ngữ, chấm điểm theo Rubric kèm câu hỏi phụ và lưu lịch sử | `F-01`, `F-03`, `F-04` | `PracticeSession`: `in_progress` $\to$ `completed` *(1 chuyển trạng thái)* | **Nguyễn Quang Thành** |
| **WF-02** | **Thi thử vấn đáp tính giờ** | Admin, Trưởng bộ môn, Sinh viên | Sinh viên đăng ký bắt đầu một lượt thi thử trong hạn ngạch ngày | Hệ thống ghi nhận lượt thi, thu câu trả lời qua microphone trong thời gian quy định và trả bảng điểm theo chuẩn đầu ra (CLO) sau khi chấm xong | `F-02`, `F-05`, `F-06` | `MockExamSession`: `in_progress` $\to$ `submitted` $\to$ `graded` *(2 chuyển trạng thái)* | **Phạm Nguyễn Đăng Hải** |
| **WF-03** | **Xây dựng và phê duyệt ngân hàng câu hỏi** | Giảng viên, Trưởng bộ môn | Giảng viên tạo câu hỏi mới (thủ công hoặc có AI hỗ trợ) và gửi duyệt | Câu hỏi và bộ tiêu chí Rubric đạt chuẩn 10.0 điểm được Trưởng bộ môn phê duyệt đưa vào kho câu hỏi luyện tập hoặc thi thật | `F-07`, `F-08` | `Question`: `DRAFT` $\to$ `SUBMITTED_FOR_REVIEW` $\to$ `APPROVED` \| `NEEDS_REVISION` \| `REJECTED` *(4 chuyển trạng thái)* | **Nguyễn Trọng Tốt** |
| **WF-04** | **Thi thật phòng máy, hậu kiểm và phúc khảo** | Trưởng bộ môn, Giám thị, Sinh viên, Giảng viên | Giám thị điểm danh từng sinh viên và mở ca thi cho phòng máy | Bài thi và tệp ghi âm được niêm phong lưu trữ, AI chấm sơ bộ, Giảng viên hậu kiểm 100% bài nộp và công bố điểm (đồng thời khóa điểm), hỗ trợ chấm phúc khảo độc lập | `F-09`, `F-10`, `F-11`, `F-12` | `StudentExamTicket`: `SCHEDULED` $\to$ `IN_PROGRESS` $\to$ `SUBMITTED` $\to$ `AI_GRADED` $\to$ `AUDITED` $\to$ `PUBLISHED / LOCKED` *(5)*;<br>`AppealRequest`: `PENDING` $\to$ `IN_REVIEW` $\to$ `APPROVED` \| `REJECTED` *(3)* | **Lê Vũ Hoàng** |

### A2. Kịch bản Demo (Demo Scenarios)

| ID | Workflow | Loại | Tiền điều kiện & Dữ liệu | Các bước chính | Kết quả mong đợi | Người trình bày |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| **DS-01** | `WF-01` | Chính | Tài khoản sinh viên; môn học đã có sẵn câu hỏi đã duyệt trong kho luyện tập | Chọn môn và bắt đầu luyện tập $\to$ Ghi âm trả lời và sửa thuật ngữ trên bản gỡ băng $\to$ Nhận kết quả chấm, trả lời câu hỏi phụ và kết thúc phiên | Hệ thống hiển thị bảng điểm chi tiết theo tiêu chí và lưu phiên luyện tập ở trạng thái hoàn tất | **Nguyễn Quang Thành** |
| **DS-02** | `WF-01` | Ngoại lệ | Phiên luyện tập đang mở nhưng sinh viên bỏ trống không thao tác quá 5 phút | Sinh viên quay lại phiên luyện tập đã hết thời gian chờ và bấm gửi câu trả lời hoặc lấy câu hỏi tiếp theo | Hệ thống tự động đóng phiên, giữ nguyên kết quả các câu đã làm trước đó và từ chối thao tác mới | **Nguyễn Quang Thành** |
| **DS-03** | `WF-02` | Chính | Môn học đã cấu hình cấu trúc thi thử; sinh viên còn lượt thi thử trong ngày | Bắt đầu bài thi thử tính giờ $\to$ Ghi âm trả lời các câu hỏi qua microphone $\to$ Nộp bài và chờ tiến trình chấm tự động hoàn tất | Hệ thống ghi nhận nộp bài, hoàn tất chấm tự động và hiển thị bảng kết quả đánh giá theo từng chuẩn đầu ra (CLO), trừ lượt thi trong ngày | **Phạm Nguyễn Đăng Hải** |
| **DS-04** | `WF-02` | Ngoại lệ | Sinh viên đã sử dụng hết số lượt thi thử tối đa trong ngày của môn học | Sinh viên tiếp tục bấm bắt đầu thêm một lượt thi thử mới cho môn học đó trong cùng ngày | Hệ thống từ chối khởi tạo phiên thi mới và thông báo đã hết hạn ngạch thi thử trong ngày | **Phạm Nguyễn Đăng Hải** |
| **DS-05** | `WF-03` | Chính | Tài khoản Giảng viên và Trưởng bộ môn; môn học đã có danh sách chuẩn đầu ra (CLO) | Giảng viên tạo câu hỏi kèm đáp án mẫu và Rubric 10.0 điểm (nhờ AI gợi ý hoặc tự soạn) rồi gửi duyệt $\to$ Trưởng bộ môn xem xét và bấm Phê duyệt | Câu hỏi chuyển sang trạng thái đã duyệt và được phát hành vào đúng kho câu hỏi đã chọn | **Nguyễn Trọng Tốt** |
| **DS-06** | `WF-03` | Ngoại lệ | Giảng viên nhập bộ tiêu chí Rubric có tổng điểm các thành phần khác 10.0 điểm | Giảng viên bấm lưu hoặc gửi duyệt câu hỏi với bộ Rubric chưa hợp lệ | Hệ thống từ chối lưu và hiển thị thông báo yêu cầu điều chỉnh lại trọng số các tiêu chí Rubric bằng đúng 10.0 điểm | **Nguyễn Trọng Tốt** |
| **DS-07** | `WF-04` | Chính | Ca thi phòng máy đã được lên lịch, gán danh sách sinh viên, Giám thị và Giảng viên | Giám thị điểm danh từng sinh viên và mở ca thi $\to$ Sinh viên kiểm tra mic, làm bài trên màn hình khóa và nộp bài $\to$ Giảng viên nghe lại ghi âm, xác minh đủ 100% bài nộp và bấm Công bố điểm | Bài thi được lưu kèm tệp ghi âm, chấm sơ bộ, xác minh hoàn tất và công bố thành công (đồng thời kích hoạt khóa chỉnh sửa điểm) | **Lê Vũ Hoàng** |
| **DS-08** | `WF-04` | Ngoại lệ | Bảng điểm ca thi đã được công bố và khóa chỉnh sửa trực tiếp | Thử chỉnh sửa trực tiếp điểm đã công bố (bị chặn) $\to$ Sinh viên nộp đơn phúc khảo $\to$ Trưởng bộ môn giao một Giảng viên độc lập chấm lại, sau đó Trưởng bộ môn xác minh và công bố kết quả phúc khảo | Thao tác sửa điểm trực tiếp bị từ chối; điểm số chỉ được cập nhật sau khi Giảng viên độc lập chấm lại và được Trưởng bộ môn xác minh, công bố | **Lê Vũ Hoàng** |

### A3. Mục tiêu Số lượng Test Case Chức năng (Functional Test Case Target)

| Nguồn (Source) | Số lượng (Count) | Tối thiểu mỗi mục (Minimum per item) | Tổng phụ (Subtotal) |
| :--- | :---: | :---: | :---: |
| **Tính năng bắt buộc (Mandatory features: `F-01`..`F-12`)** | `12` | `3` | `36` |
| **Luồng nghiệp vụ chính (Main workflows end-to-end: `WF-01`..`WF-04`)** | `4` | `1` | `4` |
| **Chuyển trạng thái của thực thể chính (State transitions: 1 + 2 + 4 + 5 + 3 = 15 transitions ở bảng A1)** | `15` | `1` | `15` |
| **Yêu cầu phi chức năng đã chốt (Agreed NFRs: `NFR-01`..`NFR-05`)** | `5` | `1` | `5` |
| **Tổng cam kết (Committed total)** | — | — | **`60`** |

- **Tỷ lệ chạy tự động trong CI (Automated in CI):** Ít nhất **50%** tổng số test case cam kết ($\ge 30$ test cases).
