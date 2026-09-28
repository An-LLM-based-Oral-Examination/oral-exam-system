# Ghi chú cho agent: chỗ API Contract và BE Task Guide chưa khớp

**Người ghi:** Nguyễn Trọng Tốt  
**Ngày:** 28/09/2026  
**Đọc hai file:**

- `docs/API_CONTRACT_AND_INTEGRATION_GUIDE.md`
- `backend/BE_TASK_EXECUTION_GUIDE.md`

**Mục đích:** Các thành viên đưa file này cho agent của mình trước khi code. Agent phải đối chiếu đúng các điểm dưới đây, không tự chọn một trong hai tài liệu khi chúng mâu thuẫn.

Bản luồng đã chốt để dựng MVP là `REVIEW 1/MainFlow.drawio.xml`. Khi contract hoặc task guide khác bản đó, ưu tiên main flow đã chốt và hỏi lại người giao việc trước khi viết code.

---

## Quy tắc đã chốt, agent không được suy ra khác

1. **Practice (MF-01).** Hai chế độ: per-question chấm ngay, full-session gom rồi chấm một lần. Follow-up chỉ ở per-question, đúng một lần cho mỗi câu gốc. Điểm từ 4.0 đến 8.0 thì hỏi. Ngoài khoảng đó thì hiện điểm và feedback. Bỏ qua thì giữ điểm cũ. Trả lời follow-up thì AI chấm lại, điểm mới thay điểm cũ, có thể tăng hoặc giảm. Sau khi chấm phải phân biệt câu vừa nộp là câu gốc hay câu follow-up. Câu follow-up thì dừng, hiện điểm và feedback, không hỏi lần nữa. Bloom / batch summary thuộc full-session.
2. **Thi thử (MF-02) giống thi lab (MF-04) ở luật follow-up.** Có nhánh môn không follow-up. Môn có follow-up thì luôn sinh đúng một câu từ ngữ cảnh của câu hỏi gốc và câu trả lời. Không xét câu trả lời tốt hay chưa để quyết định có hỏi. Không hỏi lần hai.
3. **Khác nhau giữa thi thử và thi lab:** thi thử chỉ hiện điểm, không hiện feedback. Thi lab khóa máy, đóng dấu audio, chấm sau khi đóng ca, giảng viên duyệt hoặc sửa rồi khóa, sinh viên xem điểm sau khi phát hành.

---

## 1. Hai file lệch nhau, agent dễ chọn bừa

### URL thi thử

- Contract: `POST /api/v1/mock-exam/start`, `POST /api/v1/mock-exam/submit` (số ít).
- Mục danh mục trong task guide: `POST /api/v1/mock-exams/start`, `POST /api/v1/mock-exams/submit`, `GET /api/v1/mock-exams/{id}/analytics` (số nhiều).
- Demo Tuần 3 trong task guide quay lại số ít: `POST /api/v1/mock-exam/start`.

Chưa có một đường dẫn duy nhất. Hỏi lại trước khi đặt tên controller và route.

### Đăng nhập Google

- Contract: `POST /api/v1/auth/google-login` nhận `idToken` trong body, và client gửi kèm `"role": "Student"`.
- Task guide: OAuth 2.0 PKCE, email `@fpt.edu.vn`.

Đây là hai luồng khác nhau. Role không được lấy từ body do client tự chọn. Hỏi lại luồng nào là chuẩn, và role lấy từ đâu.

### Refresh token

- Contract cho refresh token trong body hoặc HttpOnly cookie, và response trả `refreshToken` mới trong JSON.
- Task guide chỉ nói HttpOnly cookie.
- Access token: contract cấm LocalStorage, cho Memory hoặc Session Storage.

Cần một quy ước lưu trữ duy nhất cho access token và refresh token.

### `courseId` và `subjectId`

`POST /api/v1/exam-structures` dùng `courseId`. Các API còn lại dùng `subjectId`. Domain trong task guide có entity `Course`. Agent dễ tạo cả `Course` và `Subject` cho cùng một môn. Cần một tên và một khóa ngoại.

### Số bảng

Task guide viết “12 bảng”, nhưng danh sách entity dài hơn (User, Course, Class, ClassEnrollment, Question, Rubric, ExamStructure, ExamSet, Practice, bài thi lab, AuditLog, DeadLetterQueue). Agent sẽ tự gộp hoặc bỏ bảng. Cần danh sách bảng đúng với migration.

---

## 2. Lệch với main flow đã chốt

### Follow-up bị gán nhầm luồng

Task BE-3.4 giao: điểm 4.0–8.0 thì sinh câu đào sâu trong **thi thử**.

Quy tắc đã chốt: khoảng 4.0–8.0 chỉ dùng cho **Practice**, đúng một lần, và phải biết câu vừa chấm là câu gốc hay câu follow-up. Thi thử và thi lab hỏi theo môn, không theo điểm.

Contract hiện chưa đủ để làm đúng luật này:

- `GET/POST /api/v1/subjects` không có trường môn có follow-up hay không.
- `POST /api/v1/practice/submit` có `deepDiveQuestion` trong payload SignalR, nhưng không nói khi nào khác `null`, và không có API nộp câu trả lời follow-up.
- `POST /api/v1/mock-exam/submit` chỉ có `transcript` của câu gốc, không có câu follow-up.

