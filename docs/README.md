# 📚 CỔNG TÀI LIỆU KỸ THUẬT, KIẾN TRÚC & HƯỚNG DẪN TÁC CHIẾN (MASTER ARCHITECTURE & DEV HUB)
## ĐỒ ÁN TỐT NGHIỆP CAPSTONE: FA26SE166 — ĐẠI HỌC FPT TP.HCM (FPT SG)
### Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm
*(An LLM-based Oral Examination Practice and Assessment System for Software Engineering Major)*

---

> [!IMPORTANT]
> **TRUNG TÂM KIẾN TRÚC & TÀI LIỆU HỆ THỐNG (SINGLE SOURCE OF TRUTH - SSOT):**  
> Thư mục này tập hợp đầy đủ 100% tài liệu kiến trúc, hợp đồng API, thiết kế CSDL 30 bảng 3NF, công cụ kiểm thử độc lập và quy trình nghiệp vụ 4 Main Flows để 4 thành viên (**Thành, Tốt, Hải, Hoàng**) dùng làm kim chỉ nam triển khai code, đối chiếu giao tiếp và bảo vệ trước Hội đồng tốt nghiệp FPTU.

---

## 🧭 1. DANH MỤC TÀI LIỆU CỐT LÕI (BẤM ĐỂ MỞ NGAY)

| STT | Tài liệu kỹ thuật | Phạm vi nội dung cốt lõi | Đối tượng sử dụng chính |
|:---:|:---|:---|:---:|
| 1 | [**`MASTER_ARCHITECTURE.md`**](./MASTER_ARCHITECTURE.md) | **Bản kiến trúc tối thượng (8 phần):** Mô hình C4, Kiến trúc 5 tầng chiều dọc, Clean Architecture 4 tầng, Hạ tầng mạng Docker/Nginx, Phòng thủ 4 tầng Zero Data Loss. | Cả 4 thành viên & Hội đồng |
| 2 | [**`MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md`**](./MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) | **Đặc tả chi tiết 4 luồng Main Flows (MF-01 đến MF-04):** Bóc băng Whisper STT (không lưu audio MF-01/02), Buffer Screen hiệu đính Code-Switching 60s, Quota thi thử do Trưởng BM cấu hình, Instant Scorecard (MF-02), Barem Rubric $\sum \equiv 10.0$đ & Trưởng Bộ Môn phê duyệt (MF-03), Kiosk Lockdown phòng Lab, Mic-check 30s $\ge 60$dB, AI chấm ngầm, Hậu kiểm 2 nhóm, Công bố điểm One-Way Lock & Phúc khảo nội bộ Trưởng BM giao GV chấm lại (MF-04). | Hoàng (FE), Thành & Tốt (BE) |
| 3 | [**`ERD_DATABASE_DESIGN.md`**](./ERD_DATABASE_DESIGN.md) | **Thiết kế CSDL 30 bảng PostgreSQL 16 (3NF):** 6 Bounded Contexts, bảng `system_configs`, bảng `notifications`, chuẩn hóa `ip_address`, loại bỏ `password_hash`, từ điển dữ liệu và chỉ mục hiệu năng. | Hải (DB & FE), Thành & Tốt (BE) |
| 4 | [**`API_CONTRACT_AND_INTEGRATION_GUIDE.md`**](./API_CONTRACT_AND_INTEGRATION_GUIDE.md) | **Hợp đồng giao tiếp API Contract-First & WebSocket:** Chi tiết request/response JSON camelCase, mã lỗi RFC 7807 Problem Details, SignalR `/hubs/practice`, Google OAuth PKCE, presigned R2, export Excel/PDF chuẩn FPT. | Hoàng, Hải (FE) & Thành, Tốt (BE) |
| 5 | [**`MF01_Frontend_Integration.md`**](./MF01_Frontend_Integration.md) | **Hướng dẫn tích hợp Frontend cho MF-01:** Đặc tả 7 API endpoints, hook React 19 `usePracticeHub.ts`, logic Buffer Screen 60s, Follow-up Engine đa nấc và hướng dẫn chạy Mini Tester. | Hoàng & Hải (FE) |
| 6 | [**`MF01_Mini_Tester.html`**](./MF01_Mini_Tester.html) | **Công cụ kiểm thử độc lập trực tiếp:** Mở trên trình duyệt Chrome/Edge để test toàn bộ chu trình API + SignalR của MF-01 mà không cần mock. | Hoàng, Hải (FE) & QA |
| 7 | [**`DATABASE_AUDIT_REPORT.md`**](./DATABASE_AUDIT_REPORT.md) | **Báo cáo kiểm định toàn diện CSDL:** Báo cáo thẩm định chuẩn hóa 3NF, tính toàn vẹn khóa ngoại và chiến lược phân vùng/chỉ mục. | Hải & Thành |
| 8 | [**`LOG_THAY_DOI_MAINFLOW.md`**](./LOG_THAY_DOI_MAINFLOW.md) | **Nhật ký thay đổi & Chốt kỹ thuật:** Ghi nhận toàn bộ quyết định kỹ thuật Đợt 1 (Tốt chốt 07/10) và Đợt 2 (Thành chốt 08/10). | Cả 4 thành viên |
| 9 | [**`GIT_AND_TEAM_WORKFLOW.md`**](./GIT_AND_TEAM_WORKFLOW.md) | **Quy trình GitFlow & PR Review:** Quy ước đặt tên nhánh, commit Conventional, quy tắc review duyệt chéo và giải quyết Conflict. | Cả 4 thành viên |
| 10 | [**`MVP_2_WEEKS_MASTER_PLAN.md`**](./MVP_2_WEEKS_MASTER_PLAN.md) | **Kế hoạch tác chiến Sprint MVP 2 tuần:** Kế hoạch phân công 4 Khối chức năng độc lập cho 4 thành viên Thành, Tốt, Hải, Hoàng. | Cả 4 thành viên |

