# REPORT 4 — SOFTWARE DESIGN DOCUMENT (DETAILED DESIGN)
## FEATURE PACKAGE DD-04: PRACTICE VOICE/TEXT CAPTURE, STT & TRANSCRIPT BUFFER (MF-01)

> **Document Information:**
> - **Project:** LLM-Based Oral Examination System for Software Engineering (`FA26SE166`)
> - **Lead Architect:** Nguyen Quang Thanh | **Cross-Reviewer:** Pham Nguyen Dang Hai
> - **Scope:** `DD-04`: Audio Capture, Whisper STT Streaming & Transcript Buffer (`MF-01`)
> - **Diagram Files (Draw.io Vector):**
>   - `DD-04 Sequence Diagrams`: [`DD04_01_Audio_Capture_STT_Streaming_Sequence.drawio`](./DD04_01_Audio_Capture_STT_Streaming_Sequence.drawio) | [`DD04_02_Transcript_Buffer_CodeSwitching_Sequence.drawio`](./DD04_02_Transcript_Buffer_CodeSwitching_Sequence.drawio) | [`DD04_VoiceSttBuffer_Sequence_Diagram.drawio`](./DD04_VoiceSttBuffer_Sequence_Diagram.drawio)
>   - `DD-04 Class Diagram`: [`DD04_VoiceSttBuffer_Class_Diagram.drawio`](./DD04_VoiceSttBuffer_Class_Diagram.drawio)

---

## 3.4. Detailed Design Package `DD-04`: Audio Capture, Whisper STT & Transcript Buffer (`MF-01`)

### 3.4.1. Feature Overview & Architecture (`DD-04 Overview`)

Feature package `DD-04` handles speech processing and student transcript review during practice sessions:

1. **In-Memory Audio Processing & Zero-R2 Invariant:**
   - Audio is captured in browser RAM (`MediaRecorder API`). Audio buffers are streamed via `POST /api/v1/speech/transcribe` directly to **Cloudflare Whisper STT**.
   - **Zero-R2 Invariant:** Audio recordings from practice sessions are **never stored on Cloudflare R2** to eliminate storage overhead; only normalized text transcripts are retained.

2. **Transcript Countdown Buffer (10–300s):**
   - Following STT conversion, the client enters a countdown review buffer (`TranscriptBufferSeconds`, default 60s). Students can review transcripts, listen to TTS question readouts, and correct misrecognized software engineering terminology (Vietnamese-English Code-Switching, e.g., "bắt chước" $->$ "Mock", "áp dụng" $->$ "Adapter Pattern").

---

### 3.4.2. Sequence Diagrams (`DD-04 Sequence Diagrams`)

1. **Audio Capture & STT Streaming (`DD04_01`):**
   - **Vector Source File:** [`DD04_01_Audio_Capture_STT_Streaming_Sequence.drawio`](./DD04_01_Audio_Capture_STT_Streaming_Sequence.drawio)
   - Streams audio chunks from browser RAM to Cloudflare Whisper STT with Gemini audio fallback.

2. **Transcript Buffer & Code-Switching (`DD04_02`):**
   - **Vector Source File:** [`DD04_02_Transcript_Buffer_CodeSwitching_Sequence.drawio`](./DD04_02_Transcript_Buffer_CodeSwitching_Sequence.drawio)
   - Executes the 60s countdown timer allowing manual edits of specialized technical terms.

- **Consolidated Overview Diagram:** [`DD04_VoiceSttBuffer_Sequence_Diagram.drawio`](./DD04_VoiceSttBuffer_Sequence_Diagram.drawio)

---

### 3.4.3. Step-by-Step Interaction Narrative (`Table 3.4.1`)

| Step | Sender | Receiver | Message / Operation | Detailed Description & Technical Constraints |
|:---:|:---|:---|:---|:---|
| **01** | `: Student` | `: Practice UI` | Click "Start Speaking" | Initializes browser microphone via `navigator.mediaDevices`. |
| **02** | `: Practice UI` | `: Audio Visualizer` | Render Waveform | Displays audio waveform to ensure audio input exceeds 60 dB. |
| **03** | `: Student` | `: Practice UI` | Click "Finish Speaking" | Stops `MediaRecorder` and bundles audio into WebM blob. |
| **04** | `: Practice UI` | `: SpeechController` | `POST /api/v1/speech/transcribe` | Transmits raw audio bytes via multipart form data. |
| **05** | `: SpeechController`| `: IMediator` | `Send(TranscribeAudioCommand)` | Forwards command to MediatR pipeline. |
| **06** | `: TranscribeHandler`| `: WhisperSttService` | `TranscribeAsync(audioStream)` | Invokes Cloudflare Whisper STT model via REST API. |
| **07** | `: WhisperSttService`| `: GeminiClient` | Fallback on Failure | If Whisper fails, automatically fails over to Gemini Flash STT. |
| **08** | `: TranscribeHandler`| `: CodeSwitchingNormalizer` | Normalize Technical Terms | Replaces common Vietnamese phonetics with SE terminology. |
| **09** | `: SpeechController`| `: Practice UI` | Return `TranscriptDto` | Returns transcribed text to frontend client. |
| **10** | `: Practice UI` | `: Countdown Timer` | Start 60s Countdown | Begins `TranscriptBufferSeconds` countdown timer. |
| **11** | `: Student` | `: Practice UI` | Edit Terms / Listen TTS | Plays TTS audio readout and corrects technical terms. |
| **12** | `: Practice UI` | `: Countdown Timer` | Timer Reaches 0s | Automatically locks editor and prepares payload for submission. |

