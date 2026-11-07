# Class Assessment Execution Plan

## 1. Mục tiêu

Xây dựng đầy đủ luồng nghiệp vụ:

1. Admin tạo học kỳ.
2. Hệ thống sao chép toàn bộ môn học đang hoạt động từ catalog vào học kỳ mới dưới dạng đã chọn.
3. Admin có thể bỏ chọn môn; thao tác này chỉ vô hiệu hóa môn trong học kỳ, không xóa catalog hoặc lịch sử.
4. Admin phân công một Trưởng bộ môn cho ngành Software Engineering (SE).
5. Trưởng bộ môn quản lý nhiều môn và phân công nhiều giảng viên vào từng môn trong từng học kỳ.
6. Giảng viên được phân công tự tạo lớp, cấu hình mật khẩu cố định và cho sinh viên tự tham gia.
7. Giảng viên tạo bài kiểm tra cho lớp theo cấu trúc/template tái sử dụng và chọn câu hỏi trong toàn bộ Practice Question Bank của môn.
8. Sinh viên làm bài trong khung thời gian được cấu hình; hệ thống giới hạn số lượt, tính duration và tự động nộp khi hết giờ.
9. Điểm chỉ hiển thị cho sinh viên sau khi giảng viên publish.

## 2. Quyết định nghiệp vụ đã chốt

- Hiện tại chỉ triển khai ngành `SE`.
- Một ngành có đúng một Trưởng bộ môn tại một thời điểm.
- Một Trưởng bộ môn quản lý nhiều course thuộc ngành SE.
- Course là catalog dùng lại qua nhiều học kỳ.
- Khi tạo học kỳ mới, toàn bộ course active được chọn mặc định; Admin bỏ tick để vô hiệu hóa course offering tương ứng.
- Bỏ tick không được xóa course hoặc dữ liệu lịch sử.
- Một course trong một học kỳ có thể được phân công cho nhiều lecturer.
- Mỗi lecturer được phân công có thể tự tạo một hoặc nhiều lớp.
- Sinh viên tham gia lớp bằng mật khẩu cố định do lecturer cấu hình.
- Cấu trúc bài kiểm tra được lưu thành template để tái sử dụng.
- Lecturer được chọn mọi câu hỏi hợp lệ của toàn bộ môn, không giới hạn theo người tạo câu hỏi.
- Lecturer cấu hình `MaxAttempts`, `StartAt`, `EndAt` và `DurationMinutes`.
- Hết duration phải tự động nộp bài.
- Sinh viên tham gia lớp sau khi bài kiểm tra đã mở vẫn được làm nếu thời điểm hiện tại còn nằm trong khoảng `StartAt - EndAt`.
- Điểm ở trạng thái ẩn cho đến khi lecturer publish.

### 2.1. Mục đích của tài liệu này

Đây là execution plan và handoff document cho agent coding tiếp theo. Agent phải dùng file này để biết:

1. Nghiệp vụ nào đã được người dùng chốt và không được tự ý đổi.
2. Phần nào đã có code, migration và automated test.
3. Phần nào mới chỉ là thiết kế đề xuất, chưa có code.
4. Phase hiện tại, quality gate còn thiếu và thứ tự triển khai tiếp theo.
5. Các trường legacy đang được giữ có chủ đích để tránh làm gãy Practice, Mock Exam và Official Exam.

### 2.2. Quy ước trạng thái

- `Hoàn thành code`: source, migration và contract thuộc phase đã được tạo.
- `Automated test pass`: test mục tiêu của phase đã chạy thành công trên checkout hiện tại.
- `Chưa test API thủ công`: chưa chạy end-to-end qua Swagger/Postman với JWT thật và PostgreSQL thật.
- `Chưa bắt đầu`: chưa được phép xem mô tả đề xuất là behavior hiện có.
- Một phase chỉ được xem là nghiệm thu hoàn toàn sau khi cả automated test và manual API checklist đều pass.

### 2.3. Ranh giới kiến trúc bắt buộc

- Backend tiếp tục theo .NET 8 Clean Architecture: `Domain -> Application -> Infrastructure -> API`.
- API Controller chỉ nhận HTTP/input/authenticated identity, gửi MediatR command/query và ánh xạ kết quả sang RFC 7807.
- Business rule, ownership và state transition phải nằm trong Application handler/validator, không đặt trong Controller.
- Persistence dùng EF Core 8 qua `IApplicationDbContext`; migration phải có đường backfill cho dữ liệu hiện hữu.
- Mọi endpoint mutation phải có validation, authorization/BOLA check, audit log khi nghiệp vụ cần truy vết và test failure case.
- Architecture test phải tiếp tục khóa dependency direction, unique index và delete behavior quan trọng.

### 2.3.1. Seed data là deliverable bắt buộc của mỗi task

