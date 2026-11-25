# AGENTS.md — Quy Chuẩn Phát Triển Desktop Kiosk (`desktop-kiosk/`)

> **Phạm vi sở hữu:** Thư mục `desktop-kiosk/` là phân hệ Desktop Client độc lập cho luồng thi chính thức **MF-04** (BYOD phòng Lab có Giám thị). Mọi chỉnh sửa Desktop Kiosk chỉ nằm trong `desktop-kiosk/` và không được can thiệp vào `frontend/` hay `backend/`.

---

## 1. Cấu Trúc Thư Mục 3 Tầng

```text
desktop-kiosk/
├── build/                      # Tài nguyên đóng gói (icon.png)
├── docs/                       # Tài liệu kiến trúc & walkthrough MF-04
├── electron/                   # Electron Main & Preload Shell (bundle qua vite.electron.config.ts)
│   ├── main.ts                 # Vòng đời ứng dụng, cửa sổ chính & đăng ký IPC
│   ├── preload.ts              # ContextBridge sandbox (tuyệt đối KHÔNG import file nội bộ lúc runtime)
│   ├── config.ts               # Cấu hình runtime, URL API, cờ lockdown & trusted origins
│   ├── device.ts               # Quản lý X-Device-Id bền vững (^[A-Za-z0-9._:-]{16,128}$)
│   ├── security.ts             # Lockdown cửa sổ, chặn phím tắt, theo dõi màn hình & đẩy sự kiện an ninh
│   └── windows-agent.ts        # Cầu nối an toàn tới native/Oes.WindowsAgent (có fallback)
├── native/
│   └── Oes.WindowsAgent/       # Sidecar .NET 8 trên Windows (dò đa màn hình, VM, remote control, tiến trình cấm)
├── scripts/                    # Script build sidecar & kiểm chứng tự động
├── src/                        # Renderer React 19 + TypeScript (bundle qua vite.config.ts)
│   ├── types/                  # Hợp đồng DTO MF-04
│   ├── services/               # ApiClient (RAM token), IndexedDB AES-GCM offline-draft
│   ├── utils/                  # Formatters, crypto SHA-256, retry
│   ├── hooks/                  # React hooks giao tiếp window.kioskApi
│   ├── screens/                # Các màn hình tách biệt theo bước MF-04
│   └── styles/                 # CSS chuẩn Calm Academic Depth
└── tests/                      # Bộ kiểm thử hợp đồng & đơn vị
```

---

## 2. Bất Biến Bảo Mật & Nghiệp Vụ MF-04 Bắt Buộc

1. **Cách ly Electron tuyệt đối:** `contextIsolation: true`, `nodeIntegration: false`, `sandbox: true`, chặn `setWindowOpenHandler`, `will-navigate`, `will-attach-webview`.
2. **Preload tự chứa (Self-contained):** `electron/preload.ts` chỉ dùng `import { contextBridge, ipcRenderer } from 'electron'` và `import type`; không dùng relative runtime imports để đảm bảo an toàn dưới chế độ `sandbox: true`.
3. **Không lưu JWT ra storage:** Tuyệt đối cấm `localStorage`/`sessionStorage` cho token; chỉ giữ JWT trong bộ nhớ `ApiClient`.
4. **Không lộ điểm tại Kiosk:** Không hiển thị điểm số, nhận xét AI hay nút phúc khảo tại Desktop Kiosk.
5. **Trạng thái vé chữ HOA:** Chỉ dùng 7 trạng thái `SCHEDULED`, `IN_PROGRESS`, `SUBMITTED`, `AI_GRADED`, `AUDITED`, `PUBLISHED`, `LOCKED`.
6. **Persist-First & Offline Journal mã hóa:** Audio và metadata cục bộ bắt buộc mã hóa `AES-256-GCM` trong IndexedDB; chỉ xóa journal sau khi Backend xác nhận bài đã nộp.
7. **Graceful Degradation cho Native Sidecar:** `native/Oes.WindowsAgent` tăng cường giám sát trên Windows, nhưng ứng dụng phải vẫn vận hành ổn định (fallback sang tín hiệu Electron) trên macOS/môi trường chưa build sidecar.

---

## 3. Lệnh Kiểm Chứng Cứng

```powershell
npm run typecheck
npm run build
npm test
npm run verify
```
