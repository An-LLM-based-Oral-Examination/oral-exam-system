# SWP490 · CAPSTONE PROJECT · REVIEW 2 · BM04
## Review 2 Commitment (Bản Tiếng Việt Đối Soát — Đồng Bộ 100% Với BM01 & BM03)

---

### 1. Project Information (Thông tin chung)

| Trường thông tin | Nội dung |
| :--- | :--- |
| **Project code** | `FA26SE166` |
| **Project name** | An LLM-based Oral Examination Practice and Assessment System for Software Engineering Major |
| **Team** | Nguyễn Quang Thành, Nguyễn Trọng Tốt, Phạm Nguyễn Đăng Hải, Lê Vũ Hoàng |
| **Supervisor** | TS. Nguyễn Thị Cẩm Hương |
| **Review 2 date** | |
| **Reviewer 1** | |
| **Reviewer 2** | |
| **Version** | `1.0` |
| **Date** | `28/10/2026` |

---

### 2. Final Registration Form (Phiếu đăng ký phiên bản cuối)

| Trường thông tin | Nội dung |
| :--- | :--- |
| **Final version** | `2.0` |
| **Approved at Review 2 (Yes/No)** | `Yes` |
| **Attached file name** | `SWP490_FA26SE166_R2_Registration.pdf` |

---

### 3. Mandatory Features (Tính năng bắt buộc)

| ID | Tính năng (Feature) | Actor | Tiêu chí chấp nhận có thể kiểm thử (Acceptance criteria) | Workflow | Người sở hữu (Owner) |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **F-01** | **Đăng nhập, phân quyền 6 vai trò và phân cấp lớp học** | Tất cả người dùng, Quản trị viên, Trưởng bộ môn, Giảng viên, Sinh viên | Xác thực kép gồm đăng nhập Google OAuth 2.0 PKCE và tài khoản nhà trường cấp kèm yêu cầu đổi mật khẩu bắt buộc ở lần đầu đăng nhập, cấm tự đăng ký tự do; phân quyền chặt chẽ 6 vai trò người dùng (sinh viên, giảng viên, trưởng bộ môn, cán bộ khảo thí, giám thị, quản trị viên); quản lý phân cấp đào tạo 4 tầng (quản trị viên tạo học kỳ, môn học và phân công trưởng bộ môn $\to$ trưởng bộ môn phân công giảng viên $\to$ giảng viên tạo lớp học phần với mật khẩu tham gia) và hỗ trợ tổ chức ca thi trong lớp: giảng viên cấu hình tên bài kiểm tra, nguồn câu hỏi tự bốc từ ngân hàng môn học hoặc danh sách chọn lọc, thuật toán chống trùng câu hỏi giữa các sinh viên, cấu hình câu hỏi phụ follow-up (không lưu âm thanh), mật khẩu OTP tự động xoay vòng 5 phút kèm khoảng gia hạn an toàn, chấm điểm tự động và công bố kết quả song song theo thời gian thực cho sinh viên. | `WF-01` | Nguyễn Quang Thành |
| **F-02** | **Cấu hình tham số luyện tập và hạn ngạch thi thử** | Admin, Trưởng bộ môn | Admin thiết lập được các thông số phiên luyện tập và giới hạn số lượt thi thử mỗi ngày; Trưởng bộ môn cấu hình được cấu trúc câu hỏi và thời lượng thi thử cho môn học. | `WF-02` | Phạm Nguyễn Đăng Hải |
| **F-03** | **Luyện tập vấn đáp và chỉnh sửa thuật ngữ** | Sinh viên | Sinh viên bắt đầu được phiên luyện tập với câu hỏi chính lấy từ kho đã duyệt, ghi âm trả lời qua microphone và chỉnh sửa thuật ngữ chuyên ngành trên bản phiên âm trước khi nộp; phiên không tương tác quá thời gian chờ tự động kết thúc. | `WF-01` | Nguyễn Quang Thành |
| **F-04** | **Chấm điểm luyện tập, hỏi phụ và xem lịch sử** | Sinh viên | Hệ thống trả kết quả chấm điểm theo tiêu chí Rubric, sinh câu hỏi phụ theo ngữ cảnh (chỉ dùng trong phiên, không lưu vào ngân hàng câu hỏi) khi câu trả lời cần làm rõ thêm và lưu lại lịch sử luyện tập. | `WF-01` | Nguyễn Quang Thành |
| **F-05** | **Thi thử vấn đáp tính giờ có hạn ngạch** | Sinh viên | Hệ thống từ chối lượt thi mới nếu sinh viên đã dùng hết hạn ngạch trong ngày; câu hỏi chính lấy từ kho luyện tập đã duyệt, yêu cầu ghi âm câu trả lời qua microphone trước khi rà soát bản phiên âm và tự động thu bài khi hết giờ. | `WF-02` | Phạm Nguyễn Đăng Hải |
| **F-06** | **Xem bảng điểm thi thử theo chuẩn đầu ra (CLO)** | Sinh viên | Sau khi nộp bài và hoàn tất tiến trình chấm tự động, hệ thống hiển thị bảng điểm tổng hợp kèm mức độ đạt được theo từng chuẩn đầu ra (CLO) của môn học. | `WF-02` | Phạm Nguyễn Đăng Hải |
| **F-07** | **Tạo câu hỏi vấn đáp 2 luồng và thiết kế Rubric** | Giảng viên, Trưởng bộ môn | Giảng viên soạn câu hỏi độc lập cho kho luyện tập/thi thử theo CLO thuộc kho luyện tập và thi thử hoặc soạn câu hỏi thi thật bám sát đề thi mẫu / ma trận cấu trúc đề của Trưởng bộ môn thuộc ngân hàng câu hỏi thi thật; chỉ cho phép lưu khi có đáp án mẫu hợp lệ và bộ tiêu chí Rubric có tổng trọng số bằng đúng 10.0 điểm. | `WF-03` | Nguyễn Trọng Tốt |
| **F-08** | **Gửi duyệt và phê duyệt câu hỏi vào ngân hàng** | Giảng viên, Trưởng bộ môn | Câu hỏi do Giảng viên tạo phải gửi chờ duyệt; chỉ những câu hỏi đã được Trưởng bộ môn phê duyệt (`APPROVED`) mới được phát hành vào kho câu hỏi luyện tập thuộc kho luyện tập và thi thử hoặc ngân hàng câu hỏi thi thật chính thức. | `WF-03` | Nguyễn Trọng Tốt |
| **F-09** | **Tổ chức kỳ thi, ca thi phòng máy và điểm danh** | Trưởng bộ môn, Cán bộ Khảo thí, Giám thị | Trưởng bộ môn cấu hình Follow-up cho đề/ca thi thật (1–5 câu, mặc định 2); Cán bộ Khảo thí tạo kỳ thi, ca thi phòng máy, phân công Giám thị/Giám khảo, import danh sách sinh viên, phân bổ đề và mở xuất bản đề thi ca thi chính thức; Giám thị chỉ điểm danh từng sinh viên theo chỗ ngồi (Seat 1–40, đối chiếu thẻ SV/CCCD). | `WF-04` | Lê Vũ Hoàng |
| **F-10** | **Làm bài thi thật trên giao diện phòng thi khóa màn hình** | Sinh viên, Giám thị | Sinh viên chỉ vào được bài thi khi đã được Giám thị điểm danh, Cán bộ Khảo thí mở xuất bản đề và kiểm tra microphone đạt yêu cầu; giao diện khóa toàn màn hình, tự động đình chỉ bài thi và báo Giám thị ghi nhận nhật ký vi phạm an ninh phòng thi nếu thoát màn hình/mất tiêu điểm quá 3 lần, đồng thời lưu tệp ghi âm kèm mã băm `SHA-256` khi nộp bài. | `WF-04` | Lê Vũ Hoàng |
| **F-11** | **Hậu kiểm kết quả chấm và công bố điểm ca thi** | Giảng viên, Cán bộ Khảo thí | Giảng viên nghe lại tệp ghi âm, đối chiếu nhận xét của AI và điều chỉnh điểm kèm văn bản giải trình lý do điều chỉnh nếu cần; khi 100% bài nộp trong ca thi đã được xác minh, Giảng viên / Cán bộ Khảo thí công bố bảng điểm ca thi một lần duy nhất, tự động kích hoạt cơ chế khóa bảng điểm một chiều chống sửa đổi. | `WF-04` | Lê Vũ Hoàng |
| **F-12** | **Xác nhận điểm và xử lý phúc khảo nội bộ 5 bước** | Sinh viên, Cán bộ Khảo thí, Trưởng bộ môn, Giảng viên | Sinh viên xem điểm đã công bố để xác nhận hoặc nộp đơn phúc khảo (đơn phúc khảo chính thức) lên Cán bộ Khảo thí $\to$ Phòng Khảo thí và Trưởng bộ môn cùng nhận thông báo in-app; Khảo thí thẩm tra hợp lệ $\to$ Cán bộ Khảo thí phân công Giảng viên độc lập chấm lại (định mức $\le 24$ bài/GV, loại trừ 100% Giám khảo chấm ban đầu; thuật toán đề xuất phân bổ tự động và điều chỉnh thủ công) $\to$ Giảng viên chấm lại trên Evidence Panel $\to$ Cán bộ Khảo thí phê duyệt quyết định cuối cùng (mở ngoại lệ cơ chế khóa bảng điểm một chiều cập nhật điểm chính thức nếu chấp thuận; Trưởng bộ môn nhận thông báo giám sát học thuật). | `WF-04` | Lê Vũ Hoàng |