Nếu một task cần dữ liệu hoặc tài khoản theo role để automated/manual test thì task đó **chưa hoàn thành** cho đến khi có seed database test/local tương ứng.

Quy tắc seed:

- Seed qua cơ chế seeder hiện có của project; không tạo SQL thủ công rời rạc nếu không có lý do migration cụ thể.
- Idempotent: chạy nhiều lần không tạo user, assignment, class, enrollment hoặc assessment trùng.
- Deterministic: dùng code/email/business key ổn định để agent/tester biết record nào cần dùng.
- Chỉ chạy trong Development/Test; không seed tài khoản test hoặc mật khẩu mặc định vào Production.
- Mật khẩu test lấy từ cấu hình development an toàn hoặc cơ chế seed password hiện có; không ghi secrets thật vào plan/source.
- Seed phải tạo đủ ownership graph, không chỉ tạo user rời: program -> course -> semester -> offering -> assignment -> class -> enrollment.
- Sau khi thêm entity/relationship mới, cập nhật seeder và kiểm tra chạy được trên database sạch lẫn database đã có seed cũ.
- Plan/manual checklist phải ghi rõ account role nào và business data nào được dùng.

Seed tối thiểu cho Phase 1–2:

| Nhóm | Dữ liệu cần có |
|---|---|
| Accounts | 1 Admin, 1 TBM SE, ít nhất 5 Lecturer, ít nhất 2 Student, thêm 1 Department Head không sở hữu SE để test BOLA |
| Catalog | Academic Program `SE`, các course như `CSD201`, `PRN231` |
| Semester | Ít nhất 1 semester active và 1 semester khác để test boundary |
| Offering | CSD201 offering enabled; thêm một offering disabled để test rejection |
| Assignment | 5 lecturer active ở CSD201; ít nhất 1 lecturer không được phân công và 1 assignment inactive |
| Class | Lớp có fixed password hash, join enabled; thêm lớp/offering disabled |
| Enrollment | Một student chưa join và một student đã join để test duplicate |

Từ Phase 3 trở đi, mỗi phase phải bổ sung seed riêng cho template, assessment draft/published, question bank, attempts, grading result và publish state trước khi chạy API checklist.

### 2.4. Luồng đích toàn hệ thống

```text
Admin tạo Semester
  -> hệ thống tạo/tái sử dụng Course Catalog của ngành SE
  -> tất cả Course active được tick mặc định thành CourseOffering
  -> Admin bỏ tick CourseOffering không dùng
  -> Admin gán một TBM cho ngành SE
  -> TBM phân công nhiều Lecturer vào từng CourseOffering
  -> Lecturer có assignment active tạo một hoặc nhiều Class
  -> Student join Class bằng mật khẩu cố định
  -> Lecturer tạo Assessment Structure Template tái sử dụng
  -> Lecturer tạo Assessment cho Class, chọn câu hỏi trong toàn bộ Practice Question Bank của Course
  -> Lecturer publish Assessment với StartAt, EndAt, DurationMinutes, MaxAttempts
  -> Student làm bài; hệ thống giới hạn attempt và auto-submit đúng deadline
  -> pipeline chấm bài xử lý kết quả nhưng chưa hiển thị điểm
  -> Lecturer review và publish điểm
  -> Student mới được xem kết quả
```

### 2.5. Nguyên tắc tương thích legacy

- `Course.SemesterId` và `Course.DepartmentHeadUserId` chưa được xóa.
- `CourseLecturer.CourseId/SemesterId` vẫn tồn tại cùng `CourseOfferingId` trong giai đoạn chuyển tiếp.
- `Class.CourseId/SemesterId` vẫn tồn tại cùng `CourseOfferingId`.
- Endpoint legacy chưa được xóa ở Phase 1–2.
- Agent chỉ cleanup các trường/endpoint này tại Phase 8 sau khi đã truy vết và chuyển toàn bộ consumer.

## 3. Tổng quan tiến độ

**Tổng số phase: 8**

**Phase hiện tại: Phase 2 — hoàn thành implementation và automated test; chưa test API thủ công.**

**Tiến độ implementation: 2/8 phase hoàn thành (25%).**

**Tiến độ kiểm thử API thủ công: Phase 1 và Phase 2 đều chưa thực hiện.**

