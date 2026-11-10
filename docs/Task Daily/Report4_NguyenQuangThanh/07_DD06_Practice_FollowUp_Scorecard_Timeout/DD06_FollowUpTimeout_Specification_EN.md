# REPORT 4 — SOFTWARE DESIGN DOCUMENT (DETAILED DESIGN)
## FEATURE PACKAGE DD-06: CONTEXTUAL FOLLOW-UP, SCORECARD, HISTORY & TIMEOUT (MF-01)

> **Document Information:**
> - **Project:** LLM-Based Oral Examination System for Software Engineering (`FA26SE166`)
> - **Lead Architect:** Nguyen Quang Thanh | **Cross-Reviewer:** Pham Nguyen Dang Hai
> - **Scope:** `DD-06`: Contextual Follow-Up, Scorecard Generation, History & Inactivity Timeout (`MF-01`)
> - **Diagram Files (Draw.io Vector):**
>   - `DD-06 Sequence Diagrams`: [`DD06_01_Contextual_FollowUp_QA_Sequence.drawio`](./DD06_01_Contextual_FollowUp_QA_Sequence.drawio) | [`DD06_02_Session_Timeout_Scorecard_Sequence.drawio`](./DD06_02_Session_Timeout_Scorecard_Sequence.drawio) | [`DD06_FollowUpTimeout_Sequence_Diagram.drawio`](./DD06_FollowUpTimeout_Sequence_Diagram.drawio)
>   - `DD-06 Class Diagram`: [`DD06_FollowUpTimeout_Class_Diagram.drawio`](./DD06_FollowUpTimeout_Class_Diagram.drawio)

---

## 3.6. Detailed Design Package `DD-06`: Contextual Follow-Up, Scorecard & Session Lifecycle (`MF-01`)

### 3.6.1. Feature Overview & Architecture (`DD-06 Overview`)

Feature package `DD-06` governs adaptive follow-up questioning, session conclusion, and inactivity handling:

1. **Contextual Follow-up on Boundary Scores (4.0 – 8.0):**
   - In On-Demand practice mode, if an answer receives a score within the ambiguous range ($4.0 \le 	ext{Score} \le 8.0$), the system triggers an adaptive follow-up question ($Q_{\text{A2}}$).
   - Gemini dynamically generates a follow-up question probing specific gaps identified in the candidate's initial answer ($Q_{\text{A1}}$).
   - If the candidate performs well on the follow-up, the final effective score is adjusted upwards by a bonus formula ($\Delta \le +1.5$ pts, capped at 10.0).

2. **Inactivity Session Auto-Termination (5 Minutes):**
   - If no candidate activity is recorded for 5 consecutive minutes (`last_activity_at`), the background `SessionInactivityTimeoutWorker` marks the session `COMPLETED`. Any subsequent request returns **`HTTP 410 Gone`**.

3. **Comprehensive CLO Scorecard Generation:**
   - Aggregates all answer evaluations into an overall score and broken down performance metrics mapped directly to Course Learning Outcomes (CLOs).

---

### 3.6.2. Sequence Diagrams (`DD-06 Sequence Diagrams`)

1. **Contextual Follow-Up QA (`DD06_01`):**
   - **Vector Source File:** [`DD06_01_Contextual_FollowUp_QA_Sequence.drawio`](./DD06_01_Contextual_FollowUp_QA_Sequence.drawio)
   - Evaluates boundary scores and generates adaptive follow-up question probing conceptual gaps.

2. **Session Timeout & Scorecard (`DD06_02`):**
   - **Vector Source File:** [`DD06_02_Session_Timeout_Scorecard_Sequence.drawio`](./DD06_02_Session_Timeout_Scorecard_Sequence.drawio)
   - Handles the 5-minute inactivity termination and synthesizes CLO scorecard.

- **Consolidated Overview Diagram:** [`DD06_FollowUpTimeout_Sequence_Diagram.drawio`](./DD06_FollowUpTimeout_Sequence_Diagram.drawio)

---

### 3.6.3. Step-by-Step Interaction Narrative (`Table 3.6.1`)

| Step | Sender | Receiver | Message / Operation | Detailed Description & Technical Constraints |
|:---:|:---|:---|:---|:---|
| **01** | `: GradingWorker` | `: FollowUpEvaluator` | Evaluate Primary Score | Inspects primary answer score against boundary criteria ($4.0 \le 	ext{Score} \le 8.0$). |
| **02** | `: FollowUpEvaluator` | `: GeminiClient` | Generate Follow-Up Question | Prompts Gemini to synthesize targeted follow-up based on gaps in $A_1$. |
| **03** | `: FollowUpEvaluator` | `: PracticeHub` | Dispatch Follow-Up Event | Emits `ReceiveFollowUpQuestion` event to student UI. |
| **04** | `: Student` | `: Practice UI` | Submit Follow-Up Answer | Submits response to follow-up prompt. |
| **05** | `: GradingWorker` | `: GeminiClient` | Evaluate Follow-Up Answer | Grades follow-up answer according to adaptive rubric. |
| **06** | `: FollowUpEvaluator` | `: ScoreCalculator` | Calculate Effective Score | Adjusts final question score with bonus factor ($\le +1.5$ pts). |
| **07** | `: Student` | `: Practice UI` | Conclude Practice Session | Clicks "Finish Practice" to trigger session completion. |
| **08** | `: Practice UI` | `: PracticeController` | `POST .../complete` | Submits session completion request. |
| **09** | `: CompleteHandler` | `: ScorecardGenerator` | Synthesize Scorecard | Aggregates individual question scores and calculates CLO proficiencies. |
| **10** | `: ScorecardGenerator`| `: OralExamDbContext` | Save `practice_sessions` | Sets session status to `COMPLETED` and saves final scorecard. |
| **11** | `: CompleteHandler` | `: Practice UI` | Return `ScorecardResponse` | Delivers comprehensive performance report to student portal. |

