# SEP490 · CAPSTONE PROJECT · REVIEW 2 · BM04 — REVIEW 2 COMMITMENT

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
| **Attached file name** | SEP490_FA26SE166_R2_Registration.pdf |

### 3. Mandatory Features
| ID | Feature | Actor | Acceptance criteria | Workflow | Owner |
| :---: | :--- | :--- | :--- | :---: | :--- |
| **F-01** | Authentication, 6-role RBAC, class hierarchy, and in-class exam shifts | All Users, Administrator, Department Head, Lecturer, Student | Dual Authentication supporting Google OAuth 2.0 PKCE and university-provisioned accounts with mandatory password change on first login, with no public self-registration; 6-role access control (student, lecturer, department head, examination officer, proctor, administrator); 4-tier academic hierarchy management (administrator creates semesters, courses, and assigns department heads → department head assigns lecturers → lecturer manages class sections with join credentials); supports in-class formative exam shifts with configurable shift title, question bank pool selection, anti-collision question distribution across students, optional follow-up questions without audio retention, 5-minute rotating OTP security with grace period, automated parallel grading, and real-time grade publishing upon submission. | WF-01 | Nguyễn Quang Thành |
| **F-02** | Practice parameter and mock exam quota configuration | Admin, Department Head | Admin configures practice session parameters and daily mock exam limits; Department Head configures question structure and duration for subject mock exams. | WF-02 | Phạm Nguyễn Đăng Hải |
| **F-03** | Multimodal oral practice and technical term editing | Student | Student starts a practice session with approved questions; supports flexible dual input modes allowing either direct text entry or voice recording via microphone with technical term editing on the transcript before submission; idle sessions exceeding the timeout auto-close. | WF-01 | Nguyễn Quang Thành |
| **F-04** | Practice grading, follow-up questions, and history | Student | System returns Rubric-based grading results, generates contextual follow-up questions (used within the session only, not saved to the question bank) when clarification is needed, and saves practice history. | WF-01 | Nguyễn Quang Thành |
| **F-05** | Quota-controlled timed mock exam | Student | System rejects new attempts if the student has exhausted the daily quota; main questions are drawn from the approved practice pool, require recording spoken answers via microphone before reviewing the transcript, and auto-submit when time expires. | WF-02 | Phạm Nguyễn Đăng Hải |
| **F-06** | Mock exam scorecard by Course Learning Outcome (CLO) | Student | After submission and completion of automated grading, the system displays a summary scorecard showing achievement by each Course Learning Outcome (CLO). | WF-02 | Phạm Nguyễn Đăng Hải |
| **F-07** | Two-flow oral question authoring and Rubric design | Lecturer, Department Head | Lecturer authors practice and mock questions independently by course CLO for the practice question bank, or authors official exam questions strictly following the Department Head's sample exam and structure matrix for the official exam question bank; saving requires a valid model answer and Rubric criteria weights summing to exactly 10.0 points. | WF-03 | Nguyễn Trọng Tốt |
| **F-08** | Question review submission and bank approval | Lecturer, Department Head | Questions created by Lecturers must be submitted for review; only questions approved by the Department Head are published into the practice question bank or official exam question bank. | WF-03 | Nguyễn Trọng Tốt |
| **F-09** | Official exam session, lab shift setup, and check-in | Department Head, Examination Officer, Proctor, Examiner (Lecturer) | Department Head configures exam Follow-up rules (1–5 questions, default 2); Examination Officer creates exam sessions, lab shifts, assigns Proctors/Examiners, imports rosters, allocates non-duplicate exam sets, and publishes the official shift exam; Proctor only checks in students by seat number (1–40, verifying Student ID/CCCD). | WF-04 | Lê Vũ Hoàng |
| **F-10** | Official exam on fullscreen-locked kiosk interface | Student, Proctor | Student enters the exam only after Proctor check-in, exam publication by Examination Officer, and passing the microphone check; the kiosk locks fullscreen, auto-suspends and alerts the Proctor to log violations into the exam room anti-cheat violation log if fullscreen exit/blur exceeds 3 times, and saves the audio recording with a SHA-256 integrity hash upon submission. | WF-04 | Lê Vũ Hoàng |
| **F-11** | Post-exam grade verification and shift grade publication | Examiner (Evaluating Lecturer), Examination Officer | Examiner (assigned shift Lecturer) listens to recorded audio, reviews AI feedback, and adjusts scores with a formal justification statement if needed; after 100% of shift submissions are verified, Examiner / Examination Officer publishes grades once, permanently locking the grades against direct modifications. | WF-04 | Lê Vũ Hoàng |
| **F-12** | Grade acknowledgement and 5-step internal appeal handling | Student, Examination Officer, Department Head, Appeal Examiner (Lecturer) | Student acknowledges published grades or submits a formal appeal request to the Examination Officer (with both Examination Officer and Department Head receiving notifications) → Examination Officer validates the appeal → Examination Officer assigns an independent Lecturer (quota <= 24 submissions/Lecturer, excluding 100% initial shift examiners; automated proposal and manual adjustment) → Lecturer regrades on the Evidence Panel → Examination Officer approves/rejects the final decision (updating the official score through the approved appeal exception if approved; Department Head receives notification for academic oversight). | WF-04 | Lê Vũ Hoàng |

