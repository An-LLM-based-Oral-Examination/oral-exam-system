# REPORT 4 — SOFTWARE DESIGN DOCUMENT (DETAILED DESIGN)
## PHẦN PHỤ TRÁCH: NGUYỄN QUANG THÀNH — SƠ ĐỒ TUẦN TỰ DESKTOP KIOSK `D-09` & `D-10` (`[Target Design / Proposed]` — `MF-04`)

> **Thông tin tài liệu:**
> - **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (`FA26SE166`)
> - **Người thực hiện:** Nguyễn Quang Thành | **Người review chéo:** Phạm Nguyễn Đăng Hải
> - **Phạm vi bàn giao:** 02 Sơ đồ Tuần tự Desktop Kiosk (`D-09` & `D-10`) gắn nhãn minh bạch **`[Target Design / Proposed]`** cho phần Kiosk Client (`OralExam.DesktopKiosk`) kết nối trực tiếp với hệ thống Backend API thật đã triển khai trong `05_Source_Code`.
> - **Tệp sơ đồ Draw.io trong cùng thư mục:**
>   - `D-09 Sequence Diagrams`: [`D09_01_Kiosk_Startup_Mic_Calibration_Sequence.drawio`](./D09_01_Kiosk_Startup_Mic_Calibration_Sequence.drawio) | [`D09_02_Kiosk_IP_Binding_CheckIn_Sequence.drawio`](./D09_02_Kiosk_IP_Binding_CheckIn_Sequence.drawio) | [`D09_Kiosk_Startup_PreCheck_CheckIn_Sequence.drawio`](./D09_Kiosk_Startup_PreCheck_CheckIn_Sequence.drawio)
>   - `D-10 Sequence Diagrams`: [`D10_01_Kiosk_Lockdown_Sealed_Submission_Sequence.drawio`](./D10_01_Kiosk_Lockdown_Sealed_Submission_Sequence.drawio) | [`D10_Kiosk_Exam_Execution_Persist_First_Sequence.drawio`](./D10_Kiosk_Exam_Execution_Persist_First_Sequence.drawio)

---

## 2.9. Sơ Đồ Tuần Tự `D-09`: Khởi Động Desktop Kiosk, Kiểm Tra Phần Cứng ($\ge 60\text{ dB}$), Đối Chiếu IP Máy Trạm (`ip_address`), Check-in Vé Thi & Khóa Chống Gian Lận (`[Target Design / Proposed]`)
*(Sequence Diagram D-09 — Desktop Kiosk Startup, Hardware Pre-Check, Workstation IP Binding, Check-in & Anti-Cheat Lockdown)*

### 2.9.1. Tổng Quan Nghiệp Vụ & Kiến Trúc (`D-09 Overview`)
Sơ đồ `D-09` mô tả luồng tương tác từ lúc thí sinh khởi chạy ứng dụng **Desktop Kiosk (`[Target Design / Proposed]`)** tại phòng Lab cho đến khi hoàn tất kiểm tra phần cứng, nhận vé thi và chịu sự giám sát chống gian lận thời gian thực:
1. **Khóa màn hình Kiosk & Đối chiếu IP máy trạm (`ip_address`):**
   - Ngay khi khởi động, `ShellLockdown & LobbyView [Kiosk]` kích hoạt chế độ toàn màn hình (`Fullscreen Lockdown`), chặn các tổ hợp phím hệ thống (`Alt+Tab`, `Win`, `F11`, `F12`, `DevTools`).
   - `TicketCheckInService [Kiosk]` gọi `GET /api/v1/official-exams/tickets/current` tới [`OfficialExamCheckInController`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/API/Controllers/v1/OfficialExamCheckInController.cs). Backend đối chiếu địa chỉ IP máy trạm của vé thi thông qua cột chuẩn **`ip_address`** (`SeatNumber` `1..40`) và trạng thái ca thi đã xuất bản đề (`is_exam_published == true`). Nếu sai máy trạm hoặc ca thi chưa mở, hệ thống trả về **`HTTP 403 Forbidden` (`WrongWorkstation`)**.
2. **Kiểm tra Microphone 30 giây ($\ge 60\text{ dB}$) & Check-in vé thi (`SCHEDULED` $\to$ `IN_PROGRESS`):**
   - `MicCalibrationService [Kiosk]` yêu cầu thí sinh nói thử trong 30 giây. Nếu biên độ âm thanh đỉnh đạt ngưỡng **$\ge 60\text{ dB}$**, Kiosk gọi `POST /api/v1/official-exams/tickets/{id}/check-in` để chuyển trạng thái vé thi từ `SCHEDULED` sang `IN_PROGRESS`.
