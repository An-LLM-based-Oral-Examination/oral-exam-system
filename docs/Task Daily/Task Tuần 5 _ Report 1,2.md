# Task Tuần 5 — Report 1, 2

File này là bảng việc chung. Cả nhóm mở file này để biết mình làm gì, nộp output ở đâu, và xem người khác đã xong chưa.

Nội dung báo cáo viết bằng tiếng Anh, đúng heading trong template môn SEP490, để ghép vào Report 7. File này chỉ dùng để giao việc và theo dõi.

## Thời gian

| Mốc | Thời điểm |
|---|---|
| Bắt đầu | Thứ Ba 06/10/2026 |
| Check ngày 1 | 21:00, 06/10/2026 |
| Check ngày 2 | 21:00, 07/10/2026 |
| Check ngày 3 — xong | 21:00, 08/10/2026 |

Ba ngày làm việc: 06/10, 07/10, 08/10. Mỗi ngày chỉ check một lần, lúc 21:00. Trước giờ đó mỗi người để file output đúng chỗ. Đúng 21:00 cả nhóm mở file này và các file output. Tốt ghi Đạt hoặc Chưa trong bảng check. Không check thêm lần thứ hai trong ngày.

## Quy tắc

1. Mỗi người chỉ viết các mục được giao. Không sửa mục của người khác.
2. Các mục có liên quan nằm trọn ở một người. Người đó viết theo thứ tự trong phần của mình.
3. Chỉ có một điểm chờ: tối 06/10, Tốt chốt danh sách FE và LI. Sáng 07/10 Hải dùng đúng danh sách đó để ước lượng. Thành và Hoàng không chờ danh sách này.
4. Sau 21:00 ngày 06/10 không thêm, không xóa, không đổi mã FE/LI. Ngày 08/10 chỉ sửa câu chữ.
5. Report 1, mục 6 (Scope & Limitations) do Tốt viết, không dùng AI.
6. Sơ đồ quy trình do Thành vẽ, không dùng AI. Tham khảo [uml-diagram.org](https://www.uml-diagrams.org/).

## Tên dùng chung

Hải, Thành và Hoàng dùng đúng các tên dưới đây. Không đặt tên khác, nên không phải chờ nhau thống nhất.

**Mốc nộp cho giảng viên** (Hải ghi vào mục 3):

| Deliverable | Hạn |
|---|---|
| Report 1 — Project Introduction | Cuối Tuần 6 |
| Report 2 — Project Management Plan | Cuối Tuần 6 |
| Report 3 — SRS | Cuối Tuần 7 |
| Report 4 — Software Design | Cuối Tuần 9 |
| Report 5 — Test (2 file Excel) | Cuối Tuần 11 |
| Report 6 — User Guides | Cuối Tuần 13 |
| Review 3 | Tuần 14 |

**Stage:** Initiation, Planning & Requirement, Software Design, Implementation, Verification, Transition.

**Mức test:** Reviewing, Unit Test, Integration Test, System Test, Acceptance Test.

## Chỗ để output

| Người | File |
|---|---|
| Tốt | `docs/week5/Tot_Report1.md` |
| Hải | `docs/week5/Hai_Report2.md` |
| Thành | `docs/week5/Thanh_Report2.md` |
| Hoàng | `docs/week5/Hoang_Report2.md` |

Mỗi ngày bổ sung vào đúng file của mình. Phần đã xong ở lần check trước thì giữ nguyên, chỉ thêm phần của ngày hôm đó.

## Tốt — Report 1

Tốt viết toàn bộ Report 1.

| Ngày | Output phải có lúc 21:00 |
|---|---|
| 06/10 | II.1 Overview (tên dự án, mã, nhóm, loại phần mềm, bảng thành viên). II.6 Project Scope & Limitations: danh sách FE và LI đã đánh số, viết xong nội dung, không dùng AI. Đây là bản chốt để Hải ước lượng. |
| 07/10 | II.2 Product Background. II.3 Existing Systems. II.4 Business Opportunity. |
| 08/10 | II.5 Software Product Vision. I. Record of Changes (một dòng cho lần nộp này). Đọc lại cả Report 1 cho khớp với mã FE/LI đã chốt ngày 06/10. |

## Hải — Report 2, phần kế hoạch và con số

Viết theo thứ tự trong ngày. Mục sau dùng bảng của mục trước.

| Ngày | Output phải có lúc 21:00 |
|---|---|
| 06/10 | Mục 3 Project Deliverables: bảng deliverable và hạn nộp theo mốc đã khóa ở trên. Mục 4 Responsibility Assignments: ma trận D / R / S / I cho các deliverable đó, gồm Report 1 của Tốt. Khung bảng mục 1.1 để trống, chờ danh sách FE. |
| 07/10 | Mục 1.1 Scope & Estimation: mỗi FE một dòng, mức Simple / Medium / Complex, effort tính bằng man-day, có dòng tổng. Dùng đúng mã FE tối 06/10. |
| 08/10 | Mục 1.2 Project Objectives: mục tiêu chất lượng, phần trăm đúng hạn từng mốc, chia tổng man-day cho requirement, design, coding, testing, quản lý dự án. Rà mục 4 cho khớp tên deliverable ở mục 3. |

Hải không vẽ quy trình và không viết phần trăm coverage vào mục của Thành. Số coverage và số defect nằm ở bảng 1.2.

## Thành — Report 2, phần cách làm việc

Không dùng số man-day và không dùng bảng của Hải.

| Ngày | Output phải có lúc 21:00 |
|---|---|
| 06/10 | Mục 2.1 Project Process: mô tả quy trình theo 6 stage đã khóa, kèm sơ đồ do Thành tự vẽ. |
| 07/10 | Mục 2.2 Quality Management: cách làm Reviewing, Unit Test, Integration Test, System Test. Chỉ mô tả hoạt động. Không ghi phần trăm coverage hay số defect. |
| 08/10 | Mục 5 Project Communications: ai trao đổi với ai, mục đích, tần suất, kênh. |

## Hoàng — Report 2, phần rủi ro và môi trường

Không lập lịch và không điền man-day.

| Ngày | Output phải có lúc 21:00 |
|---|---|
| 06/10 | Mục 6.3 Tools & Infrastructures. Mục 6.2 Source Code Management. |
| 07/10 | Mục 6.1 Document Management. Mục 2.3 Training Plan, cùng bộ công nghệ đã ghi ở 6.3. |
| 08/10 | Mục 1.3 Project Risks: mô tả, impact, possibility, cách ứng phó. Viết từ việc đã biết của đồ án, không trích bảng effort. |

## Bảng check mỗi ngày

Tốt điền lúc 21:00. Ô để trống cho đến giờ check.

| Ngày | Tốt | Hải | Thành | Hoàng | Việc còn thiếu, xử lý trong ngày kế |
|---|---|---|---|---|---|
| 06/10 |  |  |  |  |  |
| 07/10 |  |  |  |  |  |
| 08/10 |  |  |  |  |  |

Đạt nghĩa là file output của ngày đó có đủ các mục trong bảng giao việc, người khác mở ra đọc được. Chưa nghĩa là thiếu mục hoặc chưa có file. Người Chưa hoàn tất phần thiếu trước khi làm output ngày hôm sau. Thành và Hoàng không nhận thêm việc của Hải nếu Hải đang chờ danh sách FE.

## Xong sprint

21:00 ngày 08/10/2026, bốn file output đủ các mục sau:

- Tốt: Report 1 đủ I và II.1 đến II.6.
- Hải: Report 2 mục 1.1, 1.2, 3, 4.
- Thành: Report 2 mục 2.1, 2.2, 5.
- Hoàng: Report 2 mục 1.3, 2.3, 6.1, 6.2, 6.3.

Record of Changes của Report 2 do Tốt ghi một dòng khi ghép vào Report 7, không giao trong ba ngày này.
