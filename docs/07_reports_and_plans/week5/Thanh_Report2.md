# REPORT 3 — SOFTWARE REQUIREMENT SPECIFICATION
## SECTION 1: PRODUCT OVERVIEW, SECTION 2.1: ACTORS & SECTION 2.2.2: USE CASE DESCRIPTIONS
* **Author:** Nguyen Quang Thanh (Team Leader & Lead Backend Architect)
* **Project:** LLM-based Oral Examination System for Software Engineering (FA26SE166)
* **Target Milestone:** Week 7 Report Submission (Report 3)

---

# 🇺🇸 PART 1: ENGLISH VERSION (OFFICIAL SUBMISSION FOR WORD REPORT)

## III. Software Requirement Specification

### 1. Product Overview

The **LLM-based Oral Examination System (OES)** is an AI-powered web platform designed to standardize oral practice and viva voce examinations for Software Engineering courses at FPT University. Combining speech-to-text recognition with Large Language Model (LLM) evaluation, OES grades spoken responses against Course Learning Outcome (CLO) rubrics, provides a transcript review buffer for bilingual technical terms, and generates contextual follow-up questions. The platform streamlines four core workflows—interactive practice, timed mock examinations, question bank and rubric management, and official laboratory examinations with post-exam auditing and internal appeals—serving five user roles: **Student**, **Lecturer**, **Department Head**, **Proctor**, and **Admin**.

OES operates as a web application on desktop browsers equipped with microphones across campus computer laboratories (Kiosk mode) and personal computers. The system enforces Google OAuth 2.0 and school-provisioned account authentication, department-approved question banks, fullscreen Kiosk lockdown with sealed audio evidence for official exams, and one-way grade locking upon publication, assuming stable campus network connectivity and working audio hardware in examination rooms. Externally, OES integrates with five services: **Google** (OAuth 2.0 authentication), **FLM** (course syllabus and CLO reference), **Gemini** (AI question generation, follow-up inquiry, and rubric grading), **Whisper** (speech-to-text transcription), and **Cloudflare R2** (official exam audio storage).

---

### 2. User Requirements

#### 2.1 Actors

**Table 3.2.1 : List of actors**

| # | Actor | Description |
| :-: | :--- | :--- |
| 1 | **Student** | As a user, I want:<br>1. Google OAuth 2.0 sign-in or school-provisioned username and password login, so that I can access the system quickly and securely.<br>2. Interactive oral practice in Per-Question On-Demand or Full-Session Progressive mode (3–10 questions) with a countdown transcript buffer screen, so that I can fix misrecognized English technical terms and practice at my own pace.<br>3. Timed mock examinations with voice-only input, optional AI follow-up questions, and instant CLO scorecards, so that I can familiarize myself with real exam pressure and identify knowledge gaps.<br>4. A fullscreen Kiosk interface for official laboratory exams with workstation seat check-in, 30-second microphone testing, and automatic audio sealing, so that my official exam responses are captured reliably.<br>5. A personal dashboard, practice and mock exam history, and in-app notifications, so that I can track my learning progress and upcoming exam schedules.<br>6. The ability to view published official grades, acknowledge my score, or submit an internal appeal request on the Student Portal, so that I can request a formal re-evaluation when needed. |
| 2 | **Lecturer** | As a user, I want:<br>1. An AI-assisted Question and Rubric Studio connected to FLM syllabus CLOs alongside manual authoring and AI rubric simulation tools, so that I can create oral questions, model answers, and 10.0-point rubrics efficiently.<br>2. Question pool selection (practice and/or official exam) and batch review submission, so that I can send draft questions to the Department Head for approval.<br>3. Post-exam auditing tools that separate suspicious or low-confidence AI evaluations from high-confidence ones, complete with synchronized waveform audio playback, Whisper transcripts, and AI reasoning, so that I can verify and adjust student scores with a written justification.<br>4. A one-time grade publishing action that activates a one-way grade lock and exports official shift results to Excel and digitally signed PDF formats, so that final scores are sealed for academic reporting.<br>5. A dedicated workflow to re-grade assigned student appeals, proctor assigned shifts, and view my teaching dashboard and notifications, so that I can handle re-evaluation requests and daily academic tasks. |
| 3 | **Department Head** | As a user, I want:<br>1. A question approval portal to review submitted questions and 10.0-point rubrics with Approved, Needs Revision, or Rejected decisions, as well as the ability to author questions directly, so that I can control the quality of the subject question banks.<br>2. Subject configuration tools to set mock exam structures and Bloom level distributions, so that mock assessments align with course objectives.<br>3. Official examination management functions to create exam sessions, configure follow-up question limits (1–5, default 2), input modes, and buffer durations, schedule laboratory shifts, and assign proctors and auditing lecturers, so that official exams are organized systematically.<br>4. An appeal management workspace to review student appeal requests, assign a Lecturer for re-grading, and approve the final score decision, so that grade disputes are resolved fairly.<br>5. A departmental dashboard, Dead-Letter Queue (DLQ) monitor to inspect and replay failed background AI grading tasks, and notification center, so that I can monitor managed courses, pending approvals, active exam shifts, open appeals, and unrecovered grading failures in one place. |
| 4 | **Proctor** | As a user, I want:<br>1. A 40-seat laboratory room monitor showing real-time workstation connections, assigned seat numbers (1–40), and IP bindings, so that I can supervise the entire exam room from a single screen.<br>2. Digital attendance check-in tools to verify physical student ID cards/CCCD against assigned workstation seats, so that I can prevent impersonation and wrong-seat sign-ins.<br>3. Shift control actions to open the exam shift, start the synchronized countdown timer, and close the shift, so that all candidates take the exam on a unified schedule.<br>4. Real-time security alerts for fullscreen exits or window-blur violations (3-strike limit), the ability to suspend violating candidates, and in-app shift notifications, so that I can strictly enforce exam room rules. |
| 5 | **Admin** | As a user, I want:<br>1. User management tools to search accounts, lock or unlock access, and assign roles across the five system roles for Google and school-provisioned accounts, so that platform permissions are maintained securely.<br>2. Academic administration functions to create and update semesters (without deletion), manage course and class records, and configure daily mock exam quotas per course, so that academic data and practice quotas remain consistent across terms.<br>3. Global system configuration settings to adjust practice parameters—including follow-up limits (1–5, default 2), progressive question bounds (3–10), transcript buffer time (10–300s, default 60s), and 5-minute inactivity timeouts—so that I can tune system behavior safely.<br>4. A read-only system audit log viewer and in-app notifications, so that I can trace sensitive actions such as score overrides, grade publishing, and configuration updates. |

---

#### 2.2 Use Cases

##### 2.2.2 Descriptions

**Table 3.2.2.2 : Use case descriptions**