---

### 4. Non-Functional Requirements (Yêu cầu phi chức năng)

| ID | Thuộc tính chất lượng (Quality attribute) | Chỉ số đo (Metric) | Ngưỡng đạt (Threshold) | Cách đo lường (Measurement method) | Người phụ trách (Owner) |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **NFR-01** | **Thời gian ghi nhận nộp bài** | Thời gian máy chủ lưu trạng thái nộp bài của sinh viên trước khi chuyển sang hàng đợi chấm ngầm | $\le 1\text{ giây}$ đối với các thí sinh nộp bài trong cùng một ca thi phòng máy | Chạy kiểm thử tải đồng thời trên API nộp bài phòng thi | Nguyễn Quang Thành |
| **NFR-02** | **Thời gian phản hồi tra cứu** | Thời gian phản hồi của các API lấy danh sách, cấu hình và xem kết quả đã chấm | $\le 2\text{ giây}$ trong điều kiện hoạt động thông thường | Đo thời gian phản hồi qua bộ kiểm thử tích hợp API tự động | Phạm Nguyễn Đăng Hải |
| **NFR-03** | **Độ tin cậy khi chấm điểm** | Tỷ lệ bài nộp được bảo toàn và chuyển vào hàng đợi chấm bù khi dịch vụ AI gặp lỗi tạm thời | Không mất bài nộp; tự động thử lại tối đa 3 lần trước khi chuyển vào DLQ (Admin replay DLQ luyện tập/thi thử; Cán bộ Khảo thí replay DLQ thi thật) | Kiểm thử giả lập lỗi ngắt kết nối dịch vụ AI và kiểm tra bản ghi cùng phân quyền replay trong hàng đợi lỗi (DLQ) | Nguyễn Trọng Tốt |
| **NFR-04** | **Tính toàn vẹn điểm số** | Tỷ lệ chặn các thao tác chỉnh sửa hoặc xóa điểm trực tiếp sau khi ca thi đã công bố điểm | Tất cả yêu cầu sửa/xóa điểm ngoài luồng phúc khảo bị hệ thống từ chối do vi phạm quy chế bảo toàn điểm | Kiểm thử phân quyền và kiểm thử chặn cập nhật trên bản ghi điểm đã khóa | Nguyễn Trọng Tốt |
| **NFR-05** | **An toàn bằng chứng và phòng thi** | Tỷ lệ khớp mã băm tệp ghi âm và thực thi tự động khóa bài thi khi vi phạm màn hình | Tệp ghi âm lưu trữ khớp mã băm `SHA-256`; tự động đình chỉ ngay khi thoát màn hình/mất tiêu điểm $> 3$ lần | Kiểm thử đối chiếu mã băm tệp ghi âm và kiểm thử bộ đếm vi phạm trên giao diện phòng thi | Lê Vũ Hoàng |

---

### 5. Out of Scope (Ngoài phạm vi)

| Hạng mục (Item) | Lý do (Reason) |
| :--- | :--- |
| **Lưu trữ ghi âm cho chế độ Luyện tập và Thi thử** | Hai chế độ này dùng cho mục đích tự học hàng ngày và không yêu cầu lưu bằng chứng pháp lý như kỳ thi thật. |
| **Tích hợp gọi Video Call trực tuyến và giám sát Camera** | Kỳ thi chính thức được tổ chức tập trung tại phòng máy của trường dưới sự giám sát trực tiếp của Giám thị. |
| **Tự đăng ký tài khoản công khai** | Hệ thống sử dụng nội bộ trong trường; chỉ các tài khoản được nhà trường cấp phép mới được truy cập. |
| **Chấm điểm tức thì hoặc khiếu nại trực tiếp tại máy phòng thi** | Điểm thi chính thức cần được Giảng viên hậu kiểm toàn bộ ca thi trước khi công bố trên cổng thông tin sinh viên. |

---

### 6. Workflow Commitments (Cam kết theo từng Main Workflow)

#### 6.1 Workflow WF-01

| Trường thông tin | Nội dung |
| :--- | :--- |
| **Workflow name** | Luyện tập vấn đáp tương tác (Interactive oral practice) |
| **Owner (one member)** | Nguyễn Quang Thành |
| **Supporting members** | Lê Vũ Hoàng, Nguyễn Trọng Tốt |
| **Actors** | Admin, Sinh viên |
| **Trigger** | Sinh viên chọn môn học và bắt đầu phiên luyện tập |
| **Business outcome** | Câu trả lời nói cho câu hỏi lấy từ kho đã duyệt được gỡ băng, rà soát thuật ngữ, chấm điểm theo Rubric kèm câu hỏi phụ và lưu lịch sử |
| **Main entity state path** | Khởi tạo phiên luyện tập $\to$ Đang trả lời và sửa thuật ngữ $\to$ Hoàn tất nhận xét và điểm số (phiên không tương tác quá 5 phút tự động đóng) |
| **Activity diagram (Report 4 section)** | Report 4 (SDD) — Activity Diagram `WF-01` (Interactive Oral Practice) |

