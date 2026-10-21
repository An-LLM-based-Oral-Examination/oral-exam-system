# Functional specification for copy into Report 7

This file is a personal draft. It does not edit `Report7_Final Project Report.docx`.

In the course template, heading 3.1 is System Functional Overview. The 3.1 below is only the shared functions. Paste it where you want that block to sit.

Actor names in this draft: Student, Lecturer, Department Head, Proctor, Admin.

The database stores the lecturer role as `lecturer`. The web client labels that same role Instructor. This draft uses Lecturer, which is the actor name already fixed for the report.

Proctor is placed in 3.3. Those functions are shift supervision in the lab. They are not Student functions and they are not Admin functions. A Lecturer can be assigned as the person who proctors a shift.

Each subsection is one function from Figure 1.6.1. Function Description states what the function does. Function Trigger states the event that starts it and what the system does then. No screen capture is included, and no button label is added. Business rules use BR-01, BR-02, and so on, in the table at the end of each section.

## 3.1 Common functions

### 3.1.1 Sign In With Google

**Function Description:** The user signs in with a Google ID token. The role sent by the client is ignored. If the email is not stored, the system creates a Student account and leaves the student code empty. If the email already belongs to an active account, the system keeps the role stored for that account.

**Function Trigger:**

Sign In With Google:
Trigger: The user is signed out and submits a Google ID token.
Function: The system opens a session. A new email creates a Student account with an empty student code. An existing active account keeps its stored role. An invalid token is rejected. A locked account is rejected.

**Business Rules:** BR-01, BR-02, BR-03, BR-04

### 3.1.2 Sign Up

**Function Description:** There is no separate registration form and no email-and-password registration. A new Student account is created when Sign In With Google succeeds for an email that is not stored yet. The student code is left empty.

**Function Trigger:**

Sign Up:
Trigger: Sign In With Google succeeds for an email that is not stored.
Function: The system creates a Student account and leaves the student code empty. A locked account is not created again.

**Business Rules:** BR-01, BR-03

### 3.1.3 Sign In

**Function Description:** Student, Lecturer, Department Head, Proctor, and Admin can use this function. An existing account signs in with username and password. The role is read from the stored account. The client cannot submit a role. This function does not create an account.

**Function Trigger:**

Sign In:
Trigger: The user submits a username and a password for an account that already exists.
Function: The system opens a session for the stored account and reads the role from that account. An unknown account, a wrong password, or a locked account is rejected. This function does not create an account.

**Business Rules:** BR-01, BR-02, BR-05

### 3.1.4 Sign Out

**Function Description:** The signed-in user signs out. The system revokes that refresh token. A new sign-in is required to continue.

**Function Trigger:**

Sign Out:
Trigger: The signed-in user signs out and the current refresh token is sent.
Function: The system revokes that refresh token. A missing or invalid refresh token is rejected.

**Business Rules:** BR-06

### 3.1.5 Get Courses

**Function Description:** Returns the active courses. Student uses this list to choose a course for practice or a mock examination. Lecturer uses it when maintaining courses.

**Function Trigger:**

Get Courses:
Trigger: A signed-in user opens the course list.
Function: The system returns the active courses.

**Business Rules:** BR-07

### 3.1.6 Get Notifications

**Function Description:** Returns the in-app notifications of the signed-in user. A notification has a title, a message, a type, a read flag, and the time it was created.

**Function Trigger:**

Get Notifications:
Trigger: The signed-in user opens the notification list.
Function: The system returns that user's notifications.

**Business Rules:** BR-08, BR-09

### 3.1.7 Mark Notification As Read

**Function Description:** Marks one notification of the signed-in user as read and stores the time it was read.

**Function Trigger:**

Mark Notification As Read:
Trigger: The signed-in user marks one of that user's notifications as read.
Function: The system stores the notification as read and stores the time it was read.

**Business Rules:** BR-08

### 3.1.8 Mark All Notifications As Read

**Function Description:** Marks every unread notification of the signed-in user as read.

**Function Trigger:**

Mark All Notifications As Read:
Trigger: The signed-in user marks every unread notification as read.
Function: The system marks that user's unread notifications as read.

**Business Rules:** BR-08

### Business rules for 3.1

| ID | Rule |
| --- | --- |
| BR-01 | The role is the value stored on the account. The system does not accept a role sent by the client. |
| BR-02 | A locked account cannot sign in. |
| BR-03 | The first successful Google sign-in for an unknown email creates a Student account and leaves the student code empty. |
| BR-04 | If the email already belongs to an active account, Google sign-in keeps the stored role. |
| BR-05 | Sign In with username and password does not create an account. |
| BR-06 | Sign Out revokes the refresh token of that session. |
| BR-07 | Get Courses returns active courses. |
| BR-08 | A user can read and mark only that user's notifications. |
| BR-09 | A notification type is one of: exam schedule published, exam grade published, appeal lecturer assigned, question submitted for review, question needs revision. |

## 3.2 Web for User and Admin

User in this section is Student.

### Practice exam

### 3.2.1 Create Per-Question Practice Session

**Function Description:** Starts a per-question practice session for one course. The student chooses one difficulty, or a combination of easy, medium, and hard. The session receives question 1 when it is created. Later questions are requested one at a time. The student does not have to fix the number of questions in advance.

**Function Trigger:**

Create Per-Question Practice Session:
Trigger: The student chooses a course and a difficulty, or a combination of easy, medium, and hard, and starts per-question practice.
Function: The system creates a session in progress and returns question 1. If the selected pool has no usable question, the session is not created.

**Business Rules:** BR-10, BR-11, BR-12

### 3.2.2 Create Full-Session Practice

**Function Description:** Starts a full-session practice. The student enters a question count from the configured minimum to the configured maximum. The default range is 3 to 10. The system draws the whole set at creation and orders it from easy to medium to hard. This mode has no follow-up question.

**Function Trigger:**

Create Full-Session Practice:
Trigger: The student chooses a course, enters a question count inside the configured range, and starts full-session practice.
Function: The system draws the whole set, orders it from easy to medium to hard, and creates the session. If any difficulty is short, the system rejects the request and does not create a session.

**Business Rules:** BR-10, BR-13, BR-14

### 3.2.3 Filter Practice By Topic

**Function Description:** The student filters practice by the selected course. The project does not use a chapter. Practice questions are classified by course, Bloom level, and CLO.

**Function Trigger:**

Filter Practice By Topic:
Trigger: The student selects a course as the practice scope.
Function: The system limits practice to that course.

**Business Rules:** BR-15

### 3.2.4 Enter Easy Question Count

**Function Description:** Used when the full-session set is divided across difficulties. The easy count is part of an even split from easy to hard. The student does not type three independent counts that break that split.

**Function Trigger:**

Enter Easy Question Count:
Trigger: A full-session question count is being split across difficulties.
Function: The system assigns the easy portion of that even split.

**Business Rules:** BR-13

### 3.2.5 Enter Medium Question Count