3. **Phát hiện vi phạm tiêu điểm (`Blur > 3 lần`) & Đình chỉ vé thi (`LOCKED`):**
   - Trong suốt quá trình làm bài, nếu thí sinh làm mất tiêu điểm cửa sổ quá 3 lần (`blurCount > 3`) hoặc cố tình can thiệp phím tắt hệ thống, Kiosk gửi sự kiện `POST /api/v1/official-exams/anti-cheat/log` (hoặc Giám thị lập biên bản qua `POST /api/v1/proctor/shifts/{shiftId}/violations`). Backend ghi nhận vào bảng `exam_anti_cheat_logs`, chuyển trạng thái vé thi sang **`LOCKED`** và phát cảnh báo thời gian thực qua [`AntiCheatHub`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/API/Hubs/AntiCheatHub.cs) (`/hubs/anti-cheat`).

---

### 2.9.2. Sơ Đồ Tuần Tự `D-09` (`D-09 Sequence Diagram`)

> **Tệp sơ đồ Draw.io (Phân rã & Tổng quan):**
> - Phân rã 1: [`D09_01_Kiosk_Startup_Mic_Calibration_Sequence.drawio`](./D09_01_Kiosk_Startup_Mic_Calibration_Sequence.drawio) (Kiosk Startup Lockdown & Mic Calibration >= 60dB)
> - Phân rã 2: [`D09_02_Kiosk_IP_Binding_CheckIn_Sequence.drawio`](./D09_02_Kiosk_IP_Binding_CheckIn_Sequence.drawio) (Kiosk Seat STT Matching & IP Address Binding)
> - Tổng quan kế thừa: [`D09_Kiosk_Startup_PreCheck_CheckIn_Sequence.drawio`](./D09_Kiosk_Startup_PreCheck_CheckIn_Sequence.drawio)

```mermaid
sequenceDiagram
    actor Candidate as Candidate
    participant Lobby as ShellLockdown & LobbyView
    participant CheckIn as TicketCheckInService
    participant Mic as MicCalibrationService
    participant API as OfficialExams & Proctor API
    participant Hub as AntiCheatHub SignalR
    participant DB as Database

    Candidate->>Lobby: 1. Launch Kiosk and sign in
    Lobby->>Lobby: 2. Enable fullscreen lockdown and keyboard hook

    alt Multiple Displays, Forbidden Process or VM Detected
        Lobby-->>Candidate: 3.1 Block entry and display alert
    else Workstation Environment Clean
        Candidate->>Lobby: 3.2 Select shift and seat number
        Lobby->>CheckIn: 4. Request seat check-in with IP
        CheckIn->>API: 5. Send seat check-in request
        API->>DB: 6. Verify ticket attendance and workstation IP
        DB-->>API: 7. Return ticket and IP verification status

        alt IP Conflict on Seat
            API-->>CheckIn: 8.1 Return seat IP conflict error
            CheckIn-->>Lobby: 9.1 Display workstation conflict error
        else Valid Seat and IP Sealed
            API->>DB: 8.2 Seal workstation IP and update check-in status
            DB-->>API: 9.2 Confirm check-in saved
            API-->>CheckIn: 10.2 Return check-in confirmation
        end

        Lobby->>Mic: 11. Run thirty-second microphone calibration
        alt Signal Below Threshold or Silent
            Mic-->>Lobby: 12.1 Return low signal warning
        else Signal Meets Threshold
            Mic-->>Lobby: 12.2 Confirm microphone calibrated
        end

        Lobby->>Hub: 13. Connect to anti-cheat hub and join shift group
        Hub-->>Lobby: 14. Confirm shift room joined
        Lobby-->>Candidate: 15. Display waiting lobby screen
    end

    opt Shift Exam Published by Examination Officer
        Hub->>Lobby: 16. Broadcast exam published event
        Lobby->>API: 17. Request assigned exam paper
        API->>DB: 18. Load assigned exam set and questions
        DB-->>API: 19. Return exam set questions
        API-->>Lobby: 20. Return exam paper payload
        Lobby-->>Candidate: 21. Open exam workspace
    end

    opt Exception occurs
        API->>API: 22. Log error
        API-->>CheckIn: 23. Return error response
        CheckIn-->>Lobby: 24. Display error dialog
    end
```

