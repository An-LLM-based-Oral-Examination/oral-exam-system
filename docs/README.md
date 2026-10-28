# 📚 CỔNG TÀI LIỆU KỸ THUẬT, KIẾN TRÚC & HƯỚNG DẪN TÁC CHIẾN (MASTER ARCHITECTURE & DEV HUB)
## ĐỒ ÁN TỐT NGHIỆP CAPSTONE: FA26SE166 — ĐẠI HỌC FPT TP.HCM (FPT SG)
### Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm
*(An LLM-based Oral Examination Practice and Assessment System for Software Engineering Major)*

---

> [!IMPORTANT]
> **TRUNG TÂM KIẾN TRÚC & TÀI LIỆU HỆ THỐNG (SINGLE SOURCE OF TRUTH - SSOT):**  
> Toàn bộ tài liệu kiến trúc, hợp đồng API, thiết kế CSDL 30 bảng 3NF, công cụ kiểm thử độc lập và quy trình nghiệp vụ 4 Main Flows được quy hoạch chuẩn hóa vào **7 phân nhóm thư mục chuyên đề** dưới đây để 4 thành viên (**Thành, Tốt, Hải, Hoàng**) dùng làm kim chỉ nam tác chiến và bảo vệ tốt nghiệp FPTU.

---

## 🧭 BẢN ĐỒ ĐIỀU HƯỚNG TÀI LIỆU (7 PHÂN NHÓM CHUYÊN ĐỀ)

```text
docs/
├── 01_architecture/         # Kiến trúc hệ thống Clean Architecture & Đặc tả 4 luồng nghiệp vụ
├── 02_database/             # Thiết kế CSDL 30 bảng PostgreSQL 16 (3NF) & EF Core Code-First
├── 03_api_and_contracts/    # Hợp đồng API Contract-First, SignalR & Hướng dẫn tích hợp Frontend
├── 04_devops_and_workflow/  # Hướng dẫn DevOps, Docker Compose, CI/CD Pipeline & Quy trình Git
├── 05_diagrams/             # Toàn bộ thư viện sơ đồ luồng Draw.io master và chi tiết
├── 06_testing_tools/        # Bộ công cụ kiểm thử độc lập trực tiếp (HTML Mini-Testers)
├── 07_reports_and_plans/    # Kế hoạch tác chiến Sprint MVP 2 tuần & Báo cáo tiến độ
└── README.md                # Trang chủ điều hướng tài liệu toàn hệ thống
```

---

### 🏛️ Nhóm 1: Kiến Trúc Hệ Thống & Đặc Tả Nghiệp Vụ Cốt Lõi ([`01_architecture/`](./01_architecture/))
| STT | Tài liệu kỹ thuật | Phạm vi nội dung cốt lõi | Đối tượng sử dụng chính |
|:---:|:---|:---|:---:|
| 1 | [**`MASTER_ARCHITECTURE.md`**](./01_architecture/MASTER_ARCHITECTURE.md) | **Bản kiến trúc tối thượng (8 phần):** Mô hình C4, Kiến trúc 5 tầng chiều dọc, Clean Architecture 4 tầng, Hạ tầng mạng Docker/Nginx, Phòng thủ 4 tầng Zero Data Loss. | Cả 4 thành viên & Hội đồng |
| 2 | [**`MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md`**](./01_architecture/MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) | **Đặc tả chi tiết 4 luồng Main Flows (MF-01 đến MF-04):** Bóc băng Whisper STT (không lưu audio MF-01/02), Buffer Screen hiệu đính Code-Switching 60s, Quota thi thử do Admin cấu hình, Instant Scorecard (MF-02), Barem Rubric $\sum \equiv 10.0$đ & Trưởng Bộ Môn phê duyệt (MF-03), Kiosk Lockdown phòng Lab, Mic-check 30s $\ge 60$dB, AI chấm ngầm, Hậu kiểm 2 nhóm, Công bố điểm One-Way Lock & Phúc khảo nội bộ Trưởng BM giao GV chấm lại (MF-04). | Hoàng (FE), Thành & Tốt (BE) |

---

