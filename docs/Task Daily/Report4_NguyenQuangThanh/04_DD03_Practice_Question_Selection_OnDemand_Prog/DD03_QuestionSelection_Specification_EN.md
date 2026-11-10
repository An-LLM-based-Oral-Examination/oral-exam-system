# REPORT 4 — SOFTWARE DESIGN DOCUMENT (DETAILED DESIGN)
## FEATURE PACKAGE DD-03: PRACTICE QUESTION SELECTION & INACTIVITY TIMEOUT (MF-01)

> **Document Information:**
> - **Project:** LLM-Based Oral Examination System for Software Engineering (`FA26SE166`)
> - **Lead Architect:** Nguyen Quang Thanh | **Cross-Reviewer:** Pham Nguyen Dang Hai
> - **Scope:** `DD-03`: Practice Mode & Question Selection: On-Demand & Full Progressive (`MF-01`)
> - **Diagram Files (Draw.io Vector):**
>   - `DD-03 Sequence Diagrams`: [`DD03_01_OnDemand_Question_Selection_Sequence.drawio`](./DD03_01_OnDemand_Question_Selection_Sequence.drawio) | [`DD03_02_FullSession_Progressive_Selection_Sequence.drawio`](./DD03_02_FullSession_Progressive_Selection_Sequence.drawio) | [`DD03_QuestionSelection_Sequence_Diagram.drawio`](./DD03_QuestionSelection_Sequence_Diagram.drawio)
>   - `DD-03 Class Diagram`: [`DD03_QuestionSelection_Class_Diagram.drawio`](./DD03_QuestionSelection_Class_Diagram.drawio)

---

## 3.3. Detailed Design Package `DD-03`: Practice Mode & Question Selection Algorithms (`MF-01`)

### 3.3.1. Feature Overview & Architecture (`DD-03 Overview`)

Feature package `DD-03` governs question retrieval from the isolated practice question repository (`practice_questions`):

1. **On-Demand Per-Question Selection (`POST .../next-question`):**
   - Questions are fetched one-by-one as the student progresses.
   - **Anti-3-Consecutive Difficulty Guard:** The system monitors the difficulty of the preceding two questions. If both share the same level, the third question MUST switch difficulty. If questions of that level are exhausted, the selector falls back to adjacent levels.

2. **Full-Session Progressive Selection:**
   - Pre-allocates 3–10 questions partitioned across Easy $	o$ Medium $	o$ Hard tiers, presenting an escalating cognitive challenge.
   - Contextual follow-up is disabled in full-session mode to maintain predictable assessment duration.

3. **Inactivity Timeout Guard:**
   - Prior to question dispatch, the handler verifies `last_activity_at`. If idle time exceeds 5 minutes, the session is terminated (`HTTP 410 Gone`).

---

### 3.3.2. Sequence Diagrams (`DD-03 Sequence Diagrams`)

1. **On-Demand Selection (`DD03_01`):**
   - **Vector Source File:** [`DD03_01_OnDemand_Question_Selection_Sequence.drawio`](./DD03_01_OnDemand_Question_Selection_Sequence.drawio)
   - Dispatches single questions on-demand while enforcing the Anti-3-Consecutive constraint.

2. **Full-Session Progressive Selection (`DD03_02`):**
   - **Vector Source File:** [`DD03_02_FullSession_Progressive_Selection_Sequence.drawio`](./DD03_02_FullSession_Progressive_Selection_Sequence.drawio)
   - Pre-allocates a balanced suite of questions escalating from Easy to Hard.

- **Consolidated Overview Diagram:** [`DD03_QuestionSelection_Sequence_Diagram.drawio`](./DD03_QuestionSelection_Sequence_Diagram.drawio)

---

### 3.3.3. Step-by-Step Interaction Narrative (`Table 3.3.1`)

| Step | Sender | Receiver | Message / Operation | Detailed Description & Technical Constraints |
|:---:|:---|:---|:---|:---|
| **01** | `: Student` | `: Practice UI` | Request Next Question | Clicks "Next Question" or advances automatically. |
| **02** | `: Practice UI` | `: PracticeController` | `POST /api/v1/practice/sessions/{id}/next-question` | Sends request for subsequent question. |
| **03** | `: PracticeController` | `: IMediator` | `Send(GetNextQuestionCommand)` | Forwards command to MediatR pipeline. |
| **04** | `: QuestionHandler` | `: OralExamDbContext` | Query `practice_sessions` | Retrieves session state and checks `last_activity_at`. |
| **05** | `: QuestionHandler` | `: InactivityGuard` | Verify Inactivity (< 5m) | If elapsed time > 5m, marks session `COMPLETED` and returns 410. |
| **06** | `: QuestionHandler` | `: AntiConsecutiveGuard` | Check Difficulty History | Inspects last two answers to prevent 3 consecutive identical levels. |
| **07** | `: AntiConsecutiveGuard`| `: OralExamDbContext` | Query `practice_questions` | Fetches unassigned approved questions (`is_active = true`). |
| **08** | `: QuestionHandler` | `: OralExamDbContext` | Update `last_activity_at` | Refreshes timestamp to extend session validity. |
| **09** | `: QuestionHandler` | `: PracticeController` | Return `QuestionDto` | Returns question text, topic, and CLO mapping. |
| **10** | `: PracticeController` | `: Practice UI` | HTTP 200 OK | Displays question and starts timer. |

