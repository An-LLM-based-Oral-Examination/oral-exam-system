# An LLM-based Oral Examination System (FA26SE166)

An Enterprise AI-driven Examination and Assessment platform designed to conduct interactive, rubric-aligned oral examinations for Software Engineering students using Large Language Models (LLMs).

---

## 📖 Overview

The **LLM-based Oral Examination System** automates and standardizes oral assessments (viva voce) through real-time voice interaction, rubric-driven evaluation, and automated feedback loops. The system addresses cognitive overload for examiners, provides structured practice and mock examination sessions for students, and maintains full forensic integrity for final examinations.

### Core Modules & Workflows
- **MF-01 (Interactive Practice):** Voice-driven practice sessions with instant rubric-based scoring, code-switching correction buffers, and follow-up question generation.
- **MF-02 (Mock Examination):** Timed mock exams enforcing daily quotas, dual countdown timers, and asynchronous grading fallback.
- **MF-03 (Question Bank & Rubric Studio):** Bloom's taxonomy-aligned question bank management with 10.0-point rubric invariant validation.
- **MF-04 (Lab Viva Exam & Lecturer Audit):** Secure kiosk-mode final examination with audio integrity hashing and lecturer review portal with one-way grade locking.

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
│       ├── Infrastructure/   # Database context, external integrations (LLM, Redis, Storage)
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

Additional technical documentation is maintained under the `docs/` folder:
- [Architecture & Standards](docs/README.md)
- [Architecture Decision Records (ADRs)](docs/adr/)
- [API Specifications](docs/api/)

---

## 📄 License
This project is developed as part of the Capstone Project FA26SE166. All rights reserved.