**Features in this workflow**

| Feature ID | Feature | Acceptance criteria | Test case IDs |
| :---: | :--- | :--- | :--- |
| **F-01** | Đăng nhập, phân quyền 6 vai trò và phân cấp lớp học | Xác thực kép gồm đăng nhập Google OAuth 2.0 PKCE và tài khoản nhà trường cấp (yêu cầu đổi mật khẩu lần đầu, cấm tự đăng ký tự do); phân quyền chặt chẽ 6 vai trò người dùng; hỗ trợ phân cấp đào tạo 4 tầng và ca thi trong lớp với cấu hình tên bài thi, nguồn câu hỏi chống trùng giữa các sinh viên, câu hỏi phụ follow-up không lưu âm thanh, mã OTP xoay vòng 5 phút có gia hạn an toàn, chấm điểm tự động và công bố kết quả song song theo thời gian thực cho sinh viên. | `TC-F01-01`..`TC-F01-03` |
| **F-03** | Luyện tập vấn đáp và chỉnh sửa thuật ngữ | Sinh viên bắt đầu được phiên luyện tập với câu hỏi chính lấy từ kho đã duyệt, ghi âm trả lời qua microphone và chỉnh sửa thuật ngữ chuyên ngành trên bản phiên âm trước khi nộp; phiên không tương tác quá thời gian chờ tự động kết thúc. | `TC-F03-01`..`TC-F03-03` |
| **F-04** | Chấm điểm luyện tập, hỏi phụ và xem lịch sử | Hệ thống trả kết quả chấm điểm theo tiêu chí Rubric, sinh câu hỏi phụ theo ngữ cảnh (chỉ dùng trong phiên, không lưu vào ngân hàng câu hỏi) khi câu trả lời cần làm rõ thêm và lưu lại lịch sử luyện tập. | `TC-F04-01`..`TC-F04-03` |

**Demo scenarios**

| Scenario ID | Type | Preconditions and data | Main steps | Expected result | Presenter |
| :---: | :---: | :--- | :--- | :--- | :--- |
| **DS-01** (`DS-01a`) | Chính | Tài khoản sinh viên; môn học đã có sẵn câu hỏi đã duyệt trong kho luyện tập | Chọn môn và bắt đầu luyện tập $\to$ Ghi âm trả lời và sửa thuật ngữ trên bản gỡ băng $\to$ Nhận kết quả chấm, trả lời câu hỏi phụ và kết thúc phiên | Hệ thống hiển thị bảng điểm chi tiết theo tiêu chí và lưu phiên luyện tập ở trạng thái hoàn tất | Nguyễn Quang Thành |
| **DS-02** (`DS-01b`) | Ngoại lệ | Phiên luyện tập đang mở nhưng sinh viên bỏ trống không thao tác quá thời gian chờ | Sinh viên quay lại phiên luyện tập đã hết thời gian chờ và bấm gửi câu trả lời hoặc lấy câu hỏi tiếp theo | Hệ thống tự động đóng phiên, giữ nguyên kết quả các câu đã làm trước đó và từ chối thao tác mới | Nguyễn Quang Thành |

**Committed test cases**

| Test type | Count | Automated in CI | Responsible member(s) |
| :--- | :---: | :---: | :--- |
| Feature tests ($\ge 3$ per feature) | `9` | `5` | Nguyễn Quang Thành |
| End-to-end workflow test ($\ge 1$) | `1` | `0` | Nguyễn Quang Thành |
| State transition tests ($\ge 1$ per transition) | `1` | `1` | Nguyễn Quang Thành |
| NFR tests ($\ge 1$ per NFR: `NFR-01`) | `1` | `0` | Nguyễn Quang Thành |
| **Total for this workflow** | **`12`** | **`6`** | **Nguyễn Quang Thành** |

**Pass criteria at Defense 1.1**

| Criterion | Committed |
| :--- | :--- |
| Runs end-to-end on staging with seeded data | `Yes` |
| Main scenario passes | `Yes` |
| Exception scenarios passing | `100%` (Tối thiểu $\ge 80\%$) |
| Test pass rate for this workflow | $\ge 90\%$ tổng số; `100%` test luồng chính |
| Open Critical / High defects | `0` |

**Reviewer note**

| Note | |
| :--- | :--- |

---

#### 6.2 Workflow WF-02

| Trường thông tin | Nội dung |
| :--- | :--- |
| **Workflow name** | Thi thử vấn đáp tính giờ (Timed mock exam) |
| **Owner (one member)** | Phạm Nguyễn Đăng Hải |
| **Supporting members** | Nguyễn Quang Thành, Lê Vũ Hoàng |
| **Actors** | Admin, Trưởng bộ môn, Sinh viên |
| **Trigger** | Sinh viên đăng ký bắt đầu một lượt thi thử trong hạn ngạch ngày |
| **Business outcome** | Hệ thống ghi nhận lượt thi, thu câu trả lời qua microphone trong thời gian quy định và trả bảng điểm theo chuẩn đầu ra (CLO) sau khi chấm xong |
| **Main entity state path** | Khởi tạo phiên thi thử $\to$ Đang làm bài tính giờ $\to$ Đã nộp bài $\to$ Đã xuất bảng điểm phân tích chuẩn đầu ra |
| **Activity diagram (Report 4 section)** | Report 4 (SDD) — Activity Diagram `WF-02` (Timed Mock Exam) |

**Features in this workflow**

| Feature ID | Feature | Acceptance criteria | Test case IDs |
| :---: | :--- | :--- | :--- |
| **F-02** | Cấu hình tham số luyện tập và hạn ngạch thi thử | Admin thiết lập được các thông số phiên luyện tập và giới hạn số lượt thi thử mỗi ngày; Trưởng bộ môn cấu hình được cấu trúc câu hỏi và thời lượng thi thử cho môn học. | `TC-F02-01`..`TC-F02-03` |
| **F-05** | Thi thử vấn đáp tính giờ có hạn ngạch | Hệ thống từ chối lượt thi mới nếu sinh viên đã dùng hết hạn ngạch trong ngày; câu hỏi chính lấy từ kho luyện tập đã duyệt, yêu cầu ghi âm câu trả lời qua microphone trước khi rà soát bản phiên âm và tự động thu bài khi hết giờ. | `TC-F05-01`..`TC-F05-03` |
| **F-06** | Xem bảng điểm thi thử theo chuẩn đầu ra (CLO) | Sau khi nộp bài và hoàn tất tiến trình chấm tự động, hệ thống hiển thị bảng điểm tổng hợp kèm mức độ đạt được theo từng chuẩn đầu ra (CLO) của môn học. | `TC-F06-01`..`TC-F06-03` |

**Demo scenarios**

