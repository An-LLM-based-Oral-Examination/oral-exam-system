# REPORT 4 — SOFTWARE DESIGN DOCUMENT (DETAILED DESIGN)
## FEATURE PACKAGE DD-02: PRACTICE CONFIGURATION & SESSION INITIALIZATION (MF-01)

> **Document Information:**
> - **Project:** LLM-Based Oral Examination System for Software Engineering (`FA26SE166`)
> - **Lead Architect:** Nguyen Quang Thanh | **Cross-Reviewer:** Pham Nguyen Dang Hai
> - **Scope:** `DD-02`: Practice Configuration & Session Initialization (`MF-01`)
> - **Diagram Files (Draw.io Vector):**
>   - `DD-02 Sequence Diagram`: [`DD02_PracticeInit_Sequence_Diagram.drawio`](./DD02_PracticeInit_Sequence_Diagram.drawio)
>   - `DD-02 Class Diagram`: [`DD02_PracticeInit_Class_Diagram.drawio`](./DD02_PracticeInit_Class_Diagram.drawio)

---

## 3.2. Detailed Design Package `DD-02`: Practice Configuration & Session Initialization (`MF-01`)

### 3.2.1. Feature Overview & Architecture (`DD-02 Overview`)

Feature package `DD-02` initializes autonomous student oral practice sessions (`MF-01`) under administrative governance:

1. **System Parameter Ingestion (`system_configs`):**
   - The practice environment dynamically loads parameters configured by administrators from the `system_configs` table:
     * `FollowUpQuestionCount`: Number of contextual follow-up questions allowed (1–5, default 2).
     * `MinMixedPracticeQuestions` & `MaxMixedPracticeQuestions`: Question count bounds for progressive sessions (3–10).
     * `TranscriptBufferSeconds`: Review countdown duration for audio transcripts (10–300s, default 60s).
     * `SessionInactivityTimeoutMinutes`: Allowed idle duration before session auto-termination (5 minutes).

2. **Session Initialization (`POST /api/v1/practice/sessions`):**
   - Students select their course, preferred interaction mode (`On-Demand` per-question or `Full-Session` progressive), and difficulty level.
   - The backend validates enrollment, provisions a new record in `practice_sessions` with state `IN_PROGRESS`, and assigns the first question. Zero audio recordings are stored on Cloudflare R2 (`Zero-R2 Invariant`).

---

### 3.2.2. Sequence Diagram (`DD-02 Sequence Diagram`)

- **Vector Source File:** [`DD02_PracticeInit_Sequence_Diagram.drawio`](./DD02_PracticeInit_Sequence_Diagram.drawio)
- **Technical Description:** Student accesses the practice dashboard. Client requests practice parameters via `GET /api/v1/practice/config`. System returns active configuration. When the student initiates a session (`POST /api/v1/practice/sessions`), `CreatePracticeSessionCommandHandler` validates inputs, creates a `practice_sessions` row, selects initial questions, and returns session metadata.

---

### 3.2.3. Step-by-Step Interaction Narrative (`Table 3.2.1`)

| Step | Sender | Receiver | Message / Operation | Detailed Description & Technical Constraints |
|:---:|:---|:---|:---|:---|
| **01** | `: Student` | `: Practice UI` | Navigate Practice Hub | Student opens practice interface in Web SPA. |
| **02** | `: Practice UI` | `: PracticeController` | `GET /api/v1/practice/config` | Requests active practice configuration parameters. |
| **03** | `: PracticeController` | `: IMediator` | `Send(GetPracticeConfigQuery)` | Dispatches query through MediatR pipeline. |
| **04** | `: ConfigHandler` | `: OralExamDbContext` | Query `system_configs` | Fetches follow-up bounds, buffer timers, and timeout limits. |
| **05** | `: ConfigHandler` | `: PracticeController` | Return `PracticeConfigDto` | Delivers configuration payload. |
| **06** | `: Student` | `: Practice UI` | Select Course & Mode | Chooses course, mode (On-Demand / Full-Session), and difficulty. |
| **07** | `: Practice UI` | `: PracticeController` | `POST /api/v1/practice/sessions` | Sends session creation payload. |
| **08** | `: SessionHandler` | `: OralExamDbContext` | Validate & Insert `practice_sessions` | Persists session record with status `IN_PROGRESS`. |
| **09** | `: SessionHandler` | `: PracticeQuestionSelector` | Select Initial Question | Selects first question conforming to Anti-3-Consecutive rules. |
| **10** | `: PracticeController` | `: Practice UI` | Return `PracticeSessionResponse` | Returns session ID and initial question to student. |

