# REPORT 4 — SOFTWARE DESIGN DOCUMENT (DETAILED DESIGN)
## FEATURE PACKAGE DD-05: PRACTICE ANSWER SUBMISSION, ASYNCHRONOUS AI GRADING & SIGNALR (MF-01)

> **Document Information:**
> - **Project:** LLM-Based Oral Examination System for Software Engineering (`FA26SE166`)
> - **Lead Architect:** Nguyen Quang Thanh | **Cross-Reviewer:** Pham Nguyen Dang Hai
> - **Scope:** `DD-05`: Answer Submission, Background AI Grading & Real-time SignalR (`MF-01`)
> - **Diagram Files (Draw.io Vector):**
>   - `DD-05 Sequence Diagrams`: [`DD05_01_Answer_Submission_PersistFirst_Sequence.drawio`](./DD05_01_Answer_Submission_PersistFirst_Sequence.drawio) | [`DD05_02_Background_AI_Grading_SignalR_Sequence.drawio`](./DD05_02_Background_AI_Grading_SignalR_Sequence.drawio) | [`DD05_PracticeGrading_Sequence_Diagram.drawio`](./DD05_PracticeGrading_Sequence_Diagram.drawio)
>   - `DD-05 Class Diagram`: [`DD05_PracticeGrading_Class_Diagram.drawio`](./DD05_PracticeGrading_Class_Diagram.drawio)

---

## 3.5. Detailed Design Package `DD-05`: Answer Submission, Background AI Grading & SignalR (`MF-01`)

### 3.5.1. Feature Overview & Architecture (`DD-05 Overview`)

Feature package `DD-05` delivers high-throughput asynchronous answer grading:

1. **Persist-First Submission Pipeline (< 100ms):**
   - When a student submits an answer (`POST /api/v1/practice/sessions/{id}/submit-answer`), the API validates input, persists the record into `practice_answers` with status `SUBMITTED`, and enqueues a `GradingTask` into an in-memory `BoundedChannel` (1,000 slots capacity, `FullMode.Wait`).
   - The API immediately responds with `HTTP 202 Accepted` in **$< 100	ext{ ms}$**, eliminating client freezing.

2. **Parallel Background Grading Worker (`GradingQueueWorker`):**
   - The background hosted service processes tasks with `MaxDegreeOfParallelism = 12`. It queries the question, rubric, and sample answer, then invokes **Google Gemini 1.5 Flash** using structured Chain-of-Thought (CoT) prompting.
   - **Resilience Policy:** Wrapped in a Polly retry policy (3 attempts with exponential backoff: 2s, 4s, 8s). Persistent failures are isolated into `dead_letter_queues` (DLQ) with a 5-minute cooldown.

3. **Real-time Event Dispatching via SignalR:**
   - Once graded, the worker updates `practice_answers` to `GRADED` and broadcasts the evaluation result (score, criterion breakdowns, feedback) directly to the student via `PracticeHub` (`/hubs/practice`).

---

### 3.5.2. Sequence Diagrams (`DD-05 Sequence Diagrams`)

1. **Persist-First Submission (`DD05_01`):**
   - **Vector Source File:** [`DD05_01_Answer_Submission_PersistFirst_Sequence.drawio`](./DD05_01_Answer_Submission_PersistFirst_Sequence.drawio)
   - Persists answer to PostgreSQL and enqueues into `BoundedChannel` in $< 100	ext{ms}$.

2. **Background AI Grading & SignalR (`DD05_02`):**
   - **Vector Source File:** [`DD05_02_Background_AI_Grading_SignalR_Sequence.drawio`](./DD05_02_Background_AI_Grading_SignalR_Sequence.drawio)
   - Asynchronously consumes tasks, invokes Gemini CoT, and pushes evaluations via SignalR.

- **Consolidated Overview Diagram:** [`DD05_PracticeGrading_Sequence_Diagram.drawio`](./DD05_PracticeGrading_Sequence_Diagram.drawio)

---

### 3.5.3. Step-by-Step Interaction Narrative (`Table 3.5.1`)

| Step | Sender | Receiver | Message / Operation | Detailed Description & Technical Constraints |
|:---:|:---|:---|:---|:---|
| **01** | `: Student` | `: Practice UI` | Click "Submit Answer" | Triggers answer submission with finalized transcript. |
| **02** | `: Practice UI` | `: PracticeController` | `POST .../submit-answer` | Submits JSON payload containing `questionId` and `answerText`. |
| **03** | `: PracticeController` | `: IMediator` | `Send(SubmitAnswerCommand)` | Forwards command to MediatR pipeline. |
| **04** | `: SubmitHandler` | `: OralExamDbContext` | Save `practice_answers` | Persists answer record in `SUBMITTED` state ($< 100	ext{ms}$). |
| **05** | `: SubmitHandler` | `: GradingQueueChannel` | `WriteAsync(gradingTask)` | Enqueues task into 1,000-slot RAM channel. |
| **06** | `: PracticeController` | `: Practice UI` | HTTP 202 Accepted | Returns acknowledgment with `answerId` and status `QUEUED`. |
| **07** | `: GradingQueueWorker` | `: GradingQueueChannel` | `ReadAsync()` | Worker pulls pending task from queue (parallel x12). |
| **08** | `: GradingQueueWorker` | `: OralExamDbContext` | Query Question & Rubric | Fetches rubric criteria (totaling 10.0 pts) and sample answer. |
| **09** | `: GradingQueueWorker` | `: GeminiClient` | Invoke CoT Evaluation | Calls Gemini 1.5 Flash with structured Rubric and prompt. |
| **10** | `: GeminiClient` | `: GradingQueueWorker` | Return JSON Evaluation | Returns criterion scores, deductions, and constructive feedback. |
| **11** | `: GradingQueueWorker` | `: OralExamDbContext` | Update `practice_answers` | Persists evaluation, marks answer status `GRADED`. |
| **12** | `: GradingQueueWorker` | `: PracticeHub` | `SendAsync("ReceiveGradingResult")` | Pushes real-time evaluation payload to student client. |
| **13** | `: PracticeHub` | `: Practice UI` | SignalR Event Received | Frontend updates scorecard and displays feedback. |