### 4. Non-Functional Requirements
| ID | Quality attribute | Metric | Threshold | Measurement method | Owner |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **NFR-01** | Submission recording time | Time for the server to persist a student's submission status before queuing background grading | <= 1 second for students submitting within the same lab exam shift | Run concurrent load tests on the lab exam submission API | Nguyễn Quang Thành |
| **NFR-02** | Query response time | Response time of APIs fetching lists, configurations, and graded results | <= 2 seconds under normal operating conditions | Measure response times via the automated API integration test suite | Phạm Nguyễn Đăng Hải |
| **NFR-03** | Grading reliability | Rate of submissions preserved and routed to the retry queue when the AI service fails transiently | No lost submissions; automatic retry up to 3 times before routing to DLQ (Admin replays practice/mock DLQ; Examination Officer replays official exam DLQ) | Simulate AI service disconnection and verify records and role-based replay in the Dead-Letter Queue (DLQ) | Nguyễn Trọng Tốt |
| **NFR-04** | Grade integrity | Rate of blocking direct grade update or deletion attempts after shift grades are published | All direct grade edit or delete requests outside the appeal flow are rejected due to post-publication grade locking rules | Run authorization and update-blocking tests on locked grade records | Nguyễn Trọng Tốt |
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
| **Main entity state path** | Session initiated → Responding and reviewing transcription → Scorecard and feedback delivered (inactivity timeout auto-closes after 5 minutes) |
| **Activity diagram (Report 4 section)** | Report 4 (SDD) – Activity Diagram WF-01 (Interactive Oral Practice) |

**Features in this workflow**
| Feature ID | Feature | Acceptance criteria | Test case IDs |
| :---: | :--- | :--- | :--- |
| **F-01** | Authentication, 6-role RBAC, class hierarchy, and in-class exam shifts | Dual Authentication supporting Google OAuth 2.0 PKCE and university-provisioned accounts with mandatory password change on first login, with no public self-registration; 6-role access control; supports 4-tier academic hierarchy and in-class exam shifts with configurable shift title, anti-collision question distribution across students, optional follow-up questions without audio retention, 5-minute rotating OTP security with grace period, automated parallel grading, and real-time grade publishing upon submission. | TC-F01-01..TC-F01-03 |
| **F-03** | Multimodal oral practice and technical term editing | Student starts a practice session with approved questions; supports flexible dual input modes allowing either direct text entry or voice recording via microphone with technical term editing on the transcript before submission; idle sessions exceeding the timeout auto-close. | TC-F03-01..TC-F03-03 |
| **F-04** | Practice grading, follow-up questions, and history | System returns Rubric-based grading results, generates contextual follow-up questions (used within the session only, not saved to the question bank) when clarification is needed, and saves practice history. | TC-F04-01..TC-F04-03 |

