# 📚 CỔNG TÀI LIỆU KỸ THUẬT, KIẾN TRÚC & HƯỚNG DẪN TÁC CHIẾN (MASTER ARCHITECTURE & DEV HUB)
## ĐỒ ÁN TỐT NGHIỆP CAPSTONE: FA26SE166 — ĐẠI HỌC FPT TP.HCM (FPT SG)
### Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm
*(An LLM-based Oral Examination Practice and Assessment System for Software Engineering Major)*

---

> [!IMPORTANT]
> **TRUNG TÂM KIẾN TRÚC & TÀI LIỆU HỆ THỐNG (SINGLE SOURCE OF TRUTH - SSOT):**  
> Thư mục này tập hợp đầy đủ 100% tài liệu kiến trúc, hợp đồng API, thiết kế CSDL 28 bảng và quy trình nghiệp vụ 4 Main Flows để 4 thành viên (**Thành, Tốt, Hải, Hoàng**) dùng làm kim chỉ nam triển khai code, đối chiếu giao tiếp và bảo vệ trước Hội đồng tốt nghiệp FPTU.

---

## 🧭 1. DANH MỤC TÀI LIỆU CỐT LÕI (BẤM ĐỂ MỞ NGAY)

| STT | Tài liệu kỹ thuật | Phạm vi nội dung cốt lõi | Đối tượng sử dụng chính |
|:---:|:---|:---|:---:|
| 1 | [**`MASTER_ARCHITECTURE.md`**](./MASTER_ARCHITECTURE.md) | **Bản kiến trúc tối thượng (8 phần):** Mô hình C4, Kiến trúc 5 tầng chiều dọc, Clean Architecture 4 tầng, Hạ tầng mạng Docker/Nginx, Phòng thủ 4 tầng Zero Data Loss. | Cả 4 thành viên & Hội đồng |
| 2 | [**`MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md`**](./MO_TA_4_LUONG_MAINFLOW_NGHIEP_VU.md) | **Đặc tả chi tiết 4 luồng Main Flows (MF-01 đến MF-04):** Buffer Screen hiệu đính Code-Switching (cấu hình động môn học 10–300s, default 60s), Quota Guard $K=3$, Instant Feedback Scorecard chi tiết & Lịch sử thi (MF-02), Barem Rubric $\sum = 10.0$đ, Giảng viên sử dụng AI sinh câu hỏi theo barem của mình & Gửi Bộ Môn duyệt (MF-03), Kiosk Lockdown, Mic-check 30s $\ge 60$dB, Cấu hình ExamInputMode, Kiosk khóa an toàn, AI chấm điểm ngầm, Chốt chặn AI Doubt Guard phân loại 2 nhóm, Cổng Hậu kiểm Giảng viên Waveform Player, Giảng viên Công bố điểm (Publish Grades) khi đủ 100% sinh viên, One-Way Lock HTTP 403, Sinh viên xem điểm Student Portal & Nộp đơn phúc khảo nội bộ (AppealRequest) gán Trưởng Bộ Môn thẩm định (MF-04). | Hoàng (FE), Thành & Tốt (BE) |
| 3 | [**`ERD_DATABASE_DESIGN.md`**](./ERD_DATABASE_DESIGN.md) | **Thiết kế CSDL 28 bảng PostgreSQL 16 (3NF):** Bóc tách hoàn toàn Practice vs Exam, từ điển dữ liệu 28 bảng, 6 Bounded Contexts, khóa ngoại, chỉ mục hiệu năng. | Hải (DB & FE) & Thành (BE) |
| 4 | [**`API_CONTRACT_AND_INTEGRATION_GUIDE.md`**](./API_CONTRACT_AND_INTEGRATION_GUIDE.md) | **Hợp đồng giao tiếp API Contract-First & WebSocket:** Chi tiết request/response JSON camelCase, mã lỗi RFC 7807, SignalR `/hubs/practice`, FPT Google OAuth PKCE, presigned R2, export Excel chuẩn FPT. | Hoàng, Hải (FE) & Thành, Tốt (BE) |
| 5 | [**`GIT_AND_TEAM_WORKFLOW.md`**](./GIT_AND_TEAM_WORKFLOW.md) | **Quy trình GitFlow & PR Review:** Quy ước đặt tên nhánh, commit Conventional, quy tắc bắt buộc review duyệt chéo và giải quyết Conflict. | Cả 4 thành viên |
| 6 | [**`MVP_2_WEEKS_MASTER_PLAN.md`**](./MVP_2_WEEKS_MASTER_PLAN.md) | **Kế hoạch tác chiến Sprint MVP 2 tuần:** Lịch review Thứ 3 - Thứ 5 - Thứ 7, tiêu chí nghiệm thu 2 Gate, phân bổ nhiệm vụ 25% cho Thành, Tốt, Hải, Hoàng. | Cả 4 thành viên |