### Kết quả thi thử

Contract trả `feedback` và `bloomAnalysis` khi nộp thi thử. Task guide giao Radar 6 mức Bloom cho mock (`GET .../analytics`, demo Tuần 3).

Bản chốt: thi thử chỉ hiện điểm, không feedback. Bloom và radar thuộc Practice full-session.

### Mục 5.4 bị trùng số

Trong contract, mục 5.4 vừa là Practice vừa là Mock. Khi agent được bảo “làm mục 5.4”, nó có thể làm nhầm luồng.

### Buffer 30 giây và hết giờ

- Dòng đầu contract bắt buffer sửa transcript 30 giây. Task guide không giao việc này. Swimlane chỉ có bước mở khóa ô sửa, không ghi số giây.
- Hết giờ trong contract: `SubmitTime <= EndTime + 10s`, trễ hơn thì từ chối chấm. Demo task guide nói từ chối tính điểm câu đó. Swimlane là lưu phần đang làm rồi nộp cả bài.

Ba câu này cần một quy tắc trước khi code timer.

### Quota lệch một lượt

Dòng đầu task guide viết “≥ 3 lượt thì 429”. Demo Tuần 3 cho lần 1, 2, 3 thành công, lần 4 mới 429.

Cách đếm cần ghi rõ: đếm trước khi tạo lượt mới; đã đủ 3 thì chặn lượt thứ 4.

---

## 3. Contract thiếu, agent sẽ tự bịa

- Chưa có `GET /api/v1/questions` trong khi luồng ngân hàng câu hỏi có lọc và sửa. Contract chỉ có `POST` tạo mới.
- `POST /api/v1/exam-sessions/{id}/presigned-url` chỉ xuất hiện trong task guide, không có request/response trong contract.
- Chưa có API sinh viên xem điểm sau khi giảng viên phát hành.
- Chữ “seal” đang là hai việc. `POST /api/v1/exam/seal` là đình chỉ vì vi phạm kiosk (Alt+Tab). Task guide dùng seal cho mã SHA-256 của file audio.
- Whisper trong contract là từng đoạn `start` / `end` / `text`. Task guide yêu cầu word-level timestamp.
- SignalR hub `/hubs/practice`, event `ReceiveScorecard`, chưa nói cách gửi đúng một sinh viên.
- ID mẫu lẫn ba kiểu: `usr-stu-001`, `SV202601`, `SE170001`. Import Excel trả `studentCode`, ca thi dùng `studentId`.
- `ScopeType` và `BloomLevel` chưa có danh sách giá trị. Contract chỉ có ví dụ `"SHARED"` và `"Analyze"`.
- Mục 1 bắt mọi API dùng `application/json`. Import roster là `multipart/form-data`, export điểm là file Excel. Hai ngoại lệ này cần được ghi trong quy ước chung.
- Lỗi validation dùng key `"Criteria"` (PascalCase). JSON bắt buộc camelCase, nên key phải là `criteria`. Nếu không, Frontend không highlight đúng ô.
- Khung 6 test case được giao cho mọi endpoint. TC-05 (quota 429) và TC-06 (khóa điểm 403) chỉ đúng với thi thử và bài thi lab đã khóa, không áp cho đăng nhập hay tạo câu hỏi.

---

## Câu agent cần hỏi lại trước khi code

Copy khối dưới cho agent của mình:

```text
Đọc docs/Report/Review_API_Contract_va_BE_Task_Guide.md trước khi sửa API hoặc handler.
Khi API Contract và BE Task Guide mâu thuẫn, dừng và hỏi, không tự chọn.
Ưu tiên main flow đã chốt trong REVIEW 1/MainFlow.drawio.xml cho luật follow-up, điểm thi thử, và Bloom.

Xác nhận giúp các điểm sau, chưa xác nhận thì chưa implement:

1. URL thi thử là /api/v1/mock-exam hay /api/v1/mock-exams?
2. Đăng nhập Google là xác thực idToken hay OAuth PKCE? Role lấy từ đâu, không lấy từ body?
3. Refresh token lưu ở đâu: chỉ HttpOnly cookie, hay cả JSON body?
4. courseId và subjectId có phải cùng một khóa không?
5. Danh sách bảng PostgreSQL cuối cùng là gì? Có tạo bảng Chapter không?
6. Follow-up 4.0–8.0 chỉ thuộc Practice per-question, đúng một lần. Thi thử và thi lab hỏi theo môn. Field nào trên Subject đánh dấu môn có follow-up?
7. Thi thử chỉ trả điểm, không trả feedback, không trả radar Bloom. Bloom thuộc Practice full-session. Có đúng không?
8. Quota: lần 1–3 được thi, lần 4 trả 429. Đếm trước khi tạo lượt mới.
9. Hết giờ thi thử: lưu phần đang làm rồi nộp cả bài, hay từ chối nếu trễ quá 10 giây?
10. Buffer sửa transcript có đúng 30 giây không, và áp cho luồng nào?
11. Presigned URL, xem điểm sau khi phát hành, và lưu câu follow-up của thi thử: request/response chuẩn là gì?
12. Seal kiosk và SHA-256 audio là hai API khác nhau. Tên endpoint từng cái là gì?
```