*Hình 2.9.1 — Sơ đồ tuần tự `D-09` (Khởi động Desktop Kiosk, Kiểm tra Mic $\ge 60\text{ dB}$, Đối chiếu `ip_address`, Check-in & Chống gian lận).*

---

## 2.10. Sơ Đồ Tuần Tự `D-10`: Thực Thi Bài Thi Kiosk, Ghi Âm Bất Biến `STT_MSSV.webm` + Băm SHA-256, Tải Lên Cloudflare R2 & Nộp Bài Persist-First (`< 100\text{ms}`) (`[Target Design / Proposed]`)
*(Sequence Diagram D-10 — Desktop Kiosk Exam Execution, Immutable Audio Sealing, R2 Upload & Persist-First Submission)*

### 2.10.1. Tổng Quan Nghiệp Vụ & Kiến Trúc (`D-10 Overview`)
Sơ đồ `D-10` đặc tả luồng làm bài vấn đáp chính thức trên **Desktop Kiosk (`[Target Design / Proposed]`)**, cơ chế ghi nhật ký mã hóa cục bộ chống mất điện/rớt mạng, niêm phong tệp âm thanh bất biến và giao thức nộp bài 3 bước với [`OfficialExamSubmissionsController`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/API/Controllers/v1/OfficialExamSubmissionsController.cs):
1. **Ghi âm chống mất dữ liệu & Niêm phong `STT_MSSV.webm` + `SHA-256` tại Kiosk:**
   - Khi thí sinh trả lời từng câu hỏi chính và câu hỏi phụ (`has_follow_up` 1–5 câu do Trưởng Bộ Môn cấu hình, mặc định 2), `AudioServices & LocalJournal [Kiosk]` vừa thu âm vừa lưu tạm các mảnh dữ liệu mã hóa `AES-256` xuống đĩa cục bộ (`EncryptedLocalJournal`).
   - Trong khoảng đếm ngược `TranscriptBufferSeconds` (10–300 giây, mặc định 60 giây) nếu ca thi bật chế độ `VoiceWithTranscriptEdit`, thí sinh chỉ được phép rà soát và sửa các thuật ngữ chuyên ngành Kỹ thuật Phần mềm (Code-Switching).
   - Khi kết thúc trả lời, Kiosk đóng gói tệp âm thanh theo đúng định danh bất biến **`STT_MSSV.webm`** (ví dụ: `01_SE170123.webm`, với `STT` là `SeatNumber` `01..40` định dạng 2 chữ số) và tính mã băm **`SHA-256` (64 ký tự hex)** ngay tại máy trạm trước khi truyền qua mạng.
2. **Giao thức nộp bài 3 bước & Persist-First (`< 100ms`, Không chấm điểm liền tại Kiosk):**
   - **Bước 1 (`POST /api/v1/official-exams/tickets/{ticketId}/audio-upload-authorizations`):** Xin cấp URL tải lên có chữ ký (`Presigned PUT URL`) từ Cloudflare R2 tương ứng với `STT_MSSV.webm`.
   - **Bước 2 (`Direct PUT to R2` + `PUT /api/v1/official-exams/tickets/{ticketId}/audio`):** Tải trực tiếp tệp `STT_MSSV.webm` lên Cloudflare R2, sau đó gửi `objectKey` và `audioSha256` về Backend để đối chiếu sự tồn tại của đối tượng trên R2 và khóa niêm phong `AudioSha256` vào vé thi.
   - **Bước 3 (`PUT /api/v1/official-exams/tickets/{ticketId}/submission`):** Lưu bền vững toàn bộ `exam_question_submissions` và chuyển trạng thái vé thi sang **`SUBMITTED` trong `< 100ms`**, đồng thời đẩy tác vụ chấm điểm vào `BoundedChannel<GradingTask>` (1.000 slots) và trả về **`HTTP 202 Accepted`**.
   - **Quy tắc bất biến phòng thi (`MF-04`):** Ngay khi nhận `HTTP 202 Accepted`, Kiosk xóa bản ghi tạm cục bộ và khóa màn hình ở trạng thái **`SafeSubmittedLockView`** báo đã lưu bài an toàn; **tuyệt đối không hiển thị điểm số ngay tại Kiosk và không mở form khiếu nại tại chỗ**.