**Function Description:** Student can use this function. The medium count is the middle portion of the same even split used by the full-session practice.

**Function Trigger:**

Enter Medium Question Count:
Trigger: A full-session question count is being split across difficulties.
Function: The system assigns the medium portion of that even split.

**Business Rules:** BR-13

### 3.2.6 Enter Hard Question Count

**Function Description:** Student can use this function. The hard count is the last portion of the same even split used by the full-session practice.

**Function Trigger:**

Enter Hard Question Count:
Trigger: A full-session question count is being split across difficulties.
Function: The system assigns the hard portion of that even split.

**Business Rules:** BR-13

### 3.2.7 Read Practice Question

**Function Description:** Shows the current practice question. The question can be read aloud. The student may answer by voice or by typing. Practice does not store audio.

**Function Trigger:**

Read Practice Question:
Trigger: A practice session has a current question and the student opens it.
Function: The system shows the question text. The question can be read aloud.

**Business Rules:** BR-16

### 3.2.8 Submit Practice Answer

**Function Description:** Submits the transcript of the current practice answer. In full-session practice the student may submit one answer or submit the package of answers. The system stores the transcript, not audio.

**Function Trigger:**

Submit Practice Answer:
Trigger: The student confirms the current practice answer, or confirms the full-session answer package.
Function: The system stores the transcript for grading. A session already closed for inactivity rejects the submission.

**Business Rules:** BR-16, BR-17, BR-18

### 3.2.9 Update Practice Transcript

**Function Description:** After speech is transcribed, the student can correct SE terms on the buffer screen before the answer is submitted. The buffer countdown uses the configured duration. The default is 60 seconds. The allowed range is 10 to 300 seconds.

**Function Trigger:**

Update Practice Transcript:
Trigger: Speech has been transcribed and the buffer countdown is still running.
Function: The system accepts the corrected transcript and keeps that text for submission.

**Business Rules:** BR-19

### 3.2.10 Grade Practice Answer

**Function Description:** Student can use this function. The system grades the stored practice transcript against the question rubric. The result is a score and comments by criterion. Per-question practice can open a follow-up when the score is in the follow-up band. Full-session practice does not create a follow-up.

**Function Trigger:**

Grade Practice Answer:
Trigger: A practice transcript has been stored.
Function: The system grades it against the question rubric and returns the score and the comment for each criterion.

**Business Rules:** BR-14, BR-20

### 3.2.11 Create Practice Follow-Up Question

**Function Description:** In per-question practice only, a follow-up question is created when the score is from 4.0 to 8.0 inclusive. The number of follow-up questions is configured by Admin, from 1 to 5. The default is 2. A score outside that band opens the scorecard instead. Full-session practice never creates a follow-up.

**Function Trigger:**

Create Practice Follow-Up Question:
Trigger: A per-question answer has been graded.
Function: The system creates a follow-up question when the score is from 4.0 to 8.0 inclusive. Outside that band, the system opens the scorecard. Full-session practice does not create a follow-up.

**Business Rules:** BR-14, BR-20

### 3.2.12 Get Practice Scorecard

**Function Description:** The Student returns the practice scorecard for the answers already graded, including the comment for each criterion.

**Function Trigger:**

Get Practice Scorecard:
Trigger: The student opens the result of a practice session.
Function: The system returns the scorecard for the answers already graded.

**Business Rules:** BR-21

### 3.2.13 Show Practice Question Shortage

**Function Description:** Tells the student when the selected difficulty pool cannot supply another question, or when a full-session set cannot be filled. Per-question practice stops offering more questions. Full-session practice is rejected before the session exists.

**Function Trigger:**

Show Practice Question Shortage:
Trigger: The selected pool cannot supply the next question, or a full-session set cannot be filled.
Function: The system reports the shortage. Per-question practice stops offering more questions. A full-session request that cannot be filled does not leave a session.

**Business Rules:** BR-12, BR-13

### 3.2.14 Get Practice History

**Function Description:** Returns the student's past practice sessions on the student portal, on the same history surface as mock examination history.

**Function Trigger:**

Get Practice History:
Trigger: The student opens practice history on the student portal.
Function: The system returns that student's past practice sessions.

**Business Rules:** BR-22

### Mock examination, student functions

### 3.2.15 Create Mock Session

**Function Description:** Starts a timed mock examination for one course. Questions are drawn from the practice-question bank. The student does not choose topic or difficulty. The attempt counts against the daily limit configured for that course by the Department Head.

**Function Trigger:**

Create Mock Session:
Trigger: The student chooses a course and starts a mock examination.
Function: The system creates the session when the student is still inside the daily limit. Over the limit, the attempt is rejected.

**Business Rules:** BR-23, BR-24, BR-25

### 3.2.16 Set Mock Follow-Up Option

**Function Description:** Before the mock session starts, the student chooses whether this attempt uses follow-up questions. The student does not set the follow-up count. That count belongs to the Department Head configuration.

**Function Trigger:**

Set Mock Follow-Up Option:
Trigger: The student chooses yes or no for follow-up before the mock session starts.
Function: The system stores that choice with the session.

**Business Rules:** BR-26

### 3.2.17 Read Mock Question

**Function Description:** The Student shows the current mock question. The attempt is voice-first: the keyboard stays locked and a microphone is required. Mock examination does not store audio.

**Function Trigger:**

Read Mock Question:
Trigger: A mock session has a current question and the student opens it.
Function: The system shows the question and keeps the attempt voice-first.

**Business Rules:** BR-16, BR-27

### 3.2.18 Submit Mock Answer

**Function Description:** The Student submits the transcript of the current mock answer. The system stores the transcript, not audio.

**Function Trigger:**

Submit Mock Answer:
Trigger: The student confirms the current mock answer.
Function: The system stores the transcript.

**Business Rules:** BR-16, BR-28

### 3.2.19 Update Mock Transcript

**Function Description:** The student can correct the transcript during the configured buffer before submission. The default buffer is 60 seconds, in the range 10 to 300 seconds. The keyboard lock of the voice-first gate still applies to composing the original answer.

**Function Trigger:**

Update Mock Transcript:
Trigger: The mock transcript buffer is still running.
Function: The system accepts the corrected transcript and keeps that text for submission.

**Business Rules:** BR-19, BR-27

### 3.2.20 Grade Mock Answer

**Function Description:** Student can use this function. The system grades the stored mock transcript and returns the score for that answer. Grading uses the question rubric.

**Function Trigger:**

Grade Mock Answer:
Trigger: A mock transcript has been stored.
Function: The system grades it against the question rubric and returns the score.

**Business Rules:** BR-29

### 3.2.21 Create Mock Follow-Up Question

**Function Description:** If the student turned follow-up on for this attempt, the system asks a follow-up from the answer context. The follow-up is not selected by the 4.0 to 8.0 score band used in practice. A follow-up attempt runs longer than an attempt without follow-up. If the student turned follow-up off, no follow-up question is created.

**Function Trigger:**

