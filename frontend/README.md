# Oral Examination System — Frontend Client

This package contains the Single Page Application (SPA) client for the **LLM-based Oral Examination System** (FA26SE166), built with React 19, TypeScript, and Vite.

---

## 🚀 Tech Stack

- **Framework:** React 19
- **Language:** TypeScript 5.8+ (Strict Mode)
- **Bundler & Tooling:** Vite 6+
- **Styling:** Modern Responsive CSS Tokens

---

## 🛠️ Scripts

- `npm run dev`: Start local Vite development server with Hot Module Replacement (HMR).
- `npm run build`: Type-check with TypeScript compiler (`tsc -b`) and build optimized production bundle to `dist/`.
- `npm run preview`: Locally preview production build artifacts.
- `npm run lint`: Run Oxlint for code quality and linting verification.

---

## 📁 Directory Structure

```
frontend/
├── public/               # Static assets (favicon, public icons)
├── src/
│   ├── assets/           # Application assets
│   ├── components/       # Reusable UI components
│   ├── features/         # Domain-specific feature modules
│   ├── App.css           # Root application styling
│   ├── App.tsx           # Main application view
│   ├── index.css         # Global design tokens and reset styles
│   ├── main.tsx          # Application bootstrap entrypoint
│   └── vite-env.d.ts     # Vite environment types
├── index.html            # HTML entry document
├── package.json          # Node dependencies and scripts
├── tsconfig.json         # TypeScript root configuration
└── vite.config.ts        # Vite configuration
```