3. **Khôi phục an toàn khi đứt mạng tạm thời (`Idempotent Retry Recovery`):**
   - Nếu xảy ra lỗi mạng tạm thời trong lúc nộp, `SubmissionDispatcher [Kiosk]` đọc lại gói dữ liệu và mã băm `SHA-256` từ `LocalJournal`, thực hiện gọi lại một cách bất biến (Idempotent) tới `PUT /submission` hoặc kiểm tra trạng thái qua `GET /submission` để đảm bảo **Zero Data Loss**.

---

### 2.10.2. Sơ Đồ Tuần Tự `D-10` (`D-10 Sequence Diagram`)

> **Tệp sơ đồ Draw.io (Phân rã & Tổng quan):**
> - Phân rã 1: [`D10_01_Kiosk_Lockdown_Sealed_Submission_Sequence.drawio`](./D10_01_Kiosk_Lockdown_Sealed_Submission_Sequence.drawio) (Kiosk Audio Sealing SHA-256 & R2 Persist-First Submission)
> - Tổng quan kế thừa: [`D10_Kiosk_Exam_Execution_Persist_First_Sequence.drawio`](./D10_Kiosk_Exam_Execution_Persist_First_Sequence.drawio)

```mermaid
sequenceDiagram
    actor Candidate as Candidate
    participant Canvas as ExamCanvas & SafeLockView
    participant Audio as AudioServices & LocalJournal
    participant Disp as SubmissionDispatcher
    participant R2 as Cloudflare R2 Storage
    participant API as OfficialExamSubmissions API
    participant DB as PostgreSQL & BoundedChannel

    Candidate->>Canvas: 1. Speak answer into microphone
    Canvas->>Audio: 2. Capture audio and write chunks
    Candidate->>Canvas: 3. Click Stop Recording
    Canvas->>Audio: 4. Finalize audio and compute hash
    Audio->>Audio: 5. Save encrypted journal entry
    Audio-->>Canvas: 6. Return audio path and hash

    alt Network Online - Immediate Persist-First Upload
        Canvas->>Disp: 7.1 Queue submission for dispatch
        Disp->>API: 8.1 Request speech transcription
        API-->>Disp: 9.1 Return raw transcript
        Disp-->>Canvas: 10.1 Start transcript review countdown
        Canvas-->>Candidate: 11.1 Play audio and show transcript
        opt Transcript Edit Mode Enabled
            Candidate->>Canvas: 12.1 Edit technical terms
            Canvas->>Disp: 13.1 Confirm final transcript
        end
        Disp->>R2: 14.1 Upload immutable audio file
        R2-->>Disp: 15.1 Confirm audio file stored
        Disp->>API: 16.1 Submit answer with audio URL and hash
        API->>DB: 17.1 Save submission and ticket
        DB-->>API: 18.1 Confirm submission saved
        API->>DB: 19.1 Enqueue grading task
        API-->>Disp: 20.1 Return submission receipt
        Disp-->>Canvas: 21.1 Activate safe lock screen without score
        Canvas-->>Candidate: 22.1 Display submission saved
    else Network Disconnected - Offline Local Journal & Auto-Sync
        Disp->>Audio: 7.2 Read encrypted local journal
        Audio-->>Disp: 8.2 Return persisted submission
        Disp-->>Canvas: 9.2 Display offline safe-save status
        Disp->>R2: 10.2 Retry upload when online
        R2-->>Disp: 11.2 Confirm audio file stored
        Disp->>API: 12.2 Resend submission payload
        API->>DB: 13.2 Save submission and queue task
        DB-->>API: 14.2 Confirm submission saved
        API-->>Disp: 15.2 Return submission receipt
    end

    opt Exception occurs
        API->>API: 23. Log error
        API-->>Disp: 24. Return error response
        Disp-->>Canvas: 25. Display error notification
    end
```

*Hình 2.10.1 — Sơ đồ tuần tự `D-10` (Thực thi bài thi Kiosk, Niêm phong `STT_MSSV.webm` + `SHA-256`, Tải lên R2 & Nộp bài Persist-First `< 100\text{ms}`).*

---

## 2.11. Bảng Đặc Tả Các Thành Phần Tham Gia Trong `D-09` & `D-10` (`Component & API Specifications`)

