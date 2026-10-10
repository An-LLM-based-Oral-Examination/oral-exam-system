/**
 * Enterprise Commitlint Configuration — LLM Oral Exam System (FA26SE166)
 *
 * Enforces Conventional Commits v1.0.0 specification matching:
 * - 05_Source_Code/.gitmessage
 * - 05_Source_Code/docs/GIT_AND_TEAM_WORKFLOW.md
 * - 05_Source_Code/.git/hooks/commit-msg
 */

module.exports = {
  extends: ['@commitlint/config-conventional'],
  rules: {
    // 1. Commit Types (11 valid types)
    'type-enum': [
      2,
      'always',
      [
        'feat',     // A new feature for users
        'fix',      // A bug fix
        'docs',     // Documentation only changes
        'style',    // Changes that do not affect the meaning of code (formatting, CSS)
        'refactor', // A code change that neither fixes a bug nor adds a feature
        'perf',     // A code change that improves performance
        'test',     // Adding missing tests or correcting existing tests
        'build',    // Changes that affect the build system or external dependencies
        'ci',       // Changes to CI/CD configuration files and scripts
        'chore',    // Other changes that don't modify src or test files
        'revert'    // Reverting previous commits
      ]
    ],
    'type-empty': [2, 'never'],
    'type-case': [2, 'always', 'lower-case'],

    // 2. Scopes matching Bounded Contexts
    'scope-enum': [
      2,
      'always',
      [
        'practice',      // MF-01 Interactive Practice
        'mock-exam',     // MF-02 Timed Mock Exam
        'official-exam', // MF-04 Official Lab Viva Exam & Internal Appeals
        'rubric',        // MF-03 Question Bank & Rubric Studio 10.0
        'auth',          // Authentication & RBAC (5 roles)
        'kiosk',         // Lab Kiosk Lockdown & Hardware Security
        'db',            // Database Schema & EF Core Migrations (30 tables)
        'api',           // REST API Endpoints, Middleware, Swagger
        'storage'        // Cloudflare R2 Audio & Whisper STT
      ]
    ],
    'scope-case': [2, 'always', 'lower-case'],

    // 3. Subject rules
    // Rule: Do not capitalize the first letter of the subject line
    'subject-case': [
      2,
      'never',
      ['sentence-case', 'start-case', 'pascal-case', 'upper-case']
    ],
    'subject-empty': [2, 'never'],
    // Rule: Do not place a period (.) at the end of the subject line
    'subject-full-stop': [2, 'never', '.'],

    // 4. Header length constraint (max 100 characters)
    'header-max-length': [2, 'always', 100]
  }
};
