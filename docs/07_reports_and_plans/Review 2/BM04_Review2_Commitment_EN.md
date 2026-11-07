# SWP490 · CAPSTONE PROJECT · REVIEW 2 · BM04
## Review 2 Commitment (English Submission Version)

### 1. Project Information
| Field | Value |
| :--- | :--- |
| **Project code** | FA26SE166 |
| **Project name** | An LLM-based Oral Examination Practice and Assessment System for Software Engineering Major |
| **Team** | Nguyễn Quang Thành, Nguyễn Trọng Tốt, Phạm Nguyễn Đăng Hải, Lê Vũ Hoàng |
| **Supervisor** | Dr. Nguyễn Thị Cẩm Hương (TS. Nguyễn Thị Cẩm Hương) |
| **Review 2 date** | |
| **Reviewer 1** | |
| **Reviewer 2** | |
| **Version** | 1.0 |
| **Date** | 28/10/2026 |

### 2. Final Registration Form
| Field | Value |
| :--- | :--- |
| **Final version** | 2.0 |
| **Approved at Review 2 (Yes/No)** | Yes |
| **Attached file name** | SWP490_FA26SE166_R2_Registration.pdf |

### 3. Mandatory Features
| ID | Feature | Actor | Acceptance criteria | Workflow | Owner |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **F-01** | Authentication, 6-role RBAC, and academic class hierarchy | All Users, Admin, Department Head, Lecturer, Student | Only university-provisioned accounts can sign in and access features matching their 6 roles; supports the 4-tier academic hierarchy (Admin creates semester/course & assigns Department Head -> Department Head assigns Lecturers -> Lecturer creates class section & class password + in-class shift configuring shift name, 2 anti-collision question pool auto-draw modes `ALL_COURSE_BANK`/`SELECTED_POOL`, total questions 1–10 per student, Follow-up toggle 1–5 questions, 5-minute rotating OTP, x12 parallel grading & real-time parallel grade publishing -> Student self-joins). | WF-01 | Nguyễn Quang Thành |
| **F-02** | Practice parameter and mock exam quota configuration | Admin, Department Head | Admin configures practice session parameters and daily mock exam limits; Department Head configures question structure and duration for subject mock exams. | WF-02 | Phạm Nguyễn Đăng Hải |
| **F-03** | Interactive oral practice and technical term editing | Student | Student starts a practice session with approved questions, records spoken answers, and edits technical terms on the transcript before submitting; idle sessions exceeding the timeout auto-close. | WF-01 | Nguyễn Quang Thành |
| **F-04** | Practice grading, follow-up questions, and history | Student | System returns Rubric-based grading results, generates contextual follow-up questions (used within the session only, not saved to the question bank) when clarification is needed, and saves practice history. | WF-01 | Nguyễn Quang Thành |
| **F-05** | Quota-controlled timed mock exam | Student | System rejects new attempts if the student has exhausted the daily quota; main questions are drawn from the approved practice pool, require recording spoken answers via microphone before reviewing the transcript, and auto-submit when time expires. | WF-02 | Phạm Nguyễn Đăng Hải |
| **F-06** | Mock exam scorecard by Course Learning Outcome (CLO) | Student | After submission and completion of automated grading, the system displays a summary scorecard showing achievement by each Course Learning Outcome (CLO). | WF-02 | Phạm Nguyễn Đăng Hải |
| **F-07** | Two-flow oral question authoring and Rubric design | Lecturer, Department Head | Lecturer authors practice/mock questions independently by course CLO (`practice_questions`) or authors official exam questions strictly following the Department Head's sample exam / structure matrix (`exam_questions`); saving requires a valid model answer and Rubric criteria weights summing to 10.0 points. | WF-03 | Nguyễn Trọng Tốt |
| **F-08** | Question review submission and bank approval | Lecturer, Department Head | Questions created by Lecturers must be submitted for review; only questions approved by the Department Head (`APPROVED`) are published into the practice pool (`practice_questions`) or official exam pool (`exam_questions`). | WF-03 | Nguyễn Trọng Tốt |
| **F-09** | Official exam session, lab shift setup, and check-in | Department Head, Examination Officer, Proctor | Department Head configures exam Follow-up rules (1–5 questions, default 2); Examination Officer creates exam sessions, lab shifts, assigns Proctors/Examiners, imports rosters, allocates non-duplicate exam sets, and publishes the shift exam (`is_exam_published = true`); Proctor only checks in students by seat number (1–40, verifying Student ID/CCCD). | WF-04 | Lê Vũ Hoàng |
| **F-10** | Official exam on fullscreen-locked kiosk interface | Student, Proctor | Student enters the exam only after Proctor check-in, exam publication by Examination Officer, and passing the microphone check; the kiosk locks fullscreen, auto-suspends and alerts the Proctor to log violations into `exam_anti_cheat_logs` if fullscreen exit/blur exceeds 3 times, and seals the audio file with a SHA-256 hash upon submission. | WF-04 | Lê Vũ Hoàng |
| **F-11** | Post-exam grade verification and shift grade publication | Lecturer, Examination Officer | Lecturer listens to recorded audio, reviews AI feedback, and adjusts scores with an `override_reason` if needed; after 100% of shift submissions are verified, Lecturer / Examination Officer publishes grades once (`publish-grades`), activating the One-Way Lock (`is_locked = true`). | WF-04 | Lê Vũ Hoàng |
| **F-12** | Grade acknowledgement and 5-step internal appeal handling | Student, Examination Officer, Department Head, Lecturer | Student acknowledges published grades or submits an appeal (`AppealRequest`) to the Examination Officer (with both Examination Officer and Department Head receiving notifications) -> Examination Officer validates the appeal -> Examination Officer assigns an independent Lecturer (up to 24 submissions/Lecturer; evenly divided if total > Lecturers * 24, or load-balanced with manual adjustment if <= Lecturers * 24) -> Lecturer regrades on the Evidence Panel -> Examination Officer approves/rejects the final decision (opening a One-Way Lock exception to update the official score if approved). | WF-04 | Lê Vũ Hoàng |