**Demo scenarios**
| Scenario ID | Type | Preconditions and data | Main steps | Expected result | Presenter |
| :---: | :---: | :--- | :--- | :--- | :--- |
| **DS-01** (DS-01a) | Main | Student account; subject has approved questions in the practice pool | Select subject & start practice → Answer via direct text or voice recording with transcript editing → Receive grading result, answer follow-up question, and finish session | System displays detailed criteria scorecard and saves the practice session as completed | Nguyễn Quang Thành |
| **DS-02** (DS-01b) | Exception | Practice session is open but left idle by the student beyond the inactivity timeout | Student returns to the timed-out session and clicks to submit an answer or fetch the next question | System auto-closes the session, preserves scores of previously completed questions, and rejects new actions | Nguyễn Quang Thành |

**Committed test cases**
| Test type | Count | Automated in CI | Responsible member(s) |
| :--- | :---: | :---: | :--- |
| Feature tests (>= 3 / feature) | 9 | 5 | Nguyễn Quang Thành |
| End-to-end workflow test (>= 1 / workflow) | 1 | 0 | Nguyễn Quang Thành |
| State transition tests (>= 1 / transition) | 1 | 1 | Nguyễn Quang Thành |
| NFR tests (>= 1 / NFR: NFR-01) | 1 | 0 | Nguyễn Quang Thành |
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
| **Main entity state path** | Timed mock session initiated → In progress under countdown → Submitted → Graded with learning outcome scorecard |
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
| **DS-03** (DS-02a) | Main | Subject has mock exam structure configured; student has remaining daily attempts | Start timed mock exam → Record spoken answers via microphone → Submit exam and wait for automated grading to complete | System records submission, completes automated grading, displays the CLO evaluation scorecard, and deducts one daily attempt | Phạm Nguyễn Đăng Hải |
| **DS-04** (DS-02b) | Exception | Student has used all allowed daily mock exam attempts for the subject | Student clicks to start an additional mock exam attempt for that subject on the same day | System rejects creating a new exam session and notifies that the daily mock exam quota has been reached | Phạm Nguyễn Đăng Hải |

**Committed test cases**
| Test type | Count | Automated in CI | Responsible member(s) |
| :--- | :---: | :---: | :--- |
| Feature tests (>= 3 / feature) | 9 | 5 | Phạm Nguyễn Đăng Hải |
| End-to-end workflow test (>= 1 / workflow) | 1 | 0 | Phạm Nguyễn Đăng Hải |
| State transition tests (>= 1 / transition) | 2 | 1 | Phạm Nguyễn Đăng Hải |
| NFR tests (>= 1 / NFR: NFR-02) | 1 | 1 | Phạm Nguyễn Đăng Hải |
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
| **Business outcome** | Questions and valid 10.0-point Rubric criteria are approved by the Department Head into the practice question bank or official exam question bank |
| **Main entity state path** | Question draft → Submitted for review → Approved into question bank, or Revision requested, or Rejected |
| **Activity diagram (Report 4 section)** | Report 4 (SDD) – Activity Diagram WF-03 (Question Bank & Rubric Approval) |

**Features in this workflow**
| Feature ID | Feature | Acceptance criteria | Test case IDs |
| :---: | :--- | :--- | :--- |
| **F-07** | Two-flow oral question authoring and Rubric design | Lecturer authors practice and mock questions independently by course CLO for the practice question bank, or authors official exam questions strictly following the Department Head's sample exam and structure matrix for the official exam question bank; saving requires a valid model answer and Rubric criteria weights summing to exactly 10.0 points. | TC-F07-01..TC-F07-03 |
| **F-08** | Question review submission and bank approval | Questions created by Lecturers must be submitted for review; only questions approved by the Department Head are published into the practice question bank or official exam question bank. | TC-F08-01..TC-F08-03 |