---

### 3.3.4. Class Diagram (`DD-03 Class Diagram`)

> **Draw.io Vector Source:** [`DD03_QuestionSelection_Class_Diagram.drawio`](./DD03_QuestionSelection_Class_Diagram.drawio)

*Figure 3.3.1 — Practice Question Selection & Inactivity Verification Class Diagram (`DD-03`).*

---

### 3.3.5. Class Specifications (`DD-03 Class Specifications`)

#### A. Architectural Class Summary Table

| Class Name | Architectural Layer | Package | Single Responsibility |
|:---|:---|:---|:---|
| **`GetNextPracticeQuestionCommandHandler`** | Application | `Features/Practice` | Orchestrates inactivity verification, difficulty balancing, and question retrieval. |
| **`AntiConsecutiveDifficultyGuard`** | Domain | `Services` | Enforces the invariant: at most 2 consecutive questions of identical difficulty. |
| **`PracticeQuestionSelector`** | Application | `Features/Practice` | Executes filtered database queries against `practice_questions` isolating used IDs. |

#### B. Method-Level Class Specifications

| Class | Method / Signature | Parameters | Return Type | Behavior Specification & Business Rules |
|:---|:---|:---|:---|:---|
| **`GetNextPracticeQuestionCommandHandler`** | `Handle(cmd, ct)` | `GetNextQuestionCommand`, `CancellationToken` | `Task<QuestionResponse>` | Checks 5m timeout, applies anti-consecutive rule, updates activity timestamp. |
| **`AntiConsecutiveDifficultyGuard`** | `GetAllowedDifficulties(history)` | `List<DifficultyLevel>` | `List<DifficultyLevel>` | Returns eligible difficulties excluding level if last two were identical. |
| **`PracticeQuestionSelector`** | `SelectQuestion(courseId, allowed, excludedIds)` | `UUID, List<Difficulty>, List<UUID>` | `Task<Question>` | Fetches random unassigned question matching allowed difficulty criteria. |

---

### 3.3.6. Exception Handling & Edge Case Matrix (`Table 3.3.2`)

| Scenario ID | Edge Case / Exception Name | Trigger Condition | System Detection & Handling Mechanism | HTTP Status & Response Payload |
|:---:|:---|:---|:---|:---|
| **SC-01** | Session Inactivity Timeout | Student idle time exceeds 5 minutes (`SessionInactivityTimeoutMinutes`). | Handler calculates `UtcNow - last_activity_at > 5m`. Marks session `COMPLETED`. | `HTTP 410 Gone`<br>`{"error": "Session expired due to 5 minutes of inactivity"}` |
| **SC-02** | Question Pool Exhaustion | No remaining unassigned questions in course bank. | Selector returns null. Handler flags `hasMoreQuestions = false`. | `HTTP 200 OK`<br>`{"hasMoreQuestions": false, "message": "Practice complete"}` |
| **SC-03** | Completed Session Access | Student requests question from already finished session. | Handler verifies `session.Status == "COMPLETED"`. | `HTTP 400 Bad Request`<br>`{"error": "Session is already completed"}` |
| **SC-04** | Difficulty Tier Exhaustion | Allowed difficulty pool exhausted under Anti-3-Consecutive constraint. | Guard falls back to alternate available difficulty tiers. | `HTTP 200 OK`<br>`(Dispatches question from fallback tier)` |
| **SC-05** | Unauthorized Session Access | Student attempts to retrieve questions from another user's session. | Handler verifies `session.UserId == currentUserId`. | `HTTP 403 Forbidden`<br>`{"error": "Access to foreign session denied"}` |

---

### 3.3.7. Data Contracts & DTO Specifications (`Table 3.3.3`)

| Field Name | Data Type | Required | Validation Rules | Business Meaning & Concrete Example |
|:---|:---|:---:|:---|:---|
| **`sessionId`** | UUID | Yes | Valid GUID | Identifier of active practice session. |
| **`questionId`** | UUID | Output | Valid GUID | Unique question identifier. |
| **`content`** | String | Output | Non-empty | Technical question prompt. |
| **`difficulty`** | String | Output | `EASY`, `MEDIUM`, `HARD` | Question difficulty rating. |
| **`hasMoreQuestions`** | Boolean | Output | True / False | Indicates if additional questions remain available. |