| Phase | Nội dung | Trạng thái | Ghi chú |
|---|---|---|---|
| 1 | Course Catalog, ngành SE, Course Offering theo học kỳ, TBM cấp ngành | ✅ Hoàn thành code + automated test | ⚠️ Chưa test API thủ công |
| 2 | TBM phân công lecturer theo course offering; lecturer tạo lớp | ✅ Hoàn thành code + automated test | ⚠️ Chưa test API thủ công |
| 3 | Assessment Structure Template tái sử dụng | ⏳ Chưa bắt đầu | Chưa code |
| 4 | Lecturer tạo/publish bài kiểm tra và chọn Practice Question | ⏳ Chưa bắt đầu | Chưa code |
| 5 | Enrollment eligibility, attempts, start/end, duration, auto-submit | ⏳ Chưa bắt đầu | Chưa code |
| 6 | Chấm bài và tái sử dụng pipeline Practice | ⏳ Chưa bắt đầu | Chưa code |
| 7 | Publish điểm và kiểm soát quyền xem kết quả | ⏳ Chưa bắt đầu | Chưa code |
| 8 | End-to-end hardening, architecture tests, regression và cleanup legacy | ⏳ Chưa bắt đầu | Chưa code |

## 4. Phase 1 — Course Catalog và Course Offering

### 4.1. Mục tiêu Phase 1

- Tách khái niệm course catalog khỏi việc mở môn theo học kỳ.
- Tạo ngành SE và quản lý TBM ở cấp ngành.
- Khi Admin tạo semester, tự động bật toàn bộ course active.
- Cho phép Admin đồng bộ danh sách course được chọn trong semester.
- Giữ tương thích với các luồng Practice, Mock Exam, Official Exam và Class hiện có.

### 4.2. Trạng thái

**✅ Hoàn thành implementation.**

**✅ Hoàn thành migration và kiểm tra model.**

**✅ Hoàn thành unit test, automated controller/API test và architecture test thuộc phạm vi Phase 1.**

**⚠️ Chưa test API thủ công bằng Swagger/Postman với database thật.**

### 4.3. Domain và persistence đã hoàn thành

- Thêm entity `AcademicProgram`:
  - `Code`, `Name`.
  - `DepartmentHeadUserId`.
  - `IsActive`.
  - Quan hệ một ngành có nhiều course.
- Seed ngành:
  - Code: `SE`.
  - Name: `Software Engineering`.
  - TBM seed hiện tại: user `department_head` có sẵn.
- Thêm entity `CourseOffering`:
  - `CourseId`.
  - `SemesterId`.
  - `IsEnabled`.
  - Unique index `(CourseId, SemesterId)`.
  - Foreign key dùng `DeleteBehavior.Restrict` để tránh cascade xóa lịch sử.
- Thêm `AcademicProgramId` vào `Course`.
- Thêm navigation `Course.CourseOfferings` và `Semester.CourseOfferings`.
- Thêm `AcademicPrograms` và `CourseOfferings` vào `IApplicationDbContext` và `OralExamDbContext`.

### 4.4. Nghiệp vụ đã hoàn thành

#### Tạo học kỳ

Khi Admin tạo semester:

1. Hệ thống lấy toàn bộ course có `IsActive = true`.
2. Tạo một `CourseOffering` bật sẵn cho mỗi course trong semester mới.
3. API trả về `CoursesCount` tương ứng số course đã được chọn mặc định.

#### Admin bỏ/chọn lại môn

- Admin gửi toàn bộ `SelectedCourseIds` cho semester.
- Course đã chọn nhưng chưa có offering sẽ được tạo mới.
- Offering cũ được chọn lại sẽ chuyển `IsEnabled = true`.
- Offering bị bỏ chọn sẽ chuyển `IsEnabled = false`.
- Không gọi `Remove` và không xóa course/offering lịch sử.
- Course không tồn tại hoặc đã inactive không được phép chọn.
- Mỗi lần đồng bộ tạo một audit log `SEMESTER_COURSE_OFFERINGS_SYNCED`.

#### Phân công TBM ngành SE

- Admin có thể gán một user role `department_head` đang active làm TBM của ngành.
- `AcademicProgram.DepartmentHeadUserId` chỉ lưu một TBM hiện hành.
- Khi thay đổi TBM ngành, hệ thống đồng bộ `Course.DepartmentHeadUserId` cho các course thuộc ngành.
- Việc đồng bộ trường legacy giữ cho authorization của luồng phân công lecturer hiện tại không bị gãy trước Phase 2.
- Mỗi lần phân công tạo audit log `ACADEMIC_PROGRAM_DEPARTMENT_HEAD_ASSIGNED`.

#### Tạo course mới

- Course mới được liên kết với ngành SE nếu chương trình SE đang active.
- Course dùng TBM đã cấu hình ở cấp ngành.
- Nếu API cũ truyền TBM khác với TBM của ngành SE, command trả về validation failure.
- Course mới được tạo offering cho semester được chọn hoặc semester active mặc định.

### 4.5. API đã hoàn thành

#### Xem catalog và trạng thái tick của semester

```http
GET /api/v1/semesters/{semesterId}/course-offerings
GET /api/v1/academic/semesters/{semesterId}/course-offerings
```

