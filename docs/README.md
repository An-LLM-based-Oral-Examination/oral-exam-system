# 📚 CỔNG TÀI LIỆU KỸ THUẬT, KIẾN TRÚC & HƯỚNG DẪN TÁC CHIẾN (MASTER ARCHITECTURE & DEV HUB)
## ĐỒ ÁN TỐT NGHIỆP CAPSTONE: FA26SE166 — ĐẠI HỌC FPT TP.HCM (FPT SG)
### Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm
*(An LLM-based Oral Examination Practice and Assessment System for Software Engineering Major)*

---

> [!IMPORTANT]
> **TRUNG TÂM KIẾN TRÚC & TÀI LIỆU HỆ THỐNG (SINGLE SOURCE OF TRUTH - SSOT):**  
> Toàn bộ tài liệu kiến trúc, đề xuất kỹ thuật R1–R8, hợp đồng API, thiết kế CSDL 38 bảng 3NF (7 Bounded Contexts), công cụ kiểm thử độc lập và quy trình nghiệp vụ 4 Main Flows (`MF-01..MF-04`) + Ca thi trong lớp (`MF-05`) với 6 Roles RBAC được quy hoạch chuẩn hóa vào **7 phân nhóm thư mục chuyên đề** dưới đây để 4 thành viên (**Thành, Tốt, Hải, Hoàng**) dùng làm kim chỉ nam tác chiến và bảo vệ tốt nghiệp FPTU.

---

## 🧭 BẢN ĐỒ ĐIỀU HƯỚNG TÀI LIỆU (7 PHÂN NHÓM CHUYÊN ĐỀ)

```text
docs/
├── 01_architecture/         # Kiến trúc hệ thống Clean Architecture, Đề xuất kỹ thuật R1-R8 & Đặc tả luồng nghiệp vụ
├── 02_database/             # Thiết kế CSDL 38 bảng PostgreSQL 16 (3NF) & EF Core Code-First
├── 03_api_and_contracts/    # Hợp đồng API Contract-First, SignalR & Hướng dẫn tích hợp Frontend
├── 04_devops_and_workflow/  # Hướng dẫn DevOps, Docker Compose, CI/CD Pipeline & Quy trình Git
├── 05_diagrams/             # Toàn bộ thư viện sơ đồ luồng Draw.io master và chi tiết
├── 06_testing_tools/        # Bộ công cụ kiểm thử độc lập trực tiếp (HTML Mini-Testers)
├── 07_reports_and_plans/    # Kế hoạch tác chiến Sprint MVP, Hồ sơ Review 2 & Bảng phân công
└── README.md                # Trang chủ điều hướng tài liệu toàn hệ thống
```

---

### 🏛️ Nhóm 1: Kiến Trúc Hệ Thống & Đặc Tả Nghiệp Vụ Cốt Lõi ([`01_architecture/`](./01_architecture/))
| STT | Tài liệu kỹ thuật | Phạm vi nội dung cốt lõi | Đối tượng sử dụng chính |
|:---:|:---|:---|:---:|
| 1 | [**`MASTER_ARCHITECTURE.md`**](./01_architecture/MASTER_ARCHITECTURE.md) | **Bản kiến trúc tối thượng:** Mô hình C4, Kiến trúc 5 tầng chiều dọc, Clean Architecture 4 tầng, 6 Roles RBAC, Hạ tầng mạng Docker/Nginx, Phòng thủ 4 tầng Zero Data Loss (DLQ phân định Admin cho MF-01/02 & Khảo thí cho MF-04). | Cả 4 thành viên & Hội đồng |
| 2 | [**`TECHNICAL_DESIGN_PROPOSAL_R1_R8.md`**](./01_architecture/TECHNICAL_DESIGN_PROPOSAL_R1_R8.md) | **Đề xuất thiết kế kỹ thuật R1–R8:** DDL PostgreSQL 16 chuẩn 3NF, Hợp đồng RESTful API RFC 7807, Ma trận phân quyền 6 Roles, Chuỗi học thuật 4 cấp (`course_lecturers`), Ca thi trong lớp mật khẩu xoay vòng 5 phút (`in_class_exam_shifts`), Phúc khảo 5 bước qua Khảo thí và Phân tách 2 luồng tạo câu hỏi. | Cả 4 thành viên & Hội đồng |
| 3 | [**`MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md`**](./01_architecture/MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) | **Đặc tả chi tiết 4 luồng Main Flows (MF-01 đến MF-04) & Ca thi trong lớp (MF-05):** Bóc băng Whisper STT (không lưu audio MF-01/02/05), Buffer Screen 60s, Quota thi thử do Admin cấu hình, Phân tách 2 luồng tạo câu hỏi (MF-03), Kiosk Lockdown phòng Lab, Proctor chỉ điểm danh & báo cáo vi phạm, Trưởng BM độc quyền cấu hình Follow-up thi thật, Khảo thí công bố điểm One-Way Lock & Phúc khảo 5 bước (MF-04). | Hoàng (FE), Thành & Tốt (BE) |