### 4. Non-Functional Requirements
| ID | Quality attribute | Metric | Threshold | Measurement method | Owner |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **NFR-01** | Submission recording time | Time for the server to persist a student's submission status before queuing background grading | <= 1 second for students submitting within the same lab exam shift | Run concurrent load tests on the lab exam submission API | Nguyễn Quang Thành |
| **NFR-02** | Query response time | Response time of APIs fetching lists, configurations, and graded results | <= 2 seconds under normal operating conditions | Measure response times via the automated API integration test suite | Phạm Nguyễn Đăng Hải |
| **NFR-03** | Grading reliability | Rate of submissions preserved and routed to the retry queue when the AI service fails transiently | No lost submissions; automatic retry up to 3 times before routing to DLQ (Admin replays practice/mock DLQ; Examination Officer replays official exam DLQ) | Simulate AI service disconnection and verify records and role-based replay in the Dead-Letter Queue (DLQ) | Nguyễn Trọng Tốt |
| **NFR-04** | Grade integrity | Rate of blocking direct grade update or deletion attempts after shift grades are published | All direct grade edit or delete requests outside the appeal flow are rejected (HTTP 403) | Run authorization and update-blocking tests on locked grade records | Nguyễn Trọng Tốt |
| **NFR-05** | Evidence and kiosk security | Audio file hash match rate and automatic exam suspension upon screen violation | Stored audio files match their SHA-256 hash; immediate auto-suspension when fullscreen exit/blur > 3 times | Verify audio file hash matching and test the violation counter on the kiosk interface | Lê Vũ Hoàng |

### 5. Out of Scope
| Item | Reason |
| :--- | :--- |
| **Audio file storage for Practice and Timed Mock Exam modes** | These two modes are designed for daily self-study and do not require archiving legal audio evidence like official exams. |
| **Live Video Call integration and webcam proctoring** | Official exams are conducted on-site in university computer labs under the direct supervision of Proctors. |
| **Public self-registration for user accounts** | The platform is for internal university use only; only accounts provisioned by the university are permitted to sign in. |
| **Instant grade display or on-the-spot appeals at lab exam kiosks** | Official exam grades require post-exam lecturer verification across the entire shift before being published on the Student Portal. |

### 6. Workflow Commitments

#### 6.1 Workflow WF-01
| Field | Value |
| :--- | :--- |
| **Workflow name** | Interactive oral practice |
| **Owner (one member)** | Nguyễn Quang Thành |
| **Supporting members** | Lê Vũ Hoàng, Nguyễn Trọng Tốt |
| **Actors** | Admin, Student |
| **Trigger** | Student selects a subject and starts a practice session |
| **Business outcome** | Spoken answers to approved questions are transcribed, reviewed for technical terms, graded by Rubric with follow-up questions, and saved to history |
| **Main entity state path** | PracticeSession: in_progress -> completed (1 transition) |
| **Activity diagram (Report 4 section)** | Report 4 (SDD) – Activity Diagram WF-01 (Interactive Oral Practice) |

**Features in this workflow**
| Feature ID | Feature | Acceptance criteria | Test case IDs |
| :---: | :--- | :--- | :--- |
| **F-01** | Authentication, 6-role RBAC, and academic class hierarchy | Only university-provisioned accounts can sign in and access features matching their 6 roles; supports the 4-tier academic hierarchy (Admin creates semester/course & assigns Department Head -> Department Head assigns Lecturers -> Lecturer creates class section & class password + in-class shift with 5-minute rotating OTP -> Student self-joins). | TC-F01-01..TC-F01-03 |
| **F-03** | Interactive oral practice and technical term editing | Student starts a practice session with approved questions, records spoken answers, and edits technical terms on the transcript before submitting; idle sessions exceeding the timeout auto-close. | TC-F03-01..TC-F03-03 |
| **F-04** | Practice grading, follow-up questions, and history | System returns Rubric-based grading results, generates contextual follow-up questions (used within the session only, not saved to the question bank) when clarification is needed, and saves practice history. | TC-F04-01..TC-F04-03 |

