# BẢNG PHÂN CÔNG NHIỆM VỤ & LỘ TRÌNH AGILE (BÁM SÁT REPORT 2)
**Dự án:** An LLM-based Oral Examination  
**Đội hình (4 thành viên):** Dựa theo chuẩn RACI Matrix từ Report 2.  

---

## 👨‍💻 PHẦN 1: MA TRẬN PHÂN CÔNG NHIỆM VỤ CHÍNH THỨC (TỪ REPORT 2)

Dựa trên tài liệu thiết kế (Report 2), dự án chia làm 2 nhóm chạy song song (FE và BE). Phân công chi tiết cho 4 thành viên như sau:

### 1. Lê Vũ Hoàng (Frontend Developer)
* **Vai trò:** FE Lead, chịu trách nhiệm chính về mảng giao diện.
* **Nhiệm vụ (Tasks):** 
  - Thiết kế kiến trúc Frontend (FE architecture) và code các màn hình FE (Assigned FE screens).
  - Khớp nối và tích hợp API với Backend (FE-BE integration).
  - Hỗ trợ viết và chạy các kịch bản kiểm thử giao diện (Testing support).

### 2. Phạm Nguyễn Đăng Hải (Backend & FE Support)
* **Vai trò:** BE Developer kiêm hỗ trợ Frontend.
* **Nhiệm vụ (Tasks):** 
  - Thiết kế sơ đồ CSDL (ERD), viết các file Database Migrations và dựng khung xương API (API scaffold).
  - Trực tiếp hỗ trợ Hoàng code một số màn hình Frontend khó (Support for selected FE screens).

### 3. Nguyễn Quang Thành (Team Leader & Backend Developer)
* **Vai trò:** Trưởng nhóm, định hướng kiến trúc BE.
* **Nhiệm vụ (Tasks):** 
  - Điều phối team, quản lý và ưu tiên danh sách công việc (Backlog prioritization).
  - Review kiến trúc BE, phụ trách tích hợp hệ thống (Integration).
  - Chịu trách nhiệm chính trong các buổi báo cáo, trình diễn dự án (Demonstrations / Final Defense).

### 4. Nguyễn Trọng Tốt (Backend, Project Management & QA)
* **Vai trò:** BE Developer kiêm Quản lý chất lượng (QA).
* **Nhiệm vụ (Tasks):** 
  - Code các API Backend được phân công (Implementation of assigned BE APIs).
  - Tích hợp và hỗ trợ mảng Trí tuệ nhân tạo (API/AI support).
  - Viết kịch bản kiểm thử (Test cases), chạy kiểm thử E2E và lập báo cáo kiểm thử cuối cùng (Test report).

---

## 📅 PHẦN 2: LỘ TRÌNH THỰC THI AGILE 4 TUẦN (ÉP TIẾN ĐỘ REPORT 2)
*(Rút gọn từ lộ trình 15 tuần của Report 2 xuống 4 tuần thực chiến, Review 3 ngày/lần)*

### 🏃 TUẦN 1: KHỞI TẠO NỀN TẢNG (NẮM MỐC REVIEW 1 CỦA W03)
* **Ngày 3 (Review Foundation):**
  * **Hải:** Chốt ERD, chạy Database Migrations và API scaffold.
  * **Hoàng:** Setup Repo FE, giao diện Base.
  * **Thành (Lead):** Review kiến trúc, chốt API Contract (OpenAPI) giữa FE và BE.
* **Ngày 6 (Tích hợp MF-03):**
  * **Hải & Tốt:** Code API Ngân hàng câu hỏi & Barem (Từ chối lưu nếu Rubric != 10).
  * **Hoàng:** Ráp API MF-03 lên giao diện.
  * **Tốt (QA):** Viết Test cases cho MF-03.

### 🏃 TUẦN 2: THI TƯƠNG TÁC ĐA PHƯƠNG THỨC (NẮM MỐC MF-01)
* **Ngày 9 (Âm thanh & AI Concept):**
  * **Hoàng & Hải:** Xử lý thu âm Web Speech API, đếm ngược 60s.
  * **Tốt & Thành:** Setup Prompt AI, chạy thử file audio lấy điểm (Proof of Concept).
* **Ngày 12 (Hoàn thiện MF-01):**
  * **Thành:** Code hàng đợi (Queue) hứng bài nộp.
  * **Tốt:** Đổ điểm đánh giá AI vào DB, sinh Test cases MF-01 (Lỗi mạng, lưu bài).

### 🏃 TUẦN 3: THI THỬ & CHỊU TẢI (NẮM MỐC MF-02 & REVIEW 2 CỦA W08)
* **Ngày 15 (Chốt chặn & Quota):**
  * **Hải:** Viết API kiểm tra Quota (Giới hạn thi/ngày).
  * **Tốt:** Gắn thuật toán sinh câu hỏi A2 nếu điểm 4.0 - 8.0.
  * **Hoàng:** UI thi thử có đồng hồ kép, khóa text input (Voice-First Gate).
* **Ngày 18 (End-to-End MF-02):**
  * **Thành:** Tích hợp E2E, chuẩn bị Demo cho Review 2.
  * **Tốt (QA):** Chạy hồi quy (Regression test) MF-01 & MF-02.

### 🏃 TUẦN 4: AN NINH LAB (MF-04) & ĐÓNG GÓI BẢO VỆ (FINAL DEFENSE W15)
* **Ngày 21 (An ninh & Hậu kiểm):**
  * **Hoàng & Hải:** Lockdown Kiosk (Chặn F12) và UI Cổng Hậu kiểm Giảng viên.
  * **Thành:** API khóa điểm 1 chiều (One-Way Lock) cho giảng viên.
  * **Tốt:** Lưu trữ Cloudflare R2 audio `STT_MSSV.webm`.
* **Ngày 24 & 27 (Tổng duyệt UAT & Bàn giao):**
  * **Tốt (QA):** Đảm bảo 100% Critical Cases Pass, không còn lỗi Blocker (Mất bài, khóa điểm sai). Lập Test Report.
  * **Thành:** Đóng gói bản Release, tổng duyệt Slide chuẩn bị cho Final Defense.