---

### 💾 Nhóm 2: Cơ Sở Dữ Liệu PostgreSQL 16 (38 Bảng 3NF) ([`02_database/`](./02_database/))
| STT | Tài liệu kỹ thuật | Phạm vi nội dung cốt lõi | Đối tượng sử dụng chính |
|:---:|:---|:---|:---:|
| 4 | [**`ERD_DATABASE_DESIGN.md`**](./02_database/ERD_DATABASE_DESIGN.md) | **Thiết kế CSDL 38 bảng PostgreSQL 16 (3NF):** 7 Bounded Contexts, bảng `course_clos`, `course_lecturers`, 4 bảng ca thi trong lớp (`in_class_exam_shifts`, `in_class_shift_passwords`, `in_class_shift_participants`, `in_class_exam_submissions`), `exam_anti_cheat_logs`, `system_configs`, `refresh_tokens`, chuẩn hóa `ip_address`, Dual Auth, từ điển dữ liệu và chỉ mục ESR. | Hải (DB & FE), Thành & Tốt (BE) |

---

### 🔌 Nhóm 3: Hợp Đồng Giao Tiếp API & Tích Hợp Frontend ([`03_api_and_contracts/`](./03_api_and_contracts/))
| STT | Tài liệu kỹ thuật | Phạm vi nội dung cốt lõi | Đối tượng sử dụng chính |
|:---:|:---|:---|:---:|
| 5 | [**`API_CONTRACT_AND_INTEGRATION_GUIDE.md`**](./03_api_and_contracts/API_CONTRACT_AND_INTEGRATION_GUIDE.md) | **Hợp đồng giao tiếp API Contract-First & WebSocket:** Chi tiết request/response JSON camelCase, mã lỗi RFC 7807 Problem Details, SignalR `/hubs/practice` & `/hubs/anti-cheat`, Dual Auth, presigned R2, export Excel/PDF chuẩn FPT. | Hoàng, Hải (FE) & Thành, Tốt (BE) |
| 6 | [**`MF01_Frontend_Integration.md`**](./03_api_and_contracts/MF01_Frontend_Integration.md) | **Hướng dẫn tích hợp Frontend cho MF-01:** Đặc tả 7 API endpoints, hook React 19 `usePracticeHub.ts`, logic Buffer Screen 60s, Follow-up Engine đa nấc và hướng dẫn chạy Mini Tester. | Hoàng & Hải (FE) |
| 7 | [**`MF02_Frontend_Integration.md`**](./03_api_and_contracts/MF02_Frontend_Integration.md) | **Hướng dẫn tích hợp Frontend cho MF-02 (Thi thử bấm giờ):** Đặc tả 6 API endpoints (Quota Guard, Start Session, Voice-First Answer, Instant Scorecard, Lịch sử), mã lỗi RFC 7807 (429, 410) và code mẫu MediaRecorder. | Hải & Hoàng (FE) |
| 8 | [**`FE_GOOGLE_AUTH_INTEGRATION_GUIDE.md`**](./03_api_and_contracts/FE_GOOGLE_AUTH_INTEGRATION_GUIDE.md) | **Hướng dẫn tích hợp Google OAuth 2.0 & Dual Auth:** Đăng nhập bằng Google ID Token hoặc tài khoản trường cấp, Silent Refresh Token Rotation, thu hồi token khi đăng xuất và cấu hình Client ID. | Hoàng & Hải (FE) |

