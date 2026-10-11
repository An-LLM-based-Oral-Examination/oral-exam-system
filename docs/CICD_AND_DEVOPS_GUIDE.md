# CẨM NANG VẬN HÀNH CI/CD & DEVOPS ENTERPRISE (CI/CD & DEVOPS OPERATIONS HANDBOOK)

> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (LLM Oral Exam System)  
> **Mã đề tài:** FA26SE166 — Học kỳ Fall 2026 (FA26) — Đại học FPT TP.HCM  
> **Đội ngũ kỹ sư tác chiến (FA26SE166):**  
> - **Nguyễn Quang Thành:** Team Leader & Lead Backend Architect  
> - **Nguyễn Trọng Tốt:** Backend Developer, AI Engineer & QA Lead  
> - **Nguyễn Đăng Hải:** DB Specialist & Frontend Developer  
> - **Lê Vũ Hoàng:** Lead Frontend Architect & Fullstack Coordinator  
> **Repository:** `05_Source_Code` | **Git Branch:** `feature/be-core-infrastructure`  
> **Tiêu chuẩn chất lượng:** Enterprise Level · Zero-Defect Delivery · Zero Trust Architecture · CIS Benchmark Non-Root Containers  

---

## MỤC LỤC TỔNG QUAN

1. [PHẦN 1: TỔNG QUAN KIẾN TRÚC CI/CD & CONTAINERIZATION](#phần-1-tổng-quan-kiến-trúc-cicd--containerization)
   - 1.1. [Triết lý Thiết kế: Shift-Left Quality Gates & Zero-Downtime Delivery](#11-triết-lý-thiết-kế-shift-left-quality-gates--zero-downtime-delivery)
   - 1.2. [Sơ đồ Luồng Kiểm soát Chất lượng Tự động (CI Quality Gate Workflow)](#12-sơ-đồ-luồng-kiểm-soát-chất-lượng-tự-động-ci-quality-gate-workflow)
   - 1.3. [Sơ đồ Luồng Đóng gói & Triển khai Tự động (CD Deployment Workflow)](#13-sơ-đồ-luồng-đóng-gói--triển-khai-tự-động-cd-deployment-workflow)
   - 1.4. [Sơ đồ Cấu trúc Mạng & Container Topology Nội bộ (Network & Container Topology)](#14-sơ-đồ-cấu-trúc-mạng--container-topology-nội-bộ-network--container-topology)
2. [PHẦN 2: DANH MỤC GITHUB SECRETS & QUẢN TRỊ BẢO MẬT (SECRETS CATALOG)](#phần-2-danh-mục-github-secrets--quản-trị-bảo-mật-secrets-catalog)
   - 2.1. [Bảng Ma Trận Phân Loại GitHub Secrets Toàn Diện](#21-bảng-ma-trận-phân-loại-github-secrets-toàn-diện)
   - 2.2. [Mô Tả Chi Tiết Mục Đích & Hướng Dẫn Sinh Giá Trị An Toàn](#22-mô-tả-chi-tiết-mục-đích--hướng-dẫn-sinh-giá-trị-an-toàn)
   - 2.3. [Quy Trình Cấu Hình Secrets Trên Giao Diện GitHub Repository](#23-quy-trình-cấu-hình-secrets-trên-giao-diện-github-repository)
   - 2.4. [Mẫu Tệp Biến Môi Trường Máy Chủ Production (.env.production.example)](#24-mẫu-tệp-biến-môi-trường-máy-chủ-production-envproductionexample)
3. [PHẦN 3: HƯỚNG DẪN KIỂM THỬ CI/CD CỤC BỘ (LOCAL TESTING & VALIDATION GUIDE)](#phần-3-hướng-dẫn-kiểm-thử-cicd-cục-bộ-local-testing--validation-guide)
   - 3.1. [Chạy Thử Nghiệm GitHub Actions Cục Bộ Bằng Nektos Act](#31-chạy-thử-nghiệm-github-actions-cục-bộ-bằng-nektos-act)
   - 3.2. [Xác Thực Cú Pháp & Khởi Chạy Docker Compose Production Cục Bộ](#32-xác-thực-cú-pháp--khởi-chạy-docker-compose-production-cục-bộ)
   - 3.3. [Bộ Lệnh Kiểm Chứng Cục Bộ Dành Cho Lập Trình Viên Trước Khi Mở PR](#33-bộ-lệnh-kiểm-chứng-cục-bộ-dành-cho-lập-trình-viên-trước-khi-mở-pr)
4. [PHẦN 4: KỊCH BẢN BẢO VỆ ĐỒ ÁN TRƯỚC HỘI ĐỒNG TỐT NGHIỆP FPT (DEFENSE DEMO SCRIPT)](#phần-4-kịch-bản-bảo-vệ-đồ-án-trước-hội-đồng-tốt-nghiệp-fpt-defense-demo-script)
   - 4.1. [Bảng Phân Vai Thuyết Trình Của Nhóm 4 Kỹ Sư FA26SE166](#41-bảng-phân-vai-thuyết-trình-của-nhóm-4-kỹ-sư-fa26se166)
   - 4.2. [Phần Mở Đầu: Tuyên Ngôn Kiến Trúc DevOps Cấp Enterprise (2 phút)](#42-phần-mở-đầu-tuyên-ngôn-kiến-trúc-devops-cấp-enterprise-2-phút)
   - 4.3. [Demo 1: Cơ Chế Commit Linter Chặn Đứng Commit Sai Quy Cách (3 phút)](#43-demo-1-cơ-chế-commit-linter-chặn-đứng-commit-sai-quy-cách-3-phút)
   - 4.4. [Demo 2: Tab GitHub Actions Với CI Quality Gate Song Song (4 phút)](#44-demo-2-tab-github-actions-với-ci-quality-gate-song-song-4-phút)
   - 4.5. [Demo 3: CD Pipeline, Đóng Gói Multi-Stage & Mạng Cách Ly (4 phút)](#45-demo-3-cd-pipeline-đóng-gói-multi-stage--mạng-cách-ly-4-phút)
   - 4.6. [Bộ Thẻ Phản Biện Các Câu Hỏi Hóc Búa Của Hội Đồng (Q&A Battlecard)](#46-bộ-thẻ-phản-biện-các-câu-hỏi-hóc-búa-của-hội-đồng-qa-battlecard)

---

# PHẦN 1: TỔNG QUAN KIẾN TRÚC CI/CD & CONTAINERIZATION

## 1.1. Triết lý Thiết kế: Shift-Left Quality Gates & Zero-Downtime Delivery

Hệ thống **LLM Oral Exam System (FA26SE166)** là nền tảng tổ chức và chấm thi vấn đáp chuyên sâu phục vụ đánh giá năng lực sinh viên ngành Kỹ thuật Phần mềm (SE) tại Đại học FPT. Mọi sai sót trong quá trình vận hành phòng thi — từ độ trễ mạng, mất kết nối âm thanh, lỗi mã nguồn chưa bắt ngoại lệ, cho đến lộ cơ sở dữ liệu — đều có thể làm gián đoạn kỳ thi thật (MF-04) và ảnh hưởng nghiêm trọng đến tính toàn vẹn pháp lý của kết quả tốt nghiệp.

Do đó, hạ tầng DevOps của dự án được thiết kế theo 3 trụ cột kỹ thuật bất biến:

1. **Shift-Left Quality Gate (Chặn lỗi ngay từ cửa ngõ):**  
   Mọi dòng mã nguồn được đẩy lên repository bắt buộc phải vượt qua hàng rào kiểm định tự động đa tầng:
   - **Cam kết 0 Compiler Warning (`-warnaserror`):** Ngăn chặn triệt để nguy cơ tiềm ẩn gây lỗi runtime (đặc biệt là cảnh báo dereference null `CS8602` và non-nullable property `CS8618`).
   - **Bảo toàn 100% Kiểm thử Tự động:** Bộ 542 unit tests kiểm thử logic miền lõi Domain và Application bắt buộc phải đạt Exit Code 0.
   - **Quét Lỗ hổng Bảo mật Gói phụ thuộc:** Quét tự động các gói NuGet có lỗ hổng bảo mật đã biết (`--vulnerable`).
   - **Kiểm định Cú pháp & Kiểu Tĩnh Frontend:** Kiểm tra bằng Oxlint (linter hiệu năng cao viết bằng Rust) và TypeScript composite strict compiler (`tsc -b`).
   - **Chuẩn hóa Thông điệp Commit:** Bắt buộc tuân thủ 100% quy ước Conventional Commits v1.0.0 thông qua Commitlint.

2. **Immutable Containerization & Least Privilege:**  
   - Ứng dụng Backend .NET 8 được đóng gói dưới dạng container Alpine Linux siêu nhẹ (~110MB runtime so với ~800MB SDK ban đầu).
   - Thiết lập người dùng không có đặc quyền root (`appuser`, UID `10001`) theo chuẩn bảo mật CIS Docker Benchmark.
   - Cơ sở dữ liệu PostgreSQL 16 được cô lập hoàn toàn bên trong mạng nội bộ Docker bridge (`oralexam-network`), không mở cổng 5432 ra Internet công cộng.

3. **Zero-Downtime Rolling Deployment via SSH & GHCR:**  
   - Pipeline phát hành tự động (CD) xây dựng Docker image và lưu trữ an toàn tại GitHub Container Registry (`ghcr.io`).
   - Triển khai máy chủ thông qua kết nối mã hóa SSH (`appleboy/ssh-action`), kéo image mới trước khi khởi động lại dịch vụ ngầm, bảo đảm không gián đoạn kết nối phòng thi.

---

## 1.2. Sơ đồ Luồng Kiểm soát Chất lượng Tự động (CI Quality Gate Workflow)

Quy trình CI Quality Gate được điều phối bởi tệp `.github/workflows/ci.yml`. Luồng kiểm thử được kích hoạt tự động mỗi khi có sự kiện `push` hoặc tạo `pull_request` vào các nhánh `develop`, `main`, hoặc các nhánh tính năng `feature/**`. Ba jobs được thực thi song song nhằm tối ưu hóa thời gian chạy.

```mermaid
flowchart TD
    subgraph Trigger [1. Kích Hoạt CI Workflow]
        A1[Developer Push Commit / Tạo PR] --> B1{Nhánh Đích}
        B1 -->|develop / main / feature/**| C1[GitHub Actions Runner: ubuntu-latest]
        C1 --> C2[Concurrency Manager: cancel-in-progress=true]
    end

    subgraph Parallel_Jobs [2. Thực Thi Song Song 3 Quality Gates]
        direction TB

        subgraph Job_Backend [Job 1: Backend Quality Gate .NET 8]
            BE1[actions/checkout@v4] --> BE2[actions/setup-dotnet@v4: .NET 8.0.x]
            BE2 --> BE3[Cache NuGet ~/.nuget/packages hash csproj]
            BE3 --> BE4[dotnet restore OralExamination.sln]
            BE4 --> BE5[dotnet build Release -warnaserror -nowarn:NU1900]
            BE5 --> BE6[dotnet test Release --no-build: 542 Tests + Coverage]
            BE6 --> BE7[dotnet list package --vulnerable --include-transitive]
        end

        subgraph Job_Frontend [Job 2: Frontend Quality Gate React 19]
            FE1[actions/checkout@v4] --> FE2[actions/setup-node@v4: Node.js 22 LTS]
            FE2 --> FE3[Cache npm dependencies]
            FE3 --> FE4[npm ci]
            FE4 --> FE5[npm run lint: oxlint 74ms]
            FE5 --> FE6[npx tsc -b: TypeScript Strict Zero Errors]
            FE6 --> FE7[npm run build: Vite Production Bundle]
        end

        subgraph Job_Commitlint [Job 3: Commit Message Linter]
            CL1{Sự kiện Pull Request?}
            CL1 -->|Đúng| CL2[actions/checkout@v4: fetch-depth=0]
            CL1 -->|Sai| CL6[Bỏ qua Job]
            CL2 --> CL3[wagoid/commitlint-github-action@v6]
            CL3 --> CL4[Kiểm tra commitlint.config.js]
            CL4 --> CL5[Xác thực 11 Types & 9 Bounded Context Scopes]
        end
    end

    subgraph Gate_Decision [3. Cổng Thẩm Định & Quyết Định]
        BE7 --> GD{Tất Cả 3 Jobs Đỗ?}
        FE7 --> GD
        CL5 --> GD
        GD -->|Thành công: Exit Code 0| PASS[CI Quality Gate: PASS -> Cho Phép Code Review & Merge]
        GD -->|Thất bại: Exit Code != 0| FAIL[CI Quality Gate: BLOCKED -> Chặn Đứng PR, Báo Lỗi Developer]
    end

    classDef passStyle fill:#28a745,stroke:#1e7e34,color:#ffffff,font-weight:bold;
    classDef failStyle fill:#dc3545,stroke:#bd2130,color:#ffffff,font-weight:bold;
    classDef jobStyle fill:#f8f9fa,stroke:#6c757d,color:#212529;

    class PASS passStyle;
    class FAIL failStyle;
    class Job_Backend,Job_Frontend,Job_Commitlint jobStyle;
```

---

## 1.3. Sơ đồ Luồng Đóng gói & Triển khai Tự động (CD Deployment Workflow)

Quy trình CD Deployment được điều phối bởi tệp `.github/workflows/cd.yml`. Pipeline tự động kích hoạt khi có commit được merge vào nhánh `main` hoặc khi gắn thẻ phát hành phiên bản mới (Release Tag dạng `v*.*.*`).

```mermaid
sequenceDiagram
    autonumber
    actor Dev as Kỹ Sư Lead (Nguyễn Quang Thành)
    participant GH as GitHub Repository (Main Branch)
    participant GHA as GitHub Actions Runner
    participant GHCR as GitHub Container Registry (ghcr.io)
    participant VPS as Máy Chủ Triển Khai (Production VPS)
    participant Docker as Docker Daemon VPS (Compose Engine)

    Dev->>GH: Merge PR vào nhánh main / Gắn Tag v1.0.0
    GH->>GHA: Kích hoạt Workflow cd.yml

    rect rgb(240, 248, 255)
        note over GHA,GHCR: Giai đoạn 1: Build & Push Images (Job: build-and-push)
        GHA->>GHA: Checkout mã nguồn & Khởi tạo Docker Buildx
        GHA->>GHCR: Đăng nhập ghcr.io bằng GITHUB_TOKEN
        GHA->>GHA: Build Backend Multi-stage (mcr.microsoft.com/dotnet/sdk:8.0-alpine -> aspnet:8.0-alpine)
        GHA->>GHCR: Push ghcr.io/.../backend:latest & :${GITHUB_SHA}
        GHA->>GHA: Build Frontend Multi-stage (node:22-alpine -> nginx:1.27-alpine)
        GHA->>GHCR: Push ghcr.io/.../frontend:latest & :${GITHUB_SHA}
    end

    rect rgb(255, 250, 240)
        note over GHA,Docker: Giai đoạn 2: Remote Deployment via SSH (Job: deploy)
        GHA->>VPS: Kết nối SSH an toàn qua appleboy/ssh-action (SERVER_SSH_KEY)
        VPS->>GHCR: Đăng nhập ghcr.io bằng GitHub Token
        VPS->>GHCR: docker compose -f docker-compose.prod.yml pull
        GHCR-->>VPS: Tải các Docker layers mới nhất về máy chủ
        VPS->>Docker: docker compose -f docker-compose.prod.yml up -d --remove-orphans
        Docker->>Docker: Khởi động postgres (healthcheck pg_isready)
        Docker->>Docker: Khởi động backend (.NET 8 non-root UID 10001, port 5000)
        Docker->>Docker: Khởi động frontend (Nginx 1.27 reverse proxy, port 80)
        VPS->>Docker: docker image prune -f (Dọn sạch dangling images)
        Docker-->>VPS: Hệ thống hoạt động ổn định
        VPS-->>GHA: Trả về trạng thái Deployment Completed (Exit Code 0)
    end

    GHA-->>Dev: Thông báo triển khai thành công trên GitHub Actions
```

---

## 1.4. Sơ đồ Cấu trúc Mạng & Container Topology Nội bộ (Network & Container Topology)

Hệ thống triển khai sản xuất sử dụng tệp `docker-compose.prod.yml` gồm 3 services được gắn kết trên mạng bridge biệt lập `oralexam-network`. Nginx đóng vai trò Edge Reverse Proxy đón nhận toàn bộ lưu lượng người dùng, phân tách các yêu cầu tĩnh SPA, các lời gọi REST API `/api/` và luồng thời gian thực SignalR WebSocket `/hubs/`.

```mermaid
graph TB
    subgraph Internet_Zone [Môi Trường Bên Ngoài / Internet]
        Browser[Client Browser: Sinh Viên / Giảng Viên / Giám Thị]
        Mic[Microphone Hardware: Thu Âm Vấn Đáp]
    end

    subgraph Host_Machine [Máy Chủ Production VPS / Docker Host Engine]
        subgraph Ports_Mapping [Cổng Mở Ra Máy Chủ Host]
            HostPort80["Cổng Host :80 (HTTP)"]
        end

        subgraph Docker_Network ["Mạng Nội Bộ Biệt Lập: oralexam-network (Bridge Driver)"]
            subgraph Container_Frontend ["Container: oralexam-frontend-prod (Nginx 1.27-alpine)"]
                NginxEdge[Nginx Reverse Proxy & Static Server]
                SPAFiles[Static Assets: HTML, JS, CSS, Media /usr/share/nginx/html]
            end

            subgraph Container_Backend ["Container: oralexam-backend-prod (ASP.NET 8 Alpine)"]
                AppUser[User: appuser - UID 10001 non-root]
                Kestrel[Kestrel Web Server: Port 5000 internal]
                SignalRHub[SignalR Practice Hub: /hubs/practice]
                RestControllers[REST Controllers: /api/v1/...]
                HealthProbe[Healthcheck Endpoint: /health]
            end

            subgraph Container_Database ["Container: oralexam-postgres-prod (PostgreSQL 16-alpine)"]
                PGServer[PostgreSQL Database Engine: Port 5432 internal]
                PGData[(Volume: postgres_prod_data)]
                InitScripts[Init DB: 01_schema.sql 30 tables + 02_seed.sql]
            end
        end
    end

    subgraph External_Cloud [Dịch Vụ Đám Mây Ngoài]
        CloudGemini[Google Gemini 1.5 Flash/Pro AI: Đánh Giá & Follow-up]
        CloudR2[Cloudflare R2 Object Storage: Niêm Phong Audio STT_MSSV.webm]
    end

    Browser -->|HTTP Port 80| HostPort80
    Mic --> Browser
    HostPort80 --> NginxEdge

    NginxEdge -->|Phục vụ file tĩnh SPA try_files| SPAFiles
    NginxEdge -->|Reverse Proxy /api/ HTTP 1.1| Kestrel
    NginxEdge -->|Reverse Proxy /hubs/ WebSocket Upgrade Timeout 300s| SignalRHub
    NginxEdge -->|Reverse Proxy /health Probe| HealthProbe

    Kestrel --> RestControllers
    RestControllers -->|Nội bộ: Host=postgres Port=5432| PGServer
    SignalRHub -->|Truy vấn dữ liệu| PGServer
    PGServer --- PGData
    PGServer --- InitScripts

    RestControllers -->|HTTPS API Key| CloudGemini
    RestControllers -->|S3 API Hashing SHA-256| CloudR2

    classDef edgeStyle fill:#007bff,stroke:#0056b3,color:#ffffff,font-weight:bold;
    classDef beStyle fill:#28a745,stroke:#1e7e34,color:#ffffff,font-weight:bold;
    classDef dbStyle fill:#6f42c1,stroke:#59359a,color:#ffffff,font-weight:bold;
    classDef cloudStyle fill:#fd7e14,stroke:#d35400,color:#ffffff,font-weight:bold;

    class NginxEdge edgeStyle;
    class Kestrel,SignalRHub,RestControllers,AppUser beStyle;
    class PGServer,PGData dbStyle;
    class CloudGemini,CloudR2 cloudStyle;
```

### Các Đặc Tính Kỹ Thuật Nổi Bật Của Cấu Hình Nginx (`frontend/nginx.conf`):
1. **Hỗ trợ SPA React Routing:** Chỉ thị `try_files $uri $uri/ /index.html;` xử lý triệt để bài toán tải lại trang (F5) khi người dùng đang ở các route của React Router DOM (như `/student/practice`, `/kiosk/room`).
2. **WebSocket Upgrade Cho SignalR:** Sử dụng biến ánh xạ `map $http_upgrade $connection_upgrade` kết hợp các header `Upgrade $http_upgrade` và `Connection $connection_upgrade`. Thiết lập `proxy_read_timeout 300s` và `proxy_send_timeout 300s` bảo đảm kết nối âm thanh không bị gián đoạn giữa chừng trong suốt 5 phút sinh viên phát biểu.
3. **Bộ Tiêu Chuẩn Security Headers:**
   - `X-Frame-Options "SAMEORIGIN"`: Chống tấn công Clickjacking.
   - `X-Content-Type-Options "nosniff"`: Chống giả mạo định dạng MIME type.
   - `Referrer-Policy "strict-origin-when-cross-origin"`: Kiểm soát bảo vệ thông tin referrer.
   - `Permissions-Policy "microphone=(self), camera=(), geolocation=()"`: Cấp quyền truy cập micro cho tính năng thu âm thi vấn đáp Web Speech API, đồng thời vô hiệu hóa toàn bộ camera và định vị GPS.
4. **Bộ Đệm & Nén Tối Ưu:** Nén gzip mức độ 6 cho toàn bộ assets văn bản; lưu cache 1 năm đối với các tệp tĩnh đóng gói (`max-age=31536000, immutable`).

---

# PHẦN 2: DANH MỤC GITHUB SECRETS & QUẢN TRỊ BẢO MẬT (SECRETS CATALOG)

## 2.1. Bảng Ma Trận Phân Loại GitHub Secrets Toàn Diện

Mọi thông tin nhạy cảm (thông tin đăng nhập máy chủ, mật khẩu cơ sở dữ liệu, khóa API AI, khóa lưu trữ đám mây) **tuyệt đối không được lưu trực tiếp trong mã nguồn hoặc tệp cấu hình Git**. Hệ thống quản trị tập trung toàn bộ biến bảo mật qua cơ chế GitHub Secrets.

| STT | Tên Secret | Phạm Vi Sử Dụng | Mức Độ Nhạy Cảm | Định Dạng / Giá Trị Mẫu | Mô Tả Mục Đích Kỹ Thuật |
|:---:|:---|:---|:---:|:---|:---|
| 1 | `SERVER_HOST` | CD Workflow (`cd.yml`) | 🔴 Nghiêm ngặt | `103.179.188.45` hoặc `vps.oralexam.edu.vn` | Địa chỉ IP công cộng IPv4 hoặc tên miền máy chủ VPS production để thực hiện kết nối SSH. |
| 2 | `SERVER_USER` | CD Workflow (`cd.yml`) | 🟡 Trung bình | `deployer` hoặc `ubuntu` | Tên tài khoản người dùng Linux trên VPS có quyền thực thi lệnh Docker (thuộc nhóm `docker`). |
| 3 | `SERVER_SSH_KEY` | CD Workflow (`cd.yml`) | 🔴 Nghiêm ngặt | Chuỗi PEM khóa riêng tư `-----BEGIN OPENSSH PRIVATE KEY-----...` | Khóa bảo mật riêng tư SSH (Ed25519 hoặc RSA 4096-bit) dùng để xác thực không cần mật khẩu. |
| 4 | `SERVER_PORT` | CD Workflow (`cd.yml`) | 🟢 Tiêu chuẩn | `22` (hoặc cổng tùy chỉnh ví dụ `2222`) | Cổng dịch vụ daemon SSH trên máy chủ VPS. Mặc định là cổng 22. |
| 5 | `POSTGRES_PASSWORD` | Docker Compose / Backend | 🔴 Nghiêm ngặt | `Exam_F@ll2026_SecureP@ssw0rd!#99x` | Mật khẩu siêu quản trị của PostgreSQL 16 (yêu cầu độ dài $\ge 24$ ký tự, độ hỗn loạn entropy cao). |
| 6 | `GEMINI_API_KEY` | Backend (.NET 8) | 🔴 Nghiêm ngặt | `AIzaSyD-xxx...xxx` | API Key của Google Gemini AI Studio phục vụ tính năng sinh đề thi MF-03 và chấm điểm viva CoT MF-04. |
| 7 | `R2_ACCOUNT_ID` | Backend (.NET 8) | 🟡 Trung bình | `9a8b7c6d5e4f3a2b1c0d9e8f7a6b5c4d` | Mã định danh tài khoản Cloudflare Account ID để điều hướng các API calls về dịch vụ Cloudflare R2. |
| 8 | `R2_ACCESS_KEY_ID` | Backend (.NET 8) | 🔴 Nghiêm ngặt | `a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6` | Access Key ID (tương thích AWS S3 API) có quyền ghi vào Cloudflare R2 bucket. |
| 9 | `R2_SECRET_ACCESS_KEY` | Backend (.NET 8) | 🔴 Nghiêm ngặt | `f8e7d6c5b4a3...` (chuỗi hex 64 ký tự) | Secret Access Key tương ứng của Cloudflare R2 để ký mã hóa các request S3 lưu trữ audio. |
| 10 | `R2_BUCKET` | Backend (.NET 8) | 🟢 Tiêu chuẩn | `oral-exam-bucket` | Tên vùng chứa (bucket) lưu trữ vĩnh viễn các tệp ghi âm niêm phong `STT_MSSV.webm`. |
| 11 | `GHCR_PAT` | VPS Deploy / Runner | 🔴 Nghiêm ngặt | `ghp_xxxxxxxxxxxxxxxxxxxx` | Personal Access Token của GitHub (quyền `read:packages`, `write:packages`) phục vụ pull ảnh từ xa. |

---

## 2.2. Mô Tả Chi Tiết Mục Đích & Hướng Dẫn Sinh Giá Trị An Toàn

### 1. `SERVER_SSH_KEY` (Khóa Riêng Tư SSH Máy Chủ)
- **Mục đích:** Cho phép GitHub Actions Runner kết nối vào VPS thực hiện lệnh cập nhật dịch vụ mà không yêu cầu nhập mật khẩu tương tác.
- **Cách sinh khóa an toàn trên máy trạm:**
  ```bash
  ssh-keygen -t ed25519 -C "github-actions-deployer@oralexam" -f ./id_ed25519_deployer
  ```
- **Cấu hình trên VPS:** Thêm nội dung tệp công khai `id_ed25519_deployer.pub` vào tệp `~/.ssh/authorized_keys` của tài khoản `deployer`.
- **Cấu hình trên GitHub:** Sao chép toàn bộ nội dung tệp riêng tư `id_ed25519_deployer` dán vào secret `SERVER_SSH_KEY`.

### 2. `POSTGRES_PASSWORD` (Mật Khẩu Cơ Sở Dữ Liệu)
- **Mục đích:** Khởi tạo tài khoản quản trị PostgreSQL trong container `oralexam-postgres-prod` và cung cấp chuỗi kết nối an toàn cho Backend .NET 8.
- **Cách sinh mật khẩu an toàn ngẫu nhiên:**
  ```bash
  openssl rand -base64 32
  ```

### 3. `GEMINI_API_KEY` (Khóa Tích Hợp Google Gemini AI)
- **Mục đích:** Cấp phép cho module AI Service gọi các model `gemini-1.5-flash` và `gemini-1.5-pro` thực hiện bóc tách Rubric FLM (MF-03), hỏi follow-up ngữ cảnh và chấm điểm chuỗi suy luận Chain-of-Thought (CoT) trong MF-04.
- **Khuyến nghị an toàn:** Thiết lập hạn mức ngân sách (Spending Limit) và giới hạn API Key chỉ sử dụng cho Gemini API trên Google Cloud Console.

### 4. `R2_ACCESS_KEY_ID` & `R2_SECRET_ACCESS_KEY` (Cloudflare R2 Object Storage)
- **Mục đích:** Cấp quyền cho Backend stream trực tiếp file âm thanh WebM thu từ máy trạm Kiosk lên Cloudflare R2 với tên file bất biến `STT_MSSV.webm` kèm mã băm SHA-256. R2 miễn phí phí truyền tải dữ liệu (Zero Egress Fee), tối ưu chi phí lưu trữ âm thanh cho kỳ thi.

---

## 2.3. Quy Trình Cấu Hình Secrets Trên Giao Diện GitHub Repository

Để đưa các secrets vào hệ thống GitHub an toàn, Trưởng nhóm (Nguyễn Quang Thành) thực hiện theo 4 bước sau:

1. **Truy cập Cài đặt Kho lưu trữ:**  
   Mở trình duyệt truy cập kho mã nguồn: `https://github.com/An-LLM-based-Oral-Examination/oral-exam-system` $\to$ Chọn tab **Settings**.
2. **Điều hướng mục Secrets:**  
   Tại thanh điều hướng bên trái, chọn **Secrets and variables** $\to$ Chọn **Actions**.
3. **Thêm Secret Mới:**  
   Bấm nút xanh **New repository secret**.
4. **Nhập Tên & Giá Trị:**  
   - Ô **Name:** Nhập chính xác tên viết hoa (Ví dụ: `SERVER_SSH_KEY`).
   - Ô **Secret:** Dán giá trị bí mật tương ứng (Lưu ý: Không để thừa khoảng trắng hoặc dòng trống không cần thiết).
   - Bấm **Add secret**.

> **⚠️ Nguyên Tắc Bất Biến (Zero-Leak Mandate):**  
> GitHub Actions tự động che mặt nạ (`***`) các giá trị secrets trong toàn bộ logs thực thi. Tuyệt đối không viết lệnh `echo $SECRET` hoặc debug in giá trị secrets ra màn hình điều khiển.

---

## 2.4. Mẫu Tệp Biến Môi Trường Máy Chủ Production (.env.production.example)

Tại thư mục làm việc trên máy chủ VPS (`/opt/oral-exam-system`), tạo tệp `.env` kế thừa từ mẫu sau:

```bash
# ==============================================================================
# Production Environment Configuration — LLM Oral Exam System (FA26SE166)
# ==============================================================================

# 1. Cấu hình Cơ sở dữ liệu PostgreSQL 16
POSTGRES_USER=oralexam
POSTGRES_PASSWORD=Exam_F@ll2026_SecureP@ssw0rd!#99x
POSTGRES_DB=oralexam

# 2. Cổng dịch vụ Web bên ngoài (Nginx Reverse Proxy)
FRONTEND_PORT=80

# 3. Khóa tích hợp AI Google Gemini
GEMINI_API_KEY=AIzaSyD-sample-gemini-production-key-here

# 4. Cấu hình Lưu trữ Âm thanh Cloudflare R2
CLOUDFLARE_ACCOUNT_ID=9a8b7c6d5e4f3a2b1c0d9e8f7a6b5c4d
CLOUDFLARE_API_TOKEN=cf-token-production-sample
STORAGE_ACCESS_KEY=a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6
STORAGE_SECRET_KEY=f8e7d6c5b4a39281726354453627182930495867
STORAGE_SERVICE_URL=https://9a8b7c6d5e4f3a2b1c0d9e8f7a6b5c4d.r2.cloudflarestorage.com
STORAGE_BUCKET_NAME=oral-exam-bucket
STORAGE_PUBLIC_URL_PREFIX=https://pub-audio.oralexam.edu.vn
```

---

# PHẦN 3: HƯỚNG DẪN KIỂM THỬ CI/CD CỤC BỘ (LOCAL TESTING & VALIDATION GUIDE)

## 3.1. Chạy Thử Nghiệm GitHub Actions Cục Bộ Bằng Nektos Act

`act` (`nektos/act`) là công cụ tiêu chuẩn ngành cho phép lập trình viên chạy giả lập môi trường GitHub Actions Runner cục bộ trên máy trạm thông qua Docker. Điều này giúp phát hiện và khắc phục lỗi cấu hình pipeline trước khi push lên repository.

### 1. Cài Đặt Công Cụ `act`
- **Trên Windows (qua WinGet hoặc Chocolatey):**
  ```powershell
  winget install nektos.act
  # hoặc
  choco install act-cli
  ```
- **Trên macOS:**
  ```bash
  brew install act
  ```
- **Trên Linux:**
  ```bash
  curl --proto '=https' --tlsv1.2 -sSf https://raw.githubusercontent.com/nektos/act/master/install.sh | sudo bash
  ```

### 2. Các Lệnh Thực Thi Kiểm Thử Workflow Cục Bộ

1. **Liệt kê danh sách các Jobs có trong Pipeline:**
   ```bash
   act -l -W 05_Source_Code/.github/workflows/ci.yml
   ```
   *Kết quả hiển thị:* Danh sách 3 jobs: `backend-ci`, `frontend-ci`, `commitlint`.

2. **Chạy giả lập sự kiện Pull Request kiểm thử Backend Quality Gate:**
   ```bash
   act pull_request -j backend-ci -W 05_Source_Code/.github/workflows/ci.yml -P ubuntu-latest=catthehacker/ubuntu:act-latest
   ```
   *Hành vi:* Khởi động container Ubuntu, cài đặt .NET 8 SDK, tải NuGet packages vào cache, biên dịch sạch sẽ không warning và chạy toàn bộ 542 unit tests.

3. **Chạy giả lập sự kiện Pull Request kiểm thử Frontend Quality Gate:**
   ```bash
   act pull_request -j frontend-ci -W 05_Source_Code/.github/workflows/ci.yml -P ubuntu-latest=catthehacker/ubuntu:act-latest
   ```
   *Hành vi:* Khởi động container Node.js 22 LTS, cài dependencies sạch qua `npm ci`, chạy `oxlint`, kiểm tra kiểu tĩnh `tsc -b`, và đóng gói Vite production bundle.

4. **Chạy giả lập kiểm tra Commitlint:**
   ```bash
   act pull_request -j commitlint -W 05_Source_Code/.github/workflows/ci.yml -P ubuntu-latest=catthehacker/ubuntu:act-latest
   ```

---

## 3.2. Xác Thực Cú Pháp & Khởi Chạy Docker Compose Production Cục Bộ

### 1. Kiểm Tra Tính Hợp Lệ Của Cấu Hình Compose (Dry-run Config)
Lệnh phân tích và biên dịch cấu hình khai báo biến mà không khởi động container:
```powershell
docker compose -f 05_Source_Code/docker-compose.prod.yml config
```
*Điều kiện thành công:* Lệnh trả về Exit Code 0, xuất ra cấu trúc YAML chuẩn hóa đầy đủ 3 services (`postgres`, `backend`, `frontend`), mạng `oralexam-network` và volume `postgres_prod_data`.

### 2. Khởi Động Toàn Bộ Cụm Dịch Vụ Production Cục Bộ
```powershell
# Khởi động ở chế độ chạy nền (-d) kèm build lại images nếu có thay đổi (--build)
docker compose -f 05_Source_Code/docker-compose.prod.yml up -d --build
```

### 3. Kiểm Tra Trạng Thái & Liveness Healthcheck Của Các Containers
```powershell
# Xem bảng trạng thái các container và chỉ số Health status
docker compose -f 05_Source_Code/docker-compose.prod.yml ps
```

*Bảng trạng thái kỳ vọng:*
| Name | Image / Service | Command | State | Ports | Health |
|---|---|---|---|---|---|
| `oralexam-postgres-prod` | `postgres:16-alpine` | `docker-entrypoint.sh...` | Up | 5432/tcp (internal) | `healthy` |
| `oralexam-backend-prod` | `05_source_code-backend` | `dotnet OralExamination.API.dll` | Up | 5000/tcp (internal) | `healthy` |
| `oralexam-frontend-prod` | `05_source_code-frontend` | `nginx -g daemon off;` | Up | 0.0.0.0:80->80/tcp | `healthy` |

### 4. Kiểm Thử Trực Tiếp Endpoints Bằng Curl
```powershell
# 1. Kiểm tra Liveness Probe của Backend qua Nginx Reverse Proxy
curl -i http://localhost/health
# Kỳ vọng: HTTP/1.1 200 OK, Body: Healthy

# 2. Kiểm tra Phục vụ Trang Chủ Frontend React SPA
curl -i http://localhost/
# Kỳ vọng: HTTP/1.1 200 OK, Content-Type: text/html

# 3. Kiểm tra Security Headers trả về từ Nginx
curl -I http://localhost/
# Kỳ vọng xuất hiện các header:
# X-Frame-Options: SAMEORIGIN
# X-Content-Type-Options: nosniff
# Permissions-Policy: microphone=(self), camera=(), geolocation=()
```

### 5. Dừng Và Dọn Dẹp Cụm Container
```powershell
docker compose -f 05_Source_Code/docker-compose.prod.yml down
```

---

## 3.3. Bộ Lệnh Kiểm Chứng Cục Bộ Dành Cho Lập Trình Viên Trước Khi Mở PR

Trước khi thực hiện lệnh tạo commit và mở Pull Request, mỗi thành viên trong nhóm 4 kỹ sư bắt buộc phải chạy bộ lệnh sau trên máy trạm để đảm bảo không vi phạm CI Quality Gate:

```powershell
# ==============================================================================
# BỘ LỆNH KIỂM CHỨNG TRƯỚC KHI TẠO PULL REQUEST (PRE-FLIGHT VERIFICATION)
# ==============================================================================

# 1. Kiểm chứng Backend (.NET 8 Clean Architecture)
Write-Host "=== 1. Kiểm chứng Backend ===" -ForegroundColor Cyan
dotnet restore "05_Source_Code/backend/OralExamination.sln"
dotnet build "05_Source_Code/backend/OralExamination.sln" --configuration Release --no-restore -warnaserror -nowarn:NU1900
dotnet test "05_Source_Code/backend/OralExamination.sln" --configuration Release --no-build

# 2. Kiểm chứng Frontend (React 19 & Vite)
Write-Host "=== 2. Kiểm chứng Frontend ===" -ForegroundColor Cyan
cd "05_Source_Code/frontend"
npm run lint
npx tsc -b
npm run build
cd ../..

# 3. Kiểm chứng Cú pháp Dockerfile & Docker Compose
Write-Host "=== 3. Kiểm chứng Containerization ===" -ForegroundColor Cyan
docker compose -f "05_Source_Code/docker-compose.prod.yml" config > $null
Write-Host "Docker Compose YAML hợp lệ 100%!" -ForegroundColor Green

# 4. Kiểm chứng Thông Điệp Commit Thử Nghiệm
Write-Host "=== 4. Kiểm chứng Commit Message ===" -ForegroundColor Cyan
echo "feat(practice): implement session timeout and anti-consecutive question logic" | npx --prefix 05_Source_Code commitlint
Write-Host "Commit message đúng chuẩn Conventional Commits v1.0.0!" -ForegroundColor Green
```

---

# PHẦN 4: KỊCH BẢN BẢO VỆ ĐỒ ÁN TRƯỚC HỘI ĐỒNG TỐT NGHIỆP FPT (DEFENSE DEMO SCRIPT)

## 4.1. Bảng Phân Vai Thuyết Trình Của Nhóm 4 Kỹ Sư FA26SE166

Kịch bản demo được thiết kế nhịp nhàng, liền mạch trong thời lượng **15 phút trình diễn kỹ thuật** trước Hội đồng chấm tốt nghiệp Đại học FPT TP.HCM:

| Thành Viên | Vai Trò Chính | Nhiệm Vụ Thuyết Trình & Thao Tác Trực Tiếp |
|---|---|---|
| **Nguyễn Quang Thành** | Team Leader & Lead Backend Architect | **Chủ trì phần thi & Mở đầu:** Giới thiệu tổng quan hệ thống, triết lý Shift-Left DevOps bảo vệ tính toàn vẹn kỳ thi vấn đáp; demo luồng CD Deployment, Containerization và điều phối phần Q&A phản biện. |
| **Nguyễn Trọng Tốt** | Backend Developer, AI Engineer & QA Lead | **Demo 2 (Phần Backend):** Trình diễn tab GitHub Actions với bộ 542 unit tests đỗ 100%, biên dịch 0 warning (`-warnaserror`), tích hợp kiểm tra an ninh packages và luồng chấm thi AI CoT. |
| **Lê Vũ Hoàng** | Lead Frontend Architect & Fullstack Coordinator | **Demo 1 (Commit Linter) & Demo 2 (Phần Frontend):** Trình diễn cơ chế chặn commit sai quy cách tại PR; trình diễn tốc độ Oxlint (74ms), strict TypeScript composite typecheck (`tsc -b`) và đóng gói Vite. |
| **Nguyễn Đăng Hải** | DB Specialist & Frontend Developer | **Demo 3 (Hạ Tầng & Reverse Proxy):** Trình diễn mô hình mạng nội bộ `oralexam-network` cô lập 30 bảng PostgreSQL 16 3NF; giải thích cơ chế Nginx Reverse Proxy xử lý mượt mà SignalR WebSocket `/hubs/`. |

---

## 4.2. Phần Mở Đầu: Tuyên Ngôn Kiến Trúc DevOps Cấp Enterprise (2 phút)

**Người trình bày: Nguyễn Quang Thành (Team Leader)**

> *"Kính thưa quý Thầy Cô trong Hội đồng chấm tốt nghiệp!*  
> *Một hệ thống thi vấn đáp bằng AI (Oral Exam System) phục vụ kỳ thi thật và cấp bằng cử nhân đòi hỏi những tiêu chuẩn kỹ thuật khắt khe hơn rất nhiều so với các ứng dụng web thông thường. Chúng em không thể chấp nhận rủi ro hệ thống bị rớt kết nối WebSocket khi thí sinh đang nói, không thể chấp nhận cơ sở dữ liệu bị lộ cổng ra ngoài Internet, và càng không thể chấp nhận mã nguồn có lỗi tiềm ẩn gây crash phòng thi.*  
>  
> *Chính vì vậy, ngay từ ngày đầu tiên xây dựng dự án, nhóm FA26SE166 đã thiết lập một hệ thống **Hạ tầng CI/CD & Containerization chuẩn Enterprise**. Toàn bộ mã nguồn trước khi chạm đến môi trường thực tế đều phải đi qua **Cổng kiểm soát chất lượng tự động Shift-Left Quality Gate** — nơi mà quy ước commit được chuẩn hóa bằng máy, 542 bài unit tests phải đỗ trọn vẹn 100%, không cho phép tồn tại dù chỉ một cảnh báo compiler warning nhỏ nhất, và mọi dịch vụ đều được đóng gói trong container bảo mật non-root.*  
>  
> *Sau đây, nhóm chúng em xin phép được chứng minh thực tế năng lực vận hành của hệ thống qua 3 kịch bản demo trực tiếp!"*

---

## 4.3. Demo 1: Cơ Chế Commit Linter Chặn Đứng Commit Sai Quy Cách (3 phút)

**Người thực hiện & Thuyết minh: Lê Vũ Hoàng**

### Các bước thao tác trực tiếp trên màn hình:
1. **Bước 1: Giả lập một lập trình viên tạo commit vi phạm quy cách.**  
   Lê Vũ Hoàng mở terminal và thực hiện commit một thay đổi nhỏ nhưng đặt commit message tùy tiện:
   ```bash
   git commit -m "Update practice logic."
   ```
2. **Bước 2: Đẩy lên nhánh tính năng và mở Pull Request trên GitHub.**  
   Mở giao diện GitHub, tạo Pull Request gộp nhánh tính năng vào `develop`.
3. **Bước 3: Chỉ ra hành vi bảo vệ của GitHub Actions.**  
   - Sau 5 giây, GitHub Actions kích hoạt Job **`Commit Message Linter (Conventional Commits v1.0.0)`**.
   - Pipeline ngay lập tức chuyển sang màu **ĐỎ (Failed)**.
   - Bấm vào chi tiết log của `wagoid/commitlint-github-action@v6`, màn hình chỉ rõ 3 lỗi vi phạm:
     * ❌ `type-empty`: Thiếu tiền tố loại commit hợp lệ (`feat`, `fix`, `docs`,...).
     * ❌ `subject-case`: Chữ cái đầu dòng viết hoa ('U' trong 'Update').
     * ❌ `subject-full-stop`: Đặt dấu chấm (.) ở cuối dòng tiêu đề.
   - Nút **Merge pull request** bị khóa cứng màu xám (Blocked).

4. **Bước 4: Sửa lại commit message đúng chuẩn và xác thực mở khóa.**  
   Hoàng quay lại terminal sửa lại thông điệp bằng `git commit --amend`:
   ```bash
   git commit --amend -m "feat(practice): implement anti-consecutive difficulty logic"
   git push -f
   ```
   - Workflow Commitlint chạy lại và chuyển sang màu **XANH LỤC (Passed)** trong 12 giây!

**Lời bình thuyết minh của Hoàng:**  
> *"Thưa Thầy Cô, cơ chế Commitlint kết hợp với 9 Bounded Contexts của dự án (`practice`, `mock-exam`, `official-exam`, `rubric`, `auth`, `kiosk`, `db`, `api`, `storage`) đảm bảo toàn bộ lịch sử Git của nhóm luôn sạch sẽ, minh bạch và có khả năng truy vết lỗi lập tức nếu phát sinh sự cố trong tương lai."*

---

## 4.4. Demo 2: Tab GitHub Actions Với CI Quality Gate Song Song (4 phút)

**Người thực hiện: Nguyễn Trọng Tốt & Lê Vũ Hoàng**

### 1. Trình diễn Backend Quality Gate (.NET 8 Clean Architecture) — Nguyễn Trọng Tốt
Tốt mở tab **Actions** trên GitHub và bấm vào lần chạy thành công gần nhất của workflow `ci.yml`:
- **Chỉ số NuGet Cache:** Chỉ ra bước `Cache NuGet Packages` với cache key dựa trên hash của toàn bộ file `.csproj`. Thời gian khôi phục thư viện giảm từ 45 giây xuống còn **1.8 giây**.
- **Kỷ luật Zero Warning:** Mở chi tiết bước `dotnet build -warnaserror -nowarn:NU1900`:
  > *"Thưa Thầy Cô, dự án của chúng em áp dụng cờ `-warnaserror`. Trong C# 12 và .NET 8, bất kỳ biến nào có nguy cơ gây `NullReferenceException` (cảnh báo `CS8602`, `CS8618`) đều bị compiler coi là LỖI NGHIÊM TRỌNG và dừng quá trình build ngay lập tức. Toàn bộ 5 projects trong solution của chúng em đạt con số tuyệt đối: **0 Warning, 0 Error**."*
- **Kiểm thử tự động 542 Tests:** Mở bước `dotnet test`:
  > *"Trên màn hình là kết quả thực thi tự động của toàn bộ **542 / 542 Unit Tests** bao phủ các quy tắc nghiệp vụ khắt khe nhất: từ thuật toán chống lặp câu hỏi 3 lần liên tiếp trong MF-01, cơ chế trừ quota thi thử MF-02, ràng buộc barem điểm rubric bằng đúng 10.0đ trong MF-03, cho đến khóa điểm một chiều One-Way Lock MF-04. Tất cả đều đạt Exit Code 0."*
- **Quét an ninh Packages:** Mở bước `Scan Vulnerable NuGet Packages`: Chứng minh 0 thư viện bên thứ 3 nào có lỗ hổng bảo mật chưa được vá.

### 2. Trình diễn Frontend Quality Gate (React 19 & Vite) — Lê Vũ Hoàng
Hoàng chuyển sang kiểm tra Job `frontend-ci`:
- **Hiệu năng Oxlint:**
  > *"Thay vì dùng ESLint truyền thống mất hàng chục giây, chúng em tích hợp **Oxlint** viết bằng Rust. Toàn bộ mã nguồn TypeScript của giao diện Frontend được quét sạch trong vỏn vẹn **74 mili-giây**."*
- **Strict TypeScript Composite (`tsc -b`):** Chứng minh toàn bộ các component React 19 và các lời gọi API DTO đều đạt chuẩn kiểu tĩnh nghiêm ngặt, không sử dụng kiểu dữ liệu `any` vô căn cứ.
- **Vite Production Bundler:** Đóng gói tĩnh hoàn hảo vào thư mục `dist/` sẵn sàng phục vụ trên Nginx.

---

## 4.5. Demo 3: CD Pipeline, Đóng Gói Multi-Stage & Mạng Cách Ly (4 phút)

**Người thực hiện: Nguyễn Quang Thành & Nguyễn Đăng Hải**

### 1. Triển Khai CD Tự Động Qua GHCR & SSH VPS — Nguyễn Quang Thành
Thành chuyển sang workflow `cd.yml`:
- Mở quy trình đóng gói Multi-stage:
  > *"Trong `backend/Dockerfile`, chúng em sử dụng SDK .NET 8 để build nhưng runtime chỉ sử dụng `mcr.microsoft.com/dotnet/aspnet:8.0-alpine`. Kích thước image giảm từ **840MB** xuống chỉ còn **112MB**.*  
  > *Đặc biệt, container không chạy dưới quyền `root`. Chúng em khởi tạo riêng `appuser` với UID `10001` theo khuyến nghị của CIS Benchmark. Ngay cả khi container bị tấn công khai thác, kẻ tấn công cũng không thể leo thang đặc quyền để can thiệp vào máy chủ host."*
- Mở bước SSH Deploy qua `appleboy/ssh-action`:
  > *"Khi mã nguồn được merge vào `main`, GitHub Actions tự động đẩy image lên GitHub Container Registry (`ghcr.io`), sau đó kết nối SSH vào VPS, thực hiện lệnh `docker compose pull` và `up -d` để cập nhật dịch vụ ngầm mà không làm rớt các phiên thi đang diễn ra."*

### 2. Kiến Trúc Mạng Biệt Lập & Nginx Reverse Proxy — Nguyễn Đăng Hải
Hải mở giao diện dòng lệnh trên máy chủ hoặc mô hình topology:
- **Cô lập Database PostgreSQL 16:**
  > *"Kính thưa Thầy Cô, cơ sở dữ liệu lưu trữ 30 bảng của đồ án hoàn toàn **không mở cổng 5432 ra Internet**. Cổng 5432 chỉ lắng nghe bên trong mạng bridge nội bộ `oralexam-network`. Hacker bên ngoài quét IP máy chủ sẽ hoàn toàn không thấy cổng cơ sở dữ liệu, loại bỏ 100% các cuộc tấn công brute-force mật khẩu từ Internet."*
- **Nginx Reverse Proxy & SignalR WebSocket:**
  > *"Nginx 1.27 đóng vai trò cổng chặn duy nhất ở Port 80. Khi sinh viên trả lời vấn đáp, Nginx định tuyến luồng `/hubs/practice` sang Kestrel Backend với cấu hình `Connection: Upgrade` và timeout kéo dài **300 giây (5 phút)**. Chúng em đã kiểm thử thực tế: sinh viên có thể trả lời liên tục mà không bao giờ bị ngắt kết nối WebSocket giữa chừng."*

---

## 4.6. Bộ Thẻ Phản Biện Các Câu Hỏi Hóc Búa Của Hội Đồng (Q&A Battlecard)

Dưới đây là 5 câu hỏi kỹ thuật chuyên sâu mà các Thầy Cô trong Hội đồng thường chất vấn và câu trả lời sắc bén chuẩn kỹ sư của nhóm:

### ❓ Câu hỏi 1: "Tại sao nhóm phải đặt Nginx làm Reverse Proxy phía trước mà không mở thẳng cổng Kestrel của .NET 8 ra ngoài Internet?"
> **Kỹ sư trả lời (Nguyễn Quang Thành):**  
> *"Dạ kính thưa Thầy Cô, việc đặt Nginx phía trước Kestrel mang lại 4 lợi ích sống còn cho môi trường Production:  
> 1. **Bảo mật & Ẩn danh kiến trúc (Defense in Depth):** Nginx che giấu hoàn toàn thông tin chi tiết về Kestrel và phiên bản .NET backend thông qua `server_tokens off;`, đồng thời gắn các Security Headers bắt buộc (`X-Frame-Options`, `X-Content-Type-Options`).  
> 2. **Hiệu năng phục vụ File tĩnh & Gzip:** Nginx phục vụ trực tiếp bundle React SPA (HTML, JS, CSS) với bộ đệm cache 1 năm (`immutable`) và nén gzip cấp độ 6. Kestrel không phải tốn tài nguyên CPU/RAM để phục vụ các file tĩnh này mà dồn 100% tài nguyên cho việc xử lý logic chấm thi và AI.  
> 3. **Single Origin & Tránh lỗi CORS:** Cả giao diện Frontend và API Backend đều phục vụ chung trên cùng một domain/origin (API nằm ở `/api/`, Hub nằm ở `/hubs/`). Điều này loại bỏ hoàn toàn các vấn đề phức tạp và lỗ hổng liên quan đến Cross-Origin Resource Sharing (CORS).  
> 4. **Quản lý quyền Micro (Permissions-Policy):** Nginx thiết lập rõ `Permissions-Policy: microphone=(self)` cho phép máy trạm gọi Web Speech API ghi âm vấn đáp an toàn."*

---

### ❓ Câu hỏi 2: "SignalR sử dụng kết nối WebSocket kéo dài. Nếu proxy qua Nginx, làm thế nào nhóm đảm bảo kết nối không bị đứt giữa chừng khi sinh viên đang trả lời bài thi?"
> **Kỹ sư trả lời (Nguyễn Đăng Hải & Nguyễn Quang Thành):**  
> *"Dạ thưa Thầy Cô, Nginx mặc định có `proxy_read_timeout` là 60 giây. Nếu sinh viên im lặng suy nghĩ hoặc câu trả lời kéo dài quá 60 giây mà không có dữ liệu trao đổi, Nginx mặc định sẽ tự động ngắt kết nối TCP và làm hỏng bài thi!  
> Để giải quyết triệt để vấn đề này, nhóm em đã cấu hình 3 tầng bảo vệ trong `nginx.conf`:  
> 1. Thiết lập `proxy_read_timeout 300s;` và `proxy_send_timeout 300s;` cho riêng khối `location /hubs/`, cho phép kết nối duy trì liên tục trong tối đa 5 phút.  
> 2. Sử dụng cấu trúc ánh xạ chuẩn RFC 6455: `map $http_upgrade $connection_upgrade { default upgrade; '' close; }` cùng với `proxy_set_header Upgrade $http_upgrade` và `proxy_set_header Connection $connection_upgrade` để đảm bảo cơ chế nâng cấp từ HTTP lên giao thức nhị phân WebSocket được giữ nguyên vẹn.  
> 3. Ở phía Backend .NET 8, SignalR Hub duy trì cơ chế `KeepAliveInterval = TimeSpan.FromSeconds(15)`. Mỗi 15 giây, một gói tin ping rỗng được gửi ngầm qua socket, đảm bảo Nginx luôn nhận biết kết nối đang hoạt động (Alive) và không bao giờ bị ngắt."*

---

### ❓ Câu hỏi 3: "Việc nhóm cấu hình `-warnaserror` trong pipeline CI có bị quá khắt khe không? Nếu một thành viên chỉ cần sửa giao diện hoặc thêm chú thích mà dính một cảnh báo nhỏ thì sao?"
> **Kỹ sư trả lời (Nguyễn Trọng Tốt):**  
> *"Dạ thưa Thầy Cô, đây là quyết định kiến trúc có chủ đích của nhóm em nhằm thực thi triết lý **Zero-Defect Delivery**.  
> Trong C# 12 và .NET 8 với tính năng `Nullable Reference Types` được kích hoạt, các cảnh báo như `CS8602` (Possible dereference of a null reference) hoặc `CS8618` (Non-nullable property uninitialized) chính là nguyên nhân hàng đầu dẫn đến lỗi crash hệ thống `NullReferenceException` ngoài môi trường thực tế.  
> Nếu chúng ta nhân nhượng cho phép 'chỉ 1 cảnh báo nhỏ', thì theo thời gian dự án sẽ tích tụ hàng trăm cảnh báo và không ai còn quan tâm sửa chúng nữa. Bằng cách khóa cứng `-warnaserror`, mọi lập trình viên đều có trách nhiệm kiểm tra null chặt chẽ bằng Guard Clauses trước khi gọi phương thức. Kết quả thực tế là toàn bộ 5 projects của backend hiện tại đạt con số **0 Error, 0 Warning** tuyệt đối."*

---

### ❓ Câu hỏi 4: "Cơ sở dữ liệu của nhóm lưu trữ câu hỏi thi và điểm số nhạy cảm. Nhóm bảo vệ PostgreSQL trong container như thế nào nếu máy chủ bị quét cổng?"
> **Kỹ sư trả lời (Nguyễn Đăng Hải):**  
> *"Dạ thưa Thầy Cô, trong tệp `docker-compose.prod.yml`, Thầy Cô có thể thấy service `postgres` hoàn toàn **không khai báo khối `ports:`** (không có cú pháp `5432:5432`).  
> Thay vào đó, service `postgres` chỉ nằm trong mạng nội bộ `networks: - oralexam-network` với driver là `bridge`.  
> Điều này có nghĩa là cổng 5432 chỉ lắng nghe trên giao diện mạng ảo của Docker. Kẻ tấn công quét IP public của máy chủ VPS từ Internet sẽ thấy cổng 5432 bị đóng hoàn toàn (Closed/Filtered). Chỉ có duy nhất container `backend` nằm chung trong mạng `oralexam-network` mới có thể phân giải DNS nội bộ `postgres` và kết nối vào CSDL thông qua mật khẩu entropy cao được cấp qua biến môi trường."*

---

### ❓ Câu hỏi 5: "Nếu một bản cập nhật mới trên nhánh `main` bị lỗi không khởi động được, pipeline CD của nhóm có làm sập toàn bộ hệ thống đang chạy không?"
> **Kỹ sư trả lời (Nguyễn Quang Thành):**  
> *"Dạ thưa Thầy Cô, hệ thống của nhóm em được bảo vệ bởi **cơ chế Healthcheck và Phụ thuộc 2 tầng**:  
> 1. Trong `docker-compose.prod.yml`, mỗi service đều có khối `healthcheck` riêng: `postgres` kiểm tra qua `pg_isready`, `backend` kiểm tra qua `/health`, và `frontend` kiểm tra qua HTTP 200 trang chủ. Service `backend` khai báo `depends_on: postgres: condition: service_healthy`.  
> 2. Khi chạy lệnh `docker compose up -d --remove-orphans`, Docker Compose áp dụng cơ chế khởi động song song: container cũ vẫn tiếp tục tiếp nhận traffic cho đến khi container mới khởi động thành công và vượt qua kiểm tra Healthcheck ban đầu (`start-period`).  
> 3. Nếu container mới bị lỗi (ví dụ không kết nối được database và bị crash), Docker Compose sẽ đánh dấu trạng thái `unhealthy` và pipeline CD của GitHub Actions (`appleboy/ssh-action`) sẽ phát hiện Exit Code khác 0 để gửi cảnh báo thất bại. Nhóm em có sẵn quy trình rollback bằng lệnh `docker compose rollback` hoặc kích hoạt lại image tag ổn định trước đó (`${{ github.sha }}`) chỉ trong 10 giây!"*

---

## TỔNG KẾT & CHỈ SỐ NGHIỆM THU ĐẠT ĐƯỢC

Toàn bộ hạ tầng CI/CD & DevOps của dự án **LLM Oral Exam System (FA26SE166)** đã được chuẩn hóa và sẵn sàng 100% cho ngày bảo vệ chính thức trước Hội đồng chấm tốt nghiệp Đại học FPT:

- ✅ **3 Jobs CI Quality Gate:** Backend (.NET 8, 542 unit tests đỗ 100%, 0 warning), Frontend (Node 22, Oxlint 74ms, `tsc -b` 0 lỗi, Vite bundle), Commitlint (Conventional Commits v1.0.0).
- ✅ **CD Pipeline Tự Động:** Đóng gói Multi-stage Docker Alpine siêu nhẹ, lưu trữ GHCR (`ghcr.io`), tự động hóa triển khai SSH VPS với `appleboy/ssh-action`.
- ✅ **Bảo Mật Container CIS:** Chạy non-root user `appuser` (UID 10001), cô lập mạng nội bộ `oralexam-network`, che giấu hoàn toàn cổng 5432 của PostgreSQL.
- ✅ **Nginx Reverse Proxy Thông Minh:** Hỗ trợ SPA routing fallback, giải quyết triệt để WebSocket SignalR timeout 300s cho kỳ thi vấn đáp, tích hợp đầy đủ Security Headers.
- ✅ **Cẩm Nang & Kịch Bản Bảo Vệ Hoàn Chỉnh:** Hướng dẫn chi tiết 11 GitHub Secrets, hướng dẫn chạy thử nghiệm `act` cục bộ, phân vai 4 kỹ sư và bộ thẻ phản biện 5 câu hỏi xuất sắc trước Hội đồng.
