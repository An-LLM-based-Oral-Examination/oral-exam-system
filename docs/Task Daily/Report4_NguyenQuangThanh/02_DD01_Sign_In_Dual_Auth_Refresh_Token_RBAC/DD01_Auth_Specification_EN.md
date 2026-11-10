# REPORT 4 — SOFTWARE DESIGN DOCUMENT (DETAILED DESIGN)
## FEATURE PACKAGE DD-01: SIGN IN, DUAL AUTHENTICATION, REFRESH TOKEN ROTATION & RBAC 6 ROLES

> **Document Information:**
> - **Project:** LLM-Based Oral Examination System for Software Engineering (`FA26SE166`)
> - **Lead Architect:** Nguyen Quang Thanh | **Cross-Reviewer:** Pham Nguyen Dang Hai
> - **Scope:** `DD-01`: Sign In, Dual Authentication, Refresh Token Rotation & 6 Roles RBAC
> - **Diagram Files (Draw.io Vector):**
>   - `DD-01 Sequence Diagrams`: [`DD01_01_Google_Login_Sequence.drawio`](./DD01_01_Google_Login_Sequence.drawio) | [`DD01_02_Password_Login_Sequence.drawio`](./DD01_02_Password_Login_Sequence.drawio) | [`DD01_03_Must_Change_Password_Sequence.drawio`](./DD01_03_Must_Change_Password_Sequence.drawio) | [`DD01_04_Refresh_Token_Rotation_Sequence.drawio`](./DD01_04_Refresh_Token_Rotation_Sequence.drawio) | [`DD01_Auth_Sequence_Diagram.drawio`](./DD01_Auth_Sequence_Diagram.drawio)
>   - `DD-01 Class Diagram`: [`DD01_Auth_Class_Diagram.drawio`](./DD01_Auth_Class_Diagram.drawio)

---

## 3.1. Detailed Design Package `DD-01`: Sign In, Dual Authentication, Refresh Token Rotation & 6 Roles RBAC

### 3.1.1. Feature Overview & Architecture (`DD-01 Overview`)

Package `DD-01` governs identity verification, session management, and role-based access control across 6 roles (`admin`, `department_head`, `examination_officer`, `lecturer`, `proctor`, `student`) for both the **Web Application** and **Desktop Kiosk Application**:

1. **Dual Authentication Schemes:**
   - **Google OAuth 2.0 PKCE Login (`POST /api/v1/auth/google-login`):** Cryptographically verifies the client `IdToken` against Google's public JWKS endpoints. If the email is absent from `users`, a `student` record is auto-provisioned (extracting `StudentCode` from `@fpt.edu.vn`); existing users retain their provisioned database roles.
   - **School-Issued Account Login (`POST /api/v1/auth/password-login`):** Authenticates staff and administrative accounts using `Username` and `PBKDF2-HMAC-SHA256` hashed passwords (210,000 iterations).

2. **Mandatory First-Time Password Change (`MustChangePasswordMiddleware` + `POST /api/v1/auth/change-password`):**
   - Freshly created school accounts have `MustChangePassword = true`. The custom ASP.NET Core middleware intercepts and rejects any access to other business endpoints with `HTTP 403 Forbidden` (`application/problem+json`) until the user establishes a new distinct password.

3. **Session Renewal via Refresh Token Rotation (`POST /api/v1/auth/refresh-token`):**
   - Enforces `RFC 6819 §5.2.2.3`. When renewing a session, the submitted token is permanently revoked (`RevokedAt = UtcNow`) and replaced with a fresh Access Token (15 minutes) and Refresh Token (7 days). If a previously revoked token is submitted, the system flags a Replay Attack and revokes all active sessions for that user.

---

### 3.1.2. Sequence Diagrams (`DD-01 Sequence Diagrams`)

1. **Google OAuth 2.0 PKCE Login (`DD01_01`):**
   - **Vector Source File:** [`DD01_01_Google_Login_Sequence.drawio`](./DD01_01_Google_Login_Sequence.drawio)
   - **Technical Description:** Client sends Google `IdToken` via `POST /api/v1/auth/google-login`. `GoogleLoginCommandHandler` invokes `IGoogleIdTokenValidator` to verify the RSA signature. If the email matches `@fpt.edu.vn`, `StudentCode` is extracted and a student record is activated. `IAccessTokenIssuer` issues a JWT Access Token (15m) and SHA-256 hashed Refresh Token (7d) persisted to `refresh_tokens`.