| Scenario ID | Type | Preconditions and data | Main steps | Expected result | Presenter |
| :---: | :---: | :--- | :--- | :--- | :--- |
| **DS-03** (`DS-02a`) | Chính | Môn học đã cấu hình cấu trúc thi thử; sinh viên còn lượt thi thử trong ngày | Bắt đầu bài thi thử tính giờ $\to$ Ghi âm trả lời các câu hỏi qua microphone $\to$ Nộp bài và chờ tiến trình chấm tự động hoàn tất | Hệ thống ghi nhận nộp bài, hoàn tất chấm tự động và hiển thị bảng kết quả đánh giá theo từng chuẩn đầu ra (CLO), trừ lượt thi trong ngày | Phạm Nguyễn Đăng Hải |
| **DS-04** (`DS-02b`) | Ngoại lệ | Sinh viên đã sử dụng hết số lượt thi thử tối đa trong ngày của môn học | Sinh viên tiếp tục bấm bắt đầu thêm một lượt thi thử mới cho môn học đó trong cùng ngày | Hệ thống từ chối khởi tạo phiên thi mới và thông báo đã hết hạn ngạch thi thử trong ngày | Phạm Nguyễn Đăng Hải |

**Committed test cases**

| Test type | Count | Automated in CI | Responsible member(s) |
| :--- | :---: | :---: | :--- |
| Feature tests ($\ge 3$ per feature) | `9` | `5` | Phạm Nguyễn Đăng Hải |
| End-to-end workflow test ($\ge 1$) | `1` | `0` | Phạm Nguyễn Đăng Hải |
| State transition tests ($\ge 1$ per transition) | `2` | `1` | Phạm Nguyễn Đăng Hải |
| NFR tests ($\ge 1$ per NFR: `NFR-02`) | `1` | `1` | Phạm Nguyễn Đăng Hải |
| **Total for this workflow** | **`13`** | **`7`** | **Phạm Nguyễn Đăng Hải** |

**Pass criteria at Defense 1.1**

| Criterion | Committed |
| :--- | :--- |
| Runs end-to-end on staging with seeded data | `Yes` |
| Main scenario passes | `Yes` |
| Exception scenarios passing | `100%` (Tối thiểu $\ge 80\%$) |
| Test pass rate for this workflow | $\ge 90\%$ tổng số; `100%` test luồng chính |
| Open Critical / High defects | `0` |

**Reviewer note**

| Note | |
| :--- | :--- |

---

#### 6.3 Workflow WF-03

| Trường thông tin | Nội dung |
| :--- | :--- |
| **Workflow name** | Xây dựng và phê duyệt ngân hàng câu hỏi (Question bank and Rubric approval) |
| **Owner (one member)** | Nguyễn Trọng Tốt |
| **Supporting members** | Phạm Nguyễn Đăng Hải, Nguyễn Quang Thành |
| **Actors** | Giảng viên, Trưởng bộ môn |
| **Trigger** | Giảng viên tạo câu hỏi mới (luồng luyện tập/thi thử theo CLO hoặc luồng thi thật bám sát ma trận đề của Trưởng bộ môn) và gửi duyệt |
| **Business outcome** | Câu hỏi và bộ tiêu chí Rubric đạt chuẩn 10.0 điểm được Trưởng bộ môn phê duyệt đưa vào kho câu hỏi luyện tập thuộc kho luyện tập và thi thử hoặc ngân hàng câu hỏi thi thật chính thức |
| **Main entity state path** | Bản thảo câu hỏi $\to$ Đã gửi chờ duyệt $\to$ Phê duyệt ban hành vào ngân hàng đề, hoặc Yêu cầu chỉnh sửa, hoặc Từ chối |
| **Activity diagram (Report 4 section)** | Report 4 (SDD) — Activity Diagram `WF-03` (Question Bank & Rubric Approval) |

**Features in this workflow**

| Feature ID | Feature | Acceptance criteria | Test case IDs |
| :---: | :--- | :--- | :--- |
| **F-07** | Tạo câu hỏi vấn đáp 2 luồng và thiết kế Rubric | Giảng viên soạn câu hỏi độc lập cho kho luyện tập/thi thử theo CLO thuộc kho luyện tập và thi thử hoặc soạn câu hỏi thi thật bám sát đề thi mẫu / ma trận cấu trúc đề của Trưởng bộ môn thuộc ngân hàng câu hỏi thi thật; chỉ cho phép lưu khi có đáp án mẫu hợp lệ và bộ tiêu chí Rubric có tổng trọng số bằng đúng 10.0 điểm. | `TC-F07-01`..`TC-F07-03` |
| **F-08** | Gửi duyệt và phê duyệt câu hỏi vào ngân hàng | Câu hỏi do Giảng viên tạo phải gửi chờ duyệt; chỉ những câu hỏi đã được Trưởng bộ môn phê duyệt (`APPROVED`) mới được phát hành vào kho câu hỏi luyện tập thuộc kho luyện tập và thi thử hoặc ngân hàng câu hỏi thi thật chính thức. | `TC-F08-01`..`TC-F08-03` |

**Demo scenarios**

| Scenario ID | Type | Preconditions and data | Main steps | Expected result | Presenter |
| :---: | :---: | :--- | :--- | :--- | :--- |
| **DS-05** (`DS-03a`) | Chính | Tài khoản Giảng viên và Trưởng bộ môn; môn học đã có danh sách CLO và ma trận cấu trúc đề thi mẫu | Giảng viên tạo câu hỏi (luồng luyện tập theo CLO hoặc luồng thi thật theo ma trận đề) kèm đáp án mẫu và Rubric 10.0 điểm rồi gửi duyệt $\to$ Trưởng bộ môn xem xét và bấm Phê duyệt | Câu hỏi chuyển sang trạng thái đã duyệt (`APPROVED`) và được phát hành vào đúng kho câu hỏi luyện tập hoặc ngân hàng đề thi thật | Nguyễn Trọng Tốt |
| **DS-06** (`DS-03b`) | Ngoại lệ | Giảng viên nhập bộ tiêu chí Rubric có tổng điểm các thành phần khác 10.0 điểm | Giảng viên bấm lưu hoặc gửi duyệt câu hỏi với bộ Rubric chưa hợp lệ | Hệ thống từ chối lưu và hiển thị thông báo yêu cầu điều chỉnh lại trọng số các tiêu chí Rubric bằng đúng 10.0 điểm | Nguyễn Trọng Tốt |

**Committed test cases**

| Test type | Count | Automated in CI | Responsible member(s) |
| :--- | :---: | :---: | :--- |
| Feature tests ($\ge 3$ per feature) | `6` | `3` | Nguyễn Trọng Tốt |
| End-to-end workflow test ($\ge 1$) | `1` | `0` | Nguyễn Trọng Tốt |
| State transition tests ($\ge 1$ per transition) | `4` | `2` | Nguyễn Trọng Tốt |
| NFR tests ($\ge 1$ per NFR: `NFR-03`, `NFR-04`) | `2` | `2` | Nguyễn Trọng Tốt |
| **Total for this workflow** | **`13`** | **`7`** | **Nguyễn Trọng Tốt** |

**Pass criteria at Defense 1.1**