---

### ⚙️ Nhóm 4: Vận Hành, CI/CD Pipeline & Quy Trình Git ([`04_devops_and_workflow/`](./04_devops_and_workflow/))
| STT | Tài liệu kỹ thuật | Phạm vi nội dung cốt lõi | Đối tượng sử dụng chính |
|:---:|:---|:---|:---:|
| 9 | [**`CICD_AND_DEVOPS_GUIDE.md`**](./04_devops_and_workflow/CICD_AND_DEVOPS_GUIDE.md) | **Hướng dẫn DevOps & Containerization:** Multi-stage Dockerfile cho Backend (.NET 8) và Frontend (Vite/Nginx), Docker Compose Production, Enterprise GitHub Actions CI/CD Pipeline. | Cả 4 thành viên |
| 10 | [**`GIT_AND_TEAM_WORKFLOW.md`**](./04_devops_and_workflow/GIT_AND_TEAM_WORKFLOW.md) | **Quy trình GitFlow & PR Review:** Quy ước đặt tên nhánh, commit Conventional, quy tắc review duyệt chéo và giải quyết Conflict. | Cả 4 thành viên |

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
| 11 | [**`MF01_Mini_Tester.html`**](./06_testing_tools/MF01_Mini_Tester.html) | HTML / JS độc lập | Mở trực tiếp trên Chrome/Edge để test toàn bộ chu trình MF-01 (Khởi tạo phiên, Whisper STT, Buffer Screen 60s, SignalR chấm ngầm, Follow-up A2, Lịch sử) không cần mock. |
| 12 | [**`google-id-token.html`**](./06_testing_tools/google-id-token.html) | HTML / JS độc lập | Mở trực tiếp trên trình duyệt để đăng nhập Google và copy Google ID Token phục vụ test API Auth trên Swagger hoặc Postman. |

---

### 📅 Nhóm 7: Kế Hoạch Tác Chiến, Bảng Phân Công & Báo Cáo Tiến Độ ([`07_reports_and_plans/`](./07_reports_and_plans/))
- **Bảng Phân Công & Tiến Độ Backend (Master v9.0):** [BE_TASK_BOARD.md](./07_reports_and_plans/BE_TASK_BOARD.md)
- **Bảng Phân Công & Tiến Độ Frontend (Master v9.0):** [FE_TASK_BOARD.md](./07_reports_and_plans/week5/FE_TASK_BOARD.md)
- **Kế hoạch Sprint MVP 2 tuần:** [MVP_2_WEEKS_MASTER_PLAN.md](./07_reports_and_plans/MVP_2_WEEKS_MASTER_PLAN.md)
- **Nhật ký công việc Tuần 5:** [Task Tuần 5 _ Report 1,2.md](./07_reports_and_plans/Task%20Tu%E1%BA%A7n%205%20_%20Report%201,2.md)
- **Báo cáo Report 2 (Markdown & Diagram):** [Thanh_Report2.md](./07_reports_and_plans/week5/Thanh_Report2.md) kèm sơ đồ quy trình [figure_2_2_1_project_process.svg](./07_reports_and_plans/week5/figure_2_2_1_project_process.svg)

---

