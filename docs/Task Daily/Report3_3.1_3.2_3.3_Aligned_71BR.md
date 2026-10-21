# Report 3 — Functional Specifications (Aligned with Approved 71 Business Rules)

This standalone Section 3 working document follows the approved 71-rule Section 5.1 baseline. Section 3.1 is reserved for System Functional Overview and contains no function-level descriptions or triggers. Common Functions is separate from Section 3.1. Implementation status is not asserted by inclusion. No screenshots are included.

## 3.1 System Functional Overview

The system supports university-provisioned access for Student, Admin, Lecturer, Department Head, and Proctor. Student functions cover practice, mock exams, official results, and appeals. Admin manages accounts, academic settings, mock quotas, and global practice configuration. Proctor manages authorized lab invigilation. Lecturer maintains questions and assigned classes, verifies initial grades, and handles assigned regrades. Department Head inherits Lecturer-level functions where not expressly restricted, configures official examinations, supervises grading recovery, and verifies and publishes appeal outcomes.

## Common Functions (separate from Section 3.1)

### CF.1 Sign In With Google

**Function Description:** The user, including a Proctor whose Google email has been authorized/provisioned, signs in with a Google ID token. The role sent by the client is ignored. An unknown or unauthorized Google identity is not enrolled automatically. If the email already belongs to an active account, the system keeps the role stored for that account.

**Function Trigger:** The user is signed out and submits a Google ID token.

**Business Rules:** BR-01, BR-02, BR-05

### CF.2 Sign In

**Function Description:** An existing provisioned account signs in with username and password; this includes Proctors with pre-seeded credentials. Other role-specific login availability must follow the account configuration. The role is read from the stored account. The client cannot submit a role. This function does not create an account.

**Function Trigger:** The user submits a username and a password for an account that already exists.

**Business Rules:** BR-01, BR-02, BR-05

### CF.3 Sign Out

**Function Description:** The signed-in user signs out. The system revokes that refresh token. A new sign-in is required to continue.

**Function Trigger:** The signed-in user signs out and the current refresh token is sent.

**Business Rules:** BR-46

### CF.4 Get Courses

**Function Description:** Returns the active courses. Student uses this list to choose a course for practice or a mock examination. Lecturer may use the list for assigned teaching and question-management tasks; course creation and editing are restricted to Admin and Department Head.

**Function Trigger:** A signed-in user opens the course list.

**Business Rules:** BR-47

### CF.5 Get Notifications

**Function Description:** Returns the in-app notifications of the signed-in user. A notification has a title, a message, a type, a read flag, and the time it was created.

**Function Trigger:** The signed-in user opens the notification list.

**Business Rules:** BR-48

### CF.6 Mark Notification As Read

**Function Description:** Marks one notification of the signed-in user as read and stores the time it was read.

**Function Trigger:** The signed-in user marks one of that user's notifications as read.

**Business Rules:** BR-48

### CF.7 Mark All Notifications As Read

**Function Description:** Marks every unread notification of the signed-in user as read.

**Function Trigger:** The signed-in user marks every unread notification as read.

**Business Rules:** BR-48

### CF.8 Change Password

**Function Description:** A signed-in user with university-provisioned username/password credentials changes their password after providing the current password. The system rejects a change when the current password is incorrect.

**Function Trigger:** The signed-in user submits the current password and a new password from account settings.

**Business Rules:** BR-03

## 3.2 Web for User (Student), Admin, and Proctor

User in this section means Student. Proctor functions appear at the end of this section, after Admin functions.

### Practice exam

### 3.2.1 Create Per-Question Practice Session

**Function Description:** Starts a per-question practice session for one course. The student chooses one difficulty, or a combination of easy, medium, and hard. The session receives question 1 when it is created. Later questions are requested one at a time. The student does not have to fix the number of questions in advance.

**Function Trigger:** The student chooses a course and a difficulty, or a combination of easy, medium, and hard, and starts per-question practice.

**Business Rules:** BR-06, BR-09, BR-07, BR-49, BR-50

### 3.2.2 Create Full-Session Practice

**Function Description:** Starts a full-session practice. The student enters a question count from the configured minimum to the configured maximum. The default range is 3 to 10. The system automatically distributes the requested total across Easy, Medium, and Hard as evenly as possible, draws the whole set at creation, and orders it from easy to hard; the Student does not enter separate difficulty counts. This mode has no follow-up question.

**Function Trigger:** The student chooses a course, enters a question count inside the configured range, and starts full-session practice.

**Business Rules:** BR-06, BR-09

### 3.2.3 Filter Practice By Topic

**Function Description:** The student filters practice by the selected course. The project does not use a chapter. Practice questions are classified by course, Bloom level, and CLO.

**Function Trigger:** The student selects a course as the practice scope.

**Business Rules:** BR-51

### 3.2.4 Read Practice Question

**Function Description:** Shows the current practice question. The question can be read aloud. The student may answer by voice or by typing. Practice does not store audio.

**Function Trigger:** A practice session has a current question and the student opens it.

**Business Rules:** BR-11

### 3.2.5 Submit Practice Answer

**Function Description:** Submits the transcript of the current practice answer. In full-session practice the student may submit one answer or submit the package of answers. The system stores the transcript, not audio.

**Function Trigger:** The student confirms the current practice answer, or confirms the full-session answer package.

**Business Rules:** BR-11, BR-12, BR-52

### 3.2.6 Update Practice Transcript

**Function Description:** After speech is transcribed, the student can correct SE terms on the buffer screen before the answer is submitted. The buffer countdown uses the configured duration. The default is 60 seconds. The allowed range is 10 to 300 seconds.

**Function Trigger:** Speech has been transcribed and the buffer countdown is still running.

**Business Rules:** BR-10, BR-39

### 3.2.7 Grade Practice Answer

**Function Description:** The system supports this function. The system grades the stored practice transcript against the question rubric. The result is a score and comments by criterion. Per-question practice can open a follow-up when the score is in the follow-up band. Full-session practice does not create a follow-up.

**Function Trigger:** A practice transcript has been stored.

**Business Rules:** BR-09, BR-08, BR-39