| Criterion | Committed |
| :--- | :--- |
| Runs end-to-end on staging with seeded data | `Yes` |
| Main scenario passes | `Yes` |
| Exception scenarios passing | `100%` (Tối thiểu $\ge 80\%$) |
| Test pass rate for this workflow | $\ge 90\%$ tổng số; `100%` test luồng chính |
| Open Critical / High defects | `0` |

**Reviewer note**

| Note | |
| :--- | :--- |

---

#### 6.4 Workflow WF-04

| Trường thông tin | Nội dung |
| :--- | :--- |
| **Workflow name** | Thi thật phòng máy, hậu kiểm và phúc khảo (Official lab exam, audit, and appeal) |
| **Owner (one member)** | Lê Vũ Hoàng |
| **Supporting members** | Nguyễn Trọng Tốt, Phạm Nguyễn Đăng Hải |
| **Actors** | Trưởng bộ môn, Cán bộ Khảo thí, Giám thị, Sinh viên, Giảng viên |
| **Trigger** | Cán bộ Khảo thí mở xuất bản đề thi ca thi chính thức và Giám thị điểm danh từng sinh viên theo chỗ ngồi (Seat 1–40) |
| **Business outcome** | Bài thi và tệp ghi âm được niêm phong lưu trữ, AI chấm sơ bộ, Giảng viên hậu kiểm toàn bộ bài nộp và công bố điểm 1 lần duy nhất (kích hoạt cơ chế khóa bảng điểm một chiều), hỗ trợ quy trình phúc khảo 5 bước |
| **Main entity state path** | Thẻ thi: Lên lịch ca thi $\to$ Đang thi $\to$ Đã nộp bài $\to$ Đã chấm sơ bộ $\to$ Đã hậu kiểm $\to$ Đã công bố và khóa bảng điểm một chiều. Đơn phúc khảo: Tiếp nhận đơn $\to$ Phân công chấm lại độc lập $\to$ Giảng viên đề xuất điểm $\to$ Khảo thí phê duyệt quyết định cuối cùng |
| **Activity diagram (Report 4 section)** | Report 4 (SDD) — Activity Diagram `WF-04` (Official Lab Exam, Audit & Appeal) |

**Features in this workflow**

| Feature ID | Feature | Acceptance criteria | Test case IDs |
| :---: | :--- | :--- | :--- |
| **F-09** | Tổ chức kỳ thi, ca thi phòng máy và điểm danh | Trưởng bộ môn cấu hình Follow-up cho đề/ca thi thật (1–5 câu, mặc định 2); Cán bộ Khảo thí tạo kỳ thi, ca thi phòng máy, phân công Giám thị/Giám khảo, import danh sách sinh viên, phân bổ đề và mở xuất bản đề thi ca thi chính thức; Giám thị chỉ điểm danh từng sinh viên theo chỗ ngồi (Seat 1–40, đối chiếu thẻ SV/CCCD). | `TC-F09-01`..`TC-F09-03` |
| **F-10** | Làm bài thi thật trên giao diện phòng thi khóa màn hình | Sinh viên chỉ vào được bài thi khi đã được Giám thị điểm danh, Cán bộ Khảo thí mở xuất bản đề và kiểm tra microphone đạt yêu cầu; giao diện khóa toàn màn hình, tự động đình chỉ bài thi và báo Giám thị ghi nhận nhật ký vi phạm an ninh phòng thi nếu thoát màn hình/mất tiêu điểm quá 3 lần, đồng thời lưu tệp ghi âm kèm mã băm `SHA-256` khi nộp bài. | `TC-F10-01`..`TC-F10-03` |
| **F-11** | Hậu kiểm kết quả chấm và công bố điểm ca thi | Giảng viên nghe lại tệp ghi âm, đối chiếu nhận xét của AI và điều chỉnh điểm kèm văn bản giải trình lý do điều chỉnh nếu cần; khi 100% bài nộp trong ca thi đã được xác minh, Giảng viên / Cán bộ Khảo thí công bố bảng điểm ca thi một lần duy nhất, tự động kích hoạt cơ chế khóa bảng điểm một chiều chống sửa đổi. | `TC-F11-01`..`TC-F11-03` |
| **F-12** | Xác nhận điểm và xử lý phúc khảo nội bộ 5 bước | Sinh viên xem điểm đã công bố để xác nhận hoặc nộp đơn phúc khảo (đơn phúc khảo chính thức) lên Cán bộ Khảo thí $\to$ Phòng Khảo thí và Trưởng bộ môn cùng nhận thông báo in-app; Khảo thí thẩm tra hợp lệ $\to$ Cán bộ Khảo thí phân công Giảng viên độc lập chấm lại (định mức $\le 24$ bài/GV, loại trừ 100% Giám khảo chấm ban đầu; thuật toán đề xuất phân bổ tự động và điều chỉnh thủ công) $\to$ Giảng viên chấm lại trên Evidence Panel $\to$ Cán bộ Khảo thí phê duyệt quyết định cuối cùng (mở ngoại lệ cơ chế khóa bảng điểm một chiều cập nhật điểm chính thức nếu chấp thuận; Trưởng bộ môn nhận thông báo giám sát học thuật). | `TC-F12-01`..`TC-F12-03` |

**Demo scenarios**

| Scenario ID | Type | Preconditions and data | Main steps | Expected result | Presenter |
| :---: | :---: | :--- | :--- | :--- | :--- |
| **DS-07** (`DS-04a`) | Chính | Ca thi phòng máy đã được lên lịch, gán danh sách sinh viên, Cán bộ Khảo thí, Giám thị và Giảng viên | Cán bộ Khảo thí xuất bản đề ca thi & Giám thị điểm danh từng sinh viên (Seat 1–40) $\to$ Sinh viên kiểm tra mic, làm bài trên màn hình khóa và nộp bài $\to$ Giảng viên xác minh toàn bộ bài nộp và Giảng viên / Cán bộ Khảo thí bấm Công bố điểm | Bài thi được lưu kèm tệp ghi âm `SHA-256`, chấm sơ bộ, xác minh hoàn tất và công bố thành công 1 lần duy nhất (đồng thời kích hoạt cơ chế khóa bảng điểm một chiều) | Lê Vũ Hoàng |
| **DS-08** (`DS-04b`) | Ngoại lệ | Bảng điểm ca thi đã được công bố và khóa chỉnh sửa trực tiếp | Thử chỉnh sửa trực tiếp điểm đã công bố (bị chặn) $\to$ Sinh viên nộp đơn phúc khảo lên Cán bộ Khảo thí $\to$ Khảo thí & Trưởng bộ môn nhận thông báo; Khảo thí thẩm tra và phân công Giảng viên độc lập chấm lại ($\le 24$ bài/GV, loại trừ 100% Giám khảo chấm ban đầu) $\to$ Giảng viên chấm lại và Cán bộ Khảo thí phê duyệt quyết định cuối cùng | Thao tác sửa điểm trực tiếp bị hệ thống từ chối do vi phạm quy chế bảo toàn điểm; điểm số chỉ được cập nhật sau khi hoàn tất quy trình phúc khảo 5 bước được Cán bộ Khảo thí phê duyệt | Lê Vũ Hoàng |