**Demo scenarios**
| Scenario ID | Type | Preconditions and data | Main steps | Expected result | Presenter |
| :---: | :---: | :--- | :--- | :--- | :--- |
| **DS-05** (DS-03a) | Main | Lecturer and Department Head accounts; subject has CLOs and an official exam structure matrix | Lecturer creates a question (practice flow by CLO or official exam flow by structure matrix) with model answer and 10.0-point Rubric and submits for review → Department Head reviews and clicks Approve | Question transitions to approved status and is published into the practice question bank or official exam question bank | Nguyễn Trọng Tốt |
| **DS-06** (DS-03b) | Exception | Lecturer enters a set of Rubric criteria whose component weights do not sum to 10.0 points | Lecturer clicks save or submit for review with an invalid Rubric | System rejects saving and displays a message requiring criteria weights to be adjusted to total 10.0 points | Nguyễn Trọng Tốt |

**Committed test cases**
| Test type | Count | Automated in CI | Responsible member(s) |
| :--- | :---: | :---: | :--- |
| Feature tests (>= 3 / feature) | 6 | 3 | Nguyễn Trọng Tốt |
| End-to-end workflow test (>= 1 / workflow) | 1 | 0 | Nguyễn Trọng Tốt |
| State transition tests (>= 1 / transition) | 4 | 2 | Nguyễn Trọng Tốt |
| NFR tests (>= 1 / NFR: NFR-03, NFR-04) | 2 | 2 | Nguyễn Trọng Tốt |
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
| **Actors** | Department Head, Examination Officer, Proctor, Examiner (Evaluating Lecturer), Student |
| **Trigger** | Examination Officer publishes the official shift exam and Proctor checks in students by seat (1–40) |
| **Business outcome** | Exam submissions and audio files are sealed and stored, pre-graded by AI, verified by the Lecturer, and published once (permanently locking shift grades), supporting 5-step appeal regrading |
| **Main entity state path** | Exam ticket: Scheduled → In progress → Submitted → AI scored → Lecturer audited → Published and locked. Appeal request: Submitted to exam office → Assigned to independent lecturer → Regraded → Decision approved or rejected |
| **Activity diagram (Report 4 section)** | Report 4 (SDD) – Activity Diagram WF-04 (Official Lab Exam, Audit & Appeal) |

**Features in this workflow**
| Feature ID | Feature | Acceptance criteria | Test case IDs |
| :---: | :--- | :--- | :--- |
| **F-09** | Official exam session, lab shift setup, and check-in | Department Head configures exam Follow-up rules (1–5 questions, default 2); Examination Officer creates exam sessions, lab shifts, assigns Proctors/Examiners, imports rosters, allocates non-duplicate exam sets, and publishes the official shift exam; Proctor only checks in students by seat number (1–40, verifying Student ID/CCCD). | TC-F09-01..TC-F09-03 |
| **F-10** | Official exam on fullscreen-locked kiosk interface | Student enters the exam only after Proctor check-in, exam publication by Examination Officer, and passing the microphone check; the kiosk locks fullscreen, auto-suspends and alerts the Proctor to log violations into the exam room anti-cheat violation log if fullscreen exit/blur exceeds 3 times, and saves the audio recording with a SHA-256 integrity hash upon submission. | TC-F10-01..TC-F10-03 |
| **F-11** | Post-exam grade verification and shift grade publication | Examiner (assigned shift Lecturer) listens to recorded audio, reviews AI feedback, and adjusts scores with a formal justification statement if needed; after 100% of shift submissions are verified, Examiner / Examination Officer publishes grades once, permanently locking the grades against direct modifications. | TC-F11-01..TC-F11-03 |
| **F-12** | Grade acknowledgement and 5-step internal appeal handling | Student acknowledges published grades or submits a formal appeal request to the Examination Officer (with both Examination Officer and Department Head receiving notifications) → Examination Officer validates the appeal → Examination Officer assigns an independent Lecturer (quota <= 24 submissions/Lecturer, excluding 100% initial shift examiners; automated proposal and manual adjustment) → Lecturer regrades on the Evidence Panel → Examination Officer approves/rejects the final decision (updating the official score through the approved appeal exception if approved; Department Head receives notification for academic oversight). | TC-F12-01..TC-F12-03 |