### 3.2.8 Create Practice Follow-Up Question

**Function Description:** In per-question practice only, a follow-up question is created when the score is from 4.0 to 8.0 inclusive. The number of follow-up questions is configured by Admin, from 1 to 5. The default is 2. A score outside that band opens the scorecard instead. Full-session practice never creates a follow-up.

**Function Trigger:** A per-question answer has been graded.

**Business Rules:** BR-09, BR-08, BR-39

### 3.2.9 Get Practice Scorecard

**Function Description:** The system displays the practice scorecard to the Student for the answers already graded, including the comment for each criterion.

**Function Trigger:** The student opens the result of a practice session.

**Business Rules:** BR-53

### 3.2.10 Show Practice Question Shortage

**Function Description:** Tells the student when the selected difficulty pool cannot supply another question, or when a full-session set cannot be filled. Per-question practice stops offering more questions. Full-session practice is rejected before the session exists.

**Function Trigger:** The selected pool cannot supply the next question, or a full-session set cannot be filled.

**Business Rules:** BR-50, BR-09

### 3.2.11 Get Practice History

**Function Description:** Returns the student's past practice sessions on the student portal, on the same history surface as mock examination history.

**Function Trigger:** The student opens practice history on the student portal.

**Business Rules:** BR-54

### Mock examination, student functions

### 3.2.12 Create Mock Session

**Function Description:** Starts a timed mock examination for one course. Questions are drawn from the practice-question bank. The student does not choose topic or difficulty. The attempt counts against the daily limit configured for that course by the Department Head.

**Function Trigger:** The student chooses a course and starts a mock examination.

**Business Rules:** BR-13, BR-14, BR-37

### 3.2.13 Set Mock Follow-Up Option

**Function Description:** Before the mock session starts, the student chooses whether this attempt uses follow-up questions. The student does not set the follow-up count. That count belongs to the Department Head configuration.

**Function Trigger:** The student chooses yes or no for follow-up before the mock session starts.

**Business Rules:** BR-15

### 3.2.14 Read Mock Question

**Function Description:** The system displays the current mock question to the Student. The attempt is voice-first: the keyboard stays locked and a microphone is required. Mock examination does not store audio.

**Function Trigger:** A mock session has a current question and the student opens it.

**Business Rules:** BR-11, BR-16

### 3.2.15 Submit Mock Answer

**Function Description:** The Student submits the transcript of the current mock answer. The system stores the transcript, not audio.

**Function Trigger:** The student confirms the current mock answer.

**Business Rules:** BR-11, BR-17

### 3.2.16 Update Mock Transcript

**Function Description:** The student can correct the transcript during the configured buffer before submission. The default buffer is 60 seconds, in the range 10 to 300 seconds. The keyboard lock of the voice-first gate still applies to composing the original answer.

**Function Trigger:** The mock transcript buffer is still running.

**Business Rules:** BR-10, BR-39, BR-16

### 3.2.17 Grade Mock Answer

**Function Description:** The system supports this function. The system grades the stored mock transcript and returns the score for that answer. Grading uses the question rubric.

**Function Trigger:** A mock transcript has been stored.

**Business Rules:** BR-17

### 3.2.18 Create Mock Follow-Up Question

**Function Description:** If the student turned follow-up on for this attempt, the system asks a follow-up from the answer context. The follow-up is not selected by the 4.0 to 8.0 score band used in practice. A follow-up attempt runs longer than an attempt without follow-up. If the student turned follow-up off, no follow-up question is created.

**Function Trigger:** A mock answer has been graded and the session follow-up choice is yes.

**Business Rules:** BR-15, BR-55

### 3.2.19 Get Mock Scorecard

**Function Description:** The system displays the mock scorecard to the Student on the result screen and stores it in the examination history. The scorecard includes the comment for each criterion.

**Function Trigger:** The student opens the mock result.

**Business Rules:** BR-17, BR-54

### 3.2.20 Get Mock Examination History

**Function Description:** Returns the student's past mock examinations on the student portal, together with practice history.

**Function Trigger:** The student opens mock examination history on the student portal.

**Business Rules:** BR-54

### 3.2.21 Submit Mock Examination On Timeout

**Function Description:** When the mock timer reaches zero, the system submits the attempt. Unanswered questions stay blank and are not graded. The student cannot continue that attempt.

**Function Trigger:** The mock timer reaches zero.

**Business Rules:** BR-17

### Laboratory examination, candidate functions

### 3.2.22 Check Microphone

**Function Description:** The system supports this function. Before the laboratory questions start, the workstation tests the microphone for 30 seconds. The test passes only when the level is at least 60 dB.

**Function Trigger:** The candidate starts the microphone test on the assigned workstation.

**Business Rules:** BR-26

### 3.2.23 Lock Examination Screen

**Function Description:** The system supports this function. The laboratory examination runs in a locked full screen. Alt+Tab, the Windows key, F11, F12, and developer tools are blocked. If the window blurs more than 3 times, the attempt is suspended.

**Function Trigger:** The candidate enters the examination after check-in.

**Business Rules:** BR-27

### 3.2.24 Read Examination Question

**Function Description:** The system displays the current official examination question to the Student on the locked workstation. The input mode of the session is either voice only, or voice with transcript edit.

**Function Trigger:** The locked workstation has a current examination question.

**Business Rules:** BR-56

### 3.2.25 Submit Voice Answer

**Function Description:** The Student submits the spoken answer. The workstation stores the submission first, then the audio file. The ticket moves to SUBMITTED. The kiosk does not show a score.

**Function Trigger:** The candidate finishes the spoken answer.

**Business Rules:** BR-25, BR-29

### 3.2.26 Update Examination Transcript

**Function Description:** Available only when the session input mode is voice with transcript edit. The student can correct the transcript during the buffer configured for that examination. The buffer is from 10 to 300 seconds. Voice-only mode does not open transcript editing.

**Function Trigger:** The input mode is voice with transcript edit and the examination buffer is still running.

**Business Rules:** BR-56, BR-10, BR-39

### 3.2.27 Store Examination Audio