**Demo scenarios**
| Scenario ID | Type | Preconditions and data | Main steps | Expected result | Presenter |
| :---: | :---: | :--- | :--- | :--- | :--- |
| **DS-01** (DS-01a) | Main | Student account; subject has approved questions in the practice pool | Select subject & start practice -> Record answer & edit technical terms on transcript -> Receive grading result, answer follow-up question, and finish session | System displays detailed criteria scorecard and saves the practice session as completed | Nguyễn Quang Thành |
| **DS-02** (DS-01b) | Exception | Practice session is open but left idle by the student beyond the inactivity timeout | Student returns to the timed-out session and clicks to submit an answer or fetch the next question | System auto-closes the session, preserves scores of previously completed questions, and rejects new actions | Nguyễn Quang Thành |

**Committed test cases**
| Test type | Count | Automated in CI | Responsible member(s) |
| :--- | :---: | :---: | :--- |
| Feature tests (>= 3 per feature) | 9 | 5 | Nguyễn Quang Thành |
| End-to-end workflow test (>= 1) | 1 | 0 | Nguyễn Quang Thành |
| State transition tests (>= 1 per transition) | 1 | 1 | Nguyễn Quang Thành |
| NFR tests (>= 1 per NFR: NFR-01) | 1 | 0 | Nguyễn Quang Thành |
| **Total for this workflow** | **12** | **6** | **Nguyễn Quang Thành** |

**Pass criteria at Defense 1.1**
| Criterion | Committed |
| :--- | :--- |
| Runs end-to-end on staging with seeded data | Yes |
| Main scenario passes | Yes |
| Exception scenarios passing | 100% (minimum >= 80%) |
| Test pass rate for this workflow | >= 90% overall; 100% main-path tests |
| Open Critical / High defects | 0 |

**Reviewer note**
| Note | |
| :--- | :--- |

---

#### 6.2 Workflow WF-02
| Field | Value |
| :--- | :--- |
| **Workflow name** | Timed mock exam |
| **Owner (one member)** | Phạm Nguyễn Đăng Hải |
| **Supporting members** | Nguyễn Quang Thành, Lê Vũ Hoàng |
| **Actors** | Admin, Department Head, Student |
| **Trigger** | Student registers to start a mock exam attempt within the daily quota |
| **Business outcome** | System records the attempt, captures spoken answers via microphone within the time limit, and returns a CLO scorecard after grading completes |
| **Main entity state path** | MockExamSession: in_progress -> submitted -> graded (2 transitions) |
| **Activity diagram (Report 4 section)** | Report 4 (SDD) – Activity Diagram WF-02 (Timed Mock Exam) |

**Features in this workflow**
| Feature ID | Feature | Acceptance criteria | Test case IDs |
| :---: | :--- | :--- | :--- |
| **F-02** | Practice parameter and mock exam quota configuration | Admin configures practice session parameters and daily mock exam limits; Department Head configures question structure and duration for subject mock exams. | TC-F02-01..TC-F02-03 |
| **F-05** | Quota-controlled timed mock exam | System rejects new attempts if the student has exhausted the daily quota; main questions are drawn from the approved practice pool, require recording spoken answers via microphone before reviewing the transcript, and auto-submit when time expires. | TC-F05-01..TC-F05-03 |
| **F-06** | Mock exam scorecard by Course Learning Outcome (CLO) | After submission and completion of automated grading, the system displays a summary scorecard showing achievement by each Course Learning Outcome (CLO). | TC-F06-01..TC-F06-03 |

**Demo scenarios**
| Scenario ID | Type | Preconditions and data | Main steps | Expected result | Presenter |
| :---: | :---: | :--- | :--- | :--- | :--- |
| **DS-03** (DS-02a) | Main | Subject has mock exam structure configured; student has remaining daily attempts | Start timed mock exam -> Record spoken answers via microphone -> Submit exam and wait for automated grading to complete | System records submission, completes automated grading, displays the CLO evaluation scorecard, and deducts one daily attempt | Phạm Nguyễn Đăng Hải |
| **DS-04** (DS-02b) | Exception | Student has used all allowed daily mock exam attempts for the subject | Student clicks to start an additional mock exam attempt for that subject on the same day | System rejects creating a new exam session and notifies that the daily mock exam quota has been reached | Phạm Nguyễn Đăng Hải |

**Committed test cases**
| Test type | Count | Automated in CI | Responsible member(s) |
| :--- | :---: | :---: | :--- |
| Feature tests (>= 3 per feature) | 9 | 5 | Phạm Nguyễn Đăng Hải |
| End-to-end workflow test (>= 1) | 1 | 0 | Phạm Nguyễn Đăng Hải |
| State transition tests (>= 1 per transition) | 2 | 1 | Phạm Nguyễn Đăng Hải |
| NFR tests (>= 1 per NFR: NFR-02) | 1 | 1 | Phạm Nguyễn Đăng Hải |
| **Total for this workflow** | **13** | **7** | **Phạm Nguyễn Đăng Hải** |