Create Mock Follow-Up Question:
Trigger: A mock answer has been graded and the session follow-up choice is yes.
Function: The system creates a follow-up from the answer context. If the choice is no, no follow-up is created.

**Business Rules:** BR-26, BR-30

### 3.2.22 Get Mock Scorecard

**Function Description:** The Student returns the mock scorecard on the result screen and stores it in the examination history. The scorecard includes the comment for each criterion.

**Function Trigger:**

Get Mock Scorecard:
Trigger: The student opens the mock result.
Function: The system returns the scorecard and keeps it in the examination history.

**Business Rules:** BR-29, BR-22

### 3.2.23 Get Mock Examination History

**Function Description:** Returns the student's past mock examinations on the student portal, together with practice history.

**Function Trigger:**

Get Mock Examination History:
Trigger: The student opens mock examination history on the student portal.
Function: The system returns that student's past mock examinations.

**Business Rules:** BR-22

### 3.2.24 Submit Mock Examination On Timeout

**Function Description:** When the mock timer reaches zero, the system submits the attempt. Unanswered questions stay blank and are not graded. The student cannot continue that attempt.

**Function Trigger:**

Submit Mock Examination On Timeout:
Trigger: The mock timer reaches zero.
Function: The system submits the attempt. Unanswered questions stay blank and are not graded.

**Business Rules:** BR-28

### Laboratory examination, candidate functions

### 3.2.25 Check Microphone

**Function Description:** Student can use this function. Before the laboratory questions start, the workstation tests the microphone for 30 seconds. The test passes only when the level is at least 60 dB.

**Function Trigger:**

Check Microphone:
Trigger: The candidate starts the microphone test on the assigned workstation.
Function: The system runs the 30-second test and passes it only when the level is at least 60 dB.

**Business Rules:** BR-31

### 3.2.26 Lock Examination Screen

**Function Description:** Student can use this function. The laboratory examination runs in a locked full screen. Alt+Tab, the Windows key, F11, F12, and developer tools are blocked. If the window blurs more than 3 times, the attempt is suspended.

**Function Trigger:**

Lock Examination Screen:
Trigger: The candidate enters the examination after check-in.
Function: The system locks the screen. More than 3 blurs suspend the attempt.

**Business Rules:** BR-32

### 3.2.27 Read Examination Question

**Function Description:** The Student shows the current official examination question on the locked workstation. The input mode of the session is either voice only, or voice with transcript edit.

**Function Trigger:**

Read Examination Question:
Trigger: The locked workstation has a current examination question.
Function: The system shows that question.

**Business Rules:** BR-33

### 3.2.28 Submit Voice Answer

**Function Description:** The Student submits the spoken answer. The workstation stores the submission first, then the audio file. The ticket moves to SUBMITTED. The kiosk does not show a score.

**Function Trigger:**

Submit Voice Answer:
Trigger: The candidate finishes the spoken answer.
Function: The system stores the submission and sets the ticket to SUBMITTED. The kiosk does not show a score.

**Business Rules:** BR-34, BR-35

### 3.2.29 Update Examination Transcript

**Function Description:** Available only when the session input mode is voice with transcript edit. The student can correct the transcript during the buffer configured for that examination. The buffer is from 10 to 300 seconds. Voice-only mode does not open transcript editing.

**Function Trigger:**

Update Examination Transcript:
Trigger: The input mode is voice with transcript edit and the examination buffer is still running.
Function: The system accepts the corrected transcript. Voice-only mode does not open this function.

**Business Rules:** BR-33, BR-19

### 3.2.30 Store Examination Audio

**Function Description:** The laboratory examination stores one audio file per candidate. The file name is the seat number and the student code, for example `01_SE170123.webm`. The workstation keeps a SHA-256 hash of that file. Practice and mock examination do not store audio.

**Function Trigger:**

Store Examination Audio:
Trigger: The laboratory attempt has an audio stream.
Function: The system stores one audio file named with the seat number and the student code, and keeps the SHA-256 hash with the submission.

**Business Rules:** BR-36

### 3.2.31 Suspend Examination

**Function Description:** Student can use this function. The attempt is suspended when the locked window blurs more than 3 times.

**Function Trigger:**

Suspend Examination:
Trigger: The locked examination window blurs more than 3 times.
Function: The system suspends the attempt.

**Business Rules:** BR-32

### 3.2.32 Submit Examination

**Function Description:** The Student submits the laboratory attempt. The submission and the ticket are stored as SUBMITTED. The kiosk then shows that the paper is stored. It does not show a score and it does not accept an appeal at the workstation.

**Function Trigger:**

Submit Examination:
Trigger: The candidate submits the laboratory attempt.
Function: The system stores the ticket as SUBMITTED and shows that the paper is stored. It does not show a score and it does not accept an appeal.

**Business Rules:** BR-34, BR-35

### 3.2.33 Confirm Examination Submission

**Function Description:** The Student shows the candidate that the submission was stored. The candidate signs the paper record before leaving. The screen does not show a grade.

**Function Trigger:**

Confirm Examination Submission:
Trigger: The ticket has just been stored as SUBMITTED.
Function: The system shows the storage confirmation. It does not show a grade.

**Business Rules:** BR-35

### Grade and appeal, student functions

### 3.2.34 Get Published Grade

**Function Description:** After the lecturer publishes the shift, the student can open the published grade on the student portal. The grade is not available on the laboratory kiosk.

**Function Trigger:**

Get Published Grade:
Trigger: The student opens a published grade on the student portal.
Function: The system returns that grade. The laboratory kiosk does not serve this function.

**Business Rules:** BR-37

### 3.2.35 Create Appeal

**Function Description:** On the student portal, the student submits an appeal for a published grade. The appeal is sent to the Department Head. The kiosk has no appeal action.

**Function Trigger:**

Create Appeal:
Trigger: The student submits an appeal for a published grade on the student portal.
Function: The system creates an appeal and sends it to the Department Head.

**Business Rules:** BR-37, BR-38

### 3.2.36 Confirm Published Grade

**Function Description:** The student confirms the published grade. The ticket acknowledgement is stored. This is the alternative to creating an appeal.

**Function Trigger:**

Confirm Published Grade:
Trigger: The student confirms a published grade.
Function: The system stores the acknowledgement.

**Business Rules:** BR-37

### Dashboard

### 3.2.37 Get Student Dashboard

**Function Description:** Returns the student dashboard on the student portal.

**Function Trigger:**

Get Student Dashboard:
Trigger: The student opens the student portal home.
Function: The system returns that student's dashboard.

**Business Rules:** BR-39

### System configuration, Admin functions

Admin does not configure the mock-examination daily limit and does not configure an official examination.

### 3.2.38 Get Semesters

**Function Description:** The Admin returns the semesters.

**Function Trigger:**

Get Semesters:
Trigger: Admin opens the semester list.
Function: The system returns the semesters.

**Business Rules:** BR-40

### 3.2.39 Create Semester

**Function Description:** The Admin creates a semester with a code, a name, a start date, and an end date. The end date is not before the start date.

**Function Trigger:**