Quyền: `admin`, `department_head`.

Kết quả chứa toàn bộ catalog cùng:

- `CourseOfferingId`.
- `IsSelected`.
- `AcademicProgramId`.
- `AcademicProgramCode`.
- Trạng thái active của course.

#### Đồng bộ danh sách course của semester

```http
PUT /api/v1/semesters/{semesterId}/course-offerings
PUT /api/v1/academic/semesters/{semesterId}/course-offerings
```

Quyền: `admin`.

Request:

```json
{
  "selectedCourseIds": [
    "course-id-csd201",
    "course-id-prn231"
  ]
}
```

Response báo cáo:

- `CreatedCount`.
- `EnabledCount`.
- `DisabledCount`.
- Danh sách catalog và trạng thái tick mới nhất.

#### Xem ngành và TBM

```http
GET /api/v1/academic-programs
```

#### Gán TBM cấp ngành

```http
PUT /api/v1/academic-programs/{academicProgramId}/department-head
```

Quyền: `admin`.

Request:

```json
{
  "departmentHeadUserId": "user-id"
}
```

### 4.6. Migration

Migration đã tạo:

```text
20261105064957_AddAcademicProgramAndCourseOfferings
```

Migration thực hiện:

- Tạo bảng `academic_programs`.
- Tạo bảng `course_offerings`.
- Thêm `courses.academic_program_id`.
- Seed ngành SE.
- Seed offering cho dữ liệu mẫu.
- Backfill toàn bộ course hiện hữu vào ngành SE.
- Backfill offering từ `Course.SemesterId` hiện tại.
- Dùng `ON CONFLICT (course_id, semester_id) DO NOTHING` để tránh tạo trùng.

Migration mới chỉ được scaffold và kiểm tra SQL, **chưa apply vào database của người dùng**.

### 4.7. Verification evidence

#### Build

```text
dotnet build OralExamination.sln --no-restore --disable-build-servers -m:1
Build succeeded.
0 Warning(s)
0 Error(s)
```

#### Test mục tiêu Phase 1

Bao gồm:

- Academic catalog business tests.
- API controller tests.
- Semester compatibility regression.
- Clean Architecture rules.
- CourseOffering unique index và restrict-delete rule.

Kết quả:

```text
Passed: 19
Failed: 0
Skipped: 0
```

#### EF migration model

```text
No changes have been made to the model since the last migration.
```

Idempotent migration SQL đã sinh thành công.

#### Full unit test suite

Kết quả lần chạy cuối trên checkout hiện tại:

```text
Total: 1302
Passed: 1300
Failed: 2
Skipped: 0
```

Hai failure còn lại không thuộc Phase 1:

1. `QuestionImageAndMultimodalGradingTests.OfficialExamKioskAndAudit_ShouldAutoAssignExamSet_AndDeliverQuestionImageMetadata`.
2. `ResilientSpeechServicesTests.TranscribeAndUploadAudio_WhenClientTranscriptIsNoiseHallucination_RejectsAndDoesNotResurrect`.

Các handler/service Official Exam và Speech liên quan đã có thay đổi local từ trước khi Phase 1 bắt đầu. Phase 1 không sửa hoặc hoàn nguyên các thay đổi đó.

#### Manual API checklist Phase 1 — chưa chạy

Chạy bằng PostgreSQL thật và JWT đúng role, ghi lại request/response/status code:

1. Admin tạo semester mới; xác nhận toàn bộ course catalog active xuất hiện và được tick mặc định.
2. Admin gọi GET course offerings của semester; kiểm tra `CourseOfferingId`, program và trạng thái selected.
3. Admin bỏ tick một course; xác nhận offering chuyển `IsEnabled = false` và record không bị xóa.
4. Admin tick lại course; xác nhận dùng lại record cũ.
5. Admin gán TBM SE hợp lệ; xác nhận program và các trường course legacy được đồng bộ.
6. Thử gán user sai role/inactive; xác nhận request bị từ chối.
7. Department Head đọc catalog được; Lecturer/Student không có quyền mutation.
8. Kiểm tra audit log của sync offering và assign department head.

### 4.8. Giới hạn chuyển tiếp còn giữ lại

Phase 1 chưa xóa các trường legacy:

- `Course.SemesterId`.
- `Course.DepartmentHeadUserId`.

Lý do: các module Practice, Mock Exam, Official Exam, Academic Hierarchy và Class hiện vẫn đọc các trường này. Việc chuyển hoàn toàn sang `CourseOfferingId` sẽ được thực hiện theo từng consumer ở Phase 2 và Phase 8 để tránh breaking change diện rộng.

## 5. Phase 2 — TBM phân công lecturer và lecturer tạo lớp

### 5.1. Trạng thái

