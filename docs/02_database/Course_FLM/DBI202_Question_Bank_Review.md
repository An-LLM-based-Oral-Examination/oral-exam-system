# BẢNG RÀ SOÁT NGÂN HÀNG CÂU HỎI VẤN ĐÁP MÔN DBI202 (DATABASE SYSTEMS)

> **Dự án:** Hệ Thống Luyện Thi & Đánh Giá Vấn Đáp Bằng LLM Cho Ngành Kỹ Thuật Phần Mềm (FA26SE166)
> **Mã môn học:** **DBI202** | **Tên môn học:** Introduction to Databases / Database Systems (Các hệ cơ sở dữ liệu)
> **Tài liệu nguồn:** Syllabus FLM ID 12039 ban hành theo Quyết định 1286/QĐ-ĐHFPT (ĐH FPT)
> **Quy mô ngân hàng:** **62 câu hỏi vấn đáp** (36 câu Kho Luyện tập & Thi thử + 26 câu Kho Thi thật)
> **Tiêu chuẩn kỹ thuật:** 100% câu hỏi có Model Answer chuyên sâu (>= 50 ký tự), 100% có Barem Rubric tổng điểm đúng 10.0đ, Đồng bộ 1-1 đủ 7 CLOs (`CLO1`..`CLO7`), Cô lập 2 kho theo R8, Zero Placeholder.

---

## PHẦN 1: TỔNG QUAN MÔN HỌC VÀ MA TRẬN PHÂN BỔ CÂU HỎI

### 1.1. Thông số Tổng quan Môn học

| Thuộc tính | Chi tiết trong Chương trình Đào tạo FLM |
|---|---|
| **Mã môn học** | **DBI202** |
| **Tên tiếng Anh** | Introduction to Databases / Database Systems |
| **Tên tiếng Việt** | Các hệ cơ sở dữ liệu |
| **Số tín chỉ** | 3 tín chỉ (45 giờ contact slots trên lớp + 1 giờ thi FE + 104 giờ tự học = 150 giờ) |
| **Hệ quản trị CSDL chuẩn** | **Microsoft SQL Server** (T-SQL / SSMS) |
| **Chuẩn đầu ra (FLM & CSDL 3NF)** | Đồng bộ **1-1 đủ 7 CLOs** (`CLO1` đến `CLO7` theo Quyết định `1286/QĐ-ĐHFPT`) |
| **Phân bổ cấu trúc ngân hàng** | **36 câu Luyện tập (`practice_questions`) + 26 câu Thi thật (`exam_questions`) = 62 câu** |

### 1.2. Ánh xạ Chuẩn đầu ra 1-1 (7 FLM CLOs <-> 7 Database 3NF CLOs)

| Mã CLO FLM | Mô tả Chuẩn Đầu Ra theo Syllabus FLM | Mức Bloom | Mã CLO DB (`course_clos`) | GUID Khóa chính CSDL (`course_clos.id`) |
|:---:|---|:---:|:---:|---|
| **CLO1** | Understand database concepts, evolution of database systems, and three-schema DBMS architecture (ANSI-SPARC) | Remember / Understand | **CLO1** | `00000000-0000-0000-0000-000000000661` |
| **CLO2** | Understand the relational data model, relational schemas, integrity constraints, and relational algebra operations | Remember / Understand / Apply | **CLO2** | `00000000-0000-0000-0000-000000000662` |
| **CLO3** | Apply functional dependencies, Armstrong's axioms, attribute closure, normalization (1NF, 2NF, 3NF, BCNF), and lossless-join decomposition | Remember / Understand / Apply / Analyze | **CLO3** | `00000000-0000-0000-0000-000000000663` |
| **CLO4** | Model application data requirements using Entity-Relationship Diagrams (ERD), weak entities, ISA hierarchies, and map ERD to relational schemas | Understand / Apply / Analyze | **CLO4** | `00000000-0000-0000-0000-000000000664` |
| **CLO5** | Write proficient SQL statements including DDL constraints, DML modifications, complex JOINs, aggregations, subqueries, and CTEs in MS SQL Server | Remember / Understand / Apply / Analyze | **CLO5** | `00000000-0000-0000-0000-000000000665` |
| **CLO6** | Develop T-SQL procedural objects including Views, DML Triggers (AFTER / INSTEAD OF), Stored Procedures, User-Defined Functions (UDF), and Cursors | Remember / Understand / Apply / Analyze | **CLO6** | `00000000-0000-0000-0000-000000000666` |
| **CLO7** | Apply B-Tree indexing (Clustered / Non-Clustered), query execution plan optimization, ACID transaction management, isolation levels, and deadlock prevention | Remember / Understand / Apply / Analyze | **CLO7** | `00000000-0000-0000-0000-000000000667` |

### 1.3. Ma trận Phân bổ theo Kho & Độ khó

| Kho câu hỏi | Dễ (Easy) | Trung bình (Medium) | Khó (Hard) | Tổng số câu | Tỷ lệ (%) | Mục đích sử dụng |
|---|:---:|:---:|:---:|:---:|:---:|---|
| **Kho Luyện tập & Thi thử (`practice_questions`)** | 12 | 14 | 10 | **36** | 58.1% | Luyện tập tự do MF-01 & Thi thử tính giờ MF-02 |
| **Kho Thi thật phòng Lab (`exam_questions`)** | 5 | 12 | 9 | **26** | 41.9% | Đề thi oral exam Kiosk MF-04 (Theo 5 blueprint slots) |
| **Tổng cộng toàn ngân hàng** | **17** | **26** | **19** | **62** | **100.0%** | Phủ đều toàn diện 7 CLOs chuyên môn |

### 1.4. Ma trận Phân bổ theo Thang Nhận thức Bloom

| Kho câu hỏi | Nhớ (Remember) | Hiểu (Understand) | Vận dụng (Apply) | Phân tích (Analyze) | Tổng cộng |
|---|:---:|:---:|:---:|:---:|:---:|
| **Kho Luyện tập (`practice_questions`)** | 2 | 19 | 7 | 8 | **36** |
| **Kho Thi thật (`exam_questions`)** | 6 | 8 | 7 | 5 | **26** |
| **Tổng số câu hỏi theo Bloom** | **8** | **27** | **14** | **13** | **62** |
| **Tỷ lệ phần trăm (%)** | 12.9% | 43.5% | 22.6% | 21.0% | **100.0%** |

### 1.5. Ma trận Phân bổ theo 7 Chuẩn Đầu Ra (CLOs FLM & DB 1-1)

| Mã CLO | Tên Chuẩn Đầu Ra FLM | Số câu Luyện tập | Số câu Thi thật | Tổng số câu | Trọng tâm đánh giá |
|---|---|:---:|:---:|:---:|---|
| **CLO1** | Understand database concepts, evolution of database systems, and three-schema DBMS architecture (ANSI-SPARC) | 1 | 1 | **2** | Tiến hóa hệ CSDL (CQ1.1), DBMS, kiến trúc 3 mức ANSI-SPARC, độc lập dữ liệu vật lý & logic |
| **CLO2** | Understand the relational data model, relational schemas, integrity constraints, and relational algebra operations | 2 | 2 | **4** | Lược đồ & thể hiện quan hệ, 5 phép toán Đại số quan hệ cơ bản, biểu diễn ràng buộc toàn vẹn E = ∅ & E1 ⊆ E2 (CQ3.2/3.3) |
| **CLO3** | Apply functional dependencies, Armstrong's axioms, attribute closure, normalization (1NF, 2NF, 3NF, BCNF), and lossless-join decomposition | 6 | 6 | **12** | Phụ thuộc hàm, tiên đề Armstrong, bao đóng X+, phủ tối tiểu Fc, chuẩn hóa 1NF/2NF/3NF/BCNF, Lossless Join & Dependency Preservation |
| **CLO4** | Model application data requirements using Entity-Relationship Diagrams (ERD), weak entities, ISA hierarchies, and map ERD to relational schemas | 5 | 2 | **7** | Thực thể, thuộc tính đa trị, bội số 1:1/1:N/M:N, thực thể yếu, quan hệ bậc 3, kế thừa ISA và ánh xạ sang lược đồ quan hệ |
| **CLO5** | Write proficient SQL statements including DDL constraints, DML modifications, complex JOINs, aggregations, subqueries, and CTEs in MS SQL Server | 11 | 4 | **15** | Ràng buộc PK/FK/CHECK, DELETE vs TRUNCATE, thứ tự thực thi SQL, JOINs, GROUP BY/HAVING, Correlated Subquery, CTE & Window Functions |
| **CLO6** | Develop T-SQL procedural objects including Views, DML Triggers (AFTER / INSTEAD OF), Stored Procedures, User-Defined Functions (UDF), and Cursors | 5 | 6 | **11** | Updatable Views (CHECK OPTION/SCHEMABINDING), DML Triggers (inserted/deleted), Stored Procedures, Scalar UDF vs Inline TVF, Cursor vs Set-based |
| **CLO7** | Apply B-Tree indexing (Clustered / Non-Clustered), query execution plan optimization, ACID transaction management, isolation levels, and deadlock prevention | 6 | 5 | **11** | Clustered/Non-Clustered B-Tree Index, ESR Composite Index, ACID & WAL, Isolation Levels, RCSI, Lock Escalation & chống Deadlock |
| **TỔNG** | **Toàn bộ 7 Chuẩn Đầu Ra Môn Học (1-1)** | **36** | **26** | **62** | **Phủ kín 60 Sessions và 30 CQs của Syllabus** |

---

## PHẦN 2: BẢNG CHỈ MỤC MASTER DANH MỤC 62 CÂU HỎI VẤN ĐÁP

| STT | Mã câu hỏi | Tiêu đề câu hỏi | Kho | CLO FLM | CLO DB | Mức Bloom | Độ khó | Buổi học (Slot) |
|:---:|---|---|:---:|:---:|:---:|:---:|:---:|---|
| 1 | `DBI202-PRAC-001` | Khái niệm Hệ quản trị cơ sở dữ liệu (DBMS) và Kiến trúc 3 mức ANSI-SPARC | Practice | CLO1 | CLO1 | Understand | Easy | Session 1 (Slot 1) |
| 2 | `DBI202-PRAC-002` | Phân biệt Lược đồ quan hệ (Schema) và Thể hiện quan hệ (Instance) | Practice | CLO2 | CLO2 | Understand | Easy | Session 4 (Slot 2) |
| 3 | `DBI202-PRAC-003` | Các phép toán cơ bản trong Đại số quan hệ: Selection và Projection | Practice | CLO2 | CLO2 | Understand | Easy | Session 5 (Slot 3) |
| 4 | `DBI202-PRAC-004` | Phân loại thuộc tính trong Sơ đồ Thực thể Kết hợp (ERD) | Practice | CLO4 | CLO4 | Understand | Easy | Session 19 (Slot 10) |
| 5 | `DBI202-PRAC-005` | Bội số kết hợp (Multiplicity) trong mô hình ERD: 1:1, 1:N và M:N | Practice | CLO4 | CLO4 | Understand | Easy | Session 20 (Slot 10) |
| 6 | `DBI202-PRAC-006` | Khái niệm Khóa chính (Primary Key) và Khóa ngoại (Foreign Key) | Practice | CLO5 | CLO5 | Remember | Easy | Session 29 (Slot 15) |
| 7 | `DBI202-PRAC-007` | Các toán tử lọc dữ liệu BETWEEN, IN, LIKE (Wildcards) và phân trang OFFSET-FETCH trong T-SQL | Practice | CLO5 | CLO5 | Understand | Easy | Session 31-32 (Slot 16) |
| 8 | `DBI202-PRAC-008` | Xử lý giá trị NULL trong SQL và toán tử IS NULL / IS NOT NULL | Practice | CLO5 | CLO5 | Understand | Easy | Session 32 (Slot 16) |
| 9 | `DBI202-PRAC-009` | Định nghĩa Dạng chuẩn 1 (1NF) và điều kiện nguyên tử của thuộc tính | Practice | CLO3 | CLO3 | Understand | Easy | Session 13 (Slot 7) |
| 10 | `DBI202-PRAC-010` | Định nghĩa 4 thuộc tính ACID trong Giao dịch (Transaction) | Practice | CLO7 | CLO7 | Remember | Easy | Session 43 (Slot 22) |
| 11 | `DBI202-PRAC-011` | Khái niệm và mục đích sử dụng Khung nhìn (View) trong cơ sở dữ liệu | Practice | CLO6 | CLO6 | Understand | Easy | Session 51 (Slot 26) |
| 12 | `DBI202-PRAC-012` | Phân biệt ngôn ngữ DDL và DML trong SQL | Practice | CLO5 | CLO5 | Understand | Easy | Session 27 (Slot 14) |
| 13 | `DBI202-PRAC-013` | Phân biệt Phép kết nối trong (INNER JOIN) và Phép kết nối ngoài (OUTER JOIN) | Practice | CLO5 | CLO5 | Understand | Medium | Session 33-34 (Slot 17) |
| 14 | `DBI202-PRAC-014` | Phân biệt mệnh đề WHERE và HAVING khi kết hợp với GROUP BY | Practice | CLO5 | CLO5 | Apply | Medium | Session 37-38 (Slot 19) |
| 15 | `DBI202-PRAC-015` | Phân biệt hàm COUNT(*) và COUNT(tên_cột) khi bảng chứa giá trị NULL | Practice | CLO5 | CLO5 | Understand | Medium | Session 37 (Slot 19) |
| 16 | `DBI202-PRAC-016` | Thuật toán tính bao đóng của tập thuộc tính (Attribute Closure X+) | Practice | CLO3 | CLO3 | Apply | Medium | Session 10 (Slot 5) |
| 17 | `DBI202-PRAC-017` | Phân biệt Phụ thuộc hàm đầy đủ và Phụ thuộc hàm bộ phận trong 2NF | Practice | CLO3 | CLO3 | Understand | Medium | Session 13 (Slot 7) |
| 18 | `DBI202-PRAC-018` | Ba dạng dị thường dữ liệu (Insertion, Deletion, Update Anomalies) | Practice | CLO3 | CLO3 | Understand | Medium | Session 12 (Slot 6) |
| 19 | `DBI202-PRAC-019` | Quy tắc chuyển đổi mối kết hợp Nhiều - Nhiều (M:N) từ ERD sang Lược đồ quan hệ | Practice | CLO4 | CLO4 | Apply | Medium | Session 21 (Slot 11) |
| 20 | `DBI202-PRAC-020` | Khái niệm Thực thể yếu (Weak Entity) và Khóa bộ phận (Discriminator) | Practice | CLO4 | CLO4 | Understand | Medium | Session 21 (Slot 11) |
| 21 | `DBI202-PRAC-021` | Phân biệt Clustered Index và Non-Clustered Index trong MS SQL Server | Practice | CLO7 | CLO7 | Understand | Medium | Session 41 (Slot 21) |
| 22 | `DBI202-PRAC-022` | Bảng ảo inserted và deleted trong cơ chế hoạt động của DML Trigger | Practice | CLO6 | CLO6 | Understand | Medium | Session 49 (Slot 25) |
| 23 | `DBI202-PRAC-023` | Phân biệt Stored Procedure và User-Defined Function (UDF) | Practice | CLO6 | CLO6 | Understand | Medium | Session 51-54 (Slot 26-27) |
| 24 | `DBI202-PRAC-024` | Tùy chọn xử lý vi phạm khóa ngoại: ON DELETE CASCADE vs ON DELETE SET NULL | Practice | CLO5 | CLO5 | Apply | Medium | Session 29 (Slot 15) |
| 25 | `DBI202-PRAC-025` | Phân biệt câu lệnh DELETE và TRUNCATE TABLE | Practice | CLO5 | CLO5 | Understand | Medium | Session 39-40 (Slot 20) |
| 26 | `DBI202-PRAC-026` | Kỹ thuật tự kết nối (SELF JOIN) để biểu diễn mối quan hệ đệ quy | Practice | CLO5 | CLO5 | Apply | Medium | Session 34 (Slot 17) |
| 27 | `DBI202-PRAC-027` | Truy vấn con tương quan (Correlated Subquery) và tối ưu hóa với toán tử EXISTS | Practice | CLO5 | CLO5 | Analyze | Hard | Session 36 (Slot 18) |
| 28 | `DBI202-PRAC-028` | Điều kiện Dạng chuẩn 3 (3NF) vs Dạng chuẩn Boyce-Codd (BCNF) | Practice | CLO3 | CLO3 | Analyze | Hard | Session 14 (Slot 7) |
| 29 | `DBI202-PRAC-029` | Kiểm tra tính Bảo toàn Phụ thuộc hàm (Dependency Preservation) và Sự đánh đổi giữa 3NF với BCNF | Practice | CLO3 | CLO3 | Analyze | Hard | Session 11-14 (Slot 6-7) |
| 30 | `DBI202-PRAC-030` | Phân tích 4 mức cô lập giao dịch (Transaction Isolation Levels) và hiện tượng Phantom Read | Practice | CLO7 | CLO7 | Analyze | Hard | Session 44 (Slot 22) |
| 31 | `DBI202-PRAC-031` | Bản chất Deadlock trong cơ sở dữ liệu và các kỹ thuật phòng chống / giải quyết | Practice | CLO7 | CLO7 | Analyze | Hard | Session 44 (Slot 22) |
| 32 | `DBI202-PRAC-032` | Các trường hợp vô hiệu hóa chỉ mục B-Tree (Index Invalidation) và giải pháp khắc phục | Practice | CLO7 | CLO7 | Analyze | Hard | Session 42 (Slot 21) |
| 33 | `DBI202-PRAC-033` | So sánh Hàm Vô hướng (Scalar UDF) và Hàm Giá trị Bảng Nội tuyến (Inline TVF) trong T-SQL & Tối ưu hóa Hiệu năng | Practice | CLO6 | CLO6 | Apply | Hard | Session 52-54 (Slot 26-27) |
| 34 | `DBI202-PRAC-034` | Phân tích ưu nhược điểm của 3 chiến lược ánh xạ mối quan hệ kế thừa ISA sang CSDL quan hệ | Practice | CLO4 | CLO4 | Analyze | Hard | Session 22 (Slot 11) |
| 35 | `DBI202-PRAC-035` | Tác động của Cursor đến hiệu năng hệ thống và giải pháp chuyển đổi sang tư duy tập hợp (Set-based) | Practice | CLO6 | CLO6 | Analyze | Hard | Session 53 (Slot 27) |
| 36 | `DBI202-PRAC-036` | Thiết kế và quản lý Giao dịch lồng ghép (Nested Transactions) và Điểm lưu (Savepoints) trong T-SQL | Practice | CLO7 | CLO7 | Apply | Hard | Session 43-44 (Slot 22) |
| 37 | `DBI202-EXAM-001` | Các giai đoạn Tiến hóa của Hệ CSDL và 5 Nhược điểm Cốt lõi của Hệ thống Quản lý Tệp phẳng (CQ1.1) | Exam | CLO1 | CLO1 | Remember | Easy | Session 1-2 (Slot 1) |
| 38 | `DBI202-EXAM-002` | Định nghĩa và Ký hiệu hình thức của 5 phép toán cơ bản trong Đại số quan hệ | Exam | CLO2 | CLO2 | Remember | Easy | Session 5 (Slot 3) |
| 39 | `DBI202-EXAM-003` | Hệ tiên đề Armstrong cho Phụ thuộc hàm | Exam | CLO3 | CLO3 | Remember | Medium | Session 10 (Slot 5) |
| 40 | `DBI202-EXAM-004` | Định nghĩa Khóa chính, Siêu khóa và Khóa ứng viên trong lý thuyết CSDL | Exam | CLO3 | CLO3 | Remember | Easy | Session 11 (Slot 6) |
| 41 | `DBI202-EXAM-005` | Danh mục Ràng buộc Toàn vẹn Dữ liệu chuẩn trong SQL DDL | Exam | CLO5 | CLO5 | Remember | Easy | Session 29-30 (Slot 15) |
| 42 | `DBI202-EXAM-006` | Định nghĩa và Phân loại 2 dạng DML Triggers chính trong MS SQL Server | Exam | CLO6 | CLO6 | Remember | Easy | Session 49 (Slot 25) |
| 43 | `DBI202-EXAM-007` | Cơ chế ánh xạ mối kết hợp Một - Một (1:1) từ ERD sang Lược đồ quan hệ | Exam | CLO4 | CLO4 | Understand | Medium | Session 20 (Slot 10) |
| 44 | `DBI202-EXAM-008` | Biểu diễn Ràng buộc Toàn vẹn trên Lược đồ Quan hệ bằng Đại số Quan hệ (CQ3.2 & CQ3.3) | Exam | CLO2 | CLO2 | Understand | Medium | Session 7-8 (Slot 4) |
| 45 | `DBI202-EXAM-009` | Khái niệm Phủ tối tiểu (Minimal Cover / Canonical Cover) và Quy trình 3 Bước Thu gọn Tập Phụ thuộc Hàm | Exam | CLO3 | CLO3 | Understand | Medium | Session 10-11 (Slot 5-6) |
| 46 | `DBI202-EXAM-010` | Thứ tự Thực thi Logic của các mệnh đề trong Câu truy vấn SQL | Exam | CLO5 | CLO5 | Understand | Medium | Session 31-38 (Slot 16-19) |
| 47 | `DBI202-EXAM-011` | Cơ chế hoạt động của Cây B-Tree trong Clustered Index và Tìm kiếm Bản ghi | Exam | CLO7 | CLO7 | Understand | Medium | Session 41 (Slot 21) |
| 48 | `DBI202-EXAM-012` | Bảo đảm Tính Nguyên tố (Atomicity) và Bền vững (Durability) qua Write-Ahead Logging (WAL) | Exam | CLO7 | CLO7 | Understand | Medium | Session 43 (Slot 22) |
| 49 | `DBI202-EXAM-013` | Điều kiện Khung nhìn Có thể Cập nhật (Updatable Views), WITH CHECK OPTION và WITH SCHEMABINDING (CQ7.3) | Exam | CLO6 | CLO6 | Understand | Medium | Session 45 & 51 (Slot 23 & 26) |
| 50 | `DBI202-EXAM-014` | Cơ chế Tái sử dụng Kế hoạch Thực thi (Execution Plan Cache) của Stored Procedure | Exam | CLO6 | CLO6 | Understand | Medium | Session 54 (Slot 27) |
| 51 | `DBI202-EXAM-015` | Ánh xạ Thực thể Kết hợp (Associative Entity), Mối kết hợp Bậc 3 (Ternary) và Thuộc tính Đa trị từ ERD sang Lược đồ Quan hệ | Exam | CLO4 | CLO4 | Apply | Medium | Session 20-22 (Slot 10-11) |
| 52 | `DBI202-EXAM-016` | Xác định tất cả các Khóa ứng viên (Candidate Keys) của Quan hệ | Exam | CLO3 | CLO3 | Apply | Medium | Session 11 (Slot 6) |
| 53 | `DBI202-EXAM-017` | Phân rã Chuẩn hóa Lược đồ đạt Dạng chuẩn 3 (3NF) Bảo toàn Phụ thuộc | Exam | CLO3 | CLO3 | Apply | Hard | Session 11-14 (Slot 6-7) |
| 54 | `DBI202-EXAM-018` | Viết câu truy vấn SQL tìm Khách hàng có Tổng giá trị đơn hàng cao nhất trong từng Thành phố | Exam | CLO5 | CLO5 | Apply | Hard | Session 36 (Slot 18) |
| 55 | `DBI202-EXAM-019` | Thiết kế câu lệnh DDL với các ràng buộc CHECK và FOREIGN KEY phức tạp | Exam | CLO5 | CLO5 | Apply | Medium | Session 29-30 (Slot 15) |
| 56 | `DBI202-EXAM-020` | Viết DML AFTER Trigger tự động cập nhật Tồn kho và Hủy đơn khi Thiếu hàng | Exam | CLO6 | CLO6 | Apply | Hard | Session 50 (Slot 25) |
| 57 | `DBI202-EXAM-021` | Xây dựng Stored Procedure Thực hiện Giao dịch Chuyển tiền An toàn | Exam | CLO6 | CLO6 | Apply | Hard | Session 54 (Slot 27) |
| 58 | `DBI202-EXAM-022` | Phân tích và Chứng minh Phép phân rã Kết nối Không Mất Thông tin (Lossless Join) | Exam | CLO3 | CLO3 | Analyze | Hard | Session 11-14 (Slot 6-7) |
| 59 | `DBI202-EXAM-023` | Phân tích Tranh chấp Dữ liệu giữa Hai Giao dịch và Đề xuất Mức Cô lập Tối ưu | Exam | CLO7 | CLO7 | Analyze | Hard | Session 44 (Slot 22) |
| 60 | `DBI202-EXAM-024` | Phân tích Kế hoạch Thực thi, Phát hiện Table Scan và Thiết kế Composite Index theo ESR | Exam | CLO7 | CLO7 | Analyze | Hard | Session 42 (Slot 21) |
| 61 | `DBI202-EXAM-025` | Phân tích Độ mịn của Khóa (Lock Granularity), Hiện tượng Leo thang Khóa (Lock Escalation) và Cơ chế RCSI trong SQL Server | Exam | CLO7 | CLO7 | Analyze | Hard | Session 43-44 & 55-56 (Slot 22 & 28) |
| 62 | `DBI202-EXAM-026` | So sánh Toàn diện giữa INSTEAD OF Trigger trên View Phức tạp và Stored Procedure | Exam | CLO6 | CLO6 | Analyze | Hard | Session 49-54 (Slot 25-27) |

---

## PHẦN 3: CHI TIẾT 36 CÂU HỎI KHO LUYỆN TẬP & THI THỬ (`practice_questions`)

> **Đặc điểm luồng MF-01 / MF-02:**
> - Bốc câu hỏi linh hoạt phục vụ sinh viên ôn luyện theo chủ đề, độ khó đơn hoặc tổ hợp.
> - Mỗi câu hỏi có Câu hỏi phản biện nối tiếp (`FollowUpPrompt`) để AI kích hoạt khi điểm số câu trả lời ban đầu nằm trong dải [4.0, 8.0].
> - Barem Rubric chuẩn hóa 10.0đ (gồm 3-4 tiêu chí con) giúp AI chấm điểm tức thì và sinh CoT giải trình minh bạch theo đúng 7 CLOs.

