# Cloudflare R2 Bucket Policies (`oral-exam-bucket`)

## 1. CORS Policy (`cors-policy.json`)

Allowed browser origins are strictly scoped to the canonical production apex domain and local development servers:
- Production Web SPA: `https://oralexam.io.vn` (`https://www.oralexam.io.vn` is permanently 301-redirected to the apex domain by `frontend/nginx.prod.conf`).
- Local Development: `http://localhost:3000`, `http://127.0.0.1:3000`, `http://localhost:5173`, `http://127.0.0.1:5173`, `http://127.0.0.1:5174` (Kiosk dev renderer).

### Desktop Kiosk (Electron) Upload Policy
- Stale Tauri origins (`tauri://localhost`, `https://tauri.localhost`) have been removed because the Desktop Kiosk uses Electron rather than Tauri.
- Packaged Electron builds load from `file://` (`Origin: null`). **NEVER** add `"*"` or `"null"` wildcard origins to the R2 bucket CORS policy. Instead, packaged Electron uploads to presigned R2 URLs MUST go through the Electron main-process HTTP bridge (or a scoped `webRequest` CORS bridge for `https://*.r2.cloudflarestorage.com/*`).

## 2. Lifecycle Policy (`lifecycle-policy.json`)

Retains immutable MF-04 official exam audio under `official-exams/` for 365 days minimum and aborts incomplete multipart uploads after 1 day.