**✅ Hoàn thành implementation, migration và kiểm thử mục tiêu.**

**⚠️ Chưa test API thủ công bằng Swagger/Postman với database thật.**

### 5.2. Phạm vi dự kiến

- Chuyển phân công lecturer từ course chung sang `CourseOffering` của semester.
- Chỉ TBM của ngành sở hữu course mới được phân công lecturer.
- Cho phép một course offering có nhiều lecturer.
- Chống phân công trùng lecturer trong cùng course offering.
- Disable assignment thay vì xóa lịch sử.
- Lecturer chỉ được tạo lớp từ course offering mà mình đang được phân công active.
- Lớp lưu rõ owner lecturer và course offering.
- Lecturer cấu hình tên lớp, mã lớp và mật khẩu cố định.
- Mật khẩu phải được hash, không lưu plaintext.
- Sinh viên join lớp bằng mật khẩu.
- Chặn join trùng và chặn join lớp/course offering inactive.
- Bổ sung API, validators, authorization, migration và tests tương ứng.

### 5.3. Acceptance dự kiến

- TBM có thể phân công 5 lecturer vào `CSD201` của một semester.
- Cả 5 lecturer đều thấy assignment của mình.
- Mỗi lecturer có thể tạo lớp riêng.
- Lecturer không được phân công không thể tạo lớp cho offering đó.
- Student nhập đúng mật khẩu có thể join lớp.
- Student nhập sai mật khẩu hoặc join trùng bị từ chối.

### 5.4. Domain và persistence đã hoàn thành

- Nâng cấp `CourseLecturer` để lưu `CourseOfferingId` và giữ các cột legacy trong giai đoạn chuyển tiếp.
- Unique index `uq_course_offering_lecturers` khóa một lecturer chỉ có một assignment trong cùng course offering.
- `Class` lưu `CourseOfferingId` bên cạnh `CourseId` và `SemesterId` legacy.
- Hai foreign key mới đều dùng `DeleteBehavior.Restrict` để bảo toàn lịch sử assignment và lớp.
- Assignment bị bỏ khỏi danh sách sync chuyển `IsActive = false`, không bị xóa.
- Mật khẩu lớp được hash bằng `IPasswordHasher`; không lưu plaintext.

### 5.5. Nghiệp vụ đã hoàn thành

- TBM của đúng `AcademicProgram` hoặc Admin có thể đồng bộ nhiều lecturer vào một course offering đang bật.
- Chỉ user role `lecturer` đang active mới được chọn.
- Lecturer bị bỏ chọn được disable; lecturer được chọn lại được kích hoạt lại.
- Lecturer xem được danh sách course offering đang được phân công active.
- Lecturer chỉ tạo được lớp khi có assignment active đúng course offering.
- Lớp mới lưu owner lecturer, course offering, mã lớp, join code và password hash.
- Student chỉ join được khi account, lớp và course offering đều active, self-enrollment/join đang bật và mật khẩu đúng.
- Join sai mật khẩu, join trùng và join vào offering bị tắt đều bị từ chối.
- Các thao tác sync, tạo lớp và join lớp tạo audit log tương ứng.
- Luồng API legacy vẫn được giữ để tránh breaking change cho consumer hiện có.

### 5.6. API đã hoàn thành

```http
PUT /api/v1/course-offerings/{courseOfferingId}/lecturers
GET /api/v1/course-offerings/{courseOfferingId}/lecturers
GET /api/v1/lecturer/course-offerings
POST /api/v1/lecturer/course-offerings/{courseOfferingId}/classes
```

- Endpoint sync lecturer: `admin`, `department_head`; handler tiếp tục kiểm tra ownership cấp program để chống BOLA.
- Endpoint xem assignment của lecturer: `lecturer`, `admin`.
- Endpoint tạo lớp theo offering: `lecturer`; identity lấy từ authenticated user, không tin lecturer id do client tự khai báo.
- Lỗi nghiệp vụ được ánh xạ sang RFC 7807/Problem Details theo nhóm `VALIDATION`, `NOT_FOUND`, `FORBIDDEN`, `CONFLICT` và `JOIN_DISABLED`.

### 5.7. Migration

Migration đã tạo:

```text
20261105073054_AddCourseOfferingLecturerAndClassOwnership
```

Migration thực hiện:

- Thêm `course_lecturers.course_offering_id` và `classes.course_offering_id` nullable để chuyển tiếp an toàn.
- Backfill assignment từ `(course_id, semester_id)` sang course offering tương ứng.
- Backfill lớp từ `(course_id, semester_id)` sang course offering tương ứng.
- Tạo unique/index và hai foreign key `Restrict`.
- EF xác nhận không còn pending model changes và sinh được idempotent migration script.
- Migration chưa được apply vào database của người dùng.

