# 3.9 ĐẶC TẢ THIẾT KẾ CHI TIẾT DD-09 — CẤU HÌNH ĐỀ THI THỬ & KIỂM SOÁT HẠN NGẠCH THEO NGÀY (MF-02)

> **Tài liệu:** Report 4 — Detailed Design Specification (`DD-09`)  
> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (`FA26SE166`)  
> **Tác giả (Author):** Phạm Nguyễn Đăng Hải (DB Specialist & Frontend Developer)  
> **Người kiểm tra chéo (Cross-Reviewer):** Lê Vũ Hoàng (Lead Frontend Architect)  
> **Mã nguồn đối chiếu:** [CoursesController.cs](file:///d:/Đồ%20Án/05_Source_Code/backend/src/API/Controllers/v1/CoursesController.cs), [MockExamsController.cs](file:///d:/Đồ%20Án/05_Source_Code/backend/src/API/Controllers/v1/MockExamsController.cs)

---

## 3.9.1 Class Diagram (`DD09_MockConfigQuota_Class_Diagram`)

- **Tệp nguồn Draw.io:** [DD09_MockConfigQuota_Class_Diagram.drawio](file:///d:/Đồ%20Án/05_Source_Code/docs/Task%20Daily/Report4_PhamNguyenDangHai/04_DD09_MockExam_Config_Daily_Quota_Guard/DD09_MockConfigQuota_Class_Diagram.drawio)
- **Tệp ảnh PNG:** [DD09_MockConfigQuota_Class_Diagram.png](file:///d:/Đồ%20Án/05_Source_Code/docs/Task%20Daily/Report4_PhamNguyenDangHai/04_DD09_MockExam_Config_Daily_Quota_Guard/DD09_MockConfigQuota_Class_Diagram.png)

```mermaid
classDiagram
    class CoursesController {
        <<controller>>
        -ISender _sender
        +UpdateCourseConfiguration(Guid id, UpdateCourseConfigurationRequest req, CancellationToken ct) Task~IActionResult~
        +UpdateCourse(Guid id, UpdateCourseRequest req, CancellationToken ct) Task~IActionResult~
    }
    class MockExamsController {
        <<controller>>
        -ISender _sender
        -ICurrentUserService _currentUser
        +GetQuota(Guid courseId, CancellationToken ct) Task~IActionResult~
        +StartSession(StartMockExamRequest request, CancellationToken ct) Task~IActionResult~
    }
    class UpdateCourseConfigurationCommandHandler {
        <<handler>>
        -IApplicationDbContext _context
        -ICurrentUserService _currentUser
        +Handle(UpdateCourseConfigurationCommand cmd, CancellationToken ct) Task~CourseDto~
        -UpsertMockExamStructureAsync(Course course, UpdateCourseConfigurationCommand cmd, CancellationToken ct) Task
    }
    class GetMockExamQuotaQueryHandler {
        <<handler>>
        -IApplicationDbContext _context
        -IQuotaService _quotaService
        +Handle(GetMockExamQuotaQuery query, CancellationToken ct) Task~MockExamQuotaStatusDto~
    }
    class IQuotaService {
        <<interface>>
        +CheckQuotaAsync(Guid studentId, Guid courseId, CancellationToken ct) Task~QuotaResult~
        +ConsumeQuotaAsync(Guid studentId, Guid courseId, CancellationToken ct) Task
    }
    class QuotaService {
        <<service>>
        -IApplicationDbContext _context
        +CheckQuotaAsync(Guid studentId, Guid courseId, CancellationToken ct) Task~QuotaResult~
        +ConsumeQuotaAsync(Guid studentId, Guid courseId, CancellationToken ct) Task
        -GetUtcDayBounds() ValueTuple~DateTimeOffset, DateTimeOffset~
    }
    class IApplicationDbContext {
        <<interface>>
        +DbSet~Course~ Courses
        +DbSet~ExamStructure~ ExamStructures
        +DbSet~MockExamSession~ MockExamSessions
        +SaveChangesAsync(CancellationToken ct) Task~int~
    }
    class OralExamDbContext {
        <<persistence>>
        +SaveChangesAsync(CancellationToken ct) Task~int~
    }
    class Course {
        <<entity>>
        +Guid Id
        +string Code
        +string Name
        +Guid? DepartmentHeadUserId
        +int MaxMockExamsPerDay
        +bool DefaultHasFollowUp
        +int DefaultMaxFollowUpQuestions
    }
    class ExamStructure {
        <<entity>>
        +Guid Id
        +Guid CourseId
        +Guid CreatedByUserId
        +string Name
        +int TotalQuestions
        +int DurationMinutes
        +string SlotDefinitions
        +bool IsActive
    }
    class MockExamSession {
        <<entity>>
        +Guid Id
        +Guid StudentUserId
        +Guid CourseId
        +int TotalQuestions
        +int DurationSeconds
        +bool HasFollowUp
        +string Status
        +DateTimeOffset StartedAt
    }

    CoursesController ..> UpdateCourseConfigurationCommandHandler : Uses
    MockExamsController ..> GetMockExamQuotaQueryHandler : Uses
    UpdateCourseConfigurationCommandHandler ..> IApplicationDbContext : Uses
    GetMockExamQuotaQueryHandler ..> IApplicationDbContext : Uses
    GetMockExamQuotaQueryHandler ..> IQuotaService : Uses
    QuotaService ..|> IQuotaService : Implements
    QuotaService ..> IApplicationDbContext : Uses
    OralExamDbContext ..|> IApplicationDbContext : Implements
    IApplicationDbContext ..> Course : Uses
    IApplicationDbContext ..> ExamStructure : Uses
    IApplicationDbContext ..> MockExamSession : Uses
```

---

## 3.9.2 Đặc Tả Chi Tiết Lớp & Phương Thức (Class Specifications)

### 1. Bảng Tóm Tắt Các Lớp Thiết Kế

| STT | Tên Lớp / Giao Diện | Phân Tầng | Trách Nhiệm Nghiệp Vụ |
| :--- | :--- | :--- | :--- |
| 1 | `CoursesController` | `API Layer` | Cho phép `admin` cấu hình hạn ngạch thi thử mỗi ngày (`max_mock_exams_per_day`) theo môn học và cho phép Trưởng bộ môn (`department_head`) cấu hình cấu trúc đề thi thử, tỷ lệ mức độ khó/Bloom và thời lượng làm bài (`PUT /api/v1/courses/{id}/configuration`). |
| 2 | `MockExamsController` | `API Layer` | Cung cấp endpoint kiểm tra hạn ngạch thi thử còn lại trong ngày của sinh viên theo từng môn học (`GET /api/v1/mock-exams/quota?courseId={id}`). |
| 3 | `UpdateCourseConfigurationCommandHandler` | `Application Layer` | Kiểm tra phân quyền giữa `admin` và `department_head`, xác thực tổng số câu hỏi khớp với phân bổ độ khó (`EasyCount + MediumCount + HardCount == TotalQuestions`) và lưu cấu hình vào `courses` cùng `exam_structures`. |
| 4 | `GetMockExamQuotaQueryHandler` | `Application Layer` | Phối hợp với `IQuotaService` và `IApplicationDbContext` để trả về hạn ngạch tối đa trong ngày, số lượt đã sử dụng, số lượt còn lại và thời điểm làm mới hạn ngạch (`ResetsAtUtc`). |
| 5 | `QuotaService` | `Infrastructure / Application` | Thực thi `IQuotaService`, tính toán số lượt thi thử trong ngày của sinh viên theo môn dựa trên `courses.max_mock_exams_per_day` (không khóa cứng $K=3$), chặn vượt ngưỡng bằng ngoại lệ `HTTP 429 Too Many Requests`. |
| 6 | `Course` / `ExamStructure` / `MockExamSession` | `Domain Layer` | Thực thể lưu trữ hạn ngạch môn học, ma trận cấu trúc đề (`slot_definitions` JSONB) và phiên thi thử của sinh viên. |

### 2. Đặc Tả Phương Thức Trọng Tâm (Method Specifications)

| Lớp | Phương Thức | Tham Số Đầu Vào | Kiểu Trả Về | Mô Tả Xử Lý & Quy Tắc Nghiệp Vụ |
| :--- | :--- | :--- | :--- | :--- |
| `CoursesController` | `UpdateCourseConfiguration` | `Guid id, UpdateCourseConfigurationRequest req, CancellationToken ct` | `Task<IActionResult>` | Cập nhật cấu hình đề thi thử (`MockExamTotalQuestions`, `MockExamDurationMinutes`, phân bổ độ khó) và hạn ngạch `MaxMockExamsPerDay`. |
| `MockExamsController` | `GetQuota` | `Guid courseId, CancellationToken ct` | `Task<IActionResult>` | Trả về `MockExamQuotaStatusDto` chứa `MaxAllowedPerDay`, `AttemptsUsedToday`, `RemainingToday`, `IsExhausted` và `ResetsAtUtc`. |
| `QuotaService` | `CheckQuotaAsync` | `Guid studentId, Guid courseId, CancellationToken ct` | `Task<QuotaResult>` | Đếm các phiên thi thử trong ngày hiện tại của sinh viên đối với môn `courseId` và đối chiếu với `Course.MaxMockExamsPerDay`. |

---

## 3.9.3 Sequence Diagram (`DD09_MockConfigQuota_Sequence_Diagram`)

- **Tệp nguồn Draw.io:** [DD09_MockConfigQuota_Sequence_Diagram.drawio](file:///d:/Đồ%20Án/05_Source_Code/docs/Task%20Daily/Report4_PhamNguyenDangHai/04_DD09_MockExam_Config_Daily_Quota_Guard/DD09_MockConfigQuota_Sequence_Diagram.drawio)
- **Tệp ảnh PNG:** [DD09_MockConfigQuota_Sequence_Diagram.png](file:///d:/Đồ%20Án/05_Source_Code/docs/Task%20Daily/Report4_PhamNguyenDangHai/04_DD09_MockExam_Config_Daily_Quota_Guard/DD09_MockConfigQuota_Sequence_Diagram.png)

```mermaid
sequenceDiagram
    actor User
    participant MockExamPortalUI
    participant MockExamControllers
    participant ConfigAndQuotaHandlers
    participant QuotaService
    participant IApplicationDbContext
    participant Database

    User->>MockExamPortalUI: 1. Open mock exam portal page
    alt [Department Head or Admin Updates Mock Exam Configuration]
        User->>MockExamPortalUI: 2.1 Submit mock exam configuration
        MockExamPortalUI->>MockExamControllers: 3.1 Send mock configuration request
        MockExamControllers->>ConfigAndQuotaHandlers: 4.1 Forward configuration command
        ConfigAndQuotaHandlers->>IApplicationDbContext: 5.1 Load course and exam structure
        IApplicationDbContext->>Database: 6.1 Query course and structure rows
        Database-->>IApplicationDbContext: 7.1 Return course and structure rows
        IApplicationDbContext-->>ConfigAndQuotaHandlers: 8.1 Return loaded configuration
        ConfigAndQuotaHandlers->>ConfigAndQuotaHandlers: 9.1 Validate slots and duration
        ConfigAndQuotaHandlers->>IApplicationDbContext: 10.1 Save quota and exam structure
        IApplicationDbContext->>Database: 11.1 Update course and structure rows
        Database-->>IApplicationDbContext: 12.1 Return updated records
        IApplicationDbContext-->>ConfigAndQuotaHandlers: 13.1 Confirm configuration saved
        ConfigAndQuotaHandlers-->>MockExamControllers: 14.1 Return updated configuration
        MockExamControllers-->>MockExamPortalUI: 15.1 Return configuration response
        MockExamPortalUI-->>User: 16.1 Display saved mock exam settings
    else [Student Checks Daily Mock Exam Quota]
        User->>MockExamPortalUI: 2.2 Select course to check quota
        MockExamPortalUI->>MockExamControllers: 3.2 Request daily mock exam quota
        MockExamControllers->>ConfigAndQuotaHandlers: 4.2 Forward get mock quota query
        ConfigAndQuotaHandlers->>QuotaService: 5.2 Evaluate student daily quota
        QuotaService->>IApplicationDbContext: 6.2 Query course limit and attempts
        IApplicationDbContext->>Database: 7.2 Count today mock exam sessions
        Database-->>IApplicationDbContext: 8.2 Return limit and attempt count
        IApplicationDbContext-->>QuotaService: 9.2 Return quota calculation data
        QuotaService-->>ConfigAndQuotaHandlers: 10.2 Return evaluated quota status
        alt [Daily Mock Exam Quota Exceeded]
            ConfigAndQuotaHandlers-->>MockExamControllers: 11.2 Return quota exhausted status
            MockExamControllers-->>MockExamPortalUI: 12.2 Return quota exhausted response
            MockExamPortalUI-->>User: 13.2 Display daily quota limit notice
        else [Daily Mock Exam Quota Available]
            ConfigAndQuotaHandlers-->>MockExamControllers: 11.3 Return remaining attempts status
            MockExamControllers-->>MockExamPortalUI: 12.3 Return available quota response
            MockExamPortalUI-->>User: 13.3 Enable start mock exam button
        end
    end
    opt [Exception occurs]
        ConfigAndQuotaHandlers->>ConfigAndQuotaHandlers: 14.4 Log unexpected error
        ConfigAndQuotaHandlers-->>MockExamControllers: 15.4 Return system error response
        MockExamControllers-->>MockExamPortalUI: 16.4 Display system error notification
    end
```

### Thuyết Minh Luồng Tuần Tự (`DD-09`)
1. **Nhánh 1 — Trưởng Bộ môn hoặc Admin cập nhật cấu hình Thi thử & Hạn ngạch (`2.1` $\to$ `16.1`):** Người dùng quản trị gửi cấu hình thời lượng, cơ cấu câu hỏi và hạn ngạch ngày (`2.1` $\to$ `4.1`). Handler tải bản ghi môn học và cấu trúc đề hiện tại từ `Database` (`5.1` $\to$ `8.1`), kiểm tra tổng số câu hỏi và thời lượng hợp lệ (`9.1`), sau đó lưu cập nhật vào `courses` và `exam_structures` (`10.1` $\to$ `16.1`).
2. **Nhánh 2 — Sinh viên kiểm tra hạn ngạch Thi thử trong ngày (`2.2` $\to$ `13.3`):** Khi sinh viên chọn môn học trên cổng thi thử (`2.2` $\to$ `4.2`), `QuotaService` truy vấn `Database` để lấy định mức `max_mock_exams_per_day` của môn học và đếm số phiên thi thử đã thực hiện trong ngày của sinh viên (`5.2` $\to$ `10.2`). Nếu đã hết lượt, giao diện hiển thị thông báo hết hạn ngạch kèm thời gian đặt lại (`11.2` $\to$ `13.2`); nếu còn lượt, hệ thống mở nút bắt đầu thi thử (`11.3` $\to$ `13.3`).
3. **Khung ngoại lệ `opt [Exception occurs]` (`14.4` $\to$ `16.4`):** Ghi nhật ký lỗi bất ngờ (`14.4`) và trả thông báo lỗi chuẩn hóa về giao diện (`15.4`, `16.4`).