---

### 3.6.4. Class Diagram (`DD-06 Class Diagram`)

> **Draw.io Vector Source:** [`DD06_FollowUpTimeout_Class_Diagram.drawio`](./DD06_FollowUpTimeout_Class_Diagram.drawio)

*Figure 3.6.1 — Contextual Follow-Up, Scorecard & Session Timeout Class Diagram (`DD-06`).*

---

### 3.6.5. Class Specifications (`DD-06 Class Specifications`)

#### A. Architectural Class Summary Table

| Class Name | Architectural Layer | Package | Single Responsibility |
|:---|:---|:---|:---|
| **`FollowUpService`** | Application | `Features/Practice` | Detects boundary scores and orchestrates Gemini follow-up question synthesis. |
| **`ScorecardGenerator`** | Application | `Features/Practice` | Computes weighted scores and generates CLO proficiency radar data. |
| **`CompletePracticeSessionCommandHandler`** | Application | `Features/Practice` | Finalizes practice sessions, marks completion state, and triggers scorecard synthesis. |
| **`InactivityTimeoutWorker`** | API | `Workers` | Background hosted service terminating abandoned sessions exceeding 5 minutes idle time. |

#### B. Method-Level Class Specifications

| Class | Method / Signature | Parameters | Return Type | Behavior Specification & Business Rules |
|:---|:---|:---|:---|:---|
| **`FollowUpService`** | `ShouldTriggerFollowUp(score, mode)` | `decimal, string` | `bool` | Returns true if mode is On-Demand and $4.0 \le 	ext{score} \le 8.0$. |
| **`FollowUpService`** | `GenerateFollowUpAsync(q, a, gaps)` | `Question, Answer, string` | `Task<FollowUpDto>` | Prompts Gemini to synthesize follow-up question probing specific gaps. |
| **`ScorecardGenerator`** | `Generate(sessionId)` | `UUID` | `Task<PracticeScorecardDto>` | Aggregates all answer scores, calculates averages, and maps to CLOs. |
| **`InactivityTimeoutWorker`**| `CheckAndTerminateExpiredSessions(ct)`| `CancellationToken` | `Task` | Queries sessions with `UtcNow - last_activity_at > 5m` and updates to `COMPLETED`. |

---

### 3.6.6. Exception Handling & Edge Case Matrix (`Table 3.6.2`)

| Scenario ID | Edge Case / Exception Name | Trigger Condition | System Detection & Handling Mechanism | HTTP Status & Response Payload |
|:---:|:---|:---|:---|:---|
| **SC-01** | Interaction on Expired Session | Candidate attempts action on session idle for > 5 minutes. | Controller checks session status. Returns 410 if already closed. | `HTTP 410 Gone`<br>`{"error": "Session closed due to inactivity"}` |
| **SC-02** | Follow-Up Trigger Exceeded | System already asked configured maximum follow-up questions (Admin cap: 2). | Service tracks follow-up count; terminates follow-up sequence when limit reached. | `Follow-Up Sequence Terminates` |
| **SC-03** | Partial Submission at Timeout | Session terminates while questions remain partially answered. | Answered questions are scored; unanswered items receive zero points. | State Preserved |
| **SC-04** | Invalid Scorecard Query | Student queries scorecard for a session that has not concluded. | Handler checks status; requires session completion before returning full scorecard. | `HTTP 400 Bad Request`<br>`{"error": "Session must be completed to view scorecard"}` |
| **SC-05** | Unauthorized Session Conclusion| User attempts to conclude a session belonging to another account. | Handler verifies session ownership against user claims. | `HTTP 403 Forbidden`<br>`{"error": "Access to foreign session denied"}` |

---

### 3.6.7. Data Contracts & DTO Specifications (`Table 3.6.3`)

| Field Name | Data Type | Required | Validation Rules | Business Meaning & Concrete Example |
|:---|:---|:---:|:---|:---|
| **`sessionId`** | UUID | Yes | Valid GUID | Session identifier to be concluded. |
| **`overallScore`** | Decimal | Output | 0.00 – 10.00 | Overall session performance score. |
| **`cloScores`** | Dictionary<String, Decimal> | Output | Key-value pairs | Proficiencies mapped per Course Learning Outcome (`CLO1`: 8.5). |
| **`completedAt`** | DateTime | Output | Valid UTC timestamp | Precise timestamp when session was marked complete. |
| **`totalQuestionsAnswered`**| Integer | Output | $\ge 0$ | Count of evaluated questions in the session. |