| STT | Tên Thành Phần / Lớp | Phân Loại & Đường Dẫn Nguồn | Phương Thức / Endpoint Chính | Trách Nhiệm & Quy Tắc Nghiệp Vụ Cốt Lõi |
|:---:|:---|:---|:---|:---|
| **01** | **`ShellLockdown & LobbyView`** | `Desktop Kiosk Client` **`[Target Design / Proposed]`** | `+ EnableFullscreenLockdown()`<br>`+ MonitorWindowFocusBlur()`<br>`+ LockScreenOnViolation()` | • Khóa toàn màn hình Kiosk, chặn `Alt+Tab`, `Win`, `F11`, `F12`, `DevTools`.<br>• Đếm số lần mất tiêu điểm (`blurCount`); vượt quá 3 lần sẽ tự động gửi yêu cầu khóa vé thi (`LOCKED`). |
| **02** | **`MicCalibrationService`** | `Desktop Kiosk Client` **`[Target Design / Proposed]`** | `+ Run30SecondMicTestAsync()`<br>`+ MeasurePeakDecibels()` | • Đo cường độ tín hiệu âm thanh đầu vào trong 30 giây.<br>• Chỉ mở nút Check-in vào phòng thi khi cường độ đạt ngưỡng **$\ge 60\text{ dB}$**. |
| **03** | **`AudioServices & LocalJournal`** | `Desktop Kiosk Client` **`[Target Design / Proposed]`** | `+ SpoolEncryptedChunkAsync()`<br>`+ FinalizeImmutableWebmAsync(seat, mssv)`<br>`+ ComputeSha256HexAsync()` | • Lưu tạm các mảnh âm thanh mã hóa `AES-256` xuống đĩa cục bộ.<br>• Xuất tệp âm thanh bất biến **`STT_MSSV.webm`** (vd `01_SE170123.webm`) và tính mã băm **`SHA-256` (64 ký tự hex)**. |
| **04** | **`SubmissionDispatcher`** | `Desktop Kiosk Client` **`[Target Design / Proposed]`** | `+ ExecuteThreeStepSubmitAsync()`<br>`+ RecoverFromJournalAsync()` | • Điều phối tuần tự 3 bước: xin Presigned URL $\to$ PUT tệp lên R2 + xác nhận SHA-256 $\to$ PUT bài nộp Persist-First.<br>• Tự động thử lại bất biến (Idempotent Retry) từ nhật ký cục bộ nếu rớt mạng tạm thời. |
| **05** | **`OfficialExamCheckInController`** | `Backend API Layer` *(Đã hiện thực)*<br>[`OfficialExamCheckInController.cs`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/API/Controllers/v1/OfficialExamCheckInController.cs) | `GET /api/v1/official-exams/tickets/current`<br>`POST /api/v1/official-exams/tickets/{id}/check-in` | • Kiểm tra vé thi của thí sinh theo `ip_address` máy trạm và trạng thái `is_exam_published`.<br>• Chuyển trạng thái vé thi từ `SCHEDULED` sang `IN_PROGRESS` sau khi vượt qua bước kiểm tra phần cứng. |
| **06** | **`OfficialExamsController` & `ProctorController`** | `Backend API Layer` *(Đã hiện thực)*<br>[`OfficialExamsController.cs`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/API/Controllers/v1/OfficialExamsController.cs)<br>[`ProctorController.cs`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/API/Controllers/v1/ProctorController.cs) | `POST /api/v1/official-exams/anti-cheat/log`<br>`POST /api/v1/proctor/shifts/{shiftId}/violations`<br>`POST /api/v1/official-exams/tickets/{id}/suspend` | • Ghi nhật ký vi phạm vào bảng `exam_anti_cheat_logs`.<br>• Khi `blur > 3` hoặc vi phạm nghiêm trọng, chuyển trạng thái vé thi sang `LOCKED` và phát cảnh báo qua `AntiCheatHub`. |
| **07** | **`OfficialExamSubmissionsController`** | `Backend API Layer` *(Đã hiện thực)*<br>[`OfficialExamSubmissionsController.cs`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/API/Controllers/v1/OfficialExamSubmissionsController.cs) | `POST .../audio-upload-authorizations`<br>`PUT .../tickets/{ticketId}/audio`<br>`PUT .../tickets/{ticketId}/submission`<br>`GET .../tickets/{ticketId}/submission` | • Cấp Presigned URL tải tệp `STT_MSSV.webm` lên Cloudflare R2.<br>• Xác thực tệp trên R2, khóa mã băm `AudioSha256`, lưu bài nộp ở trạng thái `SUBMITTED` trong `< 100ms`, đẩy vào `BoundedChannel` và trả `HTTP 202 Accepted`. |

*Bảng 2.11.1 — Đặc tả các thành phần Kiosk Client (`[Target Design / Proposed]`) và Backend API trong `D-09` & `D-10`.*