Create Semester:
Trigger: Admin submits a semester code, name, start date, and end date.
Function: The system creates the semester. An end date before the start date is rejected.

**Business Rules:** BR-40, BR-41

### 3.2.40 Update Semester

**Function Description:** The Admin updates a semester. A semester is not deleted.

**Function Trigger:**

Update Semester:
Trigger: Admin submits changes to an existing semester.
Function: The system updates the semester. The semester is not deleted.

**Business Rules:** BR-40, BR-41

### 3.2.41 Configure AI Model

**Function Description:** Admin can use this function. This function is the system-configuration entry for the AI model. Question generation and answer grading use Gemini. Speech-to-text uses Whisper. No separate model-parameter screen is specified beyond this function.

**Function Trigger:**

Configure AI Model:
Trigger: Admin saves the AI model setting.
Function: The system stores that setting. Question generation and grading use Gemini. Speech-to-text uses Whisper.

**Business Rules:** BR-42

### 3.2.42 Get Failed Grading Jobs

**Function Description:** The Admin returns grading jobs that failed and were kept in the dead-letter queue.

**Function Trigger:**

Get Failed Grading Jobs:
Trigger: Admin opens the failed grading jobs.
Function: The system returns the jobs kept in the dead-letter queue.

**Business Rules:** BR-43

### 3.2.43 Replay Failed Grading Job

**Function Description:** The Admin runs a failed grading job again. A failed job is also retried by the system after five minutes. Replaying it does not delete the stored submission.

**Function Trigger:**

Replay Failed Grading Job:
Trigger: Admin replays one failed grading job.
Function: The system queues that job again. The stored submission is not deleted.

**Business Rules:** BR-43

### 3.2.44 Configure Practice Question Range

**Function Description:** The Admin sets the full-session practice size. The minimum and the maximum are stored as system configuration. The default minimum is 3 and the default maximum is 10.

**Function Trigger:**

Configure Practice Question Range:
Trigger: Admin submits the minimum and maximum full-session question count.
Function: The system stores the practice range. The default is 3 to 10.

**Business Rules:** BR-13

### 3.2.45 Configure Practice Follow-Up

**Function Description:** Sets how many follow-up questions per-question practice may ask. The allowed range is 1 to 5. The default is 2. A Lecturer cannot configure practice follow-up.

**Function Trigger:**

Configure Practice Follow-Up:
Trigger: Admin submits the practice follow-up count.
Function: The system stores a count from 1 to 5. The default is 2.

**Business Rules:** BR-20

### 3.2.46 Get Audit Logs

**Function Description:** The Admin returns the audit log of sensitive changes, including grade changes and question-bank changes.

**Function Trigger:**

Get Audit Logs:
Trigger: Admin opens the audit log.
Function: The system returns the log of sensitive changes.

**Business Rules:** BR-44

### 3.2.47 Configure Transcript Buffer

**Function Description:** The Admin sets the practice transcript buffer in system configuration. The allowed range is 10 to 300 seconds. The default is 60 seconds. The buffer for an official examination is configured on that examination, not by this function.

**Function Trigger:**

Configure Transcript Buffer:
Trigger: Admin submits the practice transcript buffer in seconds.
Function: The system stores a value from 10 to 300. The default is 60. This does not change an official-examination buffer.

**Business Rules:** BR-19

### User management

### 3.2.48 Get Users

**Function Description:** Returns the user accounts. Admin does not create an account by typing an email and a password.

**Function Trigger:**

Get Users:
Trigger: Admin opens the user list.
Function: The system returns the accounts. It does not create a user from this function.

**Business Rules:** BR-45

### 3.2.49 Get User Detail

**Function Description:** The Admin returns one user account, including the stored role and whether the account is active.

**Function Trigger:**

Get User Detail:
Trigger: Admin opens one user.
Function: The system returns that account, including the stored role and whether it is active.

**Business Rules:** BR-45

### 3.2.50 Update User

**Function Description:** The Admin updates an existing user record. This function does not create a user and does not replace Assign User Role or Update User Status.

**Function Trigger:**

Update User:
Trigger: Admin submits changes to an existing user, other than role and lock status.
Function: The system updates that record. It does not create a user.

**Business Rules:** BR-45

### 3.2.51 Update User Status

**Function Description:** The Admin locks or unlocks an account. A locked account cannot sign in.

**Function Trigger:**

Update User Status:
Trigger: Admin locks or unlocks an account.
Function: The system stores the status. A locked account cannot sign in.

**Business Rules:** BR-02, BR-45

### 3.2.52 Assign User Role

**Function Description:** Sets the stored role of an existing user. The role is one of Student, Lecturer, Department Head, Proctor, or Admin. The stored value for Lecturer is `lecturer`.

**Function Trigger:**

Assign User Role:
Trigger: Admin assigns a role to an existing user.
Function: The system stores one of Student, Lecturer, Department Head, Proctor, or Admin. The stored value for Lecturer is lecturer.

**Business Rules:** BR-01, BR-45

### Business rules for 3.2

| ID | Rule |
| --- | --- |
| BR-10 | Practice has two modes: per-question on demand, and full-session. |
| BR-11 | In per-question practice, at most two of any three consecutive main questions share the same difficulty. A question already used in that session is not drawn again. When the selected difficulty runs out, the session reports that no further question is available. |
| BR-12 | Per-question practice issues question 1 at creation and later questions only when the student asks for the next one. |
| BR-13 | Full-session practice uses the configured range. The default is 3 to 10 questions, split evenly and ordered from easy to medium to hard. If one difficulty is short, the system rejects the request and does not create a session. |
| BR-14 | Full-session practice has no follow-up question. |
| BR-15 | Practice is filtered by course. The system has no chapter. |
| BR-16 | Practice and mock examination store the transcript only. They do not store audio. |
| BR-17 | A practice session with no interaction for the configured inactivity time is closed. The default is 10 minutes. Scores already given are kept. In full-session practice, an unanswered question scores 0. A later action on that session is rejected. |
| BR-18 | Full-session practice accepts one answer at a time or the whole answer package. |
| BR-19 | The transcript buffer is from 10 to 300 seconds. The default is 60 seconds. Admin configures the practice buffer. The official-examination buffer is configured on that examination. |
| BR-20 | Per-question practice creates a follow-up only when the score is from 4.0 to 8.0 inclusive. Admin sets the count from 1 to 5. The default is 2. Outside that score band, the scorecard opens. A Lecturer cannot change this practice follow-up setting. |
| BR-21 | The practice scorecard shows the score and the comment for each criterion. |
| BR-22 | Practice history and mock history are both available on the student portal. |
| BR-23 | A mock examination draws questions from the practice-question bank, not from the official examination bank. |
| BR-24 | The Department Head sets the daily mock limit for each course. The limit is not fixed at 3, and Admin does not set it. An attempt over the limit is rejected. |
| BR-25 | For a mock examination the student chooses the course only. The student does not choose topic or difficulty. |
| BR-26 | The student chooses yes or no for follow-up on that mock attempt. The student does not choose the follow-up count. |
| BR-27 | A mock examination is voice-first. The keyboard is locked and a microphone is required. |
| BR-28 | When the mock timer ends, the attempt is submitted. Blank questions are not graded. |
| BR-29 | A mock result shows a scorecard with a comment for each criterion, and that result is kept in history. |
| BR-30 | A mock follow-up follows the answer context. It does not use the 4.0 to 8.0 practice band. An attempt with follow-up runs longer. |
| BR-31 | The laboratory microphone test lasts 30 seconds and passes at 60 dB or above. |
| BR-32 | The laboratory screen is locked. More than 3 blurs suspend the attempt. |
| BR-33 | The official examination input mode is voice only, or voice with transcript edit. Transcript edit exists only in the second mode. |
| BR-34 | The laboratory submission is stored first, and the ticket status becomes SUBMITTED. |
| BR-35 | The laboratory kiosk does not show a score and does not accept an appeal. |
| BR-36 | Laboratory audio is stored as one file named with the seat number and the student code, with a SHA-256 hash. |
| BR-37 | The student sees a laboratory grade only after it is published, and only on the student portal. |
| BR-38 | An appeal is created on the student portal and is sent to the Department Head. |
| BR-39 | The student dashboard is the student portal home for that student. |
| BR-40 | Admin creates and updates a semester. A semester is not deleted. |
| BR-41 | A semester end date is not before its start date. |
| BR-42 | Question generation and grading use Gemini. Speech-to-text uses Whisper. |
| BR-43 | A grading job that still fails is kept and can be replayed. The stored submission is not deleted. The system also retries a failed job after five minutes. |
| BR-44 | Audit logs record sensitive changes, including grade changes and question-bank changes. |
| BR-45 | Admin locks or unlocks an account and assigns a stored role. Admin does not create a user by email and password. |