2. **School Account Password Login (`DD01_02`):**
   - **Vector Source File:** [`DD01_02_Password_Login_Sequence.drawio`](./DD01_02_Password_Login_Sequence.drawio)
   - **Technical Description:** Handles login for 6 RBAC roles via `POST /api/v1/auth/password-login`. `PasswordLoginCommandHandler` verifies credentials via `IPasswordHasher.Verify` using `PBKDF2-HMAC-SHA256` (210,000 iterations) with `CryptographicOperations.FixedTimeEquals` to mitigate timing attacks. If `MustChangePassword == true`, a corresponding claim is embedded in the JWT.

3. **Mandatory First-Time Password Change (`DD01_03`):**
   - **Vector Source File:** [`DD01_03_Must_Change_Password_Sequence.drawio`](./DD01_03_Must_Change_Password_Sequence.drawio)
   - **Technical Description:** Enforced by `MustChangePasswordMiddleware`. When the JWT contains `must_change_password == "true"`, requests to business endpoints return `HTTP 403 Forbidden`. The user must submit `POST /api/v1/auth/change-password`. `ChangePasswordCommandHandler` verifies the old password, validates password complexity, updates `MustChangePassword = false`, and issues a new token pair.

4. **Refresh Token Rotation — RTR (`DD01_04`):**
   - **Vector Source File:** [`DD01_04_Refresh_Token_Rotation_Sequence.drawio`](./DD01_04_Refresh_Token_Rotation_Sequence.drawio)
   - **Technical Description:** `RefreshTokenCommandHandler` hashes the token with SHA-256 and searches `refresh_tokens`. If the token is already revoked (`IsRevoked == true`), a replay attack is detected, revoking all tokens for that user with `HTTP 401 Unauthorized`. If valid, the token is revoked (`RevokedAt = UtcNow`, `ReplacedByTokenHash`) and a fresh token pair is issued.

- **Consolidated Overview Diagram:** [`DD01_Auth_Sequence_Diagram.drawio`](./DD01_Auth_Sequence_Diagram.drawio)

---

### 3.1.3. Step-by-Step Interaction Narrative (`Table 3.1.1`)

| Step | Sender | Receiver | Message / Operation | Detailed Description & Technical Constraints |
|:---:|:---|:---|:---|:---|
| **01** | `: Student / User` | `: Login UI` | Input Credentials or Click Google | User clicks "Sign in with Google" (GIS SDK) or enters username/password into the login form. |
| **02** | `: Login UI` | `: AuthController` | `POST /api/v1/auth/google-login` or `password-login` | Sends payload containing `idToken` or `username` and `password`. |
| **03** | `: AuthController` | `: IMediator` | `Send(command)` | Controller passes `GoogleLoginCommand` or `PasswordLoginCommand` to MediatR pipeline. |
| **04** | `: MediatR Pipeline` | `: ValidationBehavior` | `ValidateAsync(command)` | Validates payload non-empty, email format, or username/password bounds via FluentValidation. |
| **05** | `: ValidationBehavior`| `: AuthCommandHandler` | `Handle(command)` | Invokes handler after validation passes. |
| **06** | `: AuthCommandHandler`| `: IGoogleIdTokenValidator` or `: IPasswordHasher` | Token / Hash Verification | Verifies Google RSA signature or PBKDF2 password hash (210,000 iterations). |
| **07** | `: AuthCommandHandler`| `: OralExamDbContext` | Query `users` & `refresh_tokens` | Checks account existence, active state (`is_active = true`), and extracts roles. |
| **08** | `: AuthCommandHandler`| `: IAccessTokenIssuer` | `GenerateTokens(user)` | Generates 15-minute JWT Access Token and 7-day Refresh Token. |
| **09** | `: AuthCommandHandler`| `: OralExamDbContext` | Save `refresh_tokens` | Hashes Refresh Token with SHA-256 and persists record. |
| **10** | `: AuthCommandHandler`| `: AuthController` | Return `AuthResponse` | Returns tokens, expiration times, user role, and `mustChangePassword` status. |
| **11** | `: AuthController` | `: Login UI` | HTTP 200 OK | Delivers authentication payload to frontend client. |
| **12** | `: Login UI` | `: Local Storage` | Store Token Pair | Stores Access Token in memory/storage and initializes authenticated session. |
| **13** | `: Login UI` | `: Router` | Redirect to Dashboard | Navigates to appropriate dashboard based on assigned role. |