---

## 🖼️ 2. THƯ VIỆN SƠ ĐỒ KỸ THUẬT DRAW.IO ([`diagrams/`](./diagrams/))

> [!TIP]
> Toàn bộ sơ đồ được lưu trữ độc quyền dưới dạng file **`.drawio`** chuẩn hóa. Bạn có thể mở trực tiếp bằng tiện ích mở rộng **Draw.io Integration** trên VS Code hoặc truy cập [app.diagrams.net](https://app.diagrams.net).

- **Sơ đồ Master gộp 4 luồng (4 Tabs):** [CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio](./diagrams/CAPSTONE_FA26SE166_4_MAINFLOWS_ACTIVITY_DIAGRAMS.drawio)
- **MF-01: Luyện tập tự do:** [MF01_Interactive_Practice.drawio](./diagrams/MF01_Interactive_Practice.drawio)
- **MF-02: Thi thử bấm giờ Voice-First:** [MF02_Timed_Mock_Exam.drawio](./diagrams/MF02_Timed_Mock_Exam.drawio)
- **MF-03: Ngân hàng câu hỏi & Barem Rubric 10.0:** [MF03_Question_Bank_Rubric.drawio](./diagrams/MF03_Question_Bank_Rubric.drawio)
- **MF-04: Thi thật phòng Lab Kiosk, Hậu kiểm & Phúc khảo:** [MF04_Lab_Exam_Audit.drawio](./diagrams/MF04_Lab_Exam_Audit.drawio)
- **Kiến trúc hệ thống tổng thể:** [KIEN_TRUC_HE_THONG.drawio](./diagrams/KIEN_TRUC_HE_THONG.drawio)

---

## 👥 3. PHÂN CÔNG TRÁCH NHIỆM 4 THÀNH VIÊN (FA26SE166)

| Thành viên | Vai trò | Trách nhiệm chính |
|:---|:---|:---|
| 🧑 **Nguyễn Quang Thành** | Team Leader & Lead BE Architect | Chủ trì 100% Backend MF-01 (Interactive Practice, Progressive 3–10 câu, BoundedChannel 1000 slots RAM, SignalR `PracticeHub`, SystemConfigs) + Backend MF-02 (Timed Mock Exam, Quota Guard do Trưởng BM cấu hình, Voice-First Gate, Instant Scorecard) + Phân hệ Báo cáo Khảo thí FPT (Excel .xlsx + PDF chữ ký số) + Tích hợp hệ thống tổng thể. |
| 🧑 **Nguyễn Trọng Tốt** | Backend Developer, AI Specialist & QA Lead | Auth Google OAuth PKCE + Backend MF-03 (AI sinh đề FLM Syllabus theo CLO, Barem Rubric 10.0đ, Tick chọn 2 kho, Gửi & Duyệt đề) + TOÀN BỘ Backend MF-04 (Kiosk Check-in IP Binding `ip_address`, Nộp bài Persist First < 100ms, Audio R2 `STT_MSSV.webm` + SHA-256, AI chấm ngầm, OneWayLock, Công bố điểm, Phân hệ Phúc khảo Trưởng BM giao GV chấm lại, AI Doubt Guard, Polly/DLQ) + Backend Semester CRUD / Notification in-app / User Mgmt APIs + Toàn bộ xUnit / NetArchTest suites. |
| 🧑 **Nguyễn Đăng Hải** | DB Specialist & Frontend Developer | Quản trị CSDL 30 bảng PostgreSQL 16 (Schema DDL, Migrations, Seed Data, Docker Compose) — tuyệt đối KHÔNG code logic C# Backend; dồn toàn lực phát triển Frontend (Student Portal Dashboard, Lịch sử luyện tập & thi thử FE-02, Giao diện Thi thử Voice-First MF-02, Giao diện Trưởng BM duyệt đề MF-03, Giao diện Trưởng BM cấu hình ca thi & thẩm định/giao đơn phúc khảo MF-04, User Mgmt FE-09, Semester CRUD FE-08). |
| 🧑 **Lê Vũ Hoàng** | Lead Frontend Architect & Fullstack Coordinator | Kiến trúc Frontend Core (React 19 SPA, Tailwind CSS v4, Router DOM v7, Route Guards 5 roles, Axios Interceptors RFC 7807, Zustand stores) + Giao diện Luyện tập MF-01 (Màn hình đệm 60s, Web Speech API, SignalR hook `usePracticeHub.ts`) + Màn hình Kiosk phòng Lab MF-04 (Fullscreen lockdown, blur $\ge 3$, mic $\ge 60$dB, Upload R2 `STT_MSSV.webm` + SHA-256) + Màn hình Hậu kiểm Evidence Panel cho Giảng viên (Waveform Audio Player, sửa điểm kèm giải trình, Publish Grades) + Auth Google UI + Notification in-app FE-11 + Dashboard FE-10. |

---

## ⚡ 4. BỘ LỆNH MỘT CHẠM DÀNH CHO LẬP TRÌNH VIÊN (ONE-CLICK COMMANDS)

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