**Function Description:** The laboratory examination stores one audio file per candidate. The file name is the seat number and the student code, for example `01_SE170123.webm`. The workstation keeps a SHA-256 hash of that file. Practice and mock examination do not store audio.

**Function Trigger:** The laboratory attempt has an audio stream.

**Business Rules:** BR-28

### 3.2.28 Suspend Examination

**Function Description:** The system supports this function. The attempt is suspended when the locked window blurs more than 3 times.

**Function Trigger:** The locked examination window blurs more than 3 times.

**Business Rules:** BR-27

### 3.2.29 Submit Examination

**Function Description:** The Student submits the laboratory attempt. The submission and the ticket are stored as SUBMITTED. The kiosk then shows that the paper is stored. It does not show a score and it does not accept an appeal at the workstation.

**Function Trigger:** The candidate submits the laboratory attempt.

**Business Rules:** BR-25, BR-29

### 3.2.30 Confirm Examination Submission

**Function Description:** The system confirms to the Student that the submission was stored. The candidate signs the paper record before leaving. The screen does not show a grade.

**Function Trigger:** The ticket has just been stored as SUBMITTED.

**Business Rules:** BR-29

### Grade and appeal, student functions

### 3.2.31 Get Published Grade

**Function Description:** After the lecturer publishes the shift, the student can open the published grade on the student portal. The grade is not available on the laboratory kiosk.

**Function Trigger:** The student opens a published grade on the student portal.

**Business Rules:** BR-32, BR-33, BR-57

### 3.2.32 Create Appeal

**Function Description:** On the student portal, the student submits an appeal for a published grade. The appeal is sent to the Department Head. The kiosk has no appeal action.

**Function Trigger:** The student submits an appeal for a published grade on the student portal.

**Business Rules:** BR-32, BR-33, BR-57, BR-34

### 3.2.33 Confirm Published Grade

**Function Description:** The student confirms the published grade. The ticket acknowledgement is stored. This is the alternative to creating an appeal.

**Function Trigger:** The student confirms a published grade.

**Business Rules:** BR-32, BR-33, BR-57

### Dashboard

### 3.2.34 Get Student Dashboard

**Function Description:** Returns the student dashboard on the student portal.

**Function Trigger:** The student opens the student portal home.

**Business Rules:** None specifically applicable in the approved Section 5.1 baseline.

### System configuration, Admin functions

Admin configures course-specific daily mock-exam quotas and global practice settings. Department Head configures mock-paper structure and official examination rules.

### 3.2.35 Get Semesters

**Function Description:** The system displays the semester list to Admin.

**Function Trigger:** Admin opens the semester list.

**Business Rules:** BR-36

### 3.2.36 Create Semester

**Function Description:** The Admin creates a semester with a code, a name, a start date, and an end date. The end date is not before the start date.

**Function Trigger:** Admin submits a semester code, name, start date, and end date.

**Business Rules:** BR-36

### 3.2.37 Update Semester

**Function Description:** The Admin updates a semester. A semester is not deleted.

**Function Trigger:** Admin submits changes to an existing semester.

**Business Rules:** BR-36

### 3.2.38 Configure AI Model

**Function Description:** Admin can use this function. This function is the system-configuration entry for the AI model. Question generation and answer grading use Gemini. Speech-to-text uses Whisper. No separate model-parameter screen is specified beyond this function.

**Function Trigger:** Admin saves the AI model setting.

**Business Rules:** None specifically applicable in the approved Section 5.1 baseline.



### 3.2.39 Configure Attempt Limit

**Function Description:** Sets the maximum number of mock attempts per student per day for a course. The quota is course-specific. Only Admin configures this limit.

**Function Trigger:** Admin saves the daily mock limit of a course.

**Business Rules:** BR-14, BR-37

### 3.2.40 Configure Practice Question Range

**Function Description:** The Admin sets the full-session practice size. The minimum and the maximum are stored as system configuration. The default minimum is 3 and the default maximum is 10.

**Function Trigger:** Admin submits the minimum and maximum full-session question count.

**Business Rules:** BR-09

### 3.2.41 Configure Practice Follow-Up

**Function Description:** Sets how many follow-up questions per-question practice may ask. The allowed range is 1 to 5. The default is 2. A Lecturer cannot configure practice follow-up.

**Function Trigger:** Admin submits the practice follow-up count.

**Business Rules:** BR-08, BR-39

### 3.2.42 Get Audit Logs

**Function Description:** The system displays to Admin the audit log of sensitive changes, including grade changes and question-bank changes.

**Function Trigger:** Admin opens the audit log.

**Business Rules:** BR-44, BR-45

### 3.2.43 Configure Transcript Buffer

**Function Description:** The Admin sets the practice transcript buffer in system configuration. The allowed range is 10 to 300 seconds. The default is 60 seconds. The buffer for an official examination is configured on that examination, not by this function.

**Function Trigger:** Admin submits the practice transcript buffer in seconds.

**Business Rules:** BR-10, BR-39

### 3.2.44 Create Course

**Function Description:** The Admin creates a course with a code, a name, and a credit count greater than 0.

**Function Trigger:** The Admin submits a course code, name, and a credit count greater than 0.

**Business Rules:** BR-18, BR-37

### 3.2.45 Update Course

**Function Description:** The Admin updates a course. The credit count remains greater than 0.

**Function Trigger:** The Admin submits changes to a course.

**Business Rules:** BR-18, BR-37

### 3.2.46 Configure Session Inactivity Timeout

**Function Description:** Admin configures the practice-session inactivity timeout, with a default of five minutes. The setting applies to newly created sessions; sessions with no interaction beyond the configured timeout are closed while preserving already awarded scores.

**Function Trigger:** Admin saves an updated practice inactivity-timeout setting.

**Business Rules:** BR-12, BR-39, BR-40

### User management

### 3.2.47 Get Users

**Function Description:** Returns the user accounts. Accounts are university-provisioned; the system provides no public self-registration form.

**Function Trigger:** Admin opens the user list.

**Business Rules:** BR-02, BR-04, BR-05

### 3.2.48 Get User Detail

