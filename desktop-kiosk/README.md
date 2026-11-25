# Oral Exam Desktop Kiosk

Ứng dụng Desktop Kiosk cho luồng thi chính thức **MF-04** (Oral Examination System — FA26SE166), được tổ chức theo kiến trúc 3 tầng tách biệt: **Renderer React 19 (`src/`)**, **Electron Main/Preload (`electron/`)**, và **Native Windows Agent (`native/Oes.WindowsAgent/`)**.

## Cấu trúc thư mục

```text
desktop-kiosk/
├── build/                    # Tài nguyên đóng gói (icon.png)
├── docs/                     # Tài liệu kiến trúc & walkthrough
├── electron/                 # Electron Main, Preload, Security, Device & Agent Bridge
├── native/Oes.WindowsAgent/  # Sidecar .NET 8 phát hiện môi trường & tiến trình trên Windows
├── scripts/                  # Script build sidecar & kiểm chứng tổng thể
├── src/                      # Ứng dụng React 19 (types, services, utils, hooks, screens, styles)
├── tests/                    # Bộ kiểm thử hợp đồng & đơn vị
├── AGENTS.md                 # Quy chuẩn kỹ thuật cục bộ cho Desktop Kiosk
├── tsconfig.json             # Solution references (app + node)
├── tsconfig.app.json         # Cấu hình TS cho Renderer (src/)
├── tsconfig.node.json        # Cấu hình TS cho Electron & Vite configs
├── vite.config.ts            # Build Renderer
└── vite.electron.config.ts   # Bundle Electron Main & Preload (.cjs)
```

## Chạy môi trường phát triển (Local)

```powershell
Set-Location .\desktop-kiosk
npm install
npm run verify
npm run dev
```

Để mở cửa sổ phát triển có thể đóng tự do khi debug:

```powershell
$env:KIOSK_MODE = '0'
npm run dev
```

Có thể trỏ tới API cụ thể bằng biến môi trường `KIOSK_API_BASE_URL` (mặc định dev dùng `http://127.0.0.1:5000/api/v1`).

## Build & Đóng gói Windows

```powershell
npm run build            # Build Electron (.cjs) + Renderer (dist/)
npm run build:native     # Build & publish native/Oes.WindowsAgent (.NET 8)
npm run verify           # Chạy đầy đủ typecheck, build, native build & tests
npm run package:win      # Đóng gói bộ cài đặt NSIS Windows
```

Xem chi tiết kiến trúc tại [`docs/ARCHITECTURE.md`](./docs/ARCHITECTURE.md) và [`docs/WALKTHROUGH.md`](./docs/WALKTHROUGH.md).