**Committed test cases**

| Test type | Count | Automated in CI | Responsible member(s) |
| :--- | :---: | :---: | :--- |
| Feature tests ($\ge 3$ per feature) | `12` | `6` | Lê Vũ Hoàng |
| End-to-end workflow test ($\ge 1$) | `1` | `0` | Lê Vũ Hoàng |
| State transition tests ($\ge 1$ per transition) | `8` | `4` | Lê Vũ Hoàng |
| NFR tests ($\ge 1$ per NFR: `NFR-05`) | `1` | `0` | Lê Vũ Hoàng |
| **Total for this workflow** | **`22`** | **`10`** | **Lê Vũ Hoàng** |

**Pass criteria at Defense 1.1**

| Criterion | Committed |
| :--- | :--- |
| Runs end-to-end on staging with seeded data | `Yes` |
| Main scenario passes | `Yes` |
| Exception scenarios passing | `100%` (Tối thiểu $\ge 80\%$) |
| Test pass rate for this workflow | $\ge 90\%$ tổng số; `100%` test luồng chính |
| Open Critical / High defects | `0` |

**Reviewer note**

| Note | |
| :--- | :--- |

---

### 7. Individual Commitments (Cam kết theo từng thành viên)

#### 7.1 Member 1 — Nguyễn Quang Thành

| Trường thông tin | Nội dung |
| :--- | :--- |
| **Full name** | Nguyễn Quang Thành |
| **Student ID** | |
| **Role in team** | Team Leader & Backend |

**Ownership**

| Loại sở hữu (Type) | Hạng mục (Items: IDs / names) | Ghi chú (Notes) |
| :--- | :--- | :--- |
| **Main workflow(s) owned** | `WF-01` — Luyện tập vấn đáp tương tác | Điều phối toàn trình phiên luyện tập, gỡ băng, sửa thuật ngữ và hỏi phụ |
| **Mandatory features owned** | `F-01`, `F-03`, `F-04` | Xác thực phân quyền 6 vai trò & phân cấp lớp học, phiên luyện tập tương tác, chấm điểm & lịch sử |
| **Modules / entities owned** | Phân hệ Luyện tập vấn đáp, Kênh tương tác âm thanh thời gian thực, Báo cáo và bảng điểm sinh viên | Backend APIs, hàng đợi chấm ngầm và SignalR Hub |
| **Design decisions owned (D-IDs)** | `D1` (Kiến trúc ứng dụng & hàng đợi chấm ngầm); `R2` (Rà soát thuật ngữ chuyên ngành) | Phụ trách thiết kế kiến trúc phân tầng & xử lý bất đồng bộ |
| **NFRs responsible** | `NFR-01` (Thời gian ghi nhận nộp bài $\le 1\text{ giây}$) | Lưu trạng thái nộp bài tức thì trước khi đẩy vào hàng đợi chấm ngầm |

**Delivery commitments to Defense 1.1**

| Cam kết bàn giao (Commitment) | Số lượng / Mã định danh (Committed: count / IDs) | Hạn hoàn thành (Due) |
| :--- | :--- | :--- |
| **Functional test cases written** | `12` (`TC-F01-01..03`, `TC-F03-01..03`, `TC-F04-01..03`, `TC-WF01-E2E`, `TC-ST-PS01`, `TC-NFR01`) | Trước Defense 1.1 |
| **Of which automated in CI** | `6` | Trước Defense 1.1 |
| **Demo scenarios presented** | `2` (`DS-01` Chính, `DS-02` Ngoại lệ) | Tại buổi Defense 1.1 |
| **Required fixes owned (section 9)** | `1` (`RF-01` — Đồng bộ mã nguồn sơ đồ Mermaid và Draw.io trong kho mã nguồn) | Trước Defense 1.1 |
| **Documentation sections (Report / section)** | Report 2 (Quy trình phát triển), Report 3 (Mục 1, 2.1, 2.2.2, 3.5, 5.2), Report 4 (Kiến trúc tổng thể & `WF-01`), Report 5 (`WF-01` Tests) | Trước Defense 1.1 |

**Ownership verification at Review 2 (completed by reviewers)**

| Result | Weak points observed | Follow-up (by whom, by when) |
| :--- | :--- | :--- |
| ☐ Verified \| ☐ Partly verified \| ☐ Not verified | | |

**Member confirmation**

| Signature / date | |
| :--- | :--- |

---

#### 7.2 Member 2 — Phạm Nguyễn Đăng Hải

| Trường thông tin | Nội dung |
| :--- | :--- |
| **Full name** | Phạm Nguyễn Đăng Hải |
| **Student ID** | |
| **Role in team** | Database & Frontend |

**Ownership**

| Loại sở hữu (Type) | Hạng mục (Items: IDs / names) | Ghi chú (Notes) |
| :--- | :--- | :--- |
| **Main workflow(s) owned** | `WF-02` — Thi thử vấn đáp tính giờ | Kiểm soát hạn ngạch ngày, thu bài tự động khi hết giờ và bảng điểm CLO |
| **Mandatory features owned** | `F-02`, `F-05`, `F-06` | Cấu hình tham số/hạn ngạch, thi thử tính giờ, bảng điểm theo CLO |
| **Modules / entities owned** | Phân hệ Thi thử tính giờ và kiểm soát hạn ngạch, Quản lý chuẩn đầu ra môn học, Giao diện thi thử và cấu hình học vụ | Thiết kế ERD 3NF, tối ưu truy vấn và giao diện `WF-02` |
| **Design decisions owned (D-IDs)** | `D5` (Tách 2 luồng tạo câu hỏi luyện tập/thi thật & lưu bản chụp Rubric); `G3` (Môi trường Staging & Seed Data) | Bảo mật ngân hàng đề thi và chuẩn hóa dữ liệu mẫu demo |
| **NFRs responsible** | `NFR-02` (Thời gian phản hồi tra cứu API $\le 2\text{ giây}$) | Tối ưu chỉ mục CSDL và hiệu năng truy vấn danh sách/bảng điểm |

**Delivery commitments to Defense 1.1**

| Cam kết bàn giao (Commitment) | Số lượng / Mã định danh (Committed: count / IDs) | Hạn hoàn thành (Due) |
| :--- | :--- | :--- |
| **Functional test cases written** | `13` (`TC-F02-01..03`, `TC-F05-01..03`, `TC-F06-01..03`, `TC-WF02-E2E`, `TC-ST-MS01..02`, `TC-NFR02`) | Trước Defense 1.1 |
| **Of which automated in CI** | `7` | Trước Defense 1.1 |
| **Demo scenarios presented** | `2` (`DS-03` Chính, `DS-04` Ngoại lệ) | Tại buổi Defense 1.1 |
| **Required fixes owned (section 9)** | `1` (`RF-03` — Rà soát môi trường Staging và kịch bản nạp dữ liệu mẫu tự động) | Trước Defense 1.1 |
| **Documentation sections (Report / section)** | Report 2 (Mục 1.1, 1.2, 3, 4), Report 3 (Context Diagram, Use Case Diagram, Conceptual ERD, Mục 3.4, 4.1), Report 4 (Database Design & `WF-02`), Report 5 (`WF-02` Tests) | Trước Defense 1.1 |