**Function Description:** The system displays one user account to Admin, including the stored role and whether the account is active.

**Function Trigger:** Admin opens one user.

**Business Rules:** BR-02, BR-04, BR-05

### 3.2.49 Update User

**Function Description:** The Admin updates an existing user record. This function does not create a user and does not replace Assign User Role or Update User Status.

**Function Trigger:** Admin submits changes to an existing user, other than role and lock status.

**Business Rules:** BR-02, BR-04, BR-05

### 3.2.50 Update User Status

**Function Description:** The Admin locks or unlocks an account. A locked account cannot sign in.

**Function Trigger:** Admin locks or unlocks an account.

**Business Rules:** BR-05, BR-02, BR-04

### 3.2.51 Assign User Role

**Function Description:** Sets the stored role of an existing user. The role is one of Student, Lecturer, Department Head, Proctor, or Admin. The stored value for Lecturer is `lecturer`.

**Function Trigger:** Admin assigns a role to an existing user.

**Business Rules:** BR-02, BR-04, BR-05

### Proctor — laboratory invigilation functions

### 3.2.52 Record Identity Check

**Function Description:** Records that the candidate's student card or citizen identity was checked before the attempt.

**Function Trigger:** The Proctor records the identity check of a candidate.

**Business Rules:** BR-24, BR-65

### 3.2.53 Assign Examination Machine

**Function Description:** The Proctor assigns the workstation. The seat number is from 1 to 40 and matches the candidate order of the shift. The workstation address is stored as `ip_address`.

**Function Trigger:** The Proctor assigns a seat and a workstation to a candidate.

**Business Rules:** BR-24, BR-66

### 3.2.54 Start Examination Shift

**Function Description:** The Proctor opens the shift so checked-in candidates can start on the assigned workstations.

**Function Trigger:** The Proctor starts the shift.

**Business Rules:** BR-24, BR-65

### 3.2.55 Check In Candidate

**Function Description:** The Proctor checks the candidate in to the shift after the identity check and the seat assignment.

**Function Trigger:** The Proctor checks a candidate in after the identity check and the seat assignment.

**Business Rules:** BR-24, BR-65, BR-66

### 3.2.56 End Examination Shift

**Function Description:** The Proctor closes the shift after the candidates have submitted and signed the paper record.

**Function Trigger:** The Proctor ends the shift after submissions and the paper sign-out.

**Business Rules:** None specifically applicable in the approved Section 5.1 baseline.


### Business rules for 3.2 (including Proctor rules BR-61, BR-64–66 in the cross-reference table below)


## 3.3 Web for Instructor (Lecturer) and Department Head

Lecturer is the report name. The database value is `lecturer`. The web client shows Instructor.



### Mock examination, Department Head configuration

### 3.3.1 Configure Mock Paper

**Function Description:** Configures the mock paper structure for a course. The student does not configure the paper.

**Function Trigger:** The Department Head saves the mock paper structure of a course.

**Business Rules:** BR-13, BR-37

### 3.3.2 Configure Bloom Distribution

**Function Description:** The Department Head configures the Bloom distribution of the mock paper for a course.

**Function Trigger:** The Department Head saves the Bloom distribution of a mock paper.

**Business Rules:** BR-13, BR-37

### 3.3.3 Configure Mock Duration

**Function Description:** The Department Head configures how long a mock attempt lasts. An attempt with follow-up runs longer than an attempt without follow-up.

**Function Trigger:** The Department Head saves the mock duration of a course.

**Business Rules:** BR-13, BR-37, BR-15

### 3.3.4 Configure Mock Follow-Up

**Function Description:** Configures mock follow-up for a course. This is separate from the practice follow-up count, which Admin configures.

**Function Trigger:** The Department Head saves the mock follow-up setting of a course.

**Business Rules:** BR-13, BR-15


### Question management

### 3.3.5 Get Syllabus

**Function Description:** The Lecturer or Department Head reads the course syllabus from FLM. The system reads FLM and does not write back to FLM.

**Function Trigger:** A Lecturer or Department Head requests the syllabus of a course.

**Business Rules:** BR-18

### 3.3.6 Generate Question From Syllabus

**Function Description:** The Lecturer or Department Head generates a question, a rubric, and a sample answer from the syllabus CLOs.

**Function Trigger:** A Lecturer or Department Head asks for a question from selected syllabus CLOs.

**Business Rules:** BR-18, BR-19

### 3.3.7 Generate Question By Topic

**Function Description:** The Lecturer or Department Head generates a question from a selected topic. The topic is not a chapter.

**Function Trigger:** A Lecturer or Department Head asks for a question from a topic in the course.

**Business Rules:** BR-18, BR-58

### 3.3.8 Get Questions

**Function Description:** The system displays the questions the actor is allowed to open in the question bank.

**Function Trigger:** A Lecturer or Department Head opens the question bank.

**Business Rules:** BR-59

### 3.3.9 Create Question

**Function Description:** The Lecturer or Department Head creates a question by hand or from a generated draft. A new question is a draft. The author chooses the practice bank, the examination bank, or both.

**Function Trigger:** A Lecturer or Department Head saves a new question.

**Business Rules:** BR-19, BR-20, BR-21

### 3.3.10 Update Question

**Function Description:** The Lecturer or Department Head updates a question that is still a draft or that was returned for revision.

**Function Trigger:** A Lecturer or Department Head saves changes to a draft or to a question returned for revision.

**Business Rules:** BR-19, BR-20

### 3.3.11 Get Rubrics

**Function Description:** The system displays the rubrics of a course or question to the authorized Lecturer or Department Head.

**Function Trigger:** A Lecturer or Department Head opens the rubrics of a course or a question.

**Business Rules:** BR-20

### 3.3.12 Create Rubric

**Function Description:** The Lecturer or Department Head creates a rubric whose criterion scores sum to 10.0.

**Function Trigger:** A Lecturer or Department Head saves a new rubric.

**Business Rules:** BR-20

### 3.3.13 Update Rubric

**Function Description:** The Lecturer or Department Head updates a rubric. The criterion scores must still sum to 10.0.

