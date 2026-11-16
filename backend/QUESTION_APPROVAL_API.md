# Câu hỏi tôi đã duyệt

## Danh sách

`GET /api/v1/questions/my-approved`

Yêu cầu JWT của `department_head` hoặc `admin` đang hoạt động. Backend lấy ID
người duyệt từ JWT, luôn lọc `ApprovedBy = người đăng nhập` và
`ApprovalStatus = APPROVED`. Truyền `approvedBy` hoặc `approvalStatus` trên URL
không thay đổi hai điều kiện này. API không thay đổi quyền và luồng duyệt hiện có.

Các bộ lọc tùy chọn: `courseId`, `usageScope` (`exam`, `official_exam`, `practice`
hoặc để trống để lấy cả hai kho), `difficulty`, `bloomLevel`, `source`, `search`,
`submittedBy`. Phân trang qua `pageNumber` (mặc định 1), `pageSize` (mặc định 20,
giới hạn 1–100).

Ví dụ:

```http
GET /api/v1/questions/my-approved?usageScope=exam&pageNumber=1&pageSize=20
Authorization: Bearer <access-token>
```

Response dùng cấu trúc danh sách hiện có:

```json
{
  "items": [
    {
      "id": "00000000-0000-0000-0000-000000000020",
      "title": "Câu hỏi ví dụ",
      "approvalStatus": "APPROVED",
      "approvedBy": "00000000-0000-0000-0000-000000000002",
      "approvedAt": "2026-11-16T07:30:00Z",
      "usageScope": "exam"
    }
  ],
  "totalCount": 1,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 1
}
```

Ví dụ trên lược bớt các trường câu hỏi cũ. `approvedAt` và `usageScope` được bổ sung
vào summary; `approvedAt` cũng có trong `GET /api/v1/questions/{id}`.

Danh sách sắp theo thời gian duyệt giảm dần, thời gian chưa biết nằm cuối; dùng ID
để ổn định thứ tự khi trùng thời gian. Mã lỗi: 401 nếu chưa đăng nhập/thiếu ID hợp lệ;
403 nếu sai role, tài khoản không tồn tại hoặc ngừng hoạt động; 400 nếu bộ lọc
phạm vi không hợp lệ. Danh sách rỗng vẫn trả 200.

## Dữ liệu và giới hạn

- `approved_at` nullable, kiểu PostgreSQL `timestamp with time zone`, lưu UTC.
- Duyệt từng câu, duyệt hàng loạt, tự duyệt hợp lệ khi tạo và duyệt kèm bộ đề đều
  ghi thời gian tại cùng chỗ ghi người duyệt. Không nhận thời gian duyệt từ client.
- Dữ liệu trước migration giữ `null`; không suy diễn ngày duyệt từ ngày tạo.
- Đây là trạng thái phê duyệt hiện tại, không phải lịch sử từng quyết định.
  Câu chuyển khỏi `APPROVED` hoặc đổi người duyệt sẽ không còn thuộc danh sách cũ.
- Migration `AddQuestionApprovalTime` thêm hai cột và index. Áp dụng bằng quy trình
  migration hiện có khi triển khai; chưa cần backfill. Rollback migration sẽ xóa
  hai cột thời gian mới, nên sẽ mất thời gian đã ghi sau khi nâng cấp.
- Không có thay đổi frontend trong phạm vi này.