**Ownership verification at Review 2 (completed by reviewers)**

| Result | Weak points observed | Follow-up (by whom, by when) |
| :--- | :--- | :--- |
| ☐ Verified \| ☐ Partly verified \| ☐ Not verified | | |

**Member confirmation**

| Signature / date | |
| :--- | :--- |

---

#### 7.3 Member 3 — Nguyễn Trọng Tốt

| Trường thông tin | Nội dung |
| :--- | :--- |
| **Full name** | Nguyễn Trọng Tốt |
| **Student ID** | |
| **Role in team** | Backend, AI & QA |

**Ownership**

| Loại sở hữu (Type) | Hạng mục (Items: IDs / names) | Ghi chú (Notes) |
| :--- | :--- | :--- |
| **Main workflow(s) owned** | `WF-03` — Xây dựng và phê duyệt ngân hàng câu hỏi | Soạn câu hỏi 2 luồng (theo CLO và theo ma trận đề), kiểm tra Rubric 10.0 điểm và duyệt đề |
| **Mandatory features owned** | `F-07`, `F-08` | Tạo câu hỏi 2 luồng & thiết kế Rubric 10.0 điểm, gửi duyệt & Trưởng bộ môn phê duyệt |
| **Modules / entities owned** | Ngân hàng câu hỏi và tiêu chuẩn Rubric, Tích hợp dịch vụ trí tuệ nhân tạo, Cơ chế khóa điểm một chiều, Quy trình giải quyết phúc khảo 5 bước, Hàng đợi xử lý sự cố chấm điểm | Dịch vụ sinh câu hỏi/chấm điểm AI, chịu lỗi Polly/DLQ (Admin MF-01/02, Khảo thí MF-04) và bộ kiểm thử tự động |
| **Design decisions owned (D-IDs)** | `D2` (AI hỗ trợ có giải thích dưới quyền kiểm soát của giảng viên); `D3` (Phân tách nhiệm vụ 6 vai trò, khóa điểm & phúc khảo 5 bước); `R1` (Chịu lỗi AI & phân quyền DLQ) | Đảm bảo con người giữ quyền quyết định cuối cùng và không mất bài nộp |
| **NFRs responsible** | `NFR-03` (Độ tin cậy chấm điểm, thử lại 3 lần & DLQ); `NFR-04` (Toàn vẹn điểm số sau công bố một chiều) | Kiểm soát hàng đợi lỗi AI và chặn sửa/xóa điểm trái phép |

**Delivery commitments to Defense 1.1**

| Cam kết bàn giao (Commitment) | Số lượng / Mã định danh (Committed: count / IDs) | Hạn hoàn thành (Due) |
| :--- | :--- | :--- |
| **Functional test cases written** | `13` (`TC-F07-01..03`, `TC-F08-01..03`, `TC-WF03-E2E`, `TC-ST-Q01..04`, `TC-NFR03`, `TC-NFR04`) | Trước Defense 1.1 |
| **Of which automated in CI** | `7` | Trước Defense 1.1 |
| **Demo scenarios presented** | `2` (`DS-05` Chính, `DS-06` Ngoại lệ) | Tại buổi Defense 1.1 |
| **Required fixes owned (section 9)** | `1` (`RF-02` — Đồng bộ chi tiết 6 vai trò và quy trình hậu kiểm/phúc khảo 5 bước giữa BM03, SRS và SDD) | Trước Defense 1.1 |
| **Documentation sections (Report / section)** | Report 1 (Toàn bộ), Report 3 (Logical ERD, Mục 3.1.4, 3.3, 5.1/5.3 Question & Grade/Appeal), Report 4 (`WF-03` & AI/Resilience Design), Report 5 (Test Plan & `WF-03` Tests) | Trước Defense 1.1 |

**Ownership verification at Review 2 (completed by reviewers)**

| Result | Weak points observed | Follow-up (by whom, by when) |
| :--- | :--- | :--- |
| ☐ Verified \| ☐ Partly verified \| ☐ Not verified | | |

**Member confirmation**

| Signature / date | |
| :--- | :--- |

---

#### 7.4 Member 4 — Lê Vũ Hoàng

| Trường thông tin | Nội dung |
| :--- | :--- |
| **Full name** | Lê Vũ Hoàng |
| **Student ID** | |
| **Role in team** | Lead Frontend |

**Ownership**

| Loại sở hữu (Type) | Hạng mục (Items: IDs / names) | Ghi chú (Notes) |
| :--- | :--- | :--- |
| **Main workflow(s) owned** | `WF-04` — Thi thật phòng máy, hậu kiểm và phúc khảo | Khảo thí mở đề, Giám thị điểm danh, làm bài Kiosk khóa màn hình, hậu kiểm, công bố điểm & phúc khảo 5 bước |
| **Mandatory features owned** | `F-09`, `F-10`, `F-11`, `F-12` | Tổ chức ca thi & điểm danh, Kiosk khóa màn hình, hậu kiểm & công bố điểm, xác nhận điểm & phúc khảo 5 bước |
| **Modules / entities owned** | Giao diện nền tảng và phân quyền 6 vai trò, Giao diện phòng thi Kiosk khóa màn hình, Nhật ký giám sát phòng thi, Bảng điều khiển hậu kiểm và đối chiếu bằng chứng của Giám khảo | Kiểm soát vi phạm toàn màn hình, niêm phong ghi âm `SHA-256` và trình phát âm thanh hậu kiểm |
| **Design decisions owned (D-IDs)** | `D4` (Bắt buộc ghi âm qua microphone, chỉnh sửa bản phiên âm & chỉ lưu ghi âm cho thi thật); `R3` (An ninh phòng thi Kiosk & khóa điểm sau công bố) | Bảo vệ tính trung thực của kỳ thi tại phòng máy và bằng chứng ghi âm |
| **NFRs responsible** | `NFR-05` (Khớp mã băm `SHA-256` tệp ghi âm và tự động đình chỉ khi thoát màn hình/mất tiêu điểm $> 3$ lần) | Kiểm chứng toàn vẹn tệp ghi âm và cơ chế chống gian lận trên Kiosk |

**Delivery commitments to Defense 1.1**

| Cam kết bàn giao (Commitment) | Số lượng / Mã định danh (Committed: count / IDs) | Hạn hoàn thành (Due) |
| :--- | :--- | :--- |
| **Functional test cases written** | `22` (`TC-F09-01..03`, `TC-F10-01..03`, `TC-F11-01..03`, `TC-F12-01..03`, `TC-WF04-E2E`, `TC-ST-TK01..05`, `TC-ST-AP01..03`, `TC-NFR05`) | Trước Defense 1.1 |
| **Of which automated in CI** | `10` | Trước Defense 1.1 |
| **Demo scenarios presented** | `2` (`DS-07` Chính, `DS-08` Ngoại lệ) | Tại buổi Defense 1.1 |
| **Required fixes owned (section 9)** | `1` (`RF-04` — Hoàn tất kiểm thử tích hợp bộ đếm vi phạm Kiosk, đối chiếu SHA-256 và luồng công bố/khóa điểm) | Trước Defense 1.1 |
| **Documentation sections (Report / section)** | Report 2 (Kế hoạch tổng thể FE/BE), Report 3 (Screen Flows, Mục 3.2, 4.2, 5.4), Report 4 (Frontend Architecture & `WF-04`), Report 5 (`WF-04` Tests) | Trước Defense 1.1 |