### 5.8. Verification evidence

#### Build solution

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

#### Phase 2 và architecture gate

```text
Passed: 34
Failed: 0
Skipped: 0
```

#### Academic Hierarchy/Catalog regression

```text
Passed: 47
Failed: 0
Skipped: 0
```

#### Full unit test suite

```text
Total: 1307
Passed: 1304
Failed: 3
Skipped: 0
```

Ba failure ngoài phạm vi Phase 2 và thuộc các file Official Exam/Speech đã có thay đổi local độc lập:

1. `QuestionImageAndMultimodalGradingTests.OfficialExamKioskAndAudit_ShouldAutoAssignExamSet_AndDeliverQuestionImageMetadata`.
2. `ResilientSpeechServicesTests.TranscribeSpeechCommandHandler_WhenCloudKeysMissingAndClientTranscriptProvided_ReturnsNormalizedHybridResult`.
3. `ResilientSpeechServicesTests.TranscribeAndUploadAudio_WhenClientTranscriptIsNoiseHallucination_RejectsAndDoesNotResurrect`.

#### Manual API checklist Phase 2 — chưa chạy

Chạy theo kịch bản CSD201 với 5 lecturer, PostgreSQL thật và JWT đúng role:

1. TBM SE sync 5 lecturer vào một CSD201 course offering; xác nhận cả 5 assignment active.
2. Bỏ một lecturer khỏi request; xác nhận assignment chuyển inactive nhưng record không bị xóa.
3. TBM ngành khác thử sync; phải nhận `403 Forbidden`.
4. Lecturer gọi API `GET /api/v1/lecturer/course-offerings`; chỉ thấy assignment của chính mình.
5. Lecturer được phân công tạo lớp với mật khẩu tối thiểu 8 ký tự; response trả `CourseOfferingId` đúng.
6. Kiểm tra database không lưu plaintext password.
7. Lecturer chưa được phân công thử tạo lớp; phải nhận `403 Forbidden`.
8. Student nhập đúng join code/password; enrollment được tạo.
9. Student nhập sai password, join lần hai hoặc join khi offering/class bị tắt; đều phải bị từ chối đúng status/problem code.
10. Kiểm tra audit log `COURSE_OFFERING_LECTURERS_SYNCED`, `CLASS_CREATED`, `CLASS_JOINED`.

## 6. Phase 3 — Assessment Structure Template

### Trạng thái

**⏳ Chưa bắt đầu.**

### Phạm vi dự kiến

- Lecturer tạo template cấu trúc bài kiểm tra theo course.
- Template có thể tái sử dụng cho nhiều lớp và nhiều assessment.
- Cấu hình số câu, CLO, Bloom level, điểm và thứ tự.
- Validate tổng số câu và phân bố hợp lệ.
- Chỉ owner hoặc lecturer có quyền phù hợp được sửa template.

### Thiết kế và API dự kiến — chưa code

- Tạo aggregate `AssessmentTemplate` thuộc `Course`, có owner lecturer, name, description, trạng thái active và audit timestamps.
- Child `AssessmentTemplateSection` lưu thứ tự, số câu, điểm, CLO/Bloom/filter.
- Template không thuộc class cụ thể để tái sử dụng cho nhiều class; không lưu question cụ thể.
- Template đã được sử dụng không hard-delete; dùng inactive/versioning để giữ reproducibility.

```http
POST   /api/v1/lecturer/courses/{courseId}/assessment-templates
GET    /api/v1/lecturer/courses/{courseId}/assessment-templates
GET    /api/v1/assessment-templates/{templateId}
PUT    /api/v1/assessment-templates/{templateId}
DELETE /api/v1/assessment-templates/{templateId}
```

Acceptance: assignment/ownership hợp lệ; CLO/Bloom đúng course; section/count/score hợp lệ; chặn sửa chéo course; có migration, validation, authorization, API và architecture tests.

## 7. Phase 4 — Lecturer tạo và publish bài kiểm tra

### Trạng thái

**⏳ Chưa bắt đầu.**

### Phạm vi dự kiến

- Lecturer tạo assessment draft cho lớp mình sở hữu.
- Chọn template hoặc tạo cấu trúc riêng.
- Chọn câu hỏi từ toàn bộ Practice Question Bank của course.
- Chỉ cho chọn câu hỏi active/approved và đúng course.
- Quản lý draft, publish và đóng assessment.
- Sau khi publish, thay đổi cấu trúc/câu hỏi phải tuân theo state-transition rule.

### Thiết kế và API dự kiến — chưa code

