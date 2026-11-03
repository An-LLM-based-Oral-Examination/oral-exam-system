# SWP490 · CAPSTONE PROJECT · REVIEW 2 · BM03
## Registration Form – Record of Changes (English Submission Version)

### 1. Project Information
| Field | Value |
| :--- | :--- |
| **Project code** | FA26SE166 |
| **Project name** | An LLM-based Oral Examination Practice and Assessment System for Software Engineering Major |
| **Team** | Nguyễn Quang Thành, Nguyễn Trọng Tốt, Phạm Nguyễn Đăng Hải, Lê Vũ Hoàng |
| **Supervisor** | Dr. Nguyễn Thị Cẩm Hương (TS. Nguyễn Thị Cẩm Hương) |
| **Current registration version** | 1.0 |
| **Proposed version** | 2.0 |
| **Version** | 1.0 |
| **Date** | 28/10/2026 |

### 2. Changes
☐ No changes to the current registration form

| No. | Section | Current content | Proposed content | Reason | Proposed by |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **1** | **User Roles (Section c - Functional Requirements)** | Divided users into only two general groups: Lecturer/Admin and Student. | Split into 6 distinct roles: Student, Lecturer, Department Head, Examination Officer, Proctor, and Administrator (supporting a 4-tier academic hierarchy: Admin creates semester/course & assigns Department Head -> Department Head assigns multiple Lecturers -> Lecturer creates class section & class password + in-class shift with 5-minute rotating OTP -> Student self-joins). | Ensure clear separation of duties among academic setup, question authoring, question bank/matrix approval, exam shift organization & grade publication (Examination Officer), seat check-in & violation logging (Proctor), post-exam audit (Lecturer), and 5-step appeal handling. | Nguyễn Trọng Tốt |
| **2** | **Question Bank & Approval Workflow (Section c.1 - Lecturer/Admin)** | Classified questions by subject, chapter, and difficulty level; used a single shared question pool where lecturers saved questions directly. | Classify questions by subject, Course Learning Outcome (CLO), and Bloom cognitive level (removing chapter); decouple into two independent authoring flows: the practice/mock pool (`practice_questions` by CLO) and the official exam pool (`exam_questions` strictly following the Department Head's sample exam / structure matrix); require Department Head approval of questions and 10.0-point Rubric criteria before publication. | Align with the university syllabus structure, enforce the official exam structure matrix issued by the Department Head, prevent official exam questions from leaking during student practice, and control question quality before release. | Phạm Nguyễn Đăng Hải |
| **3** | **Answer Modality & Audio Storage Policy (Sections b & c.2 - Practice & Mock Exam)** | Allowed students to answer either by voice or by typing text directly; did not separate audio storage policies between self-practice and official exams. | Answers must be recorded via microphone first (rather than typed directly as text); after speech-to-text conversion, students are allowed to edit the generated transcript (such as misrecognized English technical terms) before submitting for grading. Audio files are archived exclusively for official lab exams as audit evidence, whereas practice and mock exam modes store only the text transcripts. | Preserve authentic oral assessment, mitigate speech-to-text errors on Software Engineering terminology, and optimize storage for daily self-study activities. | Nguyễn Quang Thành |
| **4** | **Official Exam, Post-Exam Audit & Appeal Flow (Section c.3 - Official Exam)** | Lecturers audited submissions and manually entered grades by comparing AI scores with audio recordings; recorded sessions for appeals. | Department Head configures exam Follow-up rules (1–5 questions, default 2); Examination Officer creates exam sessions/lab shifts, assigns Proctors/Examiners, allocates exam sets, and publishes shift exams (`is_exam_published = true`); Proctor only checks in students by seat (1–40, verifying Student ID/CCCD) and logs violations (`exam_anti_cheat_logs`). Students take exams on a fullscreen-locked kiosk; AI pre-grades in the background; Lecturers audit all shift submissions before grades are published once (`One-Way Lock`). 5-step appeal flow: Student submits appeal to Examination Officer -> Examination Officer validates & forwards to Department Head -> Department Head assigns independent Lecturer -> Lecturer regrades -> Department Head approves final decision. | Standardize on-site computer lab exam operations, clearly separate authority among Examination Officer, Proctor, Department Head, and Lecturer, and ensure transparent, objective post-publication grade appeals via a 5-step workflow. | Lê Vũ Hoàng |
| **5** | **Advanced Features Scope & Account Provisioning (Section c.4 - Advanced Features)** | Listed candidate extensions: English pronunciation/fluency scoring, AI follow-up generation, tab-switch/camera detection, and live video call oral exams. | Retain and implement officially: contextual AI follow-up questions, focus-loss/fullscreen violation enforcement in computer labs, and CLO-based analytics. Move Out of Scope: live Video Call integration, webcam proctoring, English pronunciation scoring, and public account self-registration (only university-provisioned accounts are used). | Official exams are conducted on-site in university computer labs under direct Proctor supervision rather than via remote Video Calls; courses assess Software Engineering domain knowledge in Vietnamese with English technical terms rather than foreign-language pronunciation. | Nguyễn Quang Thành |
| **6** | **Grading Execution & Fault-Tolerant Processing (Section d - Non-Functional Requirements)** | Invoked LLM grading directly during request wait; saved answers on LLM error or timeout for later regrading. | Decouple submission recording from automated grading: the server persists student submissions to the database immediately before queuing background grading, combined with 3x automatic retries and a Dead-Letter Queue (DLQ), where Admin monitors/replays practice & mock exam DLQ and Examination Officer monitors/replays official exam DLQ. | Ensure the exam interface in computer labs responds immediately without freezing or losing submissions during AI overload, while enforcing proper role separation for DLQ recovery between Admin and Examination Officer. | Nguyễn Trọng Tốt |

### 3. Decision at Review 2 (completed by reviewers)
| No. | Decision (Accepted / Adjusted / Rejected) | Reviewer note |
| :---: | :--- | :--- |
| **1** | | |
| **2** | | |
| **3** | | |
| **4** | | |
| **5** | | |
| **6** | | |