**Ownership verification at Review 2 (completed by reviewers)**

| Result | Weak points observed | Follow-up (by whom, by when) |
| :--- | :--- | :--- |
| ☐ Verified \| ☐ Partly verified \| ☐ Not verified | | |

**Member confirmation**

| Signature / date | |
| :--- | :--- |

---

### 8. Summary and Cross-check (Bảng tổng hợp và đối chiếu)

#### 8.1 By workflow (Tổng hợp theo Workflow)

| Workflow | Owner | Features | Demo scenarios (main / exception) | Test cases | Automated |
| :---: | :--- | :--- | :---: | :---: | :---: |
| **WF-01** | Nguyễn Quang Thành | `3` (`F-01`, `F-03`, `F-04`) | `1 / 1` (`DS-01`, `DS-02`) | `12` | `6` |
| **WF-02** | Phạm Nguyễn Đăng Hải | `3` (`F-02`, `F-05`, `F-06`) | `1 / 1` (`DS-03`, `DS-04`) | `13` | `7` |
| **WF-03** | Nguyễn Trọng Tốt | `2` (`F-07`, `F-08`) | `1 / 1` (`DS-05`, `DS-06`) | `13` | `7` |
| **WF-04** | Lê Vũ Hoàng | `4` (`F-09`, `F-10`, `F-11`, `F-12`) | `1 / 1` (`DS-07`, `DS-08`) | `22` | `10` |
| **Total** | **4 thành viên** | **`12`** | **`8` (`4 / 4`)** | **`60`** | **`30`** |

#### 8.2 By member (Tổng hợp theo Thành viên)

| Member | Workflows owned | Features | Test cases | Automated | Demo scenarios | Review 2 verification |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **Nguyễn Quang Thành** | `WF-01` | `3` (`F-01`, `F-03`, `F-04`) | `12` | `6` | `2` (`DS-01`, `DS-02`) | |
| **Phạm Nguyễn Đăng Hải** | `WF-02` | `3` (`F-02`, `F-05`, `F-06`) | `13` | `7` | `2` (`DS-03`, `DS-04`) | |
| **Nguyễn Trọng Tốt** | `WF-03` | `2` (`F-07`, `F-08`) | `13` | `7` | `2` (`DS-05`, `DS-06`) | |
| **Lê Vũ Hoàng** | `WF-04` | `4` (`F-09`..`F-12`) | `22` | `10` | `2` (`DS-07`, `DS-08`) | |
| **Total** | **`4`** | **`12`** | **`60`** | **`30`** | **`8` (`4 / 4`)** | |

#### 8.3 Committed Numbers (Con số cam kết cố định)

| Chỉ tiêu cam kết | Giá trị chốt |
| :--- | :--- |
| **Number of main workflows** | `4` (`WF-01`..`WF-04`) |
| **Number of demo scenarios (main / exception)** | `8` (`4 Main / 4 Exception`: `DS-01`..`DS-08`) |
| **Committed functional test cases** | `60` (`36` Feature + `4` E2E + `15` State Transition + `5` NFR) |
| **Of which automated in CI** | `30` (`50%` tổng số test case cam kết) |

#### 8.4 Eligibility Thresholds for the Viva (Ngưỡng đủ điều kiện ra Viva)

| Criterion | Minimum threshold | Agreed threshold |
| :--- | :--- | :--- |
| **Main workflows** | 100% run end-to-end | `100%` (cả 4 workflow `WF-01`..`WF-04` chạy thông suốt end-to-end) |
| **Demo scenarios** | 100% main; $\ge 80\%$ exception | `100%` kịch bản chính (`4/4`); $\ge 80\%$ kịch bản ngoại lệ (`100%` mục tiêu, tối thiểu `4/4`) |
| **Test case count** | $\ge$ committed number | $\ge 60$ test cases chức năng |
| **Test pass rate** | $\ge 90\%$ overall; 100% main-path tests of mandatory features | $\ge 90\%$ tổng số ($\ge 54/60$); `100%` test luồng chính của 12 tính năng bắt buộc |
| **Automation** | $\ge 50\%$ run in CI | $\ge 50\%$ chạy tự động trong CI ($\ge 30$ test cases) |
| **Defects** | No open Critical or High defects | `0` lỗi mức Critical hoặc High đang mở |
| **Individual** | Each member demos the workflow they own and meets their delivery commitments | Cả 4 thành viên đều demo đạt workflow mình sở hữu và hoàn thành 100% cam kết cá nhân |

---

### 9. Required Fixes (Danh sách việc phải sửa / hoàn thiện trước Defense 1.1)

| ID | Hạng mục công việc (Item) | Related workflow | Owner | Due |
| :---: | :--- | :---: | :--- | :--- |
| **RF-01** | Cập nhật và lưu đầy đủ mã nguồn sơ đồ (Mermaid và Draw.io) cho sơ đồ kiến trúc, Logical ERD, trạng thái và 4 luồng nghiệp vụ vào kho mã nguồn khớp với Report 4 (SDD) (`G1`). | `WF-01` | Nguyễn Quang Thành | Trước Defense 1.1 |
| **RF-02** | Đồng bộ hóa đặc tả 6 vai trò người dùng và quy trình hậu kiểm, khóa điểm, chấm phúc khảo 5 bước giữa Phiếu đăng ký bản chốt (`BM03`), SRS và SDD (`G2`). | `WF-03` | Nguyễn Trọng Tốt | Trước Defense 1.1 |
| **RF-03** | Rà soát môi trường triển khai tập trung (Staging) và kịch bản khởi tạo dữ liệu mẫu tự động để phục vụ chạy lặp lại toàn bộ 8 kịch bản demo (`G3`). | `WF-02` | Phạm Nguyễn Đăng Hải | Trước Defense 1.1 |
| **RF-04** | Hoàn tất diễn tập tích hợp end-to-end cho giao diện phòng thi khóa màn hình (tự động đình chỉ khi thoát màn hình $> 3$ lần), đối chiếu mã băm `SHA-256` tệp ghi âm và khóa chỉnh sửa điểm sau công bố. | `WF-04` | Lê Vũ Hoàng | Trước Defense 1.1 |

---

### 10. Review 2 Conclusion (completed by reviewers)

☐ Pass     ☐ Conditional pass     ☐ Re-review

---

### 11. Confirmation (Xác nhận)

| Role | Full name | Signature | Date |
| :--- | :--- | :--- | :--- |
| **Reviewer 1** | | | |
| **Reviewer 2** | | | |
| **Supervisor** | TS. Nguyễn Thị Cẩm Hương | | |
| **Team leader** | Nguyễn Quang Thành | | |