**Function Trigger:** A Lecturer or Department Head saves changes to a rubric.

**Business Rules:** BR-20

### 3.3.14 Validate Rubric Total

**Function Description:** The Lecturer or Department Head checks that the rubric scores sum to 10.0 before the question can be saved.

**Function Trigger:** A rubric is saved.

**Business Rules:** BR-20

### 3.3.15 Set Question Difficulty

**Function Description:** The Lecturer or Department Head sets the question difficulty to easy, medium, or hard.

**Function Trigger:** A Lecturer or Department Head sets a question to easy, medium, or hard.

**Business Rules:** BR-60

### 3.3.16 Preview Question

**Function Description:** The Lecturer or Department Head shows the question as it will be read by a candidate, before it is submitted for review.

**Function Trigger:** A Lecturer or Department Head opens the preview of a draft question.

**Business Rules:** BR-59

### 3.3.17 Test Rubric

**Function Description:** The Lecturer or Department Head runs the rubric against a trial answer so the author can see the criterion breakdown before submitting the question.

**Function Trigger:** A Lecturer or Department Head runs a rubric against a trial answer.

**Business Rules:** BR-20

### 3.3.18 Submit Question

**Function Description:** Submits a draft question to the Department Head. The question status becomes submitted for review. A Lecturer cannot approve the question.

**Function Trigger:** A Lecturer submits a draft question for review.

**Business Rules:** BR-21, BR-22, BR-61

### 3.3.19 Approve Question

**Function Description:** The Department Head approves a submitted question. The question enters the bank selected for it.

**Function Trigger:** The Department Head approves a submitted question.

**Business Rules:** BR-22, BR-61

### 3.3.20 Request Question Revision

**Function Description:** The Department Head returns a submitted question for revision and stores the comment. The author can update it and submit it again.

**Function Trigger:** The Department Head returns a submitted question with a revision comment.

**Business Rules:** BR-22, BR-61

### 3.3.21 Reject Question

**Function Description:** The Department Head rejects a submitted question. The question is not added to the bank.

**Function Trigger:** The Department Head rejects a submitted question.

**Business Rules:** BR-22, BR-61

### 3.3.22 Add Question To Practice Bank

**Function Description:** The Lecturer or Department Head marks the question for the practice bank. Mock examinations draw from this bank after the question is approved.

**Function Trigger:** A Lecturer or Department Head marks a question for the practice bank.

**Business Rules:** BR-19, BR-06, BR-13

### 3.3.23 Add Question To Examination Bank

**Function Description:** The Lecturer or Department Head marks the question for the official examination bank. This bank is separate from the practice bank.

**Function Trigger:** A Lecturer or Department Head marks a question for the examination bank.

**Business Rules:** BR-19, BR-06, BR-13

### Examination management

### 3.3.24 Create Examination Season

**Function Description:** The Department Head creates an official examination season.

**Function Trigger:** The Department Head saves a new examination season.

**Business Rules:** BR-23

### 3.3.25 Create Examination Shift

**Function Description:** Creates a shift under an examination season. The shift has a lab room, a proctor assignment, and the candidate list taken from a class.

**Function Trigger:** The Department Head saves a shift with a lab room, a proctor, and a class.

**Business Rules:** BR-23

### 3.3.26 Assign Course

**Function Description:** The Department Head assigns the course of an official examination season.

**Function Trigger:** The Department Head selects the course of an examination season.

**Business Rules:** BR-23

### 3.3.27 Assign Class

**Function Description:** The Department Head assigns a class to a shift. The candidates of that shift are taken from the class enrollment.

**Function Trigger:** The Department Head selects the class of a shift.

**Business Rules:** BR-23

### 3.3.28 Import Class Excel

**Function Description:** The Lecturer or Proctor imports a class roster from an Excel file when the roster is not taken only from the stored class enrollment. This is the roster import for an examination shift. It is not the grade export.

**Function Trigger:** A Lecturer or Proctor submits an Excel roster for a shift.

**Business Rules:** None specifically applicable in the approved Section 5.1 baseline.

### 3.3.29 Configure Follow-Up Count

**Function Description:** Sets the official-examination follow-up count for the season. The allowed range is 1 to 5. The default is 2. The Department Head also sets whether that examination uses follow-up.

**Function Trigger:** The Department Head saves the official-examination follow-up count.

**Business Rules:** BR-39, BR-62

### 3.3.30 Configure Examination Duration

**Function Description:** The Department Head sets the duration of the official examination.

**Function Trigger:** The Department Head saves the official-examination duration.

**Business Rules:** BR-23

### 3.3.31 Set Examination Room

**Function Description:** The Department Head sets the lab room on the shift.

**Function Trigger:** The Department Head selects the lab room of a shift.

**Business Rules:** BR-23

### 3.3.32 Assign Proctor

**Function Description:** The Department Head assigns a Proctor for an examination shift. The assigned person may be a Lecturer or an external staff member provisioned with a Proctor account.

**Function Trigger:** The Department Head selects the person who proctors a shift.

**Business Rules:** BR-01, BR-02, BR-63

### 3.3.33 Configure Examination Input Mode

**Function Description:** The Department Head sets the official examination input mode to voice only, or voice with transcript edit.

**Function Trigger:** The Department Head selects voice only, or voice with transcript edit.

**Business Rules:** None specifically applicable in the approved Section 5.1 baseline.

### 3.3.34 Configure Examination Transcript Buffer

**Function Description:** Sets the transcript buffer of the official examination, from 10 to 300 seconds. This does not change the practice buffer configured by Admin.

**Function Trigger:** The Department Head saves the official-examination transcript buffer.

**Business Rules:** BR-10, BR-39, BR-64

### 3.3.35 Get Failed Grading Jobs

**Function Description:** The system displays grading jobs that failed and were kept in the dead-letter queue.

**Function Trigger:** Department Head opens the failed grading jobs.

**Business Rules:** BR-41, BR-42, BR-43

### 3.3.36 Replay Failed Grading Job

**Function Description:** Department Head runs a failed grading job again. A failed job is also retried by the system after five minutes. Replaying it does not delete the stored submission.