### 💾 Nhóm 2: Cơ Sở Dữ Liệu PostgreSQL 16 (30 Bảng 3NF) ([`02_database/`](./02_database/))
| STT | Tài liệu kỹ thuật | Phạm vi nội dung cốt lõi | Đối tượng sử dụng chính |
|:---:|:---|:---|:---:|
| 3 | [**`ERD_DATABASE_DESIGN.md`**](./02_database/ERD_DATABASE_DESIGN.md) | **Thiết kế CSDL 30 bảng PostgreSQL 16 (3NF):** 6 Bounded Contexts, bảng `system_configs`, bảng `refresh_tokens`, chuẩn hóa `ip_address`, xác thực kép Dual Auth (Google OAuth + `password_hash` trường cấp), từ điển dữ liệu và chỉ mục ESR. Nguồn sự thật duy nhất qua EF Core Code-First (`OralExamDbContext.cs` và `Persistence/Migrations/`). | Hải (DB & FE), Thành & Tốt (BE) |

---

### 🔌 Nhóm 3: Hợp Đồng Giao Tiếp API & Tích Hợp Frontend ([`03_api_and_contracts/`](./03_api_and_contracts/))
| STT | Tài liệu kỹ thuật | Phạm vi nội dung cốt lõi | Đối tượng sử dụng chính |
|:---:|:---|:---|:---:|
| 4 | [**`API_CONTRACT_AND_INTEGRATION_GUIDE.md`**](./03_api_and_contracts/API_CONTRACT_AND_INTEGRATION_GUIDE.md) | **Hợp đồng giao tiếp API Contract-First & WebSocket:** Chi tiết request/response JSON camelCase, mã lỗi RFC 7807 Problem Details, SignalR `/hubs/practice`, Dual Auth (Google OAuth 2.0 + Username/Password trường cấp), presigned R2, export Excel/PDF chuẩn FPT. | Hoàng, Hải (FE) & Thành, Tốt (BE) |
| 5 | [**`MF01_Frontend_Integration.md`**](./03_api_and_contracts/MF01_Frontend_Integration.md) | **Hướng dẫn tích hợp Frontend cho MF-01:** Đặc tả 7 API endpoints, hook React 19 `usePracticeHub.ts`, logic Buffer Screen 60s, Follow-up Engine đa nấc và hướng dẫn chạy Mini Tester. | Hoàng & Hải (FE) |
| 6 | [**`MF02_Frontend_Integration.md`**](./03_api_and_contracts/MF02_Frontend_Integration.md) | **Hướng dẫn tích hợp Frontend cho MF-02 (Thi thử bấm giờ):** Đặc tả 6 API endpoints (Quota Guard, Start Session, Voice-First Answer, Instant Scorecard, Lịch sử), mã lỗi RFC 7807 (429, 410) và code mẫu MediaRecorder. | Hải & Hoàng (FE) |
| 7 | [**`FE_GOOGLE_AUTH_INTEGRATION_GUIDE.md`**](./03_api_and_contracts/FE_GOOGLE_AUTH_INTEGRATION_GUIDE.md) | **Hướng dẫn tích hợp Google OAuth 2.0 & Dual Auth:** Đăng nhập bằng Google ID Token hoặc tài khoản trường cấp, Silent Refresh Token Rotation, thu hồi token khi đăng xuất và cấu hình Client ID. | Hoàng & Hải (FE) |

---

### ⚙️ Nhóm 4: Vận Hành, CI/CD Pipeline & Quy Trình Git ([`04_devops_and_workflow/`](./04_devops_and_workflow/))
| STT | Tài liệu kỹ thuật | Phạm vi nội dung cốt lõi | Đối tượng sử dụng chính |
|:---:|:---|:---|:---:|
| 8 | [**`CICD_AND_DEVOPS_GUIDE.md`**](./04_devops_and_workflow/CICD_AND_DEVOPS_GUIDE.md) | **Hướng dẫn DevOps & Containerization:** Multi-stage Dockerfile cho Backend (.NET 8) và Frontend (Vite/Nginx), Docker Compose Production, Enterprise GitHub Actions CI/CD Pipeline. | Cả 4 thành viên |
| 9 | [**`GIT_AND_TEAM_WORKFLOW.md`**](./04_devops_and_workflow/GIT_AND_TEAM_WORKFLOW.md) | **Quy trình GitFlow & PR Review:** Quy ước đặt tên nhánh, commit Conventional, quy tắc review duyệt chéo và giải quyết Conflict. | Cả 4 thành viên |

