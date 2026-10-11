# 🎓 Oral Exam LLM System — Frontend Engineering Portal (FA26SE166)

> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm  
> **Nhóm thực hiện:** FA26SE166 — Đại học FPT Hà Nội  
> **Tech Stack:** React 19 · TypeScript 5.8+ · Vite · Tailwind CSS v4 · SignalR Real-time · Web Speech API

---

## 🚀 1. Khởi Động Nhanh (One-Click Setup)

Mọi thành viên FE (Hoàng, Hải) chỉ cần thực hiện 3 bước để bắt đầu làm việc:

```bash
# 1. Di chuyển vào thư mục frontend
cd "d:\Đồ Án\05_Source_Code\frontend"

# 2. Cài đặt toàn bộ thư viện dependencies (nếu chưa cài)
npm install

# 3. Khởi chạy máy chủ phát triển cục bộ
npm run dev
```

* Ứng dụng sẽ chạy tại: **`http://localhost:5173`**
* Kiểm tra type & build sản phẩm: **`npm run build`** (Bắt buộc chạy trước khi commit git, yêu cầu Exit Code = 0).

---

## 👥 2. Phân Vai Tác Chiến Kỹ Sư FE

| Thành viên | Trách nhiệm cốt lõi | Các tệp & thư mục phụ trách chính |
| :--- | :--- | :--- |
| **Hoàng** *(Lead Core Logic & API)* | • Quản lý State toàn cục & Authentication.<br>• Tích hợp API Backend .NET 8 (Axios Interceptor).<br>• Xử lý luồng Real-time WebSocket SignalR.<br>• Xử lý Web Speech API (Microphone STT/TTS). | • `src/services/` (`api.client.ts`, `auth.service.ts`,...)<br>• `src/hooks/` (`useSignalR.ts`, `useSpeechRecognition.ts`)<br>• `src/context/` (`AuthContext.tsx`)<br>• `src/types/` (Data contracts) |
| **Hải** *(Lead UI/UX & Design System)* | • Xây dựng Design System & UI Primitives.<br>• Màn hình đệm 30s (`BufferScreen`), Modal Barem Rubric.<br>• Màn thi Voice-First & Đồng hồ đếm ngược Server.<br>• An ninh phòng Lab (`useKioskLockdown.ts`), Audio Player R2.<br>• Biểu đồ năng lực Bloom Taxonomy (Recharts). | • `src/components/common/` (Button, Card, Input, Badge)<br>• `src/components/practice/`, `exam/`, `audio/`, `rubric/`<br>• `src/components/layout/` (Navbar, Sidebar, Layout)<br>• `src/pages/student/`, `lecturer/` |

---

## 🛠️ 3. Lối Tắt Dành Cho Lập Trình Viên (Dev Quick Switch)

Để thuận tiện phát triển giao diện độc lập mà không cần chờ Backend khởi động database:
* Mở trang: **`http://localhost:5173/login`**.
* Phía dưới form đăng nhập đã tích hợp sẵn **2 nút đăng nhập nhanh 1-click**:
  * 🔘 **Đăng nhập nhanh: Sinh viên (Student)** $\rightarrow$ Vào ngay Dashboard sinh viên, mở khóa luyện tập MF-01 và thi thử MF-02.
  * 🔘 **Đăng nhập nhanh: Giảng viên (Instructor)** $\rightarrow$ Mở khóa Ngân hàng câu hỏi MF-03 và Cổng thẩm định phòng Lab MF-04.

---

## 📁 4. Bản Đồ Cấu Trúc Mã Nguồn (Codebase Map)

```
src/
├── routes/              # AppRoutes, ProtectedRoute, RoleBasedRedirect
├── types/               # DTO theo practice, mock exam, official exam và rubric
├── services/            # API client và service tách theo từng phân hệ
├── stores/              # Khung dành cho Zustand khi nghiệp vụ tương ứng được triển khai
├── hooks/               # Speech Recognition, SignalR và Kiosk Lockdown
├── components/
│   ├── common/          # Button, Card, Input, Badge và Separator
│   ├── layout/          # App/Student layout, Navbar, Sidebar, Footer
│   ├── practice/        # BufferScreen, ScorecardModal
│   ├── rubric/          # Thành phần rubric và hậu kiểm điểm
│   ├── audio/           # WaveformPlayer
│   └── exam/            # VoiceFirstGate, CountdownTimer
├── pages/
│   ├── auth/            # Login và Unauthorized
│   ├── student/         # Dashboard, Practice, Mock Exam, Exam History, Official Exam
│   └── lecturer/        # Dashboard, Question Studio, Audit Evidence
├── context/             # AuthContext hiện hữu, giữ nguyên hành vi đăng nhập
├── assets/              # Logo và tài nguyên tĩnh
├── lib/                 # Tiện ích tương thích thư viện
└── utils/               # cn.ts (clsx + tailwind-merge)
```

---

## 📚 5. Ba Tài Liệu Kỹ Thuật Bắt Buộc Đọc

1. 🖥️ **Đặc tả chức năng và danh sách màn hình frontend:**
   👉 Đọc tại file: [`FE_TASK_BOARD.md`](./FE_TASK_BOARD.md)
   *(Tài liệu chuẩn hiện tại về portal, màn hình, component và hợp đồng chức năng frontend).*

2. 📋 **Kế hoạch chi tiết 4 tuần & Lịch họp 2 ngày/lần:**
   👉 Đọc tại file: [`FE_TASK_EXECUTION_GUIDE.md`](./FE_TASK_EXECUTION_GUIDE.md)  
   *(Chi tiết nhiệm vụ từng ngày từ Tuần 1 đến Tuần 4 cho Hoàng & Hải).*

3. 🔌 **Quy ước Hợp đồng API giữa FE và BE (.NET 8):**
   👉 Đọc tại file: [`../docs/API_CONTRACT_AND_INTEGRATION_GUIDE.md`](../docs/API_CONTRACT_AND_INTEGRATION_GUIDE.md)  
   *(Chi tiết Base URL `/api/v1/`, cấu trúc JSON camelCase, mã lỗi 422/429/403, và SignalR Hub `/hubs/practice`).*