- `ClassAssessment` thuộc đúng một `Class` và owner lecturer của class.
- Snapshot template vào assessment để thay đổi template không làm đổi đề đã publish.
- Mapping assessment-question lưu thứ tự và dữ liệu cần tái hiện đề.
- Question phải active/approved và cùng course; không giới hạn theo người tạo question.
- State tối thiểu: `Draft -> Published -> Closed`; không sửa trực tiếp cấu trúc/câu hỏi sau publish.

```http
POST /api/v1/classes/{classId}/assessments
GET  /api/v1/classes/{classId}/assessments
GET  /api/v1/assessments/{assessmentId}
PUT  /api/v1/assessments/{assessmentId}
PUT  /api/v1/assessments/{assessmentId}/questions
POST /api/v1/assessments/{assessmentId}/publish
POST /api/v1/assessments/{assessmentId}/close
GET  /api/v1/courses/{courseId}/assessment-question-bank
```

Acceptance: chỉ owner được mutation; chặn question sai course/inactive/unapproved; không publish đề thiếu/sai cấu trúc; publish idempotent hoặc conflict rõ ràng; student không thấy draft.

## 8. Phase 5 — Attempts, scheduling và auto-submit

### Trạng thái

**⏳ Chưa bắt đầu.**

### Phạm vi dự kiến

- Cấu hình `MaxAttempts`, `StartAt`, `EndAt`, `DurationMinutes`.
- Chỉ thành viên lớp được bắt đầu attempt.
- Sinh viên join lớp sau khi assessment mở vẫn được làm nếu còn trong cửa sổ thời gian.
- Mỗi attempt có deadline riêng: giá trị nhỏ hơn giữa `StartedAt + Duration` và `EndAt`.
- Tự động submit khi deadline đến.
- Chống submit trùng và chống vượt quá số attempt.

### Thiết kế và API dự kiến — chưa code

- `ClassAssessment` lưu `StartAt`, `EndAt`, `DurationMinutes`, `MaxAttempts`; thời gian chuẩn hóa UTC.
- `AssessmentAttempt` lưu student, attempt number, `StartedAt`, `DeadlineAt`, `SubmittedAt`, status và submission reason.
- `DeadlineAt = min(StartedAt + DurationMinutes, EndAt)`.
- Student join class muộn vẫn được start nếu còn trong cửa sổ và còn lượt.
- Auto-submit chạy server-side bằng durable/background job; timer browser chỉ hỗ trợ UX.
- Start/submit có concurrency guard và idempotency.

```http
GET  /api/v1/student/classes/{classId}/assessments
POST /api/v1/assessments/{assessmentId}/attempts
GET  /api/v1/assessment-attempts/{attemptId}
PUT  /api/v1/assessment-attempts/{attemptId}/answers
POST /api/v1/assessment-attempts/{attemptId}/submit
```

Acceptance: chặn ngoài lớp/draft/closed/sai giờ/hết lượt; không trùng attempt; submit tay và auto-submit chỉ tạo một kết quả; chặn sửa sau deadline; test boundary StartAt/EndAt/DeadlineAt.

## 9. Phase 6 — Chấm bài dùng chung pipeline Practice

### Trạng thái

**⏳ Chưa bắt đầu.**

### Phạm vi dự kiến

- Tái sử dụng phần phù hợp của Practice grading pipeline.
- Không nhân bản logic STT/AI evaluation nếu có thể trừu tượng hóa an toàn.
- Lưu grading job bền vững.
- Hỗ trợ retry/idempotency.
- Tách kết quả từng attempt khỏi Practice Session.

### Thiết kế và acceptance dự kiến — chưa code

- Tách abstraction dùng chung cho audio/STT/AI evaluation, không copy pipeline Practice.
- Assessment attempt có grading job/result riêng; persist submission trước khi enqueue.
- Worker retry/DLQ và mọi callback phải idempotent theo attempt/question/job.
- Lưu raw score, normalized score, feedback và review status nhưng chưa expose cho student.
- Test happy path, transient/permanent failure, retry, duplicate delivery và DLQ recovery.

## 10. Phase 7 — Publish điểm

### Trạng thái

**⏳ Chưa bắt đầu.**

### Phạm vi dự kiến

- Kết quả chấm được lưu nhưng ẩn với sinh viên.
- Lecturer xem và rà soát điểm trước publish.
- Chỉ owner/lecturer được ủy quyền mới có quyền publish.
- Student chỉ xem được kết quả sau publish.
- Ghi audit thời điểm và người publish.
- Xác định rule cho republish hoặc khóa điểm sau publish.

### Thiết kế và API dự kiến — chưa code

- Result có trạng thái `PendingGrading`, `ReadyForReview`, `Published`.
- Override điểm phải có reason/audit; publish lưu actor/time/version snapshot.
- Student result query bắt buộc kiểm tra publish ở backend, không chỉ ẩn frontend.
- Trước khi code phải chốt unpublish/republish; mặc định an toàn là correction bằng version/audit mới.

