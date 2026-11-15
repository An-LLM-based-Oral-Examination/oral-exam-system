# API cập nhật và xóa ca thi

## Endpoint và quyền

| Loại ca | Cập nhật | Xóa | Quyền |
|---|---|---|---|
| Chính thức | `PUT /api/v1/official-exams/shifts/{id}` | `DELETE /api/v1/official-exams/shifts/{id}` | Khảo thí (`examination_officer`) đang hoạt động |
| Trên lớp | `PUT /api/v1/in-class-shifts/{id}` | `DELETE /api/v1/in-class-shifts/{id}` | Giảng viên được phân công, trưởng bộ môn sở hữu môn, hoặc admin đang hoạt động |

Ca trên lớp có alias `/api/v1/lecturer/in-class-shifts/{id}`.
Thành công trả `204 No Content`. DELETE không nhận body. Ca không tồn tại trả `404`;
chưa đăng nhập trả `401`; không đủ quyền trả `403`; dữ liệu cập nhật không hợp lệ trả `400`.

## Chính sách

Chỉ xóa cứng ca trống còn `SCHEDULED`, chưa đến giờ, chưa kích hoạt/công bố đề,
chưa khóa/chốt điểm và chưa có dữ liệu thi. Không xóa dây chuyền vé, bài làm, điểm hay bằng chứng.
Không thay đổi kỳ thi, lớp hoặc người tạo ca qua Update.

Update ca chính thức cho phép vé còn chưa sử dụng; giữ nguyên các vé và đề đã phân.
Chặn vé đã sử dụng, kể cả khi điểm danh được hoàn tác về `SCHEDULED`.
Update ca trên lớp chỉ áp dụng khi chưa phát sinh tham gia, bài làm hoặc mật khẩu.
Giờ bắt đầu mới phải ở tương lai và giờ kết thúc sau giờ bắt đầu.
Thời gian được lưu UTC; nên gửi ISO 8601 có offset hoặc hậu tố `Z`.
Giá trị không có timezone được hiểu là UTC, thống nhất luồng tạo ca trên lớp hiện có.

## Các trường hợp HTTP 409

API trả nguyên nhân đầu tiên theo thứ tự kiểm tra. Sửa nguyên nhân này rồi gửi lại có thể
phát hiện thêm nguyên nhân khác. Mỗi lỗi có `code` ổn định và thông báo cụ thể trong `title`/`detail`.

| `code` | Điều kiện | Phạm vi |
|---|---|---|
| `SHIFT_EXAM_PUBLISHED` | Đã công bố đề hoặc đã có thời điểm công bố | Chính thức, Update/Delete |
| `SHIFT_NOT_SCHEDULED` | Trạng thái khác chờ thi: đang thi, đóng, hoàn tất, hủy… | Cả hai, Update/Delete |
| `SHIFT_ALREADY_STARTED` | Đã đến hoặc qua giờ bắt đầu, dù trạng thái vẫn chờ thi | Cả hai, Update/Delete |
| `SHIFT_LOCKED` | Ca đã khóa dữ liệu | Trên lớp, Update/Delete |
| `SHIFT_GRADES_FINALIZED` | Đã có thời điểm chốt điểm hoặc trạng thái FINALIZED | Trên lớp, Update/Delete |
| `SHIFT_HAS_TICKETS` | Có ít nhất một vé thi, kể cả chưa dùng | Chính thức, Delete |
| `SHIFT_HAS_USED_TICKETS` | Vé khác SCHEDULED; đã nộp bài, có audio, audit chấm điểm, khóa/công bố điểm hoặc lịch sử điểm danh có mặt | Chính thức, Update |
| `SHIFT_HAS_SUBMISSIONS` | Có bài làm | Cả hai, Update/Delete |
| `SHIFT_HAS_ANTI_CHEAT_LOGS` | Có log giám sát/vi phạm liên quan ca hoặc vé trong ca | Chính thức, Update/Delete |
| `SHIFT_HAS_PARTICIPANTS` | Có người tham gia | Trên lớp, Update/Delete |
| `SHIFT_HAS_PASSWORDS` | Đã phát sinh mật khẩu vào thi | Trên lớp, Update/Delete |

Ví dụ khi xóa ca có vé:

```json
{
  "type": "https://oralexam.fpt.edu.vn/errors/shift-has-tickets",
  "title": "Ca thi đã có vé thi được phân cho sinh viên. Không thể xóa; cần giữ ca và danh sách dự thi.",
  "status": 409,
  "detail": "Ca thi đã có vé thi được phân cho sinh viên. Không thể xóa; cần giữ ca và danh sách dự thi.",
  "code": "SHIFT_HAS_TICKETS"
}
```

Các thông báo đầy đủ được định nghĩa tại `src/Application/Common/Models/ShiftMutationErrors.cs`.

## Payload PUT

PUT cập nhật toàn bộ cấu hình có thể sửa; trường tùy chọn bị bỏ qua sẽ nhận giá trị mặc định,
không có ngữ nghĩa PATCH.

Ca chính thức:

```json
{
  "shiftName": "Ca sáng",
  "roomLab": "AL-L201",
  "startTime": "2027-01-10T01:00:00Z",
  "endTime": "2027-01-10T03:00:00Z",
  "proctorUserId": "<UUID giám thị đang hoạt động>",
  "examinerUserId": "<UUID giảng viên chấm thi đang hoạt động>"
}
```

Giám thị phải là proctor/lecturer; người chấm là lecturer/department_head và hai người phải khác nhau.
Tên ca tối đa 100 ký tự, phòng tối đa 50 ký tự.

Ca trên lớp:

```json
{
  "shiftName": "Kiểm tra chương 1",
  "startTime": "2027-01-10T01:00:00Z",
  "endTime": "2027-01-10T02:00:00Z",
  "description": "Nội dung chương 1",
  "questionPoolMode": "ALL_COURSE_BANK",
  "selectedQuestionIds": [],
  "totalQuestionsPerStudent": 2,
  "hasFollowUp": false,
  "maxFollowUpQuestions": 0,
  "isAutoPublishGrades": true,
  "examDurationMinutes": 20
}
```

Tên ca tối đa 255 ký tự; số câu 1–10; thời lượng 1–60 phút và phải nằm trong khung ca;
số câu phụ 0–5. Nguồn câu hỏi phải đủ câu, active, approved và đúng môn.
`SELECTED_POOL` yêu cầu danh sách ID hợp lệ; `ALL_COURSE_BANK` dùng ngân hàng được duyệt của môn.

## Bảo vệ đồng thời và kiểm chứng

PostgreSQL: giao dịch ReadCommitted khóa hàng ca trước khi đọc điều kiện, đồng thời khóa vé
hiện hữu đối với ca chính thức. Khóa cha ngăn insert con vượt qua kiểm tra ca trống;
khóa vé ngăn điểm danh/nộp bài chen vào kiểm tra vé chưa dùng.
Các writer công bố đề, kích hoạt, phân vé, import danh sách và tham gia dùng cùng khóa hàng ca.
Không cần migration vì không thay schema.

Test liên quan: `ShiftMutationTests`, `InClassShiftMutationTests`,
`OfficialShiftMutationControllerTests`, `InClassShiftsControllerTests`,
`OfficialShiftMutationPostgresTests`, `InClassShiftMutationPostgresTests`.
PostgreSQL tests dùng database/container tạm, xác nhận chờ khóa thực và dữ liệu còn nguyên.

Kết quả kiểm chứng ngày 2026-11-15:

```powershell
dotnet test backend/tests/OralExamination.UnitTests/OralExamination.UnitTests.csproj --no-restore --filter 'Category!=Integration' --verbosity minimal
# PASS: 2078/2078, gồm HTTP/JWT kiểm tra ma trận quyền và thông báo conflict.

dotnet test backend/tests/OralExamination.IntegrationTests/OralExamination.IntegrationTests.csproj --no-restore --filter 'FullyQualifiedName~ShiftMutationPostgresTests|FullyQualifiedName~InClassConcurrencyPostgresTests|FullyQualifiedName~BulkExamTicketImportPostgresTests' --verbosity minimal
# PASS: 17/17 trên PostgreSQL container riêng.
```

Lần chạy unit suite không filter trước đó: 2049 PASS, 4 FAIL vì nhóm cũ
`InClassShiftConcurrencyIntegrationTests` không kết nối được PostgreSQL tại localhost:5432.
Nhóm này không nằm trong kết quả 2078/2078 ở trên. Không tuyên bố toàn bộ suite xanh.
Chưa deploy hoặc thực hiện thao tác xóa trên dữ liệu vận hành.
