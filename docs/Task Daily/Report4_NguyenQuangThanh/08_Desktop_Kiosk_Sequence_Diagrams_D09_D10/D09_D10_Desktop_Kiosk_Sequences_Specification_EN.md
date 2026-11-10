# REPORT 4 — SOFTWARE DESIGN DOCUMENT (DETAILED DESIGN)
## DESKTOP KIOSK SEQUENCE DIAGRAMS `D-09` & `D-10` (`[TARGET DESIGN / PROPOSED]` — MF-04)

> **Document Information:**
> - **Project:** LLM-Based Oral Examination System for Software Engineering (`FA26SE166`)
> - **Lead Architect:** Nguyen Quang Thanh | **Cross-Reviewer:** Pham Nguyen Dang Hai
> - **Scope:** `D-09` & `D-10`: Desktop Kiosk Startup, Hardware Check, Station Binding & Submission (`MF-04`)
> - **Diagram Files (Draw.io Vector):**
>   - `D-09 Sequence Diagrams`: [`D09_01_Kiosk_Startup_Mic_Calibration_Sequence.drawio`](./D09_01_Kiosk_Startup_Mic_Calibration_Sequence.drawio) | [`D09_02_Kiosk_IP_Binding_CheckIn_Sequence.drawio`](./D09_02_Kiosk_IP_Binding_CheckIn_Sequence.drawio) | [`D09_Kiosk_Startup_PreCheck_CheckIn_Sequence.drawio`](./D09_Kiosk_Startup_PreCheck_CheckIn_Sequence.drawio)
>   - `D-10 Sequence Diagrams`: [`D10_01_Kiosk_Lockdown_Sealed_Submission_Sequence.drawio`](./D10_01_Kiosk_Lockdown_Sealed_Submission_Sequence.drawio) | [`D10_Kiosk_Exam_Execution_Persist_First_Sequence.drawio`](./D10_Kiosk_Exam_Execution_Persist_First_Sequence.drawio)

---

## 2.9. Sequence Diagram `D-09`: Kiosk Startup, Hardware Calibration & Station Binding (`[Target Design / Proposed]`)

### 2.9.1. Feature Overview & Architecture (`D-09 Overview`)

Sequence `D-09` governs the pre-flight verification and candidate check-in process for lab exam stations:

1. **Station Hardware & Microphone Calibration (30s $\ge 60	ext{ dB}$):**
   - The Kiosk application boots into fullscreen lockdown, disabling window management shortcuts (Alt+Tab, Win, Ctrl+Esc).
   - Candidate performs a 30-second microphone test. The system analyzes raw audio RMS levels, ensuring average volume reaches at least $60	ext{ dB}$ before unlocking the check-in screen.

2. **Station Binding (`ip_address`) & Ticket Check-In:**
   - Kiosk resolves its local IP address (`ip_address`) and submits it alongside the student ID to `POST /api/v1/official-exams/shifts/{shiftId}/check-in`.
   - Backend matches station IP against scheduled lab room station maps (`SeatNumber` 1..40), transitions ticket status to `CHECKED_IN`, and verifies that the shift has been published by an Examination Officer (`is_exam_published = true`).

---

### 2.9.2. Sequence Diagrams (`D-09 Sequence Diagrams`)

1. **Startup & Mic Calibration (`D09_01`):**
   - **Vector Source File:** [`D09_01_Kiosk_Startup_Mic_Calibration_Sequence.drawio`](./D09_01_Kiosk_Startup_Mic_Calibration_Sequence.drawio)
   - Performs hardware checks, fullscreen lock, and audio volume calibration.

2. **IP Binding & Check-In (`D09_02`):**
   - **Vector Source File:** [`D09_02_Kiosk_IP_Binding_CheckIn_Sequence.drawio`](./D09_02_Kiosk_IP_Binding_CheckIn_Sequence.drawio)
   - Verifies station `ip_address` binding against lab seat number.

- **Consolidated Overview Diagram:** [`D09_Kiosk_Startup_PreCheck_CheckIn_Sequence.drawio`](./D09_Kiosk_Startup_PreCheck_CheckIn_Sequence.drawio)

---

## 2.10. Sequence Diagram `D-10`: Exam Execution, Audio Sealing & Submission (`[Target Design / Proposed]`)

### 2.10.1. Feature Overview & Architecture (`D-10 Overview`)

Sequence `D-10` governs exam execution, tamper-proof audio hashing, and submission:

1. **Immutable Audio Recording (`STT_MSSV.webm`) & SHA-256 Hash:**
   - During the exam, candidate voice responses are recorded into an immutable file named according to the seat and student code convention: `STT_MSSV.webm` (e.g., `01_SE170123.webm`).
   - Before upload, the Kiosk computes a cryptographic **SHA-256 hash** over the audio bytes to ensure chain of custody.

2. **Cloudflare R2 Direct Upload & Persist-First Submission (< 100ms):**
   - Kiosk uploads the audio recording directly to **Cloudflare R2** via an authorized presigned URL.
   - Once uploaded, Kiosk calls `POST /api/v1/official-exams/submissions/persist-first`. The backend saves the submission record with status `SUBMITTED` and the audio SHA-256 hash in $< 100	ext{ms}$, enqueuing tasks into the background grading queue.
   - **Strict Proctor Boundary (Law 14):** Proctors only take attendance and report security violations via SignalR (`/hubs/anti-cheat`). Proctors have zero permissions to view scores, publish exams, or modify grades.

---

### 2.10.2. Sequence Diagrams (`D-10 Sequence Diagrams`)

1. **Lockdown & Sealed Submission (`D10_01`):**
   - **Vector Source File:** [`D10_01_Kiosk_Lockdown_Sealed_Submission_Sequence.drawio`](./D10_01_Kiosk_Lockdown_Sealed_Submission_Sequence.drawio)
   - Handles audio hashing, R2 upload, and submission persistence.

- **Consolidated Overview Diagram:** [`D10_Kiosk_Exam_Execution_Persist_First_Sequence.drawio`](./D10_Kiosk_Exam_Execution_Persist_First_Sequence.drawio)