```http
GET  /api/v1/assessments/{assessmentId}/results
GET  /api/v1/assessment-attempts/{attemptId}/grading-result
POST /api/v1/assessments/{assessmentId}/results/publish
GET  /api/v1/student/assessment-results
GET  /api/v1/student/assessment-results/{resultId}
```

Acceptance: không lộ điểm trước publish; chặn lecturer/student BOLA; publish lặp không nhân bản; student chỉ thấy result của chính mình sau publish.

## 11. Phase 8 — Hardening và cleanup

### Trạng thái

**⏳ Chưa bắt đầu.**

### Phạm vi dự kiến

- End-to-end test toàn luồng Admin → TBM → Lecturer → Student.
- Authorization/BOLA tests.
- Concurrency tests cho join lớp, start attempt và auto-submit.
- Architecture tests cho toàn bộ feature mới.
- Index/query review.
- OpenAPI contract review.
- Chuyển các consumer còn lại khỏi `Course.SemesterId` và course-level department head legacy.
- Chỉ xóa legacy fields sau khi mọi consumer và migration path đã được xác minh.

### Công việc bắt buộc và Definition of Done

1. Chạy E2E Admin -> TBM -> Lecturer -> Student bằng role/JWT thật.
2. Hoàn thành manual API checklist Phase 1–7 trên PostgreSQL thật.
3. Bổ sung authorization matrix, BOLA và concurrency tests cho mọi mutation quan trọng.
4. Review index/query plan và OpenAPI/Problem Details contract.
5. Truy vết toàn bộ consumer legacy trước khi tạo migration cleanup.
6. Test migration từ database sạch và database có dữ liệu cũ.
7. Chỉ xóa endpoint/column legacy khi có evidence không còn consumer.
8. Full build/test không có regression do feature mới; ghi command, output và residual risk vào file này.
9. Mọi role/data cần cho test có seed idempotent trong Development/Test và có hướng dẫn nhận diện account/record.

## 12. Hướng dẫn agent coding tiếp theo

### 12.1. Trước khi sửa code

1. Đọc `AGENTS.md`, file plan này và working tree; không hoàn nguyên thay đổi ngoài feature.
2. Xác minh checkout hiện tại vì số test và source có thể thay đổi sau lần cập nhật plan.
3. Đọc Domain/Application/Infrastructure/API call site liên quan trước khi thiết kế schema.
4. Phân biệt rõ behavior đã verify với thiết kế đề xuất trong Phase 3–8.
5. Không apply migration vào database của người dùng nếu chưa được yêu cầu.

### 12.2. Phase tiếp theo phải làm

Phase implementation tiếp theo là **Phase 3 — Assessment Structure Template**. Thứ tự:

1. Chốt aggregate, ownership và versioning/inactive rule.
2. Chốt section schema cho số câu, CLO, Bloom, điểm và thứ tự dựa trên model thực tế của Course/Question Bank.
3. Viết acceptance/failure cases trước khi code.
4. Implement Domain -> Application -> Infrastructure -> API.
5. Tạo migration/backfill nếu cần.
6. Viết unit, controller, authorization, persistence và architecture tests.
7. Build, chạy targeted regression, full suite và cập nhật evidence vào plan.
8. Bổ sung hoặc cập nhật seed data/tài khoản cần thiết để agent khác có thể test ngay trên database local.

### 12.3. Việc còn thiếu trước khi gọi Phase 1–2 là nghiệm thu hoàn toàn

- Chạy hai manual API checklist đã ghi trong mục 4.7 và 5.8.
- Apply migration trên database test/local, không phải production.
- Xác nhận JWT role/claims, HTTP status, Problem Details, audit log và dữ liệu PostgreSQL thật.
- Nếu manual test phát hiện lỗi Phase 1–2, ưu tiên sửa và cập nhật plan trước khi mở rộng Phase 3.

## 13. Checkpoint tiếp theo

Checkpoint hiện tại:

```text
Phase 1: IMPLEMENTATION COMPLETE — AUTOMATED TEST PASS — MANUAL API NOT TESTED
Phase 2: IMPLEMENTATION COMPLETE — AUTOMATED TEST PASS — MANUAL API NOT TESTED
Phase 3: NOT STARTED
```

Sau khi hoàn thành manual API smoke test Phase 1–2 hoặc khi người dùng chủ động cho phép tiếp tục song song, công việc tiếp theo là thiết kế và triển khai Phase 3 theo thứ tự:

1. Chốt aggregate và ownership cho Assessment Structure Template.
2. Thiết kế cấu trúc section/CLO/Bloom/số câu/điểm có thể tái sử dụng.
3. Xác định validation invariant và quyền sửa/xóa template.
4. Viết migration, API, unit tests và architecture tests.
