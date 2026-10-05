# An LLM-based Oral Examination System (FA26SE166)

An Enterprise AI-driven Examination and Assessment platform designed to conduct interactive, rubric-aligned oral examinations for Software Engineering students using Large Language Models (LLMs).

---

## 📖 Overview

The **LLM-based Oral Examination System** automates and standardizes oral assessments (viva voce) through real-time voice interaction, rubric-driven evaluation, and automated feedback loops. The system addresses cognitive overload for examiners, provides structured practice and mock examination sessions for students, and maintains full forensic integrity for final examinations.

### Core Modules & Workflows
- **MF-01 (Interactive Practice):** Voice-driven practice sessions with instant rubric-based scoring, dynamic code-switching correction buffers (`transcript_buffer_seconds`), and configurable follow-up questions (`max_follow_up_questions`).
- **MF-02 (Mock Examination):** Timed mock exams utilizing shared practice question pool, enforcing daily quotas ($K=3$), dual countdown timers, and instant rubric scorecard feedback.
- **MF-03 (Question Bank, FLM Generator & Rubric Studio):** Bloom's taxonomy-aligned question bank management with 10.0-point rubric invariant validation, automated question generation from FPT FLM API (accessible to Lecturers and Department Heads, reviewed & approved by Department Head), and standardized Model Answers ($\ge 50$ chars).
- **MF-04 (Lab Viva Exam, Lecturer Audit & Internal Appeals):** Secure kiosk-mode final examination with audio integrity hashing, transcript-only AI grading, Evidence Panel (AudioURL, Whisper transcript, AI CoT), atomic one-way grade publishing, and internal student grade appeals handled by Department Head.

### 🛡️ User Roles & Role-Based Access Control (RBAC)
The platform enforces strict enterprise RBAC across 5 standardized user roles:
- `student`: Enrolled SE students (Interactive Practice MF-01, Timed Mock Exam MF-02, Official Lab Kiosk Exam MF-04, Internal Grade Appeals `AppealRequest`).
- `lecturer`: Course lecturers and viva examiners (Manual question authoring, automated question generation from FPT FLM API with custom rubrics submitted for review, AI Simulator rubric calibration, Lab proctoring, Lecturer Audit Portal with Evidence Panel & grade publishing).
- `department_head`: Academic Department Head (Course syllabus management, question bank review, approval/rejection of questions submitted by lecturers, automated question generation from FPT FLM API, and adjudication of internal student appeal requests `AppealRequest`).
- `proctor`: Lab exam proctors / invigilators (Shift check-in, booth assignment 1–40, synchronized exam broadcast).
- `admin`: System administrators (System configurations, academic cohort setup, RBAC management, Dead-Letter Queue monitoring & replay).

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
- [Database ERD 28 Tables 3NF](docs/ERD_DATABASE_DESIGN.md)
- [API Contract & Integration Guide](docs/API_CONTRACT_AND_INTEGRATION_GUIDE.md)
- [Git & Team Workflow](docs/GIT_AND_TEAM_WORKFLOW.md)
- [Interactive Visual Flow Viewer (HTML)](docs/diagrams/XEM_4_MAINFLOWS_TRUC_TIEP.html)
- [Interactive System Architecture Slide (HTML)](docs/diagrams/XEM_KIEN_TRUC_HE_THONG_TRUC_TIEP.html)

---

## 👥 Core Engineering Team (FA26SE166)

| Member | Role | Primary Responsibilities |
|---|---|---|
| 🧑 **Nguyễn Quang Thành** | Team Leader & Lead Backend Architect | Clean Architecture, In-Memory Bounded Channels, SignalR Real-time, PostgreSQL Quota Guard, One-Way Lock, System Integration |
| 🧑 **Nguyễn Trọng Tốt** | Backend Developer, AI Engineer & QA Lead | Google Gemini 1.5 Flash/Pro CoT Prompts, Whisper STT, Cloudflare R2 Upload & SHA-256 Seal, Rubric 10.0 Invariants, Testing |
| 🧑 **Nguyễn Đăng Hải** | DB Specialist & Frontend Developer (phụ trách Database 28 bảng PostgreSQL và dồn toàn lực phát triển Frontend; tuyệt đối không code C# Backend) | PostgreSQL 16 Schema (28 Tables 3NF), Docker Compose, Academic & Shift Management, Kiosk Tickets, DLQ & Audit Logs |
| 🧑 **Lê Vũ Hoàng** | Lead Frontend Architect & Fullstack Coordinator | React 19 SPA, Tailwind CSS v4, Web Speech API, Buffer Screen (MF-01: 60s, MF-02: 45s, MF-04: theo môn), Kiosk Lockdown, Waveform Player, Contract-First API Sync |

---

## 📄 License
This project is developed as part of the Capstone Project FA26SE166. All rights reserved.