| ID | Use Case | Actors | Use Case Description |
| :-: | :--- | :--- | :--- |
| 1 | Sign in with Google | Student, Lecturer, Department Head, Proctor, Admin | User signs in via Google OAuth 2.0 and receives a role-scoped JWT access and refresh token pair. |
| 2 | Sign in with school-provisioned account | Student, Lecturer, Department Head, Proctor, Admin | User signs in using the username and password provisioned by the university and receives a role-scoped JWT access and refresh token pair. |
| 3 | Change account password | Student, Lecturer, Department Head, Proctor, Admin | Authenticated user changes the password of their school-provisioned account and receives a refreshed session token pair. |
| 4 | Sign out | Student, Lecturer, Department Head, Proctor, Admin | Authenticated user signs out, revoking the server-side refresh token and clearing local session credentials. |
| 5 | Select practice course and mode | Student | Student selects an enrolled course and chooses between Per-Question On-Demand mode and Full-Session Progressive mode. |
| 6 | Start Per-Question practice session | Student | Student starts an on-demand practice session with single or combined difficulty levels (Easy, Medium, Hard) and receives the first question. |
| 7 | Start Full-Session practice session | Student | Student starts a progressive practice session with 3 to 10 questions ordered from Easy to Hard across Bloom levels. |
| 8 | Listen to practice question audio | Student | Student listens to the oral question prompt read aloud by the browser Web Speech TTS engine. |
| 9 | Record practice voice answer | Student | Student speaks into the microphone while Web Speech STT transcribes the spoken answer into text in real time. |
| 10 | Edit terms in practice buffer screen | Student | Student reviews the recognized transcript during the countdown buffer window (10–300s, default 60s) and fixes misrecognized English technical terms. |
| 11 | Submit single practice answer | Student | Student submits a single answer in Per-Question mode for asynchronous AI grading without storing audio files. |
| 12 | View real-time practice scorecard | Student | Student receives real-time AI grading scores, criterion-level feedback, and strengths/weaknesses via SignalR WebSocket. |
| 13 | Answer practice follow-up question | Student | Student records and submits a spoken response to an adaptive AI follow-up question triggered when the primary score falls between 4.0 and 8.0. |
| 14 | Request next on-demand question | Student | Student requests the next practice question on demand, governed by the Anti-3-Consecutive difficulty randomizer. |
| 15 | Submit batch practice answers | Student | Student submits all answers at once in Full-Session Progressive mode for batch AI evaluation without follow-up interruption. |
| 16 | Complete practice session | Student | Student manually finishes the practice session or the system auto-closes it after 5 minutes of inactivity while preserving graded scores. |
| 17 | View practice history | Student | Student browses their past practice sessions with mode, question count, completion status, and average score. |
| 18 | View practice session detail | Student | Student inspects the full transcript, criterion breakdown, and AI pedagogical feedback of a completed practice session. |
| 19 | Configure mock exam structure | Department Head | Department Head defines the number of questions, duration, and scoring weights for a course's timed mock examination. |
| 20 | Configure mock exam Bloom distribution | Department Head | Department Head sets the percentage or count of questions per Bloom cognitive level for mock exam generation. |
| 21 | Configure daily mock exam quota | Admin | Admin sets the maximum number of mock exam attempts allowed per student per day for each managed course. |
| 22 | View mock exam configuration | Admin, Department Head, Lecturer | Admin, Department Head, or Lecturer views the active mock exam blueprint, Bloom matrix, and daily quota of a course. |
| 23 | Check remaining mock exam quota | Student | Student views their remaining daily mock exam attempts for a selected course before starting an exam. |
| 24 | Select mock exam course and follow-up option | Student | Student selects a course and chooses whether to enable contextual AI follow-up questions for the timed mock exam. |
| 25 | Start timed mock examination | Student | Student initiates a timed mock exam drawn from the practice question pool after passing the daily quota check. |
| 26 | Listen to mock exam question | Student | Student listens to each mock exam question prompt synthesized via Web Speech TTS. |
| 27 | Record answer under Voice-First Gate | Student | Student records a spoken answer via microphone with direct keyboard typing locked by the Voice-First Gate. |
| 28 | Edit terms in mock exam buffer screen | Student | Student corrects misrecognized English technical terms in the transcript buffer window before confirming the question response. |
| 29 | Answer mock exam follow-up question | Student | Student answers a contextual AI follow-up question when follow-up mode is enabled for the mock exam session. |
| 30 | Navigate mock exam and track timer | Student | Student moves through the mock exam question sequence while monitoring the synchronized countdown timer. |
| 31 | Submit timed mock examination | Student | Student submits the mock exam manually or the system auto-submits when the countdown timer expires without storing audio. |
| 32 | View instant CLO mock scorecard | Student | Student views the immediate AI-graded scorecard detailing scores and feedback mapped to each Course Learning Outcome (CLO). |
| 33 | View mock examination history | Student | Student reviews past timed mock exam attempts, overall scores, and detailed CLO performance breakdowns. |
| 34 | View FLM syllabus and CLOs | Lecturer, Department Head | Lecturer or Department Head retrieves read-only course syllabus topics and CLO definitions from the FLM system. |
| 35 | Generate questions from FLM via AI | Lecturer, Department Head | Lecturer or Department Head uses Gemini AI to generate draft oral questions, model answers, and 10.0-point rubrics aligned with selected FLM CLOs. |
| 36 | Create question manually | Lecturer, Department Head | Lecturer or Department Head manually authors an oral question prompt, Bloom level, CLO mapping, and model answer (at least 50 characters), with optional technical diagram/image attachment (`PNG/JPG/WEBP <= 5MB`) and image description for multimodal AI grading. |
| 37 | Create 10.0-point rubric | Lecturer, Department Head | Lecturer or Department Head defines evaluation criteria and point weights whose sum strictly equals 10.0 points. |
| 38 | Update rubric criteria | Lecturer, Department Head | Lecturer or Department Head modifies criterion descriptions and weight distributions while maintaining the 10.0-point total constraint. |
| 39 | Select target question pools | Lecturer, Department Head | Lecturer or Department Head selects checkboxes to assign a question to the practice question pool, the official exam question pool, or both. |
| 40 | View and filter question bank | Lecturer, Department Head | Lecturer or Department Head searches and filters questions by course, CLO, Bloom level, target pool, and approval status. |
| 41 | View question and rubric detail | Lecturer, Department Head | Lecturer or Department Head inspects the full prompt, model answer, 10.0-point rubric criteria, and review status of a question. |
| 42 | Update draft or revision question | Lecturer, Department Head | Lecturer or Department Head edits a draft or revision-requested question and re-validates its 10.0-point rubric. |
| 43 | Delete draft question | Lecturer, Department Head | Lecturer or Department Head removes an unapproved draft question from their authoring workspace. |
| 44 | Simulate rubric grading with AI | Lecturer, Department Head | Lecturer or Department Head tests a draft question and 10.0-point rubric against a sample answer using the AI Simulator to verify scoring consistency. |
| 45 | Submit single question for review | Lecturer | Lecturer submits an individual draft question and its 10.0-point rubric to the Department Head for quality review. |
| 46 | Batch submit questions for review | Lecturer | Lecturer selects multiple draft questions and submits them in a single batch to the Department Head for approval. |
| 47 | View pending question review queue | Department Head | Department Head views the list of questions submitted by Lecturers awaiting quality review. |
| 48 | Approve submitted question | Department Head | Department Head approves a submitted question, publishing it into the designated practice and/or official exam banks. |
| 49 | Request question revision | Department Head | Department Head marks a submitted question as needing revision and attaches specific feedback notes for the authoring Lecturer. |
| 50 | Reject submitted question | Department Head | Department Head rejects an unsuitable question with a stated reason, excluding it from the active question banks. |
| 51 | Batch approve submitted questions | Department Head | Department Head approves multiple reviewed questions simultaneously to publish them into the subject question banks. |
| 52 | View question review history | Lecturer, Department Head | Lecturer or Department Head tracks the approval history and reviewer comments attached to a question. |
| 53 | Create official examination session | Department Head | Department Head creates an official oral examination session for a specific course and academic semester. |
| 54 | Update official examination session | Department Head | Department Head updates the schedule, title, or general settings of an official examination session before shifts begin. |
| 55 | Configure official exam rules and input mode | Department Head | Department Head configures follow-up question limits (1–5, default 2), input mode (Voice-Only or Voice with Transcript Edit), and buffer duration (10–300s). |
| 56 | View official examination sessions | Department Head, Lecturer, Proctor | Authorized staff view the list of scheduled and completed official examination sessions by semester and course. |
| 57 | Create laboratory examination shift | Department Head | Department Head creates a laboratory exam shift specifying the lab room, shift time, and exam question blueprint. |
| 58 | Update laboratory examination shift | Department Head | Department Head modifies the room assignment, time window, or shift configuration prior to shift opening. |
| 59 | Assign proctor and auditing lecturer | Department Head | Department Head assigns a Proctor to supervise the lab room and a Lecturer to audit and publish shift grades. |
| 60 | Enroll candidates into exam shift | Department Head | Department Head assigns an enrolled class roster or imports a student list into a laboratory examination shift. |
| 61 | Assign workstation seats and IP bindings | Department Head, Proctor | Department Head or Proctor maps candidate sequence numbers (Seats 1–40) to physical lab workstation IP addresses. |
| 62 | View shift roster and ticket statuses | Department Head, Lecturer, Proctor | Authorized staff view the candidate roster of a shift along with each student's exam ticket status from Scheduled to Locked. |
| 63 | Close official examination session | Department Head | Department Head closes an official examination session after all shifts have been graded, published, and locked. |
| 64 | View 40-seat laboratory room monitor | Proctor | Proctor monitors the real-time connection, seat number, IP address, and ticket state of all 40 workstations in the lab room. |
| 65 | Verify student ID and check in candidate | Proctor | Proctor verifies a student's physical ID card/CCCD against their assigned seat (1–40) and marks attendance check-in. |
| 66 | Open laboratory examination shift | Proctor | Proctor opens the examination shift so checked-in Kiosk workstations can proceed to hardware verification. |
| 67 | Start synchronized shift timer | Proctor | Proctor starts the official shift countdown timer for all active candidates in the laboratory room. |
| 68 | Monitor Kiosk security violation alerts | Proctor | Proctor receives real-time visual and audio alerts whenever a candidate's Kiosk browser loses focus or exits fullscreen. |
| 69 | Suspend violating candidate | Proctor | Proctor suspends a candidate who violates exam rules or exceeds the 3-strike window-blur limit, locking their Kiosk screen. |
| 70 | Close laboratory examination shift | Proctor | Proctor closes the active laboratory shift once all candidates have submitted their exams and signed the paper attendance sheet. |
| 71 | Check in on Kiosk workstation | Student | Student enters their student code on the lab Kiosk, which validates that the workstation IP address matches their assigned seat number (1–40). |
| 72 | Perform 30s Kiosk microphone check | Student | Student speaks into the headset microphone for 30 seconds to verify that audio input reaches at least 60 dB before entering the exam room. |
| 73 | Enter fullscreen Kiosk lockdown room | Student | Student enters the fullscreen Kiosk exam environment where system shortcuts (Alt+Tab, F11, F12, DevTools) are blocked and focus loss is tracked. |
| 74 | Record official exam voice answer | Student | Student listens to the official question and records their spoken answer while audio is streamed for forensic storage. |
| 75 | Edit transcript in Kiosk buffer screen | Student | When transcript editing is enabled for the shift, the student reviews and corrects technical terms during the configured countdown buffer window. |
| 76 | Answer official exam follow-up question | Student | Student records a spoken answer to an AI follow-up question according to the follow-up count configured by the Department Head. |
| 77 | Submit official exam and seal audio | Student | Student submits the exam; the system seals the audio recording in Cloudflare R2 with a SHA-256 hash and locks the Kiosk screen without showing any immediate score. |
| 78 | View post-exam audit queue | Lecturer, Department Head | Lecturer views shift submissions divided into Group 1 (Suspicious, low-confidence below 0.70, or failing) and Group 2 (High-confidence). |
| 79 | Inspect submission in Evidence Panel | Lecturer, Department Head | Lecturer opens the Evidence Panel to compare the question rubric, original Whisper transcript, edited transcript, and AI Chain-of-Thought reasoning. |
| 80 | Play exam audio with waveform transcript | Lecturer, Department Head | Lecturer listens to the candidate's recorded audio from Cloudflare R2 on the interactive Waveform Audio Player (1.0x–1.5x) synchronized with transcript timestamps. |
| 81 | Override AI score with justification | Lecturer | Lecturer adjusts criterion scores and enters a mandatory override justification reason before saving the updated grade. |
| 82 | Confirm audited candidate score | Lecturer | Lecturer confirms the reviewed score of a candidate ticket, marking the submission as audited. |
| 83 | Publish shift grades and lock scores | Lecturer | Once 100% of shift candidates have grades, the Lecturer publishes shift grades in a single action, activating the irreversible one-way grade lock. |
| 84 | Export shift grade report to Excel | Lecturer, Department Head | Lecturer or Department Head exports the published and locked shift grade sheet to a standardized Excel (.xlsx) file. |
| 85 | Export signed shift grade report to PDF | Lecturer, Department Head | Lecturer or Department Head exports the official shift grade report as a digitally signed PDF document for academic archival. |
| 86 | Verify grade report via public hash | Student, Lecturer, Department Head | User verifies the authenticity of an exported grade report using its cryptographic verification hash link. |
| 87 | View published official exam grade | Student | Student views their published official exam score, criterion breakdown, and examiner feedback on the Student Portal. |
| 88 | Acknowledge official exam grade | Student | Student confirms agreement with their published official grade on the Student Portal. |
| 89 | Submit internal grade appeal request | Student | Student submits an internal appeal request on the Student Portal with a detailed reason requesting a formal score re-evaluation. |
| 90 | View personal appeal status | Student | Student tracks the processing status and updated outcome of their submitted grade appeal request. |
| 91 | View departmental appeal queue | Department Head | Department Head views all incoming student grade appeal requests for courses under their department. |
| 92 | Assign lecturer for appeal re-grading | Department Head | Department Head assigns a qualified Lecturer to review and re-grade a student's appeal request, authorizing a controlled bypass of the one-way lock. |
| 93 | Re-grade appealed submission | Lecturer | Assigned Lecturer reviews the audio evidence and rubric of the appealed exam, updates the score with a written justification, and submits the re-evaluation. |
| 94 | Approve final appeal decision | Department Head | Department Head reviews the re-graded appeal result, approves the final score, and re-locks the candidate's exam ticket. |
| 95 | View appeal audit trail | Lecturer, Department Head, Admin | Authorized staff inspect the complete history of an appeal, including original AI scores, assigned reviewer, score changes, and timestamps. |
| 96 | View academic semesters list | Admin, Department Head, Lecturer, Student | User views the list of academic semesters with their code, start/end dates, and active status. |
| 97 | Create academic semester | Admin | Admin creates a new academic semester with a unique code and valid start and end dates. |
| 98 | Update academic semester | Admin | Admin updates the name, date range, or active state of an existing semester (deletion is prohibited to preserve historical data). |
| 99 | View courses catalog | Admin, Department Head, Lecturer, Student | User views the list of Software Engineering courses and their associated syllabus information. |
| 100 | Create course | Admin | Admin registers a new course with its course code, title, credits, and department assignment. |
| 101 | Update course information | Admin | Admin updates general academic course information such as title, credit count, active status, or daily mock exam quota. |
| 102 | View classes list | Admin, Department Head, Lecturer | Authorized staff view academic classes filtered by semester, course, and assigned lecturer. |
| 103 | Create class and assign lecturer | Admin, Department Head | Admin or Department Head creates a class section for a semester and assigns a teaching Lecturer. |
| 104 | Update class details | Admin, Department Head | Admin or Department Head updates the class schedule, assigned lecturer, or active status. |
| 105 | Enroll students into class | Admin, Department Head | Admin or Department Head adds or imports student accounts into a class enrollment roster. |
| 106 | View global system configurations | Admin | Admin views all operational practice and resilience parameters in the system configuration workspace. |
| 107 | Configure practice follow-up limits | Admin | Admin updates the maximum number of AI follow-up questions (1–5, default 2) allowed in Per-Question practice mode. |
| 108 | Configure progressive practice bounds | Admin | Admin updates the minimum (default 3) and maximum (default 10) question limits for Full-Session progressive practice. |
| 109 | Configure transcript buffer duration | Admin | Admin updates the default countdown transcript buffer window (10–300s, default 60s) for practice sessions. |
| 110 | Configure session inactivity timeout | Admin | Admin updates the inactivity timeout duration (default 5 minutes) before idle practice sessions auto-close. |
| 111 | View Dead-Letter Queue tasks | Department Head | Department Head monitors background AI grading tasks isolated in the Dead-Letter Queue after exhausting automatic retry attempts. |
| 112 | Inspect DLQ task error details | Department Head | Department Head views the submission payload, retry count, and failure stack trace of a specific Dead-Letter Queue task. |
| 113 | Replay failed DLQ grading task | Department Head | Department Head triggers a manual or batch replay of failed Dead-Letter Queue tasks back into the background grading queue. |
| 114 | View system audit logs | Admin | Admin searches and filters read-only system audit logs tracking authentication, score overrides, grade publishing, and configuration changes. |
| 115 | View and search user accounts | Admin | Admin views the paginated list of platform users and searches accounts by name, email, or student/staff code. |
| 116 | Filter users by role and status | Admin | Admin filters the user directory by assigned role (Student, Lecturer, Department Head, Proctor, Admin) and account lock status. |
| 117 | View user account detail | Admin | Admin inspects the profile details, Google or school-provisioned identity binding, role, and recent activity of a user account. |
| 118 | Assign or update user role | Admin | Admin assigns or updates a user's system role across the five RBAC roles for Google and school-provisioned accounts. |
| 119 | Lock or unlock user account | Admin | Admin locks a violating or inactive user account to revoke access or unlocks a restricted account. |
| 120 | View Student dashboard | Student | Student views their personal dashboard displaying enrolled courses, practice averages, remaining daily mock quota, and upcoming lab exam shifts. |
| 121 | View Lecturer dashboard | Lecturer | Lecturer views their dashboard summarizing authored questions, pending review feedbacks, assigned proctoring shifts, and post-exam audit queues. |
| 122 | View Department Head dashboard | Department Head | Department Head views departmental statistics including pending question approvals, failed DLQ grading tasks, active official exam shifts, and open appeals. |
| 123 | View in-app notifications | Student, Lecturer, Department Head, Proctor, Admin | Authenticated user opens the notification drawer or page to view system alerts (question review results, published grades, appeal updates, shift assignments). |
| 124 | Mark single notification as read | Student, Lecturer, Department Head, Proctor, Admin | Authenticated user clicks a notification item to mark it as read and navigate to the related feature screen. |
| 125 | Mark all notifications as read | Student, Lecturer, Department Head, Proctor, Admin | Authenticated user marks all unread in-app notifications as read in a single action. |