---

### 3.5.4. Class Diagram (`DD-05 Class Diagram`)

> **Draw.io Vector Source:** [`DD05_PracticeGrading_Class_Diagram.drawio`](./DD05_PracticeGrading_Class_Diagram.drawio)

*Figure 3.5.1 — Answer Submission, Background Grading Queue & SignalR Class Diagram (`DD-05`).*

---

### 3.5.5. Class Specifications (`DD-05 Class Specifications`)

#### A. Architectural Class Summary Table

| Class Name | Architectural Layer | Package | Single Responsibility |
|:---|:---|:---|:---|
| **`SubmitAnswerCommandHandler`** | Application | `Features/Practice` | Persists answer record in $< 100	ext{ms}$ and writes task to in-memory queue. |
| **`GradingQueueChannel`** | Infrastructure | `Channels` | Singleton wrapper for `BoundedChannel<GradingTask>` with 1,000-slot capacity. |
| **`GradingQueueWorker`** | API | `Workers` | Hosted `BackgroundService` executing parallel AI evaluations (`MaxDegreeOfParallelism = 12`). |
| **`GeminiAiGradingService`** | Infrastructure | `Services` | Client wrapper for Google Gemini 1.5 Flash CoT grading API. |
| **`PracticeHub`** | API | `Hubs` | Strongly-typed SignalR Hub broadcasting real-time grading events to connected clients. |

#### B. Method-Level Class Specifications

| Class | Method / Signature | Parameters | Return Type | Behavior Specification & Business Rules |
|:---|:---|:---|:---|:---|
| **`SubmitAnswerCommandHandler`** | `Handle(cmd, ct)` | `SubmitAnswerCommand`, `CancellationToken` | `Task<SubmitAnswerResponse>` | Saves record to DB, enqueues to channel, returns HTTP 202 in $< 100	ext{ms}$. |
| **`GradingQueueChannel`** | `EnqueueAsync(task, ct)` | `GradingTask`, `CancellationToken` | `ValueTask` | Writes task to channel using `FullMode.Wait` backpressure strategy. |
| **`GradingQueueWorker`** | `ExecuteAsync(ct)` | `CancellationToken` | `Task` | Continuous consumption loop pulling tasks and invoking AI evaluator. |
| **`GeminiAiGradingService`** | `GradeAnswerAsync(context, ct)` | `GradingContext`, `CancellationToken` | `Task<AiEvaluationResult>` | Formats prompt, enforces Rubric 10.0 sum, parses structured JSON response. |

---

### 3.5.6. Exception Handling & Edge Case Matrix (`Table 3.5.2`)

| Scenario ID | Edge Case / Exception Name | Trigger Condition | System Detection & Handling Mechanism | HTTP Status & Response Payload |
|:---:|:---|:---|:---|:---|
| **SC-01** | Gemini API Rate Limit | Gemini API returns HTTP 429 Too Many Requests. | Polly policy catches 429 and retries with exponential backoff (2s, 4s, 8s). | Internal Retry |
| **SC-02** | Persistent AI Failure | All 3 Polly retry attempts exhausted due to outage. | Worker isolates task to `dead_letter_queues` table with 5m cooldown. | `DLQ Isolation` |
| **SC-03** | In-Memory Channel Saturated | 1,000 tasks pending in queue during severe load. | Channel applies `FullMode.Wait`, backpressuring the submission thread. | Graceful Wait |
| **SC-04** | SignalR Disconnection | Client loses WebSocket connection before grading finishes. | System relies on persistent database record. Client queries state on reconnect. | Fallback Query |
| **SC-05** | Malformed Answer Payload | Answer transcript is null or exceeds 5,000 characters. | FluentValidation rejects payload before database access. | `HTTP 422 Unprocessable Entity`<br>`{"error": "Answer text exceeds allowed length"}` |

---

### 3.5.7. Data Contracts & DTO Specifications (`Table 3.5.3`)

| Field Name | Data Type | Required | Validation Rules | Business Meaning & Concrete Example |
|:---|:---|:---:|:---|:---|
| **`sessionId`** | UUID | Yes | Valid GUID | Active practice session identifier. |
| **`questionId`** | UUID | Yes | Valid GUID | Question being answered. |
| **`answerText`** | String | Yes | 1–5,000 characters | Finalized answer text submitted by candidate. |
| **`score`** | Decimal | Output | 0.00 – 10.00 | Total score assigned by AI evaluation. |
| **`feedback`** | String | Output | Non-empty | Constructive qualitative feedback generated by AI. |
| **`status`** | String | Output | `SUBMITTED`, `GRADING`, `GRADED` | Current processing status of the submission. |