---

## 🖼️ 2. THƯ VIỆN SƠ ĐỒ & GIAO DIỆN XEM TRỰC QUAN ([`diagrams/`](./diagrams/))

> [!TIP]
> Bạn có thể mở trực tiếp file [**`diagrams/XEM_4_MAINFLOWS_TRUC_TIEP.html`**](./diagrams/XEM_4_MAINFLOWS_TRUC_TIEP.html) bằng bất kỳ trình duyệt web nào (Chrome, Edge) để xem giao diện tab chuyển đổi mượt mà 4 luồng nghiệp vụ với sơ đồ độ nét cực cao!

- **MF-01: Luyện tập tự do:** [Sơ đồ PNG](./diagrams/MF01_Interactive_Practice.png) | [Sơ đồ SVG Vector](./diagrams/MF01_Interactive_Practice.svg) | [Mã nguồn Mermaid](./diagrams/MF01_Interactive_Practice.mmd) | [Tệp Draw.io](./diagrams/MF01_Interactive_Practice.drawio)
- **MF-02: Thi thử bấm giờ & Instant Feedback Scorecard:** [Sơ đồ PNG](./diagrams/MF02_Timed_Mock_Exam.png) | [Sơ đồ SVG Vector](./diagrams/MF02_Timed_Mock_Exam.svg) | [Mã nguồn Mermaid](./diagrams/MF02_Timed_Mock_Exam.mmd) | [Tệp Draw.io](./diagrams/MF02_Timed_Mock_Exam.drawio)
- **MF-03: Ngân hàng câu hỏi, Giảng viên sử dụng AI sinh câu hỏi theo barem của mình & Gửi Bộ Môn duyệt:** [Sơ đồ PNG](./diagrams/MF03_Question_Bank_Rubric.png) | [Sơ đồ SVG Vector](./diagrams/MF03_Question_Bank_Rubric.svg) | [Mã nguồn Mermaid](./diagrams/MF03_Question_Bank_Rubric.mmd) | [Tệp Draw.io](./diagrams/MF03_Question_Bank_Rubric.drawio)
- **MF-04: Thi thật phòng Lab Kiosk, AI Chấm ngầm, Hậu kiểm phân 2 nhóm, Giảng viên Publish điểm & Phúc khảo Nội Bộ:** [Sơ đồ PNG](./diagrams/MF04_Lab_Exam_Audit.png) | [Sơ đồ SVG Vector](./diagrams/MF04_Lab_Exam_Audit.svg) | [Mã nguồn Mermaid](./diagrams/MF04_Lab_Exam_Audit.mmd) | [Tệp Draw.io](./diagrams/MF04_Lab_Exam_Audit.drawio)
- **Sơ đồ CSDL 28 bảng (ERD):** [Mã nguồn Mermaid ERD](./diagrams/ERD_DATABASE_DIAGRAM.mmd)
- **Kiến trúc hệ thống tổng thể:** [Trình xem Slide Kiến trúc HTML](./diagrams/XEM_KIEN_TRUC_HE_THONG_TRUC_TIEP.html) | [Sơ đồ System Context PNG](./diagrams/SYSTEM_CONTEXT_DIAGRAM.png) | [Sơ đồ Deployment PNG](./diagrams/SYSTEM_ARCHITECTURE_DEPLOYMENT_DIAGRAM.png)

---

## 👥 3. PHÂN CÔNG TRÁCH NHIỆM 4 THÀNH VIÊN (FA26SE166)