**Demo scenarios**
| Scenario ID | Type | Preconditions and data | Main steps | Expected result | Presenter |
| :---: | :---: | :--- | :--- | :--- | :--- |
| **DS-07** (DS-04a) | Main | Lab exam shift is scheduled with assigned students, Examination Officer, Proctor, and Lecturer | Examination Officer publishes shift exam & Proctor checks in seats (1–40) → Student checks mic, completes exam on locked kiosk, and submits → Lecturer verifies all submissions and Lecturer / Examination Officer clicks Publish Grades | Exam is saved with audio recording and SHA-256 integrity hash, pre-graded, verified, and published once successfully (permanently locking shift grades) | Lê Vũ Hoàng |
| **DS-08** (DS-04b) | Exception | Shift grades have been published and direct grade editing is locked | Attempt to directly edit a published grade (blocked) → Student submits appeal to Examination Officer → Examination Officer & Department Head receive notifications; Examination Officer validates & assigns independent Lecturer to regrade (<= 24 submissions/Lecturer, excluding 100% initial shift examiners) → Lecturer regrades and Examination Officer approves final decision | Direct grade edit is rejected due to post-publication grade locking rules; score is updated only after completing the 5-step appeal workflow approved by the Examination Officer | Lê Vũ Hoàng |

**Committed test cases**
| Test type | Count | Automated in CI | Responsible member(s) |
| :--- | :---: | :---: | :--- |
| Feature tests (>= 3 / feature) | 12 | 6 | Lê Vũ Hoàng |
| End-to-end workflow test (>= 1 / workflow) | 1 | 0 | Lê Vũ Hoàng |
| State transition tests (>= 1 / transition) | 8 | 4 | Lê Vũ Hoàng |
| NFR tests (>= 1 / NFR: NFR-05) | 1 | 0 | Lê Vũ Hoàng |
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
| **Main workflow(s) owned** | WF-01 — Interactive Oral Practice and In-Class Exam Shifts Workflow | End-to-end practice session lifecycle, STT transcript review, and follow-up flow |
| **Mandatory features owned** | F-01, F-03, F-04 | Authentication, 6-role RBAC & academic hierarchy, interactive oral practice, Rubric grading & practice history |
| **Modules / entities owned** | Oral practice subsystem, real-time audio interaction channel, student scorecard reporting | Backend APIs, background grading queue integration, and SignalR Hub |
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
| **Modules / entities owned** | Timed mock exam and quota management subsystem, Course learning outcome management, mock exam UI and academic configs | 3NF database schema, indexing, and WF-02 frontend screens |
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
| **Modules / entities owned** | Question bank and Rubric studio, AI scoring service integration, One-way grade lock mechanism, 5-step appeal management workflow, Grading failure isolation queue | AI generation/grading services, Polly retry/DLQ resilience (Admin MF-01/02, Exam Officer MF-04), and automated test suites |
| **Design decisions owned (D-IDs)** | D2 (AI assistive role under lecturer control); D3 (Separation of 6-role duties across question approval, exam operations, audit, and 5-step appeal regrading); R1 (AI retry & DLQ) | Human-in-the-loop authority and fault-tolerant background grading |
| **NFRs responsible** | NFR-03 (Grading reliability with 3x retry & DLQ); NFR-04 (Grade integrity blocking unauthorized direct edits) | AI failure recovery and post-publication grade immutability |

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
| **Modules / entities owned** | Platform user interface and 6-role access control, Fullscreen-locked exam kiosk interface, Exam room security monitoring log, Lecturer audit and evidence review console | Fullscreen focus-loss enforcement, SHA-256 audio evidence sealing, and audio audit UI |
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
| **RF-02** | Synchronize detailed specifications of the 6 user roles and the post-exam audit and 5-step appeal workflow across Form BM03, SRS, and SDD (G2). | Cross-workflow (WF-03 / WF-04) | Nguyễn Trọng Tốt | Before Defense 1.1 |
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