**Function Trigger:** Department Head replays one failed grading job.

**Business Rules:** BR-41, BR-42, BR-43

### Grade and appeal

### 3.3.37 Grade Examination Answer

**Function Description:** After a laboratory answer is stored, the system grades the transcript in the background. The kiosk does not show this score. A failed grading call is retried. If it still fails, the job is kept and retried after five minutes. The stored submission is not deleted. The Lecturer uses the result.

**Function Trigger:** A laboratory answer has been stored.

**Business Rules:** BR-25, BR-29, BR-41, BR-42, BR-43

### 3.3.38 Get Submission Confidence

**Function Description:** The system displays the confidence to the Lecturer of a graded submission. A submission is in the review group when it is marked suspicious, when the confidence is below 0.70, or when the result is a fail. Other graded submissions are in the high-confidence group.

**Function Trigger:** The Lecturer opens the confidence of a graded submission.

**Business Rules:** BR-30, BR-32

### 3.3.39 Get Evidence

**Function Description:** The Lecturer returns the evidence of a submission: the audio location, the original transcript, and the grading rationale.

**Function Trigger:** The Lecturer opens the evidence of a submission.

**Business Rules:** BR-67

### 3.3.40 Play Examination Audio

**Function Description:** The Lecturer plays the stored laboratory audio. Playback speed is from 1.0x to 1.5x.

**Function Trigger:** The Lecturer starts playback of the stored laboratory audio.

**Business Rules:** BR-67

### 3.3.41 Seek Examination Audio

**Function Description:** The Lecturer moves playback to a position in the stored audio.

**Function Trigger:** The Lecturer chooses a position in the stored audio.

**Business Rules:** BR-67

### 3.3.42 Jump To Spoken Timestamp

**Function Description:** The Lecturer jumps playback to the timestamp of a spoken part of the answer.

**Function Trigger:** The Lecturer chooses a spoken timestamp.

**Business Rules:** BR-67

### 3.3.43 Verify Grade

**Function Description:** The Lecturer reviews and verifies the initial official-exam grade produced by the LLM before the final grade can be published. Supporting evidence may include audio, transcript, and grading rationale; the low-confidence threshold is below 0.70.

**Function Trigger:** The Lecturer confirms the background grade after reviewing the evidence.

**Business Rules:** BR-30, BR-32

### 3.3.44 Adjust Grade With Reason

**Function Description:** The lecturer changes the grade. A reason is required. The change is written to the audit log.

**Function Trigger:** The Lecturer submits a new grade and a reason.

**Business Rules:** BR-31

### 3.3.45 Publish Grade

**Function Description:** The Lecturer publishes final official-exam grades only after Lecturer verification of the LLM-generated scores. The proposed once-per-shift publication and locking constraints remain subject to confirmation.

**Function Trigger:** The Lecturer publishes a shift in which every candidate has a grade.

**Business Rules:** BR-32, BR-33

### 3.3.46 Lock Grade

**Function Description:** A published grade is protected from ordinary edits. An appeal assigned by the Department Head enables the assigned Lecturer to submit an independent regrade through the appeal workflow; the published grade is not replaced until Department Head verification.

**Function Trigger:** A grade has just been published.

**Business Rules:** BR-33, BR-34

### 3.3.47 Assign Appeal

**Function Description:** The Department Head receives a Student appeal and assigns it to a Lecturer other than the Lecturer who verified the original official-exam grade. The assigned Lecturer is responsible for an independent regrade.

**Function Trigger:** The Department Head opens a received Student appeal and assigns a Lecturer to regrade it.

**Business Rules:** BR-34, BR-71

### 3.3.48 Regrade Appeal

**Function Description:** The Lecturer assigned by the Department Head reviews the original LLM grading result (score, evaluation and supporting evidence), as well as the first Lecturer’s verification where available. The assigned Lecturer then independently evaluates and regrades the submission. The result is forwarded for Department Head verification; it is not published as final directly by the regrading Lecturer.

**Function Trigger:** The Lecturer assigned to an appeal submits the new grade.

**Business Rules:** BR-34, BR-71

### 3.3.49 Export Grade To Excel

**Function Description:** The Lecturer exports the shift grade report as an Excel file in the FPT examination-office layout.

**Function Trigger:** The Lecturer exports a shift grade report to Excel.

**Business Rules:** BR-35

### 3.3.50 Export Grade To PDF

**Function Description:** The Lecturer exports the shift grade report as a PDF with a digital signature.

**Function Trigger:** The Lecturer exports a shift grade report to PDF.

**Business Rules:** BR-35

### 3.3.51 Get Appeals

**Function Description:** Department Head sees the appeals to assign. The assigned Lecturer sees the appeals assigned to that Lecturer.

**Function Trigger:** The Department Head or an assigned Lecturer opens the appeal list.

**Business Rules:** BR-34

### Courses, classes, and enrollments



### 3.3.52 Get Classes

**Function Description:** The Lecturer returns the classes.

**Function Trigger:** The Lecturer opens the class list.

**Business Rules:** BR-69

### 3.3.53 Create Class

**Function Description:** Creates a class for a course and a semester. The class has a lecturer assignment.

**Function Trigger:** The Lecturer submits a class code, course, semester, and lecturer.

**Business Rules:** BR-69

### 3.3.54 Update Class

**Function Description:** The Lecturer updates a class.

**Function Trigger:** The Lecturer submits changes to a class.

**Business Rules:** BR-69

### 3.3.55 Get Class Enrollments

**Function Description:** Returns the students enrolled in a class.

**Function Trigger:** The Lecturer opens the enrollment of a class.

**Business Rules:** BR-70

### 3.3.56 Create Class Enrollment

**Function Description:** Enrolls a student in a class.

**Function Trigger:** The Lecturer enrolls a student in a class.

**Business Rules:** BR-70

### 3.3.57 Update Class Enrollment

**Function Description:** The Lecturer updates an enrollment in a class.

**Function Trigger:** The Lecturer submits changes to an enrollment.

**Business Rules:** BR-70

### Dashboards

### 3.3.58 Get Lecturer Dashboard