---

### 5. Requirement Appendix

#### 5.1 Business Rules

**Table 5.1 : Business Rules**

| ID | Rule Definition |
| :-: | :--- |
| BR-01 | Users across all five roles can sign in via Google OAuth 2.0 or with a username and password provisioned by the university. |
| BR-02 | Each account's role is read from the database, and no public self-registration with username and password is allowed. |
| BR-03 | Users with a university-provisioned account can change their password only after verifying their current password. |
| BR-04 | Only Admin can change user roles or lock accounts, and Admin cannot lock their own account. |
| BR-05 | A locked account cannot sign in or access any system features until unlocked by Admin. |
| BR-06 | Practice sessions draw questions only from the approved practice question pool. |
| BR-07 | Per-Question practice mode does not allow more than two consecutive questions of the same difficulty. |
| BR-08 | Per-Question practice triggers AI follow-up questions (1–5, default 2) only when the main score is between 4.0 and 8.0. |
| BR-09 | Full-Session practice requires 3 to 10 questions ordered from Easy to Hard without follow-up questions. |
| BR-10 | Students have a countdown buffer window (10–300s, default 60s) to review and fix recognized English technical terms. |
| BR-11 | Practice and mock exams only save text transcripts and never store audio files. |
| BR-12 | Practice sessions automatically close after 5 minutes of inactivity while keeping graded scores. |
| BR-13 | Mock exams draw questions from the practice pool based on the course structure and Bloom matrix set by the Department Head. |
| BR-14 | Students cannot exceed the daily mock exam quota configured for each course by the Admin. |
| BR-15 | Students can choose whether to enable contextual AI follow-up questions before starting a mock exam. |
| BR-16 | Mock exams lock keyboard typing for primary answers and require voice recording via microphone. |
| BR-17 | Mock exams auto-submit when the timer expires and immediately display a CLO-mapped scorecard. |
| BR-18 | Course syllabi and CLOs are read from the FLM system and cannot be modified by OES. |
| BR-19 | Every question must have a model answer of at least 50 characters and be assigned to at least one question pool; manually created questions may optionally attach a validated technical diagram/image (`PNG/JPG/WEBP <= 5MB`) with a description for multimodal AI grading. |
| BR-20 | The total weight of all rubric criteria for a question must equal exactly 10.0 points. |
| BR-21 | Questions created by a Lecturer start as drafts and must be submitted to the Department Head for approval. |
| BR-22 | Only the Department Head can approve, request revision for, or reject submitted questions. |
| BR-23 | Only the Department Head can create official exam sessions, lab shifts, and official exam rules. |
| BR-24 | Each candidate is assigned a seat number (1–40) bound to a lab workstation IP and must be checked in by the Proctor. |
| BR-25 | Official exam tickets must follow seven states: SCHEDULED, IN_PROGRESS, SUBMITTED, AI_GRADED, AUDITED, PUBLISHED, and LOCKED. |
| BR-26 | Candidates must pass a 30-second microphone check reaching at least 60 dB before entering the Kiosk exam room. |
| BR-27 | Exiting fullscreen or losing window focus more than 3 times alerts the Proctor for candidate suspension. |
| BR-28 | Official exam audio is saved to Cloudflare R2 as STT_MSSV.webm and sealed with a SHA-256 hash. |
| BR-29 | Kiosk terminals lock immediately after submission and never show immediate scores or appeal options. |
| BR-30 | AI-graded official exams are split into Group 1 (suspicious or confidence below 0.70) and Group 2 (high confidence) for auditing. |
| BR-31 | Lecturers must enter a written reason whenever they override an AI-generated score. |
| BR-32 | Lecturers can publish shift grades only once after 100% of submitted exams in the shift are audited. |
| BR-33 | Publishing shift grades activates a one-way lock that prevents any unauthorized score edits or deletions. |
| BR-34 | Students can submit an appeal on the Student Portal, and the Department Head must assign a different Lecturer to re-grade it. |
| BR-35 | Exported Excel and digitally signed PDF grade reports include a public hash for authenticity verification. |
| BR-36 | Academic semesters must have valid start/end dates and can be updated by Admin, but can never be deleted. |
| BR-37 | Admin can update general course details and configure daily mock exam quotas, but cannot configure official exam rules or exam session structures. |
| BR-38 | A student can enroll in at most one active class per course in the same semester. |
| BR-39 | Only Admin can update global practice settings (follow-up 1–5, progressive 3–10, buffer 10–300s, timeout 5m). |
| BR-40 | Updated global practice settings apply only to new practice sessions and do not affect active sessions. |
| BR-41 | If automatic AI grading fails after 3 attempts, the student's submission is preserved and moved to the Dead-Letter Queue for recovery. |
| BR-42 | Tasks in the Dead-Letter Queue are retried after 5 minutes or replayed by the Department Head so no submission is lost. |
| BR-43 | Replaying a Dead-Letter Queue task will not overwrite a submission that has already been graded. |
| BR-44 | All sensitive actions (login, role changes, score overrides, grade publishing, config updates) are recorded in audit logs. |
| BR-45 | System audit logs are read-only and cannot be edited or deleted by any user, including Admin. |

