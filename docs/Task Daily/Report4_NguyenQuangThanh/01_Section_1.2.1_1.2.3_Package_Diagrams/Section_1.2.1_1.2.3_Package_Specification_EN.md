# REPORT 4 — SOFTWARE DESIGN DOCUMENT (ENGLISH EDITION)
## SECTION 1.2.1 (`D-03`) & SECTION 1.2.3 (`D-05`): PACKAGE DIAGRAMS

> **Document Information:**
> - **Project:** LLM-Based Oral Examination System for Software Engineering (`FA26SE166`)
> - **Lead Architect:** Nguyen Quang Thanh | **Cross-Reviewer:** Pham Nguyen Dang Hai
> - **Diagram Files (Draw.io Vector):**
>   - `D-03` (`1.2.1`): [`D03_Backend_Package_Diagram.drawio`](./D03_Backend_Package_Diagram.drawio)
>   - `D-05` (`1.2.3`): [`D05_Desktop_Kiosk_Package_Diagram.drawio`](./D05_Desktop_Kiosk_Package_Diagram.drawio)

---

## 1.2. System Package Diagrams

### 1.2.1. Backend Subsystem Package Diagram (`D-03`)

#### a) Architectural Rationale for Backend (4-Tier Clean Architecture + CQRS + Persist-First Pipeline)

The **LLM-Based Oral Examination System (`FA26SE166`)** eschews traditional 3-tier layering (`Controller -> Service -> Repository`) in favor of a **4-tier Clean Architecture** integrated with **CQRS (MediatR)** and an asynchronous **Persist-First + BoundedChannel + Background Workers** pipeline for **five fundamental engineering rationales**:

1. **Vendor Lock-in Isolation:**
   - The platform relies on volatile external third-party services: **Google Gemini 1.5 Flash/Pro** (question generation & Chain-of-Thought grading), **Cloudflare Whisper** (STT), **TTS**, and **Cloudflare R2** (immutable audio recording `STT_MSSV.webm`).
   - By declaring abstract domain interfaces (`IAiGradingService`, `IAiQuestionGenerationService`, `ISpeechToTextService`, `ITextToSpeechService`, `IStorageService`) within the `Application Layer` and encapsulating concrete API implementations in the `Infrastructure Layer`, domain models remain 100% vendor-agnostic. Upgrading LLM models or swapping cloud providers requires zero modifications to the `Domain` or `Application` layers.

2. **Concurrent Exam Room Resilience (< 100ms Ingestion & Zero Data Loss):**
   - Evaluating multi-question oral submissions via LLM requires **3–8 seconds** and faces potential rate limits (`HTTP 429 Too Many Requests`) or network drops.
   - Synchronous processing would freeze exam stations. The architecture cleanly separates **Submission Ingestion (`Application.Features`)** from **Background Grading (`API.Workers` + `Infrastructure.Channels`)**: client submissions are persisted to PostgreSQL in the `SUBMITTED` state in **$< 100\text{ ms}$** and enqueued into an in-memory `BoundedChannel` (1,000 slots capacity). Background services pull tasks asynchronously with **Polly 3-attempt exponential backoff (2s, 4s, 8s)** and persistent isolation into **`dead_letter_queues` (DLQ)**.

3. **Dual Client Support on a Single Core Engine (Web SPA & Desktop Kiosk):**
   - The backend concurrently powers the **Web Application** (accommodating 6 RBAC roles for practice MF-01, mock exam MF-02, question bank MF-03, and exam administration MF-04/MF-05) and the **Desktop Kiosk Application** (for candidates at stations `1..40` during official lab exams MF-04).
   - Both client applications share identical MediatR `Commands` and `Queries` without duplicating validation, security, or ticket state machine rules.

4. **Command-Query Separation (CQRS Vertical Slices):**
   - In examination workflows, **Commands** enforce strict domain invariants (Rubric sum $\equiv 10.0$, SHA-256 audio hash matching, one-way grade lock `is_locked = true`), whereas **Queries** require high-speed aggregated projections for candidate scorecards, audit panels, and proctor monitoring.
   - Vertical feature slicing (`Practice`, `MockExams`, `QuestionBank`, `OfficialExams`, `Appeals`) enables 4 team members to develop simultaneously without merge contention on shared service classes.

5. **Formal Legal Auditability & Independent Testability (1,462 Unit Tests):**
   - Because `Domain Layer` and `Application Layer` have zero dependencies on physical database drivers or external network calls, core rules (Anti-3-Consecutive difficulty, daily mock quota, 5-step appeal workflow, question repository isolation) are fully validated by **1,462 automated unit tests (`1,462 / 1,462 Passed`, Exit Code 0)** and automated architectural boundaries via [`CleanArchitectureRulesTests.cs`].

---

#### b) Architectural Layers, Responsibilities, Sub-packages and Concrete Examples