## 👥 PHÂN CÔNG TRÁCH NHIỆM 4 THÀNH VIÊN (FA26SE166)

| Thành viên | Vai trò | Trách nhiệm chính |
|:---|:---|:---|
| 🧑 **Nguyễn Quang Thành** | Team Leader & Lead BE Architect | Chủ trì 100% Backend MF-01 (Interactive Practice, Progressive 3–10 câu, Timeout 5 phút trả HTTP 410 Gone, BoundedChannel 1000 slots RAM, SignalR `PracticeHub`, SystemConfigs) + Backend MF-02 (Timed Mock Exam, Quota Guard do Admin cấu hình, Voice-First Gate, Instant Scorecard) + Phân hệ Báo cáo Khảo thí FPT (Excel .xlsx + PDF chữ ký số) + Tích hợp hệ thống tổng thể. |
| 🧑 **Nguyễn Trọng Tốt** | Backend Developer, AI Specialist & QA Lead | Dual Auth (Google OAuth 2.0 + Tài khoản trường cấp cho 6 roles) + Backend MF-03 (Phân tách 2 luồng câu hỏi R8: `practice_questions` độc lập từ CLO 3NF `course_clos` & `exam_questions` theo ma trận đề mẫu của Trưởng BM, Barem Rubric 10.0đ, đính kèm ảnh Multimodal AI, Gửi & Duyệt đề) + Backend MF-04 & MF-05 (Kiosk Check-in `ip_address`, Proctor điểm danh & ghi `exam_anti_cheat_logs`, Persist First < 100ms, Audio R2 `STT_MSSV.webm` + SHA-256, AI chấm ngầm, OneWayLock, Công bố điểm, Quy trình Phúc khảo 5 bước qua Khảo thí $\to$ Trưởng BM $\to$ GV chấm lại, Phân định DLQ Admin MF-01/02 & Khảo thí MF-04, Chuỗi học thuật 4 cấp `course_lecturers` & Ca thi trong lớp mật khẩu xoay vòng 5 phút `in_class_exam_shifts`) + Toàn bộ xUnit / NetArchTest suites. |
| 🧑 **Phạm Nguyễn Đăng Hải** | DB Specialist & Frontend Developer | Quản trị CSDL 38 bảng PostgreSQL 16 chuẩn 3NF (Schema DDL, Migrations, Seed Data, Docker Compose) — tuyệt đối KHÔNG code logic C# Backend; dồn toàn lực phát triển Frontend (Student Portal Dashboard, Lịch sử luyện tập & thi thử FE-02, Giao diện Thi thử Voice-First MF-02, Giao diện Trưởng BM duyệt đề MF-03, Giao diện Khảo thí & Trưởng BM quản lý kỳ thi/ca thi/follow-up, giám sát DLQ & quy trình phúc khảo 5 bước MF-04, User Mgmt FE-09, Semester/Academic CRUD FE-08). |
| 🧑 **Lê Vũ Hoàng** | Lead Frontend Architect & Fullstack Coordinator | Kiến trúc Frontend Core (React 19 SPA, Tailwind CSS v4, Router DOM v7, Route Guards 6 roles, Axios Interceptors RFC 7807, Zustand stores) + Giao diện Luyện tập MF-01 (Màn hình đệm 60s, Web Speech API, SignalR hook `usePracticeHub.ts`) + Giao diện Lớp học & Ca thi trong lớp MF-05 (Mật khẩu xoay vòng 5 phút TOTP) + Màn hình Kiosk phòng Lab MF-04 (Fullscreen lockdown, blur > 3, mic $\ge 60$dB, Upload R2 `STT_MSSV.webm` + SHA-256) + Màn hình Hậu kiểm Evidence Panel cho Giảng viên (Waveform Audio Player, sửa điểm kèm giải trình, Publish Grades) + Dual Auth UI + Notification in-app FE-11 + Dashboard FE-10. |

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
