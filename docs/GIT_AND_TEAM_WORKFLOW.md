# QUY TRÌNH LÀM VIỆC NHÓM TRÊN GIT (GIT WORKFLOW)
Tài liệu này quy định cách chia nhánh (Branching) và làm việc nhóm để tránh xung đột mã nguồn (Conflict) cho đội ngũ 4 kỹ sư FA26SE166:
- 🧑 **Nguyễn Quang Thành:** Team Leader & Lead Backend Architect
- 🧑 **Nguyễn Trọng Tốt:** Backend Developer, AI Engineer & QA Lead
- 🧑 **Nguyễn Đăng Hải:** DB Specialist & Frontend Developer (phụ trách Database 30 bảng PostgreSQL và dồn toàn lực phát triển Frontend; tuyệt đối không code C# Backend)
- 🧑 **Lê Vũ Hoàng:** Lead Frontend Architect & Fullstack Coordinator

Nhánh làm việc chung hiện tại là nhánh `develop`.

---

## 1. CẤU TRÚC NHÁNH CHUẨN (GITFLOW)
- `main`: Nhánh gốc cực kỳ ổn định. CHỈ code khi deploy lên Server thật báo cáo tốt nghiệp. Không ai được phép push thẳng vào `main`.
- `develop`: Nhánh làm việc chung của cả Team (Nơi ráp nối code FE và BE). Mọi tính năng sau khi làm xong sẽ gộp vào đây. Không được phép code rác hay code hỏng lên nhánh này.
- `feature/<tên-tính-năng>`: Nhánh cá nhân để code tính năng mới.
  - Ví dụ BE: `feature/be-setup-signalr`, `feature/be-rubric-crud`.
  - Ví dụ FE: `feature/fe-login-ui`, `feature/fe-voice-gate`.
- `bugfix/<tên-bug>`: Nhánh sửa lỗi phát sinh khi test. Ví dụ: `bugfix/fe-scorecard-crash`.

---

## 2. QUY TRÌNH TẠO CODE VÀ PULL REQUEST (PR)
**Bước 1: Luôn cập nhật code mới nhất từ team trước khi code**
```bash
git checkout develop
git pull origin develop
```

**Bước 2: Tạo nhánh riêng để làm việc**
```bash
git checkout -b feature/tên-tính-năng-của-bạn
```

**Bước 3: Viết Code và Commit theo chuẩn Conventional Commits (v1.0.0)**

Dự án áp dụng bắt buộc 100% chuẩn **Conventional Commits v1.0.0** bằng **Tiếng Anh**. Dự án đã tích hợp sẵn template `.gitmessage` và Git Hook kiểm soát (`.git/hooks/commit-msg`).

* **Cú pháp bắt buộc:**
  ```text
  <type>(<scope>): <short description in imperative mood>

  [optional body: giải thích nguyên nhân và thay đổi chi tiết]
  [optional footer: liên kết task/issue/breaking change]
  ```

* **Bảng danh mục các `<type>` hợp lệ:**
  - `feat`: Thêm tính năng mới cho người dùng (ví dụ: endpoint mới, cơ chế bốc đề mới).
  - `fix`: Sửa lỗi, vá bug logic hoặc xử lý ngoại lệ.
  - `docs`: Cập nhật tài liệu, API contracts, diagrams, README.
  - `test`: Thêm hoặc cập nhật unit tests, integration tests, stress tests.
  - `refactor`: Tái cấu trúc code (không đổi logic nghiệp vụ, không thêm feature).
  - `perf`: Cải thiện hiệu năng, giảm độ trễ I/O hoặc tối ưu bộ nhớ.
  - `chore`: Cấu hình build, dependencies, migration, CI/CD, seed data.
  - `style`: Thay đổi format code, CSS, không ảnh hưởng logic.

* **Bảng danh mục các `<scope>` theo Bounded Context:**
  - `(practice)`: Luồng MF-01 Luyện tập tương tác.
  - `(mock-exam)`: Luồng MF-02 Thi thử tính giờ.
  - `(rubric)`: Luồng MF-03 Ngân hàng câu hỏi & Rubric Studio.
  - `(official-exam)`: Luồng MF-04 Thi thật Kiosk phòng lab & Hậu kiểm.
  - `(auth)`: Xác thực Google OAuth, RBAC.
  - `(kiosk)`: Khóa màn hình Kiosk phòng Lab, hardware mic, blur detection.
  - `(api)`: Cấu hình middleware, routing, Swagger/OpenAPI.
  - `(db)`: Entity, migrations, schema PostgreSQL.
  - `(storage)`: Lưu trữ audio, bóc băng Whisper STT.

* **Quy tắc vàng khi viết Commit Message:**
  1. **100% Tiếng Anh**, dùng thể mệnh lệnh (Imperative mood: `add`, `update`, `implement`, `fix` — không dùng `added`, `fixed`, `updating`).
  2. Không viết hoa chữ cái đầu sau dấu hai chấm (ví dụ: `feat(practice): add next-question endpoint`, không viết `feat(practice): Add...`).
  3. Không đặt dấu chấm (`.`) ở cuối dòng tiêu đề (subject line).
  4. Giới hạn dòng tiêu đề tối đa 50–72 ký tự.

* **Ví dụ mẫu chuẩn Enterprise:**
  - `feat(practice): update per-question and full-session modes with inactivity timeout`
  - `fix(practice): resolve timeout calculation for inactive sessions`
  - `docs(api): update MF-01 integration guide and error codes`
  - `test(practice): add adversarial stress tests for anti-consecutive difficulty`
  - `chore(db): add SessionInactivityTimeoutMinutes seed data`

**Bước 4: Đẩy code lên và Mở Pull Request**
```bash
git push -u origin feature/tên-tính-năng-của-bạn
```
- Lên Github, bấm nút **Compare & pull request** để gộp nhánh của bạn vào `develop`.
- **YÊU CẦU BẮT BUỘC:** Phải có ít nhất 1 thành viên khác trong team vào đọc code của bạn (Review) và bấm Approve thì mới được phép Merge (Gộp code). Tuyệt đối không tự tạo PR rồi tự Merge.

---

## 3. CÁCH XỬ LÝ CONFLICT (XUNG ĐỘT CODE)
Nếu bạn đẩy code lên và Github báo "Can't automatically merge" (Conflict):
1. Quay lại terminal: `git pull origin develop` (Kéo code mới của thằng bạn vừa push lên máy mình).
2. VS Code sẽ hiện các dòng màu xanh/đỏ (Current Change vs Incoming Change). 
3. Hẹn người code chung ra Discord/Meet nói chuyện: "Giữ đoạn code của tao hay của mày?". Chỉnh sửa bằng tay lại đoạn đó cho đúng.
4. Xong xuôi thì commit lại: `git add .` -> `git commit -m "fix: resolve merge conflicts"` -> `git push`.

---

## 4. QUY TẮC BÀN GIAO MỖI 3 NGÀY
- Chiều hoặc Tối ngày thứ 3, tất cả thành viên phải hoàn tất Pull Request tính năng của mình gộp vào `develop`.
- Lead kéo nhánh `develop` về máy, chạy lệnh `npm run build` và `dotnet build`.
- Nếu Build báo lỗi đỏ: Người nào gây lỗi phải vào fix ngay lập tức. Đảm bảo nhánh `develop` lúc nào cũng phải sống (Runnable) để cả team demo.