## 3.3 Web for Instructor and Department Head

Lecturer is the report name. The database value is `lecturer`. The web client shows Instructor.

Proctor functions of the laboratory shift are in this section.

### Mock examination, Department Head configuration

### 3.3.1 Configure Mock Paper

**Function Description:** Configures the mock paper structure for a course. The student does not configure the paper.

**Function Trigger:**

Configure Mock Paper:
Trigger: The Department Head saves the mock paper structure of a course.
Function: The system stores that structure.

**Business Rules:** BR-46

### 3.3.2 Configure Bloom Distribution

**Function Description:** The Department Head configures the Bloom distribution of the mock paper for a course.

**Function Trigger:**

Configure Bloom Distribution:
Trigger: The Department Head saves the Bloom distribution of a mock paper.
Function: The system stores that distribution.

**Business Rules:** BR-46

### 3.3.3 Configure Mock Duration

**Function Description:** The Department Head configures how long a mock attempt lasts. An attempt with follow-up runs longer than an attempt without follow-up.

**Function Trigger:**

Configure Mock Duration:
Trigger: The Department Head saves the mock duration of a course.
Function: The system stores that duration.

**Business Rules:** BR-46, BR-47

### 3.3.4 Configure Mock Follow-Up

**Function Description:** Configures mock follow-up for a course. This is separate from the practice follow-up count, which Admin configures.

**Function Trigger:**

Configure Mock Follow-Up:
Trigger: The Department Head saves the mock follow-up setting of a course.
Function: The system stores that setting.

**Business Rules:** BR-47

### 3.3.5 Configure Attempt Limit

**Function Description:** Sets the maximum number of mock attempts per student per day for a course. The value is not fixed. Admin does not set this limit.

**Function Trigger:**

Configure Attempt Limit:
Trigger: The Department Head saves the daily mock limit of a course.
Function: The system stores that limit.

**Business Rules:** BR-48

### Question management

### 3.3.6 Get Syllabus

**Function Description:** The Lecturer or Department Head reads the course syllabus from FLM. The system reads FLM and does not write back to FLM.

**Function Trigger:**

Get Syllabus:
Trigger: A Lecturer or Department Head requests the syllabus of a course.
Function: The system reads the syllabus CLOs from FLM and does not write back to FLM.

**Business Rules:** BR-49

### 3.3.7 Generate Question From Syllabus

**Function Description:** The Lecturer or Department Head generates a question, a rubric, and a sample answer from the syllabus CLOs.

**Function Trigger:**

Generate Question From Syllabus:
Trigger: A Lecturer or Department Head asks for a question from selected syllabus CLOs.
Function: The system returns a draft question, a rubric, and a sample answer.

**Business Rules:** BR-49, BR-50

### 3.3.8 Generate Question By Topic

**Function Description:** The Lecturer or Department Head generates a question from a selected topic. The topic is not a chapter.

**Function Trigger:**

Generate Question By Topic:
Trigger: A Lecturer or Department Head asks for a question from a topic in the course.
Function: The system returns a draft question. The topic is not a chapter.

**Business Rules:** BR-49, BR-51

### 3.3.9 Get Questions

**Function Description:** The Lecturer or Department Head returns the questions the actor is allowed to open in the question bank.

**Function Trigger:**

Get Questions:
Trigger: A Lecturer or Department Head opens the question bank.
Function: The system returns the questions in that actor's scope.

**Business Rules:** BR-52

### 3.3.10 Create Question

**Function Description:** The Lecturer or Department Head creates a question by hand or from a generated draft. A new question is a draft. The author chooses the practice bank, the examination bank, or both.

**Function Trigger:**

Create Question:
Trigger: A Lecturer or Department Head saves a new question.
Function: The system stores it as a draft, including the selected bank.

**Business Rules:** BR-50, BR-53, BR-54

### 3.3.11 Update Question

**Function Description:** The Lecturer or Department Head updates a question that is still a draft or that was returned for revision.

**Function Trigger:**

Update Question:
Trigger: A Lecturer or Department Head saves changes to a draft or to a question returned for revision.
Function: The system stores the updated question.

**Business Rules:** BR-50, BR-53

### 3.3.12 Get Rubrics

**Function Description:** The Lecturer or Department Head returns the rubrics of a course or of a question.

**Function Trigger:**

Get Rubrics:
Trigger: A Lecturer or Department Head opens the rubrics of a course or a question.
Function: The system returns those rubrics.

**Business Rules:** BR-53

### 3.3.13 Create Rubric

**Function Description:** The Lecturer or Department Head creates a rubric whose criterion scores sum to 10.0.

**Function Trigger:**

Create Rubric:
Trigger: A Lecturer or Department Head saves a new rubric.
Function: The system stores it when the criterion scores sum to 10.0. Otherwise the rubric is rejected.

**Business Rules:** BR-53

### 3.3.14 Update Rubric

**Function Description:** The Lecturer or Department Head updates a rubric. The criterion scores must still sum to 10.0.

**Function Trigger:**

Update Rubric:
Trigger: A Lecturer or Department Head saves changes to a rubric.
Function: The system stores it when the criterion scores still sum to 10.0. Otherwise the change is rejected.