**Function Description:** Returns the lecturer dashboard. The web client shows this role as Instructor.

**Function Trigger:** The Lecturer opens the lecturer home.

**Business Rules:** None specifically applicable in the approved Section 5.1 baseline.

### 3.3.59 Get Department Head Dashboard

**Function Description:** The Department Head returns the department-head dashboard.

**Function Trigger:** The Department Head opens the department-head home.

**Business Rules:** None specifically applicable in the approved Section 5.1 baseline.

### 3.3.60 Verify Appeal Regrade

**Function Description:** The Department Head reviews the independent regrade submitted by a Lecturer other than the original verifier. Department Head verification is required before the appeal outcome can be published as the official result. Department Head subsequently performs the separate Publish Appeal Result action.

**Function Trigger:** The Department Head opens a completed appeal regrade and confirms the reviewed outcome.

**Business Rules:** BR-34, BR-71


### 3.3.61 Publish Appeal Result

**Function Description:** Department Head publishes the official appeal result only after reviewing and verifying the independently regraded result. The assigned Lecturer cannot publish an appeal result.

**Function Trigger:** Department Head selects Publish for an appeal whose independent regrade has been verified by Department Head.

**Business Rules:** BR-34, BR-71

---

## Approved Section 5.1 — Master Business Rules (71 rules)

The following table is the approved consolidated baseline. IDs in the function specifications above refer exclusively to this table, not to the former 80-rule draft. The bilingual wording below is preserved from the approved source.

