# Task Tuần 7 — Report 3

File này là bảng việc chung cho mục III Software Requirement Specification. Cả nhóm mở file này để biết mình làm gì, nộp output ở đâu, và xem người khác đã xong chưa.

Nội dung báo cáo viết bằng tiếng Anh, đúng heading trong template môn SEP490, để ghép vào Report 7. File này chỉ dùng để giao việc và theo dõi.

Report 3 nộp cuối Tuần 7. Ba ngày dưới đây là lịch review. Sau 21:00 ngày 22/10, Tốt ghép bốn file vào Report 7. Việc ghép không nằm trong ba ngày check.

## Thời gian


| Mốc                 | Thời điểm         |
| ------------------- | ----------------- |
| Bắt đầu             | Thứ Ba 13/10/2026 |
| Check ngày 1        | 21:00, 14/10/2026 |
| Check ngày 2        | 21:00, 15/10/2026 |
| Check ngày 3 — xong | 21:00, 16/10/2026 |


## Quy tắc

1. Mỗi người chỉ viết các mục được giao. Không sửa mục của người khác.
2. Các mục có liên quan nằm trọn ở một người. Người vẽ screen flow của nhóm màn hình nào thì viết bảng mô tả màn hình, bảng phân quyền, đặc tả function, business rule và message của nhóm đó.
3. Không có điểm chờ. Tên actor, tên feature và mã function lấy từ Figure 1.6.1 Major Features. Không đặt tên khác.
4. Trong cả ba ngày không thêm, không xóa, không đổi mã function. Mục 2.2.2 do Thành viết toàn bộ. Người khác không viết mô tả use case.
5. Diagram do người được giao tự vẽ, không dùng AI. Vẽ bằng draw.io. Tham khảo [uml-diagram.org](https://www.uml-diagrams.org/).
6. Hải chỉ vẽ ba diagram hệ thống: context diagram, use case diagram, Conceptual ERD. Screen flow và Logical ERD không tính vào ba diagram này.



## Tên dùng chung

Dùng đúng tên tiếng Anh dưới đây trong report.

**Actor:** Student, Lecturer, Department Head, Proctor, Admin.

**Feature và mã function:**


| Feature                     | Mã function |
| --------------------------- | ----------- |
| Authentication              | 1.1–1.4     |
| Practice exam               | 2.1–2.14    |
| Mock examination            | 3.1–3.15    |
| Question management         | 4.1–4.19    |
| Examination management      | 5.1–5.11    |
| Laboratory examination      | 6.1–6.14    |
| Grade and appeal management | 7.1–7.18    |
| System configuration        | 8.1–8.19    |
| User management             | 9.1–9.5     |
| Dashboard                   | 10.1–10.3   |
| Notification                | 11.1–11.3   |


**Hệ thống ngoài** (Hải dùng cho context diagram và mục 4.1): Google, FLM, Gemini, Whisper, Cloudflare R2.

## Thành

Thành không vẽ context diagram, use case diagram, Conceptual ERD, Logical ERD.


| Ngày  | Output phải có lúc 21:00                                                                                                                                                                                                                                                                                     |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 14/10 | Mục 1 Product Overview: phần chữ (người dùng, môi trường, ràng buộc, giả định, phụ thuộc). Mục 2.1 Actors: bảng năm actor. Mục 2.2.2: mô tả use case 1.1–1.4, 2.1–2.14, 9.1–9.5, 10.1–10.3, 11.1–11.3. Mỗi use case một bảng: mã, tên, actor, mô tả, điều kiện trước, điều kiện sau, luồng chính, luồng phụ. |
| 15/10 | Mục 2.2.2 tiếp: 3.1–3.15, 4.1–4.19, 5.1–5.11.                                                                                                                                                                                                                                                                |
| 16/10 | Mục 2.2.2 nốt: 6.1–6.14, 7.1–7.18, 8.1–8.19. Mục 3.5: đặc tả function 8.1–8.19 (mục đích, actor, input, output, ràng buộc, ngoại lệ). Mục 3.1.1: một screen flow cấu hình hệ thống. Mục 3.1.2 và 3.1.3 của các màn hình đó. Mục 5.1 và 5.3 của System configuration. Mục 5.2 Common Requirements.            |


## Hải

Hải vẽ đủ ba diagram hệ thống trong hai ngày đầu để cả nhóm review hình trước khi viết nốt đặc tả.


| Ngày  | Output phải có lúc 21:00                                                                                                                                                                                            |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 14/10 | Mục 1: context diagram và chú thích từng khối. Mục 2.2.1: một use case diagram cả đề tài, mức 11 feature, không vẽ từng function. Mục 4.1 External Interfaces: chữ cho Google, FLM, Gemini, Whisper, Cloudflare R2. |
| 15/10 | Mục 3.1.5: Conceptual ERD (entity và quan hệ, chưa kẻ cột). Mục 3.1.1: ba screen flow — đăng nhập, cấu hình kỳ thi, dashboard. Mục 3.1.2 và 3.1.3 của các màn hình đó.                                              |
| 16/10 | Mục 3.4: đặc tả function 1.1–1.4, 5.1–5.11, 9.1–9.5, 10.1–10.3, 11.1–11.3. Mục 5.1 và 5.3 của các feature này.                                                                                                      |


## Hoàng


| Ngày  | Output phải có lúc 21:00                                                                                                                                                                                                    |
| ----- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 14/10 | Mục 3.1.1: ba screen flow — luyện tập, thi thử, thi phòng lab.                                                                                                                                                              |
| 15/10 | Mục 3.1.2 và 3.1.3 của các màn hình đó. Mục 3.2: đặc tả function 2.1–2.14.                                                                                                                                                  |
| 16/10 | Mục 3.2 nốt: đặc tả function 3.1–3.15 và 6.1–6.14. Mục 4.2 Quality Attributes: Usability, Reliability, Performance. Mục 5.1 và 5.3 của Practice exam, Mock examination, Laboratory examination. Mục 5.4 Other Requirements. |




## Tốt


| Ngày  | Output phải có lúc 21:00                                                                                                                                                                                                       |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 14/10 | Mục 3.1.4 Non-Screen Functions: một bảng gồm chấm nền, hàng đợi và chạy lại bài lỗi, khóa điểm, lưu audio, timeout không tương tác, tự nộp khi hết giờ. Mục 3.1.1: hai screen flow — ngân hàng câu hỏi, hậu kiểm và phúc khảo. |
| 15/10 | Mục 3.1.2 và 3.1.3 của các màn hình đó. Mục 3.1.5: Logical ERD toàn đề tài và bảng mô tả entity.                                                                                                                               |
| 16/10 | Mục 3.3: đặc tả function 4.1–4.19 và 7.1–7.18. Mục 5.1 và 5.3 của Question management và Grade and appeal management.                                                                                                          |




## Bảng check mỗi ngày

Tốt điền lúc 21:00. Ô để trống cho đến giờ check.


| Ngày  | Tốt | Hải | Thành | Hoàng | Việc còn thiếu, xử lý trong ngày kế |
| ----- | --- | --- | ----- | ----- | ----------------------------------- |
| 20/10 |     |     |       |       |                                     |
| 21/10 |     |     |       |       |                                     |
| 22/10 |     |     |       |       |                                     |


Đạt nghĩa là file output của ngày đó có đủ các mục trong bảng giao việc, người khác mở ra đọc được. Chưa nghĩa là thiếu mục hoặc chưa có file. Người Chưa hoàn tất phần thiếu trước khi làm output ngày hôm sau.

## Xong sprint

21:00 ngày 16/10/2026, bốn file output đủ các mục sau:

- Thành: mục 1 phần chữ, 2.1, 2.2.2 đủ 1.1–11.3, 3.5, screen flow cấu hình hệ thống, 3.1.2 và 3.1.3 của màn đó, 5.1 và 5.3 của System configuration, 5.2.
- Hải: context diagram, 2.2.1, Conceptual ERD, 4.1, ba screen flow, 3.1.2 và 3.1.3 của các màn đó, 3.4, 5.1 và 5.3 của feature trong 3.4.
- Hoàng: ba screen flow, 3.1.2 và 3.1.3 của các màn đó, 3.2 đủ 2.1–2.14, 3.1–3.15 và 6.1–6.14, 4.2, 5.1 và 5.3 của ba feature đó, 5.4.
- Tốt: 3.1.4, hai screen flow, 3.1.2 và 3.1.3 của các màn đó, Logical ERD và bảng entity, 3.3 đủ 4.1–4.19 và 7.1–7.18, 5.1 và 5.3 của hai feature đó.

Record of Changes do Tốt ghi một dòng khi ghép vào Report 7, không giao trong ba ngày này.