**Business Rules:** BR-53

### 3.3.15 Validate Rubric Total

**Function Description:** The Lecturer or Department Head checks that the rubric scores sum to 10.0 before the question can be saved.

**Function Trigger:**

Validate Rubric Total:
Trigger: A rubric is saved.
Function: The system checks that the criterion scores sum to 10.0 and rejects the rubric when they do not.

**Business Rules:** BR-53

### 3.3.16 Set Question Difficulty

**Function Description:** The Lecturer or Department Head sets the question difficulty to easy, medium, or hard.

**Function Trigger:**

Set Question Difficulty:
Trigger: A Lecturer or Department Head sets a question to easy, medium, or hard.
Function: The system stores that difficulty.

**Business Rules:** BR-55

### 3.3.17 Preview Question

**Function Description:** The Lecturer or Department Head shows the question as it will be read by a candidate, before it is submitted for review.

**Function Trigger:**

Preview Question:
Trigger: A Lecturer or Department Head opens the preview of a draft question.
Function: The system shows the question as a candidate will read it.

**Business Rules:** BR-52

### 3.3.18 Test Rubric

**Function Description:** The Lecturer or Department Head runs the rubric against a trial answer so the author can see the criterion breakdown before submitting the question.

**Function Trigger:**

Test Rubric:
Trigger: A Lecturer or Department Head runs a rubric against a trial answer.
Function: The system returns the trial score by criterion.

**Business Rules:** BR-53

### 3.3.19 Submit Question

**Function Description:** Submits a draft question to the Department Head. The question status becomes submitted for review. A Lecturer cannot approve the question.

**Function Trigger:**

Submit Question:
Trigger: A Lecturer submits a draft question for review.
Function: The system marks the question as submitted for review.

**Business Rules:** BR-54, BR-56

### 3.3.20 Approve Question

**Function Description:** The Department Head approves a submitted question. The question enters the bank selected for it.

**Function Trigger:**

Approve Question:
Trigger: The Department Head approves a submitted question.
Function: The system marks the question approved and places it in the selected bank.

**Business Rules:** BR-56

### 3.3.21 Request Question Revision

**Function Description:** The Department Head returns a submitted question for revision and stores the comment. The author can update it and submit it again.

**Function Trigger:**

Request Question Revision:
Trigger: The Department Head returns a submitted question with a revision comment.
Function: The system marks the question as needs revision and stores the comment.

**Business Rules:** BR-56

### 3.3.22 Reject Question

**Function Description:** The Department Head rejects a submitted question. The question is not added to the bank.

**Function Trigger:**

Reject Question:
Trigger: The Department Head rejects a submitted question.
Function: The system marks the question rejected. It is not added to the bank.

**Business Rules:** BR-56

### 3.3.23 Add Question To Practice Bank

**Function Description:** The Lecturer or Department Head marks the question for the practice bank. Mock examinations draw from this bank after the question is approved.

**Function Trigger:**

Add Question To Practice Bank:
Trigger: A Lecturer or Department Head marks a question for the practice bank.
Function: The system stores that bank choice.

**Business Rules:** BR-50, BR-57

### 3.3.24 Add Question To Examination Bank

**Function Description:** The Lecturer or Department Head marks the question for the official examination bank. This bank is separate from the practice bank.

**Function Trigger:**

Add Question To Examination Bank:
Trigger: A Lecturer or Department Head marks a question for the examination bank.
Function: The system stores that bank choice.

**Business Rules:** BR-50, BR-57

### Examination management

### 3.3.25 Create Examination Season

**Function Description:** The Department Head creates an official examination season.

**Function Trigger:**

Create Examination Season:
Trigger: The Department Head saves a new examination season.
Function: The system stores the season.

**Business Rules:** BR-58

### 3.3.26 Create Examination Shift

**Function Description:** Creates a shift under an examination season. The shift has a lab room, a proctor assignment, and the candidate list taken from a class.

**Function Trigger:**

Create Examination Shift:
Trigger: The Department Head saves a shift with a lab room, a proctor, and a class.
Function: The system stores the shift.

**Business Rules:** BR-58

### 3.3.27 Assign Course

**Function Description:** The Department Head assigns the course of an official examination season.

**Function Trigger:**

Assign Course:
Trigger: The Department Head selects the course of an examination season.
Function: The system links the season to that course.

**Business Rules:** BR-58

### 3.3.28 Assign Class

**Function Description:** The Department Head assigns a class to a shift. The candidates of that shift are taken from the class enrollment.

**Function Trigger:**

Assign Class:
Trigger: The Department Head selects the class of a shift.
Function: The system links the shift to that class and takes the candidates from its enrollment.

**Business Rules:** BR-58

### 3.3.29 Import Class Excel

**Function Description:** The Lecturer or Proctor imports a class roster from an Excel file when the roster is not taken only from the stored class enrollment. This is the roster import for an examination shift. It is not the grade export.

**Function Trigger:**

Import Class Excel:
Trigger: A Lecturer or Proctor submits an Excel roster for a shift.
Function: The system imports the candidate rows of that file onto the shift.

**Business Rules:** BR-59

### 3.3.30 Configure Follow-Up Count

**Function Description:** Sets the official-examination follow-up count for the season. The allowed range is 1 to 5. The default is 2. The Department Head also sets whether that examination uses follow-up.

**Function Trigger:**

Configure Follow-Up Count:
Trigger: The Department Head saves the official-examination follow-up count.
Function: The system stores a count from 1 to 5. The default is 2.

**Business Rules:** BR-60

### 3.3.31 Configure Examination Duration

**Function Description:** The Department Head sets the duration of the official examination.

**Function Trigger:**

Configure Examination Duration:
Trigger: The Department Head saves the official-examination duration.
Function: The system stores that duration.

**Business Rules:** BR-58

### 3.3.32 Set Examination Room

**Function Description:** The Department Head sets the lab room on the shift.

**Function Trigger:**

Set Examination Room:
Trigger: The Department Head selects the lab room of a shift.
Function: The system stores that room on the shift.

**Business Rules:** BR-58

### 3.3.33 Assign Proctor

**Function Description:** Assigns the person who proctors the shift. That person is a Lecturer or a Proctor.

**Function Trigger:**

Assign Proctor:
Trigger: The Department Head selects the person who proctors a shift.
Function: The system stores a Lecturer or a Proctor on that shift.

**Business Rules:** BR-61

### 3.3.34 Configure Examination Input Mode

**Function Description:** The Department Head sets the official examination input mode to voice only, or voice with transcript edit.

**Function Trigger:**

Configure Examination Input Mode:
Trigger: The Department Head selects voice only, or voice with transcript edit.
Function: The system stores that input mode.

**Business Rules:** BR-62

### 3.3.35 Configure Examination Transcript Buffer

**Function Description:** Sets the transcript buffer of the official examination, from 10 to 300 seconds. This does not change the practice buffer configured by Admin.

**Function Trigger:**