---

### 🖼️ Nhóm 5: Thư Viện Sơ Đồ Kỹ Thuật Draw.io ([`05_diagrams/`](./05_diagrams/))
> [!TIP]
> Toàn bộ sơ đồ được lưu trữ độc quyền dưới dạng file **`.drawio`** chuẩn hóa. Bạn có thể mở trực tiếp bằng tiện ích mở rộng **Draw.io Integration** trên VS Code hoặc truy cập [app.diagrams.net](https://app.diagrams.net).

- **Sơ đồ Master gộp 4 luồng (4 Tabs):** [CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio](./05_diagrams/CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio)
- **MF-01: Luyện tập tự do:** [MF01_Interactive_Practice.drawio](./05_diagrams/MF01_Interactive_Practice.drawio)
- **MF-02: Thi thử bấm giờ Voice-First:** [MF02_Timed_Mock_Exam.drawio](./05_diagrams/MF02_Timed_Mock_Exam.drawio)
- **MF-03: Ngân hàng câu hỏi & Barem Rubric 10.0:** [MF03_Question_Bank_Rubric.drawio](./05_diagrams/MF03_Question_Bank_Rubric.drawio)
- **MF-04: Thi thật phòng Lab Kiosk, Hậu kiểm & Phúc khảo:** [MF04_Lab_Exam_Audit.drawio](./05_diagrams/MF04_Lab_Exam_Audit.drawio)
- **Kiến trúc hệ thống tổng thể:** [KIEN_TRUC_HE_THONG.drawio](./05_diagrams/KIEN_TRUC_HE_THONG.drawio)

---

### 🛠️ Nhóm 6: Bộ Công Cụ Kiểm Thử Độc Lập Trực Tiếp ([`06_testing_tools/`](./06_testing_tools/))
| STT | Công cụ | Định dạng | Mô tả chức năng |
|:---:|:---|:---|:---|
| 10 | [**`MF01_Mini_Tester.html`**](./06_testing_tools/MF01_Mini_Tester.html) | HTML / JS độc lập | Mở trực tiếp trên Chrome/Edge để test toàn bộ chu trình MF-01 (Khởi tạo phiên, Whisper STT, Buffer Screen 60s, SignalR chấm ngầm, Follow-up A2, Lịch sử) không cần mock. |
| 11 | [**`google-id-token.html`**](./06_testing_tools/google-id-token.html) | HTML / JS độc lập | Mở trực tiếp trên trình duyệt để đăng nhập Google và copy Google ID Token phục vụ test API Auth trên Swagger hoặc Postman. |

---

### 📅 Nhóm 7: Kế Hoạch Tác Chiến, Bảng Phân Công & Báo Cáo Tiến Độ ([`07_reports_and_plans/`](./07_reports_and_plans/))
- **Bảng Phân Công & Tiến Độ Backend (Master v8.0):** [BE_TASK_BOARD.md](./07_reports_and_plans/BE_TASK_BOARD.md)
- **Bảng Phân Công & Tiến Độ Frontend (Master v8.0):** [FE_TASK_BOARD.md](./07_reports_and_plans/week5/FE_TASK_BOARD.md)
- **Kế hoạch Sprint MVP 2 tuần:** [MVP_2_WEEKS_MASTER_PLAN.md](./07_reports_and_plans/MVP_2_WEEKS_MASTER_PLAN.md)
- **Nhật ký công việc Tuần 5:** [Task Tuần 5 _ Report 1,2.md](./07_reports_and_plans/Task%20Tu%E1%BA%A7n%205%20_%20Report%201,2.md)
- **Báo cáo Report 2 (Markdown & Diagram):** [Thanh_Report2.md](./07_reports_and_plans/week5/Thanh_Report2.md) kèm sơ đồ quy trình [figure_2_2_1_project_process.svg](./07_reports_and_plans/week5/figure_2_2_1_project_process.svg)

---

## 👥 PHÂN CÔNG TRÁCH NHIỆM 4 THÀNH VIÊN (FA26SE166)

| Thành viên | Vai trò | Trách nhiệm chính |
|:---|:---|:---|
| 🧑 **Nguyễn Quang Thành** | Team Leader & Lead BE Architect | Chủ trì 100% Backend MF-01 (Interactive Practice, Progressive 3–10 câu, Timeout 5 phút trả HTTP 410 Gone, BoundedChannel 1000 slots RAM, SignalR `PracticeHub`, SystemConfigs) + Backend MF-02 (Timed Mock Exam, Quota Guard do Admin cấu hình, Voice-First Gate, Instant Scorecard) + Phân hệ Báo cáo Khảo thí FPT (Excel .xlsx + PDF chữ ký số) + Tích hợp hệ thống tổng thể. |
| 🧑 **Nguyễn Trọng Tốt** | Backend Developer, AI Specialist & QA Lead | Dual Auth (Google OAuth 2.0 + Tài khoản trường cấp) + Backend MF-03 (AI sinh đề FLM Syllabus theo CLO, soạn đề thủ công đính kèm ảnh/sơ đồ kỹ thuật `PNG/JPG/WEBP <= 5MB` chấm Multimodal AI, Barem Rubric 10.0đ, Tick chọn 2 kho, Gửi & Duyệt đề) + TOÀN BỘ Backend MF-04 (Kiosk Check-in IP Binding `ip_address`, Nộp bài Persist First < 100ms, Audio R2 `STT_MSSV.webm` + SHA-256, AI chấm ngầm, OneWayLock, Công bố điểm, Phân hệ Phúc khảo Trưởng BM giao GV chấm lại, AI Doubt Guard, Polly/DLQ do Trưởng BM giám sát & replay) + Backend Semester CRUD / Notification in-app / User Mgmt APIs + Toàn bộ xUnit / NetArchTest suites. |
| 🧑 **Phạm Nguyễn Đăng Hải** | DB Specialist & Frontend Developer | Quản trị CSDL 30 bảng PostgreSQL 16 (Schema DDL, Migrations, Seed Data, Docker Compose) — tuyệt đối KHÔNG code logic C# Backend; dồn toàn lực phát triển Frontend (Student Portal Dashboard, Lịch sử luyện tập & thi thử FE-02, Giao diện Thi thử Voice-First MF-02, Giao diện Trưởng BM duyệt đề MF-03, Giao diện Trưởng BM cấu hình ca thi, giám sát DLQ & thẩm định/giao đơn phúc khảo MF-04, User Mgmt FE-09, Semester CRUD FE-08). |
| 🧑 **Lê Vũ Hoàng** | Lead Frontend Architect & Fullstack Coordinator | Kiến trúc Frontend Core (React 19 SPA, Tailwind CSS v4, Router DOM v7, Route Guards 5 roles, Axios Interceptors RFC 7807, Zustand stores) + Giao diện Luyện tập MF-01 (Màn hình đệm 60s, Web Speech API, SignalR hook `usePracticeHub.ts`) + Màn hình Kiosk phòng Lab MF-04 (Fullscreen lockdown, blur $\ge 3$, mic $\ge 60$dB, Upload R2 `STT_MSSV.webm` + SHA-256) + Màn hình Hậu kiểm Evidence Panel cho Giảng viên (Waveform Audio Player, sửa điểm kèm giải trình, Publish Grades) + Dual Auth UI + Notification in-app FE-11 + Dashboard FE-10. |

---

## ⚡ BỘ LỆNH MỘT CHẠM DÀNH CHO LẬP TRÌNH VIÊN (ONE-CLICK COMMANDS)

```powershell
# 1. Khởi động Cơ sở dữ liệu PostgreSQL 16 bằng Docker (tại thư mục 05_Source_Code)
docker compose up -d

# 2. Biên dịch Backend Solution (.NET 8 Clean Architecture)
dotnet build backend

# 3. Chạy Unit Tests Backend
dotnet test backend --verbosity normal

# 4. Biên dịch Frontend (React 19 Vite)
cd frontend; npm run build
```