---

### 3.1.4. Class Diagram (`DD-01 Class Diagram`)

> **Draw.io Vector Source:** [`DD01_Auth_Class_Diagram.drawio`](./DD01_Auth_Class_Diagram.drawio)

*Figure 3.1.1 — Authentication, Session Lifecycle and RBAC Class Diagram (`DD-01`).*

---

### 3.1.5. Class Specifications (`DD-01 Class Specifications`)

#### A. Architectural Class Summary Table

| Class Name | Architectural Layer | Package | Single Responsibility |
|:---|:---|:---|:---|
| **`AuthController`** | API | `Controllers/v1` | REST endpoints for Google Login, Password Login, Change Password, and Token Refresh. |
| **`GoogleLoginCommandHandler`** | Application | `Features/Auth` | Handles Google OAuth validation, student auto-provisioning, and token generation. |
| **`PasswordLoginCommandHandler`** | Application | `Features/Auth` | Handles school account login, PBKDF2 hash verification, and role resolution. |
| **`ChangePasswordCommandHandler`** | Application | `Features/Auth` | Validates old password, applies new PBKDF2 hash, and clears must-change flag. |
| **`RefreshTokenCommandHandler`** | Application | `Features/Auth` | Enforces Refresh Token Rotation and detects token replay attacks (RFC 6819). |
| **`PasswordHasher`** | Infrastructure | `Authentication` | Implements PBKDF2-HMAC-SHA256 (210,000 iterations) with constant-time equality check. |
| **`AccessTokenIssuer`** | Infrastructure | `Authentication` | Generates cryptographically signed JWT access tokens with embedded role and permission claims. |
| **`GoogleIdTokenValidator`** | Infrastructure | `Authentication` | Verifies Google ID Token signatures against Google public JWKS endpoints. |
| **`MustChangePasswordMiddleware`** | API | `Middlewares` | Intercepts requests bearing `must_change_password == "true"` with HTTP 403 Forbidden. |

#### B. Method-Level Class Specifications

| Class | Method / Signature | Parameters | Return Type | Behavior Specification & Business Rules |
|:---|:---|:---|:---|:---|
| **`AuthController`** | `GoogleLogin(command)` | `GoogleLoginCommand` | `Task<IActionResult>` | Maps `POST /api/v1/auth/google-login` to MediatR pipeline. Returns HTTP 200 or 401. |
| **`AuthController`** | `PasswordLogin(command)` | `PasswordLoginCommand` | `Task<IActionResult>` | Maps `POST /api/v1/auth/password-login` to MediatR pipeline. Returns HTTP 200, 401, or 403. |
| **`AuthController`** | `ChangePassword(command)` | `ChangePasswordCommand` | `Task<IActionResult>` | Maps `POST /api/v1/auth/change-password`. Requires authenticated user context. |
| **`AuthController`** | `RefreshToken(command)` | `RefreshTokenCommand` | `Task<IActionResult>` | Maps `POST /api/v1/auth/refresh-token`. Returns refreshed token pair or HTTP 401. |
| **`GoogleLoginCommandHandler`** | `Handle(request, ct)` | `GoogleLoginCommand`, `CancellationToken` | `Task<AuthResponse>` | Verifies Google signature, auto-provisions student if absent, issues tokens. |
| **`PasswordLoginCommandHandler`** | `Handle(request, ct)` | `PasswordLoginCommand`, `CancellationToken` | `Task<AuthResponse>` | Verifies username and PBKDF2 hash. Throws `UnauthorizedException` if invalid. |
| **`ChangePasswordCommandHandler`** | `Handle(request, ct)` | `ChangePasswordCommand`, `CancellationToken` | `Task<Unit>` | Enforces new password complexity and distinctness from current password. |
| **`RefreshTokenCommandHandler`** | `Handle(request, ct)` | `RefreshTokenCommand`, `CancellationToken` | `Task<AuthResponse>` | Hashes token with SHA-256; revokes all sessions on replay; rotates token on success. |
| **`PasswordHasher`** | `HashPassword(password)` | `string` | `string` | Generates 16-byte random salt, applies PBKDF2-HMAC-SHA256 (210,000 rounds). |
| **`PasswordHasher`** | `Verify(password, hash)` | `string`, `string` | `bool` | Constant-time verification using `CryptographicOperations.FixedTimeEquals`. |
| **`AccessTokenIssuer`** | `Issue(user)` | `User` | `TokenResult` | Signs JWT with HMAC-SHA256, embeds UserId, Role, Email, and MustChangePassword claims. |
| **`MustChangePasswordMiddleware`**| `InvokeAsync(context, next)` | `HttpContext`, `RequestDelegate` | `Task` | Checks claim `must_change_password`. If true and path is not change-password, returns 403. |

