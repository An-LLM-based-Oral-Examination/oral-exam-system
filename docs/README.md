# Technical Documentation — Oral Examination System

Welcome to the technical documentation repository for the **LLM-based Oral Examination System** (FA26SE166).

---

## 📁 Documentation Structure

- **`adr/`** — Architecture Decision Records (ADRs) documenting critical design choices, trade-offs, and rationale.
  - `0001-monorepo-structure.md`: Monorepo architecture selection.
  - `0002-clean-architecture-dotnet8.md`: Adoption of Clean Architecture and CQRS for Backend.
  - `0003-react-typescript-vite.md`: Modern SPA stack for Frontend.
- **`api/`** — OpenAPI / Swagger specifications and data contracts for REST endpoints and SignalR hubs.
- **`workflows/`** — Detailed workflows, sequence diagrams, and activity swimlane models corresponding to core use cases (MF-01 to MF-04).

---

## 🧭 Architectural Principles

1. **Clean Architecture Separation:** Strict boundary isolation where Domain remains pure and devoid of external framework references.
2. **Contract-First Communication:** Strongly typed DTOs and API contracts defined prior to integration.
3. **Resilience & Fault Tolerance:** Multi-tier queue defense, retry policies with exponential backoff, and dead-letter queue storage.
4. **Security & Data Integrity:** Kiosk lockdown, audio cryptographic hashing (SHA-256), and one-way grade locks.