---

#### 5.2 Common Requirements

**Table 5.2 : Common Requirements**

| ID | Rule Definition |
| :-: | :--- |
| CR-01 | All user input is validated on the client and re-validated on the server before processing. |
| CR-02 | Protected routes verify the user session before rendering and show a loading state while checking. |
| CR-03 | Unauthorized role access redirects to the 403 page, and unknown URLs redirect to the 404 page. |
| CR-04 | Real-time updates (practice scores, lab room status, alerts, notifications) are pushed live via SignalR without page refresh. |
| CR-05 | Multi-step exam flows (practice, mock exam, Kiosk check-in) run on a single route to prevent skipping steps via URL. |
| CR-06 | Simple catalog and configuration records are created and edited in modal dialogs over the list screen. |
| CR-07 | All large data lists support server-side pagination, keyword search, and filtering. |
| CR-08 | All system timestamps are stored in UTC and displayed in the user's local time zone (UTC+7). |
| CR-09 | Student submissions are saved to the database first before queuing for background AI grading. |
| CR-10 | English technical terms spoken inside Vietnamese answers are kept intact and editable during the buffer countdown. |
| CR-11 | Irreversible actions (exam submission, candidate suspension, grade publishing, account locking) require a confirmation dialog. |
| CR-12 | All user-facing notifications and error messages use the standard application messages list in Section 5.3. |

---
---

# 🇻🇳 PHẦN 2: BẢN TIẾNG VIỆT (DÀNH CHO ĐỐI SOÁT & GIẢI TRÌNH BẢO VỆ)

## III. Đặc Tả Yêu Cầu Phần Mềm (Software Requirement Specification)

### 1. Tổng Quan Sản Phẩm (Product Overview)

**Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM (OES)** là nền tảng web ứng dụng AI nhằm chuẩn hóa việc luyện tập và tổ chức thi vấn đáp cho các môn Kỹ thuật Phần mềm tại Trường Đại học FPT. Kết hợp công nghệ nhận diện giọng nói và mô hình ngôn ngữ lớn (LLM), hệ thống chấm điểm câu trả lời nói theo Barem gắn với Chuẩn đầu ra môn học (CLO), hỗ trợ màn hình đệm rà soát thuật ngữ chuyên ngành song ngữ và sinh câu hỏi phụ theo ngữ cảnh. OES kết nối bốn luồng nghiệp vụ cốt lõi—tự luyện tập tương tác, thi thử bấm giờ, quản lý ngân hàng câu hỏi và barem, và thi chính thức tại phòng Lab kèm hậu kiểm và phúc khảo nội bộ—phục vụ 5 vai trò người dùng: **Student**, **Lecturer**, **Department Head**, **Proctor**, và **Admin**.

OES vận hành trên trình duyệt máy tính có trang bị micro tại các phòng máy tính của trường (chế độ Kiosk) và máy tính cá nhân. Hệ thống áp dụng các ràng buộc: đăng nhập qua Google OAuth 2.0 hoặc tài khoản do nhà trường cấp, ngân hàng đề được bộ môn phê duyệt, khóa toàn màn hình Kiosk kèm niêm phong tệp ghi âm ở kỳ thi chính thức, và khóa điểm một chiều sau khi công bố, với giả định hạ tầng mạng nội bộ ổn định và thiết bị âm thanh phòng thi hoạt động tốt. Hệ thống tích hợp với 5 dịch vụ bên ngoài: **Google** (xác thực OAuth 2.0), **FLM** (tham chiếu đề cương và CLO), **Gemini** (AI sinh câu hỏi, hỏi phụ và chấm điểm theo barem), **Whisper** (bóc băng giọng nói thành văn bản) và **Cloudflare R2** (lưu trữ tệp ghi âm bài thi chính thức).

---

### 2. Yêu Cầu Người Dùng (User Requirements)

#### 2.1 Tác Nhân (Actors)

**Bảng 3.2.1 : Danh sách tác nhân**

| # | Tác nhân | Mô tả |
| :-: | :--- | :--- |
| 1 | **Student** | Là một người dùng, tôi muốn:<br>1. Đăng nhập qua Google OAuth 2.0 hoặc bằng tài khoản username và mật khẩu do nhà trường cấp, để truy cập hệ thống nhanh chóng và an toàn.<br>2. Luyện tập vấn đáp theo chế độ Từng câu hoặc Toàn phiên (3–10 câu) kèm màn hình đệm đếm ngược, để sửa các thuật ngữ tiếng Anh chuyên ngành bị nhận diện sai và tự rèn luyện theo tốc độ cá nhân.<br>3. Thi thử bấm giờ bắt buộc trả lời bằng giọng nói, tùy chọn bật/tắt câu hỏi phụ và nhận bảng điểm CLO tức thì, để làm quen áp lực phòng thi và biết lỗ hổng kiến thức.<br>4. Giao diện Kiosk toàn màn hình khi thi chính thức tại phòng Lab có check-in theo ghế, kiểm tra micro 30 giây và tự động niêm phong ghi âm, để bài thi được ghi nhận tin cậy.<br>5. Bảng tổng quan cá nhân, lịch sử luyện tập/thi thử và thông báo in-app, để theo dõi tiến độ học tập và lịch thi sắp tới.<br>6. Xem điểm thi chính thức sau khi công bố, xác nhận điểm hoặc gửi đơn phúc khảo nội bộ trên Student Portal, để yêu cầu chấm lại khi cần thiết. |
| 2 | **Lecturer** | Là một người dùng, tôi muốn:<br>1. Công cụ soạn câu hỏi và Barem 10.0 điểm tích hợp AI theo CLO từ FLM cùng chế độ soạn tay và trình chấm thử AI Simulator, để xây dựng câu hỏi và đáp án mẫu hiệu quả.<br>2. Chọn kho lưu trữ (luyện tập và/hoặc thi thật) và gửi duyệt theo đợt, để chuyển câu hỏi nháp lên Trưởng Bộ môn thẩm định.<br>3. Công cụ hậu kiểm sau ca thi tách rõ nhóm bài AI đáng nghi ngờ với nhóm tin cậy cao, kèm trình nghe sóng âm, bản bóc băng Whisper và lập luận của AI, để kiểm tra và sửa điểm kèm lý do giải trình.<br>4. Chức năng công bố điểm một lần kích hoạt khóa điểm một chiều và xuất kết quả ra file Excel, PDF có chữ ký số, để chốt điểm hợp lệ phục vụ báo cáo khảo thí.<br>5. Luồng chấm lại các đơn phúc khảo được giao, coi thi khi được phân công cùng bảng tổng quan và thông báo, để xử lý yêu cầu chấm lại và theo dõi công việc giảng dạy. |
| 3 | **Department Head** | Là một người dùng, tôi muốn:<br>1. Cổng phê duyệt câu hỏi để rà soát đề và barem 10.0 điểm do giảng viên gửi lên (Phê duyệt, Yêu cầu sửa, Từ chối) hoặc tự tạo câu hỏi trực tiếp, để kiểm soát chất lượng ngân hàng đề.<br>2. Công cụ cấu hình môn học để thiết lập cấu trúc đề thi thử và tỷ lệ mức Bloom, để bài thi thử bám sát mục tiêu môn học.<br>3. Chức năng quản lý kỳ thi chính thức để tạo kỳ thi, cấu hình số câu hỏi phụ (1–5, mặc định 2), chế độ làm bài và thời gian đệm, lập ca thi phòng Lab và phân công giám thị/giảng viên, để tổ chức kỳ thi bài bản.<br>4. Không gian quản lý phúc khảo để xem đơn khiếu nại của sinh viên, giao giảng viên chấm lại và duyệt quyết định điểm cuối cùng, để giải quyết phúc khảo công bằng.<br>5. Bảng tổng quan bộ môn, giám sát hàng đợi Dead-Letter Queue (DLQ) để phục hồi bài thi lỗi và trung tâm thông báo, để theo dõi các môn phụ trách, đề chờ duyệt, ca thi đang mở, đơn phúc khảo và sự cố chấm thi tại một nơi. |
| 4 | **Proctor** | Là một người dùng, tôi muốn:<br>1. Màn hình giám sát phòng Lab 40 máy hiển thị trạng thái kết nối, số ghế (1–40) và địa chỉ IP theo thời gian thực, để bao quát cả phòng thi trên một màn hình.<br>2. Công cụ điểm danh số hóa đối chiếu thẻ sinh viên/CCCD với số ghế máy trạm được xếp, để ngăn chặn thi hộ hoặc ngồi sai máy.<br>3. Các thao tác điều khiển mở ca thi, bắt đầu tính giờ đồng bộ và đóng ca thi, để toàn bộ thí sinh làm bài đúng tiến độ.<br>4. Cảnh báo an ninh tức thời khi thí sinh thoát toàn màn hình hoặc chuyển cửa sổ (quá 3 lần), quyền đình chỉ thi và thông báo ca thi, để duy trì nghiêm kỷ luật phòng thi. |
| 5 | **Admin** | Là một người dùng, tôi muốn:<br>1. Công cụ quản lý người dùng để tìm kiếm tài khoản, khóa/mở quyền truy cập và gán vai trò trong 5 vai trò hệ thống cho tài khoản Google và tài khoản nhà trường cấp, để duy trì phân quyền an toàn.<br>2. Chức năng quản trị học vụ để tạo mới, cập nhật học kỳ (không xóa), quản lý danh mục môn học, lớp học và cấu hình hạn ngạch thi thử hàng ngày theo môn, để dữ liệu đào tạo và hạn ngạch ôn luyện nhất quán qua các kỳ.<br>3. Màn hình cấu hình hệ thống để điều chỉnh các tham số luyện tập—gồm giới hạn câu hỏi phụ (1–5, mặc định 2), số câu luyện tập tiến trình (3–10), thời gian đệm sửa từ (10–300s, mặc định 60s) và thời gian timeout không tương tác (5 phút)—để tinh chỉnh hệ thống an toàn.<br>4. Trình xem nhật ký kiểm toán hệ thống chỉ đọc và thông báo hệ thống, để truy vết các thao tác nhạy cảm như sửa điểm, công bố điểm và thay đổi cấu hình. |