---

### 3.2.4. Class Diagram (`DD-02 Class Diagram`)

> **Draw.io Vector Source:** [`DD02_PracticeInit_Class_Diagram.drawio`](./DD02_PracticeInit_Class_Diagram.drawio)

*Figure 3.2.1 — Practice Configuration & Session Initialization Class Diagram (`DD-02`).*

---

### 3.2.5. Class Specifications (`DD-02 Class Specifications`)

#### A. Architectural Class Summary Table

| Class Name | Architectural Layer | Package | Single Responsibility |
|:---|:---|:---|:---|
| **`PracticeController`** | API | `Controllers/v1` | Entry point for practice configuration and session initialization endpoints. |
| **`GetPracticeConfigQueryHandler`** | Application | `Features/Practice` | Queries and formats active practice parameters from `system_configs`. |
| **`CreatePracticeSessionCommandHandler`** | Application | `Features/Practice` | Validates session requests, creates practice sessions, and assigns initial questions. |
| **`SystemConfigService`** | Infrastructure | `Services` | Cached access layer for system configuration values in PostgreSQL. |

#### B. Method-Level Class Specifications

| Class | Method / Signature | Parameters | Return Type | Behavior Specification & Business Rules |
|:---|:---|:---|:---|:---|
| **`PracticeController`** | `GetConfig()` | None | `Task<IActionResult>` | Maps `GET /api/v1/practice/config`. Returns active practice parameters. |
| **`PracticeController`** | `CreateSession(cmd)` | `CreatePracticeSessionCommand` | `Task<IActionResult>` | Maps `POST /api/v1/practice/sessions`. Returns HTTP 201 Created. |
| **`GetPracticeConfigQueryHandler`** | `Handle(q, ct)` | `GetPracticeConfigQuery`, `CancellationToken` | `Task<PracticeConfigDto>` | Retrieves configuration rows, applies default fallbacks if missing. |
| **`CreatePracticeSessionCommandHandler`** | `Handle(cmd, ct)` | `CreatePracticeSessionCommand`, `CancellationToken` | `Task<PracticeSessionResponse>` | Enforces question bounds (3–10), verifies course enrollment, creates session. |

---

### 3.2.6. Exception Handling & Edge Case Matrix (`Table 3.2.2`)

| Scenario ID | Edge Case / Exception Name | Trigger Condition | System Detection & Handling Mechanism | HTTP Status & Response Payload |
|:---:|:---|:---|:---|:---|
| **SC-01** | Missing Course ID | Student initiates session with invalid or non-existent course. | Validation behavior detects empty GUID or database query returns null. | `HTTP 400 Bad Request`<br>`{"error": "Invalid course identifier"}` |
| **SC-02** | Insufficient Questions in Bank | Practice question bank has fewer questions than requested for Full-Session. | Selector checks pool size against requested count. | `HTTP 400 Bad Request`<br>`{"error": "Insufficient approved practice questions for course"}` |
| **SC-03** | Invalid Question Count Bounds | Student requests progressive session with < 3 or > 10 questions. | FluentValidation rejects out-of-range integer value. | `HTTP 422 Unprocessable Entity`<br>`{"error": "Question count must be between 3 and 10"}` |
| **SC-04** | Inactive Course | Course exists but `is_active == false`. | Handler checks course status before provisioning session. | `HTTP 400 Bad Request`<br>`{"error": "Course is currently inactive"}` |
| **SC-05** | Unauthorized Session Creation | User role is not `student`. | Authorization guard verifies user role claim. | `HTTP 403 Forbidden`<br>`{"error": "Only students may create practice sessions"}` |

---

### 3.2.7. Data Contracts & DTO Specifications (`Table 3.2.3`)

| Field Name | Data Type | Required | Validation Rules | Business Meaning & Concrete Example |
|:---|:---|:---:|:---|:---|
| **`courseId`** | UUID | Yes | Valid GUID | Identifier of the course selected for practice. |
| **`mode`** | String | Yes | `ON_DEMAND` or `FULL_PROGRESSIVE` | Practice interaction mode. |
| **`questionCount`** | Integer | Optional | 3–10 (required if mode is `FULL_PROGRESSIVE`) | Total number of questions for progressive session. |
| **`selectedDifficulties`** | List<String> | Optional | Valid levels (`EASY`, `MEDIUM`, `HARD`) | Target difficulty filter for on-demand mode. |
| **`sessionId`** | UUID | Output | Valid GUID | Unique session identifier generated for the practice session. |
| **`initialQuestion`** | Object | Output | Question DTO | Payload of the first assigned question. |