| ID | Rule Definition |
|:---:|---|
| BR-01 | Accounts for all five roles are provisioned or authorized by the university. Students are issued username/password credentials; Google sign-in is permitted only for accounts whose email identity has been provisioned/authorized. No unknown Google email automatically creates a Student account. |
| BR-02 | An account’s role is determined from the stored account record. The system does not offer public self-registration or permit the client to select its own role. |
| BR-03 | Người dùng có tài khoản do nhà trường cấp chỉ được đổi mật khẩu mới sau khi xác nhận đúng mật khẩu hiện tại. |
| BR-04 | Chỉ Admin mới có quyền đổi vai trò hoặc khóa tài khoản, và không thể tự khóa tài khoản của chính mình. |
| BR-05 | Tài khoản bị khóa không thể đăng nhập hoặc truy cập chức năng hệ thống cho đến khi được Admin mở khóa. |
| BR-06 | Các phiên luyện tập chỉ rút câu hỏi từ kho câu hỏi luyện tập đã được phê duyệt. |
| BR-07 | Chế độ luyện Từng câu không cho phép xuất hiện quá 2 câu hỏi liên tiếp có cùng mức độ khó. |
| BR-08 | Chế độ luyện Từng câu chỉ sinh câu hỏi phụ AI (1–5 câu, mặc định 2) khi điểm câu chính đạt từ 4.0 đến 8.0. |
| BR-09 | Chế độ luyện Toàn phiên yêu cầu từ 3 đến 10 câu hỏi sắp xếp từ Dễ đến Khó và không có câu hỏi phụ. |
| BR-10 | Sinh viên có màn hình đệm đếm ngược (10–300s, mặc định 60s) để rà soát và sửa các thuật ngữ tiếng Anh chuyên ngành. |
| BR-11 | Các phiên luyện tập và thi thử chỉ lưu văn bản trả lời và không lưu trữ tệp âm thanh. |
| BR-12 | A practice session closes automatically after 5 minutes without interaction, preserving scores already awarded. The inactivity timeout is configurable by Admin, with a default of 5 minutes. |
| BR-13 | Bài thi thử rút câu hỏi từ kho luyện tập theo cấu trúc đề và ma trận Bloom do Trưởng Bộ môn thiết lập. |
| BR-14 | A Student cannot start more mock examinations for a course on a given day than the course-specific daily quota configured by Admin. |
| BR-15 | Sinh viên có thể chọn bật hoặc tắt câu hỏi phụ AI theo ngữ cảnh trước khi bắt đầu bài thi thử. |
| BR-16 | Bài thi thử khóa nhập liệu bàn phím ở câu trả lời chính và bắt buộc thu âm qua micro. |
| BR-17 | Bài thi thử tự động nộp khi hết giờ và hiển thị ngay bảng điểm chi tiết theo từng CLO. |
| BR-18 | Syllabi and CLOs are imported/read from FLM and cannot be edited in OES. Admin and Department Head may create and edit OES course-management information without altering the FLM-owned syllabus or CLOs. |
| BR-19 | Mỗi câu hỏi phải có đáp án mẫu tối thiểu 50 ký tự và được gán vào ít nhất một kho câu hỏi. |
| BR-20 | Tổng trọng số các tiêu chí trong barem của một câu hỏi phải bằng chính xác 10.0 điểm. |
| BR-21 | Câu hỏi do Giảng viên tạo khởi đầu ở trạng thái nháp và phải gửi Trưởng Bộ môn phê duyệt. |
| BR-22 | Chỉ Trưởng Bộ môn mới có quyền phê duyệt, yêu cầu chỉnh sửa hoặc từ chối câu hỏi. |
| BR-23 | Only Department Head can create official examination events, lab shifts, and official exam rules. A Lecturer may manage, conduct, and grade only assigned examinations within the permissions of the assigned role. |
| BR-24 | Mỗi thí sinh được xếp một số ghế (1–40) gắn với IP máy trạm phòng Lab và phải được Giám thị điểm danh. |
| BR-25 | Vé thi chính thức chuyển tuần tự qua 7 trạng thái: SCHEDULED, IN_PROGRESS, SUBMITTED, AI_GRADED, AUDITED, PUBLISHED, và LOCKED. |
| BR-26 | Thí sinh phải vượt qua bước kiểm tra micro 30 giây đạt tối thiểu 60 dB trước khi vào phòng thi Kiosk. |
| BR-27 | If an official-exam candidate exits fullscreen or loses window focus more than three times, the system automatically suspends that candidate’s exam attempt and notifies the Proctor. |
| BR-28 | Official examination audio is retained as STT_MSSV.webm and protected by a SHA-256 integrity hash. The specified storage destination is Cloudflare R2. |
| BR-29 | Máy trạm Kiosk khóa màn hình ngay sau khi nộp bài và tuyệt đối không hiển thị điểm liền hay khiếu nại tại chỗ. |
| BR-30 | Bài thi chính thức chấm AI xong được chia thành Nhóm 1 (nghi ngờ hoặc độ tin cậy dưới 0.70) và Nhóm 2 (tin cậy cao) để hậu kiểm. |
| BR-31 | Giảng viên bắt buộc nhập lý do giải trình mỗi khi chỉnh sửa điểm do AI chấm. |
| BR-32 | A Lecturer may publish initial official-exam shift grades only after Lecturer verification of 100% of submitted exams in that shift. This rule does not authorize publication of an appeal result. |
| BR-33 | Công bố điểm ca thi sẽ kích hoạt khóa một chiều, chặn mọi thao tác sửa hoặc xóa điểm trái phép. |
| BR-34 | A Student submits an appeal to Department Head, who must assign a Lecturer different from the original grade verifier. The assigned Lecturer independently regrades after reviewing the LLM score, comments, and any original Lecturer verification. Department Head then verifies the appeal outcome and personally publishes the official appeal result. |
| BR-35 | Bảng điểm xuất ra Excel và PDF có chữ ký số luôn kèm mã băm công khai để xác thực tính toàn vẹn. |
| BR-36 | Học kỳ phải có ngày bắt đầu/kết thúc hợp lệ và chỉ được Admin cập nhật, tuyệt đối không được xóa. |
| BR-37 | Admin and Department Head may create and edit OES course-management details. Admin configures course-specific daily mock quotas. Lecturer may not create or edit courses. Only Department Head configures official exam rules, shifts, and mock-paper structure, Bloom distribution, and duration. |
| BR-38 | Mỗi sinh viên chỉ được ghi danh vào tối đa một lớp học phần của cùng một môn trong một học kỳ. |
| BR-39 | Admin configures global practice parameters, including follow-up count (1–5; default 2), full-session question count (3–10), transcript buffer (10–300 seconds; default 60 seconds), and inactivity timeout (default 5 minutes). |
| BR-40 | Tham số luyện tập mới chỉ áp dụng cho các phiên tạo mới và không ảnh hưởng tới phiên đang diễn ra. |
| BR-41 | Nếu việc chấm AI tự động thất bại sau 3 lần thử, bài nộp của sinh viên vẫn được giữ nguyên và chuyển vào hàng đợi lỗi (DLQ) để xử lý chấm bù. |
| BR-42 | After grading fails three attempts, the stored submission remains recoverable in the DLQ. An automatic retry is scheduled after 5 minutes; Department Head may also initiate manual replay. |
| BR-43 | Việc chạy lại tác vụ từ DLQ sẽ không ghi đè nếu bài thi đó đã được chấm điểm thành công. |
| BR-44 | Mọi thao tác nhạy cảm (đăng nhập, đổi vai trò, sửa điểm, công bố điểm, đổi cấu hình) đều được ghi vào nhật ký kiểm toán. |
| BR-45 | Nhật ký kiểm toán chỉ được phép đọc, không một ai kể cả Admin được quyền sửa hoặc xóa. |
| BR-46 | A signed-out refresh token must not be accepted for session renewal. |
| BR-47 | Only active courses may be selected for new practice or mock examination sessions. |
| BR-48 | A user may access and mark read only their own notifications. |
| BR-49 | A main practice question already asked within the same session must not be drawn again; exhaustion must be reported instead of silently repeating a question. |
| BR-50 | Per-question practice issues the first question when the session starts and subsequent questions only upon the Student requesting the next question. |
| BR-51 | Practice questions are selected by course; chapter-level filtering is not supported in the specified practice workflow. |
| BR-52 | Full-session practice may accept answers individually or as a complete set; unanswered questions are not awarded a score. |
| BR-53 | Practice results retain a score and feedback for each applicable grading criterion. |
| BR-54 | Students may view their own practice and mock-exam histories. |
| BR-55 | Mock follow-up questions, when enabled, are generated from answer context and are not restricted by the per-question practice score band of 4.0–8.0. |
| BR-56 | The official examination input mode is either voice-only or voice with transcript editing; editing is permitted only in the latter mode. |
| BR-57 | A candidate may view an official-exam grade only after initial publication and only through the Student Portal. |
| BR-58 | Questions are classified by course, Bloom level, and CLO; no chapter field is required for this classification. |
| BR-59 | Only authorized Lecturer and Department Head users may view questions within their assigned question-bank scope. |
| BR-60 | Question difficulty is classified as Easy, Medium, or Hard. |
| BR-61 | Only questions approved by Department Head become available in their approved practice and/or official-exam question pools. |
| BR-62 | Department Head configures official-exam follow-up parameters independently of global practice settings; the permitted follow-up count is 1–5 with default 2. |
| BR-63 | A Proctor is an assigned Lecturer or an externally provisioned staff member and may authenticate only through a preauthorized Google identity or university-issued username/password credentials. |
| BR-64 | Official-exam transcript-review buffer is configured separately from the practice buffer; permitted range is 10–300 seconds with a default of 60 seconds. |
| BR-65 | Only an authorized Proctor may complete candidate identity checks, check-in, and lab-shift start actions. |
| BR-66 | A candidate seat number is within 1–40 and must be linked to the candidate’s assigned workstation for the relevant exam shift. |
| BR-67 | An official-exam grade reviewer must have access to retained audio, transcript, and AI grading rationale; supported playback speeds are 1.0×–1.5×. |
| BR-68 | Department Head inherits Lecturer permissions, except where a specific function is restricted by a higher-priority explicit role rule; Lecturer has no course create/edit permission. |
| BR-69 | Authorized Lecturer and Department Head users may create and update classes for their permitted courses and semesters. |
| BR-70 | Authorized Lecturer and Department Head users may maintain enrollment rosters of classes within their scope. |
| BR-71 | An appeal outcome may be officially published only by Department Head after Department Head verification of the independent regrade. |