---

#### 2.2 Các Ca Sử Dụng (Use Cases)

##### 2.2.2 Mô Tả Chi Tiết Use Case (Descriptions)

**Bảng 3.2.2.2 : Mô tả các ca sử dụng (Use case descriptions)**

| Mã | Tên Use Case | Tác nhân | Mô tả Use Case |
| :-: | :--- | :--- | :--- |
| 1 | Đăng nhập bằng Google | Student, Lecturer, Department Head, Proctor, Admin | Người dùng đăng nhập qua Google OAuth 2.0 và nhận cặp JWT access token, refresh token theo vai trò. |
| 2 | Đăng nhập bằng tài khoản trường cấp | Student, Lecturer, Department Head, Proctor, Admin | Người dùng đăng nhập bằng username và mật khẩu do nhà trường cấp và nhận cặp JWT access token, refresh token theo vai trò. |
| 3 | Đổi mật khẩu tài khoản | Student, Lecturer, Department Head, Proctor, Admin | Người dùng đã đăng nhập thay đổi mật khẩu của tài khoản do nhà trường cấp và nhận lại cặp token phiên mới. |
| 4 | Đăng xuất | Student, Lecturer, Department Head, Proctor, Admin | Người dùng kết thúc phiên làm việc, thu hồi refresh token phía máy chủ và xóa thông tin xác thực trên trình duyệt. |
| 5 | Chọn môn và chế độ luyện tập | Student | Sinh viên chọn môn học đang theo học và chọn chế độ Luyện từng câu (Per-Question) hoặc Luyện toàn phiên (Full-Session). |
| 6 | Bắt đầu phiên luyện từng câu | Student | Sinh viên khởi tạo phiên Per-Question với mức độ khó đơn hoặc kết hợp (Dễ, Trung bình, Khó) và nhận câu hỏi đầu tiên. |
| 7 | Bắt đầu phiên luyện toàn bộ | Student | Sinh viên khởi tạo phiên Full-Session gồm 3 đến 10 câu hỏi sắp xếp tăng dần từ Dễ đến Khó theo thang Bloom. |
| 8 | Nghe đọc câu hỏi luyện tập | Student | Sinh viên nghe hệ thống phát âm nội dung câu hỏi thông qua Web Speech TTS trên trình duyệt. |
| 9 | Ghi âm câu trả lời luyện tập | Student | Sinh viên nói qua micro và xem văn bản được bóc băng trực tiếp theo thời gian thực bằng Web Speech STT. |
| 10 | Sửa thuật ngữ ở màn hình đệm | Student | Sinh viên rà soát văn bản bóc băng trong thời gian đếm ngược (10–300s, mặc định 60s) và sửa các thuật ngữ tiếng Anh chuyên ngành. |
| 11 | Nộp từng câu trả lời luyện tập | Student | Sinh viên nộp câu trả lời ở chế độ Per-Question vào hàng đợi chấm AI mà không lưu trữ tệp âm thanh. |
| 12 | Nhận bảng điểm luyện tập tức thì | Student | Sinh viên nhận điểm số, nhận xét từng tiêu chí rubric và phân tích điểm mạnh/yếu qua kết nối SignalR WebSocket. |
| 13 | Trả lời câu hỏi phụ luyện tập | Student | Sinh viên ghi âm và nộp câu trả lời cho câu hỏi phụ AI khi điểm câu chính nằm trong khoảng 4.0 đến 8.0. |
| 14 | Lấy câu hỏi luyện tập tiếp theo | Student | Sinh viên yêu cầu câu hỏi tiếp theo theo cơ chế chống lặp quá 2 câu cùng độ khó liên tiếp (Anti-3-Consecutive). |
| 15 | Nộp trọn gói bài luyện toàn phiên | Student | Sinh viên nộp toàn bộ câu trả lời trong chế độ Full-Session để AI chấm hàng loạt không ngắt quãng bởi câu hỏi phụ. |
| 16 | Kết thúc phiên luyện tập | Student | Sinh viên chủ động đóng phiên hoặc hệ thống tự đóng sau 5 phút không tương tác và bảo toàn điểm đã chấm. |
| 17 | Xem lịch sử luyện tập | Student | Sinh viên tra cứu danh sách các phiên luyện tập đã thực hiện kèm chế độ, số câu và điểm trung bình. |
| 18 | Xem chi tiết bảng điểm phiên luyện | Student | Sinh viên xem lại chi tiết câu hỏi, văn bản trả lời và nhận xét từng tiêu chí của một phiên luyện tập. |
| 19 | Cấu hình cấu trúc đề thi thử | Department Head | Trưởng Bộ môn thiết lập số lượng câu hỏi, thời gian làm bài và cấu trúc điểm cho bài thi thử của môn học. |
| 20 | Cấu hình ma trận Bloom thi thử | Department Head | Trưởng Bộ môn thiết lập tỷ lệ phân bổ câu hỏi theo các mức nhận thức Bloom cho đề thi thử. |
| 21 | Cấu hình hạn ngạch thi thử mỗi ngày | Admin | Admin đặt số lượt thi thử tối đa mỗi ngày cho sinh viên theo từng môn học. |
| 22 | Xem cấu hình và hạn ngạch thi thử | Admin, Department Head, Lecturer | Admin, Trưởng Bộ môn hoặc Giảng viên xem cấu trúc đề thi thử, ma trận Bloom và hạn ngạch hiện hành của môn học. |
| 23 | Kiểm tra lượt thi thử còn lại | Student | Sinh viên xem số lượt thi thử còn lại trong ngày của môn học trước khi bắt đầu làm bài. |
| 24 | Chọn môn và tùy chọn hỏi phụ thi thử | Student | Sinh viên chọn môn thi thử và chọn bật hoặc tắt chế độ hỏi phụ AI theo ngữ cảnh. |
| 25 | Bắt đầu ca thi thử bấm giờ | Student | Sinh viên khởi tạo bài thi thử rút từ ngân hàng câu hỏi luyện tập khi vẫn còn hạn ngạch trong ngày. |
| 26 | Nghe câu hỏi thi thử qua TTS | Student | Sinh viên nghe hệ thống đọc câu hỏi thi thử bằng giọng nói tổng hợp Web Speech TTS. |
| 27 | Trả lời giọng nói qua Voice-First Gate | Student | Sinh viên bắt buộc trả lời bằng giọng nói qua micro trong khi bàn phím nhập liệu bị khóa bởi Voice-First Gate. |
| 28 | Sửa thuật ngữ ở màn hình đệm thi thử | Student | Sinh viên chỉnh sửa các thuật ngữ tiếng Anh chuyên ngành bị nhận diện sai trên màn hình đệm trước khi chốt câu trả lời. |
| 29 | Trả lời câu hỏi phụ trong thi thử | Student | Sinh viên trả lời câu hỏi phụ đào sâu theo ngữ cảnh do AI sinh ra nếu phiên thi thử có bật tùy chọn hỏi phụ. |
| 30 | Chuyển câu hỏi và theo dõi đồng hồ thi | Student | Sinh viên chuyển qua các câu hỏi trong đề thi thử và theo dõi đồng hồ đếm ngược đồng bộ với máy chủ. |
| 31 | Nộp bài thi thử bấm giờ | Student | Sinh viên bấm nộp bài hoặc hệ thống tự động thu bài khi hết giờ mà không lưu tệp ghi âm. |
| 32 | Xem bảng điểm thi thử theo CLO | Student | Sinh viên xem ngay bảng điểm chi tiết từng tiêu chí gắn với Chuẩn đầu ra môn học (CLO) ngay sau khi nộp bài. |
| 33 | Xem lịch sử thi thử | Student | Sinh viên tra cứu lịch sử các lần thi thử trước đó cùng bảng điểm CLO chi tiết. |
| 34 | Xem đề cương và CLO từ FLM | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn đọc danh sách chủ đề và Chuẩn đầu ra (CLO) của môn học từ hệ thống FLM. |
| 35 | Dùng AI sinh câu hỏi và barem từ FLM | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn dùng Gemini AI sinh câu hỏi nháp, đáp án mẫu và barem 10.0 điểm bám sát CLO từ FLM. |
| 36 | Soạn câu hỏi vấn đáp thủ công | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn tự soạn nội dung câu hỏi, chọn mức Bloom, gắn CLO, viết đáp án mẫu (tối thiểu 50 ký tự) và có thể đính kèm hình ảnh/sơ đồ kỹ thuật (`PNG/JPG/WEBP <= 5MB`) kèm mô tả phục vụ chấm điểm AI đa phương thức. |
| 37 | Tạo barem chấm điểm 10.0 | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn thiết lập các tiêu chí chấm điểm có tổng trọng số bằng chính xác 10.0 điểm. |
| 38 | Cập nhật tiêu chí barem 10.0 | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn chỉnh sửa nội dung và điểm số các tiêu chí trong barem đảm bảo tổng bằng 10.0 điểm. |
| 39 | Chọn kho lưu trữ câu hỏi | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn tích chọn đưa câu hỏi vào kho luyện tập, kho thi chính thức hoặc cả hai kho. |
| 40 | Xem và lọc ngân hàng câu hỏi | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn tìm kiếm, lọc câu hỏi theo môn học, CLO, mức Bloom, kho lưu trữ và trạng thái duyệt. |
| 41 | Xem chi tiết câu hỏi và barem | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn xem toàn bộ nội dung câu hỏi, đáp án mẫu, barem 10.0 điểm và trạng thái phê duyệt. |
| 42 | Cập nhật câu hỏi nháp hoặc cần sửa | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn chỉnh sửa câu hỏi đang ở trạng thái bản nháp hoặc cần chỉnh sửa. |
| 43 | Xóa câu hỏi nháp | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn xóa câu hỏi nháp chưa được phê duyệt khỏi danh sách soạn thảo. |
| 44 | Chấm thử barem bằng AI Simulator | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn chạy thử AI chấm đáp án mẫu theo barem 10.0 điểm để kiểm tra độ chuẩn xác trước khi gửi duyệt. |
| 45 | Gửi duyệt đơn lẻ một câu hỏi | Lecturer | Giảng viên gửi một câu hỏi nháp kèm barem 10.0 điểm lên Trưởng Bộ môn thẩm định. |
| 46 | Gửi duyệt hàng loạt câu hỏi | Lecturer | Giảng viên chọn nhiều câu hỏi nháp và gửi duyệt theo đợt lên Trưởng Bộ môn. |
| 47 | Xem danh sách câu hỏi chờ duyệt | Department Head | Trưởng Bộ môn xem danh sách các câu hỏi và barem do giảng viên gửi lên đang chờ thẩm định. |
| 48 | Phê duyệt câu hỏi vào ngân hàng đề | Department Head | Trưởng Bộ môn phê duyệt câu hỏi để đưa chính thức vào kho luyện tập và/hoặc kho thi thật. |
| 49 | Yêu cầu chỉnh sửa câu hỏi | Department Head | Trưởng Bộ môn trả câu hỏi về trạng thái yêu cầu sửa kèm nhận xét chi tiết cho giảng viên. |
| 50 | Từ chối câu hỏi | Department Head | Trưởng Bộ môn từ chối câu hỏi không đạt yêu cầu kèm lý do cụ thể. |
| 51 | Phê duyệt hàng loạt câu hỏi | Department Head | Trưởng Bộ môn phê duyệt cùng lúc nhiều câu hỏi đạt chuẩn vào ngân hàng đề môn học. |
| 52 | Xem lịch sử duyệt câu hỏi | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn xem lịch sử các lần gửi duyệt và ghi chú thẩm định của câu hỏi. |
| 53 | Tạo kỳ thi vấn đáp chính thức | Department Head | Trưởng Bộ môn khởi tạo kỳ thi chính thức cho môn học trong học kỳ. |
| 54 | Cập nhật kỳ thi chính thức | Department Head | Trưởng Bộ môn chỉnh sửa thông tin chung và thời gian của kỳ thi chính thức trước khi ca thi diễn ra. |
| 55 | Cấu hình hỏi phụ và chế độ làm bài thi thật | Department Head | Trưởng Bộ môn cấu hình số câu hỏi phụ (1–5, mặc định 2), chế độ làm bài (chỉ giọng nói hoặc cho phép sửa thuật ngữ) và thời gian đệm (10–300s). |
| 56 | Xem danh sách kỳ thi chính thức | Department Head, Lecturer, Proctor | Người dùng có thẩm quyền xem danh sách các kỳ thi chính thức theo học kỳ và môn học. |
| 57 | Tạo ca thi phòng Lab | Department Head | Trưởng Bộ môn tạo ca thi gắn với phòng máy Lab, kíp thi và cấu trúc đề thi. |
| 58 | Cập nhật ca thi phòng Lab | Department Head | Trưởng Bộ môn cập nhật phòng thi, thời gian hoặc cấu hình ca thi trước khi mở ca. |
| 59 | Phân công giám thị và giảng viên chấm | Department Head | Trưởng Bộ môn gán Giám thị coi thi phòng Lab và Giảng viên phụ trách hậu kiểm, công bố điểm. |
| 60 | Gán danh sách thí sinh vào ca thi | Department Head | Trưởng Bộ môn thêm danh sách sinh viên từ lớp học phần hoặc nhập danh sách thí sinh vào ca thi. |
| 61 | Gán số ghế và địa chỉ IP máy trạm | Department Head, Proctor | Trưởng Bộ môn hoặc Giám thị ánh xạ số thứ tự thí sinh (Ghế 1–40) với địa chỉ IP máy trạm trong phòng Lab. |
| 62 | Xem danh sách thí sinh và trạng thái vé thi | Department Head, Lecturer, Proctor | Người dùng xem danh sách thí sinh trong ca thi cùng trạng thái vé thi từ lúc xếp lịch đến khi khóa điểm. |
| 63 | Đóng kỳ thi chính thức | Department Head | Trưởng Bộ môn đóng kỳ thi sau khi toàn bộ các ca thi đã hoàn tất công bố và khóa điểm. |
| 64 | Xem màn hình giám sát 40 máy phòng Lab | Proctor | Giám thị theo dõi trực tiếp trạng thái kết nối, số ghế, địa chỉ IP và tiến độ của 40 máy trạm trong phòng thi. |
| 65 | Điểm danh và đối chiếu thẻ sinh viên | Proctor | Giám thị kiểm tra thẻ sinh viên/CCCD đối chiếu với số ghế (1–40) và xác nhận thí sinh có mặt. |
| 66 | Mở ca thi phòng Lab | Proctor | Giám thị mở ca thi để các máy trạm Kiosk của thí sinh đã điểm danh được phép bắt đầu kiểm tra phần cứng. |
| 67 | Bắt đầu tính giờ làm bài đồng bộ | Proctor | Giám thị kích hoạt đồng hồ đếm ngược thời gian làm bài đồng bộ cho toàn bộ thí sinh trong ca thi. |
| 68 | Giám sát cảnh báo vi phạm an ninh Kiosk | Proctor | Giám thị nhận cảnh báo tức thời khi máy trạm của thí sinh thoát toàn màn hình hoặc mất tiêu điểm cửa sổ. |
| 69 | Đình chỉ thí sinh vi phạm quy chế | Proctor | Giám thị đình chỉ thi đối với thí sinh vi phạm quy chế hoặc mất tiêu điểm cửa sổ quá 3 lần. |
| 70 | Đóng ca thi phòng Lab | Proctor | Giám thị đóng ca thi sau khi toàn bộ thí sinh đã nộp bài và ký biên bản xác nhận nộp bài giấy. |
| 71 | Check-in tại máy trạm Kiosk | Student | Sinh viên nhập mã số sinh viên trên máy Kiosk; hệ thống kiểm tra địa chỉ IP máy trạm khớp với số ghế được xếp (1–40). |
| 72 | Kiểm tra micro 30 giây tại Kiosk | Student | Sinh viên nói thử vào micro trong 30 giây và phải đạt ngưỡng âm lượng tối thiểu 60 dB trước khi vào phòng thi. |
| 73 | Vào phòng thi Kiosk khóa toàn màn hình | Student | Sinh viên vào giao diện thi toàn màn hình chặn hoàn toàn phím tắt (Alt+Tab, F11, F12, DevTools) và bắt lỗi chuyển cửa sổ. |
| 74 | Ghi âm câu trả lời thi chính thức | Student | Sinh viên nghe câu hỏi và phát biểu trả lời qua micro, luồng âm thanh được ghi lại để niêm phong lưu trữ. |
| 75 | Rà soát văn bản ở màn hình đệm Kiosk | Student | Khi ca thi bật chế độ sửa văn bản, sinh viên rà soát và sửa thuật ngữ chuyên ngành trong thời gian đệm đếm ngược. |
| 76 | Trả lời câu hỏi phụ trong kỳ thi chính thức | Student | Sinh viên ghi âm câu trả lời cho các câu hỏi phụ AI theo số lượng do Trưởng Bộ môn cấu hình cho kỳ thi. |
| 77 | Nộp bài thi chính thức và niêm phong âm thanh | Student | Sinh viên nộp bài; hệ thống tải tệp ghi âm kèm mã băm SHA-256 lên Cloudflare R2 và khóa màn hình Kiosk (không hiện điểm liền). |
| 78 | Xem danh sách bài thi cần hậu kiểm | Lecturer, Department Head | Giảng viên xem danh sách bài thi được chia thành Nhóm 1 (Đáng nghi ngờ, độ tin cậy dưới 0.70 hoặc điểm liệt) và Nhóm 2 (Độ tin cậy cao). |
| 79 | Đối chiếu bài làm trên Evidence Panel | Lecturer, Department Head | Giảng viên mở Evidence Panel để xem barem 10.0 điểm, bản bóc băng Whisper gốc, bản sinh viên sửa và lập luận CoT của AI. |
| 80 | Nghe lại ghi âm trên trình phát sóng âm | Lecturer, Department Head | Giảng viên nghe tệp ghi âm bài thi từ Cloudflare R2 qua Waveform Audio Player (1.0x–1.5x) đồng bộ theo mốc thời gian văn bản. |
| 81 | Sửa điểm AI kèm lý do giải trình | Lecturer | Giảng viên điều chỉnh điểm từng tiêu chí rubric và bắt buộc nhập lý do giải trình trước khi lưu. |
| 82 | Xác nhận điểm hậu kiểm cho vé thi | Lecturer | Giảng viên xác nhận kết quả rà soát của một thí sinh, chuyển trạng thái bài thi sang đã hậu kiểm. |
| 83 | Công bố điểm ca thi và khóa một chiều | Lecturer | Khi 100% thí sinh trong ca đã có điểm, Giảng viên bấm "Công Bố Điểm" 1 lần duy nhất để xuất bản điểm và kích hoạt khóa điểm một chiều. |
| 84 | Xuất bảng điểm ca thi ra file Excel | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn xuất bảng điểm chính thức của ca thi ra tệp Excel (.xlsx) theo biểu mẫu khảo thí. |
| 85 | Xuất biên bản điểm PDF có chữ ký số | Lecturer, Department Head | Giảng viên hoặc Trưởng Bộ môn xuất báo cáo điểm ca thi ra tệp PDF có chữ ký số để lưu trữ. |
| 86 | Xác thực bảng điểm qua mã băm công khai | Student, Lecturer, Department Head | Người dùng tra cứu và kiểm tra tính toàn vẹn của chứng chỉ bảng điểm thông qua đường dẫn mã băm xác thực. |
| 87 | Xem điểm thi chính thức sau công bố | Student | Sinh viên xem điểm thi chính thức, điểm thành phần từng tiêu chí và nhận xét trên Student Portal. |
| 88 | Xác nhận đồng ý điểm thi chính thức | Student | Sinh viên bấm xác nhận đồng ý với kết quả điểm thi chính thức trên Student Portal. |
| 89 | Nộp đơn phúc khảo nội bộ | Student | Sinh viên gửi đơn phúc khảo kèm lý do giải trình trên Student Portal để yêu cầu chấm lại. |
| 90 | Xem trạng thái đơn phúc khảo cá nhân | Student | Sinh viên theo dõi tiến độ xử lý và kết quả điểm sau phúc khảo trên Student Portal. |
| 91 | Xem danh sách đơn phúc khảo cấp bộ môn | Department Head | Trưởng Bộ môn xem toàn bộ danh sách đơn phúc khảo của sinh viên thuộc các môn bộ môn quản lý. |
| 92 | Giao giảng viên chấm lại đơn phúc khảo | Department Head | Trưởng Bộ môn phân công một Giảng viên chấm lại đơn phúc khảo, mở quyền cập nhật có kiểm soát qua khóa một chiều. |
| 93 | Chấm lại bài thi phúc khảo | Lecturer | Giảng viên được giao nghe lại ghi âm, rà soát barem, cập nhật điểm mới kèm giải trình cho bài thi phúc khảo. |
| 94 | Phê duyệt kết quả phúc khảo cuối cùng | Department Head | Trưởng Bộ môn duyệt kết quả chấm lại, chốt điểm phúc khảo chính thức và khóa lại bản ghi vé thi. |
| 95 | Xem lịch sử thẩm định phúc khảo | Lecturer, Department Head, Admin | Người dùng có thẩm quyền xem toàn bộ vết kiểm toán của đơn phúc khảo từ lúc nộp đến khi chốt điểm mới. |
| 96 | Xem danh sách học kỳ | Admin, Department Head, Lecturer, Student | Người dùng xem danh sách các học kỳ cùng mã kỳ, ngày bắt đầu/kết thúc và trạng thái hoạt động. |
| 97 | Tạo mới học kỳ | Admin | Admin tạo học kỳ mới với mã học kỳ duy nhất và khoảng thời gian hợp lệ. |
| 98 | Cập nhật thông tin học kỳ | Admin | Admin cập nhật tên, thời gian hoặc trạng thái kích hoạt của học kỳ (cấm xóa học kỳ để bảo toàn dữ liệu). |
| 99 | Xem danh mục môn học | Admin, Department Head, Lecturer, Student | Người dùng xem danh sách các môn học Kỹ thuật Phần mềm và thông tin đề cương môn. |
| 100 | Tạo mới môn học | Admin | Admin thêm môn học mới kèm mã môn, tên môn, số tín chỉ và bộ môn quản lý. |
| 101 | Cập nhật thông tin môn học | Admin | Admin cập nhật thông tin học vụ chung của môn học như tên môn, số tín chỉ, trạng thái hoặc hạn ngạch thi thử hàng ngày. |
| 102 | Xem danh sách lớp học phần | Admin, Department Head, Lecturer | Người dùng xem danh sách lớp học phần theo học kỳ, môn học và giảng viên phụ trách. |
| 103 | Tạo lớp học phần và gán giảng viên | Admin, Department Head | Admin hoặc Trưởng Bộ môn tạo lớp học phần mới trong học kỳ và phân công giảng viên giảng dạy. |
| 104 | Cập nhật thông tin lớp học phần | Admin, Department Head | Admin hoặc Trưởng Bộ môn cập nhật giảng viên phụ trách hoặc trạng thái của lớp học phần. |
| 105 | Gán sinh viên vào lớp học phần | Admin, Department Head | Admin hoặc Trưởng Bộ môn thêm hoặc nhập danh sách sinh viên vào lớp học phần. |
| 106 | Xem cấu hình hệ thống tổng thể | Admin | Admin xem danh sách các tham số vận hành luyện tập và chịu lỗi trên màn hình cấu hình hệ thống. |
| 107 | Cấu hình giới hạn câu hỏi phụ luyện tập | Admin | Admin điều chỉnh số câu hỏi phụ tối đa (1–5 câu, mặc định 2) cho chế độ luyện tập Per-Question. |
| 108 | Cấu hình giới hạn số câu luyện toàn phiên | Admin | Admin điều chỉnh số câu tối thiểu (mặc định 3) và tối đa (mặc định 10) cho chế độ luyện tập Full-Session Progressive. |
| 109 | Cấu hình thời gian đệm sửa thuật ngữ | Admin | Admin điều chỉnh thời gian đếm ngược mặc định (10–300s, mặc định 60s) cho màn hình đệm luyện tập. |
| 110 | Cấu hình thời gian timeout phiên luyện tập | Admin | Admin điều chỉnh thời gian chờ không tương tác (mặc định 5 phút) trước khi phiên luyện tập tự động đóng. |
| 111 | Xem danh sách tác vụ lỗi trong DLQ | Department Head | Trưởng Bộ môn giám sát các tác vụ chấm điểm AI bị lỗi được cách ly trong hàng đợi Dead-Letter Queue sau khi hết lượt thử lại. |
| 112 | Xem chi tiết lỗi tác vụ DLQ | Department Head | Trưởng Bộ môn kiểm tra dữ liệu đầu vào, số lần thử lại và thông báo lỗi chi tiết của một tác vụ trong DLQ. |
| 113 | Chạy lại tác vụ chấm điểm từ DLQ | Department Head | Trưởng Bộ môn kích hoạt đẩy tác vụ lỗi từ DLQ quay lại hàng đợi chấm ngầm để chấm bù (Zero Data Loss). |
| 114 | Xem nhật ký kiểm toán hệ thống | Admin | Admin tra cứu và lọc nhật ký kiểm toán chỉ đọc ghi nhận các thao tác đăng nhập, sửa điểm, khóa điểm và đổi cấu hình. |
| 115 | Xem và tìm kiếm tài khoản người dùng | Admin | Admin xem danh sách người dùng có phân trang và tìm kiếm theo tên, email hoặc mã số sinh viên/giảng viên. |
| 116 | Lọc người dùng theo vai trò và trạng thái | Admin | Admin lọc danh sách tài khoản theo 5 vai trò hệ thống và trạng thái khóa/mở. |
| 117 | Xem chi tiết tài khoản người dùng | Admin | Admin xem thông tin chi tiết, liên kết Google OAuth, tài khoản trường cấp, vai trò và lịch sử hoạt động của một tài khoản. |
| 118 | Gán hoặc cập nhật vai trò người dùng | Admin | Admin phân quyền hoặc đổi vai trò cho người dùng trong 5 vai trò RBAC (không có chức năng tự đăng ký tài khoản công khai). |
| 119 | Khóa hoặc mở khóa tài khoản | Admin | Admin khóa quyền truy cập của tài khoản vi phạm hoặc mở khóa lại cho tài khoản hợp lệ. |
| 120 | Xem bảng tổng quan Sinh viên | Student | Sinh viên xem thống kê số phiên luyện tập, điểm trung bình, hạn ngạch thi thử trong ngày và lịch thi phòng Lab sắp tới. |
| 121 | Xem bảng tổng quan Giảng viên | Lecturer | Giảng viên xem tổng quan số câu hỏi đã soạn, câu hỏi cần sửa, lịch coi thi và số bài thi đang chờ hậu kiểm. |
| 122 | Xem bảng tổng quan Trưởng Bộ môn | Department Head | Trưởng Bộ môn xem thống kê số câu hỏi chờ duyệt, tác vụ lỗi trong DLQ, các ca thi đang diễn ra và đơn phúc khảo chờ xử lý. |
| 123 | Xem danh sách thông báo trong ứng dụng | Student, Lecturer, Department Head, Proctor, Admin | Người dùng mở hộp thư thông báo để xem các thông báo hệ thống (kết quả duyệt đề, công bố điểm, cập nhật phúc khảo, lịch coi thi). |
| 124 | Đánh dấu đã đọc một thông báo | Student, Lecturer, Department Head, Proctor, Admin | Người dùng chọn một thông báo để đánh dấu đã đọc và chuyển tới màn hình chức năng liên quan. |
| 125 | Đánh dấu đã đọc tất cả thông báo | Student, Lecturer, Department Head, Proctor, Admin | Người dùng đánh dấu toàn bộ thông báo chưa đọc thành đã đọc chỉ với một thao tác. |

