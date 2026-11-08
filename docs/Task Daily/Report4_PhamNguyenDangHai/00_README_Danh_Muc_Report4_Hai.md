# 📋 DANH MỤC BÀN GIAO REPORT 4 — PHẠM NGUYỄN ĐĂNG HẢI (DB SPECIALIST & FRONTEND DEVELOPER)

> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (`FA26SE166` — Fall 2026 — ĐH FPT TP.HCM)  
> **Thành viên phụ trách (Author):** **Phạm Nguyễn Đăng Hải** (DB Specialist & Frontend Developer)  
> **Người kiểm tra chéo (Cross-Reviewer):** **Lê Vũ Hoàng** (Lead Frontend Architect)  
> **Phạm vi phụ trách trong Report 4:**  
> - **Section 1.1.1, 1.1.2 & 1.2.5:** Overall System Architecture (`D01`), Deployment Diagram (`D02`), Persistence Package Diagram (`D07`)  
> - **Section 3.7 (`DD-07`) & 3.8 (`DD-08`):** Quản lý Học thuật R5 (Semester, Course, Department Head Assignment, Lecturer Assignment, Class Creation & Student Enrollment)  
> - **Section 3.9 (`DD-09`) đến 3.12 (`DD-12`):** Toàn bộ Main Flow 02 — Thi Thử Tính Giờ Có Hạn Ngạch (`MF-02`: Cấu hình & Quota theo môn, Khởi tạo phiên thi & Master Timer, Nộp bài & Tự động nộp khi hết giờ, Chấm điểm AI theo Rubric 10.0 & CLO Scorecard / Lịch sử thi)  
> - **Ma trận Truy vết 38 Bảng 3NF & Báo cáo Kiểm tra Chéo `DD-01..DD-06`**

---

## 🗂️ 1. Tổng Hợp 15 Sơ Đồ (`.drawio` + `.png`) & 8 Tài Liệu Đặc Tả (`.md`)