Configure Examination Transcript Buffer:
Trigger: The Department Head saves the official-examination transcript buffer.
Function: The system stores a value from 10 to 300 seconds. The practice buffer is unchanged.

**Business Rules:** BR-63

### Laboratory examination, Proctor functions

### 3.3.36 Record Identity Check

**Function Description:** Records that the candidate's student card or citizen identity was checked before the attempt.

**Function Trigger:**

Record Identity Check:
Trigger: The Proctor records the identity check of a candidate.
Function: The system stores the check for that candidate.

**Business Rules:** BR-64

### 3.3.37 Assign Examination Machine

**Function Description:** The Proctor assigns the workstation. The seat number is from 1 to 40 and matches the candidate order of the shift. The workstation address is stored as `ip_address`.

**Function Trigger:**

Assign Examination Machine:
Trigger: The Proctor assigns a seat and a workstation to a candidate.
Function: The system stores the seat, from 1 to 40, and the workstation address as ip_address.

**Business Rules:** BR-65

### 3.3.38 Start Examination Shift

**Function Description:** The Proctor opens the shift so checked-in candidates can start on the assigned workstations.

**Function Trigger:**

Start Examination Shift:
Trigger: The Proctor starts the shift.
Function: The system opens the shift.

**Business Rules:** BR-64

### 3.3.39 Check In Candidate

**Function Description:** The Proctor checks the candidate in to the shift after the identity check and the seat assignment.

**Function Trigger:**

Check In Candidate:
Trigger: The Proctor checks a candidate in after the identity check and the seat assignment.
Function: The system stores the candidate as checked in.

**Business Rules:** BR-64, BR-65

### 3.3.40 End Examination Shift

**Function Description:** The Proctor closes the shift after the candidates have submitted and signed the paper record.

**Function Trigger:**

End Examination Shift:
Trigger: The Proctor ends the shift after submissions and the paper sign-out.
Function: The system closes the shift.

**Business Rules:** BR-66

### Grade and appeal

### 3.3.41 Grade Examination Answer

**Function Description:** After a laboratory answer is stored, the system grades the transcript in the background. The kiosk does not show this score. A failed grading call is retried. If it still fails, the job is kept and retried after five minutes. The stored submission is not deleted. The Lecturer uses the result.

**Function Trigger:**

Grade Examination Answer:
Trigger: A laboratory answer has been stored.
Function: The system grades the transcript in the background. The kiosk does not show the score. A job that still fails is kept and retried after five minutes. The stored submission is not deleted.

**Business Rules:** BR-67, BR-68

### 3.3.42 Get Submission Confidence

**Function Description:** The Lecturer returns the confidence of a graded submission. A submission is in the review group when it is marked suspicious, when the confidence is below 0.70, or when the result is a fail. Other graded submissions are in the high-confidence group.

**Function Trigger:**

Get Submission Confidence:
Trigger: The Lecturer opens the confidence of a graded submission.
Function: The system returns the confidence used to place the submission in the suspicious group or the high-confidence group.

**Business Rules:** BR-69

### 3.3.43 Get Evidence

**Function Description:** The Lecturer returns the evidence of a submission: the audio location, the original transcript, and the grading rationale.

**Function Trigger:**

Get Evidence:
Trigger: The Lecturer opens the evidence of a submission.
Function: The system returns the audio location, the original transcript, and the grading rationale.

**Business Rules:** BR-70

### 3.3.44 Play Examination Audio

**Function Description:** The Lecturer plays the stored laboratory audio. Playback speed is from 1.0x to 1.5x.

**Function Trigger:**

Play Examination Audio:
Trigger: The Lecturer starts playback of the stored laboratory audio.
Function: The system plays that audio at a speed from 1.0x to 1.5x.

**Business Rules:** BR-70

### 3.3.45 Seek Examination Audio

**Function Description:** The Lecturer moves playback to a position in the stored audio.

**Function Trigger:**

Seek Examination Audio:
Trigger: The Lecturer chooses a position in the stored audio.
Function: The system continues playback from that position.

**Business Rules:** BR-70

### 3.3.46 Jump To Spoken Timestamp

**Function Description:** The Lecturer jumps playback to the timestamp of a spoken part of the answer.

**Function Trigger:**

Jump To Spoken Timestamp:
Trigger: The Lecturer chooses a spoken timestamp.
Function: The system moves playback to that timestamp.

**Business Rules:** BR-70

### 3.3.47 Verify Grade

**Function Description:** The lecturer verifies the background grade. A high-confidence submission can be verified from the evidence. A suspicious, low-confidence, or failed submission is verified with the audio and the rationale.

**Function Trigger:**

Verify Grade:
Trigger: The Lecturer confirms the background grade after reviewing the evidence.
Function: The system stores the grade as verified.

**Business Rules:** BR-69

### 3.3.48 Adjust Grade With Reason

**Function Description:** The lecturer changes the grade. A reason is required. The change is written to the audit log.

**Function Trigger:**

Adjust Grade With Reason:
Trigger: The Lecturer submits a new grade and a reason.
Function: The system stores the adjusted grade and the reason, and writes the change to the audit log.

**Business Rules:** BR-71

### 3.3.49 Publish Grade

**Function Description:** The Lecturer publishes the grades of a shift once. Publish is allowed only when every candidate in the shift has a grade. Publishing locks those grades.

**Function Trigger:**

Publish Grade:
Trigger: The Lecturer publishes a shift in which every candidate has a grade.
Function: The system publishes the grades once and locks them. If any candidate has no grade, publish is rejected.

**Business Rules:** BR-72, BR-73

### 3.3.50 Lock Grade

**Function Description:** A published grade is locked. A later update or delete of a locked grade is rejected. The lock opens only for the Lecturer assigned to regrade an appeal of that submission.

**Function Trigger:**

Lock Grade:
Trigger: A grade has just been published.
Function: The system locks the grade. A later update or delete is rejected, except for the Lecturer assigned to regrade an appeal of that submission.

**Business Rules:** BR-73

### 3.3.51 Assign Appeal

**Function Description:** Assigns one Lecturer to regrade an appeal. The appeal was created by the student on the student portal.

**Function Trigger:**

Assign Appeal:
Trigger: The Department Head selects one Lecturer for an appeal.
Function: The system stores that assignment.

**Business Rules:** BR-74

### 3.3.52 Regrade Appeal

**Function Description:** The assigned Lecturer sets the new grade. This is the path that may change a locked grade. A Lecturer who was not assigned cannot change it.

**Function Trigger:**

Regrade Appeal:
Trigger: The Lecturer assigned to an appeal submits the new grade.
Function: The system updates the grade. A Lecturer who was not assigned cannot change the locked grade.

**Business Rules:** BR-73, BR-74

### 3.3.53 Export Grade To Excel

**Function Description:** The Lecturer exports the shift grade report as an Excel file in the FPT examination-office layout.

**Function Trigger:**

Export Grade To Excel:
Trigger: The Lecturer exports a shift grade report to Excel.
Function: The system returns an .xlsx file in the FPT examination-office layout.