---

### 3.1.6. Exception Handling & Edge Case Matrix (`Table 3.1.2`)

| Scenario ID | Edge Case / Exception Name | Trigger Condition | System Detection & Handling Mechanism | HTTP Status & Response Payload |
|:---:|:---|:---|:---|:---|
| **SC-01** | Invalid Google ID Token | Expired, forged, or invalid Google token. | `GoogleJsonWebSignature.ValidateAsync` throws `InvalidJwtException`. | `HTTP 401 Unauthorized`<br>`{"error": "Invalid or expired Google token"}` |
| **SC-02** | Invalid Username or Password | Incorrect username or mismatched password hash. | `PasswordHasher.Verify` returns `false`. Constant-time comparison mitigates timing attacks. | `HTTP 401 Unauthorized`<br>`{"error": "Invalid username or password"}` |
| **SC-03** | Account Locked or Inactive | User has `is_active = false`. | Handler checks `User.IsActive` before credential verification. | `HTTP 403 Forbidden`<br>`{"error": "Account is locked or deactivated"}` |
| **SC-04** | Must Change Password Enforcement | User with `MustChangePassword = true` accesses business API. | `MustChangePasswordMiddleware` detects claim and intercepts request. | `HTTP 403 Forbidden`<br>`{"error": "Must change password before proceeding"}` |
| **SC-05** | Refresh Token Replay Attack | Client submits a previously revoked Refresh Token. | Handler detects `IsRevoked == true`, immediately revokes all user sessions. | `HTTP 401 Unauthorized`<br>`{"error": "Compromised token detected. All sessions terminated."}` |
| **SC-06** | Expired Refresh Token | Client submits token past `ExpiresAt`. | Handler checks expiration date against UTC now. | `HTTP 401 Unauthorized`<br>`{"error": "Refresh token expired. Please re-authenticate."}` |

---

### 3.1.7. Data Contracts & DTO Specifications (`Table 3.1.3`)

| Field Name | Data Type | Required | Validation Rules | Business Meaning & Concrete Example |
|:---|:---|:---:|:---|:---|
| **`idToken`** | String | Yes | Non-empty, valid JWT format | Google OAuth 2.0 Identity Token provided by Google Identity Services SDK. |
| **`username`** | String | Yes | 3–50 chars, alphanumeric | Username for school-issued staff account (`thanhnq_admin`). |
| **`password`** | String | Yes | Min 8 chars, mixed case, digits | Account password submitted during authentication. |
| **`oldPassword`** | String | Yes | Non-empty | Current password required for verification during password change. |
| **`newPassword`** | String | Yes | Min 8 chars, upper, lower, digit, symbol | New password distinct from old password. |
| **`refreshToken`** | String | Yes | 64-char hex string | Opaque refresh token issued during login. |
| **`accessToken`** | String | Output | Signed JWT string | 15-minute access token bearing user identity and role claims. |
| **`mustChangePassword`** | Boolean | Output | True / False | Flag indicating whether user is restricted to password change. |
| **`role`** | String | Output | One of 6 RBAC roles | Assigned system role (`admin`, `lecturer`, `student`, etc.). |