| Thành viên | Vai trò | Trách nhiệm chính |
|:---|:---|:---|
| 🧑 **Nguyễn Quang Thành** | Team Leader & Lead BE Architect | Kiến trúc .NET 8 Clean Architecture, Hàng đợi `BoundedChannel` 1,000 slots, SignalR Hub, Quota Guard PostgreSQL, `OneWayLockInterceptor`, Tích hợp hệ thống tổng thể. |
| 🧑 **Nguyễn Trọng Tốt** | BE Developer, AI & QA Lead | Prompt Gemini 1.5 Flash/Pro CoT 3 bước, Whisper STT bóc băng kèm timestamps, R2 Upload & băm SHA-256 niêm phong, Barem Rubric 10.0đ, Unit Tests & NetArchTest. |
| 🧑 **Nguyễn Đăng Hải** | DB Specialist & Frontend Developer | Quản trị CSDL PostgreSQL 16 DDL (28 bảng 3NF), Docker Compose, Seed data (tuyệt đối KHÔNG code logic C# Backend); dồn toàn lực tham gia phát triển Frontend (Student Portal, Mock Exam Voice-First, Phê duyệt đề Trưởng BM, Phân hệ Phúc khảo). |
| 🧑 **Lê Vũ Hoàng** | Lead Frontend Architect & Coordinator | React 19 Vite, Tailwind CSS v4, Buffer Screen hiệu đính Code-Switching (cấu hình động môn 10–300s), Kiosk Lockdown (chặn Alt+Tab, Mic-check 30s $\ge 60$dB), Waveform Player tua theo timestamps, Kết nối API Contract-First. |

### 🛡️ 3.1. HỆ THỐNG 5 VAI TRÒ NGƯỜI DÙNG PHÂN QUYỀN (RBAC USER ROLES)

| Vai trò (Role Claim) | Định danh vai trò | Quyền hạn và Trách nhiệm nghiệp vụ chính |
|:---|:---|:---|
| `student` | Sinh viên | Luyện tập tự do (MF-01), Thi thử bấm giờ (MF-02, $K \le 3$), Thi vấn đáp phòng Lab Kiosk (MF-04), xem điểm Student Portal & nộp đơn phúc khảo nội bộ (`POST /api/v1/appeals`). |
| `lecturer` | Giảng viên | Soạn câu hỏi thủ công, Giảng viên sử dụng AI sinh câu hỏi theo barem của mình từ FLM (Rubric 10.0đ), chạy AI Simulator, gửi Bộ Môn thẩm định (MF-03); coi thi ca Lab, thẩm định điểm Hậu kiểm 2 nhóm, giải trình sửa điểm, và Công bố điểm ca thi (Publish Grades) kích hoạt One-Way Lock (MF-04). |
| `department_head` | Trưởng Bộ Môn | Quản lý đề cương môn học, kích hoạt AI sinh câu hỏi từ FLM, thẩm định và phê duyệt/yêu cầu chỉnh sửa câu hỏi do Giảng viên gửi lên (MF-03), thẩm định và xử lý đơn phúc khảo nội bộ của sinh viên (MF-04). |
| `proctor` | Giám thị | Điểm danh, gán vị trí máy trạm (Booth 1–40), phát lệnh bắt đầu/kết thúc ca thi phòng Lab (MF-04). |
| `admin` | Quản trị viên | Quản trị danh mục học thuật, phân quyền RBAC, cấu hình hệ thống, giám sát và Replay Dead-Letter Queue (DLQ). |

---

## ⚡ 4. BỘ LỆNH MỘT CHẠM DÀNH CHO LẬP TRÌNH VIÊN (ONE-CLICK COMMANDS)

```powershell
# 1. Khởi động Cơ sở dữ liệu PostgreSQL 16 bằng Docker (tại thư mục gốc 05_Source_Code)
docker compose up -d

# 2. Biên dịch Backend Solution (.NET 8 Clean Architecture)
dotnet build backend

# 3. Chạy Unit Tests Backend
dotnet test backend --verbosity normal

# 4. Biên dịch Frontend (React 19 Vite)
cd frontend; npm run build
```