**Business Rules:** BR-75

### 3.3.54 Export Grade To PDF

**Function Description:** The Lecturer exports the shift grade report as a PDF with a digital signature.

**Function Trigger:**

Export Grade To PDF:
Trigger: The Lecturer exports a shift grade report to PDF.
Function: The system returns a PDF with a digital signature.

**Business Rules:** BR-75

### 3.3.55 Get Appeals

**Function Description:** Department Head sees the appeals to assign. The assigned Lecturer sees the appeals assigned to that Lecturer.

**Function Trigger:**

Get Appeals:
Trigger: The Department Head or an assigned Lecturer opens the appeal list.
Function: The system returns the appeals that actor is allowed to see.

**Business Rules:** BR-74

### Courses, classes, and enrollments

### 3.3.56 Create Course

**Function Description:** The Lecturer creates a course with a code, a name, and a credit count greater than 0.

**Function Trigger:**

Create Course:
Trigger: The Lecturer submits a course code, name, and a credit count greater than 0.
Function: The system creates the course.

**Business Rules:** BR-76

### 3.3.57 Update Course

**Function Description:** The Lecturer updates a course. The credit count remains greater than 0.

**Function Trigger:**

Update Course:
Trigger: The Lecturer submits changes to a course.
Function: The system updates the course. The credit count must stay greater than 0.

**Business Rules:** BR-76

### 3.3.58 Get Classes

**Function Description:** The Lecturer returns the classes.

**Function Trigger:**

Get Classes:
Trigger: The Lecturer opens the class list.
Function: The system returns the classes.

**Business Rules:** BR-77

### 3.3.59 Create Class

**Function Description:** Creates a class for a course and a semester. The class has a lecturer assignment.

**Function Trigger:**

Create Class:
Trigger: The Lecturer submits a class code, course, semester, and lecturer.
Function: The system creates the class.

**Business Rules:** BR-77

### 3.3.60 Update Class

**Function Description:** The Lecturer updates a class.

**Function Trigger:**

Update Class:
Trigger: The Lecturer submits changes to a class.
Function: The system updates the class.

**Business Rules:** BR-77

### 3.3.61 Get Class Enrollments

**Function Description:** Returns the students enrolled in a class.

**Function Trigger:**

Get Class Enrollments:
Trigger: The Lecturer opens the enrollment of a class.
Function: The system returns the students in that class.

**Business Rules:** BR-78

### 3.3.62 Create Class Enrollment

**Function Description:** Enrolls a student in a class.

**Function Trigger:**

Create Class Enrollment:
Trigger: The Lecturer enrolls a student in a class.
Function: The system stores the enrollment.

**Business Rules:** BR-78

### 3.3.63 Update Class Enrollment

**Function Description:** The Lecturer updates an enrollment in a class.

**Function Trigger:**

Update Class Enrollment:
Trigger: The Lecturer submits changes to an enrollment.
Function: The system updates the enrollment.

**Business Rules:** BR-78

### Dashboards

### 3.3.64 Get Lecturer Dashboard

**Function Description:** Returns the lecturer dashboard. The web client shows this role as Instructor.

**Function Trigger:**

Get Lecturer Dashboard:
Trigger: The Lecturer opens the lecturer home.
Function: The system returns that lecturer's dashboard.

**Business Rules:** BR-79

### 3.3.65 Get Department Head Dashboard

**Function Description:** The Department Head returns the department-head dashboard.

**Function Trigger:**

Get Department Head Dashboard:
Trigger: The Department Head opens the department-head home.
Function: The system returns that dashboard.

**Business Rules:** BR-79

### Business rules for 3.3

| ID | Rule |
| --- | --- |
| BR-46 | The Department Head configures the mock paper, the Bloom distribution, and the mock duration. The student does not configure them. |
| BR-47 | Mock follow-up is configured by the Department Head. An attempt with follow-up runs longer than an attempt without follow-up. |
| BR-48 | The Department Head sets the daily mock limit per course. Admin does not set that limit. |
| BR-49 | Question generation reads the FLM syllabus and does not write back to FLM. |
| BR-50 | A generated or manual question can be marked for the practice bank, the examination bank, or both. The sample answer has at least 50 characters. |
| BR-51 | A question is classified by course, Bloom level, and CLO. There is no chapter. |
| BR-52 | Lecturer and Department Head can open the questions in their question-bank scope. |
| BR-53 | The scores of a rubric sum to 10.0. A rubric that does not sum to 10.0 is rejected. |
| BR-54 | A new question is a draft. A Lecturer must submit it for review. |
| BR-55 | Question difficulty is easy, medium, or hard. |
| BR-56 | The Department Head approves a question, returns it for revision with a comment, or rejects it. Only an approved question enters the selected bank. |
| BR-57 | The practice bank and the examination bank stay separate. A mock examination uses the practice bank. |
| BR-58 | The Department Head creates the examination season, assigns the course, creates the shift, assigns the class, sets the room, and sets the duration. Candidates of a shift come from the assigned class. |
| BR-59 | A Lecturer or a Proctor may import a shift roster from Excel when the roster is supplied as a file. |
| BR-60 | Official-examination follow-up is configured by the Department Head, from 1 to 5 questions. The default is 2. |
| BR-61 | The person assigned to proctor a shift is a Lecturer or a Proctor. |
| BR-62 | The official examination input mode is voice only, or voice with transcript edit. |
| BR-63 | The official-examination transcript buffer is from 10 to 300 seconds and is separate from the practice buffer. |
| BR-64 | A Proctor records the identity check, checks the candidate in, and starts the shift. |
| BR-65 | The seat number is from 1 to 40 and matches the candidate order. The workstation is stored as `ip_address`. |
| BR-66 | The Proctor ends the shift after submission and the paper sign-out. |
| BR-67 | Laboratory grading runs in the background. The kiosk does not show the score. |
| BR-68 | A grading job that still fails is kept and retried after five minutes. The stored submission is not deleted. |
| BR-69 | A submission is reviewed in the suspicious group when it is marked suspicious, when confidence is below 0.70, or when the result is a fail. The other submissions are the high-confidence group. |
| BR-70 | Evidence is the stored audio, the original transcript, and the grading rationale. Playback speed is from 1.0x to 1.5x. |
| BR-71 | A Lecturer who changes a grade must store a reason. |
| BR-72 | Publish runs once for a shift, and only when every candidate in that shift has a grade. |
| BR-73 | A published grade is locked. An update or delete of a locked grade is rejected, except for the Lecturer assigned to regrade that appeal. |
| BR-74 | The Department Head assigns one Lecturer to an appeal. That Lecturer regrades it. |
| BR-75 | The Lecturer exports the shift grade report to Excel and to a signed PDF. |
| BR-76 | A Lecturer creates and updates a course. Credits are greater than 0. |
| BR-77 | A Lecturer creates and updates a class under a course and a semester. |
| BR-78 | A Lecturer maintains the student list of a class. |
| BR-79 | Lecturer and Department Head each have a separate dashboard. |