| STT | Thư mục | Tên Sơ Đồ / Tài Liệu | Định Dạng | Trạng Thái |
| :--- | :--- | :--- | :--- | :--- |
| **01** | `01_Section_1.1.1_1.1.2_1.2.5_Architecture_Deployment_Persistence` | `D01_Overall_System_Architecture` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| **02** | `01_Section_1.1.1_1.1.2_1.2.5_Architecture_Deployment_Persistence` | `D02_Deployment_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| **03** | `01_Section_1.1.1_1.1.2_1.2.5_Architecture_Deployment_Persistence` | `D07_Persistence_Package_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| — | `01_Section_1.1.1_1.1.2_1.2.5_Architecture_Deployment_Persistence` | `Section_1.1_1.2.5_Architecture_Deployment_Persistence_VI.md` | `.md` | ✅ Hoàn thành 100% |
| **04** | `02_DD07_R5_Semester_Course_DeptHead_Assignment` | `DD07_SemesterCourse_Class_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| **05** | `02_DD07_R5_Semester_Course_DeptHead_Assignment` | `DD07_SemesterCourse_Sequence_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| — | `02_DD07_R5_Semester_Course_DeptHead_Assignment` | `DD07_SemesterCourse_Specification_VI.md` | `.md` | ✅ Hoàn thành 100% |
| **06** | `03_DD08_R5_Lecturer_Assignment_Class_Student_Enrollment` | `DD08_ClassEnrollment_Class_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| **07** | `03_DD08_R5_Lecturer_Assignment_Class_Student_Enrollment` | `DD08_ClassEnrollment_Sequence_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| — | `03_DD08_R5_Lecturer_Assignment_Class_Student_Enrollment` | `DD08_ClassEnrollment_Specification_VI.md` | `.md` | ✅ Hoàn thành 100% |
| **08** | `04_DD09_MockExam_Config_Daily_Quota_Guard` | `DD09_MockConfigQuota_Class_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| **09** | `04_DD09_MockExam_Config_Daily_Quota_Guard` | `DD09_MockConfigQuota_Sequence_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| — | `04_DD09_MockExam_Config_Daily_Quota_Guard` | `DD09_MockConfigQuota_Specification_VI.md` | `.md` | ✅ Hoàn thành 100% |
| **10** | `05_DD10_MockSession_Generation_Questions_MasterTimer` | `DD10_MockSessionInit_Class_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| **11** | `05_DD10_MockSession_Generation_Questions_MasterTimer` | `DD10_MockSessionInit_Sequence_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| — | `05_DD10_MockSession_Generation_Questions_MasterTimer` | `DD10_MockSessionInit_Specification_VI.md` | `.md` | ✅ Hoàn thành 100% |
| **12** | `06_DD11_MockAnswer_Submission_AutoFinalSubmit` | `DD11_MockSubmission_Class_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| **13** | `06_DD11_MockAnswer_Submission_AutoFinalSubmit` | `DD11_MockSubmission_Sequence_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| — | `06_DD11_MockAnswer_Submission_AutoFinalSubmit` | `DD11_MockSubmission_Specification_VI.md` | `.md` | ✅ Hoàn thành 100% |
| **14** | `07_DD12_MockAiGrading_CloScorecard_AttemptHistory` | `DD12_MockGradingScorecard_Class_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| **15** | `07_DD12_MockAiGrading_CloScorecard_AttemptHistory` | `DD12_MockGradingScorecard_Sequence_Diagram` | `.drawio` + `.png` | ✅ Hoàn thành 100% |
| — | `07_DD12_MockAiGrading_CloScorecard_AttemptHistory` | `DD12_MockGradingScorecard_Specification_VI.md` | `.md` | ✅ Hoàn thành 100% |
| — | `08_DB_38_Tables_Traceability_And_CrossReview` | `DB_38_Tables_Traceability_And_CrossReview_DD01_DD06_VI.md` | `.md` | ✅ Hoàn thành 100% |

---

## 🛡️ 2. Cam Kết Tuân Thủ 6 Quy Tắc Chuẩn Hóa UML 2.5 & Plain English

1. **Quy tắc 1 (Mũi tên Sequence Diagram & Cặp Truy vấn Database):** 100% lời gọi hàm (`Synchronous Call`) dùng đường nét liền + mũi tên tam giác đặc (`endArrow=block;endFill=1;dashed=0`); 100% phản hồi kết quả (`Return`) dùng đường nét đứt + mũi tên mở (`endArrow=open;dashed=1`); mọi lời gọi vào `Database` đều có thanh Activation Bar và mũi tên `Return` nét đứt trả về.
2. **Quy tắc 2 (Zero Duplicate Message Numbers):** Không có bất kỳ số thứ tự thông điệp nào bị lặp lại giữa các nhánh trong cùng một Sequence Diagram (`.1`/`.2` cho nhánh chính 1, `.3`/`.4` cho nhánh chính 2/3, `.4`/`.5` cho khung `opt [Exception occurs]`).
3. **Quy tắc 3 (Khung `opt [Exception occurs]`):** Toàn bộ 6 Sequence Diagram sử dụng khung `opt` (`[Exception occurs]`) ở cuối sơ đồ với thanh Activation Bar liền mạch và mũi tên tự gọi `Log unexpected error` nằm trọn bên trong khung.
4. **Quy tắc 4 (100% Plain English — Cấm Cú pháp Code/URL trên Sequence Diagram):** Hoàn toàn không chứa đường dẫn HTTP, tên cột `snake_case`, tên DTO/Command dài, toán tử code hay hậu tố `[Kiosk]`, `[BE]`, `[PostgreSQL]`.
5. **Quy tắc 5 (Chống Đè Chữ & Cắt Nét):** Khoảng cách giữa các cột Lifeline rộng `245px - 270px` (khổ ảnh `1580px - 1680px`), toàn bộ nhãn `[Guard]` và thông điệp có nền trắng chống cắt nét (`Zero Visual Overlap`).
6. **Quy tắc 6 (Đường nối Class Diagram & Package Diagram):** Phân tách hoàn toàn các đường `Uses` (`dashed=1;endArrow=open`) và `Implements` (`dashed=1;endArrow=block;endFill=0`) theo từng rãnh ngang riêng biệt, vẽ đường trước và nhãn nền trắng sau.
