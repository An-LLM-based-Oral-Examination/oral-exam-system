# An LLM-based Oral Examination System (FA26SE166)

An Enterprise AI-driven Examination and Assessment platform designed to conduct interactive, rubric-aligned oral examinations for Software Engineering students using Large Language Models (LLMs).

---

## 📖 Overview

The **LLM-based Oral Examination System** automates and standardizes oral assessments (viva voce) through real-time voice interaction, rubric-driven evaluation, and automated feedback loops. The system addresses cognitive overload for examiners, provides structured practice and mock examination sessions for students, and maintains full forensic integrity for final examinations.

### Core Modules & Workflows
- **MF-01 (Interactive Practice):** Voice-driven practice sessions with instant rubric-based scoring, dynamic code-switching correction buffer configured by Admin (`transcript_buffer_seconds`, 10–300s, default 60s), follow-up questions configured by Admin (1–5 questions, default 2, active only for `[Per-Question]` mode when score is between 4.0 and 8.0; `[Full-Session]` has no follow-up), student-selectable question count for both modes with progressive Easy-to-Hard (3–10 questions) option via `system_configs`, audio is not persisted (transcript only), SignalR `PracticeHub`, and Bounded Channel 1000 slots RAM.
- **MF-02 (Mock Examination):** Timed mock exams utilizing shared practice question pool (`practice_questions`), configurable daily quotas (`max_mock_exams_per_day`) and Bloom distribution configured by Department Head, student-selected follow-up option (context-driven follow-up by AI, exam duration extends when enabled), dual countdown timers with auto-submit guard (unanswered questions marked blank and ungraded), Voice-First Gate, no audio persistence (transcript only), and instant rubric scorecard feedback.
- **MF-03 (Question Bank, FLM Generator & Rubric Studio):** Bloom's taxonomy-aligned question bank management with 10.0-point rubric invariant validation ($\sum \equiv 10.0$), automated question generation from FPT FLM API using syllabus CLOs (accessible to Lecturers and Department Heads; lecturers can tick `practice_questions` and/or `exam_questions`, submit for review with `DRAFT` $\to$ `SUBMITTED_FOR_REVIEW`, reviewed & approved by Department Head as `APPROVED`, `NEEDS_REVISION`, or `REJECTED`), and standardized Model Answers ($\ge 50$ chars).
- **MF-04 (Lab Viva Exam, Lecturer Audit & Internal Appeals):** Secure kiosk-mode final examination (Department Head configures exam, course, shifts with lab room and assigned proctor/lecturer, input mode `VoiceOnly` or `VoiceWithTranscriptEdit`, follow-up 1–5 questions default 2), audio integrity hashing with Cloudflare R2 upload (`STT_MSSV.webm` + SHA-256 seal), persist-first $< 100$ms with `SUBMITTED` status, zero immediate score display and no on-site appeals, asynchronous transcript-only AI grading via BoundedChannel 1000 slots + DLQ 5m replay, Evidence Panel (AudioURL, Whisper transcript, AI CoT), atomic one-way grade publishing (`is_locked = true`), and internal student grade appeals (`AppealRequest`) adjudicated by Department Head assigning a designated lecturer for re-evaluation (One-Way Lock allows assigned lecturer grade update), with exam reporting in both Excel (.xlsx) and digitally signed PDF formats.

### 🛡️ User Roles & Role-Based Access Control (RBAC)
The platform enforces strict enterprise RBAC across 5 standardized user roles:
- `student`: Enrolled SE students (Interactive Practice MF-01, Timed Mock Exam MF-02, Official Lab Kiosk Exam MF-04, Internal Grade Appeals `AppealRequest`).
- `lecturer`: Course lecturers and viva examiners (Manual question authoring, automated question generation from FPT FLM API with custom rubrics submitted for review, AI Simulator rubric calibration, Lab proctoring, Lecturer Audit Portal with Evidence Panel & grade publishing).
- `department_head`: Academic Department Head (Course syllabus management, question bank review, approval/rejection of questions submitted by lecturers, automated question generation from FPT FLM API, shift and course exam configuration, and adjudication/assignment of internal student appeal requests `AppealRequest`).
- `proctor`: Lab exam proctors / invigilators (Shift check-in, booth assignment 1–40, synchronized exam broadcast).
- `admin`: System administrators (System configurations for MF-01, academic cohort setup, RBAC management, Dead-Letter Queue monitoring & replay).

---

## 🏛️ System Architecture

The project is structured as an enterprise monorepo with strict separation of concerns:

```
05_Source_Code/
├── backend/                  # .NET 8 Clean Architecture Solution
│   ├── OralExamination.sln   # Main Solution file
│   └── src/
│       ├── Domain/           # Enterprise entities, value objects, domain logic (zero external dependencies)
│       ├── Application/      # Use cases, CQRS commands/queries, MediatR handlers, DTOs
│       ├── Infrastructure/   # Database context, external integrations (PostgreSQL, LLM, Storage)
│       └── API/              # ASP.NET Core Web API presentation layer, middleware, endpoints
├── frontend/                 # React 19 + TypeScript + Vite Single Page Application (SPA)
│   ├── src/
│   │   ├── components/       # Reusable UI components
│   │   ├── features/         # Feature-sliced modules (Practice, Exam, Rubric, Audit)
│   │   └── App.tsx           # Application root component
│   ├── package.json          # Node dependencies and scripts
│   └── vite.config.ts        # Vite configuration
└── docs/                     # Technical documentation, Architecture Decision Records (ADRs), API specs
```