---

### 5. Phụ Lục Yêu Cầu (Requirement Appendix)

#### 5.1 Quy Tắc Nghiệp Vụ (Business Rules)

**Bảng 5.1 : Quy tắc nghiệp vụ (Business Rules)**

| Mã | Định nghĩa quy tắc (Rule Definition) |
| :-: | :--- |
| BR-01 | Người dùng thuộc cả 5 vai trò có thể đăng nhập qua Google OAuth 2.0 hoặc bằng tên đăng nhập và mật khẩu do nhà trường cấp. |
| BR-02 | Vai trò của mỗi tài khoản được đọc từ cơ sở dữ liệu và hệ thống không cho phép tự đăng ký công khai bằng tên đăng nhập và mật khẩu. |
| BR-03 | Người dùng có tài khoản do nhà trường cấp chỉ được đổi mật khẩu mới sau khi xác nhận đúng mật khẩu hiện tại. |
| BR-04 | Chỉ Admin mới có quyền đổi vai trò hoặc khóa tài khoản, và không thể tự khóa tài khoản của chính mình. |
| BR-05 | Tài khoản bị khóa không thể đăng nhập hoặc truy cập chức năng hệ thống cho đến khi được Admin mở khóa. |
| BR-06 | Các phiên luyện tập chỉ rút câu hỏi từ kho câu hỏi luyện tập đã được phê duyệt. |
| BR-07 | Chế độ luyện Từng câu không cho phép xuất hiện quá 2 câu hỏi liên tiếp có cùng mức độ khó. |
| BR-08 | Chế độ luyện Từng câu chỉ sinh câu hỏi phụ AI (1–5 câu, mặc định 2) khi điểm câu chính đạt từ 4.0 đến 8.0. |
| BR-09 | Chế độ luyện Toàn phiên yêu cầu từ 3 đến 10 câu hỏi sắp xếp từ Dễ đến Khó và không có câu hỏi phụ. |
| BR-10 | Sinh viên có màn hình đệm đếm ngược (10–300s, mặc định 60s) để rà soát và sửa các thuật ngữ tiếng Anh chuyên ngành. |
| BR-11 | Các phiên luyện tập và thi thử chỉ lưu văn bản trả lời và không lưu trữ tệp âm thanh. |
| BR-12 | Phiên luyện tập tự động đóng sau 5 phút không tương tác nhưng vẫn giữ nguyên điểm các câu đã làm. |
| BR-13 | Bài thi thử rút câu hỏi từ kho luyện tập theo cấu trúc đề và ma trận Bloom do Trưởng Bộ môn thiết lập. |
| BR-14 | Sinh viên không được vượt quá số lượt thi thử tối đa mỗi ngày theo môn do Admin cấu hình. |
| BR-15 | Sinh viên có thể chọn bật hoặc tắt câu hỏi phụ AI theo ngữ cảnh trước khi bắt đầu bài thi thử. |
| BR-16 | Bài thi thử khóa nhập liệu bàn phím ở câu trả lời chính và bắt buộc thu âm qua micro. |
| BR-17 | Bài thi thử tự động nộp khi hết giờ và hiển thị ngay bảng điểm chi tiết theo từng CLO. |
| BR-18 | Đề cương môn học và CLO được đọc từ hệ thống FLM và không thể chỉnh sửa ngược lại từ OES. |
| BR-19 | Mỗi câu hỏi phải có đáp án mẫu tối thiểu 50 ký tự và được gán vào ít nhất một kho câu hỏi; câu hỏi biên soạn thủ công được phép đính kèm hình ảnh/sơ đồ kỹ thuật hợp lệ (`PNG/JPG/WEBP <= 5MB`) kèm mô tả để AI chấm đa phương thức. |
| BR-20 | Tổng trọng số các tiêu chí trong barem của một câu hỏi phải bằng chính xác 10.0 điểm. |
| BR-21 | Câu hỏi do Giảng viên tạo khởi đầu ở trạng thái nháp và phải gửi Trưởng Bộ môn phê duyệt. |
| BR-22 | Chỉ Trưởng Bộ môn mới có quyền phê duyệt, yêu cầu chỉnh sửa hoặc từ chối câu hỏi. |
| BR-23 | Chỉ Trưởng Bộ môn mới có quyền tạo kỳ thi chính thức, ca thi phòng Lab và quy tắc thi chính thức. |
| BR-24 | Mỗi thí sinh được xếp một số ghế (1–40) gắn với IP máy trạm phòng Lab và phải được Giám thị điểm danh. |
| BR-25 | Vé thi chính thức chuyển tuần tự qua 7 trạng thái: SCHEDULED, IN_PROGRESS, SUBMITTED, AI_GRADED, AUDITED, PUBLISHED, và LOCKED. |
| BR-26 | Thí sinh phải vượt qua bước kiểm tra micro 30 giây đạt tối thiểu 60 dB trước khi vào phòng thi Kiosk. |
| BR-27 | Thoát toàn màn hình hoặc chuyển cửa sổ quá 3 lần sẽ báo động cho Giám thị để đình chỉ thi. |
| BR-28 | Âm thanh bài thi chính thức được lưu lên Cloudflare R2 theo tên STT_MSSV.webm kèm mã băm SHA-256. |
| BR-29 | Máy trạm Kiosk khóa màn hình ngay sau khi nộp bài và tuyệt đối không hiển thị điểm liền hay khiếu nại tại chỗ. |
| BR-30 | Bài thi chính thức chấm AI xong được chia thành Nhóm 1 (nghi ngờ hoặc độ tin cậy dưới 0.70) và Nhóm 2 (tin cậy cao) để hậu kiểm. |
| BR-31 | Giảng viên bắt buộc nhập lý do giải trình mỗi khi chỉnh sửa điểm do AI chấm. |
| BR-32 | Giảng viên chỉ được công bố điểm ca thi một lần duy nhất sau khi 100% bài nộp trong ca đã được hậu kiểm. |
| BR-33 | Công bố điểm ca thi sẽ kích hoạt khóa một chiều, chặn mọi thao tác sửa hoặc xóa điểm trái phép. |
| BR-34 | Sinh viên có thể gửi đơn phúc khảo trên Student Portal và Trưởng Bộ môn phải giao cho một Giảng viên khác chấm lại. |
| BR-35 | Bảng điểm xuất ra Excel và PDF có chữ ký số luôn kèm mã băm công khai để xác thực tính toàn vẹn. |
| BR-36 | Học kỳ phải có ngày bắt đầu/kết thúc hợp lệ và chỉ được Admin cập nhật, tuyệt đối không được xóa. |
| BR-37 | Admin được cập nhật thông tin môn học và cấu hình hạn ngạch thi thử hàng ngày theo môn, nhưng không được cấu hình quy chế hoặc ca thi chính thức. |
| BR-38 | Mỗi sinh viên chỉ được ghi danh vào tối đa một lớp học phần của cùng một môn trong một học kỳ. |
| BR-39 | Chỉ Admin mới được chỉnh tham số luyện tập toàn cục (hỏi phụ 1–5, toàn phiên 3–10, đệm 10–300s, timeout 5p). |
| BR-40 | Tham số luyện tập mới chỉ áp dụng cho các phiên tạo mới và không ảnh hưởng tới phiên đang diễn ra. |
| BR-41 | Nếu việc chấm AI tự động thất bại sau 3 lần thử, bài nộp của sinh viên vẫn được giữ nguyên và chuyển vào hàng đợi lỗi (DLQ) để xử lý chấm bù. |
| BR-42 | Tác vụ trong DLQ được tự động thử lại sau 5 phút hoặc do Trưởng Bộ môn chạy lại để không mất bài thi. |
| BR-43 | Việc chạy lại tác vụ từ DLQ sẽ không ghi đè nếu bài thi đó đã được chấm điểm thành công. |
| BR-44 | Mọi thao tác nhạy cảm (đăng nhập, đổi vai trò, sửa điểm, công bố điểm, đổi cấu hình) đều được ghi vào nhật ký kiểm toán. |
| BR-45 | Nhật ký kiểm toán chỉ được phép đọc, không một ai kể cả Admin được quyền sửa hoặc xóa. |