**Pass criteria at Defense 1.1**
| Criterion | Committed |
| :--- | :--- |
| Runs end-to-end on staging with seeded data | Yes |
| Main scenario passes | Yes |
| Exception scenarios passing | 100% (minimum >= 80%) |
| Test pass rate for this workflow | >= 90% overall; 100% main-path tests |
| Open Critical / High defects | 0 |

**Reviewer note**
| Note | |
| :--- | :--- |

---

#### 6.3 Workflow WF-03
| Field | Value |
| :--- | :--- |
| **Workflow name** | Question bank and Rubric approval |
| **Owner (one member)** | Nguyễn Trọng Tốt |
| **Supporting members** | Phạm Nguyễn Đăng Hải, Nguyễn Quang Thành |
| **Actors** | Lecturer, Department Head |
| **Trigger** | Lecturer creates a new question (practice flow by CLO or official exam flow following the Department Head's matrix) and submits for review |
| **Business outcome** | Questions and valid 10.0-point Rubric criteria are approved by the Department Head into `practice_questions` or `exam_questions` |
| **Main entity state path** | Question: DRAFT -> SUBMITTED_FOR_REVIEW -> APPROVED \| NEEDS_REVISION \| REJECTED (4 transitions) |
| **Activity diagram (Report 4 section)** | Report 4 (SDD) – Activity Diagram WF-03 (Question Bank & Rubric Approval) |

**Features in this workflow**
| Feature ID | Feature | Acceptance criteria | Test case IDs |
| :---: | :--- | :--- | :--- |
| **F-07** | Two-flow oral question authoring and Rubric design | Lecturer authors practice/mock questions independently by course CLO (`practice_questions`) or authors official exam questions strictly following the Department Head's sample exam / structure matrix (`exam_questions`); saving requires a valid model answer and Rubric criteria weights summing to 10.0 points. | TC-F07-01..TC-F07-03 |
| **F-08** | Question review submission and bank approval | Questions created by Lecturers must be submitted for review; only questions approved by the Department Head (`APPROVED`) are published into the practice pool (`practice_questions`) or official exam pool (`exam_questions`). | TC-F08-01..TC-F08-03 |

**Demo scenarios**
| Scenario ID | Type | Preconditions and data | Main steps | Expected result | Presenter |
| :---: | :---: | :--- | :--- | :--- | :--- |
| **DS-05** (DS-03a) | Main | Lecturer and Department Head accounts; subject has CLOs and an official exam structure matrix | Lecturer creates a question (practice flow by CLO or official exam flow by structure matrix) with model answer and 10.0-point Rubric and submits for review -> Department Head reviews and clicks Approve | Question transitions to `APPROVED` status and is published into `practice_questions` or `exam_questions` | Nguyễn Trọng Tốt |
| **DS-06** (DS-03b) | Exception | Lecturer enters a set of Rubric criteria whose component weights do not sum to 10.0 points | Lecturer clicks save or submit for review with an invalid Rubric | System rejects saving and displays a message requiring criteria weights to be adjusted to total 10.0 points | Nguyễn Trọng Tốt |

**Committed test cases**
| Test type | Count | Automated in CI | Responsible member(s) |
| :--- | :---: | :---: | :--- |
| Feature tests (>= 3 per feature) | 6 | 3 | Nguyễn Trọng Tốt |
| End-to-end workflow test (>= 1) | 1 | 0 | Nguyễn Trọng Tốt |
| State transition tests (>= 1 per transition) | 4 | 2 | Nguyễn Trọng Tốt |
| NFR tests (>= 1 per NFR: NFR-03, NFR-04) | 2 | 2 | Nguyễn Trọng Tốt |
| **Total for this workflow** | **13** | **7** | **Nguyễn Trọng Tốt** |

**Pass criteria at Defense 1.1**
| Criterion | Committed |
| :--- | :--- |
| Runs end-to-end on staging with seeded data | Yes |
| Main scenario passes | Yes |
| Exception scenarios passing | 100% (minimum >= 80%) |
| Test pass rate for this workflow | >= 90% overall; 100% main-path tests |
| Open Critical / High defects | 0 |

**Reviewer note**
| Note | |
| :--- | :--- |

---

#### 6.4 Workflow WF-04
| Field | Value |
| :--- | :--- |
| **Workflow name** | Official lab exam, audit, and appeal |
| **Owner (one member)** | Lê Vũ Hoàng |
| **Supporting members** | Nguyễn Trọng Tốt, Phạm Nguyễn Đăng Hải |
| **Actors** | Department Head, Examination Officer, Proctor, Student, Lecturer |
| **Trigger** | Examination Officer publishes the shift exam (`is_exam_published = true`) and Proctor checks in students by seat (1–40) |
| **Business outcome** | Exam submissions and audio files are sealed and stored, pre-graded by AI, verified by the Lecturer, and published once (activating One-Way Lock), supporting 5-step appeal regrading |
| **Main entity state path** | StudentExamTicket: SCHEDULED -> IN_PROGRESS -> SUBMITTED -> AI_GRADED -> AUDITED -> PUBLISHED/LOCKED (5); AppealRequest: SUBMITTED_TO_EXAM_OFFICE -> ASSIGNED_TO_LECTURER -> REGRADED_BY_LECTURER -> APPROVED \| REJECTED (3) |
| **Activity diagram (Report 4 section)** | Report 4 (SDD) – Activity Diagram WF-04 (Official Lab Exam, Audit & Appeal) |

**Features in this workflow**
| Feature ID | Feature | Acceptance criteria | Test case IDs |
| :---: | :--- | :--- | :--- |
| **F-09** | Official exam session, lab shift setup, and check-in | Department Head configures exam Follow-up rules (1–5 questions, default 2); Examination Officer creates exam sessions, lab shifts, assigns Proctors/Examiners, imports rosters, allocates non-duplicate exam sets, and publishes the shift exam (`is_exam_published = true`); Proctor only checks in students by seat number (1–40, verifying Student ID/CCCD). | TC-F09-01..TC-F09-03 |
| **F-10** | Official exam on fullscreen-locked kiosk interface | Student enters the exam only after Proctor check-in, exam publication by Examination Officer, and passing the microphone check; the kiosk locks fullscreen, auto-suspends and alerts the Proctor to log violations into `exam_anti_cheat_logs` if fullscreen exit/blur exceeds 3 times, and seals the audio file with a SHA-256 hash upon submission. | TC-F10-01..TC-F10-03 |
| **F-11** | Post-exam grade verification and shift grade publication | Lecturer listens to recorded audio, reviews AI feedback, and adjusts scores with an `override_reason` if needed; after 100% of shift submissions are verified, Lecturer / Examination Officer publishes grades once (`publish-grades`), activating the One-Way Lock (`is_locked = true`). | TC-F11-01..TC-F11-03 |
| **F-12** | Grade acknowledgement and 5-step internal appeal handling | Student acknowledges published grades or submits an appeal (`AppealRequest`) to the Examination Officer (with both Examination Officer and Department Head receiving notifications) -> Examination Officer validates the appeal -> Examination Officer assigns an independent Lecturer (up to 24 submissions/Lecturer; evenly divided if total > Lecturers * 24, or load-balanced with manual adjustment if <= Lecturers * 24) -> Lecturer regrades on the Evidence Panel -> Examination Officer approves/rejects the final decision (opening a One-Way Lock exception to update the official score if approved). | TC-F12-01..TC-F12-03 |

**Demo scenarios**
| Scenario ID | Type | Preconditions and data | Main steps | Expected result | Presenter |
| :---: | :---: | :--- | :--- | :--- | :--- |
| **DS-07** (DS-04a) | Main | Lab exam shift is scheduled with assigned students, Examination Officer, Proctor, and Lecturer | Examination Officer publishes shift exam & Proctor checks in seats (1–40) -> Student checks mic, completes exam on locked kiosk, and submits -> Lecturer verifies all submissions and Lecturer / Examination Officer clicks Publish Grades | Exam is saved with SHA-256 audio recording, pre-graded, verified, and published once successfully (activating One-Way Lock) | Lê Vũ Hoàng |
| **DS-08** (DS-04b) | Exception | Shift grades have been published and direct grade editing is locked | Attempt to directly edit a published grade (blocked) -> Student submits appeal to Examination Officer -> Examination Officer & Department Head receive notifications; Examination Officer validates & assigns independent Lecturer to regrade (<= 24 submissions/Lecturer) -> Lecturer regrades and Examination Officer approves final decision | Direct grade edit is rejected (HTTP 403); score is updated only after completing the 5-step appeal workflow approved by the Examination Officer | Lê Vũ Hoàng |

**Committed test cases**
| Test type | Count | Automated in CI | Responsible member(s) |
| :--- | :---: | :---: | :--- |
| Feature tests (>= 3 per feature) | 12 | 6 | Lê Vũ Hoàng |
| End-to-end workflow test (>= 1) | 1 | 0 | Lê Vũ Hoàng |
| State transition tests (>= 1 per transition) | 8 | 4 | Lê Vũ Hoàng |
| NFR tests (>= 1 per NFR: NFR-05) | 1 | 0 | Lê Vũ Hoàng |
| **Total for this workflow** | **22** | **10** | **Lê Vũ Hoàng** |

**Pass criteria at Defense 1.1**
| Criterion | Committed |
| :--- | :--- |
| Runs end-to-end on staging with seeded data | Yes |
| Main scenario passes | Yes |
| Exception scenarios passing | 100% (minimum >= 80%) |
| Test pass rate for this workflow | >= 90% overall; 100% main-path tests |
| Open Critical / High defects | 0 |

**Reviewer note**
| Note | |
| :--- | :--- |

---

### 7. Individual Commitments

#### 7.1 Member 1
| Field | Value |
| :--- | :--- |
| **Full name** | Nguyễn Quang Thành |
| **Student ID** | |
| **Role in team** | Team Leader & Backend |

**Ownership**
| Type | Items (IDs / names) | Notes |
| :--- | :--- | :--- |
| **Main workflow(s) owned** | WF-01 — Interactive Oral Practice Workflow | End-to-end practice session lifecycle, STT transcript review, and follow-up flow |
| **Mandatory features owned** | F-01, F-03, F-04 | Authentication, 6-role RBAC & academic hierarchy, interactive oral practice, Rubric grading & practice history |
| **Modules / entities owned** | Interactive practice module (`PracticeSession`, `PracticeAnswer`), Real-time notifications (`PracticeHub`), Grade report exports | Backend APIs, background grading queue integration, and SignalR Hub |
| **Design decisions owned (D-IDs)** | D1 (Layered backend server with background grading queue); R2 (Technical term transcript review) | Responsible for layered backend architecture and asynchronous processing |
| **NFRs responsible** | NFR-01 (Submission recording time <= 1 second) | Immediate database persistence before queuing background grading |

**Delivery commitments to Defense 1.1**
| Commitment | Committed (count / IDs) | Due |
| :--- | :--- | :--- |
| **Functional test cases written** | 12 (TC-F01-01..03, TC-F03-01..03, TC-F04-01..03, TC-WF01-E2E, TC-ST-PS01, TC-NFR01) | Before Defense 1.1 |
| **Of which automated in CI** | 6 | Before Defense 1.1 |
| **Demo scenarios presented** | 2 (DS-01 Main, DS-02 Exception) | At Defense 1.1 |
| **Required fixes owned (section 9)** | 1 (RF-01 — Commit Mermaid and Draw.io diagram source code into the repository) | Before Defense 1.1 |
| **Documentation sections (Report / section)** | Report 2 (Development Process), Report 3 (Sections 1, 2.1, 2.2.2, 3.5, 5.2), Report 4 (System Architecture & WF-01), Report 5 (WF-01 Tests) | Before Defense 1.1 |

**Ownership verification at Review 2 (completed by reviewers)**
| Result | Weak points observed | Follow-up (by whom, by when) |
| :--- | :--- | :--- |
| ☐ Verified \| ☐ Partly verified \| ☐ Not verified | | |

**Member confirmation**
| Signature / date | |
| :--- | :--- |

---

#### 7.2 Member 2
| Field | Value |
| :--- | :--- |
| **Full name** | Phạm Nguyễn Đăng Hải |
| **Student ID** | |
| **Role in team** | Database & Frontend |

**Ownership**
| Type | Items (IDs / names) | Notes |
| :--- | :--- | :--- |
| **Main workflow(s) owned** | WF-02 — Timed Mock Exam Workflow | Daily quota enforcement, timed microphone capture, auto-submission, and CLO scorecard |
| **Mandatory features owned** | F-02, F-05, F-06 | Practice & quota configuration, quota-controlled timed mock exam, CLO scorecard |
| **Modules / entities owned** | Database schema and seed data (`MockExamSession`, `MockExamAnswer`, `MockExamQuota`, `CourseClo`), Timed mock exam UI and academic configuration | 3NF database schema, indexing, and WF-02 frontend screens |
| **Design decisions owned (D-IDs)** | D5 (Two-flow practice and official exam pools with Rubric snapshots); G3 (Staging environment & seed data) | Question pool isolation and repeatable demo seed data |
| **NFRs responsible** | NFR-02 (Query response time <= 2 seconds) | Database indexing and query optimization for lists and scorecards |

**Delivery commitments to Defense 1.1**
| Commitment | Committed (count / IDs) | Due |
| :--- | :--- | :--- |
| **Functional test cases written** | 13 (TC-F02-01..03, TC-F05-01..03, TC-F06-01..03, TC-WF02-E2E, TC-ST-MS01..02, TC-NFR02) | Before Defense 1.1 |
| **Of which automated in CI** | 7 | Before Defense 1.1 |
| **Demo scenarios presented** | 2 (DS-03 Main, DS-04 Exception) | At Defense 1.1 |
| **Required fixes owned (section 9)** | 1 (RF-03 — Verify Staging deployment environment and automated seed data script) | Before Defense 1.1 |
| **Documentation sections (Report / section)** | Report 2 (Sections 1.1, 1.2, 3, 4), Report 3 (Context, Use Case, Conceptual ERD, Sections 3.4, 4.1), Report 4 (Database Design & WF-02), Report 5 (WF-02 Tests) | Before Defense 1.1 |

**Ownership verification at Review 2 (completed by reviewers)**
| Result | Weak points observed | Follow-up (by whom, by when) |
| :--- | :--- | :--- |
| ☐ Verified \| ☐ Partly verified \| ☐ Not verified | | |

**Member confirmation**
| Signature / date | |
| :--- | :--- |

---

#### 7.3 Member 3
| Field | Value |
| :--- | :--- |
| **Full name** | Nguyễn Trọng Tốt |
| **Student ID** | |
| **Role in team** | Backend, AI & QA |

**Ownership**
| Type | Items (IDs / names) | Notes |
| :--- | :--- | :--- |
| **Main workflow(s) owned** | WF-03 — Question Bank & Rubric Approval Workflow | Two-flow question authoring (CLO vs Department Head matrix), 10.0-point Rubric validation, and Department Head approval |
| **Mandatory features owned** | F-07, F-08 | Two-flow oral question authoring & Rubric design, question review submission & bank approval |
| **Modules / entities owned** | Question bank and AI authoring/grading integration (`PracticeQuestion`, `ExamQuestion`, `Rubric`, `RubricCriterion`), Grade locking (`OneWayLock`), 5-step Appeals (`AppealRequest`), DLQ (`DeadLetterQueue`) | AI generation/grading services, Polly retry/DLQ resilience (Admin MF-01/02, Exam Officer MF-04), and automated test suites |
| **Design decisions owned (D-IDs)** | D2 (AI assistive role under lecturer control); D3 (Separation of 6-role duties across question approval, exam operations, audit, and 5-step appeal regrading); R1 (AI retry & DLQ) | Human-in-the-loop authority and fault-tolerant background grading |
| **NFRs responsible** | NFR-03 (Grading reliability with 3x retry & DLQ); NFR-04 (Grade integrity blocking direct edits with HTTP 403) | AI failure recovery and post-publication grade immutability |

**Delivery commitments to Defense 1.1**
| Commitment | Committed (count / IDs) | Due |
| :--- | :--- | :--- |
| **Functional test cases written** | 13 (TC-F07-01..03, TC-F08-01..03, TC-WF03-E2E, TC-ST-Q01..04, TC-NFR03, TC-NFR04) | Before Defense 1.1 |
| **Of which automated in CI** | 7 | Before Defense 1.1 |
| **Demo scenarios presented** | 2 (DS-05 Main, DS-06 Exception) | At Defense 1.1 |
| **Required fixes owned (section 9)** | 1 (RF-02 — Synchronize 6 roles and audit/5-step appeal specifications across BM03, SRS, and SDD) | Before Defense 1.1 |
| **Documentation sections (Report / section)** | Report 1 (Full), Report 3 (Logical ERD, Sections 3.1.4, 3.3, 5.1/5.3 Question & Grade/Appeal), Report 4 (WF-03 & AI/Resilience Design), Report 5 (Test Plan & WF-03 Tests) | Before Defense 1.1 |

**Ownership verification at Review 2 (completed by reviewers)**
| Result | Weak points observed | Follow-up (by whom, by when) |
| :--- | :--- | :--- |
| ☐ Verified \| ☐ Partly verified \| ☐ Not verified | | |

**Member confirmation**
| Signature / date | |
| :--- | :--- |

---

#### 7.4 Member 4
| Field | Value |
| :--- | :--- |
| **Full name** | Lê Vũ Hoàng |
| **Student ID** | |
| **Role in team** | Frontend |

**Ownership**
| Type | Items (IDs / names) | Notes |
| :--- | :--- | :--- |
| **Main workflow(s) owned** | WF-04 — Official Lab Exam, Audit & Appeal Workflow | Exam publishing, lab shift check-in, fullscreen-locked kiosk exam, post-exam lecturer audit, grade publication, and 5-step appeals |
| **Mandatory features owned** | F-09, F-10, F-11, F-12 | Shift setup & check-in, fullscreen-locked kiosk exam, post-exam verification & grade publication, grade acknowledgement & 5-step appeals |
| **Modules / entities owned** | Core frontend and 6-role authorization, Fullscreen kiosk exam UI (`StudentExamTicket`, `ExamQuestionSubmission`, `ExamAntiCheatLog`), Lecturer audit screen (`LecturerAudit`) | Fullscreen focus-loss enforcement, SHA-256 audio evidence sealing, and audio audit UI |
| **Design decisions owned (D-IDs)** | D4 (Microphone recording requirement, transcript editing, and official exam audio sealing); R3 (Kiosk security & post-publication grade lock) | Authentic oral exam input and tamper-evident exam evidence |
| **NFRs responsible** | NFR-05 (Audio SHA-256 hash match and automatic suspension when fullscreen exit/blur > 3 times) | Audio integrity verification and kiosk anti-cheat enforcement |

**Delivery commitments to Defense 1.1**
| Commitment | Committed (count / IDs) | Due |
| :--- | :--- | :--- |
| **Functional test cases written** | 22 (TC-F09-01..03, TC-F10-01..03, TC-F11-01..03, TC-F12-01..03, TC-WF04-E2E, TC-ST-TK01..05, TC-ST-AP01..03, TC-NFR05) | Before Defense 1.1 |
| **Of which automated in CI** | 10 | Before Defense 1.1 |
| **Demo scenarios presented** | 2 (DS-07 Main, DS-08 Exception) | At Defense 1.1 |
| **Required fixes owned (section 9)** | 1 (RF-04 — Finalize end-to-end integration rehearsal for kiosk violation counter, SHA-256 verification, and grade publication lock) | Before Defense 1.1 |
| **Documentation sections (Report / section)** | Report 2 (FE/BE Master Schedule), Report 3 (Screen Flows, Sections 3.2, 4.2, 5.4), Report 4 (Frontend Architecture & WF-04), Report 5 (WF-04 Tests) | Before Defense 1.1 |

**Ownership verification at Review 2 (completed by reviewers)**
| Result | Weak points observed | Follow-up (by whom, by when) |
| :--- | :--- | :--- |
| ☐ Verified \| ☐ Partly verified \| ☐ Not verified | | |

**Member confirmation**
| Signature / date | |
| :--- | :--- |

---

### 8. Summary and Cross-check

#### 8.1 By workflow
| Workflow | Owner | Features | Demo scenarios (main / exception) | Test cases | Automated |
| :---: | :--- | :--- | :---: | :---: | :---: |
| **WF-01** | Nguyễn Quang Thành | 3 (F-01, F-03, F-04) | 1 / 1 (DS-01, DS-02) | 12 | 6 |
| **WF-02** | Phạm Nguyễn Đăng Hải | 3 (F-02, F-05, F-06) | 1 / 1 (DS-03, DS-04) | 13 | 7 |
| **WF-03** | Nguyễn Trọng Tốt | 2 (F-07, F-08) | 1 / 1 (DS-05, DS-06) | 13 | 7 |
| **WF-04** | Lê Vũ Hoàng | 4 (F-09, F-10, F-11, F-12) | 1 / 1 (DS-07, DS-08) | 22 | 10 |
| **Total** | **4 members** | **12** | **8 (4 / 4)** | **60** | **30** |

#### 8.2 By member
| Member | Workflows owned | Features | Test cases | Automated | Demo scenarios | Review 2 verification |
| :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **Nguyễn Quang Thành** | WF-01 | 3 (F-01, F-03, F-04) | 12 | 6 | 2 (DS-01, DS-02) | |
| **Phạm Nguyễn Đăng Hải** | WF-02 | 3 (F-02, F-05, F-06) | 13 | 7 | 2 (DS-03, DS-04) | |
| **Nguyễn Trọng Tốt** | WF-03 | 2 (F-07, F-08) | 13 | 7 | 2 (DS-05, DS-06) | |
| **Lê Vũ Hoàng** | WF-04 | 4 (F-09..F-12) | 22 | 10 | 2 (DS-07, DS-08) | |
| **Total** | **4** | **12** | **60** | **30** | **8 (4 / 4)** | |

#### 8.3 Committed Numbers
| Metric | Committed Value |
| :--- | :--- |
| **Number of main workflows** | 4 (WF-01..WF-04) |
| **Number of demo scenarios (main / exception)** | 8 (4 Main / 4 Exception: DS-01..DS-08) |
| **Committed functional test cases** | 60 (36 Feature + 4 E2E + 15 State Transition + 5 NFR) |
| **Of which automated in CI** | 30 (50% of committed functional test cases) |

#### 8.4 Eligibility Thresholds for the Viva
| Criterion | Minimum threshold | Agreed threshold |
| :--- | :--- | :--- |
| **Main workflows** | 100% run end-to-end | 100% run end-to-end (all 4 workflows WF-01..WF-04) |
| **Demo scenarios** | 100% main; >= 80% exception | 100% main (4/4); >= 80% exception (target 100%, 4/4) |
| **Test case count** | >= committed number | >= 60 functional test cases |
| **Test pass rate** | >= 90% overall; 100% main-path tests of mandatory features | >= 90% overall (>= 54/60); 100% main-path tests of all 12 mandatory features |
| **Automation** | >= 50% run in CI | >= 50% run in CI (>= 30 automated test cases) |
| **Defects** | No open Critical or High defects | 0 open Critical or High defects |
| **Individual** | Each member demos the workflow they own and meets their delivery commitments | All 4 members demo their owned workflow and complete 100% of their individual delivery commitments |

### 9. Required Fixes
| ID | Item | Related workflow | Owner | Due |
| :---: | :--- | :---: | :--- | :--- |
| **RF-01** | Update and commit Mermaid and Draw.io diagram source code for the architecture, Logical ERD, state machine, and business workflow diagrams into the repository matching Report 4 (G1). | WF-01 | Nguyễn Quang Thành | Before Defense 1.1 |
| **RF-02** | Synchronize detailed specifications of the 6 user roles and the post-exam audit and 5-step appeal workflow across Form BM03, SRS, and SDD (G2). | WF-03 | Nguyễn Trọng Tốt | Before Defense 1.1 |
| **RF-03** | Verify the centralized Staging deployment environment and automated sample data initialization script for repeatable demo scenarios (G3). | WF-02 | Phạm Nguyễn Đăng Hải | Before Defense 1.1 |
| **RF-04** | Finalize end-to-end integration rehearsal for the fullscreen-locked kiosk violation counter, audio SHA-256 verification, and post-publication grade lock. | WF-04 | Lê Vũ Hoàng | Before Defense 1.1 |

### 10. Review 2 Conclusion (completed by reviewers)
☐ Pass     ☐ Conditional pass     ☐ Re-review

### 11. Confirmation
| Role | Full name | Signature | Date |
| :--- | :--- | :--- | :--- |
| **Reviewer 1** | | | |
| **Reviewer 2** | | | |
| **Supervisor** | Dr. Nguyễn Thị Cẩm Hương | | |
| **Team leader** | Nguyễn Quang Thành | | |
