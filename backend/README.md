# Oral Examination System — Backend (.NET 8 Clean Architecture)

This folder contains the core backend services for the **LLM-based Oral Examination System** (FA26SE166), built with .NET 8 following Clean Architecture and CQRS principles.

---

## 🏛️ Clean Architecture Layers

```
backend/
├── OralExamination.sln       # Visual Studio Solution
└── src/
    ├── Domain/               # Enterprise core: Entities, Value Objects, Domain Exceptions
    │   └── Domain.csproj     # (Zero external project dependencies)
    ├── Application/          # Use cases: Commands, Queries, Handlers, DTOs, Validators
    │   └── Application.csproj# (References Domain)
    ├── Infrastructure/       # External adapters: Database, Redis, LLM API client, SignalR
    │   └── Infrastructure.csproj # (References Application and Domain)
    └── API/                  # Presentation: REST Controllers, Middleware, DI Composition Root
        ├── Program.cs        # Application entrypoint and pipeline setup
        ├── appsettings.json  # Configuration
        └── API.csproj        # (References Application and Infrastructure)
```

---

## 🛠️ Build & Verification Commands

### Build Entire Solution
```bash
dotnet restore
dotnet build
```

### Run Web API Server
```bash
cd src/API
dotnet run
```
API endpoints and OpenAPI documentation (Swagger) will be available at:
- Swagger UI: `http://localhost:5265/swagger`
- Health check: `http://localhost:5265/health`

---

## 🧭 Dependency Inversion Rules
- `Domain` is completely independent and references no other layers.
- `Application` depends only on `Domain`.
- `Infrastructure` implements abstractions defined in `Application` and `Domain`.
- `API` is the presentation layer and composition root depending on `Application` and `Infrastructure`.