| No. | Layer Name | Project (`.csproj`) | Sub-packages | Core Responsibilities & Dependency Rules |
|:---:|:---|:---|:---|:---|
| **01** | **`Domain Layer`**<br>*(Core Domain Model)* | [`Domain.csproj`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/Domain/Domain.csproj) | `Common`, `Entities`, `Enums`, `Exceptions`, `Identity` | • **Responsibilities:** Encapsulates **38 normalized 3NF database entities**, system enums (6 RBAC roles, 7 uppercase exam ticket states), one-way grade lock interface (`ILockableEntity`), and domain invariants.<br>• **Rule:** **Pure domain core — zero dependencies on any other layer.** |
| **02** | **`Application Layer`**<br>*(Application Business Logic — CQRS)* | [`Application.csproj`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/Application/Application.csproj) | `Features`, `Interfaces`, `Behaviors`, `Models`, `Auth`, `Extensions` | • **Responsibilities:** Coordinates use cases (`MF-01..MF-05`) via **Vertical Slice CQRS** (`Commands`, `Queries`, `Handlers`, `Validators`, `DTOs`), incoming payload validation via `FluentValidation`, and 14 abstract interface ports.<br>• **Rule:** **Depends strictly upon `Domain Layer` only.** |
| **03** | **`Infrastructure Layer`**<br>*(External Integration & Infrastructure)* | [`Infrastructure.csproj`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/Infrastructure/Infrastructure.csproj) | `Persistence`, `Interceptors`, `Services`, `Migrations`, `Channels`, `Authentication`, `Reporting` | • **Responsibilities:** Implements the 14 application interfaces: PostgreSQL 16 persistence (`OralExamDbContext`), one-way grade lock enforcement (`OneWayLockInterceptor`), 1,000-slot RAM `BoundedChannel`, Gemini AI, Whisper STT, Cloudflare R2, and Excel/PDF reporting.<br>• **Rule:** Depends upon `Application Layer` and `Domain Layer`. |
| **04** | **`API Layer`**<br>*(Web API Boundary & Background Hosts)* | [`API.csproj`](file:///d:/Đồ%20Án/05_Source_Code/backend/src/API/API.csproj) | `Controllers`, `Middlewares`, `Configuration`, `Hubs`, `Services`, `Workers` | • **Responsibilities:** HTTP REST (`/api/v1/*`) and SignalR (`/hubs/*`) endpoints listening on port `5000`, standardized RFC 7807 `ProblemDetails` error responses, and 4 background worker services consuming grading queues and DLQ retries.<br>• **Rule:** Depends upon `Application Layer`, `Infrastructure Layer`, and `Domain Layer`. |

---

#### c) Backend Package Diagram (`Figure 1.2.1` / `D-03`)

> **Draw.io Vector Source:** [`D03_Backend_Package_Diagram.drawio`](./D03_Backend_Package_Diagram.drawio)

*Figure 1.2.1 — Backend Application Subsystem Package Diagram (`D-03`).*

---

#### d) Backend Sub-package Specifications (`Table 1.2.1`)

| No. | Layer | Sub-package | Source Path | Technical Responsibilities |
|:---:|:---|:---|:---|:---|
| **01** | **API Layer** | **`Controllers`** | `API/Controllers/v1/` | Contains 25 REST Controllers serving both Web and Kiosk applications across 6 RBAC roles (`Auth`, `Speech`, `Practice`, `MockExams`, `Questions`, `Rubrics`, `OfficialExams`, `Proctor`, `Appeals`, `DeadLetterQueue`, `Admin`). |
| **02** | **API Layer** | **`Middlewares`** | `API/Middlewares/` | Centralized middleware pipeline: `ExceptionHandlingMiddleware` (RFC 7807), `MustChangePasswordMiddleware` (HTTP 403), `SecurityHeadersMiddleware`, and `RateLimitingMiddleware`. |
| **03** | **API Layer** | **`Workers`** | `API/Workers/` | Background hosted services: `GradingQueueWorker` (consumes 1,000-slot BoundedChannel with MaxDegreeOfParallelism = 12), `DeadLetterQueueRetryWorker` (5m retry), and `SessionInactivityTimeoutWorker`. |
| **04** | **API Layer** | **`Hubs`** | `API/Hubs/` | Strongly-typed SignalR Hubs: `PracticeHub` (`/hubs/practice`), `InClassShiftHub`, and `ExamAntiCheatHub` (`/hubs/anti-cheat`). |
| **05** | **Application Layer** | **`Features`** | `Application/Features/` | CQRS vertical slices organized by domain (`Auth`, `Practice`, `MockExams`, `QuestionBank`, `OfficialExams`, `InClassShifts`, `Appeals`). Each slice contains Commands, Queries, Handlers, Validators, and DTOs. |
| **06** | **Application Layer** | **`Interfaces`** | `Application/Interfaces/` | Abstract domain port contracts: `IAiGradingService`, `IAiQuestionGenerationService`, `ISpeechToTextService`, `ITextToSpeechService`, `IStorageService`, `IOralExamDbContext`, `IPasswordHasher`, `IAccessTokenIssuer`. |
| **07** | **Application Layer** | **`Behaviors`** | `Application/Behaviors/` | MediatR pipeline behaviors: `ValidationBehavior` (automatic FluentValidation check), `LoggingBehavior` (structured audit logging), `PerformanceBehavior` (identifying slow requests > 500ms). |
| **08** | **Infrastructure Layer** | **`Persistence`** | `Infrastructure/Persistence/` | Entity Framework Core 8 context (`OralExamDbContext`), entity configurations, and database query optimization. |
| **09** | **Infrastructure Layer** | **`Interceptors`** | `Infrastructure/Interceptors/` | Database save interceptors: `OneWayLockInterceptor` (blocks modifications to locked entities `is_locked = true` with HTTP 403) and `AuditableEntityInterceptor` (`CreatedAt`, `UpdatedAt`). |
| **10** | **Infrastructure Layer** | **`Channels`** | `Infrastructure/Channels/` | Thread-safe in-memory queue `BoundedChannel<GradingTask>` (capacity 1,000, `FullMode.Wait`) decoupling submission ingestion from grading execution. |
| **11** | **Infrastructure Layer** | **`Services`** | `Infrastructure/Services/` | External integrations: `GeminiAiGradingService`, `CloudflareWhisperSttService`, `CloudflareR2StorageService`, and `ReportingService` (Excel/PDF generation). |
| **12** | **Domain Layer** | **`Entities`** | `Domain/Entities/` | 38 core normalized entities in 3NF across 7 bounded contexts (`User`, `PracticeSession`, `PracticeAnswer`, `StudentExamTicket`, `ExamQuestionSubmission`, `AppealRequest`, etc.). |
| **13** | **Domain Layer** | **`Common`** | `Domain/Common/` | Base entities: `BaseEntity`, `BaseAuditableEntity`, and `ILockableEntity` (one-way lock contract). |
| **14** | **Domain Layer** | **`Enums`** | `Domain/Enums/` | Domain enums: `UserRole` (6 roles), `TicketStatus` (7 states), `DifficultyLevel`, `SessionStatus`, and `AppealStatus`. |

---

### 1.2.2. Desktop Kiosk Subsystem Package Diagram (`D-05`)

#### a) Desktop Kiosk Package Diagram (`Figure 1.2.3` / `D-05`)

> **Draw.io Vector Source:** [`D05_Desktop_Kiosk_Package_Diagram.drawio`](./D05_Desktop_Kiosk_Package_Diagram.drawio)

*Figure 1.2.3 — Desktop Kiosk Application Package Diagram (`D-05`).*

---

#### b) Desktop Kiosk Sub-package Specifications (`Table 1.2.3`)

| No. | Layer | Sub-package | Target Technology | Technical Responsibilities |
|:---:|:---|:---|:---|:---|
| **01** | **UI View Layer** | **`Views` & `Components`** | WPF / Avalonia XAML | Exam room lockdown user interface: fullscreen view, station seat display (`SeatNumber` 1..40), candidate check-in screen, audio visualizer, question display, and countdown timer. |
| **02** | **Presentation Layer** | **`ViewModels`** | CommunityToolkit.Mvvm | Presentation logic: `KioskStartupViewModel`, `HardwareCheckViewModel`, `CheckInViewModel`, `ExamExecutionViewModel`, and `SubmissionReviewViewModel`. |
| **03** | **Hardware Audio Layer** | **`HardwareAudio`** | NAudio / WASAPI | Audio hardware interaction: 30-second microphone test checking average volume $\ge 60\text{ dB}$, raw PCM audio recording, and encoding to `STT_MSSV.webm`. |
| **04** | **Security Lockdown Layer** | **`SecurityLockdown`** | Win32 API Hooks | Secure exam environment lockdown: fullscreen lock, blocking Alt+Tab, Windows key, Ctrl+Esc, Task Manager, Alt+F4, and reporting application blur events. |
| **05** | **Station Identity Layer** | **`StationIdentity`** | System.Net.NetworkInformation | Station hardware binding: identifying local IP address (`ip_address`), MAC address, and verifying against scheduled lab room station allocations. |
| **06** | **Offline Local Storage** | **`LocalStorage`** | SQLite / Local File System | Local resilience buffer: encrypts audio recordings and partial submissions on local disk before network synchronization, preventing data loss during network hiccups. |
| **07** | **Network & API Client** | **`NetworkClient`** | HttpClient & Polly | Communication with Backend API (`POST /api/v1/official-exams/submissions/persist-first`), Cloudflare R2 presigned URL upload, and real-time SignalR anti-cheat event reporting. |