### 1. `DBI202-PRAC-001`: Khái niệm Hệ quản trị cơ sở dữ liệu (DBMS) và Kiến trúc 3 mức ANSI-SPARC

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-001` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO1** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO1** (`00000000-0000-0000-0000-000000000661`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 1 (Slot 1) — The Worlds of Database Systems |
| **Mục tiêu sư phạm** | Đánh giá mức độ hiểu biết cơ bản về vai trò của DBMS và kiến trúc phân tầng dữ liệu ANSI-SPARC. |

**Nội dung câu hỏi phát vấn:**
> Hệ quản trị cơ sở dữ liệu (DBMS) là gì? Hãy nêu 3 cấp độ trong kiến trúc ANSI-SPARC và giải thích tại sao kiến trúc này lại đảm bảo được tính độc lập dữ liệu (Data Independence)?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu người dùng thay đổi chỉ mục (Index) hoặc cấu trúc lưu trữ tệp trên đĩa cứng thì các ứng dụng bên ngoài có bị ảnh hưởng hay phải biên dịch lại không? Thuộc tính độc lập dữ liệu nào thể hiện điều này?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Hệ quản trị cơ sở dữ liệu (DBMS) là phần mềm trung gian quản lý việc định nghĩa, tạo lập, lưu trữ, truy xuất và kiểm soát an ninh, toàn vẹn của cơ sở dữ liệu. Kiến trúc ANSI-SPARC gồm 3 mức: (1) Mức ngoài (External/View Level) là khung nhìn dữ liệu của từng nhóm người dùng; (2) Mức khái niệm (Conceptual/Logical Level) mô tả toàn bộ cấu trúc dữ liệu, mối quan hệ và ràng buộc nghiệp vụ tổng thể của doanh nghiệp; (3) Mức trong (Internal/Physical Level) mô tả cách dữ liệu được tổ chức lưu trữ vật lý trên đĩa (tệp tin, chỉ mục, khối lưu trữ). Kiến trúc này đảm bảo tính độc lập dữ liệu thông qua 2 cấp: Độc lập dữ liệu vật lý (Physical Data Independence - thay đổi mức trong như tạo thêm Index không ảnh hưởng đến mức khái niệm và ứng dụng) và Độc lập dữ liệu logic (Logical Data Independence - thêm thực thể, thuộc tính ở mức khái niệm không làm thay đổi các View ngoài hiện có).

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa DBMS & Vai trò cốt lõi** | Nêu rõ khái niệm DBMS là hệ thống phần mềm quản lý, lưu trữ, truy vấn và bảo đảm an toàn dữ liệu. | Understand | **3.0đ** |
| **Trình bày 3 cấp độ kiến trúc ANSI-SPARC** | Mô tả chính xác đặc điểm và ranh giới của Mức ngoài (External), Mức khái niệm (Conceptual) và Mức trong (Internal). | Understand | **4.0đ** |
| **Phân tích tính độc lập dữ liệu (Logical & Physical)** | Giải thích rõ ràng cơ chế phân tách tầng giúp ứng dụng không bị phụ thuộc vào cấu trúc lưu trữ đĩa hay mở rộng schema. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 2. `DBI202-PRAC-002`: Phân biệt Lược đồ quan hệ (Schema) và Thể hiện quan hệ (Instance)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-002` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO2** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO2** (`00000000-0000-0000-0000-000000000662`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 4 (Slot 2) — The Relational Model of Data |
| **Mục tiêu sư phạm** | Kiểm tra khả năng phân biệt cấu trúc tĩnh (Metadata) và trạng thái động (Data rows) trong mô hình quan hệ. |

**Nội dung câu hỏi phát vấn:**
> Trong mô hình quan hệ, hãy phân biệt giữa Lược đồ quan hệ (Relation Schema) và Thể hiện quan hệ (Relation Instance). Cho ví dụ cụ thể để minh họa sự khác nhau về tính biến đổi theo thời gian giữa hai khái niệm này.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Khi một câu lệnh INSERT hoặc DELETE được thực thi thành công trên bảng, thành phần nào bị thay đổi: Schema hay Instance?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Lược đồ quan hệ (Relation Schema) là thiết kế cấu trúc tĩnh bao gồm tên quan hệ, danh sách các thuộc tính (cột) kèm miền giá trị (kiểu dữ liệu) và các ràng buộc toàn vẹn; Schema ít khi thay đổi trong suốt vòng đời hoạt động của hệ thống (ví dụ: Student(StudentID: INT, FullName: NVARCHAR(100), GPA: FLOAT)). Ngược lại, Thể hiện quan hệ (Relation Instance) là trạng thái dữ liệu động tại một thời điểm cụ thể, bao gồm tập hợp các bộ dữ liệu (bản ghi/dòng) hiện có trong bảng. Khi người dùng thực hiện thao tác DML như INSERT, UPDATE, DELETE thì chỉ làm thay đổi Thể hiện quan hệ (Instance) mà không làm biến đổi Lược đồ (Schema). Lược đồ chỉ thay đổi khi quản trị viên thực thi lệnh DDL như ALTER TABLE thêm hoặc xóa cột.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa chính xác Relation Schema** | Nêu rõ Schema là cấu trúc tĩnh, metadata mô tả bảng, cột, kiểu dữ liệu và ràng buộc. | Understand | **3.0đ** |
| **Định nghĩa chính xác Relation Instance** | Nêu rõ Instance là trạng thái snapshot tập hợp các dòng dữ liệu cụ thể tại một thời điểm. | Understand | **3.5đ** |
| **So sánh tính biến đổi và ví dụ minh họa** | Đưa ra ví dụ thực tế chỉ rõ DDL thay đổi Schema còn DML thay đổi Instance một cách chính xác. | Understand | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 3. `DBI202-PRAC-003`: Các phép toán cơ bản trong Đại số quan hệ: Selection và Projection

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-003` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO2** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO2** (`00000000-0000-0000-0000-000000000662`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 5 (Slot 3) — Algebraic Query Language |
| **Mục tiêu sư phạm** | Kiểm tra kiến thức đại số hình thức về phép lọc ngang (Selection) và phép lọc dọc (Projection). |

**Nội dung câu hỏi phát vấn:**
> Hãy nêu ký hiệu hình thức, cú pháp và mục đích của hai phép toán cơ bản trong Đại số quan hệ: Phép chọn (Selection) và Phép chiếu (Projection). Chúng tương ứng với những mệnh đề nào trong câu lệnh SELECT của SQL?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Tại sao trong đại số quan hệ thuần túy, phép chiếu luôn tự động loại bỏ các dòng trùng lặp, trong khi câu lệnh SELECT của SQL lại giữ lại bản ghi trùng lặp trừ khi có từ khóa DISTINCT?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Phép chọn (Selection, ký hiệu sigma σ_condition(R)) là phép lọc theo chiều ngang, trả về tập các bộ (dòng) thỏa mãn điều kiện vị từ vị ngữ, tương ứng với mệnh đề WHERE trong SQL; kết quả giữ nguyên bậc (số cột) của quan hệ gốc. Phép chiếu (Projection, ký hiệu pi π_attributes(R)) là phép lọc theo chiều dọc, trích xuất danh sách các thuộc tính (cột) chỉ định từ quan hệ, tương ứng với danh sách cột sau mệnh đề SELECT trong SQL. Trong đại số quan hệ thuần túy dựa trên lý thuyết tập hợp (Set Theory), mọi quan hệ là một tập hợp nên không chấp nhận phần tử trùng lặp; do đó phép chiếu luôn ngầm định khử trùng lặp. Trong khi đó SQL sử dụng ngữ nghĩa đa tập (Multiset/Bag Semantics) để tối ưu hiệu năng tính toán, nên chỉ khử trùng lặp khi có từ khóa DISTINCT tường minh.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Bản chất và ký hiệu Phép chọn (Selection)** | Nêu đúng ký hiệu σ, lọc dòng thỏa mãn điều kiện và tương ứng với WHERE trong SQL. | Understand | **3.5đ** |
| **Bản chất và ký hiệu Phép chiếu (Projection)** | Nêu đúng ký hiệu π, lọc cột chỉ định và tương ứng với mệnh đề SELECT trong SQL. | Understand | **3.5đ** |
| **Phân biệt Set Semantics vs Bag Semantics** | Giải thích được cơ chế loại bỏ trùng lặp trong đại số quan hệ hình thức so với cơ chế lưu trữ của SQL. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 4. `DBI202-PRAC-004`: Phân loại thuộc tính trong Sơ đồ Thực thể Kết hợp (ERD)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-004` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO4** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO4** (`00000000-0000-0000-0000-000000000664`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 19 (Slot 10) — High-Level Database Models (ERD) |
| **Mục tiêu sư phạm** | Đánh giá khả năng nhận diện các loại thuộc tính khác nhau trong phân tích thiết kế ERD. |

**Nội dung câu hỏi phát vấn:**
> Trong mô hình ERD, hãy phân biệt: Thuộc tính đơn (Simple), Thuộc tính phức hợp (Composite), Thuộc tính đa trị (Multi-valued) và Thuộc tính phái sinh (Derived). Cho ví dụ thực tế cho từng loại.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Khi chuyển đổi sơ đồ ERD sang mô hình cơ sở dữ liệu quan hệ, thuộc tính đa trị (như số điện thoại của khách hàng) được xử lý như thế nào để không vi phạm dạng chuẩn 1 (1NF)?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> (1) Thuộc tính đơn (Simple): Không thể phân chia nhỏ hơn, ví dụ: Tuổi, Giới tính, Điểm số. (2) Thuộc tính phức hợp (Composite): Bao gồm nhiều thành phần con có ý nghĩa độc lập, ví dụ: Họ tên (gồm Họ, Tên lót, Tên) hoặc Địa chỉ (gồm Số nhà, Tên đường, Phường, Thành phố). (3) Thuộc tính đa trị (Multi-valued, ký hiệu hình oval nét đôi): Có thể chứa đồng thời nhiều giá trị cho một thực thể duy nhất, ví dụ: Số điện thoại hoặc Kỹ năng của nhân viên. (4) Thuộc tính phái sinh (Derived, ký hiệu hình oval nét đứt): Giá trị không cần lưu trữ cứng mà được tính toán suy luận từ thuộc tính khác, ví dụ: Tuổi được suy ra từ Ngày sinh (CurrentDate - DateOfBirth). Khi chuyển đổi sang CSDL quan hệ, để bảo đảm 1NF, thuộc tính đa trị phải được tách thành một bảng phụ riêng chứa khóa chính của thực thể cha kèm giá trị thuộc tính đa trị, tạo thành khóa chính phức hợp.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Phân biệt Thuộc tính Đơn và Thuộc tính Phức hợp** | Nêu đúng khái niệm, khả năng chia nhỏ của thuộc tính và đưa ví dụ thực tế chuẩn mực. | Understand | **3.0đ** |
| **Phân biệt Thuộc tính Đa trị và Thuộc tính Phái sinh** | Nêu đúng ký hiệu quy ước trong ERD, đặc điểm chứa nhiều giá trị và cơ chế tính toán giá trị phái sinh. | Understand | **3.5đ** |
| **Quy tắc ánh xạ thuộc tính đa trị sang bảng quan hệ** | Giải thích rõ việc tách thành bảng phụ riêng để bảo toàn dạng chuẩn 1 (1NF). | Apply | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 5. `DBI202-PRAC-005`: Bội số kết hợp (Multiplicity) trong mô hình ERD: 1:1, 1:N và M:N

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-005` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO4** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO4** (`00000000-0000-0000-0000-000000000664`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 20 (Slot 10) — High-Level Database Models (ERD) |
| **Mục tiêu sư phạm** | Kiểm tra sự hiểu biết về các ràng buộc bản số (Cardinality/Multiplicity) trong liên kết giữa các thực thể. |

**Nội dung câu hỏi phát vấn:**
> Bội số kết hợp (Multiplicity) trong mô hình ERD là gì? Hãy trình bày 3 loại mối kết hợp phổ biến (1:1, 1:N, M:N) và cho ví dụ thực tế tương ứng trong một hệ thống quản lý trường đại học.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Trong quan hệ 1:N giữa Khoa (Department) và Giảng viên (Lecturer), khi ánh xạ sang bảng quan hệ vật lý, khóa ngoại (Foreign Key) sẽ được đặt ở bảng nào? Tại sao?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Bội số kết hợp (Multiplicity) xác định số lượng thực thể của một tập thực thể có thể liên kết với một thực thể của tập thực thể khác thông qua mối quan hệ. (1) Quan hệ Một - Một (1:1): Mỗi thực thể bên A liên kết với tối đa 1 thực thể bên B và ngược lại, ví dụ: Mỗi Trưởng khoa quản lý 1 Khoa và mỗi Khoa chỉ do 1 Trưởng khoa đứng đầu. (2) Quan hệ Một - Nhiều (1:N): Một thực thể bên A liên kết với nhiều thực thể bên B, nhưng mỗi thực thể B chỉ thuộc về duy nhất 1 thực thể A, ví dụ: Một Khoa có nhiều Giảng viên công tác, nhưng mỗi Giảng viên chỉ thuộc 1 Khoa. Khi ánh xạ sang quan hệ vật lý, khóa chính của bên 1 (DepartmentID) sẽ được đưa sang bên N (bảng Lecturer) làm khóa ngoại để lưu trữ tham chiếu không bị dư thừa. (3) Quan hệ Nhiều - Nhiều (M:N): Một thực thể bên A liên kết với nhiều bên B và ngược lại, ví dụ: Một Sinh viên đăng ký nhiều Môn học và một Môn học có nhiều Sinh viên theo học.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Khái niệm Multiplicity trong ERD** | Nêu đúng định nghĩa bản số và vai trò ràng buộc số lượng thể hiện trong mối quan hệ. | Understand | **3.0đ** |
| **Trình bày chi tiết 3 loại quan hệ kèm ví dụ** | Giải thích rõ ràng 1:1, 1:N, M:N với các ví dụ thực tiễn trong môi trường quản lý đào tạo. | Understand | **4.0đ** |
| **Quy tắc đặt Khóa ngoại cho quan hệ 1:N** | Giải thích chính xác lý do đặt khóa chính bên 1 sang bảng bên N làm khóa ngoại. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 6. `DBI202-PRAC-006`: Khái niệm Khóa chính (Primary Key) và Khóa ngoại (Foreign Key)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-006` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Remember** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 29 (Slot 15) — SQL Constraints on Attributes |
| **Mục tiêu sư phạm** | Kiểm tra kiến thức cốt lõi về toàn vẹn thực thể và toàn vẹn tham chiếu trong cơ sở dữ liệu quan hệ. |

**Nội dung câu hỏi phát vấn:**
> Khóa chính (Primary Key) và Khóa ngoại (Foreign Key) là gì? Khóa chính bảo đảm quy tắc toàn vẹn nào và Khóa ngoại bảo đảm quy tắc toàn vẹn nào trong mô hình quan hệ?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Một khóa chính có được phép nhận giá trị NULL không? Một khóa ngoại có được phép nhận giá trị NULL không? Giải thích ý nghĩa thực tế khi khóa ngoại mang giá trị NULL."*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Khóa chính (Primary Key) là một hoặc một nhóm thuộc tính được chọn để định danh duy nhất mỗi bộ (dòng) trong bảng; nó bảo đảm Quy tắc toàn vẹn thực thể (Entity Integrity Constraint) - giá trị khóa chính phải duy nhất và tuyệt đối cấm mang giá trị NULL. Khóa ngoại (Foreign Key) là một hoặc một tập thuộc tính trong một bảng tham chiếu trực tiếp đến khóa chính (hoặc khóa ứng viên duy nhất) của một bảng khác (hoặc chính bảng đó); nó bảo đảm Quy tắc toàn vẹn tham chiếu (Referential Integrity Constraint) - mọi giá trị trong cột khóa ngoại phải tồn tại trong cột khóa chính được tham chiếu, HOẶC mang giá trị NULL. Khóa ngoại mang giá trị NULL thể hiện rằng thực thể hiện tại chưa có mối liên kết với thực thể cha nào (ví dụ: một nhân viên mới vào công ty chưa được phân bổ về phòng ban nào, nên DepartmentID của nhân viên đó là NULL).

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa Khóa chính & Toàn vẹn thực thể** | Nêu đúng khái niệm Primary Key, tính duy nhất và quy tắc bắt buộc NOT NULL. | Remember | **3.5đ** |
| **Định nghĩa Khóa ngoại & Toàn vẹn tham chiếu** | Nêu đúng khái niệm Foreign Key, quan hệ tham chiếu và bảo vệ tính nhất quán liên bảng. | Remember | **3.5đ** |
| **Ý nghĩa và quy định giá trị NULL** | Giải thích chính xác PK cấm NULL và FK được phép mang NULL biểu thị chưa gán liên kết. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 7. `DBI202-PRAC-007`: Các toán tử lọc dữ liệu BETWEEN, IN, LIKE (Wildcards) và phân trang OFFSET-FETCH trong T-SQL

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-007` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 31-32 (Slot 16) — 6.3 Simple Queries in SQL & Pattern Matching |
| **Mục tiêu sư phạm** | Đánh giá kỹ năng sử dụng các toán tử lọc phạm vi, tập hợp, so khớp mẫu ký tự đại diện và phân trang chuẩn ANSI/T-SQL. |

**Nội dung câu hỏi phát vấn:**
> Hãy giải thích cách hoạt động của các toán tử lọc trong mệnh đề `WHERE` của MS SQL Server: (1) `BETWEEN a AND b` có bao gồm hai đầu mút `a` và `b` không? (2) Sự khác nhau giữa các ký tự đại diện `%`, `_`, `[a-z]` trong toán tử `LIKE` và cách tìm chuỗi có chứa chính ký tự `%` thật bằng từ khóa `ESCAPE`. (3) Cú pháp phân trang chuẩn `ORDER BY ... OFFSET ... FETCH NEXT ...` hoạt động như thế nào?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Tại sao khi một cột có thể chứa giá trị `NULL`, mệnh đề `WHERE DeptID NOT IN (10, 20, NULL)` lại luôn trả về tập kết quả rỗng (0 dòng), trong khi `WHERE DeptID IN (10, 20, NULL)` vẫn trả về các dòng có `DeptID` bằng 10 hoặc 20?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> 1. Toán tử phạm vi `BETWEEN ... AND ...` và tập hợp `IN (...)`:
- `expr BETWEEN a AND b` là toán tử **bao gồm cả hai đầu mút (Inclusive)**, hoàn toàn tương đương với biểu thức `(expr >= a AND expr <= b)`. Lưu ý cần viết giá trị nhỏ `a` trước và giá trị lớn `b` sau.
- `expr IN (v1, v2, v3)` tương đương với `(expr = v1 OR expr = v2 OR expr = v3)`. Tuy nhiên, cần đặc biệt cẩn trọng với `NOT IN`: vì `x NOT IN (10, NULL)` tương đương `(x <> 10 AND x <> NULL)`, mà trong Logic 3 giá trị (Three-Valued Logic) của SQL thì `x <> NULL` luôn trả về `UNKNOWN`, nên phép `AND` với `UNKNOWN` khiến `WHERE` không trả về bất kỳ dòng nào!

2. Các ký tự đại diện (Wildcards) trong `LIKE` và từ khóa `ESCAPE` trong T-SQL:
- `%`: Đại diện cho một chuỗi bất kỳ gồm **0, 1 hoặc nhiều ký tự**.
- `_` (dấu gạch dưới): Đại diện cho **đúng 1 ký tự đơn lẻ** bất kỳ.
- `[charlist]` / `[^charlist]` (Đặc thù T-SQL): Khớp 1 ký tự nằm trong (hoặc không nằm trong) tập/dải ký tự chỉ định, ví dụ `LIKE 'SE[0-9][0-9]%'`.
* **Từ khóa `ESCAPE`:** Khi cần tìm chuỗi chứa ký tự `%` hoặc `_` thực sự (ví dụ tìm chương trình khuyến mãi `'Giảm 50%'`), ta định nghĩa ký tự thoát như `'\'`:
  `WHERE PromoText LIKE N'%50\%%' ESCAPE '\'`

3. Cú pháp phân trang chuẩn `OFFSET ... FETCH NEXT` (SQL Server 2012+):
- Bắt buộc phải đi kèm mệnh đề `ORDER BY` để thứ tự dòng xác định. Để lấy trang thứ `@PageNumber` (bắt đầu từ 1) với kích thước `@PageSize` dòng/trang:
```sql
SELECT ProductID, ProductName, UnitPrice
FROM Products
ORDER BY UnitPrice DESC, ProductID ASC
OFFSET (@PageNumber - 1) * @PageSize ROWS
FETCH NEXT @PageSize ROWS ONLY;
```

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Giải thích tính bao gồm 2 đầu mút của BETWEEN và lưu ý NOT IN với NULL** | Khẳng định BETWEEN tương đương >= a AND <= b, giải thích rõ logic 3 giá trị UNKNOWN khi dùng NOT IN có NULL. | Understand | **3.5đ** |
| **Phân biệt các ký tự đại diện %, _, [...] trong LIKE và cú pháp ESCAPE** | Nêu đúng ý nghĩa %, _, [...] và viết đúng ví dụ dùng mệnh đề ESCAPE để tìm ký tự % hoặc _ thật. | Understand | **3.5đ** |
| **Viết đúng cú pháp phân trang ORDER BY ... OFFSET ... FETCH NEXT** | Nêu rõ điều kiện bắt buộc phải có ORDER BY và công thức bỏ qua (PageNumber - 1) * PageSize dòng. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 8. `DBI202-PRAC-008`: Xử lý giá trị NULL trong SQL và toán tử IS NULL / IS NOT NULL

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-008` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 32 (Slot 16) — SQL Queries (Handling NULLs) |
| **Mục tiêu sư phạm** | Đánh giá mức độ hiểu biết về bản chất giá trị NULL và logic 3 giá trị (Three-valued logic) trong SQL. |

**Nội dung câu hỏi phát vấn:**
> Giá trị NULL trong cơ sở dữ liệu quan hệ mang ý nghĩa gì? Tại sao trong SQL chúng ta bắt buộc phải sử dụng toán tử IS NULL hoặc IS NOT NULL thay vì dùng phép so sánh toán học như '= NULL' hoặc '<> NULL'?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Biểu thức điều kiện 'WHERE Salary = NULL' sẽ trả về kết quả gì và ảnh hưởng như thế nào đến tập kết quả của câu truy vấn?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Giá trị NULL trong CSDL quan hệ biểu thị trạng thái không xác định (Unknown), dữ liệu bị thiếu (Missing) hoặc không áp dụng được (Not Applicable); NULL không phải là số 0, cũng không phải là chuỗi rỗng ''. SQL sử dụng Hệ logic 3 giá trị (Three-Valued Logic: TRUE, FALSE, UNKNOWN). Mọi phép so sánh toán học thông thường với NULL (kể cả NULL = NULL hay NULL <> NULL) đều không trả về TRUE hoặc FALSE mà trả về giá trị UNKNOWN. Mệnh đề WHERE chỉ giữ lại các dòng có điều kiện đánh giá ra TRUE. Do đó, câu lệnh 'WHERE Salary = NULL' sẽ luôn cho kết quả UNKNOWN đối với mọi dòng và kết quả trả về sẽ là tập rỗng (0 dòng). Vì vậy, để kiểm tra sự tồn tại của giá trị thiếu, chuẩn ANSI SQL cung cấp toán tử vị từ đặc biệt 'IS NULL' hoặc 'IS NOT NULL' để trả về giá trị boolean TRUE hoặc FALSE rõ ràng.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Bản chất của giá trị NULL trong RDBMS** | Nêu đúng NULL là dữ liệu chưa biết/thiếu, phân biệt rõ với 0 và chuỗi rỗng. | Understand | **3.0đ** |
| **Nguyên lý Logic 3 giá trị (Three-Valued Logic)** | Giải thích rõ kết quả UNKNOWN khi dùng phép toán so sánh thông thường với NULL. | Understand | **4.0đ** |
| **Toán tử chuẩn IS NULL và hành vi của WHERE** | Phân tích vì sao 'WHERE Salary = NULL' trả về 0 dòng và tại sao phải dùng IS NULL. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 9. `DBI202-PRAC-009`: Định nghĩa Dạng chuẩn 1 (1NF) và điều kiện nguyên tử của thuộc tính

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-009` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO3** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO3** (`00000000-0000-0000-0000-000000000663`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 13 (Slot 7) — Design Theory (Normal Forms) |
| **Mục tiêu sư phạm** | Kiểm tra nhận thức cơ bản về điều kiện tiên quyết của mô hình quan hệ là tính nguyên tử của thuộc tính. |

**Nội dung câu hỏi phát vấn:**
> Thế nào là một quan hệ đạt Dạng chuẩn 1 (First Normal Form - 1NF)? Hãy chỉ ra hai trường hợp vi phạm dạng chuẩn 1 thường gặp trong thiết kế cơ sở dữ liệu và cách khắc phục.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Một cột lưu trữ chuỗi JSON hoặc danh sách các giá trị ngăn cách bởi dấu phẩy như 'Hà Nội, Đà Nẵng, TP.HCM' có vi phạm 1NF không?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Một lược đồ quan hệ đạt Dạng chuẩn 1 (1NF) khi và chỉ khi: (1) Mọi thuộc tính đều mang giá trị nguyên tử (Atomic value), nghĩa là mỗi ô giao giữa dòng và cột chỉ chứa duy nhất một giá trị đơn lẻ, không thể và không cần phân chia nhỏ hơn; (2) Không tồn tại nhóm thuộc tính lặp lại (Repeating groups) hoặc mảng/danh sách giá trị trong cùng một cột. Hai trường hợp vi phạm phổ biến: (a) Lưu danh sách giá trị trong một ô (ví dụ cột Phones chứa '0901234567, 0987654321'); khắc phục bằng cách tách mỗi số điện thoại thành một dòng riêng hoặc tạo bảng phụ liên kết. (b) Thiết kế các cột lặp lại để chứa danh sách (ví dụ Phone1, Phone2, Phone3); khắc phục bằng cách chuẩn hóa đưa về bảng phụ có khóa ngoại trỏ về bảng chính. Cột lưu danh sách ngăn cách bằng dấu phẩy vi phạm nghiêm trọng tính nguyên tử của 1NF vì gây khó khăn khi tìm kiếm, indexing và làm mất tính toàn vẹn dữ liệu.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa Dạng chuẩn 1 (1NF)** | Nêu rõ điều kiện tính nguyên tử (Atomicity) và cấm mảng/nhóm lặp trong thuộc tính. | Understand | **3.5đ** |
| **Chỉ ra 2 trường hợp vi phạm điển hình** | Nhận diện đúng trường hợp chứa chuỗi ghép giá trị và trường hợp tạo cột lặp Phone1, Phone2. | Understand | **3.5đ** |
| **Giải pháp chuẩn hóa khắc phục** | Trình bày cách tách bảng phụ để chuẩn hóa về 1NF chuẩn mực. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 10. `DBI202-PRAC-010`: Định nghĩa 4 thuộc tính ACID trong Giao dịch (Transaction)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-010` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO7** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO7** (`00000000-0000-0000-0000-000000000667`) |
| **Mức nhận thức Bloom** | **Remember** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 43 (Slot 22) — Transactions & Practical Issues |
| **Mục tiêu sư phạm** | Kiểm tra kiến thức nền tảng về tính tin cậy của giao dịch cơ sở dữ liệu qua 4 tiêu chuẩn ACID. |

**Nội dung câu hỏi phát vấn:**
> Giao dịch (Transaction) trong cơ sở dữ liệu là gì? Hãy nêu tên và giải thích ngắn gọn ý nghĩa của 4 thuộc tính ACID bảo đảm độ tin cậy của giao dịch.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Trong trường hợp hệ thống chuyển tiền giữa 2 tài khoản ngân hàng, nếu tiền đã bị trừ ở tài khoản người gửi nhưng máy chủ bị mất điện đột ngột trước khi cộng tiền vào tài khoản người nhận, thuộc tính ACID nào sẽ bảo vệ hệ thống?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Giao dịch (Transaction) là một đơn vị công việc logic bao gồm một chuỗi các thao tác truy xuất hoặc cập nhật CSDL phải được thực thi như một khối duy nhất. 4 thuộc tính ACID gồm: (1) Atomicity (Tính nguyên tố): Nguyên lý 'Tất cả hoặc không có gì' (All-or-Nothing) - toàn bộ các lệnh trong giao dịch phải thành công trọn vẹn, nếu có bất kỳ thao tác nào thất bại thì toàn bộ giao dịch phải được khôi phục (Rollback) về trạng thái ban đầu. (2) Consistency (Tính nhất quán): Giao dịch đưa CSDL từ một trạng thái hợp lệ này sang một trạng thái hợp lệ khác, bảo đảm không vi phạm bất kỳ ràng buộc toàn vẹn nào. (3) Isolation (Tính cô lập): Các giao dịch thực thi đồng thời không được nhìn thấy dữ liệu trung gian chưa hoàn tất của nhau. (4) Durability (Tính bền vững): Một khi giao dịch đã Commit thành công, kết quả thay đổi sẽ được lưu trữ vĩnh viễn trên đĩa kể cả khi hệ thống bị sập nguồn đột ngột. Trong bài toán chuyển tiền bị mất điện, thuộc tính Atomicity kết hợp cơ chế ghi nhật ký phục hồi (WAL/Undo Log) sẽ tự động rollback thao tác trừ tiền, bảo toàn số dư cho khách hàng.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa Giao dịch (Transaction)** | Nêu đúng bản chất đơn vị công việc logic không thể tách rời. | Remember | **2.5đ** |
| **Giải thích chi tiết 4 thuộc tính ACID** | Trình bày rõ ràng ý nghĩa của Atomicity, Consistency, Isolation, Durability. | Understand | **4.5đ** |
| **Vận dụng vào kịch bản sập nguồn chuyển tiền** | Chỉ ra chính xác Atomicity bảo đảm cơ chế Rollback chống mất mát tiền. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 11. `DBI202-PRAC-011`: Khái niệm và mục đích sử dụng Khung nhìn (View) trong cơ sở dữ liệu

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-011` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO6** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO6** (`00000000-0000-0000-0000-000000000666`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 51 (Slot 26) — T-SQL Programming (Views) |
| **Mục tiêu sư phạm** | Đánh giá hiểu biết về bảng ảo View, cơ chế lưu trữ và ứng dụng trong đơn giản hóa truy vấn và bảo mật. |

**Nội dung câu hỏi phát vấn:**
> View trong cơ sở dữ liệu là gì? Nêu 3 lợi ích quan trọng nhất của việc sử dụng View đối với ứng dụng và bảo mật dữ liệu.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Một View chuẩn (Standard View) có thực sự lưu trữ dữ liệu các dòng trên đĩa cứng như một bảng vật lý thông thường không? Dữ liệu hiển thị từ View được lấy từ đâu khi người dùng gọi lệnh SELECT?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> View là một bảng ảo (Virtual Table) được định nghĩa dựa trên một câu lệnh truy vấn SQL (SELECT). Một Standard View không trực tiếp lưu trữ dữ liệu các dòng trên đĩa cứng mà chỉ lưu trữ câu lệnh định nghĩa (Definition Metadata) trong Data Dictionary của hệ thống. Khi người dùng thực thi lệnh SELECT từ View, DBMS sẽ kết hợp (Merge) câu lệnh của người dùng với định nghĩa gốc của View để truy vấn trực tiếp từ các bảng vật lý cơ sở (Base Tables). 3 lợi ích quan trọng: (1) Đơn giản hóa truy vấn phức tạp: Đóng gói các phép JOIN nhiều bảng, Subquery, biểu thức tính toán phức tạp thành một bảng ảo dễ gọi; (2) Tăng cường bảo mật: Cho phép phân quyền người dùng chỉ được xem các cột hoặc dòng nhất định (ví dụ giấu cột PasswordHash, Salary) mà không cần cấp quyền truy cập toàn bộ bảng gốc; (3) Tính độc lập dữ liệu logic: Khi cấu trúc bảng cơ sở thay đổi nhẹ, ta có thể sửa lại định nghĩa View để giữ nguyên giao diện dữ liệu cho các ứng dụng client mà không cần sửa code ứng dụng.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa chính xác Khung nhìn (View)** | Nêu rõ View là bảng ảo lưu câu lệnh truy vấn, không lưu dữ liệu vật lý riêng (với Standard View). | Understand | **3.0đ** |
| **Cơ chế hoạt động khi truy vấn View** | Giải thích được cơ chế Query Merger/Expansion đọc dữ liệu từ các Base Tables. | Understand | **3.5đ** |
| **Trình bày 3 lợi ích cốt lõi (Query, Security, Independence)** | Nêu rõ ràng và phân tích đúng giá trị của View trong đơn giản hóa, an ninh dữ liệu và độc lập logic. | Understand | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 12. `DBI202-PRAC-012`: Phân biệt ngôn ngữ DDL và DML trong SQL

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-012` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 27 (Slot 14) — Data Definition Language (DDL) |
| **Mục tiêu sư phạm** | Kiểm tra sự phân biệt rạch ròi giữa thao tác cấu trúc lược đồ và thao tác dữ liệu bên trong bảng. |

**Nội dung câu hỏi phát vấn:**
> Ngôn ngữ DDL (Data Definition Language) và DML (Data Manipulation Language) trong SQL khác nhau như thế nào? Hãy liệt kê ít nhất 3 lệnh tiêu biểu cho mỗi nhóm.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Câu lệnh TRUNCATE TABLE thuộc nhóm DDL hay DML? Tại sao?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> DDL (Data Definition Language - Ngôn ngữ định nghĩa dữ liệu) được sử dụng để tạo lập, sửa đổi và hủy bỏ cấu trúc các đối tượng trong cơ sở dữ liệu (Database, Table, View, Index, Trigger, Stored Procedure). Các lệnh tiêu biểu: CREATE, ALTER, DROP, TRUNCATE. Khi chạy lệnh DDL, hệ thống cập nhật siêu dữ liệu (Metadata/Catalog) và thường tự động Commit ngầm định. Ngược lại, DML (Data Manipulation Language - Ngôn ngữ thao tác dữ liệu) được sử dụng để truy xuất và sửa đổi dữ liệu thực tế bên trong các bảng mà không làm thay đổi cấu trúc bảng. Các lệnh tiêu biểu: SELECT, INSERT, UPDATE, DELETE. Lệnh DML có thể kiểm soát hoàn toàn bằng Transaction (COMMIT/ROLLBACK). Câu lệnh TRUNCATE TABLE thuộc nhóm DDL (mặc dù mang mục đích xóa dữ liệu) vì nó hoạt động bằng cách giải phóng (deallocate) toàn bộ các trang dữ liệu (Data Pages) ở mức cấu trúc lưu trữ của bảng, ghi log tối thiểu trên trang chứ không quét và xóa từng dòng dữ liệu như lệnh DML DELETE.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Khái niệm và đặc điểm của DDL** | Nêu đúng DDL thao tác trên Metadata/cấu trúc và liệt kê CREATE, ALTER, DROP. | Understand | **3.5đ** |
| **Khái niệm và đặc điểm của DML** | Nêu đúng DML thao tác trên dữ liệu bộ và liệt kê SELECT, INSERT, UPDATE, DELETE. | Understand | **3.5đ** |
| **Bản chất phân loại của TRUNCATE TABLE** | Giải thích chính xác vì sao TRUNCATE là DDL dựa trên cơ chế deallocate data pages và ghi log. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 13. `DBI202-PRAC-013`: Phân biệt Phép kết nối trong (INNER JOIN) và Phép kết nối ngoài (OUTER JOIN)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-013` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 33-34 (Slot 17) — SQL Queries (Joins) |
| **Mục tiêu sư phạm** | Đánh giá khả năng hiểu sâu và áp dụng đúng đắn các phép kết nối bảng trong truy vấn SQL. |

**Nội dung câu hỏi phát vấn:**
> Phân biệt sự khác nhau về bản chất xử lý dữ liệu giữa INNER JOIN và các loại OUTER JOIN (LEFT, RIGHT, FULL OUTER JOIN). Trong trường hợp nào chúng ta bắt buộc phải sử dụng LEFT OUTER JOIN thay vì INNER JOIN?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu một khách hàng chưa từng mua đơn hàng nào, câu truy vấn dùng INNER JOIN giữa Customers và Orders có hiển thị thông tin khách hàng đó không? Còn LEFT JOIN sẽ hiển thị như thế nào?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> INNER JOIN chỉ giữ lại các cặp dòng từ hai bảng có giá trị khóa kết nối khớp nhau hoàn toàn theo điều kiện ON; các dòng không tìm thấy bản ghi khớp ở bảng đối chiếu sẽ bị loại bỏ khỏi kết quả. Ngược lại, OUTER JOIN được thiết kế để giữ lại cả những dòng không khớp: (1) LEFT OUTER JOIN giữ lại toàn bộ dòng của bảng bên trái; nếu không có bản ghi khớp ở bảng bên phải, các cột của bảng phải sẽ được điền giá trị NULL; (2) RIGHT OUTER JOIN giữ lại toàn bộ dòng của bảng phải; (3) FULL OUTER JOIN giữ lại toàn bộ dòng của cả hai bảng, vị trí không khớp ở bên nào sẽ điền NULL ở bên đó. Chúng ta bắt buộc phải dùng LEFT JOIN khi muốn truy vấn toàn diện danh sách thực thể chính kể cả khi chúng chưa có liên kết nghiệp vụ ở bảng phụ (ví dụ: 'Liệt kê danh sách tất cả khách hàng kèm số lượng đơn đã mua, bao gồm cả những khách hàng mới chưa mua đơn hàng nào'). Với INNER JOIN, khách hàng chưa mua hàng sẽ bị loại bỏ hoàn toàn; với LEFT JOIN, khách hàng đó vẫn hiển thị trên kết quả và các cột OrderID, OrderDate sẽ mang giá trị NULL.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Bản chất xử lý của INNER JOIN** | Giải thích rõ ràng cơ chế chỉ lấy các dòng khớp điều kiện ở cả hai bảng. | Understand | **3.0đ** |
| **Cơ chế xử lý của các loại OUTER JOIN** | Phân tích chính xác cách LEFT, RIGHT, FULL OUTER JOIN giữ lại dòng và điền giá trị NULL. | Understand | **4.0đ** |
| **Tình huống nghiệp vụ bắt buộc dùng LEFT JOIN** | Đưa ra kịch bản khách hàng chưa mua hàng và so sánh kết quả cụ thể. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 14. `DBI202-PRAC-014`: Phân biệt mệnh đề WHERE và HAVING khi kết hợp với GROUP BY

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-014` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 37-38 (Slot 19) — SQL Queries (Aggregation & Grouping) |
| **Mục tiêu sư phạm** | Kiểm tra khả năng phân định phạm vi lọc dữ liệu theo cấp dòng đơn lẻ so với cấp nhóm gom tụ. |

**Nội dung câu hỏi phát vấn:**
> Trong câu lệnh SQL có sử dụng mệnh đề GROUP BY, hãy phân biệt sự khác nhau giữa mệnh đề WHERE và mệnh đề HAVING về: thời điểm thực thi, đối tượng tác động và việc sử dụng hàm tổng hợp (Aggregate Functions). Cho một ví dụ truy vấn minh họa kết hợp cả hai mệnh đề.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Điều gì xảy ra nếu lập trình viên viết điều kiện 'HAVING DepartmentID = 10' thay vì đưa vào mệnh đề WHERE? Câu truy vấn có chạy được không và ảnh hưởng thế nào đến hiệu năng?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Sự khác biệt cốt lõi: (1) Thời điểm thực thi: WHERE được thực thi TRƯỚC khi phép gom nhóm GROUP BY diễn ra; còn HAVING được thực thi SAU KHI các nhóm đã được hình thành. (2) Đối tượng tác động: WHERE lọc trên từng dòng dữ liệu đơn lẻ (Row-level filtering); HAVING lọc trên các nhóm dữ liệu tổng hợp (Group-level filtering). (3) Hàm tổng hợp: WHERE tuyệt đối không được chứa các hàm tổng hợp (như SUM, AVG, COUNT, MAX, MIN); còn HAVING chuyên dùng để đặt điều kiện trên kết quả của hàm tổng hợp (ví dụ: HAVING COUNT(*) > 5). Ví dụ: 'SELECT DepartmentID, AVG(Salary) FROM Employees WHERE Status = 'Active' GROUP BY DepartmentID HAVING AVG(Salary) > 1500;'. Nếu viết 'HAVING DepartmentID = 10', câu truy vấn trên SQL Server vẫn chạy được vì DepartmentID nằm trong GROUP BY, nhưng sẽ làm suy giảm hiệu năng nghiêm trọng: hệ thống phải gom nhóm toàn bộ bảng nhân viên của tất cả các phòng ban rồi mới lọc bỏ nhóm, thay vì dùng WHERE để lọc trước và chỉ gom nhóm riêng phòng ban 10.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **So sánh Thời điểm thực thi & Đối tượng tác động** | Chỉ rõ WHERE chạy trước lọc dòng; HAVING chạy sau lọc nhóm dữ liệu. | Understand | **3.5đ** |
| **Quy tắc sử dụng Hàm tổng hợp** | Giải thích rõ ràng WHERE cấm dùng hàm tổng hợp, HAVING chuyên dùng cho hàm tổng hợp. | Apply | **3.5đ** |
| **Ví dụ truy vấn & Phân tích tối ưu hiệu năng** | Viết đúng câu truy vấn mẫu và giải thích hậu quả hiệu năng khi đưa điều kiện cột vào HAVING. | Analyze | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 15. `DBI202-PRAC-015`: Phân biệt hàm COUNT(*) và COUNT(tên_cột) khi bảng chứa giá trị NULL

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-015` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 37 (Slot 19) — SQL Queries (Aggregate Functions) |
| **Mục tiêu sư phạm** | Đánh giá mức độ am hiểu về cách thức các hàm tổng hợp xử lý giá trị NULL. |

**Nội dung câu hỏi phát vấn:**
> Trong SQL, hàm COUNT(*) và hàm COUNT(column_name) khác nhau như thế nào khi bảng dữ liệu có chứa các dòng mang giá trị NULL? Cho ví dụ minh họa kết quả cụ thể.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Hàm COUNT(DISTINCT column_name) hoạt động như thế nào và nó có đếm giá trị NULL vào số lượng phân biệt không?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Sự khác biệt cốt lõi: COUNT(*) đếm tổng số lượng dòng (bản ghi) thỏa mãn điều kiện trong bảng mà không quan tâm đến nội dung hay tính chất của các cột; kể cả khi tất cả các cột trong dòng đó mang giá trị NULL thì dòng đó vẫn được đếm vào kết quả. Ngược lại, COUNT(column_name) chỉ đếm số lượng các dòng mà tại đó cột được chỉ định mang giá trị KHÁC NULL; mọi dòng có giá trị NULL tại cột đó sẽ bị bỏ qua hoàn toàn. Ví dụ: Bảng Employees có 5 dòng với cột Bonus nhận các giá trị: 500, NULL, 300, NULL, 200. Khi đó: COUNT(*) sẽ trả về kết quả là 5; nhưng COUNT(Bonus) sẽ chỉ trả về kết quả là 3. Đối với hàm COUNT(DISTINCT column_name), hệ thống sẽ lọc bỏ toàn bộ các giá trị trùng lặp và loại bỏ hoàn toàn giá trị NULL trước khi đếm; như trong ví dụ trên nếu có thêm 1 dòng Bonus = 500 nữa thì COUNT(DISTINCT Bonus) sẽ trả về 3 (chỉ đếm các giá trị phân biệt: 500, 300, 200).

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Cơ chế đếm của COUNT(*)** | Nêu rõ COUNT(*) đếm mọi dòng thỏa mãn điều kiện, không phụ thuộc vào giá trị NULL. | Understand | **3.0đ** |
| **Cơ chế đếm của COUNT(column_name)** | Giải thích chính xác cơ chế tự động bỏ qua các dòng mang giá trị NULL ở cột chỉ định. | Understand | **3.5đ** |
| **Ví dụ thực tế và giải thích COUNT(DISTINCT)** | Đưa ra bảng ví dụ minh họa sự khác biệt số lượng và nêu rõ COUNT(DISTINCT) loại bỏ NULL. | Apply | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 16. `DBI202-PRAC-016`: Thuật toán tính bao đóng của tập thuộc tính (Attribute Closure X+)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-016` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO3** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO3** (`00000000-0000-0000-0000-000000000663`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 10 (Slot 5) — Design Theory (Functional Dependencies) |
| **Mục tiêu sư phạm** | Kiểm tra kỹ năng áp dụng thuật toán tìm bao đóng thuộc tính để xác định khóa ứng viên của quan hệ. |

**Nội dung câu hỏi phát vấn:**
> Bao đóng của tập thuộc tính X (ký hiệu X+) dưới tập phụ thuộc hàm F là gì? Hãy trình bày các bước của thuật toán tính X+. Áp dụng: Cho quan hệ R(A, B, C, D) với F = {A -> B, B -> C, C -> D}. Hãy tính bao đóng A+ và cho biết A có phải là một Siêu khóa (Superkey) của R không?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Dựa vào kết quả bao đóng A+, làm thế nào để kết luận A có phải là một Khóa ứng viên (Candidate Key) của quan hệ R hay không?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Bao đóng của tập thuộc tính X dưới tập phụ thuộc hàm F (ký hiệu X+) là tập hợp tất cả các thuộc tính Y mà ta có thể suy dẫn hàm được từ X dựa trên F (tức X -> Y được suy ra từ F bằng hệ tiên đề Armstrong). Thuật toán tính X+: (Bước 1) Khởi tạo X+ = X. (Bước 2) Lặp qua từng phụ thuộc hàm Y -> Z trong F: nếu Y là tập con của X+ (Y ⊆ X+) thì bổ sung Z vào X+ (X+ = X+ ∪ Z). (Bước 3) Lặp lại Bước 2 cho đến khi X+ không thể mở rộng thêm thuộc tính nào nữa thì dừng. Áp dụng cho R(A, B, C, D) với F = {A -> B, B -> C, C -> D}: Khởi tạo A+ = {A}. Xét A -> B: {A} ⊆ A+ => A+ = {A, B}. Xét B -> C: {B} ⊆ A+ => A+ = {A, B, C}. Xét C -> D: {C} ⊆ A+ => A+ = {A, B, C, D}. Kết luận: Vì A+ chứa tất cả các thuộc tính của quan hệ R (A+ = R), nên A chắc chắn là một Siêu khóa (Superkey) của R. Hơn nữa, vì A chỉ gồm 1 thuộc tính đơn lẻ nên không tồn tại tập con thực sự nào khác, do đó A chính là một Khóa ứng viên (Candidate Key) của R.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa Bao đóng thuộc tính X+** | Nêu đúng khái niệm tập hợp mọi thuộc tính suy dẫn được từ X qua F. | Understand | **2.5đ** |
| **Trình bày thuật toán tính bao đóng** | Mô tả đầy đủ 3 bước: khởi tạo, vòng lặp kiểm tra tập con và điều kiện dừng. | Apply | **3.5đ** |
| **Thực hiện tính toán và chứng minh Khóa** | Tính đúng từng bước A+ = {A,B,C,D} và lập luận chặt chẽ khẳng định A là Siêu khóa & Khóa ứng viên. | Apply | **4.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 17. `DBI202-PRAC-017`: Phân biệt Phụ thuộc hàm đầy đủ và Phụ thuộc hàm bộ phận trong 2NF

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-017` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO3** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO3** (`00000000-0000-0000-0000-000000000663`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 13 (Slot 7) — Design Theory (Second Normal Form) |
| **Mục tiêu sư phạm** | Kiểm tra sự hiểu biết về phụ thuộc hàm bộ phận - nguyên nhân chính khiến quan hệ vi phạm Dạng chuẩn 2. |

**Nội dung câu hỏi phát vấn:**
> Thế nào là Phụ thuộc hàm đầy đủ (Full Functional Dependency) và Phụ thuộc hàm bộ phận (Partial Functional Dependency)? Một quan hệ đạt Dạng chuẩn 2 (2NF) khi nào? Cho ví dụ một quan hệ vi phạm 2NF và cách phân rã chuẩn hóa.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu một quan hệ có khóa chính chỉ bao gồm duy nhất 1 thuộc tính đơn lẻ và đã đạt chuẩn 1NF, thì quan hệ đó có thể vi phạm Dạng chuẩn 2 (2NF) được không? Tại sao?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Cho quan hệ R có khóa chính K và thuộc tính không khóa A. A được gọi là Phụ thuộc hàm đầy đủ vào K nếu K -> A và không tồn tại bất kỳ tập con thực sự nào K' ⊂ K mà K' -> A. Ngược lại, nếu tồn tại một tập con thực sự K' ⊂ K sao cho K' -> A, thì phụ thuộc hàm đó gọi là Phụ thuộc hàm bộ phận. Một quan hệ đạt Dạng chuẩn 2 (2NF) khi và chỉ khi nó đã đạt 1NF và KHÔNG chứa bất kỳ phụ thuộc hàm bộ phận nào (tức mọi thuộc tính không khóa đều phải phụ thuộc hàm đầy đủ vào khóa chính). Ví dụ vi phạm: Enrollment(StudentID, CourseID, Grade, CourseName). Khóa chính là (StudentID, CourseID). Ta có phụ thuộc hàm: CourseID -> CourseName. Vì CourseID là tập con thực sự của khóa chính nên CourseName phụ thuộc bộ phận vào khóa -> Vi phạm 2NF. Cách phân rã: Tách thành 2 quan hệ đạt 2NF: R1(StudentID, CourseID, Grade) với khóa (StudentID, CourseID) và R2(CourseID, CourseName) với khóa CourseID. Nếu quan hệ đã đạt 1NF và khóa chính chỉ gồm 1 thuộc tính đơn thì quan hệ đó TỰ ĐỘNG ĐẠT 2NF vì một thuộc tính đơn không có tập con thực sự khác rỗng.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa Phụ thuộc đầy đủ vs Phụ thuộc bộ phận** | Nêu rõ định nghĩa toán học dựa trên tập con thực sự K' của khóa chính. | Understand | **3.0đ** |
| **Điều kiện Dạng chuẩn 2 (2NF)** | Khẳng định điều kiện đạt 1NF và mọi thuộc tính không khóa phụ thuộc đầy đủ vào khóa. | Understand | **3.0đ** |
| **Ví dụ vi phạm, cách phân rã & Hệ quả khóa đơn** | Đưa ví dụ Enrollment phân tích đúng vi phạm, phân rã chính xác và giải thích khóa đơn tự động đạt 2NF. | Apply | **4.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 18. `DBI202-PRAC-018`: Ba dạng dị thường dữ liệu (Insertion, Deletion, Update Anomalies)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-018` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO3** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO3** (`00000000-0000-0000-0000-000000000663`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 12 (Slot 6) — Design Theory (Data Anomalies) |
| **Mục tiêu sư phạm** | Đánh giá mức độ nhận thức về các rủi ro toàn vẹn dữ liệu khi thiết kế bảng chưa chuẩn hóa. |

**Nội dung câu hỏi phát vấn:**
> Hãy phân tích chi tiết 3 dạng dị thường dữ liệu (Anomalies) thường gặp trong cơ sở dữ liệu chưa chuẩn hóa: Dị thường chèn (Insertion Anomaly), Dị thường xóa (Deletion Anomaly) và Dị thường cập nhật (Update Anomaly). Cho ví dụ thực tế minh họa.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Phương pháp cốt lõi nào trong lý thuyết cơ sở dữ liệu được sử dụng để loại bỏ triệt để 3 dạng dị thường này?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Trong một bảng chưa chuẩn hóa (gộp nhiều thực thể vào một bảng, ví dụ gộp Sinh viên và Khoa: Student_Dept(StudentID, StudentName, DeptID, DeptName, DeptDean)): (1) Dị thường chèn (Insertion Anomaly): Không thể thêm thông tin của một thực thể độc lập nếu chưa có thực thể kia; ví dụ: Nhà trường vừa thành lập 'Khoa Trí tuệ Nhân tạo' mới nhưng chưa có sinh viên nào nhập học; do StudentID là khóa chính (cấm NULL) nên hệ thống không thể chèn bản ghi thông tin của Khoa mới này vào CSDL. (2) Dị thường xóa (Deletion Anomaly): Khi xóa thông tin của một thực thể thì vô tình làm mất luôn thông tin quan trọng của một thực thể khác; ví dụ: Nếu sinh viên duy nhất của Khoa Triết học tốt nghiệp và bị xóa khỏi bảng, thông tin về tên khoa và trưởng khoa Triết học cũng bị biến mất hoàn toàn khỏi hệ thống. (3) Dị thường cập nhật (Update Anomaly): Do dữ liệu bị lặp lại ở nhiều dòng (ví dụ tên Trưởng khoa bị lặp lại ở 1,000 dòng sinh viên), khi Trưởng khoa thay đổi, nếu không cập nhật đồng bộ ở toàn bộ 1,000 dòng thì dữ liệu sẽ rơi vào trạng thái mâu thuẫn mất nhất quán (Inconsistent state). Phương pháp cốt lõi để loại bỏ 3 dị thường này là Kỹ thuật Chuẩn hóa dữ liệu (Normalization) bằng cách phân rã quan hệ thành các bảng độc lập bảo toàn thông tin.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Phân tích Dị thường chèn (Insertion Anomaly)** | Giải thích rõ nguyên nhân không chèn được thực thể mới do vướng ràng buộc khóa chính. | Understand | **3.0đ** |
| **Phân tích Dị thường xóa (Deletion Anomaly)** | Giải thích rõ hiện tượng xóa thực thể này kéo theo mất dữ liệu của thực thể độc lập khác. | Understand | **3.0đ** |
| **Phân tích Dị thường cập nhật & Giải pháp Chuẩn hóa** | Chỉ rõ rủi ro mâu thuẫn dữ liệu do dư thừa và khẳng định giải pháp phân rã chuẩn hóa. | Understand | **4.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 19. `DBI202-PRAC-019`: Quy tắc chuyển đổi mối kết hợp Nhiều - Nhiều (M:N) từ ERD sang Lược đồ quan hệ

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-019` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO4** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO4** (`00000000-0000-0000-0000-000000000664`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 21 (Slot 11) — ERD to Relational Mapping |
| **Mục tiêu sư phạm** | Kiểm tra kỹ năng chuyển đổi mô hình quan niệm ERD sang mô hình logic quan hệ chuẩn hóa. |

**Nội dung câu hỏi phát vấn:**
> Khi chuyển đổi một mối kết hợp Nhiều - Nhiều (M:N) từ sơ đồ ERD sang mô hình cơ sở dữ liệu quan hệ, chúng ta phải thực hiện như thế nào? Bảng trung gian (Junction Table) sinh ra sẽ có cấu trúc, khóa chính và các khóa ngoại như thế nào?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu mối kết hợp M:N đó có chứa các thuộc tính riêng (ví dụ mối kết hợp 'Đăng ký học' giữa Sinh viên và Môn học có thuộc tính 'Điểm thi' và 'Học kỳ'), các thuộc tính này sẽ được đặt ở đâu trong lược đồ quan hệ?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Khi chuyển đổi mối kết hợp Nhiều - Nhiều (M:N) giữa hai tập thực thể A và B sang mô hình quan hệ, chúng ta không thể đặt khóa ngoại trực tiếp ở bảng A hay bảng B vì sẽ gây vi phạm tính nguyên tử (1NF) hoặc dư thừa dữ liệu. Giải pháp bắt buộc là: Tạo một Bảng trung gian (Junction Table / Bridge Table / Associative Table), đại diện cho chính mối kết hợp đó. Cấu trúc của bảng trung gian bao gồm: (1) Khóa ngoại trỏ về khóa chính của bảng A; (2) Khóa ngoại trỏ về khóa chính của bảng B; (3) Khóa chính của bảng trung gian thông thường là Khóa chính phức hợp (Composite Primary Key) kết hợp từ 2 khóa ngoại nói trên (hoặc tạo một Surrogate Key ID độc lập kèm ràng buộc UNIQUE trên cặp 2 khóa ngoại). Toàn bộ các thuộc tính gắn liền với mối kết hợp M:N (ví dụ: 'Grade', 'Semester' của mối quan hệ đăng ký học giữa Student và Course) sẽ được chuyển thành các cột thuộc tính nằm trực tiếp trong bảng trung gian này.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Nguyên lý tạo Bảng trung gian (Junction Table)** | Giải thích rõ vì sao không thể đặt FK ở bảng gốc và bắt buộc phải tách bảng trung gian. | Understand | **3.0đ** |
| **Xác định cấu trúc Khóa chính & Khóa ngoại của bảng trung gian** | Chỉ rõ cặp FK tham chiếu về 2 bảng cha và cấu trúc Composite Primary Key. | Apply | **4.0đ** |
| **Vị trí lưu trữ các thuộc tính của mối kết hợp** | Khẳng định chính xác các thuộc tính riêng của relationship được đặt trực tiếp trong bảng trung gian. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 20. `DBI202-PRAC-020`: Khái niệm Thực thể yếu (Weak Entity) và Khóa bộ phận (Discriminator)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-020` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO4** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO4** (`00000000-0000-0000-0000-000000000664`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 21 (Slot 11) — High-Level Database Models (Weak Entities) |
| **Mục tiêu sư phạm** | Đánh giá mức độ am hiểu về mô hình hóa các thực thể phụ thuộc tồn tại và cơ chế định danh phức hợp. |

**Nội dung câu hỏi phát vấn:**
> Thực thể yếu (Weak Entity Set) là gì? Khóa bộ phận (Discriminator / Partial Key) có đặc điểm gì? Hãy nêu quy tắc chuyển đổi một thực thể yếu sang bảng trong mô hình quan hệ và cho ví dụ minh họa.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Trên bảng quan hệ biểu diễn thực thể yếu, tùy chọn xóa 'ON DELETE CASCADE' ở ràng buộc khóa ngoại có vai trò đặc biệt quan trọng như thế nào?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Thực thể yếu (Weak Entity Set) là tập thực thể không có đủ thuộc tính để tự tạo thành một khóa chính độc lập; sự tồn tại của nó phụ thuộc hoàn toàn vào một thực thể cha (Identifying / Owner Entity) thông qua một mối kết hợp định danh (Identifying Relationship - ký hiệu hình thoi nét đôi). Khóa bộ phận (Discriminator / Partial Key - ký hiệu gạch chân nét đứt) là thuộc tính chỉ giúp phân biệt các thể hiện thực thể yếu cùng thuộc về một thực thể cha duy nhất. Quy tắc chuyển đổi sang mô hình quan hệ: Tạo một bảng riêng cho thực thể yếu; Khóa chính của bảng này là một Khóa phức hợp gồm: Khóa chính của thực thể cha (đóng vai trò Khóa ngoại) + Khóa bộ phận của chính nó. Ví dụ: Thực thể Nhân viên (Employee: EmpID) và thực thể yếu Người phụ thuộc (Dependent: DependentName, Relationship, BirthDate). Bảng Dependent sẽ có khóa chính là (EmpID, DependentName), trong đó EmpID là Foreign Key tham chiếu đến Employee(EmpID). Tùy chọn ON DELETE CASCADE là bắt buộc về mặt nghiệp vụ: khi một nhân viên nghỉ việc và bị xóa khỏi CSDL, toàn bộ hồ sơ người phụ thuộc của họ phải tự động bị xóa theo.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa Thực thể yếu & Khóa bộ phận** | Nêu rõ tính phụ thuộc tồn tại, ký hiệu nét đôi/nét đứt và vai trò của Discriminator. | Understand | **3.5đ** |
| **Quy tắc ánh xạ sang bảng quan hệ** | Trình bày đúng cấu trúc Khóa chính phức hợp ghép từ FK của thực thể cha và Discriminator. | Apply | **3.5đ** |
| **Ví dụ thực tế & Phân tích ON DELETE CASCADE** | Đưa ra ví dụ Employee-Dependent chuẩn xác và giải thích tầm quan trọng của CASCADE delete. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 21. `DBI202-PRAC-021`: Phân biệt Clustered Index và Non-Clustered Index trong MS SQL Server

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-021` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO7** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO7** (`00000000-0000-0000-0000-000000000667`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 41 (Slot 21) — Practical Issues (Indexing) |
| **Mục tiêu sư phạm** | Kiểm tra kiến thức cốt lõi về tổ chức vật lý chỉ mục B-Tree trong hệ quản trị cơ sở dữ liệu. |

**Nội dung câu hỏi phát vấn:**
> Trong MS SQL Server, hãy phân biệt sự khác nhau giữa Clustered Index và Non-Clustered Index về: cấu trúc tổ chức vật lý trên đĩa, số lượng chỉ mục tối đa trên một bảng và con trỏ tại mức lá (Leaf Node).

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Tại sao mỗi bảng trong SQL Server chỉ có thể có duy nhất 1 Clustered Index, trong khi có thể có hàng trăm Non-Clustered Index?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Sự khác biệt cốt lõi: (1) Cấu trúc lưu trữ vật lý: Clustered Index xác định trực tiếp thứ tự sắp xếp vật lý của các dòng dữ liệu thực tế trên đĩa cứng; bản thân các trang dữ liệu (Data Pages) tại mức lá (Leaf Level) của cây B-Tree chính là dòng dữ liệu thực tế của bảng. Vì các dòng dữ liệu vật lý trên đĩa chỉ có thể sắp xếp theo duy nhất một thứ tự cụ thể, nên mỗi bảng CHỈ CÓ THỂ CÓ DUY NHẤT 1 Clustered Index. Ngược lại, Non-Clustered Index có cấu trúc cây B-Tree lưu trữ hoàn toàn tách rời khỏi các trang dữ liệu của bảng. (2) Con trỏ tại mức lá (Leaf Node): Mức lá của Clustered Index chứa toàn bộ dữ liệu của bản ghi. Mức lá của Non-Clustered Index chỉ chứa các cột khóa chỉ mục và một con trỏ trỏ về dòng dữ liệu thực tế (con trỏ là RID - Row Identifier nếu bảng là Heap không có Clustered Index; hoặc con trỏ chính là giá trị Clustered Key nếu bảng đã có Clustered Index). (3) Số lượng chỉ mục: Một bảng chỉ có tối đa 1 Clustered Index, nhưng có thể tạo tới 999 Non-Clustered Index trong MS SQL Server.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Bản chất và cấu trúc Clustered Index** | Giải thích rõ Clustered Index định đoạt vị trí vật lý trên đĩa và leaf level là data pages. | Understand | **3.5đ** |
| **Bản chất và con trỏ mức lá của Non-Clustered Index** | Phân tích rõ cấu trúc tách rời và con trỏ mức lá trỏ về RID hoặc Clustered Key. | Understand | **3.5đ** |
| **Giải thích giới hạn số lượng chỉ mục trên bảng** | Lập luận chính xác vì sao chỉ có duy nhất 1 Clustered Index và số lượng Non-Clustered Index. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 22. `DBI202-PRAC-022`: Bảng ảo inserted và deleted trong cơ chế hoạt động của DML Trigger

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-022` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO6** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO6** (`00000000-0000-0000-0000-000000000666`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 49 (Slot 25) — T-SQL Programming (Triggers) |
| **Mục tiêu sư phạm** | Đánh giá mức độ hiểu biết về cơ chế cung cấp dữ liệu tạm thời trong bộ nhớ đệm khi DML Trigger kích hoạt. |

**Nội dung câu hỏi phát vấn:**
> Trong MS SQL Server, hai bảng ảo inserted và deleted là gì? Hãy trình bày trạng thái dữ liệu có trong hai bảng ảo này khi thực hiện lần lượt các thao tác DML: INSERT, DELETE và UPDATE.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Bản chất của thao tác UPDATE trong SQL Server được hai bảng ảo inserted và deleted mô phỏng như thế nào?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Hai bảng ảo 'inserted' và 'deleted' là các bảng tạm thời trong bộ nhớ RAM do SQL Server tự động tạo ra và quản lý trong suốt quá trình một DML Trigger đang thực thi; chúng có cùng lược đồ cấu trúc cột giống hệt bảng gốc mà trigger gắn vào. Trạng thái dữ liệu qua các thao tác: (1) Thao tác INSERT: Bảng 'inserted' chứa bản sao của toàn bộ các dòng mới vừa được thêm vào; bảng 'deleted' hoàn toàn rỗng. (2) Thao tác DELETE: Bảng 'deleted' chứa bản sao của toàn bộ các dòng vừa bị xóa khỏi bảng gốc; bảng 'inserted' hoàn toàn rỗng. (3) Thao tác UPDATE: Bản chất của câu lệnh UPDATE trong kiến trúc của hệ cơ sở dữ liệu quan hệ được mô phỏng như một cặp thao tác kép: Xóa dòng cũ và Chèn dòng mới. Do đó, bảng 'deleted' sẽ chứa trạng thái dữ liệu CŨ của các dòng trước khi được cập nhật; còn bảng 'inserted' sẽ chứa trạng thái dữ liệu MỚI của chính các dòng đó sau khi được chỉnh sửa. Lập trình viên sử dụng hai bảng này để so sánh giá trị cũ và mới nhằm thực hiện kiểm tra nghiệp vụ và ghi vết kiểm toán (Audit Trail).

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Khái niệm và đặc điểm của bảng inserted / deleted** | Nêu rõ đây là bảng ảo RAM tạm thời, cấu trúc tương đồng với bảng cơ sở. | Understand | **3.0đ** |
| **Trạng thái dữ liệu trong thao tác INSERT và DELETE** | Mô tả chính xác trạng thái có dữ liệu và rỗng của inserted/deleted trong INSERT và DELETE. | Understand | **3.5đ** |
| **Cơ chế hoạt động trong thao tác UPDATE** | Phân tích rõ bản chất UPDATE = DELETE (dòng cũ) + INSERT (dòng mới) trên 2 bảng ảo. | Understand | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 23. `DBI202-PRAC-023`: Phân biệt Stored Procedure và User-Defined Function (UDF)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-023` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO6** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO6** (`00000000-0000-0000-0000-000000000666`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 51-54 (Slot 26-27) — T-SQL Programming (SP vs Functions) |
| **Mục tiêu sư phạm** | Kiểm tra khả năng phân định mục đích kiến trúc giữa hàm tính toán và thủ tục thực thi nghiệp vụ. |

**Nội dung câu hỏi phát vấn:**
> Trong T-SQL, hãy so sánh sự khác nhau giữa Stored Procedure (Thủ tục lưu trữ) và User-Defined Function (Hàm người dùng định nghĩa) về: giá trị trả về, khả năng thay đổi trạng thái dữ liệu (DML), và cách thức gọi thực thi trong câu lệnh SQL.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Chúng ta có thể gọi một Stored Procedure bên trong mệnh đề WHERE của câu lệnh SELECT được không? Tại sao?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Sự khác biệt cốt lõi: (1) Giá trị trả về: Function BẮT BUỘC phải trả về một giá trị duy nhất (với Scalar Function) hoặc một bảng dữ liệu (với Table-Valued Function) thông qua mệnh đề RETURNS. Ngược lại, Stored Procedure không bắt buộc trả về giá trị; SP có thể trả về 0, 1 hoặc nhiều tập kết quả (Result Sets), kèm theo mã trạng thái RETURN (kiểu INT) và hỗ trợ nhiều tham số đầu ra (OUTPUT parameters). (2) Khả năng thay đổi dữ liệu (Side Effects): Function tuyệt đối KHÔNG ĐƯỢC làm thay đổi trạng thái của CSDL (cấm các lệnh INSERT, UPDATE, DELETE trên bảng vật lý, cấm dùng Transaction); Function được thiết kế thuần túy để tính toán. Stored Procedure được phép thực hiện mọi thao tác DDL, DML, quản lý Transaction (BEGIN TRAN, COMMIT, ROLLBACK). (3) Cách thức gọi thực thi: Function có thể được nhúng trực tiếp vào các mệnh đề SELECT, WHERE, HAVING, JOIN của câu truy vấn SQL như một biểu thức. Ngược lại, Stored Procedure phải được gọi độc lập bằng lệnh EXECUTE (hoặc EXEC) và TUYỆT ĐỐI KHÔNG THỂ gọi bên trong mệnh đề WHERE của SELECT.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **So sánh cơ chế Giá trị trả về** | Chỉ rõ Function bắt buộc RETURNS giá trị/bảng; SP linh hoạt result sets và OUTPUT params. | Understand | **3.5đ** |
| **So sánh Tác động trạng thái CSDL (DML/Transaction)** | Nêu rõ Function cấm side-effects, SP được phép thao tác DML và quản trị Transaction. | Understand | **3.5đ** |
| **So sánh Cách thức gọi & Giải thích lệnh EXEC** | Khẳng định Function gọi trong SELECT/WHERE; SP chỉ gọi bằng EXEC và cấm trong WHERE. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 24. `DBI202-PRAC-024`: Tùy chọn xử lý vi phạm khóa ngoại: ON DELETE CASCADE vs ON DELETE SET NULL

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-024` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 29 (Slot 15) — SQL Constraints on Attributes |
| **Mục tiêu sư phạm** | Kiểm tra kiến thức về các hành động toàn vẹn tham chiếu khi dữ liệu bảng cha bị xóa. |

**Nội dung câu hỏi phát vấn:**
> Khi khai báo ràng buộc Khóa ngoại (FOREIGN KEY) trong SQL, các tùy chọn ON DELETE CASCADE, ON DELETE SET NULL và ON DELETE NO ACTION hoạt động như thế nào? Trong tình huống nào thì bắt buộc không được dùng CASCADE?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Để sử dụng được tùy chọn ON DELETE SET NULL, cột khóa ngoại ở bảng con bắt buộc phải thỏa mãn điều kiện gì về mặt cấu trúc DDL?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Khi một bản ghi ở bảng cha (chứa khóa chính) bị xóa, hệ thống sẽ xử lý các bản ghi tương ứng ở bảng con (chứa khóa ngoại) theo 3 tùy chọn: (1) ON DELETE NO ACTION (Mặc định): Hệ thống từ chối thao tác xóa ở bảng cha và ném ra lỗi vi phạm toàn vẹn tham chiếu nếu còn tồn tại bản ghi ở bảng con trỏ tới nó. (2) ON DELETE CASCADE: Xóa dây chuyền - hệ thống tự động xóa toàn bộ các bản ghi liên quan ở bảng con khi bản ghi cha tương ứng bị xóa. Thích hợp cho quan hệ cha-con phụ thuộc sống còn (như Đơn hàng và Chi tiết đơn hàng). (3) ON DELETE SET NULL: Hệ thống giữ lại các bản ghi ở bảng con, nhưng tự động cập nhật giá trị của cột khóa ngoại thành NULL. Để sử dụng được ON DELETE SET NULL, điều kiện tiên quyết là cột khóa ngoại ở bảng con BẮT BUỘC PHẢI CHO PHÉP NHẬN GIÁ TRỊ NULL (nghĩa là không được khai báo NOT NULL). Tình huống bắt buộc CẤM dùng CASCADE: Khi quan hệ có tính chất tham chiếu độc lập hoặc nhiều đường dẫn quan hệ vòng (Multiple Cascade Paths / Cycles) gây lỗi Deadlock hoặc tự xóa dây chuyền mất kiểm soát (ví dụ xóa Khoa không được tự ý xóa vĩnh viễn Giảng viên).

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Cơ chế hoạt động của NO ACTION và CASCADE** | Giải thích rõ NO ACTION chặn lỗi và CASCADE xóa dây chuyền bản ghi bảng con. | Understand | **3.5đ** |
| **Cơ chế SET NULL và điều kiện DDL bắt buộc** | Nêu rõ việc gán NULL và điều kiện tiên quyết cột khóa ngoại phải Nullable (không NOT NULL). | Apply | **3.5đ** |
| **Rủi ro kỹ thuật và trường hợp cấm dùng CASCADE** | Chỉ ra nguy cơ mất dữ liệu diện rộng và hiện tượng Multiple Cascade Paths gây lỗi trong SQL Server. | Analyze | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 25. `DBI202-PRAC-025`: Phân biệt câu lệnh DELETE và TRUNCATE TABLE

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-025` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 39-40 (Slot 20) — 6.7 Database Modifications (DELETE vs TRUNCATE) |
| **Mục tiêu sư phạm** | Đánh giá mức độ am hiểu về cơ chế ghi nhật ký giao dịch, hiệu năng và xử lý cột IDENTITY. |

**Nội dung câu hỏi phát vấn:**
> Hãy so sánh chi tiết sự khác biệt giữa câu lệnh DELETE và TRUNCATE TABLE trong MS SQL Server về: mệnh đề WHERE, cơ chế ghi Transaction Log, việc kích hoạt DML Trigger và hành vi với cột IDENTITY.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu một bảng đang được tham chiếu bởi một Khóa ngoại (FOREIGN KEY) từ bảng khác thì lệnh TRUNCATE TABLE trên bảng đó có thực hiện được không, ngay cả khi bảng con đang hoàn toàn rỗng?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> So sánh chi tiết: (1) Mệnh đề WHERE: DELETE là lệnh DML cho phép sử dụng mệnh đề WHERE để xóa có chọn lọc từng dòng; TRUNCATE là lệnh DDL luôn xóa sạch 100% dữ liệu của bảng, không hỗ trợ WHERE. (2) Ghi Transaction Log: DELETE quét từng dòng, xóa dòng và ghi chi tiết việc xóa của từng dòng vào transaction log, dẫn đến chi phí I/O cao và log file phình to; TRUNCATE ghi log tối thiểu (Minimally Logged), nó chỉ ghi nhận việc giải phóng (deallocate) toàn bộ các trang dữ liệu (Extents/Pages) nên tốc độ thực thi nhanh vượt trội. (3) Kích hoạt Trigger: DELETE kích hoạt các AFTER/INSTEAD OF DELETE Trigger; TRUNCATE tuyệt đối KHÔNG kích hoạt bất kỳ DELETE Trigger nào. (4) Cột IDENTITY: DELETE giữ nguyên giá trị bộ đếm hiện tại của IDENTITY; TRUNCATE tự động đặt lại (reset) giá trị cột IDENTITY về giá trị Seed ban đầu. (5) Khóa ngoại: Nếu một bảng đang được tham chiếu bởi một FOREIGN KEY constraint từ một bảng khác thì lệnh TRUNCATE TABLE sẽ bị HỆ THỐNG CHẶN HOÀN TOÀN (báo lỗi), ngay cả khi bảng con tham chiếu hiện tại không có dữ liệu nào.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **So sánh Cú pháp WHERE & Phạm vi thao tác** | Nêu rõ DELETE có WHERE chọn lọc, TRUNCATE xóa toàn bộ bảng không có WHERE. | Understand | **2.5đ** |
| **So sánh Cơ chế Ghi Log & Tốc độ hiệu năng** | Phân tích rõ Row-by-row logging của DELETE so với Page deallocation minimal logging của TRUNCATE. | Understand | **3.0đ** |
| **So sánh Hành vi với Trigger và cột IDENTITY** | Chỉ rõ DELETE kích hoạt Trigger và giữ Seed; TRUNCATE không kích hoạt Trigger và reset Seed. | Understand | **2.5đ** |
| **Ràng buộc toàn vẹn tham chiếu Foreign Key** | Khẳng định chính xác TRUNCATE bị chặn tuyệt đối nếu bảng đang là đích tham chiếu của FK. | Apply | **2.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 26. `DBI202-PRAC-026`: Kỹ thuật tự kết nối (SELF JOIN) để biểu diễn mối quan hệ đệ quy

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-026` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 34 (Slot 17) — SQL Queries (Self Joins) |
| **Mục tiêu sư phạm** | Kiểm tra kỹ năng truy vấn dữ liệu có cấu trúc phân cấp (Hierarchical data) trong cùng một bảng. |

**Nội dung câu hỏi phát vấn:**
> SELF JOIN là gì? Trong cấu trúc cơ sở dữ liệu thực tế, kỹ thuật SELF JOIN thường được sử dụng cho kịch bản nào? Cho ví dụ bảng Employees có cột ManagerID và viết câu lệnh truy vấn hiển thị tên Nhân viên kèm tên Người quản lý trực tiếp của họ.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Đối với nhân viên cấp cao nhất (Tổng giám đốc - CEO) không có ManagerID (ManagerID IS NULL), làm thế nào để câu truy vấn vẫn hiển thị tên CEO với tên quản lý là 'None' hoặc NULL?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> SELF JOIN là kỹ thuật kết nối một bảng với chính nó; để thực hiện được, ta bắt buộc phải sử dụng hai bí danh (aliases) khác nhau cho cùng một bảng để DBMS coi như đang thao tác trên hai thực thể tách biệt. Trong thực tế, SELF JOIN chuyên được sử dụng để truy vấn các mối quan hệ đệ quy hoặc cấu trúc cây phân cấp (Hierarchical / Tree structures) như: Nhân viên - Người quản lý, Danh mục sản phẩm cha - Danh mục con, hoặc tìm các cặp bản ghi có cùng đặc tính trong cùng một bảng. Ví dụ bảng Employees(EmpID, FullName, ManagerID). Câu truy vấn dùng INNER JOIN chỉ lấy được người có quản lý. Để hiển thị cả cấp cao nhất (CEO có ManagerID là NULL), ta BẮT BUỘC phải dùng LEFT JOIN: 'SELECT E.FullName AS EmployeeName, ISNULL(M.FullName, 'None') AS ManagerName FROM Employees E LEFT JOIN Employees M ON E.ManagerID = M.EmpID;'. Trong câu truy vấn này, bảng E đại diện cho vai trò nhân viên cấp dưới, bảng M đại diện cho vai trò cấp quản lý.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Khái niệm SELF JOIN & Vai trò của Bí danh (Alias)** | Nêu đúng định nghĩa tự kết nối và việc bắt buộc dùng alias để phân biệt hai vai trò. | Understand | **3.0đ** |
| **Kịch bản ứng dụng cấu trúc phân cấp** | Chỉ ra chính xác các quan hệ đệ quy phổ biến như Nhân viên - Quản lý, Cây danh mục. | Understand | **3.0đ** |
| **Viết câu truy vấn T-SQL chuẩn mực & Xử lý NULL** | Viết đúng cú pháp LEFT JOIN kết hợp hàm ISNULL hiển thị đầy đủ kể cả cấp CEO. | Apply | **4.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 27. `DBI202-PRAC-027`: Truy vấn con tương quan (Correlated Subquery) và tối ưu hóa với toán tử EXISTS

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-027` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 36 (Slot 18) — Nested Queries in SQL |
| **Mục tiêu sư phạm** | Đánh giá khả năng hiểu sâu về cơ chế thực thi lặp của Correlated Subquery và so sánh hiệu năng với EXISTS. |

**Nội dung câu hỏi phát vấn:**
> Truy vấn con tương quan (Correlated Subquery) khác với Truy vấn con độc lập (Uncorrelated/Non-correlated Subquery) như thế nào về cơ chế thực thi? Tại sao khi kiểm tra sự tồn tại của dữ liệu, toán tử EXISTS thường mang lại hiệu năng tối ưu hơn so với toán tử IN khi tập dữ liệu lớn?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Toán tử NOT IN gặp phải lỗi logic nghiêm trọng nào khi tập kết quả của subquery chứa ít nhất một giá trị NULL? Toán tử NOT EXISTS xử lý vấn đề này an toàn ra sao?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Truy vấn con độc lập chỉ được thực thi DUY NHẤT MỘT LẦN trước khi truy vấn cha chạy, kết quả của nó được lưu vào bộ nhớ tạm để truy vấn ngoài sử dụng. Ngược lại, Truy vấn con tương quan (Correlated Subquery) tham chiếu trực tiếp đến các cột của truy vấn cha hiện tại; do đó nó BẮT BUỘC PHẢI THỰC THI LẶP LẠI cho từng dòng ứng viên của truy vấn cha (Row-by-row execution). Toán tử EXISTS vượt trội hơn IN về hiệu năng khi kiểm tra tồn tại vì: EXISTS áp dụng cơ chế đánh giá đoản mạch (Short-circuit Evaluation); bộ tối ưu hóa (Query Optimizer) sẽ dừng quét ngay khi tìm thấy bản ghi đầu tiên thỏa mãn điều kiện mà không cần đọc hết bảng, đồng thời không tốn bộ nhớ lưu trữ danh sách giá trị tạm thời như IN. Nguy cơ lỗi logic của NOT IN với NULL: Nếu subquery trả về tập giá trị có chứa dù chỉ MỘT giá trị NULL (ví dụ: (1, 2, NULL)), biểu thức 'x NOT IN (1, 2, NULL)' sẽ tương đương với '(x <> 1) AND (x <> 2) AND (x <> NULL)'. Vì 'x <> NULL' trả về UNKNOWN nên toàn bộ biểu thức logic sẽ trả về UNKNOWN; do đó câu truy vấn dùng NOT IN sẽ trả về 0 dòng (tập rỗng) ngoài ý muốn. Trong khi đó, NOT EXISTS chỉ kiểm tra số lượng dòng trả về (boolean có dòng hay không) nên hoàn toàn miễn nhiễm với giá trị NULL trong cột.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Cơ chế thực thi Correlated vs Non-correlated Subquery** | Phân tích rõ cơ chế chạy 1 lần của subquery độc lập so với lặp từng dòng của correlated subquery. | Understand | **3.0đ** |
| **Phân tích tối ưu hóa của EXISTS (Short-circuit)** | Giải thích cơ chế dừng sớm khi tìm thấy bản ghi đầu tiên giúp tiết kiệm I/O và RAM. | Analyze | **3.5đ** |
| **Bẫy logic của NOT IN với NULL và giải pháp NOT EXISTS** | Chứng minh toán học biểu thức AND với UNKNOWN làm NOT IN trả về rỗng và ưu điểm an toàn của NOT EXISTS. | Analyze | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 28. `DBI202-PRAC-028`: Điều kiện Dạng chuẩn 3 (3NF) vs Dạng chuẩn Boyce-Codd (BCNF)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-028` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO3** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO3** (`00000000-0000-0000-0000-000000000663`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 14 (Slot 7) — Design Theory (3NF vs BCNF) |
| **Mục tiêu sư phạm** | Đánh giá năng lực phân tích lý thuyết thiết kế cơ sở dữ liệu ở cấp độ chuẩn hóa cao cấp. |

**Nội dung câu hỏi phát vấn:**
> Hãy nêu định nghĩa hình thức chuẩn mực của Dạng chuẩn 3 (3NF) và Dạng chuẩn Boyce-Codd (BCNF). Điểm khác biệt mấu chốt giữa 3NF và BCNF là gì? Trong trường hợp nào một quan hệ đạt chuẩn 3NF nhưng lại KHÔNG đạt chuẩn BCNF?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Tại sao trong thực tế thiết kế phần mềm doanh nghiệp, các kỹ sư thường chấp nhận dừng lại ở 3NF thay vì cố gắng phân rã lên BCNF?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Định nghĩa hình thức: Cho lược đồ quan hệ R và tập phụ thuộc hàm F. Với mọi phụ thuộc hàm không tầm thường X -> A trong F: (1) R đạt Dạng chuẩn 3 (3NF) khi và chỉ khi: Hoặc X là một Siêu khóa (Superkey), HOẶC A là một Thuộc tính khóa (Prime Attribute - thuộc tính nằm trong ít nhất một khóa ứng viên). (2) R đạt Dạng chuẩn Boyce-Codd (BCNF) khi và chỉ khi: X BẮT BUỘC PHẢI LÀ MỘT SIÊU KHÓA (Superkey). Điểm khác biệt mấu chốt: BCNF loại bỏ hoàn toàn vế điều kiện ngoại lệ 'A là thuộc tính khóa'. Một quan hệ đạt 3NF nhưng vi phạm BCNF khi và chỉ khi: Quan hệ có nhiều khóa ứng viên phức hợp và các khóa ứng viên này bị gối/chồng chéo lên nhau (Overlapping Candidate Keys). Ví dụ điển hình: R(Student, Subject, Teacher) với FDs = {(Student, Subject) -> Teacher; Teacher -> Subject}. Khóa ứng viên là (Student, Subject) và (Student, Teacher). Xét Teacher -> Subject: Teacher không phải siêu khóa, nhưng Subject là thuộc tính khóa nên R đạt 3NF; tuy nhiên vì Teacher không là siêu khóa nên R vi phạm BCNF. Trong thực tế, người ta thường dừng ở 3NF vì: Phân rã đạt BCNF tuy loại bỏ triệt để dư thừa nhưng đôi khi KHÔNG BẢO TOÀN ĐƯỢC PHỤ THUỘC HÀM (Dependency Preservation), đòi hỏi phải dùng Trigger phức tạp để kiểm tra.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa hình thức chuẩn mực của 3NF và BCNF** | Nêu chính xác điều kiện X là siêu khóa hoặc A là prime attribute trong 3NF và điều kiện khắt khe của BCNF. | Understand | **3.0đ** |
| **Phân tích điều kiện Overlapping Candidate Keys** | Đưa ví dụ mẫu (Student, Subject, Teacher) chỉ rõ vì sao đạt 3NF nhưng trượt BCNF. | Analyze | **4.0đ** |
| **Đánh đổi kỹ thuật giữa BCNF và Bảo toàn phụ thuộc** | Lập luận sâu sắc lý do thực tế chọn 3NF để tránh mất chi phí kiểm tra phụ thuộc hàm liên bảng. | Analyze | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 29. `DBI202-PRAC-029`: Kiểm tra tính Bảo toàn Phụ thuộc hàm (Dependency Preservation) và Sự đánh đổi giữa 3NF với BCNF

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-029` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO3** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO3** (`00000000-0000-0000-0000-000000000663`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 11-14 (Slot 6-7) — 3.3 Design of Relational Database Schema & 3.5 Normal Forms |
| **Mục tiêu sư phạm** | Đánh giá năng lực kiểm tra tính chất Bảo toàn phụ thuộc hàm của một phép phân rã và phân tích bài toán kinh điển khi chuẩn BCNF làm mất phụ thuộc hàm. |

**Nội dung câu hỏi phát vấn:**
> Thế nào là một phép phân rã lược đồ bảo đảm tính chất **Bảo toàn phụ thuộc hàm (`Dependency Preservation`)**? Tại sao trong hệ quản trị CSDL thực tế, việc một phép phân rã làm mất phụ thuộc hàm lại gây tốn kém chi phí hiệu năng nghiêm trọng mỗi khi thực thi lệnh `INSERT`/`UPDATE`? Hãy phân tích ví dụ kinh điển trên lược đồ `R(Student, Course, Instructor)` với tập phụ thuộc hàm $F = \{\text{Student, Course} \to \text{Instructor};\; \text{Instructor} \to \text{Course}\}$ để chứng minh vì sao ta không thể vừa đạt chuẩn **BCNF** vừa bảo toàn được toàn bộ phụ thuộc hàm.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Trong trường hợp thực tế gặp lược đồ giống như `R(Student, Course, Instructor)` ở trên, người kiến trúc sư CSDL thường ưu tiên giữ nguyên ở chuẩn **3NF** hay ép phân rã lên **BCNF**? Vì sao?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> 1. Định nghĩa và ý nghĩa hiệu năng của Bảo toàn phụ thuộc hàm (`Dependency Preservation`):
- Cho lược đồ $R$ với tập phụ thuộc hàm $F$ được phân rã thành $\rho = \{R_1, R_2, \dots, R_k\}$. Gọi $\pi_{R_i}(F)$ là tập các phụ thuộc hàm trong $F^+$ chỉ chứa các thuộc tính thuộc về $R_i$. Phép phân rã $\rho$ được gọi là **Bảo toàn phụ thuộc hàm** nếu:
  $$\left(\bigcup_{i=1}^{k} \pi_{R_i}(F)\right)^+ = F^+$$
* **Ý nghĩa hiệu năng sống còn:** Nếu bảo toàn phụ thuộc hàm, mỗi phụ thuộc hàm đều nằm gọn trong một bảng đơn lẻ $R_i$ và có thể được DBMS kiểm tra tức thì ($O(\log n)$) bằng ràng buộc `PRIMARY KEY` / `UNIQUE` cục bộ trên bảng đó. Ngược lại, nếu mất phụ thuộc hàm, mỗi câu lệnh `INSERT` hoặc `UPDATE` buộc DBMS phải thực hiện phép kết nối (`JOIN`) đắt đỏ giữa nhiều bảng mới kiểm tra được ràng buộc!

2. Phân tích bài toán kinh điển `R(Student, Course, Instructor)` với $F = \{SC \to I,\; I \to C\}$:
* **Khóa ứng viên:** $(SC)^+ = \{S, C, I\}$ và $(SI)^+ = \{S, I, C\} \Rightarrow$ Hai khóa ứng viên là $\{S, C\}$ và $\{S, I\}$. Toàn bộ 3 thuộc tính $S, C, I$ đều là thuộc tính khóa (Prime attributes).
* **Kiểm tra 3NF vs BCNF:**
  + Vì $C$ là thuộc tính khóa, phụ thuộc hàm $I \to C$ **thỏa mãn 3NF** $\Rightarrow R(S, C, I)$ đã đạt chuẩn **3NF** và bảo toàn 100% phụ thuộc hàm!
  + Nhưng trong $I \to C$, vế trái $I$ không phải là siêu khóa của $R$, nên $R$ **vi phạm BCNF**.
* **Hệ quả khi phân rã về BCNF:** Ta buộc phải tách theo $I \to C$ thành $R_1(\underline{I}, C)$ và $R_2(\underline{S, I})$. Khi đó cả hai bảng con đều đạt BCNF và kết nối không mất thông tin ($R_1 \cap R_2 = \{I\} \to C$), nhưng phụ thuộc hàm $SC \to I$ (một sinh viên học một môn chỉ do một giảng viên dạy) bị xé lẻ qua hai bảng và **bị mất hoàn toàn**!

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa đúng tính chất Bảo toàn phụ thuộc hàm và chi phí JOIN khi vi phạm** | Nêu đúng công thức hợp các hình chiếu phụ thuộc hàm bằng F+ và giải thích việc kiểm tra cục bộ bằng UNIQUE/PK mà không cần JOIN. | Understand | **3.5đ** |
| **Xác định đúng 2 khóa ứng viên {S, C}, {S, I} và chứng minh R đạt 3NF nhưng vi phạm BCNF** | Chỉ rõ mọi thuộc tính đều là prime nên I -> C thỏa 3NF, nhưng I không phải siêu khóa nên vi phạm BCNF. | Analyze | **3.5đ** |
| **Chứng minh phép phân rã BCNF {R1(I,C), R2(S,I)} làm mất phụ thuộc hàm SC -> I** | Chỉ rõ sau khi tách thành (I, C) và (S, I), không bảng nào chứa đồng thời cả S, C, I để kiểm tra SC -> I. | Analyze | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 30. `DBI202-PRAC-030`: Phân tích 4 mức cô lập giao dịch (Transaction Isolation Levels) và hiện tượng Phantom Read

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-030` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO7** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO7** (`00000000-0000-0000-0000-000000000667`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 44 (Slot 22) — Transactions & Concurrency Control |
| **Mục tiêu sư phạm** | Kiểm tra năng lực phân tích kiểm soát đồng thời, các mức cô lập và các dị thường tương tranh. |

**Nội dung câu hỏi phát vấn:**
> Hãy lập bảng đối chiếu 4 mức cô lập giao dịch chuẩn ANSI SQL (Read Uncommitted, Read Committed, Repeatable Read, Serializable) với 3 hiện tượng tương tranh: Dirty Read, Non-repeatable Read và Phantom Read. Giải thích bản chất của hiện tượng Bóng ma (Phantom Read).

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Tại sao mức cô lập Repeatable Read có thể chống được Non-repeatable Read nhưng lại không thể ngăn chặn được hiện tượng Phantom Read?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Bảng đối chiếu 4 mức cô lập và hiện tượng tương tranh: (1) Read Uncommitted: Cho phép Dirty Read, Non-repeatable Read, Phantom Read; (2) Read Committed (Mặc định SQL Server): Chống Dirty Read; vẫn bị Non-repeatable Read và Phantom Read; (3) Repeatable Read: Chống Dirty Read, Non-repeatable Read; vẫn bị Phantom Read; (4) Serializable: Chống hoàn toàn cả 3 hiện tượng. Bản chất Phantom Read (Đọc bóng ma): Xảy ra khi Giao dịch 1 thực hiện cùng một câu truy vấn quét khoảng (Range query, ví dụ: 'SELECT * FROM Orders WHERE Amount > 1000') hai lần trong cùng một transaction. Ở giữa 2 lần đọc đó, Giao dịch 2 thực hiện INSERT một dòng mới thỏa mãn điều kiện và COMMIT. Khi Giao dịch 1 chạy lại câu truy vấn, nó phát hiện xuất hiện thêm các dòng mới 'ma quái' chưa từng thấy trước đó. Mức Repeatable Read chỉ đặt khóa chia sẻ (Shared Locks) trên các dòng dữ liệu HIỆN CÓ mà nó đọc để ngăn transaction khác UPDATE hoặc DELETE (chống Non-repeatable Read), nhưng nó KHÔNG KHÓA ĐƯỢC CÁC KHOẢNG TRỐNG (Key Ranges). Do đó, transaction khác vẫn có thể INSERT dòng mới vào khoảng trống đó. Chỉ có mức Serializable sử dụng khóa phạm vi (Range Locks) mới ngăn chặn được Phantom Read.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Bảng ma trận 4 mức cô lập và 3 hiện tượng** | Lập chính xác bảng đối chiếu Read Uncommitted, Read Committed, Repeatable Read, Serializable. | Understand | **3.5đ** |
| **Phân tích bản chất hiện tượng Phantom Read** | Giải thích rõ ràng cơ chế truy vấn quét khoảng phát hiện dòng mới do lệnh INSERT commit. | Analyze | **3.5đ** |
| **Cơ chế khóa Shared Lock vs Range Lock** | Lập luận thuyết phục vì sao khóa dòng không chống được INSERT và vai trò của Range Lock. | Analyze | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 31. `DBI202-PRAC-031`: Bản chất Deadlock trong cơ sở dữ liệu và các kỹ thuật phòng chống / giải quyết

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-031` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO7** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO7** (`00000000-0000-0000-0000-000000000667`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 44 (Slot 22) — Transactions & Concurrency Control |
| **Mục tiêu sư phạm** | Đánh giá năng lực chẩn đoán hiện tượng tắc nghẽn chết và phương pháp thiết kế code chống Deadlock. |

**Nội dung câu hỏi phát vấn:**
> Deadlock (Khóa chết) trong hệ quản trị cơ sở dữ liệu là gì? DBMS phát hiện Deadlock bằng cơ chế nào? Hãy trình bày 3 nguyên tắc vàng khi lập trình ứng dụng và viết câu lệnh T-SQL để giảm thiểu tối đa rủi ro xảy ra Deadlock.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Khi phát hiện Deadlock, DBMS sẽ làm gì để giải phóng hệ thống và dựa vào tiêu chí nào để chọn 'Nạn nhân' (Deadlock Victim)?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Deadlock là trạng thái bế tắc xảy ra khi hai hoặc nhiều giao dịch thực thi đồng thời cùng giữ khóa trên một tài nguyên và cùng chờ khóa trên tài nguyên mà giao dịch kia đang nắm giữ, tạo thành một chu trình chờ đợi khép kín (Circular Wait Dependency) khiến không bên nào có thể tiếp tục. DBMS phát hiện Deadlock thông qua một tiểu trình nền (Deadlock Monitor) chạy định kỳ (mỗi 5 giây trong SQL Server) để phân tích Đồ thị Chờ (Wait-For Graph); nếu phát hiện chu trình (Cycle) trong đồ thị, Deadlock được xác nhận. Để giải phóng bế tắc, DBMS sẽ chọn một giao dịch làm 'Nạn nhân' (Deadlock Victim), tự động ROLLBACK giao dịch đó và trả mã lỗi 1205 về cho client; tiêu chí chọn victim thường dựa trên giao dịch có chi phí khôi phục thấp nhất (ít log I/O nhất) hoặc dựa vào cấu hình độ ưu tiên 'SET DEADLOCK_PRIORITY LOW'. 3 nguyên tắc vàng phòng chống: (1) Luôn truy cập các bảng và tài nguyên theo một thứ tự cố định, nhất quán trong toàn bộ hệ thống (Consistent Access Order); (2) Giữ giao dịch càng ngắn càng tốt, không thực hiện các thao tác tính toán nặng hoặc chờ tương tác người dùng bên trong Transaction; (3) Sử dụng mức cô lập Read Committed Snapshot Isolation (RCSI) để đọc không chặn ghi và ghi không chặn đọc.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa Deadlock & Đồ thị Wait-For Graph** | Nêu rõ bản chất chu trình chờ kín và cơ chế quét đồ thị Wait-For Graph định kỳ. | Understand | **3.0đ** |
| **Cơ chế chọn Nạn nhân (Victim) & Rollback** | Phân tích cách DBMS rollback victim giải phóng tắc nghẽn và tiêu chí ưu tiên chi phí log. | Analyze | **3.5đ** |
| **3 nguyên tắc vàng thiết kế code chống Deadlock** | Trình bày chuẩn xác nguyên tắc thứ tự truy cập tài nguyên, giao dịch ngắn và áp dụng RCSI. | Apply | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 32. `DBI202-PRAC-032`: Các trường hợp vô hiệu hóa chỉ mục B-Tree (Index Invalidation) và giải pháp khắc phục

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-032` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO7** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO7** (`00000000-0000-0000-0000-000000000667`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 42 (Slot 21) — Query Optimization & Indexing |
| **Mục tiêu sư phạm** | Kiểm tra kỹ năng tối ưu hóa truy vấn nâng cao và nhận diện các anti-patterns làm mất tác dụng của Index. |

**Nội dung câu hỏi phát vấn:**
> Trong tối ưu hóa truy vấn SQL, hiện tượng Vô hiệu hóa chỉ mục (Index Invalidation / SARGability failure) là gì? Hãy phân tích 3 trường hợp viết câu lệnh WHERE phổ biến khiến bộ tối ưu hóa buộc phải bỏ qua Index Seek và chuyển sang Table Scan / Index Scan. Đưa ra cú pháp khắc phục cho từng trường hợp.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Việc sử dụng toán tử 'LIKE '%ABC'' có tận dụng được chỉ mục B-Tree không? Tại sao?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Hiện tượng Vô hiệu hóa chỉ mục xảy ra khi một cột đã được đánh Index nhưng câu truy vấn được viết vi phạm tính SARGable (Search Argument Able), khiến Query Optimizer không thể thực hiện phép tìm kiếm nhánh cây B-Tree (Index Seek) mà bắt buộc phải quét toàn bộ bảng hoặc toàn bộ chỉ mục (Table/Index Scan), làm hiệu năng suy giảm nghiêm trọng. 3 trường hợp phổ biến và giải pháp: (1) Bọc hàm quanh cột được đánh chỉ mục: Ví dụ 'WHERE YEAR(OrderDate) = 2024'. Vì hàm YEAR phải tính toán trên từng dòng nên Index bị vô hiệu hóa; Khắc phục: Viết lại theo khoảng ngày: 'WHERE OrderDate >= '2024-01-01' AND OrderDate < '2025-01-01''. (2) Thực hiện phép toán học trên cột: Ví dụ 'WHERE Salary * 1.1 > 5000'; Khắc phục: Biến đổi đại số chuyển phép toán sang vế hằng số: 'WHERE Salary > 5000 / 1.1'. (3) Ép kiểu dữ liệu ngầm định (Implicit Data Type Conversion): Ví dụ cột Phone là VARCHAR nhưng so sánh với số nguyên 'WHERE Phone = 0901234567'; SQL Server tự động gọi hàm CONVERT trên cột khiến Index vô dụng; Khắc phục: Truyền đúng kiểu chuỗi 'WHERE Phone = '0901234567''. Với toán tử 'LIKE '%ABC'', Index B-Tree hoàn toàn bị vô hiệu hóa vì cây B-Tree được sắp xếp theo thứ tự ký tự từ trái qua phải; việc đặt ký tự đại diện '%' ở đầu khiến cây không biết bắt đầu tìm kiếm từ nhánh nào.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Bản chất khái niệm SARGable & Index Invalidation** | Giải thích rõ sự chuyển đổi từ Index Seek sang Scan do mất khả năng điều hướng cây B-Tree. | Understand | **3.0đ** |
| **Phân tích 3 Anti-patterns điển hình và giải pháp** | Chỉ rõ lỗi bọc hàm, lỗi phép toán và lỗi ép kiểu ngầm định kèm câu lệnh sửa chuẩn mực. | Analyze | **4.5đ** |
| **Giải thích nguyên lý B-Tree với toán tử LIKE '%prefix'** | Lập luận chính xác cấu trúc sắp xếp từ trái sang phải của B-Tree đối với wildcard '%'. | Analyze | **2.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 33. `DBI202-PRAC-033`: So sánh Hàm Vô hướng (Scalar UDF) và Hàm Giá trị Bảng Nội tuyến (Inline TVF) trong T-SQL & Tối ưu hóa Hiệu năng

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-033` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO6** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO6** (`00000000-0000-0000-0000-000000000666`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 52-54 (Slot 26-27) — 8.2 User-Defined Functions (Scalar UDF vs Inline TVF) |
| **Mục tiêu sư phạm** | Đánh giá kỹ năng viết User-Defined Functions trong T-SQL, phân biệt 3 loại UDF và giải quyết điểm nghẽn hiệu năng RBAR của Scalar UDF bằng Inline TVF + CROSS APPLY. |

**Nội dung câu hỏi phát vấn:**
> Trong MS SQL Server, hãy phân biệt 3 loại Hàm do người dùng định nghĩa (`User-Defined Functions - UDF`): (1) `Scalar UDF`, (2) `Inline Table-Valued Function (iTVF)`, và (3) `Multi-Statement Table-Valued Function (mTVF)`. Tại sao việc gọi một `Scalar UDF` có chứa câu lệnh truy vấn bảng bên trong mệnh đề `SELECT` hoặc `WHERE` trên bảng hàng triệu dòng lại gây suy giảm hiệu năng nghiêm trọng, và làm thế nào để viết lại thành `Inline TVF` kết hợp toán tử `CROSS APPLY`?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Tại sao một `User-Defined Function (UDF)` trong MS SQL Server bị cấm sử dụng các câu lệnh có hiệu ứng phụ làm thay đổi trạng thái cơ sở dữ liệu (như `INSERT`, `UPDATE`, `DELETE` lên bảng thật) hoặc gọi hàm không tất định như `RAND()`, `NEWID()`?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> 1. Phân biệt 3 loại `User-Defined Functions (UDF)` trong MS SQL Server:
* **`Scalar UDF` (Hàm vô hướng):** Có phần thân `BEGIN ... END`, trả về một giá trị đơn lẻ thuộc kiểu dữ liệu cơ bản (`INT`, `DECIMAL`, `NVARCHAR`,...).
* **`Inline Table-Valued Function (iTVF)` (Hàm bảng nội tuyến):** **KHÔNG có khối `BEGIN ... END`**; toàn bộ nội dung chỉ gồm `RETURN (<Một câu lệnh SELECT duy nhất>)`. Đóng vai trò như một **Khung nhìn có tham số (`Parameterized View`)**.
* **`Multi-Statement Table-Valued Function (mTVF)` (Hàm bảng đa câu lệnh):** Khai báo cấu trúc một biến bảng `@ResultTable TABLE (...)`, thực hiện nhiều câu lệnh chèn dữ liệu vào biến bảng đó trong khối `BEGIN ... END` rồi mới `RETURN`.

2. Tại sao `Scalar UDF` có truy vấn dữ liệu gây thảm họa hiệu năng (`RBAR — Row-By-Agonizing-Row`):
- Khi đặt `dbo.ufn_GetTotalSpent(c.CustomerID)` trong câu lệnh `SELECT` trên bảng `Customers` gồm $1,000,000$ dòng, bộ tối ưu hóa truy vấn (`Query Optimizer`) coi `Scalar UDF` như một "hộp đen", dẫn đến:
  1. Thực thi lặp lại câu lệnh `SELECT SUM(...)` bên trong hàm **1,000,000 lần riêng biệt** (mỗi dòng 1 lần ngữ cảnh gọi hàm).
  2. Ép toàn bộ kế hoạch thực thi của câu truy vấn ngoài phải chạy **Đơn luồng (`Serial Plan`, cấm thực thi song song `Parallelism`)**.

3. Cài đặt chuẩn mực chuyển đổi sang `Inline TVF` kết hợp `CROSS APPLY`:
```sql
-- 1. Định nghĩa Inline TVF (Không có BEGIN...END, được Optimizer trải phẳng trực tiếp vào cây đại số)
CREATE FUNCTION dbo.ufn_CustomerOrderSummary (@CustomerID INT)
RETURNS TABLE
AS
RETURN (
    SELECT 
        COUNT(OrderID) AS OrderCount,
        ISNULL(SUM(TotalAmount), 0.00) AS TotalSpent
    FROM Orders
    WHERE CustomerID = @CustomerID
);
GO

-- 2. Gọi hàm bằng CROSS APPLY (Cho phép Optimizer gộp thành 1 phép JOIN tập hợp duy nhất)
SELECT 
    c.CustomerID,
    c.CustomerName,
    s.OrderCount,
    s.TotalSpent
FROM Customers c
CROSS APPLY dbo.ufn_CustomerOrderSummary(c.CustomerID) s;
```

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Phân biệt chính xác 3 loại UDF (Scalar UDF, Inline TVF, Multi-Statement TVF)** | Trình bày rõ cú pháp và kiểu trả về của từng loại, nhấn mạnh Inline TVF không có BEGIN...END và tương đương View có tham số. | Understand | **3.5đ** |
| **Phân tích đúng nguyên nhân nghẽn hiệu năng RBAR và chặn Parallelism của Scalar UDF** | Chỉ rõ việc gọi lặp hàm theo từng dòng (Row-by-row) và hạn chế ép thực thi đơn luồng trên bảng lớn. | Analyze | **3.0đ** |
| **Viết đúng mã T-SQL cho Inline TVF (RETURNS TABLE AS RETURN (...)) và CROSS APPLY** | Cài đặt chuẩn cú pháp Inline TVF và viết câu truy vấn kết hợp CROSS APPLY tối ưu hóa theo tập hợp. | Apply | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 34. `DBI202-PRAC-034`: Phân tích ưu nhược điểm của 3 chiến lược ánh xạ mối quan hệ kế thừa ISA sang CSDL quan hệ

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-034` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO4** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO4** (`00000000-0000-0000-0000-000000000664`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 22 (Slot 11) — ERD Subclassing & Mapping |
| **Mục tiêu sư phạm** | Kiểm tra năng lực phân tích kiến trúc khi chuyển đổi các cấu trúc phân cấp kế thừa (Generalization/Specialization) sang mô hình bảng. |

**Nội dung câu hỏi phát vấn:**
> Khi chuyển đổi một mối quan hệ kế thừa phân cấp (ISA Hierarchy, ví dụ: Lớp cha Person có 2 lớp con là Student và Instructor) sang cơ sở dữ liệu quan hệ, hãy phân tích chi tiết 3 chiến lược thiết kế phổ biến: (1) Tạo bảng cho từng thực thể (ER Approach / Table-per-Type); (2) Tạo bảng chỉ cho các lớp lá (OO Approach / Table-per-Concrete-Class); (3) Gộp tất cả vào một bảng duy nhất (Single Table / Table-per-Hierarchy). Đánh giá ưu nhược điểm của từng phương án.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Phương án gộp tất cả vào một bảng duy nhất (Single Table) gây ra nhược điểm lớn gì về mặt ràng buộc toàn vẹn dữ liệu (NOT NULL)?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> 3 chiến lược ánh xạ quan hệ kế thừa: (1) Table-per-Type (ER Approach): Tạo bảng riêng cho cả lớp cha và từng lớp con. Bảng Person(PersonID, Name); Bảng Student(PersonID, GPA); Bảng Instructor(PersonID, Salary). PersonID ở bảng con vừa là Khóa chính vừa là Khóa ngoại trỏ về bảng cha. Ưu điểm: Chuẩn hóa 3NF tuyệt đối, không có giá trị NULL dư thừa, dễ dàng mở rộng lớp con mới. Nhược điểm: Truy vấn thông tin đầy đủ bắt buộc phải dùng JOIN nhiều bảng làm chậm hiệu năng. (2) Table-per-Concrete-Class (OO Approach): Chỉ tạo bảng cho các lớp con lá, mỗi bảng chứa toàn bộ thuộc tính của lớp cha và lớp con đó. Bảng Student(StudentID, Name, GPA); Bảng Instructor(InstructorID, Name, Salary). Ưu điểm: Truy vấn thực thể con nhanh chóng không cần JOIN. Nhược điểm: Trùng lặp cấu trúc thuộc tính chung ở nhiều bảng; rất khó truy vấn danh sách tổng hợp toàn bộ Person; không hỗ trợ một người vừa là Student vừa là Instructor (Overlapping). (3) Table-per-Hierarchy (Single Table): Gộp toàn bộ vào 1 bảng Person(PersonID, Name, Discriminator, GPA, Salary). Ưu điểm: Hiệu năng cao nhất, không cần JOIN, truy vấn cực kỳ đơn giản. Nhược điểm: Bảng bị phình to chứa rất nhiều giá trị NULL ở các cột đặc thù của lớp khác, và KHÔNG THỂ đặt ràng buộc NOT NULL trên các cột nghiệp vụ bắt buộc của lớp con (ví dụ không thể đặt GPA NOT NULL vì Instructor không có GPA).

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Phân tích chiến lược Table-per-Type (TPT)** | Mô tả đúng cấu trúc bảng cha/con, khóa ngoại và đánh giá ưu thế 3NF cùng nhược điểm phép JOIN. | Analyze | **3.5đ** |
| **Phân tích chiến lược Table-per-Concrete-Class (TPC)** | Mô tả đúng việc chỉ tạo bảng lá, ưu điểm không JOIN và nhược điểm khó truy vấn tổng hợp. | Analyze | **3.0đ** |
| **Phân tích chiến lược Single Table (TPH) & Hạn chế NOT NULL** | Chỉ rõ cơ chế dùng cột Discriminator, ưu thế tốc độ và nhược điểm nghiêm trọng mất ràng buộc NOT NULL. | Analyze | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 35. `DBI202-PRAC-035`: Tác động của Cursor đến hiệu năng hệ thống và giải pháp chuyển đổi sang tư duy tập hợp (Set-based)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-035` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO6** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO6** (`00000000-0000-0000-0000-000000000666`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 53 (Slot 27) — T-SQL Programming (Cursors vs Set-based) |
| **Mục tiêu sư phạm** | Đánh giá mức độ nhận thức về sự khác biệt giữa tư duy lập trình tuần tự (Procedural RBAR) và tư duy xử lý tập hợp (Set-based). |

**Nội dung câu hỏi phát vấn:**
> Cursor (Con trỏ) trong T-SQL là gì? Tại sao trong các hệ quản trị cơ sở dữ liệu quan hệ, việc lạm dụng Cursor bị coi là một Anti-pattern gây suy giảm hiệu năng nghiêm trọng? Nêu các giải pháp kỹ thuật hiện đại trong SQL để thay thế Cursor.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Khái niệm RBAR trong tối ưu hóa cơ sở dữ liệu là viết tắt của cụm từ gì và phản ánh bản chất gì của Cursor?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Cursor là cơ chế trong T-SQL cho phép lập trình viên duyệt và xử lý dữ liệu theo từng dòng tuần tự (Row-by-Row) giống như vòng lặp trong các ngôn ngữ lập trình thủ tục (C/C++, Java). Thuật ngữ RBAR là viết tắt của 'Row-By-Agonizing-Row' (Duyệt từng dòng đầy thống khổ), phản ánh sự xung đột trực tiếp với triết lý thiết kế cốt lõi của RDBMS là Xử lý theo Tập hợp (Set-based processing). Lý do Cursor là Anti-pattern gây thảm họa hiệu năng: (1) Chi phí tài nguyên: Cursor duy trì trạng thái con trỏ liên tục trên bộ nhớ RAM của máy chủ SQL Server; (2) Khóa và tương tranh: Cursor thường giữ các khóa tài nguyên (Locks) trong thời gian dài duyệt qua từng dòng, gây nghẽn và Deadlock hàng loạt; (3) Triệt tiêu khả năng tối ưu hóa: Bộ xử lý truy vấn không thể tận dụng cơ chế xử lý song song (Parallelism) và các thuật toán kết nối tối ưu (Hash Join, Merge Join). Các giải pháp thay thế Cursor: (a) Sử dụng các câu lệnh UPDATE/INSERT trực tiếp dựa trên mệnh đề JOIN hoặc Subquery; (b) Tận dụng các hàm cửa sổ (Window Functions như ROW_NUMBER, RANK, LEAD, LAG, SUM() OVER); (c) Sử dụng Biểu thức bảng chung đệ quy (Recursive CTE); (d) Áp dụng bảng tạm hoặc toán tử CROSS APPLY.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Khái niệm Cursor & Thuật ngữ RBAR** | Nêu đúng định nghĩa duyệt từng dòng và giải nghĩa chính xác thuật ngữ Row-By-Agonizing-Row. | Understand | **3.0đ** |
| **Phân tích 3 nguyên nhân Cursor làm suy giảm hiệu năng** | Phân tích sâu sắc về chi phí RAM, hiện tượng giữ khóa gây Deadlock và vô hiệu hóa Parallelism. | Analyze | **4.0đ** |
| **Các giải pháp kỹ thuật thay thế chuẩn Set-based** | Đề xuất đầy đủ các công cụ hiện đại: Window Functions, Recursive CTE, APPLY và JOIN. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 36. `DBI202-PRAC-036`: Thiết kế và quản lý Giao dịch lồng ghép (Nested Transactions) và Điểm lưu (Savepoints) trong T-SQL

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-PRAC-036` |
| **Kho dữ liệu** | `practice_questions` (MF-01 / MF-02) |
| **Ánh xạ CLO FLM** | **CLO7** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO7** (`00000000-0000-0000-0000-000000000667`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 43-44 (Slot 22) — 7.2 Transaction Processing & Savepoints |
| **Mục tiêu sư phạm** | Kiểm tra kỹ năng quản trị giao dịch nâng cao, cơ chế biến @@TRANCOUNT và kỹ thuật Rollback có chọn lọc. |

**Nội dung câu hỏi phát vấn:**
> Trong MS SQL Server, biến hệ thống @@TRANCOUNT hoạt động như thế nào khi gặp các lệnh BEGIN TRANSACTION, COMMIT TRANSACTION và ROLLBACK TRANSACTION? Tại sao lệnh ROLLBACK TRANSACTION thông thường không thể khôi phục một phần của giao dịch và giải pháp SAVE TRANSACTION (Savepoint) giải quyết vấn đề này như thế nào?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu trong một chuỗi gồm 3 mức BEGIN TRAN lồng nhau, ta thực thi một lệnh ROLLBACK TRANSACTION không truyền tên Savepoint thì giá trị của @@TRANCOUNT sẽ trở thành bao nhiêu?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Biến hệ thống `@@TRANCOUNT` theo dõi số lượng mức giao dịch đang hoạt động: (1) Mỗi khi thực thi `BEGIN TRANSACTION`, giá trị `@@TRANCOUNT` tự động tăng thêm 1; (2) Mỗi khi thực thi `COMMIT TRANSACTION`, giá trị `@@TRANCOUNT` giảm đi 1; dữ liệu chỉ thực sự được ghi vĩnh viễn xuống đĩa (Physical Commit) khi `@@TRANCOUNT` giảm về đúng 0; (3) Khi thực thi một lệnh `ROLLBACK TRANSACTION` thông thường (không truyền tên điểm lưu), toàn bộ mọi thao tác của tất cả các mức giao dịch lồng nhau sẽ bị hủy bỏ hoàn toàn và `@@TRANCOUNT` lập tức bị đặt về 0 (ngay cả khi trước đó đang ở mức 3 hay mức 10). Do đó, SQL Server không thực sự hỗ trợ giao dịch con độc lập. Để có thể khôi phục một phần công việc (Partial Rollback) mà không hủy bỏ toàn bộ giao dịch ngoài, ta phải sử dụng Điểm lưu (Savepoints) thông qua cú pháp: `SAVE TRANSACTION <SavepointName>`. Khi có lỗi cục bộ xảy ra, ta thực thi `ROLLBACK TRANSACTION <SavepointName>`: hệ thống chỉ hoàn tác các câu lệnh kể từ thời điểm đặt Savepoint trở lại đây, trong khi giá trị `@@TRANCOUNT` vẫn được giữ nguyên và giao dịch chính vẫn tiếp tục chạy để COMMIT an toàn.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Cơ chế hoạt động của biến @@TRANCOUNT** | Giải thích rõ ràng quy tắc tăng giảm của @@TRANCOUNT và điều kiện commit vật lý khi về 0. | Understand | **3.5đ** |
| **Hành vi của lệnh ROLLBACK thông thường** | Chỉ rõ ROLLBACK đưa @@TRANCOUNT ngay lập tức về 0 và hủy bỏ toàn bộ chuỗi lồng nhau. | Understand | **3.0đ** |
| **Kỹ thuật Savepoint (SAVE TRANSACTION) và Partial Rollback** | Trình bày chính xác cơ chế rollback có chọn lọc bảo toàn giao dịch chính không bị gián đoạn. | Apply | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

## PHẦN 4: CHI TIẾT 26 CÂU HỎI KHO THI THẬT PHÒNG LAB KIOSK (`exam_questions`)

> **Đặc điểm luồng MF-04 (Phòng Lab Kiosk):**
> - Cô lập hoàn toàn với kho luyện tập theo Quy tắc R8, bám sát Ma trận 5 Slots Đề thi của Trưởng Bộ Môn.
> - 100% câu hỏi có Barem Rubric 10.0đ, Model Answer >= 50 ký tự và FollowUpPrompt phục vụ hỏi xoáy theo cấu hình Trưởng Bộ Môn.

### 1. `DBI202-EXAM-001`: Các giai đoạn Tiến hóa của Hệ CSDL và 5 Nhược điểm Cốt lõi của Hệ thống Quản lý Tệp phẳng (CQ1.1)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-001` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 1 (Kiến trúc CSDL & Mô hình quan hệ)** |
| **Ánh xạ CLO FLM** | **CLO1** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO1** (`00000000-0000-0000-0000-000000000661`) |
| **Mức nhận thức Bloom** | **Remember** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 1-2 (Slot 1) — 1.2 Evolution of Database Systems (CQ1.1) |
| **Mục tiêu đánh giá** | Đánh giá mức độ nắm vững lịch sử tiến hóa của các hệ quản trị CSDL theo câu hỏi Constructive Question CQ1.1 trong Syllabus và lý do chuyển dịch từ File-based sang RDBMS. |

**Nội dung câu hỏi phát vấn:**
> Theo nội dung tiến hóa của các hệ cơ sở dữ liệu (Constructive Question `CQ1.1`), hãy trình bày các giai đoạn phát triển chính từ Hệ thống quản lý tệp truyền thống (`File-based Systems`) đến Hệ quản trị CSDL quan hệ (`RDBMS`) và hiện đại. Phân tích 5 nhược điểm nghiêm trọng của việc lưu trữ dữ liệu nghiệp vụ trên các tệp phẳng độc lập khiến doanh nghiệp bắt buộc phải sử dụng DBMS.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Trong 5 nhược điểm của hệ thống tệp phẳng, hiện tượng 'Phụ thuộc chương trình - dữ liệu' (Program-Data Dependence) gây tốn kém chi phí bảo trì phần mềm như thế nào khi doanh nghiệp muốn thêm một trường mới vào bản ghi khách hàng?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> 1. Các giai đoạn tiến hóa lịch sử của Hệ thống Cơ sở dữ liệu (`CQ1.1`):
* **Giai đoạn 1 — Hệ thống quản lý tệp phẳng (`File-Processing Systems`, trước 1960s):** Dữ liệu được lưu trữ rời rạc trong các tệp văn bản/nhị phân riêng lẻ của từng phòng ban; mỗi ứng dụng tự viết mã nguồn C/COBOL để đọc/ghi định dạng tệp của riêng mình.
* **Giai đoạn 2 — Hệ CSDL Phân cấp và Mạng (`Hierarchical & Network DBMS`, 1960s–1970s):** Tiêu biểu là IBM IMS (mô hình cây phân cấp 1:N) và CODASYL DBTG (mô hình đồ thị mạng). Tốc độ truy xuất nhanh nhưng cấu trúc cứng nhắc, lập trình viên phải điều hướng con trỏ vật lý cực kỳ phức tạp.
* **Giai đoạn 3 — Hệ quản trị CSDL Quan hệ (`Relational DBMS - RDBMS`, từ 1970 đến nay):** Do E.F. Codd khởi xướng. Dữ liệu được trừu tượng hóa thành các Bảng (Relations) 2 chiều dựa trên lý thuyết tập hợp và Đại số quan hệ, sử dụng ngôn ngữ truy vấn phi thủ tục chuẩn hóa **SQL**, tách biệt hoàn toàn mức logic khỏi mức lưu trữ vật lý.
* **Giai đoạn 4 — Hệ CSDL Hướng đối tượng, Quan hệ-Đối tượng và NoSQL / NewSQL (1990s–nay):** Mở rộng cho dữ liệu bán cấu trúc, phi cấu trúc (JSON, Graph, Key-Value) và phân tán đám mây.

2. Năm (05) nhược điểm chí mạng của Hệ thống tệp phẳng (`File-based Systems`):
1. **Dư thừa và Không nhất quán dữ liệu (`Data Redundancy & Inconsistency`):** Cùng một thông tin khách hàng bị sao chép ở nhiều tệp của phòng Kế toán và phòng Kinh doanh; khi cập nhật số điện thoại ở một nơi mà quên nơi kia sẽ gây mâu thuẫn dữ liệu.
2. **Phụ thuộc chặt giữa Chương trình và Dữ liệu (`Program-Data Dependence`):** Cấu trúc bản ghi được nhúng cứng trong mã nguồn ứng dụng; chỉ cần đổi thứ tự cột hoặc thêm 1 trường là phải sửa code và biên dịch lại toàn bộ phần mềm.
3. **Khó khăn khi truy cập và tổng hợp dữ liệu liên phòng ban (`Data Isolation`):** Không có ngôn ngữ truy vấn chung; mỗi báo cáo mới đều phải viết một chương trình đọc ghép tệp thủ công.
4. **Không đảm bảo tính Nguyên tố khi xảy ra sự cố (`Atomicity Problems`):** Nếu hệ thống mất điện giữa lúc đang chuyển tiền giữa 2 tệp, dữ liệu sẽ rơi vào trạng thái hỏng một nửa mà không có cơ chế tự động khôi phục (`Recovery/WAL`).
5. **Dị thường truy cập đồng thời và Thiếu an ninh phân quyền (`Concurrent-Access Anomalies & Security Gaps`):** Hai nhân viên cùng ghi vào một tệp dễ ghi đè mất dữ liệu của nhau (`Lost Update`) và không thể phân quyền chi tiết đến từng cột/dòng.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Trình bày đúng 4 giai đoạn tiến hóa của hệ CSDL (CQ1.1)** | Nêu rõ tiến trình từ File-based -> Hierarchical/Network -> Relational DBMS (E.F. Codd, SQL) -> NoSQL/NewSQL. | Remember | **3.5đ** |
| **Phân tích rõ sự dư thừa/bất nhất và phụ thuộc chương trình - dữ liệu** | Giải thích cụ thể nguyên nhân gây mâu thuẫn dữ liệu và việc phải sửa code ứng dụng khi đổi cấu trúc tệp phẳng. | Understand | **3.5đ** |
| **Phân tích 3 nhược điểm về Atomicity, Concurrency và Bảo mật/Cô lập dữ liệu** | Chỉ rõ rủi ro mất toàn vẹn khi crash giữa chừng, xung đột ghi đè đồng thời và thiếu ngôn ngữ truy vấn chuẩn. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 2. `DBI202-EXAM-002`: Định nghĩa và Ký hiệu hình thức của 5 phép toán cơ bản trong Đại số quan hệ

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-002` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 1 (Lý thuyết nền tảng CSDL & Mô hình)** |
| **Ánh xạ CLO FLM** | **CLO2** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO2** (`00000000-0000-0000-0000-000000000662`) |
| **Mức nhận thức Bloom** | **Remember** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 5 (Slot 3) — Algebraic Query Language |
| **Mục tiêu đánh giá** | Đánh giá khả năng ghi nhớ chính xác ký hiệu toán học và bản chất của 5 phép toán tập hợp trong đại số quan hệ. |

**Nội dung câu hỏi phát vấn:**
> Nêu tên gọi, ký hiệu hình thức và bản chất xử lý của 5 phép toán nguyên thủy (Primitive Operations) tạo nên nền tảng của Đại số quan hệ. Phép kết nối tự nhiên (Natural Join) có phải là phép toán nguyên thủy không?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Hãy biểu diễn phép Kết nối tự nhiên (Natural Join R ⋈ S) và phép Giao tập hợp (Intersection R ∩ S) chỉ bằng cách sử dụng các phép toán nguyên thủy trong 5 phép toán nền tảng của Đại số quan hệ."*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> 5 phép toán nguyên thủy cơ bản trong Đại số quan hệ: (1) Phép chọn (Selection - ký hiệu sigma: σ_p(R)): Lọc các bộ (dòng) của quan hệ R thỏa mãn vị từ logic p. (2) Phép chiếu (Projection - ký hiệu pi: π_A(R)): Trích xuất danh sách các thuộc tính A từ quan hệ R và khử trùng lặp. (3) Tích Descartes (Cartesian Product - ký hiệu dấu nhân: R × S): Kết hợp mỗi bộ của R với tất cả các bộ của S; bậc của kết quả bằng tổng bậc của R và S. (4) Phép hợp (Union - ký hiệu: R ∪ S): Trả về tập hợp các bộ thuộc R hoặc thuộc S hoặc thuộc cả hai; đòi hỏi R và S phải Khả hợp (Union-compatible). (5) Phép hiệu (Set Difference - ký hiệu: R - S): Trả về tập hợp các bộ thuộc R nhưng không thuộc S; đòi hỏi R và S phải khả hợp. Phép kết nối tự nhiên (Natural Join - ký hiệu ⋈) KHÔNG PHẢI là phép toán nguyên thủy; nó là một phép toán phái sinh (Derived Operation) được định nghĩa bằng sự kết hợp của: Tích Descartes (×), Phép chọn (σ trên các thuộc tính cùng tên) và Phép chiếu (π loại bỏ cột trùng).

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Liệt kê và ký hiệu đúng 5 phép toán nguyên thủy** | Nêu đúng ký hiệu hình thức σ, π, ×, ∪, - và tên gọi tương ứng. | Remember | **4.0đ** |
| **Bản chất xử lý và điều kiện khả hợp** | Giải thích đúng cơ chế lọc dòng, lọc cột, ghép tích và điều kiện union-compatible. | Remember | **3.5đ** |
| **Phân tích bản chất của Phép kết nối tự nhiên** | Khẳng định Natural Join là phép phái sinh cấu thành từ tích Descartes, phép chọn và phép chiếu. | Understand | **2.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 3. `DBI202-EXAM-003`: Hệ tiên đề Armstrong cho Phụ thuộc hàm

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-003` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 3 (Chuẩn hóa lược đồ & Ràng buộc toàn vẹn)** |
| **Ánh xạ CLO FLM** | **CLO3** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO3** (`00000000-0000-0000-0000-000000000663`) |
| **Mức nhận thức Bloom** | **Remember** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 10 (Slot 5) — Rules About Functional Dependencies |
| **Mục tiêu đánh giá** | Kiểm tra khả năng tái hiện chính xác 3 tiên đề Armstrong cơ bản và các quy tắc suy dẫn bổ trợ. |

**Nội dung câu hỏi phát vấn:**
> Hệ tiên đề Armstrong là gì? Hãy phát biểu chính xác bằng ký hiệu toán học 3 tiên đề cơ bản của Armstrong. Nêu thêm 2 quy tắc bổ trợ phái sinh từ hệ tiên đề này và giải thích ý nghĩa tính Đúng đắn (Soundness) và Đầy đủ (Completeness).

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Dựa vào 3 tiên đề cơ bản của Armstrong, hãy chứng minh chặt chẽ Quy tắc Hợp (Union Rule): Nếu X -> Y và X -> Z thì X -> YZ. Quy tắc ngược lại (phân rã vế trái) có luôn đúng không?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Hệ tiên đề Armstrong là tập hợp các quy tắc suy luận dùng để suy dẫn tất cả các phụ thuộc hàm logic phát sinh từ một tập phụ thuộc hàm F ban đầu. 3 tiên đề cơ bản: (1) Tính phản xạ (Reflexivity): Nếu Y là tập con của X (Y ⊆ X), thì X -> Y. (2) Tính tăng trưởng (Augmentation): Nếu X -> Y, thì XZ -> YZ với mọi tập thuộc tính Z. (3) Tính bắc cầu (Transitivity): Nếu X -> Y và Y -> Z, thì X -> Z. Hai quy tắc bổ trợ phái sinh phổ biến: Quy tắc phân rã (Decomposition: nếu X -> YZ thì X -> Y và X -> Z); Quy tắc hợp (Union: nếu X -> Y và X -> Z thì X -> YZ). Ý nghĩa tính chất: Hệ tiên đề Armstrong có tính Đúng đắn (Sound) nghĩa là mọi phụ thuộc hàm suy dẫn ra từ các tiên đề đều đúng đắn về mặt ngữ nghĩa (không sinh ra phụ thuộc hàm sai); và có tính Đầy đủ (Complete) nghĩa là mọi phụ thuộc hàm hợp lệ có thể suy luận được từ F đều có thể được chứng minh thông qua hệ tiên đề này.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Phát biểu chính xác 3 tiên đề cơ bản Armstrong** | Nêu đúng ký hiệu toán học của Reflexivity, Augmentation và Transitivity. | Remember | **4.0đ** |
| **Nêu đúng các quy tắc suy dẫn bổ trợ** | Trình bày chính xác quy tắc Phân rã (Decomposition) và quy tắc Hợp (Union). | Remember | **3.0đ** |
| **Giải thích tính Đúng đắn (Sound) và Đầy đủ (Complete)** | Giải thích rõ ràng giá trị toán học của hệ tiên đề trong việc tìm bao đóng phụ thuộc hàm. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 4. `DBI202-EXAM-004`: Định nghĩa Khóa chính, Siêu khóa và Khóa ứng viên trong lý thuyết CSDL

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-004` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 3 (Chuẩn hóa lược đồ & Ràng buộc toàn vẹn)** |
| **Ánh xạ CLO FLM** | **CLO3** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO3** (`00000000-0000-0000-0000-000000000663`) |
| **Mức nhận thức Bloom** | **Remember** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 11 (Slot 6) — Design Theory (Keys) |
| **Mục tiêu đánh giá** | Đánh giá mức độ nắm vững các khái niệm toán học về khóa trong lý thuyết thiết kế cơ sở dữ liệu quan hệ. |

**Nội dung câu hỏi phát vấn:**
> Trong lý thuyết cơ sở dữ liệu quan hệ, hãy định nghĩa chính xác các khái niệm: Siêu khóa (Superkey), Khóa ứng viên (Candidate Key) và Khóa chính (Primary Key). Mối quan hệ tập hợp giữa 3 khái niệm này là gì?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Một quan hệ có thể có nhiều Khóa ứng viên không? Điểm khác biệt mấu chốt giữa Khóa ứng viên và Siêu khóa là gì, và tại sao một Khóa ứng viên tối thiểu (Minimal Superkey) lại không được phép chứa bất kỳ tập con thực sự nào cũng là một Siêu khóa?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Cho lược đồ quan hệ R: (1) Siêu khóa (Superkey - SK): Là một tập hợp thuộc tính K ⊆ R sao cho bao đóng K+ bao trùm toàn bộ thuộc tính của quan hệ (K+ = R). Giá trị của siêu khóa đảm bảo định danh duy nhất mỗi bộ (dòng) trong bảng, không thể có hai dòng khác nhau có cùng giá trị trên K. (2) Khóa ứng viên (Candidate Key - CK): Là một Siêu khóa tối tiểu (Minimal Superkey); nghĩa là K là siêu khóa và không tồn tại bất kỳ tập con thực sự K' ⊂ K nào mà K' cũng là một siêu khóa. Nếu bỏ bớt dù chỉ một thuộc tính ra khỏi CK thì nó sẽ mất đi khả năng định danh duy nhất. (3) Khóa chính (Primary Key - PK): Là một trong các Khóa ứng viên được người thiết kế cơ sở dữ liệu lựa chọn chính thức để làm định danh chính cho bảng; giá trị của khóa chính bắt buộc duy nhất và tuyệt đối không được chứa giá trị NULL. Mối quan hệ bao hàm: Mọi Khóa chính đều là Khóa ứng viên; mọi Khóa ứng viên đều là Siêu khóa. Quan hệ bao hàm tập hợp: PK ∈ {Candidate Keys} ⊆ {Superkeys}.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa Siêu khóa (Superkey)** | Nêu đúng tính chất bao đóng K+ = R và khả năng định danh duy nhất. | Remember | **3.0đ** |
| **Định nghĩa Khóa ứng viên (Candidate Key)** | Khẳng định bản chất siêu khóa tối tiểu (không có tập con thực sự nào là siêu khóa). | Remember | **3.5đ** |
| **Định nghĩa Khóa chính & Mối quan hệ bao hàm** | Nêu đúng PK là 1 CK được chọn (cấm NULL) và thể hiện quan hệ PK ⊆ CK ⊆ SK. | Remember | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 5. `DBI202-EXAM-005`: Danh mục Ràng buộc Toàn vẹn Dữ liệu chuẩn trong SQL DDL

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-005` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 3 (Chuẩn hóa lược đồ & Ràng buộc toàn vẹn)** |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Remember** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 29-30 (Slot 15) — SQL Constraints on Attributes |
| **Mục tiêu đánh giá** | Kiểm tra khả năng tái hiện đầy đủ 6 ràng buộc toàn vẹn chuẩn ANSI SQL trong câu lệnh DDL. |

**Nội dung câu hỏi phát vấn:**
> Liệt kê 6 ràng buộc toàn vẹn dữ liệu (Constraints) được hỗ trợ chuẩn trong ngôn ngữ SQL DDL. Nêu mục đích nghiệp vụ của từng ràng buộc và cho một câu lệnh CREATE TABLE minh họa đồng thời ít nhất 4 ràng buộc đó.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Trong chuẩn SQL và MS SQL Server, cột có ràng buộc UNIQUE có chấp nhận giá trị NULL không, và nếu có thì chấp nhận tối đa bao nhiêu giá trị NULL? Sự khác biệt căn bản giữa UNIQUE Constraint và UNIQUE Index là gì?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> 6 ràng buộc toàn vẹn chuẩn trong SQL DDL: (1) NOT NULL: Bắt buộc thuộc tính phải có giá trị, cấm để trống. (2) UNIQUE: Đảm bảo giá trị trong cột (hoặc tổ hợp cột) không được trùng lặp giữa các dòng. (3) PRIMARY KEY: Định danh duy nhất mỗi dòng trong bảng, ngầm định là NOT NULL và UNIQUE. (4) FOREIGN KEY: Ràng buộc toàn vẹn tham chiếu, đảm bảo giá trị phải tồn tại trong khóa chính bảng cha hoặc mang giá trị NULL. (5) CHECK: Kiểm tra điều kiện logic trên giá trị dữ liệu của dòng (ví dụ: Score >= 0 AND Score <= 10). (6) DEFAULT: Tự động gán giá trị mặc định cho cột khi lệnh INSERT không cung cấp giá trị. Ví dụ minh họa: 
```sql
CREATE TABLE Employees (
    EmpID INT PRIMARY KEY,
    NationalID VARCHAR(20) NOT NULL UNIQUE,
    Salary DECIMAL(10,2) CHECK (Salary >= 0),
    DepartmentID INT FOREIGN KEY REFERENCES Departments(DeptID),
    Status VARCHAR(20) DEFAULT 'Active'
);
```

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Liệt kê và nêu mục đích 6 ràng buộc chuẩn** | Nêu đúng NOT NULL, UNIQUE, PRIMARY KEY, FOREIGN KEY, CHECK, DEFAULT. | Remember | **4.0đ** |
| **Câu lệnh DDL minh họa chuẩn cú pháp T-SQL** | Viết đúng câu lệnh CREATE TABLE với các kiểu dữ liệu và cú pháp constraint chuẩn. | Apply | **3.5đ** |
| **Giải thích tính toàn vẹn của ràng buộc đã viết** | Chỉ rõ vai trò của từng ràng buộc trong bảng mẫu đối với an toàn dữ liệu. | Understand | **2.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 6. `DBI202-EXAM-006`: Định nghĩa và Phân loại 2 dạng DML Triggers chính trong MS SQL Server

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-006` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 5 (Lập trình T-SQL & Toàn vẹn nghiệp vụ)** |
| **Ánh xạ CLO FLM** | **CLO6** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO6** (`00000000-0000-0000-0000-000000000666`) |
| **Mức nhận thức Bloom** | **Remember** |
| **Mức độ khó** | **Easy** |
| **Phân bổ Session** | Session 49 (Slot 25) — T-SQL Programming (Triggers) |
| **Mục tiêu đánh giá** | Kiểm tra kiến thức cơ bản về định nghĩa Trigger và phân loại AFTER vs INSTEAD OF Trigger. |

**Nội dung câu hỏi phát vấn:**
> Trigger trong hệ quản trị cơ sở dữ liệu là gì? Trong MS SQL Server, DML Triggers được phân thành 2 dạng chính nào? Nêu sự khác nhau cơ bản về thời điểm thực thi giữa hai loại trigger này đối với thao tác DML.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Tại sao chúng ta không thể tạo AFTER Trigger trên một View phức hợp gồm nhiều bảng kết nối (JOIN), mà bắt buộc phải sử dụng INSTEAD OF Trigger? Quá trình điều hướng dữ liệu của INSTEAD OF Trigger diễn ra như thế nào?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Trigger là một dạng thủ tục lưu trữ đặc biệt (Special Stored Procedure) được lưu trong CSDL và tự động thực thi (kích hoạt) khi xảy ra một sự kiện xác định (sự kiện DML: INSERT, UPDATE, DELETE; hoặc sự kiện DDL: CREATE, ALTER, DROP). Trong MS SQL Server, DML Trigger được chia thành 2 loại chính: (1) AFTER Trigger (còn gọi là FOR Trigger): Được kích hoạt và thực thi SAU KHI câu lệnh DML gốc đã thực hiện xong các thao tác ghi dữ liệu vào bảng tạm và vượt qua toàn bộ các ràng buộc toàn vẹn cơ bản (Constraints). Nếu trong AFTER Trigger phát hiện vi phạm nghiệp vụ, lập trình viên phải gọi lệnh ROLLBACK TRANSACTION để hủy bỏ thay đổi. AFTER Trigger chỉ có thể được gắn trên Bảng vật lý (Tables), không gắn được trên Views chuẩn. (2) INSTEAD OF Trigger: Được kích hoạt THAY THẾ HOÀN TOÀN cho câu lệnh DML gốc; câu lệnh DML kích hoạt sẽ không thực thi trên bảng cơ sở mà nhường toàn bộ quyền điều khiển cho thân mã của trigger. INSTEAD OF Trigger có thể được gắn trên cả Bảng vật lý và Khung nhìn (Views), chuyên dùng để hỗ trợ cập nhật dữ liệu qua các View phức tạp.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa chính xác Trigger** | Nêu đúng Trigger là thủ tục tự động kích hoạt bởi sự kiện CSDL (DML/DDL). | Remember | **3.0đ** |
| **Phân loại và cơ chế của AFTER Trigger** | Giải thích rõ AFTER Trigger chạy sau DML và constraint, chỉ gắn trên bảng vật lý. | Remember | **3.5đ** |
| **Phân loại và cơ chế của INSTEAD OF Trigger** | Giải thích rõ INSTEAD OF thay thế DML gốc, gắn được trên cả View và Bảng. | Remember | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 7. `DBI202-EXAM-007`: Cơ chế ánh xạ mối kết hợp Một - Một (1:1) từ ERD sang Lược đồ quan hệ

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-007` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 1 (Lý thuyết nền tảng CSDL & Mô hình)** |
| **Ánh xạ CLO FLM** | **CLO4** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO4** (`00000000-0000-0000-0000-000000000664`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 20 (Slot 10) — High-Level Database Models (ERD to Relational) |
| **Mục tiêu đánh giá** | Đánh giá khả năng hiểu sâu về các phương án thiết kế khi ánh xạ quan hệ 1:1 và xử lý tối ưu giá trị NULL. |

**Nội dung câu hỏi phát vấn:**
> Khi chuyển đổi một mối kết hợp Một - Một (1:1) từ sơ đồ ERD sang mô hình cơ sở dữ liệu quan hệ, chúng ta có những phương án thiết kế nào? Việc lựa chọn phương án phụ thuộc vào ràng buộc tham gia (Total / Partial Participation) của các thực thể như thế nào?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu cả hai thực thể A và B trong quan hệ 1:1 đều có sự tham gia toàn phần (Total Participation - 100%), phương án thiết kế nào là tối ưu nhất để tránh phát sinh giá trị NULL và triệt tiêu hoàn toàn chi phí JOIN khi truy vấn?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Khi ánh xạ mối kết hợp 1:1 giữa thực thể A và B, có 3 phương án thiết kế: (Phương án 1 - Gộp thành 1 bảng duy nhất): Tạo một bảng duy nhất chứa toàn bộ thuộc tính của A và B. Phương án này tối ưu nhất khi CẢ HAI BÊN ĐỀU CÓ RÀNG BUỘC THAM GIA TOÀN PHẦN (Total - Total Participation); giúp loại bỏ hoàn toàn phép JOIN và không sinh ra giá trị NULL dư thừa. (Phương án 2 - Giữ 2 bảng, đưa Khóa ngoại sang một bên kèm ràng buộc UNIQUE): Đưa khóa chính của bảng này sang làm khóa ngoại của bảng kia, và BẮT BUỘC gán ràng buộc UNIQUE trên cột khóa ngoại đó để bảo đảm tỷ số 1:1. Nguyên tắc vàng: Nên đưa khóa ngoại sang phía thực thể có Ràng buộc tham gia Toàn phần (Total Participation) để cột khóa ngoại đó có thể đặt thêm ràng buộc NOT NULL, tránh triệt để việc xuất hiện các giá trị NULL dư thừa. (Phương án 3 - Dùng bảng trung gian): Tạo bảng thứ 3 lưu cặp khóa; phương án này chỉ nên dùng khi cả hai bên đều tham gia một phần (Partial - Partial) và tần suất liên kết rất hiếm, nhằm bảo vệ cả 2 bảng gốc không bị rác giá trị NULL.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Phương án gộp bảng (Merged Table)** | Giải thích rõ việc gộp bảng tối ưu khi cả hai bên cùng tham gia toàn phần (Total-Total). | Understand | **3.0đ** |
| **Phương án Khóa ngoại kèm ràng buộc UNIQUE** | Phân tích quy tắc đặt khóa ngoại về bên Total Participation để áp dụng NOT NULL. | Understand | **4.0đ** |
| **Phương án Bảng trung gian & Tối ưu hóa NULL** | Chỉ ra trường hợp Partial-Partial dùng bảng riêng để tránh lãng phí bộ nhớ lưu NULL. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 8. `DBI202-EXAM-008`: Biểu diễn Ràng buộc Toàn vẹn trên Lược đồ Quan hệ bằng Đại số Quan hệ (CQ3.2 & CQ3.3)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-008` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 1 (Kiến trúc CSDL & Mô hình quan hệ)** |
| **Ánh xạ CLO FLM** | **CLO2** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO2** (`00000000-0000-0000-0000-000000000662`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 7-8 (Slot 4) — 2.4 Algebraic Constraints on Relations (CQ3.2 & CQ3.3) |
| **Mục tiêu đánh giá** | Đánh giá khả năng sử dụng hai dạng phương trình Đại số quan hệ E = ∅ và E1 ⊆ E2 để đặc tả hình thức các ràng buộc khóa ngoại, miền giá trị và khóa chính. |

**Nội dung câu hỏi phát vấn:**
> Trong lý thuyết Mô hình Quan hệ (Constructive Questions `CQ3.2` & `CQ3.3`), mọi ràng buộc toàn vẹn đều có thể được biểu diễn hình thức bằng một trong hai dạng biểu thức Đại số quan hệ: (1) $E = \emptyset$ (biểu thức trả về tập rỗng) hoặc (2) $E_1 \subseteq E_2$ (phép bao hàm tập hợp). Cho hai quan hệ `Employees(EmpID, FullName, Salary, DeptID)` và `Departments(DeptID, DeptName)`. Hãy viết biểu thức Đại số quan hệ theo cả 2 cách để biểu diễn: (a) Ràng buộc khóa ngoại `Employees.DeptID` tham chiếu tới `Departments.DeptID`, và (b) Ràng buộc miền giá trị lương của mọi nhân viên phải lớn hơn hoặc bằng `500`.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Làm thế nào để biểu diễn ràng buộc Khóa chính (Functional Dependency `DeptID -> DeptName` không có hai bộ trùng `DeptID` nhưng khác `DeptName`) chỉ bằng phép Đổi tên (`ρ`), Phép Chọn (`σ`) và Tích Đề-các (`×`) với phương trình $E = \emptyset$?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> 1. Hai hình thức tương đương biểu diễn Ràng buộc Toàn vẹn trong Đại số quan hệ (`CQ3.2` & `CQ3.3`):
* **Dạng 1 — Tập hợp vi phạm bằng rỗng ($E = \emptyset$):** Xây dựng biểu thức đại số $E$ truy vấn tất cả các bộ dữ liệu **vi phạm** quy tắc nghiệp vụ, và đặt điều kiện $E = \emptyset$ (không tồn tại bộ vi phạm nào).
* **Dạng 2 — Bao hàm tập hợp ($E_1 \subseteq E_2$):** Mọi giá trị xuất hiện trong tập chiếu $E_1$ bắt buộc phải nằm trong tập hợp hợp lệ $E_2$. Hai dạng này hoàn toàn tương đương nhau vì:
  $$E_1 \subseteq E_2 \iff E_1 - E_2 = \emptyset$$

2. Áp dụng cho hai bài toán cụ thể trên `Employees` và `Departments`:
* **Bài toán (a): Ràng buộc toàn vẹn tham chiếu (Khóa ngoại `Employees.DeptID` $\to$ `Departments.DeptID`):**
  + *Biểu diễn theo dạng bao hàm ($E_1 \subseteq E_2$):* Tập hợp mọi mã phòng ban của nhân viên phải là tập con của tập mã phòng ban hiện có trong bảng `Departments`:
    $$\pi_{\text{DeptID}}(\text{Employees}) \subseteq \pi_{\text{DeptID}}(\text{Departments})$$
  + *Biểu diễn theo dạng tập rỗng ($E = \emptyset$):* Hiệu tập hợp giữa mã phòng ban trong `Employees` và mã phòng ban trong `Departments` phải bằng rỗng:
    $$\pi_{\text{DeptID}}(\text{Employees}) - \pi_{\text{DeptID}}(\text{Departments}) = \emptyset$$
* **Bài toán (b): Ràng buộc miền giá trị (`Salary >= 500` cho mọi nhân viên):**
  + *Biểu diễn theo dạng tập rỗng ($E = \emptyset$):* Tập hợp các nhân viên có mức lương nhỏ hơn `500` phải bằng rỗng:
    $$\sigma_{\text{Salary} < 500}(\text{Employees}) = \emptyset$$
  + *Biểu diễn theo dạng bao hàm ($E_1 \subseteq E_2$):* Toàn bộ quan hệ `Employees` phải là tập con của tập các bộ nhân viên thỏa mãn `Salary >= 500`:
    $$\text{Employees} \subseteq \sigma_{\text{Salary} \ge 500}(\text{Employees})$$

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Giải thích nguyên lý tương đương giữa E1 ⊆ E2 và E1 - E2 = ∅** | Trình bày rõ bản chất toán học của hai cách biểu diễn ràng buộc toàn vẹn bằng đại số quan hệ. | Understand | **3.0đ** |
| **Viết đúng biểu thức Ràng buộc Khóa ngoại bằng Phép Chiếu π và Phép Trừ -** | Viết chính xác π_DeptID(Employees) ⊆ π_DeptID(Departments) và π_DeptID(Employees) - π_DeptID(Departments) = ∅. | Apply | **3.5đ** |
| **Viết đúng biểu thức Ràng buộc Miền giá trị bằng Phép Chọn σ** | Viết chính xác σ_{Salary < 500}(Employees) = ∅ và dạng bao hàm tương đương. | Apply | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 9. `DBI202-EXAM-009`: Khái niệm Phủ tối tiểu (Minimal Cover / Canonical Cover) và Quy trình 3 Bước Thu gọn Tập Phụ thuộc Hàm

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-009` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 3 (Chuẩn hóa lược đồ & Ràng buộc toàn vẹn)** |
| **Ánh xạ CLO FLM** | **CLO3** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO3** (`00000000-0000-0000-0000-000000000663`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 10-11 (Slot 5-6) — 3.2 Functional Dependencies & Minimal Cover |
| **Mục tiêu đánh giá** | Đánh giá mức độ nắm vững 3 điều kiện của một Phủ tối tiểu (Minimal Cover) và thuật toán loại bỏ thuộc tính dư thừa, phụ thuộc hàm dư thừa. |

**Nội dung câu hỏi phát vấn:**
> Trong lý thuyết thiết kế CSDL quan hệ, thế nào là hai tập phụ thuộc hàm tương đương ($F \equiv G$) và một tập phụ thuộc hàm $F_c$ cần thỏa mãn **3 điều kiện chuẩn mực nào** để được gọi là **Phủ tối tiểu (Minimal Cover / Canonical Cover)** của $F$? Tại sao bước tìm Phủ tối tiểu $F_c$ luôn là bước khởi đầu bắt buộc trong Thuật toán tổng hợp chuẩn 3NF (Bernstein's 3NF Synthesis)?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Khi thực hiện 3 bước tìm Phủ tối tiểu, tại sao ta BẮT BUỘC phải thực hiện bước 'Loại bỏ thuộc tính dư thừa ở vế trái' TRƯỚC bước 'Loại bỏ phụ thuộc hàm dư thừa' (hoặc kiểm tra lại sau khi rút gọn vế trái)? Cho ví dụ minh họa."*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> 1. Khái niệm tương đương ($F \equiv G$) và 3 điều kiện của Phủ tối tiểu ($F_c$):
- Hai tập phụ thuộc hàm $F$ và $G$ trên cùng lược đồ $R$ được gọi là tương đương ($F \equiv G$) nếu bao đóng của chúng bằng nhau: $F^+ = G^+$ (mọi phụ thuộc hàm suy dẫn được từ $F$ đều suy dẫn được từ $G$ và ngược lại).
- Một tập phụ thuộc hàm $F_c$ tương đương với $F$ được gọi là **Phủ tối tiểu (Minimal Cover)** khi và chỉ khi thỏa mãn đồng thời **3 điều kiện**:
  1. **Vế phải đơn thuộc tính (Singleton Right-Hand Side):** Mọi phụ thuộc hàm trong $F_c$ đều có dạng $X \to A$, trong đó $A$ chỉ gồm **duy nhất 1 thuộc tính đơn lẻ**.
  2. **Không có thuộc tính dư thừa ở vế trái (No Extraneous Attributes in LHS):** Với mọi $X \to A \in F_c$, không tồn tại bất kỳ tập con thực sự $Y \subsetneq X$ nào sao cho $(F_c - \{X \to A\}) \cup \{Y \to A\} \equiv F_c$ (kiểm tra bằng cách xem $A \in Y^+$ hoặc thuộc tính bị bỏ có suy ra được từ các thuộc tính còn lại của $X$ hay không).
  3. **Không có phụ thuộc hàm dư thừa (No Redundant Dependencies):** Không tồn tại bất kỳ phụ thuộc hàm $X \to A \in F_c$ nào mà khi loại bỏ nó đi, tập còn lại $F_c - \{X \to A\}$ vẫn tương đương với $F_c$ (tức là $A \in X^+_{\text{trên } F_c - \{X \to A\}}$).

2. Tại sao tìm Phủ tối tiểu $F_c$ là bước bắt buộc đầu tiên khi tổng hợp chuẩn 3NF:
- Nếu không rút gọn thuộc tính dư thừa ở vế trái trước khi tạo bảng con, thuật toán sẽ chọn vế trái bị thừa làm khóa của bảng con, dẫn đến tạo ra các bảng vi phạm ngay chuẩn 2NF (phụ thuộc bộ phận).
- Nếu không loại bỏ các phụ thuộc hàm bắc cầu dư thừa (ví dụ có $A \to B, B \to C$ và cả $A \to C$), thuật toán sẽ sinh ra bảng dư thừa $R_k(A, C)$ gây trùng lặp dữ liệu và tốn chi phí lưu trữ không cần thiết.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Định nghĩa đúng hai tập phụ thuộc hàm tương đương F+ = G+** | Nêu rõ khái niệm bao đóng tập phụ thuộc hàm và điều kiện suy dẫn hai chiều giữa F và G. | Understand | **2.5đ** |
| **Trình bày chính xác 3 điều kiện của Phủ tối tiểu Fc** | Nêu đủ: (1) Vế phải 1 thuộc tính, (2) Vế trái không có thuộc tính dư thừa, (3) Không có phụ thuộc hàm dư thừa. | Understand | **4.5đ** |
| **Giải thích vai trò của Fc trong thuật toán tổng hợp 3NF và thứ tự thực hiện** | Chỉ rõ việc rút gọn vế trái trước giúp phát hiện các phụ thuộc trùng lặp và ngăn sinh bảng con dư thừa hoặc vi phạm 2NF. | Analyze | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 10. `DBI202-EXAM-010`: Thứ tự Thực thi Logic của các mệnh đề trong Câu truy vấn SQL

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-010` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 2 (Phân tích kịch bản truy vấn SQL)** |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 31-38 (Slot 16-19) — SQL Logical Query Processing |
| **Mục tiêu đánh giá** | Kiểm tra mức độ am hiểu về vòng đời xử lý nội bộ của bộ tối ưu hóa truy vấn SQL Server. |

**Nội dung câu hỏi phát vấn:**
> Trình bày thứ tự thực thi logic đầy đủ gồm 6 giai đoạn chính của một câu truy vấn SQL chuẩn (FROM, WHERE, GROUP BY, HAVING, SELECT, ORDER BY). Dựa vào thứ tự này, hãy giải thích tại sao ta không thể dùng bí danh cột (Alias) đặt ở SELECT trong mệnh đề WHERE, nhưng lại CÓ THỂ dùng bí danh đó trong mệnh đề ORDER BY?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu mệnh đề SELECT chứa biểu thức tính toán phức tạp như hàm cửa sổ ROW_NUMBER() hoặc toán tử CASE...WHEN, làm thế nào để chúng ta có thể lọc dữ liệu theo kết quả tính toán đó ở mệnh đề WHERE mà không cần phải lặp lại toàn bộ biểu thức tính toán nhiều lần?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Thứ tự thực thi logic chuẩn của Query Processor trong DBMS: 
(1) FROM & JOIN: Xác định các bảng nguồn và thực hiện các phép kết nối bảng để tạo tập dữ liệu cơ sở; 
(2) WHERE: Đánh giá vị từ trên từng dòng để lọc bỏ các dòng không thỏa mãn; 
(3) GROUP BY: Gom các dòng dữ liệu thành các nhóm theo các thuộc tính chỉ định; 
(4) HAVING: Lọc bỏ các nhóm dữ liệu không thỏa mãn điều kiện tổng hợp; 
(5) SELECT & OVER (Window Functions): Tính toán các biểu thức, trích xuất danh sách cột chỉ định, gán bí danh (Alias) và khử trùng lặp (DISTINCT nếu có); 
(6) ORDER BY: Sắp xếp tập kết quả cuối cùng theo thứ tự chỉ định. 
Giải thích về bí danh (Alias): Mệnh đề WHERE được thực thi ở Bước 2, trước khi Bước 5 (SELECT) được kích hoạt; do đó tại thời điểm WHERE đánh giá điều kiện, bí danh cột được khai báo tại SELECT hoàn toàn chưa tồn tại trong ngữ cảnh thực thi, dẫn đến lỗi cú pháp 'Invalid column name'. Ngược lại, mệnh đề ORDER BY được thực thi ở Bước 6, tức là SAU KHI mệnh đề SELECT đã xử lý xong và đã gắn nhãn bí danh cho các cột; vì vậy ORDER BY hoàn toàn nhận biết và sử dụng được các bí danh đã đặt ở SELECT.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Trình bày chính xác thứ tự thực thi 6 giai đoạn** | Nêu đúng trình tự: FROM -> WHERE -> GROUP BY -> HAVING -> SELECT -> ORDER BY. | Understand | **4.0đ** |
| **Giải thích lý do WHERE không dùng được Alias** | Lập luận chính xác do WHERE chạy ở bước 2 trước khi SELECT gán alias ở bước 5. | Understand | **3.0đ** |
| **Giải thích lý do ORDER BY dùng được Alias** | Lập luận chính xác do ORDER BY chạy ở bước cuối cùng sau khi SELECT hoàn tất. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 11. `DBI202-EXAM-011`: Cơ chế hoạt động của Cây B-Tree trong Clustered Index và Tìm kiếm Bản ghi

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-011` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 4 (ACID, Mức cô lập & Indexing)** |
| **Ánh xạ CLO FLM** | **CLO7** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO7** (`00000000-0000-0000-0000-000000000667`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 41 (Slot 21) — Practical Issues (B-Tree Indexing) |
| **Mục tiêu đánh giá** | Đánh giá mức độ thấu hiểu cấu trúc vật lý cây B+Tree cân bằng và độ phức tạp tìm kiếm O(log N). |

**Nội dung câu hỏi phát vấn:**
> Hãy giải thích cấu trúc phân tầng của cây B-Tree (gốc Root, nhánh Intermediate, lá Leaf Level) trong Clustered Index của MS SQL Server. Quá trình tìm kiếm một bản ghi theo khóa chính diễn ra như thế nào và tại sao nó lại có độ phức tạp thời gian là O(log N)?

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Tại sao việc chọn một cột kiểu dữ liệu GUID ngẫu nhiên (NEWID()) làm Clustered Index Key lại gây ra hiện tượng phân mảnh trang dữ liệu nghiêm trọng (Page Split) và suy giảm hiệu năng ghi dữ liệu, trong khi kiểu tuần tự (BIGINT IDENTITY hoặc NEWSEQUENTIALID()) lại khắc phục được?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Cấu trúc B-Tree trong Clustered Index thực chất là biến thể B+Tree cân bằng hoàn hảo, gồm 3 tầng trang dữ liệu (8KB mỗi trang): (1) Nút gốc (Root Node): Trang đầu vào duy nhất chứa các giá trị khóa phân định và con trỏ trỏ tới các trang mức trung gian. (2) Các nút trung gian (Intermediate Nodes): Các trang chỉ mục điều hướng tiếp tục chia nhỏ dải giá trị khóa thành các khoảng hẹp hơn. (3) Các nút lá (Leaf Level): Đối với Clustered Index, tầng lá CHÍNH LÀ CÁC TRANG DỮ LIỆU THỰC TẾ (Data Pages) của bảng, được tổ chức thành một danh sách liên kết kép (Doubly-linked list) và sắp xếp vật lý tăng dần theo khóa chỉ mục. Quá trình tìm kiếm (Index Seek): Bắt đầu từ Root, bộ máy đối chiếu giá trị khóa cần tìm với các mốc phân vùng để chọn nhánh con phù hợp; tiếp tục duyệt qua tầng Intermediate cho đến khi chạm tới đúng Trang dữ liệu ở mức Lá; sau đó quét nhị phân trong trang để lấy ra dòng dữ liệu hoàn chỉnh. Độ phức tạp là O(log N) vì cây B-Tree có bậc (Fanout) rất lớn (hàng trăm nhánh trên 1 trang 8KB); với bảng 10 triệu bản ghi, chiều cao của cây thường chỉ từ 3 đến 4 tầng; do đó hệ thống chỉ tốn 3-4 thao tác I/O đọc trang để định vị bản ghi chính xác.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Cấu trúc 3 tầng phân cấp của cây B-Tree** | Mô tả chính xác Root, Intermediate và Leaf level là data pages trong Clustered Index. | Understand | **3.5đ** |
| **Quy trình điều hướng Index Seek từ gốc tới lá** | Giải thích rõ việc so sánh mốc phân vùng qua các trang chỉ mục để tới trang đích. | Understand | **3.5đ** |
| **Giải thích độ phức tạp O(log N) và Fanout** | Chứng minh chiều cao cây thấp (3-4 cấp) nhờ Fanout lớn giúp giảm thiểu chi phí I/O. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 12. `DBI202-EXAM-012`: Bảo đảm Tính Nguyên tố (Atomicity) và Bền vững (Durability) qua Write-Ahead Logging (WAL)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-012` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 4 (ACID, Mức cô lập & Indexing)** |
| **Ánh xạ CLO FLM** | **CLO7** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO7** (`00000000-0000-0000-0000-000000000667`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 43 (Slot 22) — Transactions (WAL & Crash Recovery) |
| **Mục tiêu đánh giá** | Kiểm tra mức độ hiểu biết về cơ chế Write-Ahead Logging và quy trình phục hồi dữ liệu sau sự cố sập nguồn. |

**Nội dung câu hỏi phát vấn:**
> Hệ quản trị cơ sở dữ liệu làm thế nào để đảm bảo được hai thuộc tính Atomicity và Durability khi có sự cố sập nguồn (Crash)? Giải thích nguyên lý cốt lõi của kỹ thuật Write-Ahead Logging (WAL) và vai trò của hai tiến trình Undo và Redo.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Trong tiến trình khôi phục sự cố (Crash Recovery) dựa trên thuật toán ARIES, tại sao DBMS luôn phải thực hiện pha Redo trước (Repeat History) rồi mới thực hiện pha Undo cho các giao dịch chưa commit (Active Transactions) tại thời điểm sập nguồn?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> DBMS đảm bảo Atomicity và Durability thông qua Nhật ký giao dịch (Transaction Log file) kết hợp kỹ thuật Ghi nhật ký trước khi ghi dữ liệu (Write-Ahead Logging - WAL). Nguyên lý bất biến của WAL: Mọi thay đổi dữ liệu trên trang bộ nhớ đệm (Buffer Pool) BẮT BUỘC phải được ghi chi tiết vào Transaction Log trên đĩa cứng TRƯỚC KHI trang dữ liệu vật lý (Data Page) được ghi xuống đĩa, và một giao dịch chỉ được coi là đã COMMIT thành công khi bản ghi log commit đã được ghi an toàn xuống đĩa (Flush to disk). Khi máy chủ bị mất điện đột ngột và khởi động lại, tiểu trình Crash Recovery sẽ kích hoạt cơ chế ARIES gồm 2 giai đoạn: (1) Tiến trình REDO (Roll Forward): Quét nhật ký giao dịch từ điểm kiểm tra (Checkpoint) gần nhất, thực hiện lại toàn bộ các thao tác của tất cả các giao dịch ĐÃ COMMIT nhưng dữ liệu trên trang bộ nhớ chưa kịp ghi xuống đĩa, bảo đảm trọn vẹn thuộc tính Durability. (2) Tiến trình UNDO (Roll Back): Quét ngược lại nhật ký, hoàn tác và khôi phục lại trạng thái cũ của toàn bộ các giao dịch ĐANG CHẠY DỞ DANG (chưa Commit) tại thời điểm mất điện, bảo đảm tính nguyên lý 'Tất cả hoặc không có gì' của thuộc tính Atomicity.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Nguyên lý bất biến của Write-Ahead Logging (WAL)** | Nêu đúng quy tắc ghi log xuống đĩa trước khi ghi data page và flush log khi commit. | Understand | **3.5đ** |
| **Vai trò của tiến trình REDO và tính Bền vững (Durability)** | Giải thích rõ việc làm lại các thay đổi của transaction đã commit để bảo vệ dữ liệu. | Understand | **3.5đ** |
| **Vai trò của tiến trình UNDO và tính Nguyên tố (Atomicity)** | Giải thích rõ việc hoàn tác các transaction chưa commit để chống trạng thái dở dang. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 13. `DBI202-EXAM-013`: Điều kiện Khung nhìn Có thể Cập nhật (Updatable Views), WITH CHECK OPTION và WITH SCHEMABINDING (CQ7.3)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-013` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 5 (Lập trình T-SQL & Toàn vẹn nghiệp vụ)** |
| **Ánh xạ CLO FLM** | **CLO6** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO6** (`00000000-0000-0000-0000-000000000666`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 45 & 51 (Slot 23 & 26) — 7.3 Views & Indexed Views (CQ7.3) |
| **Mục tiêu đánh giá** | Đánh giá mức độ hiểu sâu về các điều kiện để một View cho phép thực hiện INSERT/UPDATE/DELETE trực tiếp và vai trò của WITH CHECK OPTION, WITH SCHEMABINDING. |

**Nội dung câu hỏi phát vấn:**
> Trong MS SQL Server (Constructive Question `CQ7.3`), một Khung nhìn (`View`) cần thỏa mãn những điều kiện nào để trở thành **Khung nhìn có thể cập nhật (`Updatable View`)** thông qua các lệnh `INSERT`, `UPDATE`, `DELETE`? Hãy giải thích vai trò của hai tùy chọn nâng cao khi định nghĩa View: (1) `WITH CHECK OPTION` (đặt ở cuối View) và (2) `WITH SCHEMABINDING` (đặt ở đầu View).

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu một View kết nối hai bảng `Orders` và `Customers` bằng `INNER JOIN`, điều gì xảy ra nếu một câu lệnh `UPDATE` trên View đó cố tình sửa đồng thời một cột của bảng `Orders` và một cột của bảng `Customers` trong cùng một lệnh?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> 1. Các điều kiện bắt buộc để một View là `Updatable View` trong MS SQL Server (`CQ7.3`):
- Mọi thao tác sửa đổi (`INSERT`, `UPDATE`, `DELETE`) trên View thực chất được dịch chuyển xuống bảng vật lý cơ sở (`Base Table`). Một View chỉ cho phép cập nhật trực tiếp (không cần `INSTEAD OF Trigger`) khi thỏa mãn đồng thời:
  1. Mỗi câu lệnh sửa đổi chỉ được phép tác động lên các cột thuộc về **duy nhất một bảng cơ sở (Single Base Table)** tại một thời điểm (ngay cả khi View có `JOIN` nhiều bảng).
  2. Các cột được cập nhật phải tham chiếu trực tiếp tới cột vật lý của bảng gốc, **không phải** là cột tính toán từ biểu thức số học, hằng số, hay hàm tổng hợp (`SUM`, `COUNT`, `AVG`, `MAX`, `MIN`).
  3. Câu truy vấn định nghĩa View **không được chứa** các mệnh đề làm mất ánh xạ 1-1 tới dòng gốc như: `GROUP BY`, `HAVING`, `DISTINCT`, `TOP` (không kèm `OFFSET` hợp lệ), hoặc các phép toán tập hợp `UNION`, `UNION ALL`, `INTERSECT`, `EXCEPT`.

2. Vai trò của tùy chọn `WITH CHECK OPTION`:
- Ngăn chặn hiện tượng **"Biến mất dòng qua View" (Row Migration out of View Scope)**: Khi có `WITH CHECK OPTION`, mọi câu lệnh `INSERT` hoặc `UPDATE` thực hiện thông qua View bắt buộc phải tạo ra bản ghi **vẫn thỏa mãn điều kiện lọc `WHERE` của chính View đó**.
- Ví dụ: Nếu `vw_ActiveEmployees` có điều kiện `WHERE IsActive = 1 WITH CHECK OPTION`, câu lệnh `UPDATE vw_ActiveEmployees SET IsActive = 0 WHERE EmpID = 5` sẽ bị SQL Server từ chối báo lỗi vì sau khi sửa, dòng đó không còn nhìn thấy được qua View nữa.

3. Vai trò của tùy chọn `WITH SCHEMABINDING`:
- Khóa chặt cấu trúc của các bảng cơ sở được tham chiếu bởi View: Không ai được phép `DROP TABLE` hoặc `ALTER TABLE` (xóa cột, đổi kiểu dữ liệu cột đang dùng trong View) nếu không xóa hoặc gỡ `SCHEMABINDING` của View trước.
- Bắt buộc phải viết tên bảng theo định dạng 2 phần (`dbo.TableName`) và liệt kê tường minh tên cột (cấm dùng `SELECT *`). Đây cũng là điều kiện tiên quyết bắt buộc để tạo **Indexed View (Materialized View)** trong SQL Server.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Nêu đúng các điều kiện của Updatable View (1 bảng đích, không GROUP BY/DISTINCT/Aggregate)** | Trình bày đầy đủ điều kiện ánh xạ trực tiếp tới 1 base table và các từ khóa làm View trở thành Read-only. | Understand | **4.0đ** |
| **Giải thích chính xác cơ chế kiểm soát của WITH CHECK OPTION** | Giải thích rõ WITH CHECK OPTION ép mọi dòng INSERT/UPDATE qua View phải thỏa mãn mệnh đề WHERE của View. | Understand | **3.0đ** |
| **Giải thích vai trò khóa cấu trúc và điều kiện tạo Indexed View của WITH SCHEMABINDING** | Nêu rõ tác dụng chống ALTER/DROP cột bảng gốc, quy tắc viết dbo.Table và điều kiện tạo Unique Clustered Index trên View. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 14. `DBI202-EXAM-014`: Cơ chế Tái sử dụng Kế hoạch Thực thi (Execution Plan Cache) của Stored Procedure

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-014` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 5 (Lập trình T-SQL & Toàn vẹn nghiệp vụ)** |
| **Ánh xạ CLO FLM** | **CLO6** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO6** (`00000000-0000-0000-0000-000000000666`) |
| **Mức nhận thức Bloom** | **Understand** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 54 (Slot 27) — T-SQL Programming (Stored Procedures) |
| **Mục tiêu đánh giá** | Kiểm tra mức độ am hiểu về cơ chế tối ưu hóa biên dịch và bộ nhớ đệm Plan Cache trong MS SQL Server. |

**Nội dung câu hỏi phát vấn:**
> Tại sao việc thực thi một Stored Procedure thường nhanh hơn đáng kể so với việc gửi trực tiếp các câu lệnh SQL Ad-hoc thô từ ứng dụng? Hãy giải thích cơ chế phân tích cú pháp, tối ưu hóa và lưu bộ nhớ đệm Kế hoạch thực thi (Execution Plan Cache) của Stored Procedure trong SQL Server.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Hiện tượng Parameter Sniffing trong Stored Procedure của SQL Server là gì? Trong trường hợp nào việc tái sử dụng Execution Plan biên dịch lần đầu lại biến thành thảm họa hiệu năng, và DBA xử lý tình huống này bằng gợi ý truy vấn (Query Hint) nào?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Khi một câu lệnh SQL Ad-hoc thô được gửi lên máy chủ, SQL Server bắt buộc phải trải qua 4 công đoạn tuần tự: (1) Parsing: Phân tích cú pháp; (2) Binding/Resolving: Kiểm tra tính hợp lệ của các đối tượng bảng/cột và phân quyền; (3) Optimization: Bộ tối ưu hóa chi phí (Cost-Based Optimizer) đánh giá các giải thuật Join, Scan, Seek để tạo ra Kế hoạch thực thi (Execution Plan) tối ưu; (4) Execution: Thực thi kế hoạch. Công đoạn Optimization tiêu tốn rất nhiều tài nguyên CPU và thời gian. Ngược lại, Stored Procedure mang lại hiệu năng vượt trội nhờ cơ chế Tái sử dụng Kế hoạch thực thi (Execution Plan Cache): Trong lần gọi đầu tiên, SQL Server biên dịch, tối ưu và lưu Kế hoạch thực thi vào vùng nhớ RAM Plan Cache. Trong các lần gọi tiếp theo, khi nhận lệnh EXEC với cùng mã SP, hệ thống lập tức lấy ngay Kế hoạch thực thi đã có sẵn trong bộ nhớ đệm để chạy ngay mà không cần tốn chi phí phân tích và tối ưu hóa lại. Ngoài ra, SP còn giảm thiểu tối đa băng thông đường truyền mạng (chỉ truyền tên SP và tham số thay vì cả đoạn văn bản SQL dài) và loại bỏ hoàn toàn nguy cơ tấn công SQL Injection nhờ cơ chế tham số hóa tự động.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **4 giai đoạn xử lý truy vấn SQL thô (Ad-hoc)** | Liệt kê đúng Parsing, Binding, Optimization và Execution cùng chi phí CPU. | Understand | **3.0đ** |
| **Cơ chế lưu và tái sử dụng Plan Cache của SP** | Giải thích rõ việc biên dịch lần đầu và tái sử dụng trực tiếp kế hoạch trong RAM các lần sau. | Understand | **4.0đ** |
| **Lợi ích bổ trợ về Network Bandwidth và SQL Injection** | Chỉ rõ lợi thế giảm lưu lượng mạng và an ninh tham số hóa chống SQL Injection. | Understand | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 15. `DBI202-EXAM-015`: Ánh xạ Thực thể Kết hợp (Associative Entity), Mối kết hợp Bậc 3 (Ternary) và Thuộc tính Đa trị từ ERD sang Lược đồ Quan hệ

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-015` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 1 (Kiến trúc CSDL & Mô hình quan hệ)** |
| **Ánh xạ CLO FLM** | **CLO4** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO4** (`00000000-0000-0000-0000-000000000664`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 20-22 (Slot 10-11) — 4.5 Mapping ERD to Relational Schema |
| **Mục tiêu đánh giá** | Đánh giá kỹ năng chuyển đổi các cấu trúc ERD nâng cao (mối kết hợp bậc 3, thuộc tính đa trị và thực thể kết hợp) sang lược đồ quan hệ chuẩn 3NF. |

**Nội dung câu hỏi phát vấn:**
> Hãy áp dụng quy tắc ánh xạ từ Sơ đồ Thực thể Kết hợp (ERD) sang Lược đồ Cơ sở dữ liệu Quan hệ chuẩn 3NF cho bài toán Quản lý Khám chữa bệnh tại Bệnh viện gồm: (1) Thực thể `Doctor(DoctorID, FullName)` có thuộc tính đa trị `Specialties` (một bác sĩ có thể có nhiều chuyên khoa); (2) Thực thể `Patient(PatientID, FullName, BirthDate)`; (3) Thực thể `Medicine(MedID, MedName, UnitPrice)`; và (4) Mối kết hợp bậc 3 (Ternary Relationship) `Prescribes` giữa `Doctor`, `Patient` và `Medicine` có thuộc tính riêng là `PrescriptionDate` và `Dosage` (một bác sĩ có thể kê nhiều thuốc cho một bệnh nhân vào nhiều ngày khác nhau). Hãy xác định đầy đủ tên các quan hệ, Khóa chính (PK) và Khóa ngoại (FK).

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu nghiệp vụ bệnh viện thay đổi thành: Mỗi lần khám của Bác sĩ cho Bệnh nhân tạo ra một Đơn thuốc (`Prescription` có mã định danh độc lập `PrescriptionID`), và trong đơn thuốc đó mới liệt kê nhiều loại thuốc `Medicine`, thì mô hình lúc này là một mối kết hợp bậc 3 hay hai mối kết hợp hai ngôi (`1:N` và `M:N`)? So sánh ưu điểm của cách thiết kế này."*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Áp dụng chuẩn xác quy tắc ánh xạ ERD sang Lược đồ Quan hệ chuẩn 3NF (tổng cộng **5 quan hệ**):

1. **Ánh xạ các Thực thể mạnh (Strong Entities):**
   - `Doctors`(**<u>DoctorID</u>**, FullName)
     + *Khóa chính (PK):* `(DoctorID)`
   - `Patients`(**<u>PatientID</u>**, FullName, BirthDate)
     + *Khóa chính (PK):* `(PatientID)`
   - `Medicines`(**<u>MedID</u>**, MedName, UnitPrice)
     + *Khóa chính (PK):* `(MedID)`

2. **Ánh xạ Thuộc tính Đa trị (`Specialties` của `Doctor`):**
   - Để bảo toàn Dạng chuẩn 1 (1NF — cấm nhồi nhiều chuyên khoa phân cách bằng dấu phẩy vào 1 cột), thuộc tính đa trị bắt buộc phải được tách thành một **bảng riêng biệt**, nhận khóa chính của thực thể chủ làm khóa ngoại, và khóa chính của bảng mới là **khóa phức hợp gồm cả hai cột**:
   - `DoctorSpecialties`(**<u>DoctorID, SpecialtyName</u>**)
     + *Khóa chính (PK):* `(DoctorID, SpecialtyName)`
     + *Khóa ngoại (FK):* `DoctorID` tham chiếu tới `Doctors(DoctorID)` (`ON DELETE CASCADE`).

3. **Ánh xạ Mối kết hợp Bậc 3 (`Prescribes` giữa `Doctor` — `Patient` — `Medicine`):**
   - Tạo một quan hệ trung gian mới gom khóa chính của cả 3 thực thể tham gia kèm theo các thuộc tính mô tả của mối kết hợp. Vì một bác sĩ có thể kê cùng một loại thuốc cho cùng một bệnh nhân vào các ngày khác nhau, thuộc tính phân biệt thời gian `PrescriptionDate` tham gia vào khóa chính phức hợp:
   - `Prescriptions`(**<u>DoctorID, PatientID, MedID, PrescriptionDate</u>**, Dosage)
     + *Khóa chính (PK):* `(DoctorID, PatientID, MedID, PrescriptionDate)`
     + *Khóa ngoại (FK):*
       * `DoctorID` tham chiếu tới `Doctors(DoctorID)`
       * `PatientID` tham chiếu tới `Patients(PatientID)`
       * `MedID` tham chiếu tới `Medicines(MedID)`

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Ánh xạ đúng 3 bảng Thực thể mạnh (Doctors, Patients, Medicines)** | Xác định đúng lược đồ và khóa chính đơn cho 3 thực thể mạnh. | Apply | **2.5đ** |
| **Tách bảng riêng cho Thuộc tính đa trị DoctorSpecialties bảo toàn 1NF** | Tạo bảng DoctorSpecialties với khóa chính phức hợp (DoctorID, SpecialtyName) và khóa ngoại DoctorID. | Apply | **3.5đ** |
| **Ánh xạ đúng Mối kết hợp bậc 3 Prescriptions với PK và 3 FK** | Tạo bảng Prescriptions chứa đủ 3 khóa ngoại tham chiếu 3 thực thể, thuộc tính Dosage và khóa chính phức hợp (DoctorID, PatientID, MedID, PrescriptionDate). | Apply | **4.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 16. `DBI202-EXAM-016`: Xác định tất cả các Khóa ứng viên (Candidate Keys) của Quan hệ

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-016` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 3 (Chuẩn hóa lược đồ & Ràng buộc toàn vẹn)** |
| **Ánh xạ CLO FLM** | **CLO3** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO3** (`00000000-0000-0000-0000-000000000663`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 11 (Slot 6) — Design Theory (Candidate Keys) |
| **Mục tiêu đánh giá** | Kiểm tra kỹ năng thuật toán phân tích thuộc tính nguồn/đích và tính bao đóng để tìm toàn bộ khóa ứng viên. |

**Nội dung câu hỏi phát vấn:**
> Cho lược đồ quan hệ R(A, B, C, D, E) với tập phụ thuộc hàm F = { A -> BC, CD -> E, B -> D, E -> A }. Hãy trình bày các bước tính toán chi tiết để tìm TẤT CẢ các Khóa ứng viên (Candidate Keys) của quan hệ R. Xác định các thuộc tính khóa (Prime Attributes) và thuộc tính không khóa.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Trong thuật toán tìm khóa ứng viên tổng quát, làm thế nào để dựa vào vị trí xuất hiện của các thuộc tính ở vế trái (L), vế phải (R) của các phụ thuộc hàm để loại trừ sớm các thuộc tính chắc chắn không thể đứng một mình làm khóa?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Phân tích vị trí thuộc tính trong F: 
- Tập thuộc tính chỉ xuất hiện ở vế trái (Nguồn thuần túy): Không có. 
- Tập thuộc tính chỉ xuất hiện ở vế phải (Đích thuần túy): Không có. 
- Tập thuộc tính không xuất hiện trong F: Không có. 
- Tất cả các thuộc tính {A, B, C, D, E} đều có thể tham gia vào khóa. 
Thử tính bao đóng của các thuộc tính đơn: 
(1) Xét {A}: A+ = {A} -> {A, B, C} (do A -> BC) -> {A, B, C, D} (do B -> D) -> {A, B, C, D, E} (do CD -> E). Vì A+ = R nên {A} là một Khóa ứng viên (CK1). 
(2) Do E -> A và A là khóa ứng viên, xét {E}: E+ = {E} -> {A, E} (do E -> A) = A+ = R. Vì E+ = R nên {E} là Khóa ứng viên thứ hai (CK2). 
(3) Xét các cặp chứa B hoặc CD: Xét {C, D}: (CD)+ = {C, D, E} (do CD -> E) -> {A, C, D, E} (do E -> A) = R. Vì (CD)+ = R và không có tập con thực sự nào sinh ra R nên {C, D} là Khóa ứng viên thứ ba (CK3). 
(4) Do B -> D, xét {B, C}: (BC)+ = {B, C, D} (do B -> D) = (CD)+ = R. Do đó {B, C} là Khóa ứng viên thứ tư (CK4). 
Kết luận: Quan hệ R có đúng 4 Khóa ứng viên là: {A}, {E}, {C, D}, {B, C}. Toàn bộ thuộc tính {A, B, C, D, E} đều là Thuộc tính khóa (Prime Attributes); quan hệ không có thuộc tính không khóa nào.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Phân loại vị trí thuộc tính và chiến lược tìm khóa** | Nhận diện đúng không có thuộc tính nguồn/đích thuần túy và định hướng thử tập nhỏ. | Understand | **2.0đ** |
| **Các bước tính bao đóng tìm đúng các khóa ứng viên** | Tính toán chi tiết A+ = R, E+ = R, (CD)+ = R, (BC)+ = R đầy đủ từng bước. | Apply | **5.0đ** |
| **Xác định chính xác Thuộc tính khóa (Prime Attributes)** | Kết luận đúng 4 khóa và chỉ ra toàn bộ 5 thuộc tính đều là Prime Attributes. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 17. `DBI202-EXAM-017`: Phân rã Chuẩn hóa Lược đồ đạt Dạng chuẩn 3 (3NF) Bảo toàn Phụ thuộc

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-017` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 3 (Chuẩn hóa lược đồ & Ràng buộc toàn vẹn)** |
| **Ánh xạ CLO FLM** | **CLO3** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO3** (`00000000-0000-0000-0000-000000000663`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 13-14 (Slot 7) — 3.5 Normal Forms (2NF & 3NF Synthesis Algorithm) |
| **Mục tiêu đánh giá** | Đánh giá kỹ năng áp dụng thuật toán tổng hợp Bernsteins (3NF Synthesis Algorithm) bảo toàn thông tin và phụ thuộc hàm. |

**Nội dung câu hỏi phát vấn:**
> Cho lược đồ quan hệ R(A, B, C, D, E) với tập phụ thuộc hàm F = { AB -> C, C -> D, D -> B, D -> E }. Hãy cho biết R hiện tại đạt dạng chuẩn mấy và vi phạm dạng chuẩn nào? Áp dụng thuật toán phân rã chuẩn hóa để đưa R về tập các quan hệ đạt Dạng chuẩn 3 (3NF) bảo toàn phụ thuộc hàm và kết nối không mất thông tin.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Sau khi phân rã theo thuật toán tổng hợp 3NF (Bernstein's Synthesis), nếu không có quan hệ con nào chứa một Khóa ứng viên của lược đồ ban đầu R, chúng ta bắt buộc phải bổ sung thêm quan hệ nào để bảo đảm tính chất Kết nối không mất thông tin (Lossless Join)?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> 1. Tìm tất cả các Khóa ứng viên (Candidate Keys) của $R(A, B, C, D, E)$ với $F = \{AB \to C, C \to D, D \to B, D \to E\}$:
- Nhận xét: Thuộc tính $A$ chỉ xuất hiện ở vế trái và không bao giờ xuất hiện ở vế phải của bất kỳ phụ thuộc hàm nào trong $F$, do đó $A$ bắt buộc phải có mặt trong mọi khóa ứng viên của $R$. Bao đóng $A^+ = \{A\} \neq R$.
- Xét $(AB)^+ = \{A, B\} \xrightarrow{AB \to C} \{A, B, C\} \xrightarrow{C \to D} \{A, B, C, D\} \xrightarrow{D \to E} \{A, B, C, D, E\} = R \Rightarrow \{AB\}$ là khóa ứng viên.
- Do có $D \to B$ và $C \to D$, ta kiểm tra $(AC)^+$ và $(AD)^+$:
  + $(AD)^+ = \{A, D\} \xrightarrow{D \to B, D \to E} \{A, B, D, E\} \xrightarrow{AB \to C} \{A, B, C, D, E\} = R \Rightarrow \{AD\}$ là khóa ứng viên.
  + $(AC)^+ = \{A, C\} \xrightarrow{C \to D} \{A, C, D\} = (AD)^+ = R \Rightarrow \{AC\}$ là khóa ứng viên.
- Kết luận tập khóa: $R$ có **3 khóa ứng viên** là $\{AB\}, \{AC\}, \{AD\}$. Tập thuộc tính khóa (Prime attributes): $\{A, B, C, D\}$. Tập thuộc tính không khóa (Non-prime attributes): $\{E\}$.

2. Đánh giá dạng chuẩn hiện tại của $R$ (Phát hiện vi phạm ngay từ 2NF):
- Xét phụ thuộc hàm $D \to E$: Vế phải $E$ là **thuộc tính không khóa (non-prime attribute)**, còn vế trái $D$ là một **tập con thực sự** của khóa ứng viên $\{AD\}$ ($D \subsetneq \{AD\}$).
- Theo định nghĩa chuẩn hóa: Một thuộc tính không khóa ($E$) phụ thuộc hàm vào một phần của khóa ứng viên ($\{AD\}$) là **Phụ thuộc hàm bộ phận (Partial Dependency)**.
- Do đó, **$R$ vi phạm ngay từ Dạng chuẩn 2 (2NF)** (và tất yếu cũng vi phạm 3NF và BCNF). Vậy lược đồ **$R$ hiện tại chỉ đạt Dạng chuẩn 1 (1NF)**!
*(Ngoài ra, các phụ thuộc $C \to D$ và $D \to B$ có vế phải $D, B$ là thuộc tính khóa nên không vi phạm 2NF/3NF nhưng vi phạm BCNF vì $C$ và $D$ không phải là siêu khóa).*

3. Phân rã $R$ về Dạng chuẩn 3 (3NF) bảo toàn phụ thuộc hàm và kết nối không mất thông tin (Thuật toán tổng hợp Bernstein):
* **Bước 1:** Tìm phủ tối tiểu $F_{\min} = \{AB \to C, C \to D, D \to B, D \to E\}$ (vế phải đã đơn thuộc tính, không có phụ thuộc dư thừa, thuộc tính $A$ và $B$ trong $AB \to C$ không dư thừa vì $A^+ = \{A\}$ và $B^+ = \{B\}$).
* **Bước 2:** Gom các phụ thuộc hàm có cùng vế trái để tạo các lược đồ con:
  + Từ $AB \to C \Rightarrow R_1(\underline{A, B}, C)$ với khóa chính $\{AB\}$.
  + Từ $C \to D \Rightarrow R_2(\underline{C}, D)$ với khóa chính $\{C\}$.
  + Từ $D \to B$ và $D \to E \Rightarrow R_3(\underline{D}, B, E)$ với khóa chính $\{D\}$.
* **Bước 3:** Kiểm tra điều kiện Kết nối không mất thông tin (Lossless Join): Vì $R_1(A, B, C)$ đã chứa khóa ứng viên $\{AB\}$ của lược đồ gốc $R$, ta không cần thêm quan hệ khóa nào nữa.
* **Kết quả:** Phép phân rã $\rho = \{R_1(\underline{A, B}, C), R_2(\underline{C}, D), R_3(\underline{D}, B, E)\}$ đạt **3NF**, bảo toàn 100% phụ thuộc hàm và bảo đảm Lossless Join.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Tìm đúng 3 khóa ứng viên {AB}, {AC}, {AD} và thuộc tính không khóa E** | Xác định chuẩn xác 3 khóa ứng viên thông qua bao đóng và chỉ rõ E là thuộc tính không khóa duy nhất. | Apply | **3.0đ** |
| **Chẩn đoán chính xác R vi phạm 2NF (chỉ đạt 1NF) do phụ thuộc bộ phận D -> E** | Giải thích rõ vì D là tập con thực sự của khóa ứng viên {AD} và E là thuộc tính không khóa nên D -> E là phụ thuộc bộ phận, khiến R vi phạm ngay từ 2NF và chỉ đạt 1NF. | Analyze | **3.0đ** |
| **Thực hiện chuẩn xác thuật toán tổng hợp 3NF (Bernstein)** | Tìm phủ tối tiểu, gom nhóm theo vế trái sinh đúng R1(A,B,C), R2(C,D), R3(D,B,E) và kiểm tra R1 chứa khóa ứng viên {AB} bảo đảm Lossless Join. | Apply | **4.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 18. `DBI202-EXAM-018`: Viết câu truy vấn SQL tìm Khách hàng có Tổng giá trị đơn hàng cao nhất trong từng Thành phố

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-018` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 2 (Phân tích kịch bản truy vấn SQL)** |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 36 (Slot 18) — SQL Queries (Advanced Subqueries & CTE) |
| **Mục tiêu đánh giá** | Kiểm tra kỹ năng viết truy vấn phức tạp kết hợp gom nhóm, CTE và kỹ thuật xếp hạng hoặc Correlated Subquery. |

**Nội dung câu hỏi phát vấn:**
> Cho hai bảng dữ liệu: 
Customers(CustomerID INT, CustomerName NVARCHAR(100), City NVARCHAR(50)) 
Orders(OrderID INT, CustomerID INT, OrderDate DATE, TotalAmount DECIMAL(12,2)) 
Hãy viết một câu truy vấn T-SQL chuẩn mực để tìm ra: Ở mỗi Thành phố (City), thông tin của Khách hàng (CustomerID, CustomerName, City) có tổng giá trị đơn hàng (SUM(TotalAmount)) là cao nhất. Nếu một thành phố có nhiều khách hàng cùng đạt mức chi tiêu cao nhất bằng nhau thì hiển thị tất cả.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Tại sao trong bài toán xếp hạng có thể có nhiều khách hàng đồng hạng nhất, chúng ta bắt buộc phải sử dụng hàm xếp hạng DENSE_RANK() thay vì ROW_NUMBER() trong biểu thức CTE?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Giải pháp chuẩn mực sử dụng Biểu thức bảng chung (CTE) kết hợp hàm xếp hạng cửa sổ DENSE_RANK(): 
```sql
WITH CustomerSpending AS (
    SELECT 
        c.CustomerID,
        c.CustomerName,
        c.City,
        SUM(o.TotalAmount) AS TotalSpent
    FROM Customers c
    INNER JOIN Orders o ON c.CustomerID = o.CustomerID
    GROUP BY c.CustomerID, c.CustomerName, c.City
),
RankedSpending AS (
    SELECT 
        CustomerID,
        CustomerName,
        City,
        TotalSpent,
        DENSE_RANK() OVER (
            PARTITION BY City 
            ORDER BY TotalSpent DESC
        ) AS SpendingRank
    FROM CustomerSpending
)
SELECT 
    CustomerID,
    CustomerName,
    City,
    TotalSpent
FROM RankedSpending
WHERE SpendingRank = 1
ORDER BY City ASC, TotalSpent DESC;
``` 
Phân tích giải thuật: (1) CTE thứ nhất `CustomerSpending` thực hiện JOIN giữa Customers và Orders, gom nhóm theo CustomerID và City để tính tổng tiền mua sắm `TotalSpent` của từng khách hàng. (2) CTE thứ hai `RankedSpending` sử dụng hàm cửa sổ `DENSE_RANK()` phân vùng theo `City` (`PARTITION BY City`) và sắp xếp giảm dần theo `TotalSpent`. Hàm `DENSE_RANK()` đảm bảo nếu có nhiều khách hàng hòa điểm chi tiêu cao nhất trong cùng thành phố thì tất cả họ đều được xếp hạng 1. (3) Câu lệnh SELECT ngoài cùng chỉ việc lọc các bản ghi có `SpendingRank = 1`.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Tính toán đúng Tổng chi tiêu của từng khách hàng** | Sử dụng chính xác INNER JOIN kết hợp GROUP BY tính SUM(TotalAmount). | Apply | **3.0đ** |
| **Kỹ thuật xếp hạng theo từng thành phố (Window Function / Subquery)** | Áp dụng đúng PARTITION BY City và hàm xếp hạng DENSE_RANK hoặc Correlated Subquery. | Apply | **4.0đ** |
| **Xử lý triệt để trường hợp đồng hạng cao nhất** | Lọc SpendingRank = 1 đảm bảo không làm mất khách hàng khi có giá trị bằng nhau. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 19. `DBI202-EXAM-019`: Thiết kế câu lệnh DDL với các ràng buộc CHECK và FOREIGN KEY phức tạp

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-019` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 3 (Chuẩn hóa lược đồ & Ràng buộc toàn vẹn)** |
| **Ánh xạ CLO FLM** | **CLO5** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO5** (`00000000-0000-0000-0000-000000000665`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Medium** |
| **Phân bổ Session** | Session 29-30 (Slot 15) — SQL Constraints Implementation |
| **Mục tiêu đánh giá** | Kiểm tra năng lực chuyển đổi các quy tắc nghiệp vụ thực tế thành các ràng buộc kỹ thuật trong câu lệnh DDL. |

**Nội dung câu hỏi phát vấn:**
> Hãy viết mã T-SQL hoàn chỉnh tạo bảng `CourseEnrollments` để quản lý việc đăng ký môn học của sinh viên thỏa mãn toàn bộ các yêu cầu nghiệp vụ sau: 
(1) Khóa chính là sự kết hợp của `StudentID` (INT) và `CourseID` (INT). 
(2) Khóa ngoại `StudentID` tham chiếu bảng `Students(StudentID)` với tùy chọn xóa dây chuyền CASCADE. 
(3) Khóa ngoại `CourseID` tham chiếu bảng `Courses(CourseID)` với tùy chọn chặn xóa NO ACTION. 
(4) Cột `EnrollmentDate` kiểu DATE có giá trị mặc định là ngày hiện tại. 
(5) Cột `Grade` kiểu DECIMAL(4,2) có thể nhận NULL (chưa có điểm) nhưng nếu có điểm thì giá trị bắt buộc phải nằm trong khoảng từ 0.00 đến 10.00. 
(6) Cột `Status` kiểu VARCHAR(20) chỉ chấp nhận một trong 4 giá trị: 'Enrolled', 'Completed', 'Dropped', 'Failed'.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu sinh viên đăng ký môn học nhưng chưa có điểm (cột Grade nhận giá trị NULL), ràng buộc CHECK (Grade >= 0.00 AND Grade <= 10.00) sẽ đánh giá ra kết quả logic gì trong Three-Valued Logic (3VL) của SQL, và bản ghi đó có được phép chèn vào bảng không? Tại sao?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Đoạn mã T-SQL tạo bảng chuẩn mực: 
```sql
CREATE TABLE CourseEnrollments (
    StudentID INT NOT NULL,
    CourseID INT NOT NULL,
    EnrollmentDate DATE NOT NULL CONSTRAINT DF_Enrollments_Date DEFAULT (CAST(GETDATE() AS DATE)),
    Grade DECIMAL(4,2) NULL,
    Status VARCHAR(20) NOT NULL CONSTRAINT DF_Enrollments_Status DEFAULT ('Enrolled'),
    
    -- Ràng buộc Khóa chính phức hợp
    CONSTRAINT PK_CourseEnrollments PRIMARY KEY (StudentID, CourseID),
    
    -- Ràng buộc Khóa ngoại StudentID với ON DELETE CASCADE
    CONSTRAINT FK_Enrollments_Students FOREIGN KEY (StudentID)
        REFERENCES Students(StudentID)
        ON DELETE CASCADE
        ON UPDATE CASCADE,
        
    -- Ràng buộc Khóa ngoại CourseID với ON DELETE NO ACTION
    CONSTRAINT FK_Enrollments_Courses FOREIGN KEY (CourseID)
        REFERENCES Courses(CourseID)
        ON DELETE NO ACTION
        ON UPDATE CASCADE,
        
    -- Ràng buộc CHECK trên điểm số Grade
    CONSTRAINT CK_Enrollments_Grade CHECK (Grade IS NULL OR (Grade >= 0.00 AND Grade <= 10.00)),
    
    -- Ràng buộc CHECK trên trạng thái Status
    CONSTRAINT CK_Enrollments_Status CHECK (Status IN ('Enrolled', 'Completed', 'Dropped', 'Failed'))
);
```

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Khai báo Khóa chính phức hợp & Cột dữ liệu** | Định nghĩa đúng kiểu dữ liệu, NOT NULL và PRIMARY KEY (StudentID, CourseID). | Apply | **2.5đ** |
| **Thiết lập đúng 2 Khóa ngoại với tùy chọn CASCADE / NO ACTION** | Viết chính xác FOREIGN KEY REFERENCES với ON DELETE CASCADE và ON DELETE NO ACTION. | Apply | **3.5đ** |
| **Cài đặt các ràng buộc CHECK và DEFAULT** | Viết đúng CHECK cho Grade (xử lý an toàn với NULL), CHECK cho Status IN và DEFAULT GETDATE(). | Apply | **4.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 20. `DBI202-EXAM-020`: Viết DML AFTER Trigger tự động cập nhật Tồn kho và Hủy đơn khi Thiếu hàng

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-020` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 5 (Lập trình T-SQL & Toàn vẹn nghiệp vụ)** |
| **Ánh xạ CLO FLM** | **CLO6** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO6** (`00000000-0000-0000-0000-000000000666`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 50 (Slot 25) — T-SQL Programming (Inventory Triggers) |
| **Mục tiêu đánh giá** | Đánh giá kỹ năng lập trình Trigger xử lý nghiệp vụ kho bãi, kiểm tra tính khả dụng và tự động đồng bộ tồn kho. |

**Nội dung câu hỏi phát vấn:**
> Cho hai bảng: 
`Products(ProductID INT PK, ProductName NVARCHAR(100), UnitsInStock INT)` 
`OrderDetails(OrderID INT, ProductID INT, Quantity INT, UnitPrice DECIMAL(10,2), PK(OrderID, ProductID))` 
Hãy viết một AFTER INSERT Trigger trên bảng `OrderDetails` thực hiện: 
(1) Kiểm tra xem có sản phẩm nào trong các dòng vừa được chèn có số lượng đặt mua (`Quantity`) lớn hơn số lượng tồn kho (`UnitsInStock`) của sản phẩm đó hay không. Nếu có, lập tức hủy bỏ toàn bộ giao dịch chèn và ném ra thông báo lỗi thích hợp. 
(2) Nếu tất cả sản phẩm đều đủ hàng, tự động trừ số lượng tồn kho `UnitsInStock` trong bảng `Products` tương ứng với số lượng vừa bán.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Tại sao trong trigger của MS SQL Server, chúng ta tuyệt đối không được viết code gán biến vô hướng như 'SELECT @Qty = Quantity, @PID = ProductID FROM inserted' mà bắt buộc phải viết câu lệnh UPDATE kết hợp JOIN trực tiếp với bảng ảo inserted?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Đoạn mã T-SQL viết Trigger chuẩn mực theo tư duy tập hợp: 
```sql
CREATE TRIGGER trg_OrderDetails_AfterInsert
ON OrderDetails
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;
    
    -- Bước 1: Kiểm tra điều kiện thiếu hàng trong kho
    IF EXISTS (
        SELECT 1 
        FROM inserted i
        INNER JOIN Products p ON i.ProductID = p.ProductID
        WHERE i.Quantity > p.UnitsInStock
    )
    BEGIN
        RAISERROR (N'Giao dịch bị hủy: Tồn kho không đủ để đáp ứng số lượng đặt hàng!', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
    
    -- Bước 2: Tự động trừ tồn kho khi đủ hàng (xử lý an toàn cho trường hợp chèn nhiều dòng)
    UPDATE p
    SET p.UnitsInStock = p.UnitsInStock - aggregated_i.TotalOrdered
    FROM Products p
    INNER JOIN (
        SELECT ProductID, SUM(Quantity) AS TotalOrdered
        FROM inserted
        GROUP BY ProductID
    ) aggregated_i ON p.ProductID = aggregated_i.ProductID;
END;
```

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Kiểm tra điều kiện tồn kho qua bảng ảo inserted** | Sử dụng IF EXISTS kết hợp INNER JOIN đối chiếu Quantity > UnitsInStock. | Apply | **3.5đ** |
| **Xử lý hủy giao dịch bằng RAISERROR và ROLLBACK** | Gọi đúng RAISERROR ném lỗi và ROLLBACK TRANSACTION kèm RETURN để ngắt trigger. | Apply | **3.0đ** |
| **Cập nhật trừ tồn kho theo tập hợp chuẩn mực (Gom nhóm inserted)** | Viết câu lệnh UPDATE Products trừ TotalOrdered từ subquery gom nhóm của inserted. | Apply | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 21. `DBI202-EXAM-021`: Xây dựng Stored Procedure Thực hiện Giao dịch Chuyển tiền An toàn

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-021` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 5 (Lập trình T-SQL & Toàn vẹn nghiệp vụ)** |
| **Ánh xạ CLO FLM** | **CLO6** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO6** (`00000000-0000-0000-0000-000000000666`) |
| **Mức nhận thức Bloom** | **Apply** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 54 (Slot 27) — T-SQL Programming (Transactions & Error Handling) |
| **Mục tiêu đánh giá** | Kiểm tra kỹ năng lập trình thủ tục T-SQL tích hợp quản trị giao dịch ACID và bẫy lỗi TRY...CATCH hoàn chỉnh. |

**Nội dung câu hỏi phát vấn:**
> Cho bảng `BankAccounts(AccountID INT PRIMARY KEY, AccountHolder NVARCHAR(100), Balance DECIMAL(15,2), IsActive BIT)`. 
Hãy viết một Stored Procedure có tên `sp_TransferMoney` nhận 3 tham số: `@FromAccountID INT, @ToAccountID INT, @Amount DECIMAL(15,2)`. 
Thủ tục phải quản trị Transaction chặt chẽ: kiểm tra số tiền chuyển hợp lệ (> 0), kiểm tra hai tài khoản có tồn tại và đang hoạt động (`IsActive = 1`), kiểm tra tài khoản nguồn có đủ số dư; thực hiện trừ tiền và cộng tiền trong một Transaction an toàn bằng khối `TRY...CATCH` và ROLLBACK nếu có bất kỳ lỗi nào xảy ra.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Trong kịch bản chuyển tiền đồng thời giữa hai tài khoản (A chuyển cho B cùng lúc B chuyển cho A), cơ chế khóa hàng (Row-level X-Lock) của câu lệnh UPDATE có thể dẫn đến Deadlock như thế nào? Thủ tục lưu trữ cần làm gì để phòng chống triệt để tình huống này?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Đoạn mã T-SQL hoàn chỉnh chuẩn công nghiệp cho Stored Procedure `sp_TransferMoney` (Tích hợp `SET XACT_ABORT ON` và **Khóa dòng theo thứ tự ID tăng dần** để triệt tiêu 100% Cyclic Deadlock):
```sql
CREATE PROCEDURE sp_TransferMoney
    @FromAccountID INT,
    @ToAccountID INT,
    @Amount DECIMAL(15,2)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON; -- Tự động hủy và rollback giao dịch ngay khi phát sinh lỗi runtime nghiêm trọng

    -- 1. Kiểm tra tính hợp lệ của tham số đầu vào trước khi mở Transaction
    IF (@Amount IS NULL OR @Amount <= 0)
    BEGIN
        RAISERROR(N'Số tiền chuyển khoản phải lớn hơn 0!', 16, 1);
        RETURN -1;
    END

    IF (@FromAccountID IS NULL OR @ToAccountID IS NULL OR @FromAccountID = @ToAccountID)
    BEGIN
        RAISERROR(N'Tài khoản nguồn và tài khoản đích không hợp lệ hoặc trùng nhau!', 16, 1);
        RETURN -2;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        DECLARE @SourceBalance DECIMAL(15,2), @SourceActive BIT;
        DECLARE @DestBalance DECIMAL(15,2), @DestActive BIT;

        -- 2. Khóa UPDLOCK, ROWLOCK theo đúng THỨ TỰ AccountID TĂNG DẦN để chống Cyclic Deadlock
        IF (@FromAccountID < @ToAccountID)
        BEGIN
            SELECT @SourceBalance = Balance, @SourceActive = IsActive
            FROM BankAccounts WITH (UPDLOCK, ROWLOCK)
            WHERE AccountID = @FromAccountID;

            SELECT @DestBalance = Balance, @DestActive = IsActive
            FROM BankAccounts WITH (UPDLOCK, ROWLOCK)
            WHERE AccountID = @ToAccountID;
        END
        ELSE
        BEGIN
            SELECT @DestBalance = Balance, @DestActive = IsActive
            FROM BankAccounts WITH (UPDLOCK, ROWLOCK)
            WHERE AccountID = @ToAccountID;

            SELECT @SourceBalance = Balance, @SourceActive = IsActive
            FROM BankAccounts WITH (UPDLOCK, ROWLOCK)
            WHERE AccountID = @FromAccountID;
        END

        -- 3. Kiểm tra trạng thái hoạt động và số dư khả dụng
        IF (@SourceBalance IS NULL OR @SourceActive = 0)
        BEGIN
            RAISERROR(N'Tài khoản nguồn không tồn tại hoặc đã bị khóa!', 16, 1);
        END

        IF (@DestBalance IS NULL OR @DestActive = 0)
        BEGIN
            RAISERROR(N'Tài khoản đích không tồn tại hoặc không hoạt động!', 16, 1);
        END

        IF (@SourceBalance < @Amount)
        BEGIN
            RAISERROR(N'Số dư tài khoản nguồn không đủ để thực hiện giao dịch!', 16, 1);
        END

        -- 4. Cập nhật trừ tiền tài khoản nguồn và cộng tiền tài khoản đích
        UPDATE BankAccounts
        SET Balance = Balance - @Amount
        WHERE AccountID = @FromAccountID;

        UPDATE BankAccounts
        SET Balance = Balance + @Amount
        WHERE AccountID = @ToAccountID;

        COMMIT TRANSACTION;
        RETURN 0;
    END TRY
    BEGIN CATCH
        IF (@@TRANCOUNT > 0)
        BEGIN
            ROLLBACK TRANSACTION;
        END

        DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
        DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
        DECLARE @ErrorState INT = ERROR_STATE();
        RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
        RETURN -99;
    END CATCH
END;
```

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Kiểm tra tham số đầu vào và thiết lập SET XACT_ABORT ON** | Kiểm tra @Amount > 0, @FromAccountID <> @ToAccountID và bật SET NOCOUNT ON, SET XACT_ABORT ON trước khi mở Transaction. | Apply | **2.5đ** |
| **Khóa dòng UPDLOCK, ROWLOCK theo thứ tự ID tăng dần chống Deadlock** | Sử dụng IF (@FromAccountID < @ToAccountID) để luôn lấy khóa tài khoản có ID nhỏ trước, ID lớn sau, kết hợp kiểm tra tồn tại và số dư. | Analyze | **4.0đ** |
| **Cập nhật số dư nguyên tố và xử lý ROLLBACK an toàn trong TRY...CATCH** | Thực hiện UPDATE trừ/cộng chính xác, COMMIT khi thành công và kiểm tra IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION trong khối CATCH. | Apply | **3.5đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 22. `DBI202-EXAM-022`: Phân tích và Chứng minh Phép phân rã Kết nối Không Mất Thông tin (Lossless Join)

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-022` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 3 (Chuẩn hóa lược đồ & Ràng buộc toàn vẹn)** |
| **Ánh xạ CLO FLM** | **CLO3** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO3** (`00000000-0000-0000-0000-000000000663`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 11-12 (Slot 6) — 3.3 Design of Relational Database Schema (Lossless Join Proof) |
| **Mục tiêu đánh giá** | Đánh giá năng lực tư duy toán học hình thức trong việc chứng minh tính chất Lossless Join bằng phương pháp bảng kiểm tra Chase Test. |

**Nội dung câu hỏi phát vấn:**
> Cho lược đồ quan hệ R(A, B, C, D, E) với tập phụ thuộc hàm F = { A -> D, B -> E, DE -> C }. 
Xét phép phân rã R thành 3 quan hệ con: R1(A, D), R2(B, E) và R3(C, D, E). 
Hãy sử dụng Thuật toán Bảng kiểm tra (Chase Test / Tableau Method) hoặc định lý đại số để chứng minh chặt chẽ xem phép phân rã này có phải là Phép phân rã kết nối không mất thông tin (Lossless Join Decomposition) hay không. Giải thích ý nghĩa của dòng chứa toàn bộ ký hiệu không chỉ số (a1, a2, ..., an).

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu một phép phân rã bị mất tính chất Kết nối không mất thông tin (Lossy Join Decomposition) thì khi chúng ta thực hiện phép kết nối tự nhiên (NATURAL JOIN) các bảng con lại với nhau, kết quả thu được sẽ bị thiếu bản ghi hay phát sinh thêm các bản ghi giả mạo (Spurious Tuples)?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Ta có thể chứng minh chặt chẽ phép phân rã $\rho = \{R_1(A, D), R_2(B, E), R_3(C, D, E)\}$ trên $R(A, B, C, D, E)$ với $F = \{A \to D, B \to E, DE \to C\}$ theo **cả 2 phương pháp chuẩn mực** (sinh viên trình bày đúng 1 trong 2 hoặc cả 2 đều đạt điểm tối đa):

### Phương pháp 1: Thuật toán Bảng kiểm tra (Chase Test / Tableau Method)
1. **Khởi tạo bảng ma trận ($3 \text{ dòng} \times 5 \text{ cột}$):**
   Ký hiệu $a_j$ nếu thuộc tính thứ $j$ thuộc lược đồ con $R_i$, và $b_{ij}$ nếu thuộc tính vắng mặt:
   - Dòng 1 ($R_1(A, D)$): $[a_1, b_{12}, b_{13}, a_4, b_{15}]$
   - Dòng 2 ($R_2(B, E)$): $[b_{21}, a_2, b_{23}, b_{24}, a_5]$
   - Dòng 3 ($R_3(C, D, E)$): $[b_{31}, b_{32}, a_3, a_4, a_5]$
2. **Áp dụng lặp các phụ thuộc hàm trong $F$:**
   - Xét $A \to D$: Cột $A$ có 3 giá trị đôi một khác nhau ($a_1, b_{21}, b_{31}$) $\to$ không đồng nhất được dòng nào.
   - Xét $B \to E$: Cột $B$ có 3 giá trị khác nhau ($b_{12}, a_2, b_{32}$) $\to$ không thay đổi bảng.
   - Xét $DE \to C$: Chỉ có Dòng 3 mang bộ $(a_4, a_5)$ trên hai cột $(D, E)$ (Dòng 1 là $(a_4, b_{15})$, Dòng 2 là $(b_{24}, a_5)$) $\to$ không có hai dòng nào trùng nhau đồng thời trên cả $D$ và $E$.
3. **Kết luận theo Chase Test:** Bảng dừng lại mà **không có bất kỳ dòng nào chứa toàn bộ ký hiệu $a_1, a_2, a_3, a_4, a_5$**.

### Phương pháp 2: Định lý Bao đóng Phép giao Nhị phân (Binary Intersection Closure Test)
Một phép phân rã thành 3 lược đồ là Lossless Join khi và chỉ khi ta có thể ghép nối không mất thông tin từng cặp liên tiếp (trong đó bao đóng của phần giao $(S_1 \cap S_2)^+$ phải chứa toàn bộ $S_1$ hoặc toàn bộ $S_2$):
1. **Thử ghép cặp 1:**
   - Cặp $(R_1, R_2)$: $R_1 \cap R_2 = \{A, D\} \cap \{B, E\} = \emptyset \to$ Không thể kết nối không mất thông tin.
   - Cặp $(R_1, R_3)$: $R_1 \cap R_3 = \{A, D\} \cap \{C, D, E\} = \{D\}$. Tính bao đóng: $D^+ = \{D\}$. Vì $R_1 \not\subseteq D^+$ (thiếu $A$) và $R_3 \not\subseteq D^+$ (thiếu $C, E$), nên $\{D\}$ không phải là siêu khóa của $R_1$ cũng không phải siêu khóa của $R_3$.
   - Cặp $(R_2, R_3)$: $R_2 \cap R_3 = \{B, E\} \cap \{C, D, E\} = \{E\}$. Tính bao đóng: $E^+ = \{E\}$. Tương tự, $\{E\}$ không phải siêu khóa của $R_2$ (thiếu $B$) cũng không phải siêu khóa của $R_3$ (thiếu $C, D$).
2. **Thử ghép bước 2 (giả sử ghép $(R_1 \cup R_3)$ với $R_2$):**
   - $(R_1 \cup R_3) \cap R_2 = \{A, C, D, E\} \cap \{B, E\} = \{E\}$, có $E^+ = \{E\}$ vẫn không phải siêu khóa của bất kỳ bên nào.

### Kết luận chung:
Phép phân rã $\rho = \{R_1, R_2, R_3\}$ là **Phép phân rã MẤT THÔNG TIN (Lossy Join Decomposition)**. Khi thực hiện phép kết nối tự nhiên $R_1 \bowtie R_2 \bowtie R_3$, kết quả sẽ phát sinh các **bản ghi giả mạo (Spurious Tuples)**. Một dòng toàn ký hiệu $(a_1, \dots, a_n)$ trong bảng Chase (hoặc phần giao là siêu khóa) là điều kiện cần và đủ bảo đảm phép kết nối tự nhiên tái tạo đúng 100% quan hệ gốc.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Thiết lập đúng Bảng Chase 3x5 hoặc Phát biểu đúng Định lý Bao đóng Phép giao** | Khởi tạo đúng ma trận Chase 3 dòng x 5 cột với ký hiệu a_j, b_ij HOẶC nêu đúng điều kiện (S1 ∩ S2)+ phải là siêu khóa của S1 hoặc S2. | Apply | **3.5đ** |
| **Chứng minh từng bước bằng Chase Test hoặc Tính bao đóng D+ = {D}, E+ = {E}** | Chứng minh A->D, B->E, DE->C không làm xuất hiện dòng toàn a_j HOẶC chỉ rõ R1∩R3={D} (D+={D}) và R2∩R3={E} (E+={E}) đều không phải siêu khóa. | Analyze | **3.5đ** |
| **Kết luận chính xác Lossy Join và giải thích hiện tượng Spurious Tuples** | Khẳng định phép phân rã bị mất thông tin (Lossy Join), sinh ra các bộ giả mạo (Spurious Tuples) khi thực hiện Natural Join. | Analyze | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 23. `DBI202-EXAM-023`: Phân tích Tranh chấp Dữ liệu giữa Hai Giao dịch và Đề xuất Mức Cô lập Tối ưu

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-023` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 4 (ACID, Mức cô lập & Indexing)** |
| **Ánh xạ CLO FLM** | **CLO7** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO7** (`00000000-0000-0000-0000-000000000667`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 44 (Slot 22) — Transactions (Concurrency & Isolation Trade-offs) |
| **Mục tiêu đánh giá** | Đánh giá khả năng phân tích biểu đồ thời gian thực thi (Schedule/Timeline) của hai transaction và ra quyết định cân bằng giữa an toàn và hiệu năng. |

**Nội dung câu hỏi phát vấn:**
> Cho kịch bản hệ thống đặt vé máy bay: 
- Giao dịch T1 (Khách hàng): Đọc số ghế trống chuyến bay VN123 (thấy còn 1 ghế); sau đó hệ thống chờ khách xác nhận thanh toán thẻ (khoảng 30 giây); rồi thực hiện UPDATE giảm số ghế trống về 0 và COMMIT. 
- Giao dịch T2 (Khách hàng khác): Bắt đầu 5 giây sau T1, cũng đọc số ghế trống của chuyến bay VN123 (vẫn thấy còn 1 ghế); khách thanh toán nhanh (trong 10 giây); thực hiện UPDATE số ghế trống về 0 và COMMIT thành công TRƯỚC KHI T1 hoàn tất. 
Hãy phân tích: Hiện tượng tranh chấp dữ liệu này được gọi là gì? Nếu hệ thống đang chạy ở mức cô lập mặc định Read Committed thì hậu quả gì sẽ xảy ra cho T1? Đề xuất 2 giải pháp kỹ thuật (mức cô lập hoặc cơ chế khóa) để ngăn chặn hoàn toàn lỗi nghiệp vụ bán vé vượt số lượng (Overbooking) này.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Phân biệt sự khác nhau giữa hiện tượng Đọc không thể lặp lại (Non-repeatable Read) và Đọc dữ liệu ma (Phantom Read). Tại sao nâng mức cô lập lên Repeatable Read chỉ ngăn được Non-repeatable Read mà vẫn có thể bị Phantom Read, và mức Serializable giải quyết triệt để vấn đề này bằng loại khóa nào?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Phân tích hiện tượng: 
Hiện tượng này được gọi là Dị thường Ghi đè Mất mát (Lost Update Anomaly) hoặc Không thể đọc lại (Non-repeatable Read). Cả hai giao dịch T1 và T2 cùng đọc số ghế còn lại là 1; T2 đặt chỗ và commit thành công trước, làm số ghế thực tế bằng 0. Khi T1 tiếp tục chạy, ở mức cô lập Read Committed, Shared Lock đọc dữ liệu của T1 đã bị giải phóng ngay sau câu lệnh SELECT ban đầu. Do đó, T1 không biết dữ liệu đã bị T2 thay đổi, T1 tiếp tục chạy lệnh UPDATE và ghi đè lên kết quả của T2. Hậu quả: Ghế số 1 bị bán trùng lặp cho cả 2 hành khách (Overbooking) gây thiệt hại kinh doanh nghiêm trọng. 
2 giải pháp kỹ thuật triệt để: 
(1) Giải pháp 1 - Khóa bi quan tường minh (Pessimistic Locking với gợi ý khóa UPDLOCK): Trong câu lệnh SELECT ban đầu của T1, ta sử dụng gợi ý khóa: `SELECT SeatsAvailable FROM Flights WITH (UPDLOCK, ROWLOCK) WHERE FlightID = 'VN123'`. Khóa cập nhật UPDLOCK sẽ chặn ngay lập tức T2 không cho phép đọc để ghi, T2 buộc phải chờ T1 hoàn tất giao dịch rồi mới được đọc lại trạng thái mới (thấy số ghế bằng 0 và báo hết vé). 
(2) Giải pháp 2 - Khóa lạc quan (Optimistic Concurrency Control qua Version/Timestamp): Thêm cột `Version INT` hoặc `RowVersion (TIMESTAMP)` vào bảng Flights. Khi T1 thực thi UPDATE, nó kiểm tra: `UPDATE Flights SET SeatsAvailable = 0, Version = Version + 1 WHERE FlightID = 'VN123' AND Version = @OriginalVersion`. Khi T2 đã commit trước thì Version của dòng đã tăng lên; câu lệnh UPDATE của T1 sẽ tác động 0 dòng (@@ROWCOUNT = 0). Hệ thống phát hiện xung đột và tự động thông báo cho khách hàng T1 vé đã được người khác đặt.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Chẩn đoán chính xác hiện tượng Lost Update / Overbooking** | Chỉ rõ việc giải phóng Shared Lock sớm ở Read Committed dẫn đến ghi đè mất mát dữ liệu. | Analyze | **3.5đ** |
| **Giải pháp Khóa bi quan (Pessimistic UPDLOCK)** | Viết đúng câu truy vấn WITH (UPDLOCK, ROWLOCK) và giải thích cơ chế ngăn chặn T2. | Apply | **3.5đ** |
| **Giải pháp Khóa lạc quan (Optimistic Versioning)** | Trình bày chuẩn mực cơ chế cột Version và kiểm tra @@ROWCOUNT để phát hiện xung đột. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 24. `DBI202-EXAM-024`: Phân tích Kế hoạch Thực thi, Phát hiện Table Scan và Thiết kế Composite Index theo ESR

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-024` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 4 (ACID, Mức cô lập & Indexing)** |
| **Ánh xạ CLO FLM** | **CLO7** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO7** (`00000000-0000-0000-0000-000000000667`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 42 (Slot 21) — Query Optimization (Execution Plan & ESR Rule) |
| **Mục tiêu đánh giá** | Đánh giá năng lực đọc Kế hoạch thực thi (Execution Plan), xác định điểm nghẽn Table Scan và áp dụng quy tắc vàng ESR để thiết kế chỉ mục phức hợp tối ưu. |

**Nội dung câu hỏi phát vấn:**
> Cho câu lệnh truy vấn báo cáo thường xuyên chạy trên bảng `Orders` (có 5 triệu bản ghi): 
```sql
SELECT CustomerID, OrderDate, TotalAmount 
FROM Orders 
WHERE Status = 'Completed' 
  AND OrderDate >= '2024-01-01' AND OrderDate < '2024-07-01' 
ORDER BY CustomerID ASC;
``` 
Khi kiểm tra Execution Plan, DBA thấy hệ thống đang thực hiện Clustered Index Scan (chi phí 92%) và một toán tử Sort (chi phí 8%). Hãy phân tích nguyên nhân và áp dụng Nguyên tắc vàng ESR (Equality - Sort - Range) để thiết kế một Chỉ mục phức hợp (Composite Non-Clustered Index) hoàn hảo nhất giúp loại bỏ hoàn toàn toán tử Sort và biến Scan thành Index Seek tối ưu.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Nếu trong câu truy vấn, điều kiện lọc theo phạm vi (Range: OrderDate >= ... AND OrderDate < ...) được đưa lên trước cột sắp xếp (Sort: CustomerID) trong định nghĩa khóa chỉ mục thì Query Optimizer có còn tận dụng được cây B-Tree để tránh toán tử Sort nữa không? Tại sao quy tắc ESR bắt buộc Sort phải đứng trước Range?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> Phân tích Kế hoạch thực thi hiện tại: 
(1) Nguyên nhân Clustered Index Scan: Bảng Orders chưa có chỉ mục nào phù hợp với bộ lọc kết hợp giữa Status, OrderDate và CustomerID; do đó Query Optimizer bắt buộc phải duyệt tuần tự qua toàn bộ 5 triệu trang dữ liệu của bảng để kiểm tra điều kiện lọc, gây lãng phí hàng chục nghìn lượt I/O đọc đĩa. 
(2) Nguyên nhân Toán tử Sort: Do dữ liệu sau khi lọc không được sắp xếp sẵn theo CustomerID, hệ thống phải cấp phát vùng nhớ RAM (Memory Grant) để thực hiện sắp xếp thô; nếu thiếu RAM nó sẽ tràn vào tempdb gây nghẽn nghiêm trọng. 
Áp dụng Nguyên tắc vàng ESR (Equality - Sort - Range) để sắp xếp thứ tự các cột khóa chỉ mục: 
- E (Equality - So sánh bằng): Cột có điều kiện so sánh bằng (`Status = 'Completed'`) BẮT BUỘC phải đặt ở vị trí TIÊN PHONG ĐẦU TIÊN (Vị trí 1) trong Index Key. Điều này giúp bộ máy nhảy ngay tới đúng nhánh cây chứa các đơn hàng Completed. 
- S (Sort - Sắp xếp): Cột nằm trong mệnh đề ORDER BY (`CustomerID`) phải được đặt ở VỊ TRÍ THỨ HAI (Vị trí 2) để dữ liệu trong nhánh Status đã lọc tự động được sắp xếp sẵn theo CustomerID, loại bỏ 100% chi phí của toán tử Sort. 
- R (Range - So sánh khoảng): Cột có điều kiện so sánh khoảng (`OrderDate >= ... AND OrderDate < ...`) được đặt ở VỊ TRÍ THỨ BA (Vị trí 3). 
- Covering Index (INCLUDE): Đưa cột `TotalAmount` vào mệnh đề INCLUDE để tạo chỉ mục bao phủ hoàn toàn (Covering Index), giúp hệ thống không cần thực hiện thêm bước Key Lookup về bảng gốc. 
Câu lệnh tạo chỉ mục hoàn hảo: 
```sql
CREATE NONCLUSTERED INDEX IX_Orders_Status_CustomerID_OrderDate
ON Orders (Status, CustomerID, OrderDate)
INCLUDE (TotalAmount);
```

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Phân tích nguyên nhân Clustered Index Scan & Toán tử Sort** | Chỉ rõ chi phí quét 5 triệu dòng và rủi ro cấp phát RAM/TempDB của phép Sort. | Analyze | **3.0đ** |
| **Áp dụng chính xác quy tắc vàng ESR** | Lập luận đúng thứ tự: Status (Equality) -> CustomerID (Sort) -> OrderDate (Range). | Analyze | **4.0đ** |
| **Kỹ thuật Covering Index với mệnh đề INCLUDE** | Viết đúng lệnh CREATE INDEX và giải thích việc đưa TotalAmount vào INCLUDE để triệt tiêu Key Lookup. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 25. `DBI202-EXAM-025`: Phân tích Độ mịn của Khóa (Lock Granularity), Hiện tượng Leo thang Khóa (Lock Escalation) và Cơ chế RCSI trong SQL Server

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-025` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 4 (Tối ưu hóa Index & Quản trị Giao dịch ACID)** |
| **Ánh xạ CLO FLM** | **CLO7** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO7** (`00000000-0000-0000-0000-000000000667`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 43-44 & 55-56 (Slot 22 & 28) — 9.2 Concurrency Control, Lock Escalation & Snapshot Isolation |
| **Mục tiêu đánh giá** | Đánh giá năng lực phân tích cơ chế quản lý khóa đa cấp độ (Row, Page, Table), nguyên nhân gây nghẽn toàn bảng do Lock Escalation và giải pháp đa phiên bản dòng RCSI. |

**Nội dung câu hỏi phát vấn:**
> Trong MS SQL Server, hãy phân tích sự đánh đổi giữa các cấp độ mịn của khóa (`Lock Granularity`: `RID/KEY Row Lock`, `PAGE Lock`, `TABLE Lock`) và vai trò của **Khóa Ý định (`Intent Locks`: `IS`, `IX`, `SIX`)**. Hiện tượng **Leo thang khóa (`Lock Escalation`)** xảy ra khi nào và tại sao một câu lệnh `DELETE` hoặc `UPDATE` hàng chục ngàn dòng trong giờ cao điểm lại có thể làm treo đứng toàn bộ các giao dịch đọc `SELECT` khác trên bảng? Hãy đề xuất 2 giải pháp kỹ thuật (chia nhỏ lô `Batching` và bật `READ_COMMITTED_SNAPSHOT`) để xử lý triệt để.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Khi bật tùy chọn `ALTER DATABASE ... SET READ_COMMITTED_SNAPSHOT ON (RCSI)`, MS SQL Server lưu trữ các phiên bản dòng cũ (`Row Versions`) ở cơ sở dữ liệu hệ thống nào, và cần giám sát tài nguyên phần cứng nào để tránh nghẽn hệ thống đó?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> 1. Đa độ mịn của Khóa (`Lock Granularity`) và Khóa Ý định (`Intent Locks`):
* **Sự đánh đổi (Trade-off):**
  + *Khóa mức dòng (`Row Lock - KEY/RID`):* Độ tương tranh (Concurrency) cao nhất vì các giao dịch khác vẫn truy cập được các dòng lân cận, nhưng tốn rất nhiều bộ nhớ RAM để quản lý hàng ngàn cấu trúc khóa (mỗi khóa tốn ~96 bytes).
  + *Khóa mức bảng (`Table Lock`):* Chi phí quản lý bộ nhớ cực thấp (chỉ 1 khóa), nhưng triệt tiêu khả năng chạy đồng thời.
* **Vai trò của Khóa Ý định (`Intent Shared - IS`, `Intent Exclusive - IX`):** Khi giao dịch $T_1$ muốn khóa độc quyền (`X`) một dòng, nó sẽ đặt khóa `IX` ở mức Bảng và mức Trang trước. Nhờ đó, nếu giao dịch $T_2$ muốn khóa toàn bộ Bảng (`S` hoặc `X`), $T_2$ chỉ cần kiểm tra cờ `IX` ở ngay đỉnh Bảng trong $O(1)$ mà không cần quét hàng triệu dòng để xem có dòng nào đang bị khóa hay không.

2. Hiện tượng Leo thang khóa (`Lock Escalation`) gây treo bảng:
- Khi một câu lệnh đơn lẻ tích lũy quá ngưỡng **5,000 khóa mức dòng/trang** trên cùng một bảng (hoặc khi bộ nhớ Lock Manager vượt ngưỡng), SQL Server tự động thu hồi toàn bộ các khóa nhỏ và nâng cấp thẳng lên một **Khóa toàn bảng (`Table-level X Lock`)** để giải phóng RAM (*lưu ý: SQL Server leo thang từ Row/Page thẳng lên Table, không leo thang từ Row lên Page*).
- Hậu quả: Ở chế độ `READ COMMITTED` mặc định (dùng khóa bi quan), mọi câu lệnh `SELECT` cần xin khóa đọc (`S` / `IS`) trên bảng đó đều bị chặn đứng (`Blocked`) cho đến khi giao dịch `UPDATE`/`DELETE` khổng lồ kia `COMMIT`.

3. Hai giải pháp khắc phục triệt để trong thực tế:
* **Giải pháp 1 — Chia nhỏ lô cập nhật (`Chunking / Batching < 5,000 rows`):** Thay vì xóa/sửa 100,000 dòng trong 1 lệnh, dùng vòng lặp `WHILE (1 = 1)` kết hợp `DELETE TOP (2000) FROM ...` để mỗi giao dịch nhỏ chỉ giữ dưới 2,000 khóa dòng, không bao giờ chạm ngưỡng 5,000 của Lock Escalation.
* **Giải pháp 2 — Bật `READ_COMMITTED_SNAPSHOT (RCSI)`:** Chuyển cơ chế đọc sang đa phiên bản dòng (`Row Versioning` lưu bản chụp cũ trong `tempdb`). Khi đó **Đọc không chặn Ghi và Ghi không chặn Đọc** (`Readers do not block Writers`), giúp các câu lệnh `SELECT` đọc ngay bản chụp nhất quán gần nhất mà không cần xin khóa `S`.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **Phân tích đúng đánh đổi giữa Row/Page/Table Lock và vai trò của Intent Locks (IS, IX)** | Giải thích rõ sự đánh đổi Concurrency vs Memory Overhead và cách Intent Lock giúp kiểm tra xung đột ở mức bảng trong O(1). | Analyze | **3.5đ** |
| **Giải thích cơ chế Lock Escalation (ngưỡng ~5,000 khóa) từ Row/Page lên Table Lock** | Mô tả chính xác nguyên nhân leo thang khóa thẳng lên mức Table gây chặn toàn bộ truy vấn đọc/ghi khác. | Analyze | **3.5đ** |
| **Đề xuất đúng 2 giải pháp: Chia lô TOP (2000) và bật READ_COMMITTED_SNAPSHOT (RCSI)** | Trình bày rõ kỹ thuật Batching dưới 5,000 dòng và cơ chế Row Versioning trong tempdb giúp Đọc không bị chặn bởi Ghi. | Apply | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---

### 26. `DBI202-EXAM-026`: So sánh Toàn diện giữa INSTEAD OF Trigger trên View Phức tạp và Stored Procedure

| Thuộc tính | Giá trị cấu hình |
|---|---|
| **Mã câu hỏi** | `DBI202-EXAM-026` |
| **Kho dữ liệu** | `exam_questions` (MF-04 Phòng Lab Kiosk) |
| **Cấu trúc đề thi** | **Slot 5 (Lập trình T-SQL & Toàn vẹn nghiệp vụ)** |
| **Ánh xạ CLO FLM** | **CLO6** (Chi tiết chuẩn đầu ra Syllabus) |
| **Ánh xạ CLO DB** | **CLO6** (`00000000-0000-0000-0000-000000000666`) |
| **Mức nhận thức Bloom** | **Analyze** |
| **Mức độ khó** | **Hard** |
| **Phân bổ Session** | Session 49-54 (Slot 25-27) — T-SQL Architecture Trade-offs |
| **Mục tiêu đánh giá** | Đánh giá năng lực so sánh các mô hình kiến trúc lập trình CSDL giữa tính trong suốt của View và tính tường minh của Stored Procedure. |

**Nội dung câu hỏi phát vấn:**
> Để cho phép các ứng dụng bên ngoài thực hiện cập nhật dữ liệu vào một cấu trúc gồm nhiều bảng có quan hệ phức tạp, các kiến trúc sư có hai phương án tiếp cận: (Phương án A) Tạo một View phức hợp (JOIN nhiều bảng) và gắn một INSTEAD OF Trigger trên View đó; (Phương án B) Xây dựng một Stored Procedure nhận tham số và trực tiếp thực thi cập nhật các bảng. Hãy so sánh toàn diện hai phương án này về: tính trong suốt đối với ứng dụng (Transparency), khả năng tái sử dụng với các ORM (như EF Core), độ phức tạp bảo trì và hiệu năng xử lý.

**Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):**
> *"Khi các ứng dụng hiện đại sử dụng ORM như Entity Framework Core để thực hiện thao tác thêm/sửa/xóa dữ liệu, giải pháp INSTEAD OF Trigger trên View có ưu thế vượt trội gì về khả năng tương thích với mô hình Change Tracker và Unit of Work so với việc gọi Stored Procedure thủ công?"*

**Câu trả lời mẫu chuẩn kỹ thuật (Model Answer):**
> So sánh toàn diện giữa hai giải pháp kiến trúc: 
(1) Tính trong suốt (Transparency) và Trải nghiệm tích hợp: 
- Phương án A (View + INSTEAD OF Trigger): Mang tính trong suốt tuyệt đối (Abstract/Transparent Data Layer). Đối với ứng dụng bên ngoài hoặc các công cụ BI/Reporting, View hoạt động giống hệt như một bảng dữ liệu thông thường; ứng dụng chỉ cần gửi các câu lệnh INSERT/UPDATE/DELETE chuẩn ANSI. 
- Phương án B (Stored Procedure): Không có tính trong suốt; ứng dụng client buộc phải biết rõ tên thủ tục, danh sách tham số đầu vào và phải gọi bằng cú pháp riêng (EXEC sp_Update...). 
(2) Khả năng tương thích với ORM (Entity Framework Core, Hibernate): 
- Phương án A (View + Trigger): Tương thích rất tự nhiên với ORM; ORM có thể ánh xạ View như một Entity và tự động sinh câu lệnh SQL DML mà không cần can thiệp cấu hình phức tạp. 
- Phương án B (Stored Procedure): Đòi hỏi ORM phải cấu hình ánh xạ thủ công các hàm gọi SP; việc theo dõi trạng thái thay đổi bản ghi (Change Tracking) của ORM trở nên cồng kềnh hơn. 
(3) Độ phức tạp bảo trì và Gỡ lỗi (Debugging / Maintainability): 
- Phương án A (View + Trigger): Logic bị ẩn ngầm (Hidden Business Logic / Side Effects); khi có lỗi xảy ra, lập trình viên rất khó phát hiện lệnh DML bị chuyển hướng âm thầm bởi Trigger; việc bảo trì phức tạp khi cấu trúc bảng cơ sở thay đổi. 
- Phương án B (Stored Procedure): Tường minh 100% (Explicit Logic); luồng xử lý rõ ràng, dễ dàng đặt breakpoint debug, dễ dàng ghi log kiểm toán và kiểm soát các mã lỗi trả về chi tiết. 
(4) Hiệu năng và Khả năng xử lý theo lô (Performance & Batch Processing): 
- Phương án A (Trigger): Thường gặp khó khăn khi xử lý các câu lệnh cập nhật số lượng lớn nếu mã trigger không được thiết kế tối ưu theo tập hợp từ bảng `inserted`/`deleted`. 
- Phương án B (Stored Procedure): Dễ dàng tối ưu hóa Kế hoạch thực thi (Plan Cache), hỗ trợ xử lý mảng tham số (Table-Valued Parameters - TVP) cho hiệu năng ghi hàng triệu bản ghi vượt trội. 
Kết luận: Chọn Phương án A khi muốn cung cấp một giao diện bảng ảo chuẩn cho các ứng dụng legacy hoặc ORM; chọn Phương án B cho các nghiệp vụ lõi (Core Business Services) đòi hỏi an toàn giao dịch cao, kiểm tra nghiệp vụ khắt khe và hiệu năng tối đa.

**Barem Rubric chấm điểm chi tiết (Tổng 10.0đ):**

| Tiêu chí đánh giá | Mô tả chi tiết tiêu chí | Mức Bloom | Điểm tối đa |
|---|---|:---:|:---:|
| **So sánh Tính trong suốt & Tương thích ORM** | Phân tích rõ ưu thế giao diện bảng chuẩn của View đối với ORM so với tính tường minh của SP. | Analyze | **3.5đ** |
| **So sánh Độ phức tạp bảo trì và Gỡ lỗi (Debugging)** | Chỉ ra cạm bẫy logic ẩn ngầm của Trigger so với khả năng kiểm soát luồng rõ ràng của SP. | Analyze | **3.5đ** |
| **So sánh Hiệu năng xử lý theo lô và Kết luận kiến trúc** | Đánh giá TVP, Plan Cache của SP và đưa ra tiêu chí lựa chọn cho từng ngữ cảnh thực tế. | Analyze | **3.0đ** |
| **TỔNG ĐIỂM BAREM** | **Đạt trọn vẹn 100% các tiêu chí kỹ thuật chuẩn mực** | — | **10.0đ** |

---