### Dependency Inversion in Backend
```
API  ───►  Application  ◄───  Infrastructure
                 │
                 ▼
               Domain (Pure Core)
```

---

## 🛠️ Prerequisites

Before getting started, ensure you have the following installed on your system:

| Technology | Minimum Version | Recommended Version | Purpose |
|------------|-----------------|---------------------|---------|
| **.NET SDK** | `8.0.x` | `8.0.319` or `10.0.x` (targeting net8.0) | Backend API & Application services |
| **Node.js** | `v20.x` LTS | `v24.x` | Frontend development and tooling |
| **npm** | `10.x` | `11.x` | Package manager for frontend |
| **Git** | `2.40+` | `2.52+` | Version control |

---

## 🚀 Getting Started

### 1. Clone & Navigate
```bash
git clone <repository-url>
cd 05_Source_Code
```

### 2. Backend Setup (.NET 8)
Navigate to the `backend/` directory, restore packages, and compile the solution:

```bash
cd backend
dotnet restore
dotnet build
```

To run the API server:
```bash
cd src/API
dotnet run
```
By default, the API will be available at `https://localhost:5001` or `http://localhost:5000`.

### 3. Frontend Setup (React + TypeScript)
Navigate to the `frontend/` directory, install dependencies, and launch the development server:

```bash
cd ../frontend
npm install
npm run dev
```
The frontend application will start at `http://localhost:5173`.

To verify a production build:
```bash
npm run build
```

---

## 📚 Technical Documentation

Complete technical documentation is maintained under the [`docs/`](docs/README.md) folder:
- [Developer Documentation Hub](docs/README.md)
- [Master System Architecture](docs/MASTER_ARCHITECTURE.md)
- [4 Core Main Flows Specification](docs/MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md)
- [Database ERD 30 Tables 3NF](docs/ERD_DATABASE_DESIGN.md)
- [API Contract & Integration Guide](docs/API_CONTRACT_AND_INTEGRATION_GUIDE.md)
- [Frontend Integration Guide for MF-01](docs/MF01_Frontend_Integration.md)
- [Frontend Mini Tester (HTML)](docs/MF01_Mini_Tester.html)
- [Git & Team Workflow](docs/GIT_AND_TEAM_WORKFLOW.md)
- [Master 4 Main Flows Activity Diagrams (.drawio)](docs/diagrams/CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio)
- [System Architecture Diagram (.drawio)](docs/diagrams/KIEN_TRUC_HE_THONG.drawio)

---

## 👥 Core Engineering Team (FA26SE166)

| Member | Role | Primary Responsibilities |
|---|---|---|
| 🧑 **Nguyễn Quang Thành** | Team Leader & Lead Backend Architect | Backend MF-01 (Interactive Practice, Progressive 3–10 Qs, BoundedChannel, SignalR `PracticeHub`, SystemConfigs) + Backend MF-02 (Timed Mock Exam, Quota Guard, Voice-First, Bốc đề `practice_questions`, Instant Scorecard) + Module Báo cáo Khảo thí FPT (Excel .xlsx + PDF) + System Integration |
| 🧑 **Nguyễn Trọng Tốt** | Backend Developer, AI Specialist & QA Lead | Auth Google OAuth PKCE + Backend MF-03 (AI sinh đề FLM theo CLO, Barem 10.0đ, Tick 2 kho, Gửi & Duyệt đề) + TOÀN BỘ Backend MF-04 (Kiosk Check-in IP Binding `ip_address`, Nộp bài Persist First < 100ms, Audio R2 `STT_MSSV.webm` + SHA-256, AI chấm ngầm, OneWayLock, Công bố điểm, Phân hệ Phúc khảo Trưởng BM giao GV chấm lại, AI Doubt Guard, Polly/DLQ) + Backend Semester CRUD / Notification in-app / User Mgmt APIs + Toàn bộ xUnit / NetArchTest suites |
| 🧑 **Nguyễn Đăng Hải** | DB Specialist & Frontend Developer (phụ trách Database 30 bảng PostgreSQL và dồn toàn lực phát triển Frontend; tuyệt đối không code C# Backend) | Quản trị CSDL 30 bảng PostgreSQL 16 (Schema DDL, Migrations, Seed Data, Docker Compose — Đã xong) + Dồn toàn lực Frontend (Student Portal Dashboard, Lịch sử luyện tập & thi thử, Giao diện Thi thử Voice-First MF-02, Giao diện Trưởng BM duyệt đề MF-03, Giao diện Trưởng BM cấu hình ca thi & thẩm định/giao đơn phúc khảo MF-04, User Mgmt FE-09, Semester CRUD FE-08) |
| 🧑 **Lê Vũ Hoàng** | Lead Frontend Architect & Fullstack Coordinator | Kiến trúc Frontend Core (React 19 SPA, Tailwind CSS v4, Router DOM v7, Route Guards 5 roles, Axios Interceptors RFC 7807, Zustand stores) + Giao diện Luyện tập MF-01 (Màn hình đệm 60s, Web Speech API, SignalR hook `usePracticeHub.ts`) + Màn hình Kiosk phòng Lab MF-04 (Fullscreen lockdown, blur $\ge 3$, mic $\ge 60$dB, Upload R2 `STT_MSSV.webm` + SHA-256) + Màn hình Hậu kiểm Evidence Panel cho Giảng viên (Waveform Audio Player Wavesurfer.js, sửa điểm kèm giải trình, Publish Grades) + Auth Google UI + Notification in-app FE-11 + Dashboard FE-10 |

---

## 📄 License
This project is developed as part of the Capstone Project FA26SE166. All rights reserved.