---

#### 5.2 Yêu Cầu Chung Toàn Hệ Thống (Common Requirements)

**Bảng 5.2 : Yêu cầu chung (Common Requirements)**

| Mã | Định nghĩa quy tắc (Rule Definition) |
| :-: | :--- |
| CR-01 | Mọi dữ liệu nhập đều được kiểm tra ở trình duyệt và xác thực lại ở máy chủ trước khi xử lý. |
| CR-02 | Các trang bảo vệ luôn kiểm tra phiên đăng nhập trước khi hiển thị và hiện trạng thái chờ trong lúc kiểm tra. |
| CR-03 | Truy cập sai vai trò sẽ chuyển về trang 403, và đường dẫn không tồn tại sẽ chuyển về trang 404. |
| CR-04 | Dữ liệu thời gian thực (điểm luyện tập, phòng máy Lab, cảnh báo, thông báo) được đẩy trực tiếp qua SignalR không cần tải lại trang. |
| CR-05 | Các luồng làm bài nhiều bước (luyện tập, thi thử, check-in Kiosk) chạy trên một đường dẫn duy nhất để tránh nhảy cóc qua URL. |
| CR-06 | Các danh mục và cấu hình đơn giản được thêm và sửa ngay trên hộp thoại nổi (modal) đè lên danh sách. |
| CR-07 | Mọi danh sách dữ liệu lớn đều hỗ trợ phân trang, tìm kiếm và lọc từ phía máy chủ. |
| CR-08 | Toàn bộ mốc thời gian được lưu theo chuẩn UTC và hiển thị theo múi giờ địa phương của người dùng (UTC+7). |
| CR-09 | Bài nộp của sinh viên luôn được lưu vào cơ sở dữ liệu trước khi đưa vào hàng đợi chấm AI ngầm. |
| CR-10 | Thuật ngữ tiếng Anh chuyên ngành nói chêm trong tiếng Việt được giữ nguyên và cho phép sửa ở màn hình đệm. |
| CR-11 | Các thao tác không thể đảo ngược (nộp bài thi, đình chỉ thi, công bố điểm, khóa tài khoản) bắt buộc có hộp thoại xác nhận. |
| CR-12 | Toàn bộ thông báo và báo lỗi hiển thị cho người dùng đều lấy từ danh mục thông báo chuẩn tại Mục 5.3. |

