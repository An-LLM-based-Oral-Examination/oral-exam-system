import './App.css'

function App() {
  return (
    <div className="app-container">
      <header className="app-header">
        <span className="badge">FA26SE166 Capstone Project</span>
        <h1>An LLM-based Oral Examination</h1>
        <p className="subtitle">
          Interactive, rubric-driven oral examination platform powered by Large Language Models
        </p>
      </header>

      <main className="app-content">
        <div className="status-card">
          <div className="status-indicator">
            <span className="pulse-dot"></span>
            <span>System Skeleton Ready</span>
          </div>
          <p className="status-description">
            Frontend single page application skeleton initialized with React 19, TypeScript, and Vite.
          </p>
          <div className="meta-grid">
            <div className="meta-item">
              <span className="meta-label">Architecture</span>
              <span className="meta-value">Clean Architecture Monorepo</span>
            </div>
            <div className="meta-item">
              <span className="meta-label">Backend</span>
              <span className="meta-value">.NET 8 Web API</span>
            </div>
            <div className="meta-item">
              <span className="meta-label">Frontend</span>
              <span className="meta-value">React 19 + TypeScript</span>
            </div>
          </div>
        </div>
      </main>

      <footer className="app-footer">
        <p>&copy; {new Date().getFullYear()} An LLM-based Oral Examination System. All rights reserved.</p>
      </footer>
    </div>
  )
}

export default App