---

### 3.4.4. Class Diagram (`DD-04 Class Diagram`)

> **Draw.io Vector Source:** [`DD04_VoiceSttBuffer_Class_Diagram.drawio`](./DD04_VoiceSttBuffer_Class_Diagram.drawio)

*Figure 3.4.1 — Voice Capture, Speech-to-Text & Transcript Buffer Class Diagram (`DD-04`).*

---

### 3.4.5. Class Specifications (`DD-04 Class Specifications`)

#### A. Architectural Class Summary Table

| Class Name | Architectural Layer | Package | Single Responsibility |
|:---|:---|:---|:---|
| **`SpeechController`** | API | `Controllers/v1` | Endpoints for speech transcription and TTS synthesis. |
| **`TranscribeAudioCommandHandler`** | Application | `Features/Speech` | Coordinates STT transcription, fallback logic, and code-switching normalization. |
| **`CloudflareWhisperSttService`** | Infrastructure | `Services` | Client wrapper for Cloudflare Workers AI Whisper large-v3. |
| **`CodeSwitchingNormalizer`** | Domain | `Services` | Normalizes spoken Vietnamese software engineering terminology. |

#### B. Method-Level Class Specifications

| Class | Method / Signature | Parameters | Return Type | Behavior Specification & Business Rules |
|:---|:---|:---|:---|:---|
| **`SpeechController`** | `Transcribe(file)` | `IFormFile` | `Task<IActionResult>` | Maps `POST /api/v1/speech/transcribe`. Validates audio/webm format $\le 15	ext{MB}$. |
| **`TranscribeAudioCommandHandler`** | `Handle(cmd, ct)` | `TranscribeAudioCommand`, `CancellationToken` | `Task<TranscriptResponse>` | Invokes Whisper with Polly retry; executes Gemini fallback if needed. |
| **`CloudflareWhisperSttService`** | `TranscribeAsync(stream, ct)` | `Stream`, `CancellationToken` | `Task<string>` | Sends binary audio stream to Whisper model endpoint. |
| **`CodeSwitchingNormalizer`** | `Normalize(text)` | `string` | `string` | Regex-based dictionary mapping technical terms. |

---

### 3.4.6. Exception Handling & Edge Case Matrix (`Table 3.4.2`)

| Scenario ID | Edge Case / Exception Name | Trigger Condition | System Detection & Handling Mechanism | HTTP Status & Response Payload |
|:---:|:---|:---|:---|:---|
| **SC-01** | Empty Audio Stream | Client submits zero-byte or silent recording. | Handler checks stream length. Throws `ValidationException` if empty. | `HTTP 400 Bad Request`<br>`{"error": "Audio stream is empty or silent"}` |
| **SC-02** | Unsupported Audio Codec | Audio file format is not WebM/Opus. | Multipart validator inspects file header magic bytes. | `HTTP 415 Unsupported Media Type`<br>`{"error": "Only audio/webm format supported"}` |
| **SC-03** | Cloudflare Whisper Outage | Cloudflare Whisper returns HTTP 500 or times out. | Polly policy catches timeout and triggers Gemini Flash STT fallback. | `HTTP 200 OK`<br>`(Returned from fallback STT service)` |
| **SC-04** | Buffer Countdown Expiration | Student fails to click submit before timer expires. | Client automatically invokes submission pipeline when timer hits 0s. | Client Auto-Action |
| **SC-05** | Oversized Audio Payload | Audio file exceeds 15 MB boundary limit. | Middleware checks `Content-Length` header. | `HTTP 413 Payload Too Large`<br>`{"error": "Audio recording exceeds 15MB limit"}` |

---

### 3.4.7. Data Contracts & DTO Specifications (`Table 3.4.3`)

| Field Name | Data Type | Required | Validation Rules | Business Meaning & Concrete Example |
|:---|:---|:---:|:---|:---|
| **`audioFile`** | Binary FormFile | Yes | Format `audio/webm`, $\le 15	ext{MB}$ | Raw audio recording captured in browser RAM. |
| **`transcript`** | String | Output | Non-empty text | Speech-to-text converted answer transcript. |
| **`confidence`** | Decimal | Output | 0.00 – 1.00 | STT acoustic confidence score. |
| **`bufferSeconds`** | Integer | Output | 10–300s | Review buffer duration configured for the session. |
