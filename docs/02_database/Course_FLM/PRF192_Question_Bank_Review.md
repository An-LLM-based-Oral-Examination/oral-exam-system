# TÀI LIỆU RÀ SOÁT NGÂN HÀNG CÂU HỎI VẤN ĐÁP: PRF192 — PROGRAMMING FUNDAMENTALS (C)

> **Học phần:** PRF192 — Programming Fundamentals (Cơ sở lập trình C)  
> **Quyết định ban hành:** 1286/QĐ-ĐHFPT ngày 22/11/2024 — Đại học FPT  
> **Syllabus ID:** 12223 | **Số tín chỉ:** 3 tín chỉ (45 giờ contact slots trên lớp + 104 giờ tự học)  
> **Tài liệu tham khảo chính:** *The C Programming Language (2nd Edition)* — Kernighan & Ritchie (K&R); *Foundations of Programming Using C* — Evan Weaver.  
> **Đơn vị biên soạn:** Tiểu ban Đảm bảo Chất lượng Học thuật & Đánh giá Vấn đáp AI (FA26SE166)  
> **Mã tệp tài liệu:** `PRF192_Question_Bank_Review.md` | **Phiên bản:** v2.0.0 (Chuẩn hóa FLM 1-1 & 3NF)  
> **Tổng quy mô ngân hàng:** **62 câu hỏi vấn đáp kỹ thuật chuẩn hóa** (36 câu Kho Luyện tập & Thi thử + 26 câu Kho Thi thật)
> **Tiêu chuẩn kỹ thuật bắt buộc:** 100% câu hỏi có Model Answer chuyên sâu (>= 50 ký tự), 100% có Barem Rubric tổng điểm đúng 10.0đ, 100% có Follow-up Prompt, Cô lập tuyệt đối 2 kho theo Quy tắc R8, Zero Placeholder.

---

## PHẦN 1: BẢNG TỔNG QUAN HỌC PHẦN VÀ MA TRẬN PHÂN BỔ CÂU HỎI

### 1.1. Thông số Tổng quan Học phần PRF192

| Thuộc tính | Giá trị chi tiết từ Syllabus FLM và Hệ thống CSDL |
|---|---|
| **Mã môn học (Course Code)** | **PRF192** |
| **Tên môn học tiếng Việt** | Cơ sở lập trình |
| **Tên môn học tiếng Anh** | Programming Fundamentals |
| **Syllabus ID** | `12223` (Ban hành kèm Quyết định `1286/QĐ-ĐHFPT`) |
| **Số tín chỉ (Credits)** | 3 tín chỉ (150 giờ học: 45 giờ trên lớp, 1 giờ thi kết thúc, 104 giờ tự học) |
| **Môn tiên quyết (Prerequisite)** | Không có (None) |
| **Môi trường & Trình biên dịch** | **C/C++ (Dev-C++ 6.3, GCC/MinGW, Clang, VS Code)** |
| **Cơ cấu đánh giá quá trình** | Assignment (15%) + Practical Exam PE (30%) + Progress Tests (15%) + Workshop (10%) + Final Exam (30%) |
| **Điều kiện đạt học phần** | Điểm trung bình tổng kết $\ge 5.0/10$ và Điểm thi Final Exam $\ge 4.0/10$ |
| **Chuẩn đầu ra (`course_clos` 3NF)** | Đồng bộ **1-1 đủ 9 CLOs** (`CLO1` đến `CLO9`) giữa FLM Syllabus và CSDL |

### 1.2. Ánh xạ Chuẩn đầu ra 1-1 (9 FLM CLOs <-> 9 Database 3NF CLOs)

| Mã CLO FLM | Mô tả chuẩn đầu ra theo Syllabus FLM | Mức Bloom | Mã CLO DB (`course_clos`) | GUID Khóa chính CSDL (`course_clos.id`) |
|:---:|---|:---:|:---:|---|
| **CLO1** | Hiểu chương trình máy tính, quy trình biên dịch (preprocessor, compile, assemble, link) và quy chuẩn chú thích mã nguồn C | Remember / Understand | **CLO1** | `00000000-0000-0000-0000-000000000461` |
| **CLO2** | Minh họa và vận dụng biến, hằng, kiểu dữ liệu, biểu thức, ép kiểu và các toán tử (số học, quan hệ, logic, thao tác bit) | Understand / Apply | **CLO2** | `00000000-0000-0000-0000-000000000462` |
| **CLO3** | Sử dụng cấu trúc điều khiển rẽ nhánh (if/else, switch/case) và vòng lặp (for, while, do-while) trong giải thuật C | Understand / Apply | **CLO3** | `00000000-0000-0000-0000-000000000463` |
| **CLO4** | Thiết kế module hóa chương trình (Cohesion & Coupling), hàm tự định nghĩa, truyền tham trị (Pass-by-value), phạm vi biến và lớp lưu trữ | Understand / Apply / Analyze | **CLO4** | `00000000-0000-0000-0000-000000000464` |
| **CLO5** | Hiểu và vận dụng con trỏ (&, *), số học con trỏ, truyền địa chỉ, cấp phát bộ nhớ động (malloc, calloc, realloc, free) và con trỏ hàm | Apply / Analyze | **CLO5** | `00000000-0000-0000-0000-000000000465` |
| **CLO6** | Khai thác hiệu quả các thư viện chuẩn C (stdio.h, stdlib.h, string.h, math.h, ctype.h, time.h), sinh số ngẫu nhiên và định dạng nhập/xuất | Understand / Apply | **CLO6** | `00000000-0000-0000-0000-000000000466` |
| **CLO7** | Cài đặt và xử lý mảng một chiều (tìm kiếm tuyến tính, nhị phân, sắp xếp), ma trận hai chiều và kiểu dữ liệu cấu trúc (struct) | Apply / Analyze | **CLO7** | `00000000-0000-0000-0000-000000000467` |
| **CLO8** | Xử lý chuỗi ký tự C (null terminator '\0'), chuẩn hóa xâu và phòng chống lỗ hổng tràn bộ đệm (Buffer Overflow) | Apply / Analyze | **CLO8** | `00000000-0000-0000-0000-000000000468` |
| **CLO9** | Làm việc với luồng tệp (File Streams), phân biệt tệp văn bản (Text File) và tệp nhị phân (Binary File), đọc/ghi cấu trúc và truy cập ngẫu nhiên | Apply / Analyze | **CLO9** | `00000000-0000-0000-0000-000000000469` |

### 1.3. Ma trận Phân bổ theo Kho & Độ khó

| Kho câu hỏi | Dễ (Easy) | Trung bình (Medium) | Khó (Hard) | Tổng số câu | Tỷ lệ (%) | Mục đích sử dụng nghiệp vụ |
|---|:---:|:---:|:---:|:---:|:---:|---|
| **Kho Luyện tập & Thi thử (`practice_questions`)** | 12 | 14 | 10 | **36** | 58.1% | Luyện tập tự do MF-01 & Thi thử tính giờ MF-02 |
| **Kho Thi thật phòng Lab (`exam_questions`)** | 5 | 13 | 8 | **26** | 41.9% | Đề thi oral exam Kiosk MF-04 (Theo 5 blueprint slots) |
| **TỔNG CỘNG TOÀN NGÂN HÀNG** | **17** | **27** | **18** | **62** | **100.0%** | Đáp ứng vượt mức yêu cầu $\ge 60$ câu hỏi/môn |

### 1.4. Ma trận Phân bổ theo Thang Nhận thức Bloom

| Kho câu hỏi | Nhớ (Remember) | Hiểu (Understand) | Vận dụng (Apply) | Phân tích (Analyze) | Tổng cộng |
|---|:---:|:---:|:---:|:---:|:---:|
| **Kho Luyện tập (`practice_questions`)** | 5 | 13 | 11 | 7 | **36** |
| **Kho Thi thật (`exam_questions`)** | 6 | 8 | 7 | 5 | **26** |
| **TỔNG SỐ CÂU HỎI THEO BLOOM** | **11** | **21** | **18** | **12** | **62** |
| **Tỷ lệ phần trăm (%)** | 17.7% | 33.9% | 29.0% | 19.4% | **100.0%** |

### 1.5. Ma trận Phân bổ theo 9 Chuẩn Đầu Ra (CLOs FLM & DB 1-1)

| Mã CLO | Mô tả Chuẩn Đầu Ra | Số câu Luyện tập | Số câu Thi thật | Tổng số câu | Trọng tâm đánh giá chuyên môn |
|---|---|:---:|:---:|:---:|---|
| **CLO1** | Hiểu chương trình máy tính, quy trình biên dịch (preprocessor, compile, assemble, link) và quy chuẩn chú thích mã nguồn C | 2 | 1 | **3** | Cấu trúc chương trình, hàm main, preprocessor, quy trình biên dịch 4 bước, chú thích mã nguồn (CQ2.2) |
| **CLO2** | Minh họa và vận dụng biến, hằng, kiểu dữ liệu, biểu thức, ép kiểu và các toán tử (số học, quan hệ, logic, thao tác bit) | 5 | 3 | **8** | Kiểu dữ liệu, dải giá trị, ép kiểu, toán tử ++/--, đoản mạch, toán tử thao tác bit (Bitwise & Bitmask) |
| **CLO3** | Sử dụng cấu trúc điều khiển rẽ nhánh (if/else, switch/case) và vòng lặp (for, while, do-while) trong giải thuật C | 4 | 1 | **5** | Rẽ nhánh if-else, toán tử 3 ngôi, switch-case, vòng lặp đơn/lồng nhau, cờ hiệu & lính canh |
| **CLO4** | Thiết kế module hóa chương trình (Cohesion & Coupling), hàm tự định nghĩa, truyền tham trị (Pass-by-value), phạm vi biến và lớp lưu trữ | 3 | 4 | **7** | Thiết kế Module hóa, Cohesion & Coupling, nguyên mẫu hàm, truyền tham trị, Storage Classes, Header Guards |
| **CLO5** | Hiểu và vận dụng con trỏ (&, *), số học con trỏ, truyền địa chỉ, cấp phát bộ nhớ động (malloc, calloc, realloc, free) và con trỏ hàm | 7 | 6 | **13** | Toán tử &, *, con trỏ NULL/void*/const, số học con trỏ, malloc/calloc/realloc/free, int**, qsort callback |
| **CLO6** | Khai thác hiệu quả các thư viện chuẩn C (stdio.h, stdlib.h, string.h, math.h, ctype.h, time.h), sinh số ngẫu nhiên và định dạng nhập/xuất | 3 | 2 | **5** | Thư viện chuẩn stdio.h, stdlib.h, math.h, time.h, ctype.h, PRNG rand()/srand(), định dạng printf/scanf |
| **CLO7** | Cài đặt và xử lý mảng một chiều (tìm kiếm tuyến tính, nhị phân, sắp xếp), ma trận hai chiều và kiểu dữ liệu cấu trúc (struct) | 6 | 3 | **9** | Mảng 1D/2D, Row-major order, Linear Search, Selection/Bubble Sort, Struct, Data Alignment & Padding |
| **CLO8** | Xử lý chuỗi ký tự C (null terminator '\0'), chuẩn hóa xâu và phòng chống lỗ hổng tràn bộ đệm (Buffer Overflow) | 3 | 3 | **6** | Chuỗi ký tự '\0', fgets, chuẩn hóa họ tên, Buffer Overflow, Off-by-One strncpy & snprintf an toàn |
| **CLO9** | Làm việc với luồng tệp (File Streams), phân biệt tệp văn bản (Text File) và tệp nhị phân (Binary File), đọc/ghi cấu trúc và truy cập ngẫu nhiên | 3 | 3 | **6** | Text vs Binary File, fopen modes, fgets/sscanf log parsing, fread/fwrite Struct, cập nhật ngẫu nhiên fseek |
| **TỔNG** | **Toàn bộ 9 Chuẩn Đầu Ra Môn Học (1-1)** | **36** | **26** | **62** | **Phủ kín 60 Sessions, cô lập 100% giữa 2 kho theo R8** |

---

## PHẦN 2: BẢNG CHỈ MỤC MASTER DANH MỤC 62 CÂU HỎI VẤN ĐÁP PRF192

### 2.1. Chỉ mục 36 câu hỏi Kho Luyện tập (`practice_questions`: `PRF_PQ01` — `PRF_PQ36`)

| Mã câu hỏi | Tiêu đề câu hỏi vấn đáp kỹ thuật | FLM CLO | DB CLO | Mức Bloom | Độ khó | Buổi học (Session) | Cụm kỹ thuật trọng tâm |
|:---:|---|:---:|:---:|:---:|:---:|:---:|---|
| **PRF_PQ01** | Cấu trúc cơ bản của chương trình C và vai trò của hàm main | CLO1 | CLO1 | `Remember` | **Easy** | Session 1-3 | C Basic Syntax & Program Structure |
| **PRF_PQ02** | Quy trình biên dịch mã nguồn C qua 4 giai đoạn | CLO1 | CLO1 | `Understand` | **Easy** | Session 3 | C Compilation Pipeline |
| **PRF_PQ03** | Kiểu dữ liệu nguyên thủy, dải giá trị và kích thước bộ nhớ trong C | CLO2 | CLO2 | `Remember` | **Easy** | Session 4-5 | Data Types & Memory Allocation |
| **PRF_PQ04** | Phân biệt hằng số #define (Macro) và từ khóa const trong C | CLO2 | CLO2 | `Understand` | **Easy** | Session 4 | Constants & Preprocessor vs Compiler |
| **PRF_PQ05** | Phép chia nguyên và kỹ thuật ép kiểu tường minh trong C | CLO2 | CLO2 | `Understand` | **Easy** | Session 7-8 | Expressions & Type Casting |
| **PRF_PQ06** | Toán tử tăng/giảm tiền tố (++x) và hậu tố (x++) trong C | CLO2 | CLO2 | `Understand` | **Easy** | Session 7 | Operators & Pre/Post-increment |
| **PRF_PQ07** | Cấu trúc điều khiển rẽ nhánh if-else và toán tử điều kiện 3 ngôi | CLO3 | CLO3 | `Understand` | **Easy** | Session 9 | Control Flow & Selection Constructs |
| **PRF_PQ08** | Cấu trúc lựa chọn switch-case và hiện tượng fall-through khi thiếu break | CLO3 | CLO3 | `Understand` | **Easy** | Session 9 | Switch-Case & Fall-through Behavior |
| **PRF_PQ09** | So sánh vòng lặp while và do-while về số lần lặp tối thiểu | CLO3 | CLO3 | `Understand` | **Easy** | Session 10 | Loops: While vs Do-While |
| **PRF_PQ10** | Lệnh điều khiển vòng lặp: Phân biệt break và continue | CLO3 | CLO3 | `Remember` | **Easy** | Session 10 | Loop Control: Break vs Continue |
| **PRF_PQ11** | Bản chất lưu trữ ô nhớ của mảng một chiều và đánh chỉ số trong C | CLO7 | CLO7 | `Remember` | **Easy** | Session 37 | 1D Arrays & Memory Layout |
| **PRF_PQ12** | Bản chất chuỗi ký tự C và vai trò của ký tự null terminator '\0' | CLO8 | CLO8 | `Remember` | **Easy** | Session 46 | C Strings & Null Terminator |
| **PRF_PQ13** | Cơ chế đánh giá đoản mạch (Short-circuit Evaluation) của toán tử logic && và || | CLO2 | CLO2 | `Understand` | **Medium** | Session 7 | Logical Operators & Short-circuit Evaluation |
| **PRF_PQ14** | Nguyên mẫu hàm (Function Prototype) và vai trò trong kiểm tra kiểu dữ liệu | CLO4 | CLO4 | `Understand` | **Medium** | Session 19 | Modular Programming & Function Prototypes |
| **PRF_PQ15** | Cơ chế truyền tham trị (Pass-by-value) và Stack Frame khi gọi hàm trong C | CLO4 | CLO4 | `Understand` | **Medium** | Session 20 | Function Calls & Call Stack Frames |
| **PRF_PQ16** | Phân biệt biến cục bộ (Local), toàn cục (Global) và biến tĩnh cục bộ (static local) | CLO4 | CLO4 | `Understand` | **Medium** | Session 22 | Variable Scope & Storage Duration |
| **PRF_PQ17** | Khai thác thư viện chuẩn math.h, time.h và cơ chế sinh số giả ngẫu nhiên rand() / srand() | CLO6 | CLO6 | `Understand` | **Medium** | Session 32-33 | Standard Libraries: math.h, time.h & Pseudo-Random Number Generation |
| **PRF_PQ18** | Bản chất con trỏ, toán tử lấy địa chỉ & và toán tử giải tham chiếu * | CLO5 | CLO5 | `Understand` | **Medium** | Session 28-29 | Pointers & Address Operators |
| **PRF_PQ19** | Mô phỏng truyền tham chiếu bằng con trỏ trong bài toán hoán vị swap | CLO5 | CLO5 | `Apply` | **Medium** | Session 30 | Pass-by-reference Simulation & Swap |
| **PRF_PQ20** | Số học con trỏ (Pointer Arithmetic): Ý nghĩa của ptr + 1 phụ thuộc vào sizeof(*ptr) | CLO5 | CLO5 | `Apply` | **Medium** | Session 29 | Pointer Arithmetic & Memory Scaling |
| **PRF_PQ21** | Mối quan hệ tương đương giữa Mảng và Con trỏ: arr[i] == *(arr + i) | CLO5 | CLO5 | `Apply` | **Medium** | Session 29-30 & 37 | Array-Pointer Equivalence |
| **PRF_PQ22** | Ma trận hai chiều (2D Array) và thứ tự lưu trữ ưu tiên dòng (Row-major order) | CLO7 | CLO7 | `Apply` | **Medium** | Session 38 | 2D Matrices & Row-Major Layout |
| **PRF_PQ23** | Thuật toán sắp xếp chọn (Selection Sort) trên mảng một chiều | CLO7 | CLO7 | `Apply` | **Medium** | Session 39 | Sorting Algorithms: Selection Sort |
| **PRF_PQ24** | Nguy cơ bảo mật của hàm gets() và kỹ thuật nhập chuỗi an toàn với fgets() | CLO8 | CLO8 | `Apply` | **Medium** | Session 47 | String Input Security: gets vs fgets |
| **PRF_PQ25** | Các hàm thao tác chuỗi cốt lõi trong string.h: strlen, strcmp, strcpy, strcat | CLO6 | CLO6 | `Apply` | **Medium** | Session 48 | String Functions in string.h |
| **PRF_PQ26** | Khai báo Struct và phân biệt toán tử chấm '.' và mũi tên '->' | CLO7 | CLO7 | `Apply` | **Medium** | Session 40, 42 | Structures: Dot vs Arrow Operator |
| **PRF_PQ27** | Quản lý bộ nhớ động trong C: Phân biệt malloc, calloc, realloc và free | CLO5 | CLO5 | `Apply` | **Hard** | Session 31 | Dynamic Memory Allocation (DMA) |
| **PRF_PQ28** | Phân tích 3 lỗi bộ nhớ nguy hiểm: Memory Leak, Dangling Pointer và Double Free | CLO5 | CLO5 | `Analyze` | **Hard** | Session 31 | Memory Bugs: Leak, Dangling Pointer, Double Free |
| **PRF_PQ29** | Kỹ thuật cấp phát động ma trận 2 chiều bằng con trỏ cấp hai (int**) và giải phóng bộ nhớ đúng cách | CLO5 | CLO5 | `Analyze` | **Hard** | Session 31, 38 | Dynamic 2D Arrays & Double Pointers |
| **PRF_PQ30** | Căn chỉnh bộ nhớ (Data Alignment) và Byte Padding trong cấu trúc Struct C | CLO7 | CLO7 | `Analyze` | **Hard** | Session 40 | Struct Padding & Memory Alignment |
| **PRF_PQ31** | Mảng các cấu trúc (Array of Structs): Quản lý danh sách sinh viên và sắp xếp theo điểm | CLO7 | CLO7 | `Apply` | **Hard** | Session 42, 45 | Array of Structs & Sorting Algorithm |
| **PRF_PQ32** | Phân tích bản chất lỗi tràn bộ đệm (Buffer Overflow) trên Call Stack và cách khai thác | CLO8 | CLO8 | `Analyze` | **Hard** | Session 47 | Stack-based Buffer Overflow Analysis |
| **PRF_PQ33** | Phân biệt tệp văn bản (Text File) và tệp nhị phân (Binary File): Cơ chế mã hóa và hiệu năng | CLO9 | CLO9 | `Analyze` | **Hard** | Session 53 | File I/O: Text vs Binary Files |
| **PRF_PQ34** | Kỹ thuật lưu trữ và phục hồi mảng Struct bằng hàm fread và fwrite trên File nhị phân | CLO9 | CLO9 | `Apply` | **Hard** | Session 55, 57 | Binary File I/O: fread & fwrite with Structs |
| **PRF_PQ35** | Truy cập ngẫu nhiên trên File với fseek, ftell, rewind và bẫy lỗi vòng lặp feof() | CLO9 | CLO9 | `Analyze` | **Hard** | Session 54-56 | Random File Access & feof Pitfalls |
| **PRF_PQ36** | Con trỏ hàm (Function Pointer) và cơ chế Callback trong hàm thư viện chuẩn qsort | CLO6 | CLO6 | `Analyze` | **Hard** | Session 32 | Function Pointers & qsort Callback |

### 2.2. Chỉ mục 26 câu hỏi Kho Thi thật (`exam_questions`: `PRF_EQ01` — `PRF_EQ26`)

| Mã câu hỏi | Tiêu đề câu hỏi vấn đáp kỹ thuật | FLM CLO | DB CLO | Mức Bloom | Độ khó | Buổi học (Session) | Cụm kỹ thuật trọng tâm |
|:---:|---|:---:|:---:|:---:|:---:|:---:|---|
| **PRF_EQ01** | Quy chuẩn chú thích mã nguồn C (// vs /* */) và cơ chế xử lý của Preprocessor (CQ2.2) | CLO1 | CLO1 | `Remember` | **Easy** | Session 2 | Code Documentation, Comments (CQ2.2) & Preprocessor Stripping |
| **PRF_EQ02** | Các toán tử thao tác Bit (Bitwise Operators) và kỹ thuật mặt nạ Bitmask trong C | CLO2 | CLO2 | `Remember` | **Easy** | Session 7 | Bitwise Operators (&, |, ^, ~, <<, >>) & Bitmasking |
| **PRF_EQ03** | Danh mục và mức ưu tiên của các toán tử số học, logic và quan hệ trong C | CLO2 | CLO2 | `Remember` | **Easy** | Session 7-8 | Operator Precedence & Associativity Table |
| **PRF_EQ04** | Định nghĩa và phân loại 4 lớp lưu trữ (Storage Classes): auto, register, static, extern | CLO4 | CLO4 | `Remember` | **Medium** | Session 22 | C Storage Classes (auto, register, static, extern) |
| **PRF_EQ05** | Phân biệt con trỏ NULL, con trỏ tổng quát void* và con trỏ hoang (Wild Pointer) trong C | CLO5 | CLO5 | `Remember` | **Easy** | Session 28-29 | Pointer Classifications: NULL Pointer, void* Generic Pointer & Wild Pointer |
| **PRF_EQ06** | Danh mục các chế độ mở tệp (File Open Modes) trong hàm fopen | CLO9 | CLO9 | `Remember` | **Easy** | Session 55 | File Handling: fopen Modes (r, w, a, rb, wb, ab) |
| **PRF_EQ07** | Đặc tả định dạng nhập xuất (Format Specifiers) và kiểm soát luồng đệm trong printf / scanf | CLO6 | CLO6 | `Understand` | **Medium** | Session 6 & 34 | Formatted I/O Specifiers, Field Width, Scanset & Return Value of scanf |
| **PRF_EQ08** | Cơ chế thăng hạng kiểu ngầm định (Implicit Type Promotion) trong biểu thức số học | CLO2 | CLO2 | `Understand` | **Medium** | Session 8 | Type Promotion & Arithmetic Conversions |
| **PRF_EQ09** | Phân tích vòng lặp lồng nhau (Nested Loops), biến cờ hiệu (Flag) và vòng lặp lính canh (Sentinel) | CLO3 | CLO3 | `Understand` | **Medium** | Session 10-12 | Nested Loops Complexity, Boolean Flags & Sentinel-Controlled Loops |
| **PRF_EQ10** | Nguyên lý thiết kế Module hóa chương trình C: Độ kết dính (Cohesion) và Độ ghép nối (Coupling) | CLO4 | CLO4 | `Understand` | **Medium** | Session 16-18 | Modular Programming: High Cohesion, Low Coupling & Top-Down Design |
| **PRF_EQ11** | Ý nghĩa của từ khóa const khi kết hợp với con trỏ và bảo vệ tham số mảng trong hàm C | CLO5 | CLO5 | `Understand` | **Medium** | Session 29-30 & 37 | Const-Correctness with Pointers & Read-Only Array Parameters |
| **PRF_EQ12** | Phân biệt bản chất bộ nhớ giữa mảng ký tự (char[]) và con trỏ hằng chuỗi (char*) | CLO8 | CLO8 | `Understand` | **Medium** | Session 46 | String Memory Segments: char[] vs char |
| **PRF_EQ13** | Thuật toán Tìm kiếm tuyến tính (Linear Search) và Tìm kiếm nhị phân (Binary Search) trên mảng 1 chiều | CLO7 | CLO7 | `Understand` | **Medium** | Session 39 | 1D Array Searching: Linear Search vs Binary Search & Overflow-Safe Midpoint |
| **PRF_EQ14** | Cơ chế hoạt động của File Buffering trong thư viện stdio và vai trò của hàm fflush() | CLO6 | CLO6 | `Understand` | **Medium** | Session 52, 55 | File Streams & I/O Buffering (fflush) |
| **PRF_EQ15** | Cài đặt thuật toán kiểm tra số nguyên tố tối ưu hóa vòng lặp trong C | CLO4 | CLO4 | `Apply` | **Medium** | Session 19-20 | Algorithmic Functions: Prime Check Optimization |
| **PRF_EQ16** | Cài đặt hàm đảo ngược mảng một chiều bằng kỹ thuật hai con trỏ (Two Pointers) | CLO5 | CLO5 | `Apply` | **Medium** | Session 30, 43 | Two-Pointer Technique & Array Reversal |
| **PRF_EQ17** | Cài đặt thuật toán sắp xếp nổi bọt (Bubble Sort) trên mảng số nguyên tối ưu với cờ swapped | CLO7 | CLO7 | `Apply` | **Medium** | Session 39 | Bubble Sort & Early Termination Optimization |
| **PRF_EQ18** | Xử lý chuỗi ký tự: Viết hàm chuẩn hóa họ tên trong C | CLO8 | CLO8 | `Apply` | **Medium** | Session 51 | String Manipulation: Name Normalization |
| **PRF_EQ19** | Cấp phát động mảng số nguyên bằng malloc, kiểm tra NULL, mở rộng bằng realloc và giải phóng bằng free | CLO5 | CLO5 | `Apply` | **Hard** | Session 31 | Dynamic Array Resizing with realloc |
| **PRF_EQ20** | Thiết kế Struct quản lý Kho hàng (Inventory) và cài đặt hàm tìm kiếm, lọc mặt hàng dưới định mức | CLO7 | CLO7 | `Apply` | **Hard** | Session 42-45 | Struct Array Processing: Inventory Search & Low-Stock Filtering |
| **PRF_EQ21** | Đọc và phân tích tệp văn bản có cấu trúc (CSV / Log File) an toàn bằng kết hợp fgets() và sscanf() | CLO9 | CLO9 | `Apply` | **Hard** | Session 54-56 | Text File Parsing: Combining fgets() and sscanf() for Resilient I/O |
| **PRF_EQ22** | Phân tích kiến trúc biên dịch đa tệp (Multi-file Compilation) và cơ chế Header Guards (#ifndef / #define / #endif) | CLO4 | CLO4 | `Analyze` | **Hard** | Session 16-22 | Multi-File C Architecture, Header Guards & Internal vs External Linkage |
| **PRF_EQ23** | Phân tích lỗi trả về địa chỉ biến cục bộ trên Stack (Returning Pointer to Local Variable) và 2 mẫu thiết kế thay thế an toàn | CLO5 | CLO5 | `Analyze` | **Hard** | Session 30-31 | Stack Frame Lifetime Hazard: Returning Local Address vs Caller-Allocated Buffer |
| **PRF_EQ24** | Thiết kế ma trận động bằng mảng 1 chiều trải phẳng (1D Flattened Array) và tối ưu hóa CPU Cache Locality | CLO5 | CLO5 | `Analyze` | **Hard** | Session 31 & 38 | Contiguous 1D Flattened Matrix vs Pointer-to-Pointer & Cache Locality |
| **PRF_EQ25** | Phân tích bẫy lỗi thiếu ký tự kết thúc '\0' của strncpy() và thiết kế hàm ghép chuỗi an toàn với snprintf() | CLO8 | CLO8 | `Analyze` | **Hard** | Session 47-50 | String Termination Traps: strncpy Non-Null-Termination & snprintf Truncation Detection |
| **PRF_EQ26** | Kỹ thuật cập nhật trực tiếp bản ghi Struct tại chỗ (In-Place Update) trên tệp nhị phân bằng fseek() và ftell() | CLO9 | CLO9 | `Analyze` | **Hard** | Session 56-57 | Random Access Binary File I/O: In-Place Record Modification with fseek & rb+ |

---

## PHẦN 3: CHI TIẾT 36 CÂU HỎI KHO LUYỆN TẬP & THI THỬ (`practice_questions`)

> Kho câu hỏi này được lưu trữ độc lập trong bảng `practice_questions`, phục vụ hai luồng nghiệp vụ:
> 1. **MF-01:** Luyện tập tương tác tự do (chế độ On-Demand từng câu hoặc Progressive 3–10 câu tăng dần từ Dễ đến Khó).
> 2. **MF-02:** Thi thử tính giờ có hạn ngạch quota theo ngày, tự động sinh điểm số và bảng Scorecard phân tích theo đúng 9 CLOs.

### 1. [PRF_PQ01] Cấu trúc cơ bản của chương trình C và vai trò của hàm main

- **Mã định danh (Question Code):** `PRF_PQ01`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO1** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000461`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 1-3 — *C Basic Syntax & Program Structure*
- **Mục tiêu đánh giá:** Đánh giá khả năng hiểu cấu trúc khung chương trình C và vai trò điểm vào thực thi duy nhất của hàm main.

#### ❓ Câu hỏi vấn đáp:
> "Hãy trình bày các thành phần bắt buộc trong cấu trúc của một chương trình C tiêu chuẩn. Tại sao hàm main() lại đóng vai trò tối quan trọng và ý nghĩa của câu lệnh return 0 ở cuối hàm main là gì?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu chương trình C có nhiều hàm tự định nghĩa thì hệ điều hành sẽ bắt đầu thực thi từ hàm nào đầu tiên?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Một chương trình C tiêu chuẩn bao gồm các thành phần cốt lõi:
1. Các chỉ thị tiền xử lý (Preprocessor Directives): Bắt đầu bằng dấu `#` như `#include <stdio.h>` để chèn nội dung các tệp tiêu đề (header files) chứa khai báo thư viện chuẩn.
2. Khai báo nguyên mẫu hàm (Function Prototypes) hoặc định nghĩa hằng số/macro bằng `#define`.
3. Hàm khởi nhập thực thi `int main()`: Là điểm vào (Entry Point) duy nhất và bắt buộc của mọi chương trình C độc lập. Khi chương trình được nạp vào bộ nhớ RAM, hệ điều hành sẽ trao quyền điều khiển và bắt đầu thực thi luồng lệnh đầu tiên từ hàm `main()`.
4. Khối lệnh thực thi nằm trong cặp ngoặc nhọn `{}` chứa các câu lệnh khai báo biến, nhập xuất và xử lý logic.
5. Câu lệnh `return 0;`: Báo hiệu cho hệ điều hành biết tiến trình chương trình đã thực thi thành công và kết thúc an toàn mà không phát sinh lỗi ngoại lệ (mã trạng thái EXIT_SUCCESS). Nếu trả về giá trị khác 0, hệ điều hành sẽ hiểu chương trình đã gặp lỗi.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Trình bày các khối thành phần chương trình C** | **3.0đ** | Nêu đủ các phần: chỉ thị tiền xử lý, nguyên mẫu hàm, hàm main, khối lệnh thực thi. |
| **Giải thích vai trò Entry Point của hàm main** | **4.0đ** | Phân tích rõ hàm main là điểm vào thực thi duy nhất được hệ điều hành kích hoạt khi nạp tiến trình. |
| **Ý nghĩa mã trả về của return 0** | **3.0đ** | Giải thích chính xác mã trạng thái 0 báo hiệu thực thi thành công cho hệ điều hành quản trị tiến trình. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 2. [PRF_PQ02] Quy trình biên dịch mã nguồn C qua 4 giai đoạn

- **Mã định danh (Question Code):** `PRF_PQ02`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO1** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000461`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 3 — *C Compilation Pipeline*
- **Mục tiêu đánh giá:** Kiểm tra hiểu biết về 4 giai đoạn chuyển hóa từ mã nguồn .c sang tệp thực thi nhị phân .exe.

#### ❓ Câu hỏi vấn đáp:
> "Từ một tệp mã nguồn C (.c) cho đến khi tạo ra tệp thực thi (.exe), trình biên dịch (như GCC) trải qua 4 giai đoạn nào? Nêu nhiệm vụ tóm tắt của từng giai đoạn."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Ở giai đoạn nào các chú thích (comments) trong mã nguồn bị loại bỏ?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Quy trình biên dịch chương trình C trải qua 4 giai đoạn tuần tự:
1. Tiền xử lý (Preprocessing): Bộ tiền xử lý (Preprocessor) đọc mã nguồn `.c`, xử lý các chỉ thị bắt đầu bằng `#` (như chèn mã từ header file qua `#include`, thay thế macro qua `#define`), loại bỏ toàn bộ các khối chú thích (comments `//` và `/* */`), sinh ra tệp mã trung gian đã mở rộng (thường có phần mở rộng `.i`).
2. Biên dịch (Compilation): Trình biên dịch phân tích từ vựng (Lexical), phân tích cú pháp (Syntax) và ngữ nghĩa (Semantic), sau đó dịch mã C đã tiền xử lý thành mã hợp ngữ Assembly tương ứng với kiến trúc CPU mục tiêu (sinh ra tệp `.s`).
3. Hợp dịch (Assembly): Trình hợp dịch (Assembler) chuyển đổi các lệnh hợp ngữ Assembly thành mã máy nhị phân thô (Machine Code), đóng gói thành tệp đối tượng (Object File `.o` hoặc `.obj`).
4. Liên kết (Linking): Trình liên kết (Linker) kết nối các tệp đối tượng (`.o`) lại với nhau và liên kết với mã máy của các thư viện chuẩn (như `libc`), giải quyết các tham chiếu hàm ngoài (ví dụ tìm định nghĩa hàm `printf`), tạo ra tệp thực thi nhị phân hoàn chỉnh cuối cùng (`.exe` trên Windows hoặc ELF executable trên Linux).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Liệt kê đúng tên 4 giai đoạn tuần tự** | **3.0đ** | Nêu chính xác thứ tự: Preprocessing -> Compilation -> Assembly -> Linking. |
| **Nhiệm vụ của Preprocessing & Compilation** | **4.0đ** | Mô tả rõ xử lý header/macro/comment ở tiền xử lý và dịch cú pháp C sang hợp ngữ Assembly. |
| **Nhiệm vụ của Assembly & Linking** | **3.0đ** | Giải thích đúng vai trò chuyển hợp ngữ sang mã máy object và linker nối thư viện tạo file thực thi. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 3. [PRF_PQ03] Kiểu dữ liệu nguyên thủy, dải giá trị và kích thước bộ nhớ trong C

- **Mã định danh (Question Code):** `PRF_PQ03`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000462`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 4-5 — *Data Types & Memory Allocation*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức về các kiểu dữ liệu cơ bản, dung lượng ô nhớ và định từ bổ nghĩa trong C.

#### ❓ Câu hỏi vấn đáp:
> "Hãy liệt kê các kiểu dữ liệu nguyên thủy cơ bản trong ngôn ngữ C (char, int, float, double). Trình bày kích thước bộ nhớ thông thường và dải giá trị của kiểu int có dấu (signed) và không dấu (unsigned) trên hệ thống 32-bit/64-bit."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Toán tử nào trong C cho phép xác định chính xác kích thước byte của một kiểu dữ liệu hoặc một biến lúc biên dịch?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Các kiểu dữ liệu nguyên thủy cơ bản trong ngôn ngữ C gồm:
1. `char`: Kích thước 1 byte (8 bits), dải giá trị từ -128 đến 127 (signed) hoặc 0 đến 255 (unsigned), dùng để lưu trữ ký tự theo bảng mã ASCII.
2. `int`: Kích thước 4 bytes (32 bits) trên hầu hết kiến trúc hiện đại:
   - `signed int`: Dải giá trị từ $-2^{31}$ đến $2^{31} - 1$ (khoảng -2,147,483,648 đến 2,147,483,647).
   - `unsigned int`: Dải giá trị từ $0$ đến $2^{32} - 1$ (từ 0 đến 4,294,967,295).
3. `float`: Kích thước 4 bytes (32 bits), biểu diễn số thực dấu chấm động độ chính xác đơn (Single Precision) theo chuẩn IEEE 754, độ chính xác khoảng 6-7 chữ số thập phân.
4. `double`: Kích thước 8 bytes (64 bits), biểu diễn số thực dấu chấm động độ chính xác kép (Double Precision), độ chính xác khoảng 15-17 chữ số thập phân.
5. Toán tử `sizeof` là toán tử tiền biên dịch cho phép xác định chính xác dung lượng byte của kiểu dữ liệu hoặc biến trên kiến trúc phần cứng cụ thể (ví dụ `sizeof(int)` trả về 4).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Liệt kê đầy đủ 4 kiểu dữ liệu nguyên thủy** | **3.0đ** | Nêu rõ char, int, float, double và mục đích biểu diễn của từng kiểu. |
| **Kích thước bộ nhớ và dải giá trị của int** | **4.0đ** | Trình bày chính xác dung lượng 4 bytes, dải giá trị signed [-2^31, 2^31-1] và unsigned [0, 2^32-1]. |
| **Sử dụng toán tử sizeof** | **3.0đ** | Nêu đúng vai trò và cú pháp của toán tử sizeof trong việc kiểm tra kích thước bộ nhớ. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 4. [PRF_PQ04] Phân biệt hằng số #define (Macro) và từ khóa const trong C

- **Mã định danh (Question Code):** `PRF_PQ04`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000462`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 4 — *Constants & Preprocessor vs Compiler*
- **Mục tiêu đánh giá:** Đánh giá khả năng phân biệt cơ chế tiền xử lý thay thế chuỗi và kiểm tra kiểu của trình biên dịch.

#### ❓ Câu hỏi vấn đáp:
> "Trong ngôn ngữ C, có hai cách phổ biến để định nghĩa hằng số: dùng chỉ thị tiền xử lý #define và dùng từ khóa const. Hãy phân biệt sự khác nhau giữa hai cách này về cơ chế hoạt động, kiểm tra kiểu dữ liệu và cấp phát bộ nhớ."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Cách nào trong hai cách trên giúp trình biên dịch phát hiện lỗi ép sai kiểu dữ liệu tốt hơn?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Sự khác biệt căn bản giữa `#define` và `const`:
1. Cơ chế hoạt động:
   - `#define NAME VALUE`: Là chỉ thị tiền xử lý. Bộ tiền xử lý (Preprocessor) chỉ thực hiện thay thế văn bản thô (text substitution) trước khi mã được biên dịch. Trình biên dịch không biết đến sự tồn tại của tên định danh macro này.
   - `const type NAME = VALUE;`: Là một biến có định từ bất biến được quản lý bởi trình biên dịch (Compiler). Giá trị được gán một lần khi khởi tạo và bị khóa không cho phép sửa đổi.
2. Kiểm tra kiểu dữ liệu (Type Safety):
   - `#define`: Không có kiểu dữ liệu, không có kiểm tra an toàn kiểu (type checking), dễ phát sinh lỗi logic khó debug khi sử dụng trong các biểu thức phức tạp.
   - `const`: Có kiểu dữ liệu tường minh, trình biên dịch thực hiện kiểm tra kiểu nghiêm ngặt và cảnh báo nếu có vi phạm.
3. Cấp phát bộ nhớ và Phạm vi (Scope):
   - `#define`: Không chiếm ô nhớ trong bảng biểu tượng biến, có phạm vi từ vị trí định nghĩa đến hết tệp (hoặc đến khi gặp `#undef`).
   - `const`: Biến được cấp phát không gian trong bộ nhớ (thường trong phân vùng Read-only Data Segment), tuân thủ phạm vi khối lệnh (Block Scope) hoặc phạm vi tệp.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **So sánh cơ chế tiền xử lý vs trình biên dịch** | **4.0đ** | Phân tích rõ thay thế chuỗi văn bản thô (#define) so với biến bất biến của compiler (const). |
| **Kiểm tra an toàn kiểu dữ liệu (Type Safety)** | **3.0đ** | Chỉ ra const có kiểu dữ liệu rõ ràng giúp kiểm tra lỗi, trong khi #define không kiểm tra kiểu. |
| **Phạm vi và cấp phát bộ nhớ** | **3.0đ** | Giải thích đúng phạm vi scope và cơ chế chiếm bộ nhớ ô nhớ của biến const so với macro. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 5. [PRF_PQ05] Phép chia nguyên và kỹ thuật ép kiểu tường minh trong C

- **Mã định danh (Question Code):** `PRF_PQ05`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000462`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 7-8 — *Expressions & Type Casting*
- **Mục tiêu đánh giá:** Kiểm tra sự hiểu biết về phép chia số nguyên và kỹ thuật ép kiểu (type casting) trong biểu thức số học.

#### ❓ Câu hỏi vấn đáp:
> "Điều gì xảy ra khi thực hiện biểu thức `double res = 5 / 2;` trong C? Kết quả biến res nhận được là bao nhiêu và tại sao? Làm thế nào để thu được kết quả chính xác 2.5 bằng kỹ thuật ép kiểu?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu biến a và b đều là int, ta viết `(double)(a / b)` thì có ra kết quả số thực chính xác không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Kết quả biểu thức `double res = 5 / 2;`:
Biến `res` nhận giá trị là `2.0` (chứ không phải 2.5).
Nguyên nhân: Trong C, khi cả hai toán hạng `5` và `2` đều là kiểu số nguyên (`int`), toán tử chia `/` sẽ thực hiện phép chia lấy phần nguyên (Integer Division). Phép toán `5 / 2` tính ra kết quả nguyên là `2`, phần dư `1` bị cắt cụt. Sau đó, giá trị `2` mới được thăng hạng ngầm định (implicit conversion) thành số thực `2.0` để gán vào biến `res`.

2. Cách khắc phục để thu được kết quả 2.5:
Cần làm cho ít nhất một trong hai toán hạng trở thành kiểu số thực:
- Cách 1 (Sử dụng hằng số thực): `double res = 5.0 / 2;` hoặc `double res = 5 / 2.0;`
- Cách 2 (Ép kiểu tường minh - Explicit Type Casting): Dùng cú pháp `(double)` đặt trước một trong hai toán hạng:
  `double res = (double)5 / 2;` hoặc với hai biến nguyên `a, b`: `double res = (double)a / b;`
Lưu ý: Nếu viết `(double)(a / b)` thì phép chia nguyên `a / b` đã diễn ra xong trước rồi mới ép kiểu kết quả, nên vẫn bị mất phần thập phân.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích bản chất phép chia nguyên** | **4.0đ** | Chỉ rõ toán tử / giữa 2 số nguyên sẽ cắt cụt phần thập phân và cho ra 2.0 khi gán vào double. |
| **Cú pháp ép kiểu tường minh chuẩn xác** | **4.0đ** | Đưa ra cú pháp đúng (double)a / b hoặc dùng hằng số thực 5.0 / 2 để có kết quả 2.5. |
| **Phân tích bẫy lỗi (double)(a / b)** | **2.0đ** | Cảnh báo chính xác thứ tự thực hiện dấu ngoặc khiến phép chia nguyên hoàn tất trước khi ép kiểu. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 6. [PRF_PQ06] Toán tử tăng/giảm tiền tố (++x) và hậu tố (x++) trong C

- **Mã định danh (Question Code):** `PRF_PQ06`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000462`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 7 — *Operators & Pre/Post-increment*
- **Mục tiêu đánh giá:** Đánh giá khả năng phân biệt thời điểm cập nhật giá trị giữa toán tử tiền tố và hậu tố.

#### ❓ Câu hỏi vấn đáp:
> "Phân biệt sự khác nhau giữa toán tử tăng tiền tố (++x) và hậu tố (x++) trong ngôn ngữ C. Cho ví dụ đoạn code nhỏ thể hiện sự khác biệt về giá trị trả về trong một biểu thức gán."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu viết `y = x++;` với x = 5 ban đầu, thì sau câu lệnh x và y có giá trị là bao nhiêu?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Toán tử `++` dùng để tăng giá trị của biến lên 1 đơn vị, nhưng thời điểm sinh giá trị trong biểu thức khác nhau hoàn toàn:
1. Tiền tố (`++x` - Pre-increment):
   - Giá trị của biến `x` được tăng lên 1 TRƯỚC TIÊN.
   - Sau đó giá trị mới này được sử dụng để đánh giá biểu thức xung quanh.
   - Ví dụ: `int x = 5; int y = ++x;` -> `x` tăng lên 6, sau đó `y` nhận giá trị 6. Kết quả: `x = 6, y = 6`.

2. Hậu tố (`x++` - Post-increment):
   - Giá trị HIỆN TẠI của `x` được lấy ra để tham gia tính toán hoặc gán trong biểu thức trước.
   - SAU KHI biểu thức hoàn tất (tại điểm sequence point), giá trị của `x` mới được tăng thêm 1.
   - Ví dụ: `int x = 5; int y = x++;` -> `y` nhận giá trị cũ của `x` là 5, sau đó `x` mới tăng lên 6. Kết quả: `x = 6, y = 5`.

Quy tắc tương tự áp dụng cho toán tử giảm tiền tố `--x` và hậu tố `x--`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích cơ chế toán tử tiền tố ++x** | **3.5đ** | Trình bày rõ biến tăng trước rồi mới trả về giá trị mới cho biểu thức. |
| **Giải thích cơ chế toán tử hậu tố x++** | **3.5đ** | Trình bày rõ trả về giá trị cũ cho biểu thức trước, sau đó biến mới được tăng. |
| **Ví dụ minh họa và giá trị chính xác** | **3.0đ** | Đưa ra ví dụ gán cụ thể và xác định chuẩn xác giá trị của cả hai biến sau lệnh. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 7. [PRF_PQ07] Cấu trúc điều khiển rẽ nhánh if-else và toán tử điều kiện 3 ngôi

- **Mã định danh (Question Code):** `PRF_PQ07`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO3** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000463`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 9 — *Control Flow & Selection Constructs*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng sử dụng cấu trúc rẽ nhánh if-else và toán tử điều kiện bậc 3 (ternary operator).

#### ❓ Câu hỏi vấn đáp:
> "Hãy trình bày cú pháp và luồng hoạt động của cấu trúc rẽ nhánh if-else trong C. Khi nào ta nên thay thế cấu trúc if-else đơn giản bằng toán tử điều kiện 3 ngôi `condition ? expr1 : expr2`?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Toán tử 3 ngôi có trả về giá trị biểu thức không, và có thể dùng trực tiếp trong lệnh printf không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cấu trúc rẽ nhánh `if-else`:
- Cú pháp:
  ```c
  if (condition) {
      // Khối lệnh thực thi khi condition đúng (khác 0)
  } else {
      // Khối lệnh thực thi khi condition sai (bằng 0)
  }
  ```
- Hoạt động: Trình biên dịch đánh giá biểu thức điều kiện `condition`. Trong C, mọi giá trị khác 0 được coi là đúng (True), giá trị bằng 0 được coi là sai (False). Nếu đúng, khối lệnh sau `if` chạy; nếu sai, nhảy sang khối `else`.

2. Toán tử điều kiện 3 ngôi (Ternary Operator):
- Cú pháp: `condition ? expr1 : expr2;`
- Bản chất: Đây là một biểu thức (Expression) trả về giá trị: Nếu `condition` đúng thì trả về giá trị của `expr1`, ngược lại trả về giá trị của `expr2`.
- Trường hợp nên dùng: Thích hợp cho việc gán giá trị nhanh gọn hoặc in ra màn hình dựa trên điều kiện đơn giản (ví dụ: `int max = (a > b) ? a : b;` hoặc `printf("%s", score >= 5 ? "Pass" : "Fail");`).
- Không nên lạm dụng khi các nhánh xử lý nhiều câu lệnh phức tạp hoặc lồng nhau nhiều cấp vì làm giảm khả năng đọc hiểu mã nguồn.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Cú pháp và nguyên lý logic của if-else** | **3.5đ** | Mô tả chuẩn cú pháp, quy ước đúng/sai (khác 0 là true, 0 là false) trong C. |
| **Bản chất toán tử điều kiện 3 ngôi** | **3.5đ** | Phân tích rõ toán tử 3 ngôi là biểu thức trả về giá trị dựa trên điều kiện. |
| **Ứng dụng thực tế và khuyến nghị phong cách** | **3.0đ** | Nêu ví dụ tìm min/max hoặc in chuỗi và khuyến nghị tránh lồng ghép phức tạp. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 8. [PRF_PQ08] Cấu trúc lựa chọn switch-case và hiện tượng fall-through khi thiếu break

- **Mã định danh (Question Code):** `PRF_PQ08`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO3** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000463`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 9 — *Switch-Case & Fall-through Behavior*
- **Mục tiêu đánh giá:** Đánh giá sự am hiểu về cấu trúc switch-case, vai trò của lệnh break và hiện tượng fall-through.

#### ❓ Câu hỏi vấn đáp:
> "Cấu trúc switch-case hoạt động như thế nào trong C? Lệnh break có vai trò gì trong mỗi nhánh case và điều gì sẽ xảy ra nếu lập trình viên quên viết lệnh break (hiện tượng Fall-through)?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Biểu thức trong lệnh switch có thể là một số thực kiểu float hay chuỗi ký tự được không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cách hoạt động của `switch-case`:
Cấu trúc `switch(expression)` đánh giá một biểu thức nguyên (kiểu `int`, `char`, `enum` - tuyệt đối KHÔNG chấp nhận `float`, `double` hay chuỗi ký tự). Giá trị thu được sẽ được so sánh lần lượt với các hằng số nguyên tại các nhãn `case constant:`. Nếu khớp, luồng thực thi sẽ nhảy thẳng đến nhãn `case` đó. Nếu không khớp nhãn nào, nhánh `default` (nếu có) sẽ được thực thi.

2. Vai trò của lệnh `break`:
Lệnh `break` đóng vai trò kết thúc khối lệnh của nhánh hiện tại và thoát ngay ra khỏi cấu trúc `switch-case`, ngăn không cho chương trình chạy tiếp xuống các lệnh phía dưới.

3. Hiện tượng Fall-through khi thiếu `break`:
Nếu thiếu lệnh `break` ở cuối một nhánh `case`, trình biên dịch C sẽ không tự động thoát ra mà tiếp tục thực thi tuần tự tất cả các câu lệnh của các `case` bên dưới bất kể điều kiện của các `case` sau có khớp hay không. Hiện tượng này gọi là Fall-through.
Ứng dụng có chủ đích của Fall-through: Được tận dụng khi nhiều giá trị case dùng chung một đoạn logic (ví dụ nhóm các tháng có cùng 31 ngày: `case 1: case 3: case 5: days = 31; break;`).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Nguyên lý hoạt động và ràng buộc kiểu của switch** | **3.5đ** | Trình bày đúng cách so sánh nhãn hằng số nguyên và nhấn mạnh cấm dùng float/string. |
| **Vai trò của lệnh break trong cấu trúc switch** | **3.0đ** | Nêu rõ break giúp ngắt luồng và thoát ra khỏi thân switch ngay lập tức. |
| **Phân tích hiện tượng Fall-through và ứng dụng** | **3.5đ** | Giải thích đúng lỗi trôi lệnh khi thiếu break và trường hợp chủ động ứng dụng. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 9. [PRF_PQ09] So sánh vòng lặp while và do-while về số lần lặp tối thiểu

- **Mã định danh (Question Code):** `PRF_PQ09`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO3** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000463`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 10 — *Loops: While vs Do-While*
- **Mục tiêu đánh giá:** Kiểm tra khả năng phân biệt vòng lặp kiểm tra trước và vòng lặp kiểm tra sau.

#### ❓ Câu hỏi vấn đáp:
> "Hãy so sánh sự khác nhau căn bản giữa vòng lặp while và vòng lặp do-while trong ngôn ngữ C về thời điểm kiểm tra điều kiện và số lần thực thi thân vòng lặp tối thiểu. Cho ví dụ thực tế nên dùng do-while."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu điều kiện của vòng lặp sai ngay từ đầu, vòng lặp nào sẽ không chạy lần nào, vòng lặp nào vẫn chạy 1 lần?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
So sánh sự khác nhau giữa vòng lặp `while` và `do-while`:
1. Thời điểm kiểm tra điều kiện:
   - Vòng lặp `while (condition)`: Kiểm tra điều kiện TRƯỚC khi thực hiện thân vòng lặp (Pre-test Loop). Nếu điều kiện đúng, khối lệnh thân mới được chạy.
   - Vòng lặp `do { ... } while (condition);`: Thực hiện thân vòng lặp TRƯỚC, sau đó mới kiểm tra điều kiện ở cuối (Post-test Loop). Lưu ý cú pháp `do-while` bắt buộc phải có dấu chấm phẩy `;` sau `while(...)`.

2. Số lần thực thi tối thiểu:
   - `while`: Số lần lặp tối thiểu là **0 lần** (nếu điều kiện ban đầu sai thì thân vòng lặp bị bỏ qua hoàn toàn).
   - `do-while`: Số lần lặp tối thiểu luôn là **1 lần** (khối lệnh luôn chạy ít nhất một lần trước khi điều kiện được kiểm tra lần đầu tiên).

3. Ví dụ thực tế thích hợp cho `do-while`:
Các bài toán kiểm tra tính hợp lệ dữ liệu nhập từ bàn phím (Input Validation) hoặc hiển thị Menu lựa chọn: Chương trình phải in ra lời nhắc và đọc dữ liệu từ người dùng ít nhất 1 lần, sau đó nếu dữ liệu không hợp lệ mới bắt nhập lại:
```c
int n;
do {
    printf("Nhap so nguyen duong n (> 0): ");
    scanf("%d", &n);
} while (n <= 0);
```

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân biệt thời điểm kiểm tra điều kiện trước và sau** | **3.5đ** | Nêu rõ while kiểm tra trước khi chạy, do-while chạy trước rồi mới kiểm tra sau. |
| **Số lần thực thi tối thiểu (0 lần vs 1 lần)** | **3.5đ** | Khẳng định chuẩn xác while tối thiểu 0 lần còn do-while luôn tối thiểu 1 lần. |
| **Ví dụ ứng dụng thực tế nhập dữ liệu hợp lệ** | **3.0đ** | Cung cấp ví dụ code do-while kiểm tra nhập số dương hoặc hiển thị menu chính xác. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 10. [PRF_PQ10] Lệnh điều khiển vòng lặp: Phân biệt break và continue

- **Mã định danh (Question Code):** `PRF_PQ10`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO3** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000463`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 10 — *Loop Control: Break vs Continue*
- **Mục tiêu đánh giá:** Đánh giá mức độ nắm vững hành vi của lệnh break và continue trong vòng lặp C.

#### ❓ Câu hỏi vấn đáp:
> "Trong thân vòng lặp (for, while, do-while), lệnh break và continue khác nhau như thế nào? Mô tả chính xác luồng điều khiển của chương trình khi gặp mỗi lệnh này."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trong vòng lặp lồng nhau (nested loops), lệnh break ở vòng lặp trong cùng có làm thoát cả vòng lặp ngoài không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Sự khác biệt giữa lệnh `break` và `continue`:
1. Lệnh `break`:
   - Hành vi: Lập tức chấm dứt hoàn toàn vòng lặp gần nhất bao bọc nó.
   - Luồng điều khiển: Bỏ qua toàn bộ các câu lệnh còn lại trong thân vòng lặp hiện tại và chuyển luồng thực thi ra lệnh đầu tiên ngay sau khối vòng lặp đó.
   - Ứng dụng: Dùng khi đã tìm thấy phần tử cần tìm trong thuật toán tìm kiếm tuần tự và muốn dừng duyệt sớm để tối ưu hiệu năng.

2. Lệnh `continue`:
   - Hành vi: Bỏ qua phần còn lại của thân vòng lặp trong LẦN LẶP HIỆN TẠI.
   - Luồng điều khiển: Nhảy ngay đến bước lặp kế tiếp:
     + Trong vòng lặp `for`: Nhảy đến biểu thức cập nhật bước nhảy (`step`) rồi kiểm tra lại điều kiện.
     + Trong vòng lặp `while` và `do-while`: Nhảy thẳng đến bước kiểm tra biểu thức điều kiện lặp.
   - Ứng dụng: Dùng khi muốn bỏ qua các trường hợp ngoại lệ hoặc dữ liệu không hợp lệ (ví dụ bỏ qua các số chẵn khi chỉ muốn xử lý số lẻ).

Lưu ý: Trong vòng lặp lồng nhau, cả `break` và `continue` chỉ tác động lên vòng lặp trực tiếp bao quanh nó, không thoát ra khỏi các vòng lặp bên ngoài.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Hành vi và luồng điều khiển của lệnh break** | **3.5đ** | Trình bày rõ thoát hẳn vòng lặp gần nhất và nhảy tới câu lệnh kế sau khối lặp. |
| **Hành vi và luồng điều khiển của lệnh continue** | **3.5đ** | Trình bày rõ bỏ qua bước hiện tại và chuyển sang bước lặp/điều kiện kế tiếp. |
| **Phạm vi ảnh hưởng trong vòng lặp lồng nhau** | **3.0đ** | Nêu chính xác break/continue chỉ tác động lên vòng lặp trong cùng bao bọc nó. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 11. [PRF_PQ11] Bản chất lưu trữ ô nhớ của mảng một chiều và đánh chỉ số trong C

- **Mã định danh (Question Code):** `PRF_PQ11`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO7** (`00000000-0000-0000-0000-000000000467`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 37 — *1D Arrays & Memory Layout*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức nền tảng về cấu trúc ô nhớ liên tục của mảng một chiều và đánh chỉ số 0-based.

#### ❓ Câu hỏi vấn đáp:
> "Mảng một chiều (1D Array) được lưu trữ trong bộ nhớ RAM như thế nào? Dải chỉ số hợp lệ của mảng N phần tử là gì và điều gì xảy ra nếu chương trình truy cập vào chỉ số arr[N]?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trình biên dịch C có tự động cảnh báo hoặc chặn lỗi truy cập vượt chỉ số mảng (out-of-bounds) lúc đang chạy không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Bản chất lưu trữ ô nhớ của mảng một chiều:
Mảng một chiều trong C là một tập hợp các phần tử có cùng kiểu dữ liệu, được cấp phát trên một khối vùng nhớ liên tục (Contiguous Memory Blocks).
Địa chỉ của phần tử thứ `i` được tính toán trực tiếp theo công thức:
`Địa_chỉ(arr[i]) = Địa_chỉ_cơ_sở(arr) + i * sizeof(kiểu_dữ_liệu)`
Do đó, việc truy xuất bất kỳ phần tử nào thông qua chỉ số đều có độ phức tạp thời gian hằng số $O(1)$.

2. Dải chỉ số hợp lệ:
C sử dụng hệ thống đánh chỉ số bắt đầu từ 0 (Zero-based indexing). Với mảng có $N$ phần tử, dải chỉ số hợp lệ là từ `0` đến `N - 1`.

3. Hậu quả khi truy cập vượt chỉ số `arr[N]`:
Phần tử `arr[N]` nằm ngoài vùng nhớ hợp lệ đã cấp phát cho mảng (lỗi Out-of-bounds).
Ngôn ngữ C KHÔNG thực hiện kiểm tra ranh giới mảng lúc chạy (No Runtime Bounds Checking). Truy cập `arr[N]` sẽ đọc hoặc ghi đè lên ô nhớ lân cận trên Stack Frame, gây ra hiện tượng hành vi không xác định (Undefined Behavior): làm sai lệch dữ liệu của biến khác, hỏng con trỏ trả về của hàm, hoặc dẫn đến lỗi vi phạm phân đoạn bộ nhớ (Segmentation Fault / Access Violation) làm chương trình bị sập.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Đặc tính lưu trữ ô nhớ liên tục và công thức địa chỉ** | **3.5đ** | Mô tả chuẩn khối nhớ liên tiếp và công thức tính địa chỉ phần tử O(1). |
| **Xác định chính xác dải chỉ số từ 0 đến N-1** | **3.0đ** | Nêu rõ nguyên tắc đánh chỉ số zero-based và dải hợp lệ 0 .. N-1. |
| **Phân tích lỗi Undefined Behavior khi truy cập arr[N]** | **3.5đ** | Giải thích sâu C không check bounds, nguy cơ ghi đè ô nhớ lân cận và gây crash. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 12. [PRF_PQ12] Bản chất chuỗi ký tự C và vai trò của ký tự null terminator '\0'

- **Mã định danh (Question Code):** `PRF_PQ12`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO8** | **DB 3NF CLO8** (`00000000-0000-0000-0000-000000000468`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 46 — *C Strings & Null Terminator*
- **Mục tiêu đánh giá:** Đánh giá sự hiểu biết về bản chất mảng ký tự và vai trò kết thúc chuỗi của ký tự null trong C.

#### ❓ Câu hỏi vấn đáp:
> "Trong ngôn ngữ C, chuỗi ký tự (String) được định nghĩa và lưu trữ như thế nào? Ký tự null terminator ('\0') có vai trò gì và điều gì xảy ra nếu một mảng ký tự không có ký tự này khi gọi hàm printf hay strlen?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Ký tự '\0' có mã ASCII là bao nhiêu và nó có khác với ký tự số '0' không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Bản chất chuỗi ký tự trong C:
Trong C, không có kiểu dữ liệu nguyên thủy `string` tích hợp sẵn. Chuỗi ký tự thực chất là một mảng một chiều các ký tự kiểu `char`, được kết thúc bằng một ký tự đặc biệt gọi là ký tự null terminator (`'\0'`).
Ví dụ chuỗi `"FPT"` gồm 3 ký tự hiển thị nhưng cần mảng tối thiểu 4 bytes để lưu trữ: `{'F', 'P', 'T', '\0'}`.

2. Vai trò của ký tự null terminator (`'\0'`):
Ký tự `'\0'` có giá trị mã ASCII bằng `0` (khác hoàn toàn với ký tự chữ số `'0'` có mã ASCII là 48).
Vì mảng trong C không lưu trữ thông tin về độ dài chuỗi, ký tự `'\0'` đóng vai trò là cột mốc báo hiệu điểm dừng cho mọi hàm xử lý chuỗi trong thư viện chuẩn (như `strlen`, `strcpy`, `printf("%s")`).

3. Hậu quả khi thiếu ký tự `'\0'`:
Nếu mảng ký tự không có ký tự null ở cuối:
- Hàm `strlen(str)` sẽ tiếp tục đếm qua các ô nhớ lân cận trong RAM cho đến khi tình cờ gặp một byte có giá trị 0x00, trả về độ dài sai lệch ngẫu nhiên.
- Hàm `printf("%s", str)` sẽ in toàn bộ ký tự trong mảng và tiếp tục in các byte rác kế tiếp trên bộ nhớ màn hình, có thể dẫn đến lỗi Segmentation Fault khi đọc chạm vùng nhớ bị cấm.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa chuỗi C là mảng char kết thúc bằng '\0'** | **3.5đ** | Nêu rõ C không có kiểu string riêng, chuỗi là mảng ký tự kèm null terminator. |
| **Phân tích vai trò cột mốc dừng và mã ASCII của '\0'** | **3.5đ** | Giải thích ASCII = 0, phân biệt với ký tự '0', là điểm dừng cho hàm thư viện. |
| **Hậu quả khi thiếu '\0' đối với printf và strlen** | **3.0đ** | Mô tả chính xác in ký tự rác, tính sai độ dài chuỗi và nguy cơ tràn bộ nhớ. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 13. [PRF_PQ13] Cơ chế đánh giá đoản mạch (Short-circuit Evaluation) của toán tử logic && và ||

- **Mã định danh (Question Code):** `PRF_PQ13`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000462`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 7 — *Logical Operators & Short-circuit Evaluation*
- **Mục tiêu đánh giá:** Đánh giá khả năng hiểu cơ chế tối ưu hóa đánh giá biểu thức logic đoản mạch và các tác dụng phụ (side-effects).

#### ❓ Câu hỏi vấn đáp:
> "Cơ chế đánh giá đoản mạch (Short-circuit Evaluation) của hai toán tử logic AND (&&) và OR (||) trong C hoạt động như thế nào? Phân tích ví dụ biểu thức `(a != 0) && (b / a > 2)` để chứng minh tầm quan trọng của cơ chế này trong việc phòng ngừa lỗi chia cho 0."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu vế đầu của toán tử || trả về true (khác 0), thì biểu thức ở vế thứ hai có được thực thi hay không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cơ chế đánh giá đoản mạch (Short-circuit Evaluation):
Trong C, các toán tử logic `&&` và `||` đánh giá các toán hạng từ trái sang phải và dừng ngay khi kết quả cuối cùng đã được xác định chắc chắn:
- Với toán tử `&&` (Logic AND): Nếu toán hạng bên trái có giá trị SAI (bằng 0), toàn bộ biểu thức chắc chắn là 0. Do đó, trình biên dịch sẽ KHÔNG đánh giá toán hạng bên phải nữa.
- Với toán tử `||` (Logic OR): Nếu toán hạng bên trái có giá trị ĐÚNG (khác 0), toàn bộ biểu thức chắc chắn là 1. Do đó, toán hạng bên phải sẽ bị BỎ QUA hoàn toàn.

2. Phân tích ví dụ `(a != 0) && (b / a > 2)`:
- Nếu $a = 0$: Vế trái `(a != 0)` cho kết quả là `0` (False). Do cơ chế đoản mạch, vế phải `(b / a > 2)` sẽ không bao giờ được thực thi. Nhờ đó, chương trình ngăn chặn triệt để lỗi chia cho 0 tại thời điểm chạy (Divide-by-zero Exception / Crash).
- Nếu không có cơ chế đoản mạch và cả 2 vế đều bị ép tính, phép toán `b / 0` sẽ làm chương trình bị sập ngay lập tức.

3. Lưu ý về Side-effects: Cần tránh đặt các biểu thức thay đổi giá trị biến (như `x++` hoặc gọi hàm) ở vế phải, vì chúng có thể không được chạy nếu vế trái đoản mạch.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Nguyên lý đoản mạch của toán tử && và ||** | **4.0đ** | Giải thích rõ ràng dừng đánh giá khi kết quả vế trái đã quyết định giá trị toàn biểu thức. |
| **Phân tích ví dụ phòng tránh chia cho 0** | **3.5đ** | Chứng minh khi a == 0 thì vế b / a không chạy, bảo vệ an toàn cho tiến trình. |
| **Cảnh báo tác dụng phụ (Side-effects)** | **2.5đ** | Chỉ ra rủi ro khi đặt phép tăng ++ hoặc gọi hàm ở vế phải của toán tử logic. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 14. [PRF_PQ14] Nguyên mẫu hàm (Function Prototype) và vai trò trong kiểm tra kiểu dữ liệu

- **Mã định danh (Question Code):** `PRF_PQ14`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000464`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 19 — *Modular Programming & Function Prototypes*
- **Mục tiêu đánh giá:** Kiểm tra hiểu biết về khai báo nguyên mẫu hàm (Forward Declaration) và kiểm tra kiểu lúc biên dịch.

#### ❓ Câu hỏi vấn đáp:
> "Nguyên mẫu hàm (Function Prototype) là gì? Tại sao trong C ta nên khai báo nguyên mẫu hàm trước hàm main() nếu phần định nghĩa chi tiết của hàm nằm ở phía dưới hàm main()?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu không có nguyên mẫu hàm, trình biên dịch C cổ điển (C89) sẽ giả định kiểu trả về mặc định của hàm là gì?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Khái niệm Nguyên mẫu hàm (Function Prototype):
Nguyên mẫu hàm (hay khai báo trước - Forward Declaration) là câu lệnh thông báo cho trình biên dịch biết tên hàm, danh sách các kiểu dữ liệu của tham số và kiểu dữ liệu trả về trước khi hàm đó được gọi hoặc được định nghĩa chi tiết:
`return_type function_name(type1 param1, type2 param2);`

2. Vai trò tối quan trọng của nguyên mẫu hàm:
- Trình biên dịch C hoạt động theo cơ chế quét từ trên xuống dưới (Single-pass parsing). Khi gặp một lời gọi hàm trong `main()`, nếu chưa có nguyên mẫu hàm trước đó:
  + Trình biên dịch hiện đại sẽ báo lỗi hoặc cảnh báo nghiêm trọng (implicit function declaration).
  + Trong chuẩn C89, trình biên dịch sẽ tự động giả định hàm trả về kiểu `int`, dẫn đến lỗi sai lệch kiểu nghiêm trọng nếu hàm thực tế trả về `double` hoặc `pointer`.
- Cho phép trình biên dịch kiểm tra tính hợp lệ về số lượng và kiểu dữ liệu của các đối số truyền vào tại thời điểm biên dịch (Compile-time Type Checking), tự động ép kiểu ngầm định nếu tương thích hoặc báo lỗi nếu không khớp.
- Hỗ trợ thiết kế mã nguồn module hóa: Giúp đặt khai báo hàm vào tệp tiêu đề `.h` để các tệp `.c` khác có thể liên kết sử dụng mà không cần biết chi tiết cài đặt bên trong.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa chính xác Function Prototype** | **3.0đ** | Nêu rõ nguyên mẫu gồm tên hàm, danh sách kiểu tham số và kiểu giá trị trả về. |
| **Giải thích cơ chế quét đơn và bẫy kiểu ngầm định** | **4.0đ** | Phân tích rõ cơ chế trình biên dịch quét từ trên xuống và lỗi giả định kiểu trả về int. |
| **Khả năng kiểm tra kiểu (Type Checking) và module hóa** | **3.0đ** | Chỉ ra lợi ích bắt lỗi lúc biên dịch và hỗ trợ tách file header .h tái sử dụng. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 15. [PRF_PQ15] Cơ chế truyền tham trị (Pass-by-value) và Stack Frame khi gọi hàm trong C

- **Mã định danh (Question Code):** `PRF_PQ15`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000464`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 20 — *Function Calls & Call Stack Frames*
- **Mục tiêu đánh giá:** Đánh giá khả năng hiểu sâu về mô hình Call Stack và cơ chế truyền tham trị trong bộ nhớ RAM.

#### ❓ Câu hỏi vấn đáp:
> "Trong ngôn ngữ C, cơ chế truyền tham số mặc định cho hàm là gì? Hãy mô tả những gì diễn ra trên Call Stack (Stack Frame) khi một hàm được gọi và giải thích tại sao thay đổi tham số bên trong hàm lại không làm thay đổi giá trị của biến ở ngoài hàm gọi."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi hàm con thực hiện lệnh return, điều gì xảy ra với Stack Frame của nó?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cơ chế truyền tham số mặc định:
Trong C, cơ chế truyền tham số mặc định duy nhất là Truyền theo giá trị (Pass-by-value).

2. Diễn biến trên Call Stack (Stack Frame):
- Khi một hàm được gọi (Function Invocation), hệ điều hành và CPU sẽ phân bổ một khung ngăn xếp mới gọi là Stack Frame (hoặc Activation Record) dành riêng cho hàm đó trên vùng nhớ Call Stack.
- Giá trị của các đối số thực tế (Actual Arguments) từ hàm gọi được sao chép (copy) vào các biến tham số hình thức (Formal Parameters) nằm trên Stack Frame của hàm con.
- Địa chỉ quay về (Return Address) và con trỏ khung (Base Pointer) cũng được lưu trữ để phục vụ việc quay trở lại sau khi hàm kết thúc.

3. Lý do biến ngoài không thay đổi:
Mọi thao tác gán hoặc chỉnh sửa giá trị bên trong hàm con chỉ tác động lên bản sao dữ liệu nằm trên Stack Frame riêng của chính nó. Khi hàm con gặp lệnh `return`, toàn bộ Stack Frame của hàm con bị thu hồi (pop off/deallocated).
Biến ban đầu ở hàm `main()` nằm trên một Stack Frame hoàn toàn độc lập và không hề bị tác động bởi bất kỳ phép tính nào của hàm con.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Khẳng định cơ chế Pass-by-value mặc định** | **3.0đ** | Nêu rõ C chỉ có truyền tham trị, sao chép bản sao dữ liệu khi gọi hàm. |
| **Mô tả cấu trúc và hoạt động của Stack Frame** | **4.0đ** | Vẽ/mô tả luồng đẩy khung ngăn xếp, lưu tham số, biến cục bộ và return address. |
| **Giải thích tính độc lập ô nhớ và giải phóng khi return** | **3.0đ** | Chỉ rõ vùng nhớ độc lập giữa hai stack frames và thu hồi bộ nhớ sau khi hàm kết thúc. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 16. [PRF_PQ16] Phân biệt biến cục bộ (Local), toàn cục (Global) và biến tĩnh cục bộ (static local)

- **Mã định danh (Question Code):** `PRF_PQ16`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000464`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 22 — *Variable Scope & Storage Duration*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức về phạm vi (Scope), thời gian sống (Lifetime) và từ khóa static trong C.

#### ❓ Câu hỏi vấn đáp:
> "Hãy phân biệt biến cục bộ (Local variable), biến toàn cục (Global variable) và biến tĩnh cục bộ (static local variable) về: Phạm vi truy cập (Scope), Vùng nhớ lưu trữ (Memory segment), và Thời gian sống (Storage duration)."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Biến static local được khởi tạo bao nhiêu lần trong suốt vòng đời của chương trình?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
So sánh 3 loại biến trong ngôn ngữ C:

1. Biến cục bộ (Local variable):
- Phạm vi: Khối lệnh (Block scope `{}`), chỉ truy cập được bên trong hàm hoặc khối lệnh khai báo.
- Vùng nhớ: Vùng nhớ Ngăn xếp (Stack Segment).
- Thời gian sống: Tự động (Automatic duration) - sinh ra khi luồng điều khiển vào khối lệnh và bị hủy ngay khi thoát khỏi khối lệnh.

2. Biến toàn cục (Global variable):
- Phạm vi: Toàn tệp tin mã nguồn (File scope), mọi hàm trong tệp đều có thể đọc/ghi trực tiếp.
- Vùng nhớ: Phân vùng dữ liệu tĩnh (Data Segment / BSS Segment).
- Thời gian sống: Tĩnh (Static duration) - tồn tại từ lúc chương trình bắt đầu chạy cho đến khi tiến trình kết thúc.

3. Biến tĩnh cục bộ (static local variable):
- Phạm vi: Khối lệnh (Block scope), chỉ truy cập được bên trong hàm định nghĩa nó (bảo đảm tính đóng gói).
- Vùng nhớ: Phân vùng dữ liệu tĩnh (Data/BSS Segment, giống biến toàn cục).
- Thời gian sống: Tồn tại suốt vòng đời chương trình. Giá trị của biến được bảo toàn nguyên vẹn qua các lần gọi hàm khác nhau. Biến chỉ được khởi tạo giá trị ĐÚNG MỘT LẦN DUY NHẤT khi chương trình nạp.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Đặc tính của biến cục bộ thông thường** | **3.0đ** | Nêu rõ Block scope, vùng nhớ Stack, sinh và hủy theo chu kỳ sống của hàm. |
| **Đặc tính của biến toàn cục** | **3.0đ** | Nêu rõ File scope, vùng nhớ Data/BSS Segment, tồn tại suốt chương trình. |
| **Đặc tính ưu việt của biến static local** | **4.0đ** | Phân tích rõ kết hợp Block scope và Static lifetime, khởi tạo 1 lần và bảo toàn giá trị. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 17. [PRF_PQ17] Khai thác thư viện chuẩn math.h, time.h và cơ chế sinh số giả ngẫu nhiên rand() / srand()

- **Mã định danh (Question Code):** `PRF_PQ17`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO6** | **DB 3NF CLO6** (`00000000-0000-0000-0000-000000000466`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 32-33 — *Standard Libraries: math.h, time.h & Pseudo-Random Number Generation*
- **Mục tiêu đánh giá:** Đánh giá khả năng sử dụng các hàm toán học trong <math.h>, thời gian hệ thống trong <time.h> và khởi tạo hạt giống sinh số ngẫu nhiên an toàn.

#### ❓ Câu hỏi vấn đáp:
> "Tại sao khi gọi hàm `rand()` trong `<stdlib.h>` mà không gọi `srand()` thì chương trình luôn sinh ra cùng một dãy số giống hệt nhau ở mọi lần chạy? Hãy giải thích vai trò của `srand((unsigned int)time(NULL))`, công thức sinh số nguyên ngẫu nhiên trong đoạn `[min, max]` và lưu ý khi dùng các hàm toán học `sqrt()`, `pow()`, `fabs()` trong `<math.h>`."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao ta chỉ nên gọi hàm `srand(time(NULL))` đúng một lần duy nhất ở đầu hàm `main()` thay vì đặt `srand(time(NULL))` bên trong vòng lặp `for` mỗi khi lấy số ngẫu nhiên?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Bản chất bộ sinh số giả ngẫu nhiên (PRNG) với `rand()` và `srand()`:
- Hàm `rand()` trong `<stdlib.h>` sử dụng thuật toán đồng dư tuyến tính (Linear Congruential Generator - LCG) để tính toán số tiếp theo dựa trên một giá trị trạng thái khởi đầu gọi là **Hạt giống (Seed)**, trả về số nguyên trong đoạn `[0, RAND_MAX]`.
- Nếu lập trình viên không gọi `srand()`, chuẩn C mặc định khởi tạo hạt giống cố định `srand(1)`. Vì thuật toán hoàn toàn tất định (deterministic), cùng một hạt giống `1` sẽ luôn tạo ra chính xác cùng một chuỗi số ở mọi lần khởi chạy chương trình.
- Hàm `time(NULL)` trong `<time.h>` trả về số giây đã trôi qua kể từ mốc Unix Epoch (`00:00:00 UTC 01/01/1970`). Khi gọi `srand((unsigned int)time(NULL))` đúng **1 lần duy nhất** ở đầu hàm `main()`, hạt giống sẽ thay đổi theo từng giây thực tế, giúp mỗi lần chạy chương trình sinh ra một dãy số khác nhau. *(Nếu đặt `srand(time(NULL))` bên trong vòng lặp chạy hàng ngàn lần trong cùng 1 giây, hạt giống bị reset liên tục về cùng một giá trị `time(NULL)` khiến `rand()` in ra toàn các số trùng nhau).*

2. Công thức chuẩn sinh số ngẫu nhiên trong đoạn `[min, max]` và ví dụ kết hợp `<math.h>`:
```c
#include <stdio.h>
#include <stdlib.h>
#include <time.h>
#include <math.h>

int randomInRange(int min, int max) {
    return min + rand() % (max - min + 1);
}

int main(void) {
    srand((unsigned int)time(NULL)); // Khởi tạo seed 1 lần duy nhất
    int x = randomInRange(-10, 10);
    double val = fabs((double)x);
    printf("x = %d, |x| = %.2f, sqrt(|x|) = %.4f, x^3 = %.2f\n",
           x, val, sqrt(val), pow((double)x, 3.0));
    return 0;
}
```

3. Lưu ý kỹ thuật khi sử dụng `<math.h>`:
- Các hàm `sqrt(double)`, `pow(double, double)`, `ceil(double)`, `floor(double)`, `fabs(double)` đều nhận và trả về kiểu dấu chấm động `double` (không dùng `abs()` của `<stdlib.h>` cho số thực vì `abs()` sẽ cắt cụt phần thập phân về `int`).
- Với `sqrt(x)`, phải kiểm tra điều kiện miền xác định `x >= 0.0` để tránh lỗi miền giá trị (`Domain Error` sinh `NaN`).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích cơ chế PRNG của rand() và vai trò hạt giống srand(time(NULL))** | **3.5đ** | Nêu rõ mặc định srand(1) gây lặp dãy số và cách dùng time(NULL) khởi tạo 1 lần ở đầu hàm main. |
| **Viết đúng công thức quy đổi dải số ngẫu nhiên [min, max]** | **3.5đ** | Viết đúng công thức min + rand() % (max - min + 1) và cài đặt minh họa chuẩn C. |
| **Phân biệt các hàm trong math.h (fabs, sqrt, pow) và kiểm tra miền xác định** | **3.0đ** | Chỉ rõ kiểu trả về double của fabs/sqrt/pow và kiểm tra điều kiện không âm trước khi gọi sqrt. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 18. [PRF_PQ18] Bản chất con trỏ, toán tử lấy địa chỉ & và toán tử giải tham chiếu *

- **Mã định danh (Question Code):** `PRF_PQ18`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 28-29 — *Pointers & Address Operators*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức cốt lõi về biến con trỏ, toán tử địa chỉ ô nhớ và toán tử truy xuất giá trị.

#### ❓ Câu hỏi vấn đáp:
> "Biến con trỏ (Pointer) trong C là gì? Hãy phân biệt cú pháp và ý nghĩa hoạt động của hai toán tử: Toán tử lấy địa chỉ & (Address-of) và Toán tử giải tham chiếu * (Dereference / Indirection)."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Kích thước của một biến con trỏ (ví dụ `sizeof(int*)` và `sizeof(char*)`) trên hệ điều hành 64-bit là bao nhiêu bytes?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Bản chất biến con trỏ:
Biến con trỏ là một biến đặc biệt dùng để lưu trữ địa chỉ vùng nhớ (Memory Address) của một biến hoặc ô nhớ khác trong RAM, thay vì trực tiếp lưu trữ giá trị dữ liệu thông thường.
Kích thước của mọi biến con trỏ phụ thuộc hoàn toàn vào kiến trúc hệ thống: 4 bytes trên hệ thống 32-bit và 8 bytes trên hệ thống 64-bit, bất kể kiểu dữ liệu mà nó trỏ tới là `char*`, `int*` hay `double*`.

2. Phân biệt hai toán tử con trỏ cốt lõi:
- Toán tử lấy địa chỉ `&` (Address-of operator): Đặt trước một biến để trích xuất địa chỉ ô nhớ vật lý của biến đó.
  Ví dụ: `int x = 10; int *p = &x;` -> Toán tử `&` lấy địa chỉ ô nhớ của `x` gán vào con trỏ `p`.
- Toán tử giải tham chiếu `*` (Dereference operator): Đặt trước một biến con trỏ để truy cập gián tiếp vào giá trị của ô nhớ mà con trỏ đang trỏ tới (cho phép đọc hoặc ghi đè giá trị).
  Ví dụ: `*p = 20;` -> Ghi đè giá trị 20 trực tiếp vào ô nhớ của biến `x`, làm `x` thay đổi thành 20.
  Lưu ý phân biệt dấu `*` khi khai báo kiểu con trỏ (`int *p;`) và dấu `*` trong câu lệnh thực thi (`*p = 20;`).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa biến con trỏ và kích thước ô nhớ kiến trúc** | **3.0đ** | Nêu rõ con trỏ lưu địa chỉ RAM, kích thước 4 bytes trên 32-bit và 8 bytes trên 64-bit. |
| **Ý nghĩa và cách dùng toán tử lấy địa chỉ &** | **3.5đ** | Giải thích chính xác toán tử & trích xuất địa chỉ vật lý của biến gán cho con trỏ. |
| **Ý nghĩa và cơ chế giải tham chiếu qua toán tử *** | **3.5đ** | Giải thích chính xác toán tử * truy xuất trực tiếp đọc/ghi ô nhớ gốc mà con trỏ trỏ đến. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 19. [PRF_PQ19] Mô phỏng truyền tham chiếu bằng con trỏ trong bài toán hoán vị swap

- **Mã định danh (Question Code):** `PRF_PQ19`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 30 — *Pass-by-reference Simulation & Swap*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng áp dụng con trỏ để sửa đổi dữ liệu ngoài phạm vi hàm thông qua bài toán kinh điển swap.

#### ❓ Câu hỏi vấn đáp:
> "Tại sao hàm `void swap(int a, int b)` viết thông thường lại không thể hoán đổi giá trị của hai biến trong hàm main()? Hãy viết lại hàm swap chính xác bằng cách sử dụng con trỏ và giải thích cơ chế hoạt động."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi gọi hàm swap viết bằng con trỏ từ hàm main, ta cần truyền đối số như thế nào?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Lý do hàm swap thông thường thất bại:
Vì ngôn ngữ C truyền tham số theo giá trị (Pass-by-value). Khi gọi `swap(x, y)`, hai bản sao của `x` và `y` được đẩy vào Stack Frame của hàm `swap`. Phép đổi chỗ biến chỉ diễn ra trên hai biến cục bộ `a` và `b` của hàm `swap`, khi hàm kết thúc Stack Frame bị giải phóng, hai biến gốc `x` và `y` trong `main()` hoàn toàn không thay đổi.

2. Cài đặt hàm swap chuẩn xác bằng con trỏ:
```c
void swap(int *a, int *b) {
    int temp = *a; // Lấy giá trị tại ô nhớ a trỏ tới lưu vào temp
    *a = *b;       // Ghi giá trị tại ô nhớ b vào ô nhớ a
    *b = temp;     // Ghi giá trị temp vào ô nhớ b
}
```

3. Cơ chế hoạt động:
- Trong hàm `main()`, ta truyền địa chỉ ô nhớ của hai biến thông qua toán tử `&`: `swap(&x, &y);`.
- Hàm `swap` nhận vào hai con trỏ `a` và `b` chứa địa chỉ của `x` và `y`.
- Nhờ toán tử giải tham chiếu `*a` và `*b`, hàm can thiệp trực tiếp vào các byte ô nhớ của `x` và `y` nằm trên Stack Frame của hàm `main()`, do đó hoán đổi thành công dữ liệu gốc.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích bản chất thất bại do truyền tham trị** | **3.0đ** | Chỉ rõ hàm nhận bản sao giá trị nên thao tác không tác động tới biến gốc ngoài main. |
| **Cài đặt đúng 100% mã nguồn hàm swap với con trỏ** | **4.0đ** | Viết đúng tham số int *a, int *b, biến tạm temp và toán tử giải tham chiếu *. |
| **Cú pháp gọi hàm với toán tử & và phân tích ô nhớ** | **3.0đ** | Nêu đúng lời gọi swap(&x, &y) và giải thích can thiệp trực tiếp ô nhớ gốc. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 20. [PRF_PQ20] Số học con trỏ (Pointer Arithmetic): Ý nghĩa của ptr + 1 phụ thuộc vào sizeof(*ptr)

- **Mã định danh (Question Code):** `PRF_PQ20`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 29 — *Pointer Arithmetic & Memory Scaling*
- **Mục tiêu đánh giá:** Kiểm tra hiểu biết về phép toán số học trên con trỏ và cơ chế nhân kích thước kiểu dữ liệu tự động.

#### ❓ Câu hỏi vấn đáp:
> "Trong C, phép cộng `ptr + 1` trên con trỏ khác với phép cộng số học thông thường như thế nào? Nếu con trỏ `int *p` đang có giá trị địa chỉ là `0x1000` (giả sử `sizeof(int) = 4`), thì sau khi thực hiện `p = p + 2`, địa chỉ mới của p là bao nhiêu (hệ thập lục phân)?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Có thể thực hiện phép cộng giữa hai con trỏ với nhau (ptr1 + ptr2) được không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Bản chất của số học con trỏ (Pointer Arithmetic):
Phép cộng/trừ trên con trỏ không phải là phép cộng số học trên từng byte đơn thuần, mà là phép dịch chuyển theo từng phần tử dữ liệu (Scaled by data type size).
Khi thực hiện biểu thức `ptr + n`, địa chỉ mới được tính theo công thức:
`Địa_chỉ_mới = Địa_chỉ_cũ + n * sizeof(*ptr)`
Trong đó `sizeof(*ptr)` là kích thước byte của kiểu dữ liệu mà con trỏ đang trỏ tới.
- Nếu `char *pc`: `pc + 1` tăng đúng 1 byte (vì `sizeof(char) = 1`).
- Nếu `int *pi`: `pi + 1` tăng 4 bytes (vì `sizeof(int) = 4`).
- Nếu `double *pd`: `pd + 1` tăng 8 bytes (vì `sizeof(double) = 8`).

2. Tính toán địa chỉ cho ví dụ:
- Địa chỉ ban đầu: `p = 0x1000`.
- Biểu thức `p = p + 2` sẽ tăng địa chỉ thêm: `2 * sizeof(int) = 2 * 4 = 8 bytes` (tức `0x8` trong hệ Hex).
- Địa chỉ mới: `0x1000 + 0x8 = 0x1008`.

3. Các phép toán hợp lệ trên con trỏ:
- Cộng/trừ con trỏ với một số nguyên (`ptr + n`, `ptr - n`).
- Trừ hai con trỏ cùng kiểu trỏ vào cùng một mảng (`ptr1 - ptr2`) để tính khoảng cách số phần tử.
- TUYỆT ĐỐI CẤM: Phép cộng hai con trỏ (`ptr1 + ptr2`), phép nhân, chia hoặc lấy dư con trỏ.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Công thức tính số học con trỏ theo sizeof** | **4.0đ** | Nêu rõ bước nhảy phụ thuộc vào kích thước kiểu dữ liệu: n * sizeof(*ptr). |
| **Tính toán chuẩn xác địa chỉ mới 0x1008** | **3.0đ** | Thực hiện phép tính 0x1000 + 2 * 4 = 0x1008 trong hệ Hex hoàn toàn chính xác. |
| **Phân loại các phép toán hợp lệ và bất hợp lệ** | **3.0đ** | Nêu rõ được phép trừ 2 con trỏ cùng mảng nhưng cấm tuyệt đối cộng 2 con trỏ. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 21. [PRF_PQ21] Mối quan hệ tương đương giữa Mảng và Con trỏ: arr[i] == *(arr + i)

- **Mã định danh (Question Code):** `PRF_PQ21`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 29-30 & 37 — *Array-Pointer Equivalence*
- **Mục tiêu đánh giá:** Đánh giá khả năng hiểu bản chất tên mảng là hằng con trỏ và tính tương đương giữa chỉ số và giải tham chiếu.

#### ❓ Câu hỏi vấn đáp:
> "Hãy chứng minh mối quan hệ tương đương giữa cách viết mảng qua chỉ số `arr[i]` và cách viết qua con trỏ `*(arr + i)` trong C. Tại sao biểu thức kỳ lạ `i[arr]` lại hoàn toàn hợp lệ và cho cùng kết quả với `arr[i]`?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tên mảng arr khác với một biến con trỏ thông thường ở điểm cốt lõi nào khi thực hiện phép gán?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Bản chất tên mảng trong C:
Trong C, tên của một mảng `arr` khi xuất hiện trong hầu hết các biểu thức (trừ khi là toán hạng của `sizeof` hoặc toán tử `&`) sẽ tự động bị phân rã (decay) thành một con trỏ hằng (constant pointer) trỏ đến địa chỉ của phần tử đầu tiên: `arr == &arr[0]`.

2. Chứng minh `arr[i] == *(arr + i)`:
Theo định nghĩa của ngôn ngữ C, toán tử truy cập chỉ số `[]` thực chất là một phép toán cú pháp rút gọn (syntactic sugar) của phép toán số học con trỏ và giải tham chiếu:
`arr[i]` được trình biên dịch chuyển đổi chính xác thành `*(arr + i)`.
- `arr`: Địa chỉ phần tử đầu tiên của mảng.
- `arr + i`: Dịch chuyển đến địa chỉ của phần tử thứ `i` (tăng thêm `i * sizeof(element)` bytes).
- `*(arr + i)`: Giải tham chiếu lấy giá trị tại địa chỉ đó.

3. Giải thích biểu thức `i[arr]`:
Phép cộng số học có tính chất giao hoán: `arr + i` hoàn toàn tương đương với `i + arr`.
Do đó: `*(arr + i)` chính là `*(i + arr)`, mà theo quy tắc định nghĩa toán tử `[]` thì `*(i + arr)` được viết lại thành `i[arr]`. Trình biên dịch C chấp nhận cú pháp này một cách hoàn toàn hợp lệ.

4. Điểm khác nhau cốt lõi giữa tên mảng và con trỏ:
Tên mảng là một HẰNG CON TRỎ (Constant Pointer), địa chỉ của nó cố định và không thể thay đổi giá trị. Ta KHÔNG THỂ viết `arr = ptr;` hoặc `arr++;` (lỗi biên dịch lvalue required), trong khi biến con trỏ thông thường có thể thoải mái gán địa chỉ mới.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Bản chất tên mảng suy giảm thành con trỏ phần tử 0** | **3.0đ** | Nêu rõ tên mảng arr decay thành con trỏ trỏ tới &arr[0] trong biểu thức. |
| **Chứng minh arr[i] là cú pháp rút gọn của *(arr + i)** | **4.0đ** | Chỉ ra cơ chế compiler chuyển arr[i] thành *(arr + i) và giải thích phép giao hoán i[arr]. |
| **Phân biệt hằng con trỏ (tên mảng) vs biến con trỏ** | **3.0đ** | Nêu bật tên mảng không thể gán lại hay tăng arr++, bảo toàn địa chỉ cơ sở. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 22. [PRF_PQ22] Ma trận hai chiều (2D Array) và thứ tự lưu trữ ưu tiên dòng (Row-major order)

- **Mã định danh (Question Code):** `PRF_PQ22`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO7** (`00000000-0000-0000-0000-000000000467`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 38 — *2D Matrices & Row-Major Layout*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức về cơ chế trải phẳng mảng hai chiều trên RAM và cách truyền ma trận vào hàm.

#### ❓ Câu hỏi vấn đáp:
> "Mảng hai chiều (Matrix) được tổ chức lưu trữ vật lý trong bộ nhớ RAM như thế nào theo quy ước Row-major order của C? Tại sao khi truyền ma trận 2 chiều vào một hàm, ta bắt buộc phải chỉ định kích thước số cột cố định (ví dụ `void printMatrix(int a[][COLS], int rows)`)?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu ma trận có R dòng và C cột, phần tử `a[i][j]` nằm ở vị trí byte thứ bao nhiêu tính từ địa chỉ đầu của ma trận?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Tổ chức lưu trữ Row-major order trong RAM:
Bộ nhớ RAM thực chất là một không gian địa chỉ tuyến tính một chiều. Ngôn ngữ C tổ chức lưu trữ mảng hai chiều theo thứ tự ưu tiên dòng (Row-major order):
- Toàn bộ các phần tử của Hàng 0 được lưu trữ liên tiếp trước: `a[0][0], a[0][1], ..., a[0][COLS-1]`.
- Ngay sau đó là toàn bộ các phần tử của Hàng 1, Hàng 2... cho đến hàng cuối cùng.
Công thức tính vị trí phần tử tuyến tính:
`Vị_trí(a[i][j]) = i * COLS + j`
Địa chỉ byte tương ứng: `Địa_chỉ(a[i][j]) = Địa_chỉ_gốc + (i * COLS + j) * sizeof(kiểu_dữ_liệu)`.

2. Lý do bắt buộc phải khai báo kích thước cột `COLS`:
Để trình biên dịch có thể sinh mã máy tính toán địa chỉ của `a[i][j]`, nó bắt buộc phải biết công thức nhảy qua bao nhiêu phần tử để đến được dòng thứ `i`.
Số phần tử cần nhảy qua cho mỗi dòng chính là số cột (`COLS`).
Nếu không khai báo `COLS`, trình biên dịch không thể biết được mỗi hàng chiếm bao nhiêu bytes, do đó không thể tính được địa chỉ `a[i][j]` và báo lỗi biên dịch (array type has incomplete element type). Trong khi đó số dòng `rows` không cần thiết lúc tính toán chỉ số vì địa chỉ bắt đầu không phụ thuộc vào giới hạn dòng cuối.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích cơ chế Row-major order tuyến tính** | **3.5đ** | Mô tả chuẩn lưu tuần tự hàng 0 rồi đến hàng 1 và công thức (i * COLS + j). |
| **Công thức tính địa chỉ ô nhớ vật lý** | **3.0đ** | Viết đúng công thức địa chỉ: BaseAddress + (i * COLS + j) * sizeof(type). |
| **Lý do compiler bắt buộc biết số cột COLS** | **3.5đ** | Phân tích rõ compiler cần COLS để biết bước nhảy qua từng hàng, nếu thiếu sẽ lỗi. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 23. [PRF_PQ23] Thuật toán sắp xếp chọn (Selection Sort) trên mảng một chiều

- **Mã định danh (Question Code):** `PRF_PQ23`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO7** (`00000000-0000-0000-0000-000000000467`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 39 — *Sorting Algorithms: Selection Sort*
- **Mục tiêu đánh giá:** Đánh giá khả năng hiểu nguyên lý, cài đặt và phân tích độ phức tạp của thuật toán Selection Sort trong C.

#### ❓ Câu hỏi vấn đáp:
> "Hãy trình bày ý tưởng của thuật toán Sắp xếp chọn (Selection Sort) để sắp xếp mảng N số nguyên theo thứ tự tăng dần. Viết hàm C cài đặt thuật toán và cho biết độ phức tạp thời gian trường hợp tốt nhất và xấu nhất."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Thuật toán Selection Sort có phải là một thuật toán sắp xếp ổn định (Stable Sort) không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Ý tưởng thuật toán Selection Sort:
Chia mảng thành hai phần: phần đầu đã sắp xếp và phần sau chưa sắp xếp.
Thuật toán duyệt qua $N - 1$ lượt:
- Tại mỗi lượt thứ `i` (từ 0 đến $N - 2$), tìm phần tử có giá trị nhỏ nhất trong đoạn chưa sắp xếp từ chỉ số `i` đến $N - 1$.
- Sau khi tìm được vị trí `min_idx`, hoán đổi phần tử nhỏ nhất đó với phần tử tại vị trí `i`.
- Sau mỗi lượt, phần tử nhỏ nhất được cố định đúng vị trí đầu mảng còn lại.

2. Cài đặt thuật toán bằng ngôn ngữ C:
```c
void selectionSort(int arr[], int n) {
    for (int i = 0; i < n - 1; i++) {
        int min_idx = i;
        for (int j = i + 1; j < n; j++) {
            if (arr[j] < arr[min_idx]) {
                min_idx = j;
            }
        }
        if (min_idx != i) {
            int temp = arr[i];
            arr[i] = arr[min_idx];
            arr[min_idx] = temp;
        }
    }
}
```

3. Độ phức tạp thời gian:
- Tổng số phép so sánh luôn là $(n-1) + (n-2) + ... + 1 = \frac{n(n-1)}{2}$.
- Độ phức tạp thời gian trong cả trường hợp tốt nhất (Best Case), trung bình (Average Case) và xấu nhất (Worst Case) đều là $O(N^2)$.
- Độ phức tạp không gian phụ: $O(1)$ (in-place sort).
- Selection Sort không ổn định (Unstable) vì phép hoán đổi xa có thể làm đảo lộn thứ tự tương đối của các phần tử có giá trị bằng nhau.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Trình bày ý tưởng thuật toán chia 2 phần và tìm min** | **3.0đ** | Nêu rõ nguyên lý tìm vị trí nhỏ nhất trong đoạn chưa sắp xếp đưa về đầu. |
| **Cài đặt chuẩn xác mã nguồn C với 2 vòng lặp** | **4.0đ** | Viết đúng 2 vòng for lồng nhau, biến min_idx và phép hoán vị swap an toàn. |
| **Phân tích độ phức tạp thời gian O(N^2) và tính ổn định** | **3.0đ** | Xác định chuẩn O(N^2) trong mọi trường hợp và giải thích tính không ổn định. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 24. [PRF_PQ24] Nguy cơ bảo mật của hàm gets() và kỹ thuật nhập chuỗi an toàn với fgets()

- **Mã định danh (Question Code):** `PRF_PQ24`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO8** | **DB 3NF CLO8** (`00000000-0000-0000-0000-000000000468`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 47 — *String Input Security: gets vs fgets*
- **Mục tiêu đánh giá:** Kiểm tra nhận thức về lỗ hổng tràn bộ đệm khi nhập chuỗi và kỹ thuật sử dụng fgets an toàn kèm xử lý ký tự xuống dòng.

#### ❓ Câu hỏi vấn đáp:
> "Tại sao hàm gets() bị cấm và loại bỏ hoàn toàn trong chuẩn C11? Hãy trình bày cách sử dụng hàm fgets() để nhập chuỗi an toàn từ bàn phím và cách loại bỏ ký tự xuống dòng '\n' mà fgets lưu lại ở cuối chuỗi."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu người dùng nhập chuỗi dài hơn kích thước bộ đệm truyền vào fgets, phần ký tự dư thừa sẽ đi đâu?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Nguy cơ bảo mật nghiêm trọng của hàm `gets()`:
Hàm `gets(char *str)` chỉ nhận địa chỉ mảng đầu vào mà không nhận giới hạn kích thước tối đa của bộ đệm.
Nếu người dùng nhập số ký tự vượt quá dung lượng mảng, hàm `gets()` tiếp tục ghi đè các byte dữ liệu lên vùng nhớ lân cận trên Stack Frame (Lỗ hổng Buffer Overflow). Kẻ tấn công có thể lợi dụng điều này để ghi đè địa chỉ trả về của hàm (Return Address), chiếm quyền điều khiển luồng thực thi của tiến trình. Do đó, `gets()` bị loại bỏ hoàn toàn khỏi chuẩn C11.

2. Sử dụng hàm an toàn `fgets()`:
Cú pháp: `fgets(char *str, int size, FILE *stream);`
Ví dụ nhập chuỗi an toàn từ bàn phím:
```c
char buffer[100];
printf("Nhap chuoi: ");
fgets(buffer, sizeof(buffer), stdin);
```\n`fgets` luôn đảm bảo đọc tối đa `sizeof(buffer) - 1` ký tự và tự động chèn ký tự null `'\0'` ở cuối, chống tràn bộ đệm tuyệt đối.

3. Xử lý ký tự xuống dòng `\n`:
`fgets` lưu cả ký tự Enter (`'\n'`) mà người dùng nhấn vào cuối chuỗi. Để loại bỏ nó, ta dùng thư viện `string.h`:
```c
buffer[strcspn(buffer, "\n")] = '\0';
// Hoặc kiểm tra thủ công:
size_t len = strlen(buffer);
if (len > 0 && buffer[len - 1] == '\n') {
    buffer[len - 1] = '\0';
}
```

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân tích lỗ hổng Buffer Overflow của hàm gets** | **3.5đ** | Nêu rõ gets không kiểm soát độ dài, ghi đè Call Stack gây lỗ hổng bảo mật. |
| **Cú pháp và tham số an toàn của fgets** | **3.5đ** | Sử dụng đúng cú pháp fgets(buf, sizeof(buf), stdin) kiểm soát dung lượng bộ đệm. |
| **Kỹ thuật loại bỏ ký tự '\n' chuẩn C** | **3.0đ** | Đưa ra code loại bỏ ký tự newline dùng strcspn hoặc kiểm tra strlen chính xác. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 25. [PRF_PQ25] Các hàm thao tác chuỗi cốt lõi trong string.h: strlen, strcmp, strcpy, strcat

- **Mã định danh (Question Code):** `PRF_PQ25`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO6** | **DB 3NF CLO6** (`00000000-0000-0000-0000-000000000466`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 48 — *String Functions in string.h*
- **Mục tiêu đánh giá:** Đánh giá khả năng hiểu và vận dụng đúng các hàm xử lý chuỗi thư viện chuẩn và giá trị trả về của chúng.

#### ❓ Câu hỏi vấn đáp:
> "Hãy nêu mục đích, nguyên mẫu và quy ước giá trị trả về của 4 hàm xử lý chuỗi cơ bản trong thư viện <string.h>: strlen(), strcmp(), strcpy() và strcat(). Tại sao không thể so sánh hai chuỗi bằng toán tử `str1 == str2`?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Hàm strcmp trả về giá trị âm, 0, dương khi nào?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Bốn hàm cốt lõi trong `<string.h>`:
- `size_t strlen(const char *s)`:
  + Mục đích: Đếm số lượng ký tự trong chuỗi `s` cho đến trước ký tự null terminator `'\0'`. Lưu ý độ dài chuỗi KHÔNG bao gồm ký tự `'\0'`.
- `int strcmp(const char *s1, const char *s2)`:
  + Mục đích: So sánh hai chuỗi theo thứ tự từ điển (Lexicographical order) dựa trên mã ASCII từng ký tự.
  + Giá trị trả về: Trả về `< 0` nếu `s1 < s2`; trả về `0` nếu `s1 == s2` (hai chuỗi giống hệt nhau); trả về `> 0` nếu `s1 > s2`.
- `char *strcpy(char *dest, const char *src)`:
  + Mục đích: Sao chép toàn bộ chuỗi nguồn `src` (kể cả ký tự `'\0'`) sang vùng nhớ đích `dest`. Yêu cầu mảng `dest` phải đủ lớn để chứa `src`.
- `char *strcat(char *dest, const char *src)`:
  + Mục đích: Nối chuỗi `src` vào sau chuỗi `dest`. Vị trí `'\0'` ban đầu của `dest` bị ghi đè bởi ký tự đầu của `src`, và một `'\0'` mới được thêm vào cuối.

2. Tại sao không thể dùng toán tử `str1 == str2`?
Trong C, `str1` và `str2` thực chất là các biến con trỏ chứa địa chỉ ô nhớ của hai mảng ký tự. Phép so sánh `str1 == str2` chỉ so sánh xem hai con trỏ có cùng trỏ tới MỘT ĐỊA CHỈ Ô NHỚ hay không (so sánh địa chỉ), chứ KHÔNG hề so sánh nội dung các ký tự bên trong chuỗi. Muốn so sánh nội dung chuỗi, bắt buộc phải dùng hàm `strcmp(str1, str2) == 0`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Đặc tả chính xác hàm strlen và strcmp** | **4.0đ** | Nêu rõ strlen không đếm '\0', strcmp trả về <0, ==0, >0 theo thứ tự từ điển ASCII. |
| **Đặc tả chính xác hàm strcpy và strcat** | **3.0đ** | Trình bày đúng cơ chế copy và nối chuỗi kèm yêu cầu dung lượng mảng đích. |
| **Giải thích lỗi so sánh địa chỉ khi dùng str1 == str2** | **3.0đ** | Chỉ rõ toán tử == chỉ so sánh 2 địa chỉ con trỏ chứ không so sánh nội dung xâu. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 26. [PRF_PQ26] Khai báo Struct và phân biệt toán tử chấm '.' và mũi tên '->'

- **Mã định danh (Question Code):** `PRF_PQ26`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO7** (`00000000-0000-0000-0000-000000000467`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 40, 42 — *Structures: Dot vs Arrow Operator*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức về cấu trúc dữ liệu Struct, khai báo typedef và cách truy xuất thành viên qua biến và con trỏ.

#### ❓ Câu hỏi vấn đáp:
> "Struct trong C là gì? Hãy viết khai báo một kiểu dữ liệu struct Student bằng từ khóa typedef (gồm ID, Name, GPA). Phân biệt sự khác nhau khi truy xuất thành viên bằng toán tử dấu chấm '.' và toán tử mũi tên '->'."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Biểu thức `ptr->GPA` tương đương với cách viết nào sử dụng toán tử giải tham chiếu * và dấu chấm .?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Khái niệm Struct trong C:
`struct` (Structure) là kiểu dữ liệu do người dùng tự định nghĩa, cho phép gom nhóm nhiều biến có kiểu dữ liệu khác nhau (như `int`, `char[]`, `float`) lại thành một thực thể duy nhất có cấu trúc logic.

2. Khai báo `struct Student` kết hợp `typedef`:
```c
typedef struct {
    int id;
    char name[50];
    float gpa;
} Student;
```
Từ khóa `typedef` cho phép đặt bí danh kiểu dữ liệu, giúp khai báo biến sau này ngắn gọn (`Student s1;`) mà không cần phải viết lặp lại từ khóa `struct Student s1;`.

3. Phân biệt toán tử chấm `.` và mũi tên `->`:
- Toán tử dấu chấm `.` (Direct Member Access): Dùng khi làm việc trực tiếp với một BIẾN CẤU TRÚC thông thường.
  Ví dụ: `Student s1; s1.gpa = 3.8;`
- Toán tử mũi tên `->` (Indirect Member Access): Dùng khi làm việc với một BIẾN CON TRỎ trỏ tới struct.
  Ví dụ: `Student *ptr = &s1; ptr->gpa = 3.8;`
- Mối quan hệ tương đương: `ptr->gpa` chính là cú pháp viết ngắn gọn của biểu thức `(*ptr).gpa`.
  Lưu ý dấu ngoặc tròn `(*ptr)` là bắt buộc vì toán tử dấu chấm `.` có độ ưu tiên cao hơn toán tử giải tham chiếu `*`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Khái niệm struct và khai báo typedef chuẩn C** | **3.5đ** | Định nghĩa gom nhóm biến khác kiểu và viết code typedef struct Student chính xác. |
| **Phân biệt trường hợp dùng toán tử . và ->** | **3.5đ** | Chỉ rõ dùng dấu chấm cho biến trực tiếp và dùng mũi tên cho biến con trỏ struct. |
| **Chứng minh tương đương ptr->member == (*ptr).member** | **3.0đ** | Giải thích cú pháp tương đương và tầm quan trọng của dấu ngoặc do độ ưu tiên. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 27. [PRF_PQ27] Quản lý bộ nhớ động trong C: Phân biệt malloc, calloc, realloc và free

- **Mã định danh (Question Code):** `PRF_PQ27`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 31 — *Dynamic Memory Allocation (DMA)*
- **Mục tiêu đánh giá:** Đánh giá khả năng hiểu sâu và vận dụng thành thạo 4 hàm cấp phát bộ nhớ động trên Heap trong stdlib.h.

#### ❓ Câu hỏi vấn đáp:
> "Hãy phân tích cơ chế hoạt động, nguyên mẫu hàm và sự khác nhau căn bản giữa 4 hàm cấp phát bộ nhớ động trong C: malloc(), calloc(), realloc() và free(). Tại sao sau khi cấp phát động luôn bắt buộc phải kiểm tra con trỏ NULL?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu realloc không tìm được vùng nhớ liền kề đủ lớn để mở rộng, nó sẽ xử lý như thế nào và điều gì xảy ra nếu realloc thất bại trả về NULL?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Bốn hàm quản lý bộ nhớ động trong `<stdlib.h>` trên vùng nhớ Heap:
- `void* malloc(size_t size)`:
  + Cấp phát một khối nhớ liên tục có dung lượng `size` bytes.
  + KHÔNG khởi tạo dữ liệu: Các byte trong vùng nhớ mới chứa dữ liệu rác (Garbage values).
- `void* calloc(size_t num, size_t size)`:
  + Cấp phát vùng nhớ cho một mảng gồm `num` phần tử, mỗi phần tử có kích thước `size` bytes (tổng `num * size` bytes).
  + Tự động khởi tạo TOÀN BỘ các byte trong vùng nhớ về giá trị `0`.
- `void* realloc(void *ptr, size_t new_size)`:
  + Thay đổi kích thước khối nhớ đã cấp phát trước đó.
  + Nếu có đủ ô nhớ trống kế tiếp: Mở rộng trực tiếp tại chỗ.
  + Nếu không đủ ô nhớ trống kế tiếp: Tìm một vùng nhớ mới đủ lớn ở vị trí khác trên Heap, tự động sao chép toàn bộ dữ liệu cũ sang vùng mới, giải phóng vùng nhớ cũ và trả về địa chỉ mới.
  + Nếu `ptr == NULL`: Hoạt động tương đương `malloc(new_size)`. Nếu `new_size == 0`: Tương đương `free(ptr)`.
- `void free(void *ptr)`:
  + Trả lại khối vùng nhớ Heap cho hệ điều hành quản lý. Vùng nhớ này không còn thuộc quyền sở hữu của tiến trình nữa.

2. Tầm quan trọng của việc kiểm tra con trỏ `NULL`:
Hệ điều hành có thể hết bộ nhớ RAM vật lý hoặc vượt quá hạn ngạch (quota) của tiến trình. Trong trường hợp đó, các hàm cấp phát sẽ thất bại và trả về con trỏ `NULL`.
Nếu chương trình không kiểm tra mà cố tình truy cập giải tham chiếu `*ptr = 10;`, CPU sẽ kích hoạt ngắt phần cứng xử lý lỗi phân trang bộ nhớ (Page Fault / Segmentation Fault) làm sập chương trình ngay lập tức.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân biệt chi tiết malloc và calloc** | **3.0đ** | Nêu rõ tham số, dung lượng và sự khác nhau về khởi tạo byte 0 của calloc vs rác của malloc. |
| **Cơ chế mở rộng/di chuyển vùng nhớ của realloc** | **3.5đ** | Phân tích 2 trường hợp mở rộng tại chỗ hoặc cấp khối mới copy dữ liệu cũ. |
| **Hàm free và lý do bắt buộc kiểm tra con trỏ NULL** | **3.5đ** | Giải thích giải phóng Heap và kiểm tra NULL chống crash khi hệ thống cạn kiệt RAM. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 28. [PRF_PQ28] Phân tích 3 lỗi bộ nhớ nguy hiểm: Memory Leak, Dangling Pointer và Double Free

- **Mã định danh (Question Code):** `PRF_PQ28`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 31 — *Memory Bugs: Leak, Dangling Pointer, Double Free*
- **Mục tiêu đánh giá:** Đánh giá khả năng nhận diện, phân tích nguyên nhân gốc rễ và đề xuất giải pháp phòng ngừa 3 lỗi bộ nhớ kinh điển trong C.

#### ❓ Câu hỏi vấn đáp:
> "Hãy phân tích bản chất kỹ thuật, kịch bản phát sinh và hậu quả của 3 lỗi bộ nhớ nghiêm trọng trong C: Rò rỉ bộ nhớ (Memory Leak), Con trỏ lơ lửng (Dangling Pointer) và Giải phóng hai lần (Double Free). Nêu giải pháp lập trình phòng ngừa cho từng lỗi."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Sau khi gọi free(ptr), việc gán `ptr = NULL;` giúp ngăn ngừa những lỗi nào trong 3 lỗi trên?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Rò rỉ bộ nhớ (Memory Leak):
- Bản chất: Bộ nhớ được cấp phát trên Heap qua `malloc/calloc` nhưng chương trình làm mất con trỏ duy nhất trỏ tới nó (ví dụ: gán con trỏ sang địa chỉ khác, hoặc biến con trỏ cục bộ bị hủy khi hàm return) mà chưa gọi `free()`.
- Hậu quả: Vùng nhớ đó bị chiếm dụng vĩnh viễn không thể thu hồi. Khi ứng dụng chạy lâu dài (như daemon, web server), dung lượng RAM tiêu thụ tăng dần dẫn đến cạn kiệt bộ nhớ hệ thống (OOM Killer).
- Giải pháp: Tuân thủ nguyên tắc 'cấp phát ở đâu thì giải phóng ở đó'; sử dụng công cụ kiểm toán bộ nhớ như Valgrind.

2. Con trỏ lơ lửng (Dangling Pointer):
- Bản chất: Con trỏ vẫn lưu giữ địa chỉ của một ô nhớ đã bị giải phóng bằng `free()`, hoặc trỏ tới địa chỉ của biến cục bộ đã bị hủy sau khi hàm thoát.
- Hậu quả: Nếu cố tình đọc/ghi qua con trỏ này, chương trình có thể đọc dữ liệu rác, ghi đè làm hỏng cấu trúc dữ liệu mới được cấp phát tại ô nhớ đó, hoặc bị crash ngẫu nhiên cực kỳ khó gỡ lỗi (Use-After-Free Vulnerability).
- Giải pháp: Ngay sau khi gọi `free(ptr);`, BẮT BUỘC gán ngay `ptr = NULL;`. Không bao giờ trả về địa chỉ của biến cục bộ từ một hàm.

3. Giải phóng hai lần (Double Free):
- Bản chất: Gọi hàm `free()` hai lần trên cùng một địa chỉ ô nhớ mà không có thao tác cấp phát lại ở giữa.
- Hậu quả: Làm hỏng cấu trúc quản lý danh sách ô nhớ tự do (Free List / Heap Metadata) của bộ cấp phát `glibc`, gây ra lỗi `double free or corruption` và sập chương trình lập tức; kẻ tấn công có thể lợi dụng để thực thi mã độc tùy ý (Arbitrary Code Execution).
- Giải pháp: Gán `ptr = NULL;` ngay sau lệnh `free(ptr);` đầu tiên, vì hàm `free(NULL)` trong chuẩn C là một thao tác an toàn (No-op, không làm gì cả).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân tích lỗi Memory Leak và hậu quả cạn RAM** | **3.0đ** | Nêu rõ mất con trỏ Heap không thể thu hồi, dẫn đến tràn bộ nhớ tiến trình. |
| **Phân tích lỗi Dangling Pointer và giải pháp gán NULL** | **3.5đ** | Mô tả ô nhớ đã giải phóng nhưng con trỏ vẫn trỏ tới và cách gán ptr = NULL phòng ngừa. |
| **Phân tích lỗi Double Free và hỏng Heap Metadata** | **3.5đ** | Giải thích gọi free 2 lần gây hỏng bảng cấp phát Heap và tính an toàn của free(NULL). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 29. [PRF_PQ29] Kỹ thuật cấp phát động ma trận 2 chiều bằng con trỏ cấp hai (int**) và giải phóng bộ nhớ đúng cách

- **Mã định danh (Question Code):** `PRF_PQ29`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 31, 38 — *Dynamic 2D Arrays & Double Pointers*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng cấp phát động mảng hai chiều kích thước linh hoạt bằng con trỏ cấp hai và thứ tự giải phóng bộ nhớ tránh rò rỉ.

#### ❓ Câu hỏi vấn đáp:
> "Trình bày chi tiết các bước cấp phát động một ma trận gồm M dòng và N cột số nguyên bằng con trỏ cấp hai (int**). Viết đoạn mã C cấp phát, kiểm tra lỗi và quy trình giải phóng bộ nhớ theo đúng thứ tự."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu ta giải phóng con trỏ ngoài `free(matrix)` trước khi giải phóng các dòng con thì điều gì sẽ xảy ra?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Ý tưởng cấp phát động ma trận qua con trỏ cấp hai:
Ta cần một con trỏ cấp hai `int **matrix` để quản lý một mảng các con trỏ dòng:
- Bước 1: Dùng `malloc` cấp phát một mảng gồm $M$ phần tử kiểu con trỏ `int*`.
- Bước 2: Dùng vòng lặp duyệt qua từng dòng $i$ từ 0 đến $M-1$, cấp phát tiếp cho mỗi con trỏ dòng một mảng gồm $N$ phần tử kiểu `int`.

2. Đoạn mã C cấp phát an toàn:
```c
int **matrix = (int**)malloc(M * sizeof(int*));
if (matrix == NULL) return -1;

for (int i = 0; i < M; i++) {
    matrix[i] = (int*)malloc(N * sizeof(int));
    if (matrix[i] == NULL) {
        // Nếu lỗi ở dòng thứ i, phải giải phóng các dòng trước đó
        for (int k = 0; k < i; k++) free(matrix[k]);
        free(matrix);
        return -1;
    }
}
```

3. Quy trình giải phóng bộ nhớ đúng thứ tự:
Bắt buộc phải giải phóng NGƯỢC THỨ TỰ: từ trong ra ngoài!
- Bước 1: Duyệt vòng lặp giải phóng từng dòng con trước:
  ```c
  for (int i = 0; i < M; i++) {
      free(matrix[i]);
  }
  ```
- Bước 2: Giải phóng mảng con trỏ ngoài cùng và gán NULL:
  ```c
  free(matrix);
  matrix = NULL;
  ```
Hậu quả nếu `free(matrix)` trước: Con trỏ ngoài bị hủy, chương trình mất toàn bộ địa chỉ của $M$ mảng dòng con trên Heap, dẫn đến lỗi Rò rỉ bộ nhớ (Memory Leak) vĩnh viễn đối với $M \times N$ số nguyên.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Quy trình cấp phát 2 bước qua con trỏ cấp hai** | **3.5đ** | Mô tả chuẩn cấp phát mảng con trỏ int* trước rồi duyệt cấp mảng int cho từng dòng. |
| **Xử lý ngoại lệ và kiểm tra con trỏ NULL** | **3.0đ** | Bắt lỗi NULL ở cả con trỏ ngoài và từng dòng con một cách chuyên nghiệp. |
| **Thứ tự giải phóng từ trong ra ngoài và hậu quả nếu làm sai** | **3.5đ** | Khẳng định free từng dòng trước mới free con trỏ ngoài, phân tích bẫy Memory Leak. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 30. [PRF_PQ30] Căn chỉnh bộ nhớ (Data Alignment) và Byte Padding trong cấu trúc Struct C

- **Mã định danh (Question Code):** `PRF_PQ30`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO7** (`00000000-0000-0000-0000-000000000467`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 40 — *Struct Padding & Memory Alignment*
- **Mục tiêu đánh giá:** Đánh giá hiểu biết sâu sắc về kiến trúc phần cứng, lý do tại sao sizeof(struct) không bằng tổng kích thước các thành viên.

#### ❓ Câu hỏi vấn đáp:
> "Xét cấu trúc struct sau trên hệ thống 32-bit/64-bit:
```c
struct Example {
    char a;   // 1 byte
    int b;    // 4 bytes
    char c;   // 1 byte
};
```
Tại sao kết quả `sizeof(struct Example)` lại bằng 12 bytes chứ không phải 6 bytes? Khái niệm Căn chỉnh dữ liệu (Data Alignment) và Byte Padding là gì? Làm thế nào để sắp xếp lại các trường nhằm tối ưu dung lượng bộ nhớ?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu sắp xếp lại thứ tự khai báo thành `char a; char c; int b;`, kích thước của struct sẽ giảm xuống còn bao nhiêu bytes?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Khái niệm Căn chỉnh dữ liệu (Data Alignment) và Byte Padding:
- Bộ vi xử lý (CPU) hiện đại không đọc dữ liệu từ RAM theo từng byte đơn lẻ mà đọc theo từng khối từ nhớ (Word: 4 bytes trên 32-bit, 8 bytes trên 64-bit).
- Để tối ưu hóa tốc độ truy xuất ô nhớ (truy xuất trong 1 chu kỳ máy thay vì 2 chu kỳ), CPU yêu cầu các biến kiểu dữ liệu $K$ bytes phải bắt đầu tại các địa chỉ ô nhớ là bội số của $K$ (Natural Alignment):
  + Kiểu `char` (1 byte): Có thể nằm ở bất kỳ địa chỉ nào.
  + Kiểu `int` (4 bytes): Bắt buộc phải bắt đầu ở địa chỉ chia hết cho 4.
- Trình biên dịch sẽ tự động chèn thêm các byte trống không dùng đến (gọi là Padding Bytes) vào giữa các thành viên để đáp ứng quy tắc căn chỉnh phần cứng này.

2. Phân tích `sizeof(struct Example)` ban đầu (12 bytes):
- Offset 0: `char a` (1 byte).
- Offset 1..3: Trình biên dịch chèn thêm 3 padding bytes để đưa địa chỉ tiếp theo về offset 4 (chia hết cho 4).
- Offset 4..7: `int b` (4 bytes).
- Offset 8: `char c` (1 byte).
- Offset 9..11: Trình biên dịch chèn thêm 3 padding bytes vào cuối để tổng kích thước struct là bội số của thành viên lớn nhất (bội số của 4).
-> Tổng kích thước: $1 + 3 (pad) + 4 + 1 + 3 (pad) = 12\text{ bytes}$.

3. Cách tối ưu hóa thứ tự khai báo:
Gom các biến có kích thước nhỏ lại cạnh nhau:
```c
struct Optimized {
    char a;   // 1 byte (offset 0)
    char c;   // 1 byte (offset 1)
    // Chèn 2 padding bytes tại offset 2..3
    int b;    // 4 bytes (offset 4..7)
};
```
-> Kích thước mới giảm xuống còn đúng **8 bytes** (tiết kiệm được 33.3% dung lượng RAM).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích bản chất phần cứng và quy tắc Natural Alignment** | **3.5đ** | Nêu rõ CPU đọc theo khối Word và yêu cầu địa chỉ là bội số kích thước kiểu dữ liệu. |
| **Phân tích chi tiết từng byte offset dẫn đến kích thước 12 bytes** | **3.5đ** | Mô tả chuẩn xác 3 byte đệm sau char a và 3 byte đệm cuối cùng sau char c. |
| **Đề xuất tái cấu trúc struct giảm xuống 8 bytes** | **3.0đ** | Gom char a và char c lại cạnh nhau, chỉ còn 2 padding bytes, tổng kích thước 8 bytes. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 31. [PRF_PQ31] Mảng các cấu trúc (Array of Structs): Quản lý danh sách sinh viên và sắp xếp theo điểm

- **Mã định danh (Question Code):** `PRF_PQ31`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO7** (`00000000-0000-0000-0000-000000000467`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 42, 45 — *Array of Structs & Sorting Algorithm*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng quản lý mảng các đối tượng phức tạp và cài đặt thuật toán sắp xếp dựa trên trường dữ liệu struct.

#### ❓ Câu hỏi vấn đáp:
> "Hãy định nghĩa cấu trúc `Student` gồm: mã sinh viên (chuỗi), họ tên (chuỗi), điểm GPA (số thực). Viết một hàm trong C nhận vào một mảng N sinh viên và sắp xếp danh sách sinh viên theo thứ tự điểm GPA giảm dần (nếu điểm bằng nhau thì sắp xếp theo tên từ điển A-Z)."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi hoán đổi hai phần tử trong mảng struct, ta nên hoán đổi từng trường riêng biệt hay hoán đổi toàn bộ biến struct?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Định nghĩa cấu trúc `Student`:
```c
#include <stdio.h>
#include <string.h>

typedef struct {
    char id[20];
    char name[50];
    float gpa;
} Student;
```

2. Cài đặt hàm sắp xếp mảng struct:
Trong C, phép gán giữa hai biến struct cùng kiểu `Student temp = a[i]; a[i] = a[j]; a[j] = temp;` được hỗ trợ nguyên bản và tự động sao chép toàn bộ khối byte dữ liệu của struct:
```c
void sortStudents(Student list[], int n) {
    for (int i = 0; i < n - 1; i++) {
        for (int j = i + 1; j < n; j++) {
            // Điều kiện 1: GPA của j lớn hơn GPA của i (sắp giảm dần)
            // Điều kiện 2: GPA bằng nhau thì so sánh tên tăng dần theo từ điển
            int needSwap = 0;
            if (list[j].gpa > list[i].gpa) {
                needSwap = 1;
            } else if (list[j].gpa == list[i].gpa) {
                if (strcmp(list[j].name, list[i].name) < 0) {
                    needSwap = 1;
                }
            }

            if (needSwap) {
                Student temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}
```

3. Phân tích tối ưu:
Nên hoán đổi trực tiếp toàn bộ biến struct thông qua biến tạm `Student temp` vì mã nguồn ngắn gọn, an toàn và trình biên dịch sử dụng lệnh copy bộ nhớ tương đương `memcpy`. Với các struct có kích thước rất lớn (> 1KB), ta nên sắp xếp một mảng con trỏ `Student *ptrs[N]` để chỉ hoán đổi địa chỉ 8 bytes thay vì copy toàn bộ nội dung.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa đúng struct Student và trường dữ liệu** | **2.5đ** | Khai báo chuẩn typedef struct với mảng chuỗi id, name và số thực gpa. |
| **Cài đặt thuật toán sắp xếp đa tiêu chí (GPA & Name)** | **4.5đ** | Xử lý chính xác GPA giảm dần và kết hợp strcmp xử lý tie-breaker tên A-Z. |
| **Kỹ thuật hoán đổi struct và phân tích hiệu năng** | **3.0đ** | Nêu rõ phép gán struct nguyên khối và gợi ý dùng mảng con trỏ cho struct lớn. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 32. [PRF_PQ32] Phân tích bản chất lỗi tràn bộ đệm (Buffer Overflow) trên Call Stack và cách khai thác

- **Mã định danh (Question Code):** `PRF_PQ32`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO8** | **DB 3NF CLO8** (`00000000-0000-0000-0000-000000000468`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 47 — *Stack-based Buffer Overflow Analysis*
- **Mục tiêu đánh giá:** Đánh giá nhận thức an ninh phần mềm chuyên sâu về cơ chế ghi đè Call Stack và các biện pháp bảo vệ của trình biên dịch.

#### ❓ Câu hỏi vấn đáp:
> "Lỗi tràn bộ đệm trên ngăn xếp (Stack-based Buffer Overflow) là gì? Hãy vẽ hoặc mô tả cấu trúc bố trí ô nhớ của một Stack Frame (chứa Local buffer, Saved Frame Pointer EBP/RBP, Return Address EIP/RIP). Kẻ tấn công có thể khai thác lỗ hổng này để chiếm quyền điều khiển hệ thống như thế nào?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Cơ chế Stack Canary (Stack Guard) trong các trình biên dịch hiện đại như GCC hoạt động như thế nào để phát hiện tràn bộ đệm?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Bản chất Stack-based Buffer Overflow:
Xảy ra khi một hàm ghi dữ liệu vào một mảng đệm cục bộ (Local Buffer) trên Stack vượt quá kích thước được cấp phát mà không có cơ chế kiểm tra giới hạn (Bounds Checking), ví dụ sử dụng các hàm không an toàn như `gets`, `strcpy`, `sprintf`.

2. Bố trí ô nhớ của một Stack Frame (từ địa chỉ thấp đến cao):
```
[ Địa chỉ thấp ]
  [ Local Buffer (mảng ký tự) ]  <-- Bắt đầu ghi từ đây
  [ Biến cục bộ khác ]
  [ Saved Frame Pointer (EBP / RBP) ] (4 hoặc 8 bytes)
  [ Return Address (EIP / RIP) ]      (4 hoặc 8 bytes) <-- Mục tiêu tấn công!
  [ Các tham số truyền vào hàm ]
[ Địa chỉ cao ]
```

3. Kịch bản khai thác của kẻ tấn công:
- Vì mảng đệm ghi dữ liệu từ địa chỉ thấp lên địa chỉ cao, nếu kẻ tấn công nhập một chuỗi dài có chủ đích:
  + Dữ liệu sẽ ghi tràn qua mảng đệm.
  + Ghi đè lên Saved EBP.
  + Ghi đè trực tiếp lên ô nhớ lưu `Return Address` bằng địa chỉ của một đoạn mã độc (Shellcode) đã được nhồi sẵn vào bộ nhớ.
- Khi hàm con kết thúc và thực hiện lệnh `RET` (Return), CPU sẽ nạp địa chỉ vừa bị ghi đè vào thanh ghi con trỏ lệnh (`EIP`/`RIP`), khiến luồng thực thi nhảy thẳng sang thực thi mã độc với quyền hạn của tiến trình.

4. Cơ chế phòng thủ:
- Thay thế 100% bằng các hàm an toàn có giới hạn kích thước (`fgets`, `strncpy`, `snprintf`).
- Kích hoạt cơ chế Stack Canary của trình biên dịch (GCC `-fstack-protector`): Chèn một giá trị bí mật (Canary) ngay trước Return Address, kiểm tra trước khi return; nếu Canary bị đổi, lập tức terminate chương trình.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa và nguyên nhân gây lỗi Stack Buffer Overflow** | **3.0đ** | Nêu rõ ghi vượt dung lượng buffer cục bộ trên Stack do thiếu bounds checking. |
| **Mô tả cấu trúc bố trí Stack Frame từ địa chỉ thấp lên cao** | **3.5đ** | Vẽ/mô tả đúng thứ tự: Buffer -> Saved EBP -> Return Address EIP/RIP. |
| **Phân tích cơ chế chiếm quyền điều khiển và Stack Canary** | **3.5đ** | Giải thích ghi đè Return Address để chuyển hướng con trỏ lệnh CPU và cơ chế Canary. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 33. [PRF_PQ33] Phân biệt tệp văn bản (Text File) và tệp nhị phân (Binary File): Cơ chế mã hóa và hiệu năng

- **Mã định danh (Question Code):** `PRF_PQ33`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO9** | **DB 3NF CLO9** (`00000000-0000-0000-0000-000000000469`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 53 — *File I/O: Text vs Binary Files*
- **Mục tiêu đánh giá:** Kiểm tra sự hiểu biết sâu sắc về sự khác nhau giữa lưu trữ văn bản ký tự và lưu trữ nhị phân trực tiếp từ RAM xuống đĩa.

#### ❓ Câu hỏi vấn đáp:
> "Hãy so sánh toàn diện giữa Tệp văn bản (Text File) và Tệp nhị phân (Binary File) trong ngôn ngữ C về: Cơ chế mã hóa dữ liệu trên đĩa, Kích thước lưu trữ (lấy ví dụ số nguyên 123456789), Tốc độ đọc/ghi I/O, và Khả năng đọc hiểu trực tiếp bằng mắt thường."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trên hệ điều hành Windows, chế độ mở file văn bản tự động chuyển đổi ký tự xuống dòng '\n' thành chuỗi byte nào?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
So sánh chi tiết giữa Text File và Binary File:

1. Cơ chế mã hóa dữ liệu:
- Text File: Dữ liệu được chuyển đổi thành chuỗi các ký tự con người đọc được theo bảng mã ASCII/UTF-8. Các số phải được phân giải (format) thành từng chữ số ký tự.
- Binary File: Dữ liệu được sao chép nguyên vẹn luồng byte nhị phân (Raw bytes) trực tiếp từ bộ nhớ RAM xuống đĩa cứng mà không qua bất kỳ bước chuyển đổi nào.

2. So sánh kích thước lưu trữ (Ví dụ số nguyên `int n = 123456789`):
- Trong Text File: Số này gồm 9 chữ số, mỗi chữ số là 1 ký tự ASCII chiếm 1 byte. Do đó tốn **9 bytes** trên đĩa.
- Trong Binary File: Số này lưu nguyên dạng 4 bytes của kiểu `int`. Do đó chỉ tốn đúng **4 bytes** trên đĩa (tiết kiệm hơn 55% dung lượng).

3. Tốc độ đọc/ghi (Hiệu năng I/O):
- Text File: Chậm hơn đáng kể vì CPU phải tốn thời gian phân tích cú pháp (Parsing): Hàm `fprintf` phải chuyển số sang chuỗi, hàm `fscanf` phải đọc từng ký tự và nhân cơ số 10 để dựng lại số nguyên.
- Binary File: Nhanh vượt trội vì sử dụng hàm `fread` và `fwrite` để sao chép nguyên khối byte bằng cơ chế DMA (Direct Memory Access) mà không cần tính toán format.

4. Khả năng đọc hiểu và tính tương thích nền tảng:
- Text File: Dễ đọc và sửa đổi bằng các trình soạn thảo thông thường (Notepad), ít bị ảnh hưởng bởi kiến trúc vi xử lý Endianness.
- Binary File: Không thể đọc trực tiếp bằng mắt thường (hiển thị ký tự rác khi mở bằng Notepad), có thể bị phụ thuộc vào thứ tự byte (Little-endian vs Big-endian) của CPU.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Cơ chế mã hóa ký tự ASCII vs Raw bytes** | **3.0đ** | Nêu rõ văn bản phải định dạng chuỗi ký tự, nhị phân ghi trực tiếp khối byte RAM. |
| **Ví dụ so sánh kích thước cụ thể 9 bytes vs 4 bytes** | **3.5đ** | Phân tích rõ ràng số 123456789 tốn 9 bytes ký tự so với 4 bytes nhị phân int. |
| **Hiệu năng I/O (Parsing overhead) và tính tương thích** | **3.5đ** | Chỉ ra overhead của fprintf/fscanf so với tốc độ raw của fread/fwrite và vấn đề Endianness. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 34. [PRF_PQ34] Kỹ thuật lưu trữ và phục hồi mảng Struct bằng hàm fread và fwrite trên File nhị phân

- **Mã định danh (Question Code):** `PRF_PQ34`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO9** | **DB 3NF CLO9** (`00000000-0000-0000-0000-000000000469`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 55, 57 — *Binary File I/O: fread & fwrite with Structs*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng đọc ghi cấu trúc dữ liệu phức hợp ra file nhị phân bằng một lệnh duy nhất.

#### ❓ Câu hỏi vấn đáp:
> "Hãy trình bày cú pháp và ý nghĩa các tham số của hai hàm fread() và fwrite() trong thư viện stdio.h. Viết một đoạn mã C hoàn chỉnh thực hiện ghi một mảng gồm N cấu trúc `Student` vào tệp nhị phân 'students.dat' và sau đó đọc ngược lại mảng đó vào RAM."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu trong struct Student có một trường kiểu con trỏ `char *notes`, việc dùng fwrite ghi struct này ra file có an toàn không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cú pháp và ý nghĩa tham số của `fread` và `fwrite`:
- `size_t fwrite(const void *ptr, size_t size, size_t count, FILE *stream);`
- `size_t fread(void *ptr, size_t size, size_t count, FILE *stream);`
  + `ptr`: Con trỏ trỏ tới vùng nhớ đệm chứa dữ liệu nguồn (khi ghi) hoặc vùng nhớ nhận dữ liệu (khi đọc).
  + `size`: Kích thước tính bằng byte của mỗi phần tử cần đọc/ghi (thường dùng `sizeof(...)`).
  + `count`: Số lượng phần tử cần thao tác.
  + `stream`: Con trỏ tệp `FILE*` đã mở ở chế độ nhị phân (`"wb"` hoặc `"rb"`).
  + Giá trị trả về: Số lượng phần tử đã đọc/ghi thành công. Nếu khác `count`, có thể đã chạm đáy tệp (EOF) hoặc gặp lỗi I/O.

2. Đoạn mã C hoàn chỉnh ghi và đọc mảng Struct:
```c
#include <stdio.h>

typedef struct {
    int id;
    char name[50];
    float gpa;
} Student;

void saveAndLoad(Student list[], int n) {
    // 1. Ghi mảng struct ra file nhị phân
    FILE *fp = fopen("students.dat", "wb");
    if (fp != NULL) {
        fwrite(list, sizeof(Student), n, fp);
        fclose(fp);
    }

    // 2. Đọc lại mảng struct từ file vào RAM
    Student loaded[100];
    fp = fopen("students.dat", "rb");
    if (fp != NULL) {
        size_t read_count = fread(loaded, sizeof(Student), n, fp);
        printf("Da doc thanh cong %zu sinh vien.\n", read_count);
        fclose(fp);
    }
}
```

3. Cảnh báo an toàn về trường con trỏ trong struct:
TUYỆT ĐỐI KHÔNG an toàn! Nếu struct chứa trường con trỏ (như `char *notes`), `fwrite` chỉ ghi giá trị ĐỊA CHỈ Ô NHỚ (con trỏ 8 bytes) xuống đĩa, chứ KHÔNG hề ghi nội dung chuỗi mà nó trỏ tới. Khi đọc lại ở phiên chạy khác, địa chỉ đó đã vô hiệu, dẫn đến lỗi Dangling Pointer và crash chương trình.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Đặc tả đúng 4 tham số của fread và fwrite** | **3.0đ** | Giải thích chính xác ý nghĩa của ptr, size, count, stream và giá trị trả về. |
| **Viết mã nguồn C đọc ghi mảng struct an toàn** | **4.0đ** | Code mở file chế độ wb/rb, kiểm tra con trỏ NULL và đóng file bằng fclose đầy đủ. |
| **Cảnh báo bẫy con trỏ trong struct khi ghi nhị phân** | **3.0đ** | Phân tích sâu lỗi chỉ ghi địa chỉ RAM vô giá trị khi struct chứa biến con trỏ. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 35. [PRF_PQ35] Truy cập ngẫu nhiên trên File với fseek, ftell, rewind và bẫy lỗi vòng lặp feof()

- **Mã định danh (Question Code):** `PRF_PQ35`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO9** | **DB 3NF CLO9** (`00000000-0000-0000-0000-000000000469`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 54-56 — *Random File Access & feof Pitfalls*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng điều khiển con trỏ tệp ngẫu nhiên và hiểu biết về cơ chế hoạt động của cờ EOF.

#### ❓ Câu hỏi vấn đáp:
> "Hãy giải thích công dụng của các hàm định vị con trỏ tệp trong C: fseek(), ftell() và rewind() cùng 3 hằng số định vị (SEEK_SET, SEEK_CUR, SEEK_END). Tại sao việc sử dụng vòng lặp `while(!feof(fp))` để đọc file thường dẫn đến lỗi đọc thừa bản ghi cuối cùng hai lần?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Làm thế nào để xác định dung lượng (kích thước byte) của một tệp tin bằng cách kết hợp fseek và ftell?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Các hàm định vị con trỏ tệp (Random Access):
- `int fseek(FILE *stream, long offset, int whence)`: Di chuyển con trỏ tệp đến vị trí mới dựa trên mốc `whence`:
  + `SEEK_SET`: Tính từ đầu tệp (offset >= 0).
  + `SEEK_CUR`: Tính từ vị trí hiện tại của con trỏ tệp.
  + `SEEK_END`: Tính từ cuối tệp (thường dùng offset <= 0).
- `long ftell(FILE *stream)`: Trả về vị trí byte hiện tại của con trỏ tệp tính từ đầu tệp.
- `void rewind(FILE *stream)`: Đưa con trỏ tệp trở về ngay đầu tệp (tương đương `fseek(stream, 0, SEEK_SET)` và xóa cờ lỗi).

*Kỹ thuật đo dung lượng tệp tin:*
```c
fseek(fp, 0, SEEK_END);
long fileSize = ftell(fp);
rewind(fp);
```

2. Bẫy lỗi đọc thừa bản ghi khi lạm dụng `while(!feof(fp))`:
- Nguyên nhân: Hàm `feof(fp)` CHỈ trả về `true` (khác 0) SAU KHI một thao tác đọc (như `fread`, `fscanf`) ĐÃ CỐ ĐỌC VƯỢT QUA ĐÁY TỆP VÀ BỊ THẤT BẠI.
- Kịch bản lỗi:
  + Khi vừa đọc xong bản ghi cuối cùng của file, con trỏ tệp đang đứng ở đáy tệp nhưng cờ EOF CHƯA ĐƯỢC BẬT.
  + Vòng lặp `while(!feof(fp))` vẫn đánh giá là đúng và tiếp tục lặp thêm một lần nữa.
  + Lệnh đọc kế tiếp cố đọc nhưng không có dữ liệu (thất bại). Tuy nhiên, biến lưu trữ vẫn giữ nguyên giá trị cũ của lần lặp trước đó.
  + Kết quả: Dữ liệu của bản ghi cuối cùng bị xử lý hoặc in ra màn hình 2 lần!
- Giải pháp chuẩn công nghiệp: Kiểm tra trực tiếp giá trị trả về của hàm đọc:
  `while (fscanf(fp, "%d", &x) == 1) { ... }` hoặc `while (fread(&item, sizeof(Item), 1, fp) == 1) { ... }`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Đặc tả hàm fseek, ftell, rewind và 3 hằng số mốc** | **3.5đ** | Mô tả chuẩn chức năng, SEEK_SET, SEEK_CUR, SEEK_END và code đo dung lượng file. |
| **Giải thích cơ chế kích hoạt cờ EOF của feof** | **3.5đ** | Phân tích rõ feof chỉ bật sau khi đọc hỏng ở đáy tệp, dẫn đến lặp thừa 1 lần. |
| **Đề xuất giải pháp kiểm tra giá trị trả về của hàm đọc** | **3.0đ** | Viết đúng điều kiện vòng lặp while(fread == 1) hoặc while(fscanf == 1) chuẩn mực. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 36. [PRF_PQ36] Con trỏ hàm (Function Pointer) và cơ chế Callback trong hàm thư viện chuẩn qsort

- **Mã định danh (Question Code):** `PRF_PQ36`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO6** | **DB 3NF CLO6** (`00000000-0000-0000-0000-000000000466`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 32 — *Function Pointers & qsort Callback*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức nâng cao về con trỏ hàm, nguyên lý Callback và áp dụng vào hàm sắp xếp tổng quát qsort.

#### ❓ Câu hỏi vấn đáp:
> "Con trỏ hàm (Function Pointer) trong C là gì và cú pháp khai báo như thế nào? Hãy giải thích cơ chế Callback và cách hàm thư viện chuẩn qsort() trong stdlib.h sử dụng con trỏ hàm so sánh `int (*compare)(const void*, const void*)` để sắp xếp dữ liệu tổng quát."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao tham số trong hàm so sánh của qsort lại là `const void*` và làm sao để lấy giá trị số nguyên từ nó?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Bản chất của Con trỏ hàm (Function Pointer) và cơ chế Callback:
- Con trỏ hàm là biến lưu trữ địa chỉ bắt đầu của đoạn mã máy (machine code) của một hàm trong vùng nhớ Text/Code Segment.
- Cơ chế Callback: Hàm thư viện chuẩn `qsort` trong `<stdlib.h>` được thiết kế tổng quát để sắp xếp mảng thuộc bất kỳ kiểu dữ liệu nào (`int`, `double`, `struct`). Vì `qsort` không biết trước quy tắc so sánh hai phần tử cụ thể, nó nhận vào một con trỏ hàm `compar`. Ở mỗi bước phân hoạch, `qsort` sẽ "gọi ngược" (callback) hàm `compar` này để quyết định thứ tự giữa hai phần tử.
- Nguyên mẫu hàm `qsort`:
  `void qsort(void *base, size_t nmemb, size_t size, int (*compar)(const void *, const void *));`

2. Cài đặt hàm so sánh `compareInts` chuẩn công nghiệp (Chống tràn số nguyên có dấu - Signed Integer Overflow):
```c
#include <stdio.h>
#include <stdlib.h>

int compareInts(const void *a, const void *b) {
    int val_a = *(const int *)a;
    int val_b = *(const int *)b;
    // TUYỆT ĐỐI KHÔNG DÙNG: return (val_a - val_b); vì gây Signed Integer Overflow (Undefined Behavior)
    // khi hiệu vượt quá INT_MAX (ví dụ val_a = -2000000000, val_b = 1000000000).
    return (val_a > val_b) - (val_a < val_b);
}

int main(void) {
    int arr[] = {1000000000, -2000000000, 42, -15, 0};
    size_t n = sizeof(arr) / sizeof(arr[0]);
    qsort(arr, n, sizeof(int), compareInts);
    for (size_t i = 0; i < n; i++) {
        printf("%d ", arr[i]);
    }
    return 0;
}
```

3. Tại sao phép trừ `return (val_a - val_b);` là lỗi Undefined Behavior nghiêm trọng:
- Trong chuẩn C99/C11, tràn số nguyên có dấu (`Signed Integer Overflow`) là **Undefined Behavior**.
- Ví dụ: Nếu `val_a = -2,000,000,000` và `val_b = 1,000,000,000`, hiệu toán học là `-3,000,000,000` vượt quá giới hạn âm của kiểu `int` 32-bit (`INT_MIN = -2,147,483,648`). Trên kiến trúc bù 2, phép trừ bị tràn vòng (wrap-around) thành số dương `+1,294,967,296 > 0`, khiến `qsort` kết luận sai lầm rằng `-2 tỷ > +1 tỷ` và sắp xếp mảng sai hoàn toàn!
- Biểu thức chuẩn `(val_a > val_b) - (val_a < val_b)` chỉ thực hiện phép so sánh quan hệ trả về `0` hoặc `1`, do đó kết quả luôn nằm trong tập `{-1, 0, +1}` và miễn nhiễm 100% với lỗi tràn số trên mọi dải giá trị `int`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích bản chất Con trỏ hàm và cơ chế Callback trong qsort** | **3.5đ** | Trình bày rõ con trỏ hàm lưu địa chỉ mã lệnh và cách qsort gọi ngược hàm so sánh qua đối số con trỏ hàm. |
| **Cài đặt đúng hàm compareInts ép kiểu const void* an toàn** | **3.5đ** | Ép kiểu chính xác từ const void* sang const int* và giải tham chiếu lấy giá trị. |
| **Phân tích rủi ro Signed Integer Overflow và viết biểu thức so sánh an toàn** | **3.0đ** | Giải thích rõ vì sao (val_a - val_b) gây tràn số UB khi gần INT_MIN/INT_MAX và dùng (val_a > val_b) - (val_a < val_b). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

## PHẦN 4: CHI TIẾT 26 CÂU HỎI KHO THI THẬT PHÒNG LAB KIOSK (`exam_questions`)

> Kho câu hỏi này được cô lập hoàn toàn trong bảng `exam_questions` theo Quy tắc R8, chỉ phục vụ cho kỳ thi vấn đáp chính thức tại phòng Lab Kiosk (**MF-04**), bám sát Ma trận Cấu trúc Đề thi (`exam_structures`) do Trưởng Bộ Môn ban hành.

### 1. [PRF_EQ01] Quy chuẩn chú thích mã nguồn C (// vs /* */) và cơ chế xử lý của Preprocessor (CQ2.2)

- **Mã định danh (Question Code):** `PRF_EQ01`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO1** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000461`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 2 — *Code Documentation, Comments (CQ2.2) & Preprocessor Stripping*
- **Mục tiêu đánh giá:** Đánh giá việc nắm vững quy tắc viết chú thích mã nguồn C theo câu hỏi Constructive Question CQ2.2 trong Syllabus và cách bộ tiền xử lý xử lý comment.

#### ❓ Câu hỏi vấn đáp:
> "Trình bày hai cú pháp viết chú thích (comment) trong ngôn ngữ C (`/* ... */` và `//`). Tại sao chuẩn C không cho phép lồng các khối chú thích `/* ... */` vào nhau, và bộ tiền xử lý (Preprocessor) thay thế mỗi khối chú thích bằng gì trước khi biên dịch?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu bạn cần tạm thời vô hiệu hóa (comment out) một đoạn mã nguồn lớn bên trong đã có sẵn nhiều chú thích dạng `/* ... */`, bạn nên dùng chỉ thị tiền xử lý nào thay thế để không bị lỗi cú pháp?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Hai cú pháp chú thích (Comment) trong ngôn ngữ C:
- **Chú thích khối nhiều dòng (`/* ... */` - Block Comment):** Có từ chuẩn K&R C / ANSI C89. Bắt đầu bằng cặp ký tự `/*` và kết thúc ngay khi gặp cặp ký tự `*/` đầu tiên. Có thể trải dài trên nhiều dòng hoặc đặt xen giữa một dòng lệnh.
- **Chú thích một dòng (`//` - Single-line Comment):** Được chuẩn hóa chính thức vào C từ chuẩn **C99**. Bắt đầu từ `//` và kéo dài đến hết dòng hiện tại (gặp ký tự xuống dòng `\n`).

2. Tại sao C cấm lồng khối chú thích `/* ... */` (No Nested Block Comments):
- Bộ phân tích từ vựng (Lexer) của C không đếm độ sâu mở/đóng của `/*`. Ngay khi đang ở trong trạng thái comment `/*`, nó sẽ bỏ qua mọi ký tự (kể cả các dấu `/*` bên trong) và **kết thúc khối chú thích ngay tại dấu `*/` đầu tiên** nó bắt gặp.
- Hậu quả: Phần mã lệnh nằm sau dấu `*/` đầu tiên cho tới dấu `*/` cuối cùng sẽ lộ ra ngoài và gây **Lỗi biên dịch (Compile Error)** tại dấu `*/` dư thừa ở cuối.
- Giải pháp chuẩn công nghiệp khi muốn vô hiệu hóa một khối code lớn có chứa sẵn `/* ... */`: Sử dụng chỉ thị tiền xử lý điều kiện `#if 0 ... #endif` vì các chỉ thị `#if` hỗ trợ lồng nhau hoàn hảo.

3. Cơ chế xử lý chú thích của Bộ tiền xử lý (Translation Phase 3):
- Trong giai đoạn Tiền xử lý (Preprocessing), mỗi chú thích **KHÔNG bị xóa trắng hoàn toàn**, mà được thay thế bằng **đúng một ký tự khoảng trắng đơn (`' '`)**.
- Ví dụ: `int/*comment*/x = 10;` sẽ được biến đổi thành `int x = 10;` (hợp lệ), chứ không bị dính liền thành `intx = 10;`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân biệt 2 kiểu chú thích /* ... */ (C89) và // (C99)** | **3.0đ** | Trình bày rõ cú pháp, phạm vi dòng và chuẩn C áp dụng cho block comment và single-line comment. |
| **Giải thích nguyên nhân lỗi khi lồng khối /* ... */ và giải pháp #if 0** | **4.0đ** | Chỉ rõ Lexer đóng comment ngay tại dấu */ đầu tiên gây lỗi cú pháp và nêu giải pháp dùng #if 0 ... #endif. |
| **Cơ chế Preprocessor thay thế comment bằng 1 khoảng trắng** | **3.0đ** | Khẳng định ở giai đoạn Preprocessing mỗi comment được thay bằng 1 khoảng trắng đơn tránh dính từ khóa. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 2. [PRF_EQ02] Các toán tử thao tác Bit (Bitwise Operators) và kỹ thuật mặt nạ Bitmask trong C

- **Mã định danh (Question Code):** `PRF_EQ02`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000462`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 7 — *Bitwise Operators (&, |, ^, ~, <<, >>) & Bitmasking*
- **Mục tiêu đánh giá:** Đánh giá khả năng ghi nhớ và phân biệt 6 toán tử thao tác trên bit với các toán tử logic, cùng công thức bật/tắt/đảo/kiểm tra bit thứ k.

#### ❓ Câu hỏi vấn đáp:
> "Hãy liệt kê 6 toán tử thao tác bit (Bitwise Operators: `&`, `|`, `^`, `~`, `<<`, `>>`) trong ngôn ngữ C và phân biệt sự khác nhau căn bản giữa toán tử `&`, `|` với toán tử logic `&&`, `||`. Viết biểu thức C sử dụng mặt nạ bit (Bitmask) để bật (set), tắt (clear), đảo (toggle) và kiểm tra (check) bit thứ `k` của một số nguyên không dấu `unsigned int flags`."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao trong ngôn ngữ C, việc thực hiện phép dịch phải `>>` trên một số nguyên âm có dấu (`signed int`) lại phụ thuộc vào trình biên dịch (Implementation-defined behavior), và làm thế nào để đảm bảo phép dịch phải luôn là dịch logic (chèn bit 0)?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Danh mục 6 toán tử thao tác bit (Bitwise Operators) trong C:
- `&` (Bitwise AND): Kết quả bit bằng `1` khi và chỉ khi cả 2 bit tương ứng đều bằng `1`.
- `|` (Bitwise OR): Kết quả bit bằng `1` khi có ít nhất 1 trong 2 bit tương ứng bằng `1`.
- `^` (Bitwise XOR): Kết quả bit bằng `1` khi 2 bit tương ứng khác nhau (`0 ^ 1 = 1`), bằng `0` khi giống nhau (`x ^ x = 0`).
- `~` (Bitwise NOT / One's Complement): Đảo ngược toàn bộ từng bit (`0` thành `1`, `1` thành `0`).
- `<<` (Left Shift): Dịch các bit sang trái `k` vị trí, chèn `0` vào bên phải (tương đương nhân với $2^k$ nếu không tràn số).
- `>>` (Right Shift): Dịch các bit sang phải `k` vị trí (trên kiểu `unsigned`, luôn chèn bit `0` vào bên trái — tương đương chia nguyên cho $2^k$).

2. Phân biệt Bitwise (`&`, `|`) với Logical (`&&`, `||`):
- **Đơn vị thao tác & Kết quả:** `&` và `|` tính toán độc lập trên từng cặp bit nhị phân của hai toán hạng nguyên và trả về một giá trị nguyên mới; trong khi `&&` và `||` đánh giá tính đúng/sai của toàn bộ biểu thức và chỉ trả về `0` hoặc `1`.
- **Cơ chế đoản mạch (Short-circuit):** `&&` và `||` có tính chất đoản mạch (dừng đánh giá vế phải ngay khi vế trái đã quyết định kết quả); ngược lại `&` và `|` **luôn luôn tính toán cả hai toán hạng**.

3. Bốn công thức mặt nạ bit (Bitmask) kinh điển cho bit thứ `k` ($0 \le k < 32$):
```c
unsigned int flags = 0U;
// 1. Bật (Set) bit thứ k lên 1:
flags |= (1U << k);
// 2. Tắt (Clear) bit thứ k về 0:
flags &= ~(1U << k);
// 3. Đảo trạng thái (Toggle) bit thứ k:
flags ^= (1U << k);
// 4. Kiểm tra (Check) bit thứ k có bằng 1 hay không:
int is_set = (flags & (1U << k)) != 0U;
```

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Liệt kê đúng 6 toán tử Bitwise (&, |, ^, ~, <<, >>)** | **3.0đ** | Trình bày chính xác chức năng của cả 6 toán tử thao tác bit trong C. |
| **Phân biệt rõ Bitwise (&, |) với Logical (&&, ||)** | **3.0đ** | Chỉ rõ sự khác biệt về thao tác mức bit vs mức giá trị boolean và cơ chế đoản mạch Short-circuit. |
| **Viết đúng 4 biểu thức Bitmask (Set, Clear, Toggle, Check bit k)** | **4.0đ** | Sử dụng đúng hằng không dấu (1U << k) kết hợp |=, &= ~, ^= và & để thao tác bit thứ k. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 3. [PRF_EQ03] Danh mục và mức ưu tiên của các toán tử số học, logic và quan hệ trong C

- **Mã định danh (Question Code):** `PRF_EQ03`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000462`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 7-8 — *Operator Precedence & Associativity Table*
- **Mục tiêu đánh giá:** Đánh giá khả năng ghi nhớ và áp dụng bảng thứ tự ưu tiên (Precedence) và tính kết hợp (Associativity) của các toán tử C.

#### ❓ Câu hỏi vấn đáp:
> "Hãy sắp xếp các nhóm toán tử sau theo thứ tự ưu tiên từ cao nhất đến thấp nhất trong C: Toán tử gán (=, +=), Toán tử số học nhân chia (*, /, %), Toán tử logic AND/OR (&&, ||), Toán tử dấu ngoặc đơn (), Toán tử số học cộng trừ (+, -), Toán tử quan hệ (<, <=, >, >=), và Toán tử so bằng (==, !=)."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Hầu hết các toán tử trong C có tính kết hợp từ trái sang phải, nhưng toán tử nào lại có tính kết hợp từ phải sang trái?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Thứ tự ưu tiên các toán tử trong C từ cao nhất đến thấp nhất:
1. Nhóm 1 (Cao nhất): Dấu ngoặc đơn `()` và truy xuất mảng/thành viên `[]`, `.`, `->`. (Tính kết hợp: Trái qua phải).
2. Nhóm 2: Toán tử một ngôi (Unary): Tiền tố `++`, `--`, phủ định `!`, lấy địa chỉ `&`, giải tham chiếu `*`, ép kiểu `(type)`, `sizeof`. (Tính kết hợp: Phải qua trái).
3. Nhóm 3: Toán tử số học nhân, chia, lấy dư: `*`, `/`, `%`. (Tính kết hợp: Trái qua phải).
4. Nhóm 4: Toán tử số học cộng, trừ: `+`, `-`. (Tính kết hợp: Trái qua phải).
5. Nhóm 5: Toán tử dịch bit: `<<`, `>>`. (Tính kết hợp: Trái qua phải).
6. Nhóm 6: Toán tử quan hệ thứ tự: `<`, `<=`, `>`, `>=`. (Tính kết hợp: Trái qua phải).
7. Nhóm 7: Toán tử quan hệ so bằng: `==`, `!=`. (Tính kết hợp: Trái qua phải).
8. Nhóm 8: Toán tử logic AND: `&&`. (Tính kết hợp: Trái qua phải).
9. Nhóm 9: Toán tử logic OR: `||`. (Tính kết hợp: Trái qua phải).
10. Nhóm 10: Toán tử điều kiện 3 ngôi: `? :`. (Tính kết hợp: Phải qua trái).
11. Nhóm 11 (Thấp nhất): Các toán tử gán: `=`, `+=`, `-=`, `*=`, `/=`, `%=`. (Tính kết hợp: Phải qua trái).

Các toán tử có tính kết hợp từ PHẢI qua TRÁI: Toán tử một ngôi (Unary), Toán tử điều kiện 3 ngôi, và Các toán tử gán.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Sắp xếp đúng 100% thứ tự ưu tiên từ ngoặc () đến gán =** | **4.5đ** | Xếp đúng: () > Nhân/Chia > Cộng/Trừ > Quan hệ < > > So bằng == > && > || > Gán =. |
| **Giải thích tính kết hợp (Associativity) của các toán tử** | **2.5đ** | Trình bày được quy tắc xử lý khi các toán tử có cùng mức ưu tiên trong biểu thức. |
| **Nhận diện chính xác các toán tử kết hợp từ phải qua trái** | **3.0đ** | Chỉ rõ toán tử gán (=, +=), toán tử 3 ngôi (? :) và toán tử 1 ngôi kết hợp từ phải sang trái. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 4. [PRF_EQ04] Định nghĩa và phân loại 4 lớp lưu trữ (Storage Classes): auto, register, static, extern

- **Mã định danh (Question Code):** `PRF_EQ04`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000464`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 22 — *C Storage Classes (auto, register, static, extern)*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức về 4 từ khóa định nghĩa lớp lưu trữ, phạm vi, thời gian sống và cơ chế liên kết trong C.

#### ❓ Câu hỏi vấn đáp:
> "Ngôn ngữ C định nghĩa 4 lớp lưu trữ (Storage Classes) thông qua các từ khóa: auto, register, static và extern. Hãy trình bày ý nghĩa, vùng nhớ lưu trữ và thời gian sống của từng lớp lưu trữ."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Có thể sử dụng toán tử lấy địa chỉ & trên một biến khai báo bằng từ khóa `register` được không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Bốn lớp lưu trữ trong ngôn ngữ C:

1. `auto` (Mặc định cho biến cục bộ):
- Vùng nhớ: Vùng nhớ Ngăn xếp (Stack).
- Thời gian sống: Tự động (Automatic duration) - sinh ra khi vào khối lệnh `{}` và bị hủy khi thoát khối lệnh.
- Phạm vi: Nội bộ khối lệnh (Block scope).

2. `register`:
- Ý nghĩa: Gợi ý cho trình biên dịch lưu trữ biến trực tiếp vào một Thanh ghi CPU (CPU Register) thay vì bộ nhớ RAM để tăng tốc độ truy xuất tối đa (thường dùng cho biến đếm vòng lặp).
- Lưu ý: KHÔNG THỂ sử dụng toán tử lấy địa chỉ `&` trên biến `register` vì thanh ghi CPU không có địa chỉ bộ nhớ RAM. Nếu CPU không còn thanh ghi trống, biến tự động chuyển về vùng nhớ Stack.

3. `static`:
- Vùng nhớ: Phân vùng dữ liệu tĩnh (Data Segment).
- Thời gian sống: Tĩnh (Static duration) - tồn tại suốt toàn bộ vòng đời của chương trình.
- Hai ứng dụng:
  + `static local variable`: Bảo toàn giá trị qua nhiều lần gọi hàm, nhưng chỉ hàm đó truy cập được (Block scope).
  + `static global variable` / `static function`: Giới hạn phạm vi chỉ trong nội bộ tệp tin `.c` hiện tại (Internal Linkage), ngăn không cho các tệp khác thấy.

4. `extern`:
- Ý nghĩa: Khai báo biến toàn cục hoặc hàm được định nghĩa ở một tệp mã nguồn khác (External Linkage), giúp liên kết các tệp trong dự án đa module mà không bị lỗi khai báo trùng lặp.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Đặc tả lớp lưu trữ auto và register** | **3.0đ** | Nêu rõ auto trên Stack và register gợi ý CPU register kèm lưu ý cấm dùng toán tử &. |
| **Đặc tả lớp lưu trữ static (cục bộ và toàn cục)** | **4.0đ** | Phân tích bảo toàn giá trị suốt vòng đời và giới hạn Internal Linkage trong file .c. |
| **Đặc tả lớp lưu trữ extern và liên kết đa module** | **3.0đ** | Giải thích đúng vai trò chia sẻ biến/hàm toàn cục xuyên qua các tệp nguồn khác nhau. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 5. [PRF_EQ05] Phân biệt con trỏ NULL, con trỏ tổng quát void* và con trỏ hoang (Wild Pointer) trong C

- **Mã định danh (Question Code):** `PRF_EQ05`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 28-29 — *Pointer Classifications: NULL Pointer, void* Generic Pointer & Wild Pointer*
- **Mục tiêu đánh giá:** Đánh giá khả năng nhận diện và phân biệt rõ 3 trạng thái/loại con trỏ đặc biệt trong C cùng các quy tắc an toàn khi giải tham chiếu.

#### ❓ Câu hỏi vấn đáp:
> "Hãy định nghĩa và phân biệt rõ sự khác nhau giữa: (1) Con trỏ `NULL` (`NULL Pointer`), (2) Con trỏ kiểu `void*` (`Generic Pointer`), và (3) Con trỏ hoang (`Wild Pointer`). Tại sao trình biên dịch C cấm giải tham chiếu trực tiếp `*ptr` hoặc cộng trừ số học trên biến con trỏ kiểu `void*`?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi khai báo một biến con trỏ cục bộ trong hàm mà chưa có vùng nhớ đích để gán ngay, thói quen khởi tạo nào giúp lập trình viên ngăn chặn triệt để lỗi Wild Pointer?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Phân biệt 3 khái niệm con trỏ cốt lõi trong C:
- **Con trỏ `NULL` (`NULL Pointer`):** Là con trỏ đã được khởi tạo tường minh bằng hằng số `NULL` (thường định nghĩa là `((void *)0)` trong `<stddef.h>`), biểu thị có chủ đích rằng con trỏ hiện **không trỏ tới bất kỳ vùng nhớ hợp lệ nào**. Lập trình viên có thể kiểm tra an toàn qua `if (ptr != NULL)` trước khi truy xuất.
- **Con trỏ tổng quát `void*` (`Generic Pointer`):** Là con trỏ lưu trữ địa chỉ vùng nhớ hợp lệ nhưng **không gắn liền với bất kỳ kiểu dữ liệu cụ thể nào** (ví dụ kiểu trả về của `malloc()` hay tham số của `qsort()`, `memcpy()`). Mọi con trỏ dữ liệu (`int*`, `double*`, `struct Student*`) đều có thể gán sang `void*` và ngược lại.
- **Con trỏ hoang (`Wild Pointer`):** Là biến con trỏ cục bộ được khai báo trên Stack nhưng **chưa được khởi tạo giá trị** (ví dụ `int *p;`). Nó chứa giá trị rác ngẫu nhiên còn sót lại trên RAM. Vì `p != NULL`, câu lệnh kiểm tra `if (p != NULL)` hoàn toàn vô tác dụng, và việc ghi vào `*p = 10;` sẽ gây lỗi nghiêm trọng **Segmentation Fault** hoặc làm hỏng dữ liệu ngẫu nhiên của tiến trình.

2. Tại sao chuẩn C cấm giải tham chiếu `*vptr` và số học con trỏ `vptr + 1` trên `void*`:
- Để giải tham chiếu `*vptr`, CPU cần biết phải đọc bao nhiêu byte kể từ địa chỉ đó (1 byte cho `char`, 4 bytes cho `int`, 8 bytes cho `double`) và giải mã theo định dạng nào.
- Để tính `vptr + 1`, trình biên dịch cần áp dụng công thức địa chỉ: `Address(vptr) + 1 * sizeof(*vptr)`.
- Vì kiểu `void` là kiểu không hoàn chỉnh (incomplete type) không có kích thước xác định, trình biên dịch không biết `sizeof(void)` bằng bao nhiêu. Do đó, bắt buộc phải **ép kiểu tường minh** sang con trỏ có kiểu cụ thể trước khi giải tham chiếu: `*(int *)vptr` hoặc `((char *)vptr) + 1`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa và phân biệt rõ NULL Pointer, void* và Wild Pointer** | **4.0đ** | Trình bày chính xác bản chất từng loại con trỏ và sự nguy hiểm của Wild Pointer chứa địa chỉ rác trên Stack. |
| **Giải thích lý do không thể giải tham chiếu hoặc tính số học trực tiếp trên void*** | **3.5đ** | Phân tích rõ trình biên dịch không biết kích thước sizeof(*vptr) để đọc số byte hoặc nhảy bước địa chỉ. |
| **Nêu đúng cách ép kiểu void* và quy tắc khởi tạo phòng chống Wild Pointer** | **2.5đ** | Viết đúng cú pháp ép kiểu *(int*)vptr và quy tắc luôn khởi tạo int *p = NULL ngay khi khai báo. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 6. [PRF_EQ06] Danh mục các chế độ mở tệp (File Open Modes) trong hàm fopen

- **Mã định danh (Question Code):** `PRF_EQ06`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO9** | **DB 3NF CLO9** (`00000000-0000-0000-0000-000000000469`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 55 — *File Handling: fopen Modes (r, w, a, rb, wb, ab)*
- **Mục tiêu đánh giá:** Kiểm tra trí nhớ và sự phân biệt rõ ràng giữa các chế độ mở file văn bản và nhị phân trong C.

#### ❓ Câu hỏi vấn đáp:
> "Hàm fopen() trong C hỗ trợ những chế độ mở tệp (Mode) cơ bản nào? Hãy phân biệt sự khác nhau giữa ba chế độ văn bản: 'r' (Read), 'w' (Write) và 'a' (Append) về: Hành vi khi tệp đã tồn tại và Hành vi khi tệp chưa tồn tại."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Chữ cái 'b' trong chế độ mở file (như "rb", "wb") có ý nghĩa gì?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Các chế độ mở tệp cơ bản trong hàm `FILE *fopen(const char *filename, const char *mode)`:

1. Chế độ `"r"` (Read - Đọc văn bản):
- Khi tệp đã tồn tại: Mở tệp để đọc dữ liệu, con trỏ tệp đặt ở ngay đầu tệp.
- Khi tệp CHƯA tồn tại: Mở thất bại và hàm trả về con trỏ `NULL`.

2. Chế độ `"w"` (Write - Ghi văn bản mới):
- Khi tệp đã tồn tại: Toàn bộ nội dung cũ của tệp bị XÓA SẠCH (Truncate về 0 bytes), chuẩn bị ghi dữ liệu mới từ đầu.
- Khi tệp CHƯA tồn tại: Hệ điều hành tự động TẠO MỚI một tệp rỗng trên đĩa cứng.

3. Chế độ `"a"` (Append - Ghi nối tiếp vào cuối):
- Khi tệp đã tồn tại: Dữ liệu cũ được BẢO TOÀN NGUYÊN VẸN, con trỏ tệp tự động đặt ở cuối tệp để ghi thêm dữ liệu mới vào đuôi.
- Khi tệp CHƯA tồn tại: Tự động TẠO MỚI một tệp rỗng để bắt đầu ghi.

4. Hậu tố `"b"` (Binary Mode - Chế độ nhị phân):
- Ví dụ: `"rb"`, `"wb"`, `"ab"`.
- Báo hiệu cho thư viện C xử lý tệp theo luồng byte nhị phân thô nguyên bản, ngăn chặn việc hệ thống tự động chuyển đổi ký tự kết thúc dòng `\n` thành cặp byte `\r\n` (CRLF) trên môi trường Windows.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Đặc tả chính xác chế độ đọc 'r'** | **3.0đ** | Nêu rõ mở để đọc từ đầu, trả về NULL nếu file không tồn tại. |
| **Đặc tả chính xác chế độ ghi 'w' và ghi nối 'a'** | **4.0đ** | Phân tích rõ 'w' xóa sạch nội dung cũ còn 'a' giữ nguyên và ghi vào đuôi; cả 2 đều tự tạo file mới. |
| **Ý nghĩa của chế độ nhị phân với hậu tố 'b'** | **3.0đ** | Giải thích đúng cờ 'b' giữ nguyên raw bytes và ngăn chuyển đổi ký tự xuống dòng CRLF. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 7. [PRF_EQ07] Đặc tả định dạng nhập xuất (Format Specifiers) và kiểm soát luồng đệm trong printf / scanf

- **Mã định danh (Question Code):** `PRF_EQ07`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO6** | **DB 3NF CLO6** (`00000000-0000-0000-0000-000000000466`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 6 & 34 — *Formatted I/O Specifiers, Field Width, Scanset & Return Value of scanf*
- **Mục tiêu đánh giá:** Đánh giá mức độ hiểu sâu về các bộ đặc tả định dạng của printf/scanf, ý nghĩa giá trị trả về của scanf và cách chống lỗi trôi lệnh nhập.

#### ❓ Câu hỏi vấn đáp:
> "Hãy giải thích ý nghĩa của các đặc tả định dạng sau trong `printf` và `scanf`: `%-12s`, `%08.2f`, `%19[^\n]`, và sự khác biệt giữa `%f` với `%lf` khi dùng trong `scanf`. Giá trị trả về của hàm `scanf()` có ý nghĩa gì và làm thế nào để kiểm tra người dùng nhập sai kiểu dữ liệu (ví dụ nhập chữ cái khi chương trình yêu cầu số nguyên `%d`)?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao khi gọi `scanf("%d", &age);` rồi gọi tiếp `scanf("%c", &grade);` thì biến `grade` lại tự động nhận ký tự xuống dòng `'\n'` mà không dừng lại chờ người dùng nhập, và cách khắc phục gọn nhất trong chuỗi định dạng của `scanf` là gì?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Giải mã chi tiết các đặc tả định dạng (Format Specifiers):
- `%-12s` (trong `printf`): In chuỗi ký tự với độ rộng trường tối thiểu 12 ký tự và **căn lề trái** (dấu `-`). Nếu chuỗi ngắn hơn 12 ký tự, phần dư bên phải được bù bằng khoảng trắng.
- `%08.2f` (trong `printf`): In số thực với tổng độ rộng tối thiểu 8 ký tự (bao gồm cả dấu chấm thập phân), làm tròn **2 chữ số sau dấu phẩy** (`.2`), và điền các chữ số `0` vào phần trống bên trái (`08`).
- `%19[^\n]` (Scanset trong `scanf`): Đọc tối đa **19 ký tự** (chừa 1 byte cuối cho `'\0'` trong mảng `char[20]`) bao gồm cả khoảng trắng cho đến khi gặp ký tự xuống dòng `'\n'`.
- Phân biệt `%f` và `%lf` trong `scanf()`: Trong `scanf`, đối số truyền vào là **con trỏ địa chỉ** (`float*` hoặc `double*`) nên không có cơ chế tự động thăng hạng `float` lên `double` như trong `printf`. Do đó, biến `float` bắt buộc dùng `%f` (ghi 4 bytes), còn biến `double` **bắt buộc phải dùng `%lf`** (ghi 8 bytes). Dùng nhầm `%f` cho biến `double` trong `scanf` sẽ chỉ ghi 4 bytes đầu và làm hỏng giá trị số thực!

2. Ý nghĩa giá trị trả về của `scanf()` và xử lý lỗi nhập sai kiểu dữ liệu:
- Hàm `scanf()` trả về **số lượng mục dữ liệu đã được đọc và gán thành công** (hoặc `EOF` nếu kết thúc luồng đầu vào).
- Ví dụ với `int rc = scanf("%d %lf", &id, &score);`: Nếu người dùng nhập hợp lệ cả 2 số, `rc == 2`. Nếu người dùng nhập chữ `"abc"`, `scanf` dừng ngay, giữ nguyên `"abc"` trong bộ đệm `stdin` và trả về `rc == 0`.
- Cách kiểm tra và xả bộ đệm `stdin` chuẩn:
```c
int age, c;
while (printf("Nhap tuoi: "), scanf("%d", &age) != 1) {
    printf("Du lieu khong hop le! Vui long nhap so nguyen.\n");
    while ((c = getchar()) != '\n' && c != EOF) { } // Xả sạch ký tự rác và '\n'
}
```
*(Mẹo xử lý trôi lệnh khi đọc `%c` sau `%d`: Thêm một khoảng trắng trước `%c` dưới dạng `scanf(" %c", &grade);` để `scanf` tự động bỏ qua mọi ký tự whitespace/newline còn tồn đọng).*

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích đúng các đặc tả %-12s, %08.2f, %19[^\n]** | **3.5đ** | Phân tích chính xác cờ căn trái (-), bù số 0, độ rộng, độ chính xác và giới hạn độ dài scanset chống tràn đệm. |
| **Phân biệt rõ %f (float*) và %lf (double*) trong scanf** | **3.0đ** | Giải thích vì sao scanf truyền con trỏ nên bắt buộc phân biệt kích thước 4 bytes (%f) và 8 bytes (%lf). |
| **Kiểm tra giá trị trả về của scanf và xả bộ đệm stdin khi nhập lỗi** | **3.5đ** | Nêu đúng ý nghĩa giá trị trả về của scanf và viết vòng lặp getchar() xả sạch rác tới '\n' hoặc EOF. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 8. [PRF_EQ08] Cơ chế thăng hạng kiểu ngầm định (Implicit Type Promotion) trong biểu thức số học

- **Mã định danh (Question Code):** `PRF_EQ08`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000462`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 8 — *Type Promotion & Arithmetic Conversions*
- **Mục tiêu đánh giá:** Kiểm tra sự hiểu biết về quy tắc thăng hạng số nguyên (Integer Promotion) và chuyển đổi số học thông thường trong C.

#### ❓ Câu hỏi vấn đáp:
> "Quy tắc thăng hạng kiểu ngầm định (Implicit Type Conversion / Integer Promotion) trong C hoạt động như thế nào khi một biểu thức chứa các toán hạng có kiểu dữ liệu khác nhau? Phân tích kết quả của biểu thức: `int a = -10; unsigned int b = 5; if (a + b > 0) ...`."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao phép so sánh `a + b > 0` với a=-10 và b=5 lại đánh giá là TRUE trong ngôn ngữ C?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Quy tắc chuyển đổi số học thông thường (Usual Arithmetic Conversions):
Khi hai toán hạng của một toán tử số học có kiểu dữ liệu khác nhau, trình biên dịch C sẽ tự động chuyển đổi toán hạng có 'thứ bậc thấp hơn' lên kiểu của toán hạng có 'thứ bậc cao hơn' để thực hiện phép tính trên cùng một miền giá trị:
Thứ bậc kiểu dữ liệu:
`int` < `unsigned int` < `long` < `unsigned long` < `float` < `double` < `long double`.
Quy tắc thăng hạng số nguyên (Integer Promotion): Các kiểu dữ liệu nhỏ hơn `int` (như `char`, `short`, `signed char`) luôn được tự động nâng cấp thành `int` trước khi tính toán.

2. Phân tích biểu thức bẫy kinh điển `int a = -10; unsigned int b = 5;`:
- Xét biểu thức `a + b`:
  + Toán hạng `a` có kiểu `int` (có dấu, thứ bậc thấp hơn).
  + Toán hạng `b` có kiểu `unsigned int` (không dấu, thứ bậc cao hơn).
  + Do đó, toán hạng `a` (-10) bị ép kiểu ngầm định sang `unsigned int`!
- Cơ chế biểu diễn bù 2 (Two's Complement):
  Giá trị số âm `-10` trong hệ 32-bit là `0xFFFFFFF6`. Khi được diễn dịch dưới dạng `unsigned int`, nó trở thành một số dương khổng lồ: `4,294,967,286`.
- Phép cộng: `4,294,967,286 + 5 = 4,294,967,291` (vẫn là một số unsigned cực lớn).
- Biểu thức điều kiện: `4,294,967,291 > 0` trả về kết quả là ĐÚNG (True)!
Đây là một lỗi logic cực kỳ nguy hiểm do vô tình trộn lẫn giữa kiểu số nguyên có dấu và không dấu trong C.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Trình bày bảng thứ bậc chuyển đổi kiểu ngầm định** | **3.0đ** | Nêu rõ quy tắc integer promotion và thứ bậc từ int lên unsigned int lên float/double. |
| **Giải thích cơ chế ép kiểu ngầm định của biến int a=-10** | **3.5đ** | Phân tích rõ a bị ép sang unsigned int và biểu diễn bù 2 thành số dương khổng lồ. |
| **Giải thích tại sao điều kiện if(a + b > 0) trả về TRUE** | **3.5đ** | Chứng minh giá trị unsigned lớn hơn 0 và cảnh báo lỗi trộn lẫn kiểu có dấu/không dấu. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 9. [PRF_EQ09] Phân tích vòng lặp lồng nhau (Nested Loops), biến cờ hiệu (Flag) và vòng lặp lính canh (Sentinel)

- **Mã định danh (Question Code):** `PRF_EQ09`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO3** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000463`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 10-12 — *Nested Loops Complexity, Boolean Flags & Sentinel-Controlled Loops*
- **Mục tiêu đánh giá:** Đánh giá khả năng phân tích luồng thực thi của vòng lặp lồng nhau, kỹ thuật thoát sớm bằng biến cờ hiệu và điều khiển vòng lặp bằng giá trị lính canh.

#### ❓ Câu hỏi vấn đáp:
> "Hãy phân tích cơ chế hoạt động và tổng số bước lặp của cấu trúc vòng lặp lồng nhau (Nested Loops) khi vòng ngoài chạy $M$ lần và vòng trong chạy $N$ lần. Thế nào là vòng lặp điều khiển bằng giá trị lính canh (Sentinel-Controlled Loop) và làm thế nào để dùng biến cờ hiệu (Flag variable) nhằm thoát an toàn khỏi cả hai vòng lặp lồng nhau mà không lạm dụng lệnh `goto`?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trong vòng lặp lồng nhau phụ thuộc chỉ số dạng `for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++)`, tổng số lần thân vòng lặp trong được thực thi theo $n$ là bao nhiêu?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cơ chế hoạt động và số lần thực thi của Vòng lặp lồng nhau (Nested Loops):
- Trong cấu trúc vòng lặp lồng nhau, ứng với **mỗi 1 lần lặp** của vòng lặp bên ngoài (outer loop), vòng lặp bên trong (inner loop) sẽ khởi tạo lại từ đầu và chạy **trọn vẹn toàn bộ chu kỳ** của nó.
- Nếu vòng ngoài chạy $M$ lần độc lập và vòng trong chạy $N$ lần, tổng số lần thực thi khối lệnh trong cùng là tích Đề-các: $M 	imes N$ lần (độ phức tạp $O(M 	imes N)$, hoặc $rac{n(n-1)}{2} = O(n^2)$ nếu `j` chạy từ `i + 1` đến `n - 1`).

2. Vòng lặp điều khiển bằng giá trị lính canh (Sentinel-Controlled Loop):
- Là kỹ thuật dùng cho bài toán **chưa biết trước số lượng phần tử đầu vào**. Chương trình quy ước một giá trị đặc biệt nằm ngoài miền dữ liệu hợp lệ gọi là **Lính canh (Sentinel)** (ví dụ nhập `-1` để kết thúc việc nhập điểm số dương).
- Giá trị Sentinel chỉ đóng vai trò tín hiệu chấm dứt vòng lặp và **tuyệt đối không được cộng gộp** vào kết quả thống kê (tổng, trung bình).

3. Kỹ thuật dùng biến cờ hiệu (`Flag`) để thoát khỏi nhiều tầng vòng lặp lồng nhau (Thay thế `goto`):
- Câu lệnh `break;` trong C chỉ thoát khỏi **duy nhất 1 vòng lặp gần nhất** chứa nó. Để thoát đồng thời cả vòng trong lẫn vòng ngoài một cách có cấu trúc (Structured Programming), ta kết hợp biến cờ `found` trong điều kiện duy trì vòng lặp:
```c
int found = 0;
for (int i = 0; i < rows && !found; i++) {
    for (int j = 0; j < cols && !found; j++) {
        if (grid[i][j] == target) {
            found = 1; // Bật cờ hiệu -> cả vòng trong và vòng ngoài đều dừng ngay ở bước kiểm tra kế tiếp
        }
    }
}
```

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân tích đúng quy tắc nhân số bước lặp của Nested Loops** | **3.5đ** | Giải thích rõ mỗi bước vòng ngoài kích hoạt toàn bộ chu kỳ vòng trong với tổng chi phí M * N (hoặc n(n-1)/2). |
| **Trình bày đúng bản chất vòng lặp điều khiển bằng Sentinel** | **3.0đ** | Định nghĩa rõ giá trị lính canh ngoài miền hợp lệ và nguyên tắc không tính giá trị sentinel vào dữ liệu. |
| **Cài đặt kỹ thuật biến cờ hiệu (Flag) thoát đa vòng lặp cấu trúc** | **3.5đ** | Chỉ rõ giới hạn của break chỉ thoát 1 vòng lặp và viết đúng điều kiện kết hợp && !found ở cả 2 vòng lặp. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 10. [PRF_EQ10] Nguyên lý thiết kế Module hóa chương trình C: Độ kết dính (Cohesion) và Độ ghép nối (Coupling)

- **Mã định danh (Question Code):** `PRF_EQ10`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000464`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 16-18 — *Modular Programming: High Cohesion, Low Coupling & Top-Down Design*
- **Mục tiêu đánh giá:** Đánh giá tư duy thiết kế phần mềm C theo hướng module hóa (Modular Programming), phân rã bài toán Top-Down và tiêu chuẩn High Cohesion - Low Coupling.

#### ❓ Câu hỏi vấn đáp:
> "Trong thiết kế chương trình C theo phương pháp Module hóa (Modular Programming / Top-Down Stepwise Refinement), hãy giải thích hai tiêu chuẩn chất lượng cốt lõi: Độ kết dính nội tại cao (`High Cohesion`) và Độ ghép nối thấp (`Low Coupling`). Tại sao việc lạm dụng biến toàn cục (`Global Variables`) để chia sẻ dữ liệu giữa các hàm lại vi phạm nghiêm trọng nguyên tắc `Low Coupling`?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi thiết kế một hàm xử lý nghiệp vụ tính thuế thu nhập trong C, vì sao ta nên tách riêng hàm nhập/xuất (`printf`/`scanf`) ra khỏi hàm tính toán toán học thuần túy?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Phương pháp phân rã Top-Down và hai tiêu chuẩn `High Cohesion` — `Low Coupling`:
- **Phân rã từ trên xuống (Top-Down Stepwise Refinement):** Chia một bài toán lớn phức tạp thành các bài toán con nhỏ hơn (Sub-problems), mỗi bài toán con được đóng gói thành một hàm (Function) hoặc một module độc lập.
- **Độ kết dính nội tại cao (`High Cohesion`):** Mỗi hàm chỉ nên thực hiện **đúng một nhiệm vụ chức năng duy nhất, rõ ràng và trọn vẹn** (Single Responsibility). Ví dụ: hàm `calculateTax(double income)` chỉ tính và trả về tiền thuế, không kiêm nhiệm thêm việc in menu hay đọc tệp. Điều này giúp hàm dễ hiểu, dễ kiểm thử đơn vị (Unit Test) và tái sử dụng ở nhiều nơi.
- **Độ ghép nối thấp (`Low Coupling`):** Các hàm/module nên phụ thuộc vào nhau ít nhất có thể và chỉ giao tiếp với nhau thông qua **danh sách tham số đầu vào tường minh và giá trị trả về** (Data Coupling).

2. Tại sao lạm dụng biến toàn cục (`Global Variables`) tạo ra `Tight/Common Coupling` nguy hiểm:
- **Mất tính cô lập và phát sinh hiệu ứng phụ ngầm (Hidden Side Effects):** Bất kỳ hàm nào trong toàn bộ chương trình cũng có thể âm thầm sửa đổi giá trị biến toàn cục. Khi biến toàn cục mang giá trị sai, lập trình viên phải rà soát toàn bộ hàng chục hàm thay vì chỉ kiểm tra nơi truyền tham số.
- **Phá vỡ khả năng tái sử dụng (Non-reusable) và không an toàn luồng:** Hàm phụ thuộc cứng vào tên biến toàn cục không thể tách sang dự án khác hoặc gọi đệ quy/song song an toàn.
- **Chuẩn thiết kế C:** Hạn chế tối đa biến toàn cục; truyền dữ liệu qua tham số giá trị hoặc con trỏ `const` khi chỉ đọc.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa rõ nguyên lý High Cohesion (Độ kết dính cao)** | **3.5đ** | Giải thích nguyên tắc mỗi hàm chỉ đảm nhận một chức năng đơn lẻ rõ ràng, tách biệt tính toán và nhập/xuất. |
| **Định nghĩa rõ nguyên lý Low Coupling (Độ ghép nối thấp)** | **3.5đ** | Giải thích việc giao tiếp minh bạch qua tham số và giá trị trả về thay vì phụ thuộc trạng thái ngầm. |
| **Phân tích tác hại của Global Variables gây Common/Tight Coupling** | **3.0đ** | Chỉ rõ rủi ro hiệu ứng phụ ngầm (side effects), khó truy vết lỗi và mất khả năng tái sử dụng hàm. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 11. [PRF_EQ11] Ý nghĩa của từ khóa const khi kết hợp với con trỏ và bảo vệ tham số mảng trong hàm C

- **Mã định danh (Question Code):** `PRF_EQ11`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 29-30 & 37 — *Const-Correctness with Pointers & Read-Only Array Parameters*
- **Mục tiêu đánh giá:** Đánh giá khả năng phân biệt 3 cách đặt từ khóa const với con trỏ và ứng dụng bảo vệ dữ liệu mảng truyền vào hàm không bị sửa đổi ngoài ý muốn.

#### ❓ Câu hỏi vấn đáp:
> "Hãy phân biệt sự khác nhau về quyền thay đổi giá trị vùng nhớ (`*p`) và quyền thay đổi địa chỉ con trỏ (`p`) giữa ba khai báo sau trong C: (1) `const int *p`, (2) `int *const p`, và (3) `const int *const p`. Tại sao các hàm chỉ đọc dữ liệu mảng hoặc chuỗi (như `strlen(const char *s)` hay `double calcAverage(const int arr[], int n)`) luôn sử dụng từ khóa `const` trong khai báo tham số?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu một biến được khai báo là `const int max_users = 100;`, điều gì sẽ xảy ra nếu lập trình viên cố tình ép kiểu bỏ `const` qua con trỏ `int *ptr = (int *)&max_users; *ptr = 200;` theo chuẩn C?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Quy tắc đọc từ phải sang trái (Right-to-Left Rule) cho 3 dạng kết hợp `const` và con trỏ `*`:
- **Trường hợp 1: `const int *p` (hoặc `int const *p` — Pointer to Constant Data):**
  + Bản chất: `p` là con trỏ trỏ tới một số nguyên hằng.
  + Giá trị vùng nhớ `*p` là **chỉ đọc (Read-only)** thông qua `p` (`*p = 10;` $\to$ **Lỗi biên dịch**).
  + Bản thân con trỏ `p` **được phép thay đổi địa chỉ** để trỏ sang biến khác (`p = &other_var;` hoặc `p++;` $\to$ **Hợp lệ**).
- **Trường hợp 2: `int *const p = &x;` (Constant Pointer to Mutable Data):**
  + Bản chất: `p` là một **con trỏ hằng** (bắt buộc phải khởi tạo địa chỉ ngay lúc khai báo).
  + Địa chỉ lưu trong `p` bị **khóa cố định** (`p = &y;` $\to$ **Lỗi biên dịch**).
  + Giá trị tại vùng nhớ mà `p` trỏ tới **được phép thay đổi** (`*p = 99;` $\to$ **Hợp lệ**).
- **Trường hợp 3: `const int *const p = &x;` (Constant Pointer to Constant Data):**
  + Khóa kép cả hai chiều: Không được phép đổi địa chỉ `p` VÀ cũng không được phép sửa giá trị `*p`.

2. Lý do sử dụng `const` cho tham số mảng/chuỗi chỉ đọc (`const int arr[]` / `const char *s`):
- Vì mảng truyền vào hàm bị suy giảm thành con trỏ địa chỉ gốc (pass-by-address), mọi thao tác ghi `arr[i] = ...` bên trong hàm sẽ làm biến đổi trực tiếp mảng gốc của hàm gọi (`Caller`).
- Khai báo `const int arr[]` thiết lập **Hợp đồng giao diện (Interface Contract - Const-Correctness)**: Trình biên dịch sẽ chặn đứng ngay lúc compile nếu thân hàm vô tình ghi đè phần tử mảng, đồng thời cho phép truyền cả mảng hằng (`const`) lẫn mảng thường vào hàm một cách an toàn.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân biệt chính xác const int *p và int *const p** | **4.0đ** | Chỉ rõ trường hợp nào khóa giá trị vùng nhớ (*p) và trường hợp nào khóa địa chỉ con trỏ (p). |
| **Giải thích đúng khai báo khóa kép const int *const p** | **2.5đ** | Nêu rõ cả địa chỉ con trỏ p và giá trị vùng nhớ *p đều không thể bị thay đổi. |
| **Phân tích vai trò của const trong bảo vệ tham số mảng truyền vào hàm** | **3.5đ** | Giải thích cơ chế mảng suy giảm thành con trỏ và vai trò của const giúp trình biên dịch chặn lỗi ghi đè ngoài ý muốn. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 12. [PRF_EQ12] Phân biệt bản chất bộ nhớ giữa mảng ký tự (char[]) và con trỏ hằng chuỗi (char*)

- **Mã định danh (Question Code):** `PRF_EQ12`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO8** | **DB 3NF CLO8** (`00000000-0000-0000-0000-000000000468`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 46 — *String Memory Segments: char[] vs char*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức về các phân vùng bộ nhớ (Stack vs Read-Only Data Segment) khi khai báo chuỗi trong C.

#### ❓ Câu hỏi vấn đáp:
> "Xét hai khai báo chuỗi sau trong C:
```c
char s1[] = "Hello";
char *s2 = "Hello";
```
Hãy phân biệt sự khác nhau giữa s1 và s2 về: Vùng nhớ lưu trữ chuỗi, Khả năng chỉnh sửa nội dung ký tự (ví dụ `s1[0] = 'h'` vs `s2[0] = 'h'`), và Dung lượng bộ nhớ được cấp phát."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Điều gì xảy ra nếu chương trình cố gắng thực thi câu lệnh `s2[0] = 'h';`?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
So sánh chi tiết giữa `char s1[]` và `char *s2`:

1. Vùng nhớ lưu trữ:
- `char s1[] = "Hello";`:
  Chuỗi ký tự được cấp phát và sao chép trực tiếp vào một mảng ký tự nằm trên vùng nhớ Ngăn xếp (Stack Segment) của hàm.
- `char *s2 = "Hello";`:
  Hằng chuỗi `"Hello"` (String Literal) được lưu trữ cố định trong Phân vùng dữ liệu chỉ đọc (Read-Only Data Segment / Text Segment). Biến `s2` chỉ là một con trỏ 8 bytes nằm trên Stack lưu địa chỉ trỏ tới hằng chuỗi chỉ đọc đó.

2. Khả năng sửa đổi ký tự:
- `s1[0] = 'h';`: Hoàn toàn HỢP LỆ. Vì mảng `s1` nằm trên Stack của tiến trình và có quyền đọc/ghi (Read/Write). Chuỗi biến thành `"hello"`.
- `s2[0] = 'h';`: BẤT HỢP LỆ (Lỗi nghiêm trọng). Vì cố tình ghi đè lên phân vùng bộ nhớ chỉ đọc (Read-Only). Hệ điều hành sẽ gửi tín hiệu vi phạm phân đoạn bộ nhớ (SIGSEGV - Segmentation Fault) làm chương trình sập ngay lập tức. Khuyến nghị chuẩn: Luôn khai báo `const char *s2 = "Hello";`.

3. Dung lượng bộ nhớ:
- `s1`: Chiếm đúng 6 bytes trên Stack (5 ký tự + 1 byte `'\0'`).
- `s2`: Chiếm 6 bytes trên Read-Only Data Segment + 8 bytes cho biến con trỏ `s2` trên Stack = 14 bytes tổng cộng.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân biệt vùng nhớ Stack (s1) vs Read-Only Data Segment (s2)** | **3.5đ** | Nêu rõ s1 cấp phát mảng trên Stack, s2 trỏ vào String Literal chỉ đọc. |
| **Phân tích lỗi Segmentation Fault khi sửa s2[0]** | **3.5đ** | Giải thích s1 sửa thoải mái, s2 gây crash do vi phạm vùng nhớ chỉ đọc và khuyến nghị const. |
| **So sánh dung lượng bộ nhớ cấp phát thực tế** | **3.0đ** | Tính chuẩn 6 bytes của mảng s1 so với 8 bytes con trỏ + 6 bytes literal của s2. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 13. [PRF_EQ13] Thuật toán Tìm kiếm tuyến tính (Linear Search) và Tìm kiếm nhị phân (Binary Search) trên mảng 1 chiều

- **Mã định danh (Question Code):** `PRF_EQ13`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO7** (`00000000-0000-0000-0000-000000000467`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 39 — *1D Array Searching: Linear Search vs Binary Search & Overflow-Safe Midpoint*
- **Mục tiêu đánh giá:** Đánh giá khả năng cài đặt và so sánh hai thuật toán tìm kiếm cốt lõi trên mảng một chiều trong chương trình PRF192.

#### ❓ Câu hỏi vấn đáp:
> "Hãy trình bày nguyên lý hoạt động, điều kiện áp dụng và độ phức tạp thời gian (tốt nhất, xấu nhất) của thuật toán Tìm kiếm tuyến tính (`Linear Search`) và Tìm kiếm nhị phân (`Binary Search`) trên mảng 1 chiều. Viết hàm C cài đặt `Linear Search` trả về chỉ số xuất hiện đầu tiên của khóa `key` (hoặc `-1` nếu không thấy) và giải thích vì sao trong `Binary Search` ta nên viết `int mid = left + (right - left) / 2;` thay vì `(left + right) / 2`."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu một mảng gồm $N = 1,000,000$ phần tử chưa được sắp xếp và chương trình chỉ cần tìm kiếm đúng 1 lần duy nhất, ta nên dùng ngay `Linear Search` hay sắp xếp mảng rồi dùng `Binary Search`? Giải thích bằng độ phức tạp thuật toán."*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. So sánh nguyên lý, điều kiện áp dụng và độ phức tạp giữa `Linear Search` và `Binary Search`:
- **Tìm kiếm tuyến tính (`Linear Search`):**
  + Nguyên lý: Duyệt tuần tự từng phần tử từ chỉ số `0` đến `n - 1`, so sánh `arr[i] == key`. Dừng ngay khi tìm thấy phần tử đầu tiên khớp hoặc khi hết mảng.
  + Điều kiện áp dụng: Hoạt động trên **mọi mảng** (không yêu cầu mảng phải được sắp xếp trước).
  + Độ phức tạp: Tốt nhất $O(1)$ (nằm ngay ở `arr[0]`), Xấu nhất và Trung bình $O(n)$ (so sánh tối đa $n$ lần).
- **Tìm kiếm nhị phân (`Binary Search`):**
  + Nguyên lý: Liên tục chia đôi khoảng tìm kiếm `[left, right]` bằng cách so sánh `key` với phần tử chính giữa `arr[mid]`.
  + Điều kiện tiên quyết bắt buộc: Mảng **phải được sắp xếp có thứ tự** (tăng dần hoặc giảm dần).
  + Độ phức tạp: Tốt nhất $O(1)$, Xấu nhất $O(\log_2 n)$ (ví dụ với $n = 10^6$ chỉ cần tối đa $\lceil \log_2(10^6) \rceil = 20$ phép so sánh).

2. Cài đặt chuẩn hàm `linearSearch` trong C:
```c
int linearSearch(const int arr[], int n, int key) {
    if (arr == NULL || n <= 0) return -1;
    for (int i = 0; i < n; i++) {
        if (arr[i] == key) {
            return i; // Trả về vị trí đầu tiên tìm thấy (0-indexed)
        }
    }
    return -1; // Không tìm thấy key trong mảng
}
```

3. Tại sao trong `Binary Search` phải viết `int mid = left + (right - left) / 2;`:
- Nếu mảng rất lớn có `left` và `right` đều lớn hơn `INT_MAX / 2` (ví dụ `left = 1,200,000,000` và `right = 1,500,000,000`), tổng `left + right = 2,700,000,000` vượt quá `INT_MAX` ($2,147,483,647$), gây **Tràn số nguyên có dấu (`Signed Integer Overflow`)** khiến `mid` bị âm và làm chương trình truy cập ngoài mảng (`Segmentation Fault`).
- Công thức `left + (right - left) / 2` luôn bảo đảm hiệu `right - left >= 0` không bao giờ vượt quá `right`, triệt tiêu 100% nguy cơ tràn số.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **So sánh đúng điều kiện áp dụng và độ phức tạp của Linear Search vs Binary Search** | **3.5đ** | Chỉ rõ Linear Search O(n) không cần sắp xếp, Binary Search O(log n) bắt buộc mảng đã sắp xếp. |
| **Cài đặt chuẩn xác hàm linearSearch trong C** | **3.5đ** | Viết đúng vòng lặp kiểm tra arr[i] == key, dùng const int arr[] và trả về -1 khi không tìm thấy. |
| **Giải thích công thức tính mid = left + (right - left) / 2 chống tràn số** | **3.0đ** | Phân tích rõ hiện tượng tràn số nguyên khi tổng left + right vượt quá INT_MAX. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 14. [PRF_EQ14] Cơ chế hoạt động của File Buffering trong thư viện stdio và vai trò của hàm fflush()

- **Mã định danh (Question Code):** `PRF_EQ14`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO6** | **DB 3NF CLO6** (`00000000-0000-0000-0000-000000000466`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 52, 55 — *File Streams & I/O Buffering (fflush)*
- **Mục tiêu đánh giá:** Kiểm tra hiểu biết về cơ chế bộ đệm luồng I/O (Stream Buffering) và cách ép đẩy dữ liệu xuống đĩa an toàn.

#### ❓ Câu hỏi vấn đáp:
> "Bộ đệm luồng tệp (File Buffering) trong thư viện C stdio.h hoạt động như thế nào? Nêu 3 chế độ đệm (Unbuffered, Line-buffered, Fully-buffered). Hàm fflush() đóng vai trò gì và tại sao chuẩn C nghiêm cấm sử dụng `fflush(stdin)`?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Cách chuẩn xác nhất để dọn dẹp các ký tự còn sót lại trong bộ đệm bàn phím stdin trong C là gì?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cơ chế hoạt động của File Buffering:
Thao tác đọc/ghi trực tiếp xuống đĩa cứng vật lý (Disk I/O) rất chậm do liên quan đến chuyển động cơ học hoặc giao tiếp bus hệ thống.
Thư viện `<stdio.h>` sử dụng một vùng nhớ trung gian trên RAM gọi là Bộ đệm luồng (Stream Buffer):
- Khi gọi `fprintf` hoặc `fwrite`, dữ liệu chưa được ghi ngay xuống đĩa mà tạm thời tích tụ trong bộ đệm RAM.
- Chỉ khi bộ đệm bị đầy, hoặc tệp được đóng bằng `fclose()`, hoặc gặp ký tự `\n` (tùy chế độ), dữ liệu mới được đẩy hàng loạt xuống đĩa (Flush), giúp giảm thiểu số lần gọi System Call của hệ điều hành.

2. Ba chế độ đệm chuẩn:
- Không đệm (`_IONBF` - Unbuffered): Dữ liệu được ghi ngay lập tức (ví dụ luồng lỗi `stderr`).
- Đệm theo dòng (`_IOLBF` - Line-buffered): Dữ liệu được xả mỗi khi gặp ký tự xuống dòng `\n` (mặc định cho màn hình `stdout`).
- Đệm toàn phần (`_IOFBF` - Fully-buffered): Chỉ xả khi bộ đệm (thường là 4KB - 8KB) đầy hoàn toàn (mặc định cho tệp trên đĩa).

3. Vai trò của hàm `fflush(FILE *stream)`:
Ép buộc hệ thống xả toàn bộ dữ liệu đang chờ trong bộ đệm ra thiết bị ngoại vi hoặc ghi ngay xuống đĩa cứng mà không cần chờ bộ đệm đầy.

4. Cảnh báo cấm sử dụng `fflush(stdin)`:
Theo chuẩn ISO C, hàm `fflush()` CHỈ ĐƯỢC ĐỊNH NGHĨA CHO LUỒNG ĐẦU RA (Output Streams như `stdout`, file write).
Hành vi gọi `fflush()` trên luồng đầu vào (Input Stream như `stdin`) là một Hành vi không xác định (Undefined Behavior): Một số trình biên dịch Windows cũ (MSVC) có thể xóa đệm, nhưng trên Linux/GCC thao tác này hoàn toàn bị phớt lờ.
Giải pháp dọn đệm stdin chuẩn C: Dùng vòng lặp đọc hết ký tự rác cho tới khi gặp `\n` hoặc `EOF`:
```c
int c;
while ((c = getchar()) != '\n' && c != EOF);
```

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích cơ chế tối ưu I/O của Stream Buffering** | **3.0đ** | Nêu rõ đệm RAM tích lũy dữ liệu giảm số lần gọi System Call ghi đĩa chậm chạp. |
| **Phân biệt rõ 3 chế độ đệm (_IONBF, _IOLBF, _IOFBF)** | **3.5đ** | Trình bày chuẩn xác unbuffered, line-buffered (stdout) và fully-buffered (disk file). |
| **Hàm fflush và cảnh báo Undefined Behavior với stdin** | **3.5đ** | Giải thích fflush chỉ dùng cho output stream, cấm fflush(stdin) và code dọn đệm getchar(). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 15. [PRF_EQ15] Cài đặt thuật toán kiểm tra số nguyên tố tối ưu hóa vòng lặp trong C

- **Mã định danh (Question Code):** `PRF_EQ15`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000464`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 19-20 — *Algorithmic Functions: Prime Check Optimization*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng cài đặt hàm thuật toán, tối ưu hóa điều kiện lặp từ O(N) xuống O(sqrt(N)) và xử lý các trường hợp biên.

#### ❓ Câu hỏi vấn đáp:
> "Hãy viết một hàm C hoàn chỉnh `int isPrime(int n)` kiểm tra xem số nguyên dương n có phải là số nguyên tố hay không (trả về 1 nếu là nguyên tố, 0 nếu không phải). Hãy giải thích tại sao vòng lặp chỉ cần duyệt từ 2 đến căn bậc hai của n (`i * i <= n`) và bước nhảy có thể tăng 2 đơn vị."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Các số nguyên n <= 1 (như 0, 1 hoặc số âm) có phải là số nguyên tố không và hàm của bạn xử lý trường hợp này như thế nào?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Phân tích toán học để tối ưu hóa hàm kiểm tra số nguyên tố `isPrime(int n)`:
- Theo định nghĩa, số nguyên tố $n$ là số tự nhiên lớn hơn 1 chỉ chia hết cho 1 và chính nó. Do đó mọi $n \le 1$ đều không phải là số nguyên tố.
- Nếu $n$ là hợp số, nó bắt buộc phải có ít nhất một ước số nguyên tố $d$ thỏa mãn $2 \le d \le \sqrt{n}$. Nếu duyệt tới $\lfloor\sqrt{n}floor$ mà không tìm thấy ước nào, ta kết luận chắc chắn $n$ là số nguyên tố (giảm độ phức tạp từ $O(n)$ xuống $O(\sqrt{n})$).
- Tối ưu hóa quy luật $6k \pm 1$: Mọi số nguyên lớn hơn 3 không chia hết cho 2 và 3 đều có dạng $6k - 1$ hoặc $6k + 1$. Ta kiểm tra riêng $n == 2, n == 3$, loại bỏ các bội số của 2 và 3, sau đó vòng lặp chỉ cần bắt đầu từ $i = 5$ với bước nhảy `i += 6` (kiểm tra cặp `i` và `i + 2`).

2. Cài đặt hàm `isPrime(int n)` chuẩn C99 chống tràn số nguyên (`i <= n / i`):
```c
#include <stdbool.h>

bool isPrime(int n) {
    if (n <= 1) return false;
    if (n <= 3) return true;
    if (n % 2 == 0 || n % 3 == 0) return false;

    // BẮT BUỘC dùng điều kiện i <= n / i thay vì i * i <= n để chống tràn số (Signed Integer Overflow)
    for (int i = 5; i <= n / i; i += 6) {
        if (n % i == 0 || n % (i + 2) == 0) {
            return false;
        }
    }
    return true;
}
```

3. Tại sao phải viết `i <= n / i` thay vì `i * i <= n` khi `n` gần `INT_MAX`:
- Trong kiểu `int` 32-bit có dấu, `INT_MAX = 2,147,483,647` (là một số nguyên tố Mersenne $2^{31}-1$), có $\sqrt{	ext{INT\_MAX}} pprox 46,340.95$.
- Khi vòng lặp chạy tới $i = 46,337$ (hoặc $i + 6 = 46,343$), tích `i * i` bằng $46,343^2 = 2,147,673,649 > 	ext{INT\_MAX}$, gây ra **Tràn số nguyên có dấu (Signed Integer Overflow — Undefined Behavior)**, làm giá trị `i * i` bị lật thành số âm khiến điều kiện `i * i <= n` luôn đúng và dẫn đến vòng lặp chạy sai hoặc vô tận!
- Khi viết `i <= n / i`, phép chia nguyên `n / i` hoàn toàn không bao giờ vượt quá `n`, đảm bảo an toàn tuyệt đối cho mọi giá trị `n` thuộc toàn dải `[INT_MIN, INT_MAX]`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Xử lý đúng các ca biên (n <= 1, n <= 3, chia hết cho 2 và 3)** | **3.0đ** | Kiểm tra đầy đủ và chính xác các trường hợp biên n <= 1 trả về false, 2 và 3 trả về true, loại bội số của 2 và 3. |
| **Tối ưu hóa vòng lặp O(sqrt(n)) với bước nhảy i += 6** | **3.5đ** | Giải thích nguyên lý ước số không vượt quá căn bậc hai của n và quy luật 6k +- 1 kiểm tra n % i và n % (i + 2). |
| **Chống tràn số nguyên có dấu bằng điều kiện i <= n / i** | **3.5đ** | Viết đúng điều kiện lặp i <= n / i và giải thích rủi ro tràn số của i * i <= n khi n gần INT_MAX. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 16. [PRF_EQ16] Cài đặt hàm đảo ngược mảng một chiều bằng kỹ thuật hai con trỏ (Two Pointers)

- **Mã định danh (Question Code):** `PRF_EQ16`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 30, 43 — *Two-Pointer Technique & Array Reversal*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng vận dụng số học con trỏ và giải tham chiếu để thao tác đảo ngược mảng tại chỗ không dùng mảng phụ.

#### ❓ Câu hỏi vấn đáp:
> "Hãy viết một hàm C `void reverseArray(int *start, int *end)` nhận vào con trỏ đầu và con trỏ cuối của một mảng, thực hiện đảo ngược mảng tại chỗ (In-place) bằng kỹ thuật hai con trỏ. Giải thích chi tiết bước di chuyển con trỏ và điều kiện dừng."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi gọi hàm này từ hàm main để đảo ngược mảng `int a[10]`, ta phải truyền hai đối số như thế nào?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cài đặt hàm `reverseArray` bằng kỹ thuật hai con trỏ:
```c
void reverseArray(int *start, int *end) {
    if (start == NULL || end == NULL) return;

    // Lặp cho đến khi hai con trỏ gặp nhau hoặc vượt qua nhau
    while (start < end) {
        // Hoán đổi giá trị tại hai ô nhớ giải tham chiếu
        int temp = *start;
        *start = *end;
        *end = temp;

        // Dịch chuyển hai con trỏ lại gần nhau
        start++; // Tăng con trỏ đầu lên 1 phần tử (tiến sang phải)
        end--;   // Giảm con trỏ cuối xuống 1 phần tử (lùi sang trái)
    }
}
```

2. Giải thích cơ chế hoạt động:
- Con trỏ `start` ban đầu trỏ vào phần tử đầu mảng (`&a[0]`), con trỏ `end` trỏ vào phần tử cuối mảng (`&a[n-1]`).
- Điều kiện `while (start < end)`: Trong C, phép so sánh địa chỉ giữa hai con trỏ cùng trỏ vào một mảng là hoàn toàn hợp lệ. Khi `start < end`, nghĩa là phần tử đầu vẫn nằm trước phần tử cuối.
- Mỗi lần lặp: Hoán đổi nội dung qua biến tạm `temp`, sau đó tăng `start++` và giảm `end--`.
- Khi số phần tử là lẻ, hai con trỏ gặp nhau ở phần tử chính giữa (`start == end`), vòng lặp dừng. Nếu số phần tử là chẵn, hai con trỏ vượt qua nhau (`start > end`), vòng lặp cũng dừng.
- Độ phức tạp thời gian: $O(N/2) = O(N)$. Độ phức tạp không gian: $O(1)$ tại chỗ.

3. Lời gọi hàm từ `main()`:
`reverseArray(a, a + n - 1);` (hoặc `reverseArray(&a[0], &a[n - 1]);`).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Cài đặt đúng 100% thuật toán hai con trỏ đảo mảng** | **4.0đ** | Viết đúng tham số con trỏ, hoán đổi qua biến temp và bước nhảy start++, end--. |
| **Giải thích điều kiện dừng start < end và so sánh địa chỉ con trỏ** | **3.0đ** | Nêu rõ phép so sánh địa chỉ hợp lệ và dừng khi con trỏ gặp hoặc vượt qua nhau. |
| **Cú pháp lời gọi hàm chính xác từ main và độ phức tạp O(1)** | **3.0đ** | Chỉ ra cách gọi reverseArray(a, a + n - 1) và phân tích tối ưu bộ nhớ in-place. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 17. [PRF_EQ17] Cài đặt thuật toán sắp xếp nổi bọt (Bubble Sort) trên mảng số nguyên tối ưu với cờ swapped

- **Mã định danh (Question Code):** `PRF_EQ17`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO7** (`00000000-0000-0000-0000-000000000467`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 39 — *Bubble Sort & Early Termination Optimization*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng cài đặt thuật toán Bubble Sort và kỹ thuật cờ hiệu dừng sớm (Early Termination) khi mảng đã sắp xếp.

#### ❓ Câu hỏi vấn đáp:
> "Hãy trình bày nguyên lý hoạt động của thuật toán Sắp xếp nổi bọt (Bubble Sort). Viết hàm C cài đặt thuật toán có tối ưu hóa bằng cờ hiệu (flag/swapped) để đạt độ phức tạp O(N) trong trường hợp mảng đã được sắp xếp từ trước."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao sau mỗi lượt lặp i của vòng ngoài, vòng lặp trong chỉ cần duyệt đến `n - 1 - i`?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Nguyên lý hoạt động của Bubble Sort:
Thuật toán so sánh liên tiếp từng cặp phần tử liền kề `a[j]` và `a[j+1]`. Nếu phần tử trước lớn hơn phần tử sau, chúng được hoán đổi cho nhau. Qua mỗi lượt duyệt, phần tử lớn nhất trong đoạn chưa sắp xếp sẽ 'nổi bọt' dần về vị trí cuối mảng.

2. Cài đặt thuật toán tối ưu với cờ `swapped`:
```c
void bubbleSort(int a[], int n) {
    for (int i = 0; i < n - 1; i++) {
        int swapped = 0; // Cờ theo dõi có xảy ra hoán đổi không
        // Sau i lượt, i phần tử lớn nhất đã nằm đúng vị trí ở cuối
        for (int j = 0; j < n - 1 - i; j++) {
            if (a[j] > a[j + 1]) {
                int temp = a[j];
                a[j] = a[j + 1];
                a[j + 1] = temp;
                swapped = 1; // Đánh dấu có hoán đổi
            }
        }
        // Nếu qua 1 lượt mà không có cặp nào bị hoán đổi -> mảng đã sắp xếp xong!
        if (swapped == 0) {
            break; // Thoát sớm
        }
    }
}
```

3. Phân tích độ phức tạp:
- Tối ưu `n - 1 - i`: Sau lượt $i$, chắc chắn $i$ phần tử lớn nhất đã ở vị trí cuối cùng, không cần so sánh lại, giảm một nửa số phép tính.
- Trường hợp tốt nhất (Best Case - mảng đã sắp xếp tăng dần sẵn): Vòng lặp trong chạy 1 lượt, `swapped` vẫn bằng 0, lệnh `break` kích hoạt ngay -> Đạt độ phức tạp thời gian **$O(N)$**.
- Trường hợp xấu nhất (Worst Case - mảng giảm dần): Duyệt đủ $n(n-1)/2$ phép so sánh -> Độ phức tạp **$O(N^2)$**.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Trình bày đúng nguyên lý so sánh cặp liền kề và nổi bọt** | **3.0đ** | Mô tả chuẩn cơ chế so sánh a[j] và a[j+1], đẩy phần tử lớn nhất về đuôi mảng. |
| **Cài đặt đúng 100% mã nguồn C kèm cờ hiệu swapped** | **4.0đ** | Viết đúng 2 vòng for, giới hạn n - 1 - i, kiểm tra cờ swapped và lệnh break dừng sớm. |
| **Phân tích độ phức tạp tốt nhất O(N) và xấu nhất O(N^2)** | **3.0đ** | Chứng minh khi mảng đã sắp xếp cờ ngắt sau 1 lượt đạt O(N) và Worst case O(N^2). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 18. [PRF_EQ18] Xử lý chuỗi ký tự: Viết hàm chuẩn hóa họ tên trong C

- **Mã định danh (Question Code):** `PRF_EQ18`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO8** | **DB 3NF CLO8** (`00000000-0000-0000-0000-000000000468`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 51 — *String Manipulation: Name Normalization*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng thao tác mảng ký tự, sử dụng thư viện ctype.h và xử lý các khoảng trắng thừa.

#### ❓ Câu hỏi vấn đáp:
> "Hãy viết một hàm C `void normalizeName(char *str)` thực hiện chuẩn hóa chuỗi họ tên người dùng theo các quy tắc: Xóa toàn bộ khoảng trắng ở đầu và cuối chuỗi; Giữa các từ chỉ giữ lại đúng một khoảng trắng; Ký tự đầu mỗi từ viết hoa và các ký tự còn lại viết thường. (Ví dụ: "   ngUYen   vAN   a  " biến thành "Nguyen Van A")."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Những hàm nào trong thư viện `<ctype.h>` được sử dụng để kiểm tra khoảng trắng và đổi chữ hoa/thường?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cài đặt hàm chuẩn hóa họ tên `normalizeName`:
```c
#include <stdio.h>
#include <ctype.h>
#include <string.h>

void normalizeName(char *str) {
    if (str == NULL) return;

    int read_idx = 0;
    int write_idx = 0;
    int in_word = 0;

    // Bỏ qua khoảng trắng đầu chuỗi
    while (isspace((unsigned char)str[read_idx])) {
        read_idx++;
    }

    while (str[read_idx] != '\0') {
        if (!isspace((unsigned char)str[read_idx])) {
            // Nếu bắt đầu một từ mới và không phải từ đầu tiên -> chèn đúng 1 khoảng trắng
            if (in_word == 0 && write_idx > 0) {
                str[write_idx++] = ' ';
            }
            // Ký tự đầu từ viết hoa, ký tự sau viết thường
            if (in_word == 0) {
                str[write_idx++] = toupper((unsigned char)str[read_idx]);
                in_word = 1;
            } else {
                str[write_idx++] = tolower((unsigned char)str[read_idx]);
            }
        } else {
            // Gặp khoảng trắng -> kết thúc từ hiện tại
            in_word = 0;
        }
        read_idx++;
    }
    // Đóng chuỗi tại vị trí viết cuối cùng bằng ký tự null
    str[write_idx] = '\0';
}
```

2. Giải thích cơ chế:
- Sử dụng kỹ thuật hai con trỏ chỉ số đọc/ghi (`read_idx` và `write_idx`) để xử lý chuỗi trực tiếp tại chỗ (In-place) không cần cấp phát thêm bộ nhớ phụ.
- Dùng hàm `isspace()` kiểm tra khoảng trắng, `toupper()` chuyển chữ hoa và `tolower()` chuyển chữ thường từ thư viện `<ctype.h>`.
- Biến trạng thái `in_word` theo dõi đang ở trong từ hay khoảng trắng, bảo đảm chèn đúng 1 khoảng trắng giữa các từ và viết hoa chữ cái đầu từ.
- Cuối cùng kết thúc mảng bằng ký tự `\0` tại vị trí `write_idx`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Xử lý xóa sạch khoảng trắng đầu và cuối chuỗi** | **3.0đ** | Bỏ qua khoảng trắng đầu và kết thúc chuỗi bằng '\0' không để khoảng trắng đuôi. |
| **Chuẩn hóa đúng 1 khoảng trắng duy nhất giữa các từ** | **3.5đ** | Kỹ thuật biến cờ in_word bảo đảm giữa các từ chỉ có đúng 1 ký tự space. |
| **Đổi hoa chữ cái đầu và thường các chữ cái sau với ctype.h** | **3.5đ** | Sử dụng đúng toupper/tolower từ ctype.h biến đổi ký tự chuẩn xác. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 19. [PRF_EQ19] Cấp phát động mảng số nguyên bằng malloc, kiểm tra NULL, mở rộng bằng realloc và giải phóng bằng free

- **Mã định danh (Question Code):** `PRF_EQ19`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 31 — *Dynamic Array Resizing with realloc*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng quản lý mảng động co giãn kích thước an toàn, chống rò rỉ bộ nhớ khi realloc thất bại.

#### ❓ Câu hỏi vấn đáp:
> "Hãy viết một chương trình C nhỏ minh họa quy trình làm việc với mảng động: Cấp phát mảng gồm 5 số nguyên bằng malloc(); Nhập giá trị; Mở rộng mảng lên 10 phần tử bằng realloc(); Khởi tạo các phần tử mới; Và cuối cùng giải phóng bộ nhớ bằng free(). Phân tích tại sao không nên gán trực tiếp kết quả của realloc vào chính con trỏ cũ `arr = realloc(arr, new_size)`."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu realloc thất bại và trả về NULL, điều gì xảy ra với vùng nhớ ban đầu của con trỏ arr nếu ta gán trực tiếp `arr = realloc(arr, new_size)`?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Mã nguồn C minh họa quy trình cấp phát và co giãn an toàn:
```c
#include <stdio.h>
#include <stdlib.h>

int main() {
    // 1. Cấp phát mảng ban đầu 5 phần tử
    int *arr = (int*)malloc(5 * sizeof(int));
    if (arr == NULL) {
        printf("Loi cap phat bo nho ban dau!
");
        return 1;
    }

    for (int i = 0; i < 5; i++) arr[i] = i + 1;

    // 2. Mở rộng kích thước mảng lên 10 phần tử an toàn qua con trỏ tạm
    int *temp = (int*)realloc(arr, 10 * sizeof(int));
    if (temp == NULL) {
        printf("Loi mo rong bo nho! Giu nguyen mang cu.
");
        free(arr); // Giải phóng mảng cũ trước khi thoát
        return 1;
    }
    // Gán lại con trỏ chính sau khi realloc thành công
    arr = temp;

    // Khởi tạo các phần tử mới từ chỉ số 5 đến 9
    for (int i = 5; i < 10; i++) arr[i] = (i + 1) * 10;

    // 3. Giải phóng bộ nhớ và gán NULL
    free(arr);
    arr = NULL;
    return 0;
}
```

2. Phân tích bẫy lỗi gán trực tiếp `arr = realloc(arr, new_size)`:
- Nếu hệ thống cạn RAM, hàm `realloc` sẽ thất bại và trả về con trỏ `NULL`.
- Lúc này, nếu viết `arr = realloc(arr, ...);`, biến con trỏ `arr` sẽ bị ghi đè bằng `NULL`.
- Hậu quả nghiêm trọng: Chương trình vừa làm mất địa chỉ của vùng nhớ 5 phần tử ban đầu trên Heap (không còn con trỏ nào trỏ tới để gọi `free`), dẫn đến rò rỉ bộ nhớ (Memory Leak) ngay lập tức.
- Quy tắc chuẩn: Luôn lưu kết quả `realloc` vào một con trỏ tạm `int *temp`, kiểm tra `if (temp != NULL)` rồi mới gán `arr = temp`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Cấp phát malloc và kiểm tra con trỏ NULL** | **2.5đ** | Khởi tạo đúng malloc(5 * sizeof(int)) kèm kiểm tra con trỏ NULL an toàn. |
| **Kỹ thuật mở rộng bằng con trỏ tạm temp với realloc** | **4.0đ** | Dùng con trỏ tạm temp hứng kết quả realloc và kiểm tra NULL chống rò rỉ bộ nhớ. |
| **Giải phóng bộ nhớ bằng free và gán arr = NULL** | **3.5đ** | Gọi free(arr) đúng vị trí và phân tích sâu bẫy Memory Leak khi gán trực tiếp arr = realloc. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 20. [PRF_EQ20] Thiết kế Struct quản lý Kho hàng (Inventory) và cài đặt hàm tìm kiếm, lọc mặt hàng dưới định mức

- **Mã định danh (Question Code):** `PRF_EQ20`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO7** (`00000000-0000-0000-0000-000000000467`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 42-45 — *Struct Array Processing: Inventory Search & Low-Stock Filtering*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng định nghĩa kiểu dữ liệu cấu trúc Struct thực tế, thao tác trên mảng Struct với con trỏ và tìm kiếm chuỗi không phân biệt/có phân biệt mã định danh.

#### ❓ Câu hỏi vấn đáp:
> "Hãy khai báo kiểu cấu trúc `Product` trong C để quản lý hàng hóa trong kho gồm: mã SKU (`char sku[16]`), tên sản phẩm (`char name[64]`), đơn giá (`double unit_price`), số lượng tồn kho (`int quantity`) và ngưỡng cảnh báo tồn tối thiểu (`int min_threshold`). Viết hai hàm C hoàn chỉnh: (1) `int findProductBySku(const Product list[], int n, const char *target_sku)` trả về chỉ số mặt hàng theo mã SKU, và (2) `double exportLowStockReport(const Product list[], int n)` in danh sách các mặt hàng cần nhập thêm (`quantity < min_threshold`) và trả về tổng chi phí cần chi để bù đủ định mức tối thiểu."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao khi so sánh hai mã SKU dạng chuỗi trong hàm `findProductBySku`, ta bắt buộc phải dùng hàm `strcmp(list[i].sku, target_sku) == 0` thay vì phép so sánh `list[i].sku == target_sku`?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Thiết kế cấu trúc `Product` và cài đặt hai hàm nghiệp vụ kho hàng:
```c
#include <stdio.h>
#include <string.h>

typedef struct {
    char sku[16];
    char name[64];
    double unit_price;
    int quantity;
    int min_threshold;
} Product;

// 1. Tìm kiếm tuyến tính mặt hàng theo mã SKU bằng strcmp
int findProductBySku(const Product list[], int n, const char *target_sku) {
    if (list == NULL || target_sku == NULL || n <= 0) {
        return -1;
    }
    for (int i = 0; i < n; i++) {
        if (strcmp(list[i].sku, target_sku) == 0) {
            return i;
        }
    }
    return -1;
}

// 2. Lọc các mặt hàng dưới định mức tồn tối thiểu và tính tổng chi phí nhập bù
double exportLowStockReport(const Product list[], int n) {
    if (list == NULL || n <= 0) return 0.0;
    double total_restock_cost = 0.0;

    printf("\n=== DANH SACH MAT HANG CAN NHAP BO SUNG ===\n");
    printf("%-12s | %-28s | %-8s | %-8s | %-12s\n", "SKU", "Ten San Pham", "Ton Kho", "Toi Thieu", "Chi Phi Bu");
    printf("--------------------------------------------------------------------------------\n");

    for (int i = 0; i < n; i++) {
        if (list[i].quantity < list[i].min_threshold) {
            int deficit = list[i].min_threshold - list[i].quantity;
            double item_cost = (double)deficit * list[i].unit_price;
            total_restock_cost += item_cost;
            printf("%-12s | %-28s | %-8d | %-8d | %12.2f\n",
                   list[i].sku, list[i].name, list[i].quantity, list[i].min_threshold, item_cost);
        }
    }
    return total_restock_cost;
}
```

2. Lý giải kỹ thuật quan trọng:
- Phép so sánh `list[i].sku == target_sku` chỉ so sánh **hai địa chỉ ô nhớ (con trỏ)** chứ không so sánh nội dung ký tự bên trong chuỗi, nên sẽ luôn trả về sai (`0`) khi hai chuỗi nằm ở hai vùng nhớ khác nhau. Bắt buộc phải dùng `strcmp(s1, s2) == 0` để so sánh từng ký tự cho tới `'\0'`.
- Sử dụng từ khóa `const Product list[]` bảo đảm các hàm tra cứu và xuất báo cáo không vô tình làm biến đổi dữ liệu tồn kho gốc.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa đúng typedef struct Product với các trường và kích thước chuẩn** | **3.0đ** | Khai báo đầy đủ 5 trường sku, name, unit_price, quantity, min_threshold với kiểu dữ liệu phù hợp. |
| **Cài đặt đúng hàm findProductBySku sử dụng strcmp và kiểm tra NULL** | **3.5đ** | Kiểm tra tham số đầu vào an toàn, duyệt mảng và dùng strcmp(list[i].sku, target_sku) == 0. |
| **Cài đặt đúng hàm lọc tồn kho và tính tổng chi phí bù định mức** | **3.5đ** | Tính đúng lượng thiếu hụt deficit = min_threshold - quantity, cộng dồn chi phí ép kiểu double và định dạng bảng rõ ràng. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 21. [PRF_EQ21] Đọc và phân tích tệp văn bản có cấu trúc (CSV / Log File) an toàn bằng kết hợp fgets() và sscanf()

- **Mã định danh (Question Code):** `PRF_EQ21`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO9** | **DB 3NF CLO9** (`00000000-0000-0000-0000-000000000469`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 54-56 — *Text File Parsing: Combining fgets() and sscanf() for Resilient I/O*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng đọc tệp văn bản theo dòng an toàn và bóc tách trường dữ liệu có kiểm tra lỗi định dạng bằng bộ đôi fgets() + sscanf().

#### ❓ Câu hỏi vấn đáp:
> "Giả sử một tệp văn bản `sensors.csv` lưu dữ liệu cảm biến theo từng dòng có định dạng `SensorID,Temperature,Humidity` (ví dụ: `SEN01,26.5,78`). Tại sao việc dùng trực tiếp `fscanf(fp, "%[^,],%lf,%d", ...)` trong vòng lặp dễ bị treo hoặc lệch dòng khi tệp có một dòng bị hỏng định dạng? Hãy viết hàm C sử dụng kết hợp `fgets()` và `sscanf()` để đọc tệp này, bỏ qua các dòng lỗi định dạng và tính nhiệt độ trung bình của các dòng hợp lệ."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu một dòng trong tệp `sensors.csv` dài hơn kích thước bộ đệm `char line[128]` truyền vào `fgets()`, làm thế nào để nhận biết dòng đó chưa được đọc hết và xử lý phần dư của dòng đó trước khi đọc dòng tiếp theo?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Tại sao dùng trực tiếp `fscanf()` trên tệp văn bản dễ gây lỗi dây chuyền:
- Hàm `fscanf()` đọc trực tiếp trên luồng byte và không nhận biết ranh giới từng dòng vật lý. Nếu dòng số 5 bị lỗi định dạng (ví dụ thiếu trường hoặc ghi chữ `"ERR"` vào cột số thực), `fscanf()` dừng lại ngay tại ký tự lỗi đó trong bộ đệm tệp. Ở các vòng lặp tiếp theo, con trỏ tệp bị kẹt tại chỗ hoặc đọc lệch cột của dòng sau nối vào dòng trước, làm hỏng toàn bộ dữ liệu phía sau.
- **Giải pháp chuẩn công nghiệp (`fgets` + `sscanf`):** Dùng `fgets(line, sizeof(line), fp)` để đọc trọn vẹn **từng dòng độc lập** vào bộ đệm RAM trước, sau đó dùng `sscanf()` phân tích chuỗi `line`. Nếu dòng số 5 bị lỗi (`sscanf` trả về `< 3`), ta chỉ việc ghi cảnh báo và bỏ qua dòng 5; vòng lặp kế tiếp gọi `fgets` sẽ tự động đọc sang dòng số 6 một cách hoàn toàn sạch sẽ!

2. Cài đặt hàm đọc và phân tích tệp `sensors.csv` bằng `fgets()` + `sscanf()`:
```c
#include <stdio.h>
#include <string.h>

int parseSensorCsv(const char *filename, double *out_avg_temp) {
    if (filename == NULL || out_avg_temp == NULL) return -1;
    FILE *fp = fopen(filename, "r");
    if (fp == NULL) {
        perror("Khong the mo tep sensors.csv");
        return -1;
    }

    char line[256];
    char sensor_id[32];
    double temp = 0.0, sum_temp = 0.0;
    int humidity = 0, valid_count = 0, line_no = 0;

    while (fgets(line, sizeof(line), fp) != NULL) {
        line_no++;
        // Bỏ qua dòng trống hoặc dòng tiêu đề/comment bắt đầu bằng '#'
        if (line[0] == '\n' || line[0] == '\r' || line[0] == '#') continue;

        if (sscanf(line, "%31[^,],%lf,%d", sensor_id, &temp, &humidity) == 3) {
            sum_temp += temp;
            valid_count++;
        } else {
            fprintf(stderr, "Canh bao: Dong %d sai dinh dang CSV, da bo qua.\n", line_no);
        }
    }

    fclose(fp);
    *out_avg_temp = (valid_count > 0) ? (sum_temp / valid_count) : 0.0;
    return valid_count;
}
```

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân tích nhược điểm của fscanf() khi gặp dòng lỗi và ưu thế của fgets() + sscanf()** | **3.5đ** | Giải thích rõ cơ chế cô lập lỗi theo từng dòng của fgets giúp không làm kẹt con trỏ luồng tệp. |
| **Cài đặt đúng vòng lặp fgets() kết hợp kiểm tra sscanf() == 3** | **3.5đ** | Sử dụng đúng đặc tả %31[^,],%lf,%d trong sscanf và kiểm tra giá trị trả về bằng đúng 3 trường. |
| **Quản lý tài nguyên tệp an toàn (kiểm tra fopen NULL, fclose, chia cho 0)** | **3.0đ** | Kiểm tra fp != NULL, gọi fclose(fp) đầy đủ và kiểm tra valid_count > 0 trước khi tính trung bình. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 22. [PRF_EQ22] Phân tích kiến trúc biên dịch đa tệp (Multi-file Compilation) và cơ chế Header Guards (#ifndef / #define / #endif)

- **Mã định danh (Question Code):** `PRF_EQ22`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000464`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 16-22 — *Multi-File C Architecture, Header Guards & Internal vs External Linkage*
- **Mục tiêu đánh giá:** Đánh giá năng lực phân tích quy trình tách chương trình C thành nhiều tệp .h/.c, phòng chống lỗi định nghĩa lặp và kiểm soát phạm vi liên kết (Linkage).

#### ❓ Câu hỏi vấn đáp:
> "Khi tổ chức một dự án C lớn thành nhiều tệp tiêu đề (`.h`) và tệp mã nguồn (`.c`), hãy phân tích nguyên nhân xảy ra lỗi biên dịch `redefinition of struct ...` và lỗi liên kết `multiple definition of ...`. Cơ chế Header Guards (`#ifndef` / `#define` / `#endif`) hoạt động như thế nào để ngăn chặn lỗi nạp chồng tệp `.h` trong cùng một Translation Unit, và tại sao không được đặt định nghĩa biến toàn cục (không kèm `extern` hay `static`) bên trong tệp `.h`?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu bạn viết một hàm tiện ích nội bộ `static int helper(int x)` trong tệp `math_utils.c`, từ khóa `static` ở mức hàm này tác động như thế nào đến Bảng ký hiệu (Symbol Table) của trình liên kết (Linker)?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Phân biệt lỗi Biên dịch (`Redefinition`) và lỗi Liên kết (`Multiple Definition`):
- **Translation Unit (Đơn vị dịch):** Là một tệp `.c` sau khi Bộ tiền xử lý (Preprocessor) đã nhúng toàn bộ nội dung các tệp `.h` được `#include` vào.
- **Lỗi `Redefinition of struct ...` (Compile-time Error):** Xảy ra trong **cùng một Translation Unit** khi tệp `main.c` vừa `#include "student.h"`, vừa `#include "course.h"`, mà bản thân `course.h` lại `#include "student.h"` lần nữa $\to$ cấu trúc `struct Student` bị khai báo 2 lần trong cùng một tệp `.c`.
- **Lỗi `Multiple definition of ...` (Link-time Error):** Xảy ra ở giai đoạn **Linker** ghép nhiều tệp đối tượng (`main.o`, `course.o`) lại với nhau. Nếu tệp `.h` chứa định nghĩa thân hàm hoặc định nghĩa biến toàn cục `int g_count = 0;`, thì cả `main.o` và `course.o` đều xuất khẩu ký hiệu `g_count` (External Linkage) $\to$ Linker báo lỗi trùng lặp ký hiệu toàn cục!

2. Cơ chế hoạt động của Header Guards (`#ifndef` / `#define` / `#endif`):
```c
// Tệp student.h
#ifndef STUDENT_H
#define STUDENT_H

typedef struct {
    char id[12];
    double gpa;
} Student;

extern int g_total_students; // CHỈ KHAI BÁO (Declaration) với extern, không cấp phát bộ nhớ
void printStudent(const Student *s); // Nguyên mẫu hàm (Prototype)

#endif /* STUDENT_H */
```
- Lần đầu tiên `student.h` được nạp vào `main.c`, macro `STUDENT_H` chưa tồn tại nên Preprocessor định nghĩa `STUDENT_H` và giữ lại nội dung. Ở lần `#include "student.h"` thứ hai trong cùng `main.c`, vì `STUDENT_H` đã được định nghĩa, Preprocessor loại bỏ toàn bộ nội dung bên trong, chống lỗi `Redefinition` 100%.
- **Lưu ý quan trọng:** Header Guards chỉ có tác dụng trong phạm vi **từng Translation Unit riêng lẻ** (mỗi tệp `.c` biên dịch độc lập). Vì vậy, biến toàn cục dùng chung bắt buộc phải khai báo `extern int g_total_students;` trong `.h` và chỉ định nghĩa cấp phát bộ nhớ `int g_total_students = 0;` trong **đúng một tệp `.c` duy nhất**.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân biệt rõ lỗi Redefinition (Compile-time) và Multiple Definition (Link-time)** | **3.5đ** | Giải thích chính xác khái niệm Translation Unit và sự khác biệt giữa lỗi nạp lặp trong 1 file .c với lỗi trùng symbol khi Linker ghép nhiều file .o. |
| **Trình bày đúng cơ chế và cú pháp Header Guards (#ifndef/#define/#endif)** | **3.5đ** | Viết đúng cấu trúc Header Guard và giải thích cách Preprocessor bỏ qua nội dung nạp lặp lần 2. |
| **Giải thích quy tắc dùng extern trong .h và định nghĩa biến trong duy nhất 1 tệp .c** | **3.0đ** | Chỉ rõ vì sao Header Guard không ngăn được lỗi Linker nếu đặt định nghĩa biến toàn cục trong file .h. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 23. [PRF_EQ23] Phân tích lỗi trả về địa chỉ biến cục bộ trên Stack (Returning Pointer to Local Variable) và 2 mẫu thiết kế thay thế an toàn

- **Mã định danh (Question Code):** `PRF_EQ23`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 30-31 — *Stack Frame Lifetime Hazard: Returning Local Address vs Caller-Allocated Buffer*
- **Mục tiêu đánh giá:** Đánh giá năng lực chẩn đoán lỗi con trỏ treo do trả về địa chỉ mảng cục bộ trên Call Stack và thiết kế giao diện hàm C an toàn bộ nhớ.

#### ❓ Câu hỏi vấn đáp:
> "Hãy phân tích đoạn mã C sau đây và giải thích tại sao hàm `createGreeting()` lại chứa lỗi bộ nhớ nghiêm trọng dù có thể biên dịch không báo lỗi (chỉ cảnh báo): `char* createGreeting(const char *name) { char buf[64]; snprintf(buf, sizeof(buf), "Xin chao %s", name); return buf; }`. Trình bày 2 phương pháp thiết kế lại hàm này chuẩn công nghiệp trong C và phân tích ưu/nhược điểm của từng phương pháp."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu một lập trình viên sửa nhanh hàm trên bằng cách thêm từ khóa `static` trước mảng cục bộ (`static char buf[64];`), giải pháp này có nhược điểm nguy hiểm nào khi gọi hàm nhiều lần trong cùng một biểu thức `printf("%s va %s", createGreeting("An"), createGreeting("Binh"))`?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Phân tích nguyên nhân gốc rễ (Stack Frame Unwinding & Dangling Pointer):
- Trong hàm `createGreeting()`, mảng `char buf[64]` là biến cục bộ có lớp lưu trữ tự động (`auto`), được cấp phát trên **Stack Frame** của riêng hàm `createGreeting`.
- Ngay khi câu lệnh `return buf;` thực thi, hàm `createGreeting` kết thúc, con trỏ ngăn xếp (`Stack Pointer - ESP/RSP`) lập tức lùi về để **thu hồi toàn bộ Stack Frame** của hàm này.
- Địa chỉ trả về cho hàm `main()` trở thành một **Con trỏ lơ lửng (Dangling Pointer)** trỏ vào vùng nhớ Stack đã bị giải phóng. Khi hàm tiếp theo (như `printf`) được gọi, Stack Frame mới của `printf` sẽ ghi đè trực tiếp lên vùng nhớ cũ của `buf`, làm chuỗi bị biến dạng thành ký tự rác hoặc gây crash (**Undefined Behavior**).

2. Hai phương pháp thiết kế lại chuẩn công nghiệp:
- **Phương pháp 1: Hàm gọi tự cấp phát bộ đệm và truyền vào kèm kích thước (Caller-Allocated Buffer — Khuyên dùng nhất trong C hệ thống/nhúng):**
```c
int createGreetingSafe(const char *name, char *out_buf, size_t buf_size) {
    if (name == NULL || out_buf == NULL || buf_size == 0) return -1;
    return snprintf(out_buf, buf_size, "Xin chao %s", name);
}
```
  + *Ưu điểm:* Không tốn chi phí cấp phát Heap (`malloc`), không bao giờ bị Memory Leak, an toàn tuyệt đối trong môi trường đa luồng.
- **Phương pháp 2: Cấp phát động trên vùng nhớ Heap (`malloc`) và chuyển giao quyền sở hữu cho Caller:**
```c
char* createGreetingHeap(const char *name) {
    if (name == NULL) return NULL;
    size_t len = strlen(name) + 16;
    char *buf = (char *)malloc(len);
    if (buf != NULL) {
        snprintf(buf, len, "Xin chao %s", name);
    }
    return buf; // Caller BẮT BUỘC phải gọi free() sau khi dùng xong
}
```
  + *Ưu điểm:* Linh hoạt với chuỗi có độ dài bất kỳ. *Nhược điểm:* Tốn chi phí Heap và có nguy cơ Memory Leak nếu Caller quên `free()`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân tích đúng cơ chế thu hồi Stack Frame biến buf thành Dangling Pointer** | **4.0đ** | Giải thích rõ vòng đời biến cục bộ trên Stack kết thúc ngay khi hàm return và bị ghi đè bởi lời gọi hàm kế tiếp. |
| **Cài đặt đúng Phương pháp 1 (Caller-Allocated Buffer kèm kích thước)** | **3.0đ** | Viết đúng chữ ký hàm nhận out_buf và buf_size, dùng snprintf an toàn không cần cấp phát động. |
| **Cài đặt đúng Phương pháp 2 (Cấp phát Heap bằng malloc) và so sánh đánh đổi** | **3.0đ** | Tính đúng kích thước cấp phát, kiểm tra malloc != NULL và nêu rõ trách nhiệm gọi free() của Caller. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 24. [PRF_EQ24] Thiết kế ma trận động bằng mảng 1 chiều trải phẳng (1D Flattened Array) và tối ưu hóa CPU Cache Locality

- **Mã định danh (Question Code):** `PRF_EQ24`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO5** (`00000000-0000-0000-0000-000000000465`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 31 & 38 — *Contiguous 1D Flattened Matrix vs Pointer-to-Pointer & Cache Locality*
- **Mục tiêu đánh giá:** Đánh giá năng lực thiết kế ma trận động hiệu năng cao bằng khối nhớ liên tục duy nhất và phân tích tác động của thứ tự duyệt dòng/cột lên bộ nhớ đệm CPU Cache.

#### ❓ Câu hỏi vấn đáp:
> "Thay vì cấp phát ma trận động $R 	imes C$ bằng con trỏ cấp hai `double **mat` (phải gọi `malloc` $R + 1$ lần), hãy trình bày kỹ thuật cấp phát ma trận động bằng **một khối nhớ liên tục duy nhất** (`double *mat`) chỉ với 1 lệnh `malloc()`. Viết hàm C nhân hai ma trận động theo cách biểu diễn này và giải thích tại sao việc đổi thứ tự vòng lặp từ `i-j-k` sang `i-k-j` lại giúp tăng tốc độ thực thi vượt trội nhờ nguyên lý **Spatial Locality (Tính cục bộ không gian)** trên CPU Cache?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trong công thức ánh xạ chỉ số hai chiều `(r, c)` sang mảng một chiều `r * cols + c`, điều gì sẽ xảy ra nếu `r * cols` vượt quá giới hạn kiểu `int` và tại sao ta nên dùng kiểu `size_t` cho các biến chỉ số và kích thước bộ nhớ?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Kỹ thuật cấp phát ma trận động bằng 1 khối nhớ liên tục (`1D Flattened Row-Major Array`):
- Toàn bộ ma trận kích thước $R \times C$ được cấp phát bằng **đúng 1 lệnh `malloc(R * C * sizeof(double))`** và giải phóng chỉ bằng **1 lệnh `free()`**, triệt tiêu hoàn toàn hiện tượng phân mảnh bộ nhớ Heap và không tốn thêm $R \times 8$ bytes cho mảng con trỏ trung gian.
- Công thức ánh xạ tọa độ dòng `r`, cột `c` ($0 \le r < R, 0 \le c < C$) theo thứ tự ưu tiên dòng (`Row-Major Order`):
  `INDEX(r, c, C) = (r) * (C) + (c)`.

2. Cài đặt hàm nhân hai ma trận động tối ưu hóa thứ tự vòng lặp `i-k-j`:
```c
#include <stdlib.h>

double* multiplyMatrices(const double *A, const double *B, size_t M, size_t K, size_t N) {
    if (A == NULL || B == NULL || M == 0 || K == 0 || N == 0) return NULL;
    // Dùng calloc để khởi tạo toàn bộ ma trận kết quả C[M x N] về 0.0
    double *C = (double *)calloc(M * N, sizeof(double));
    if (C == NULL) return NULL;

    // Thứ tự vòng lặp tối ưu Cache: i -> k -> j (duyệt tuần tự theo dòng cho cả B và C)
    for (size_t i = 0; i < M; i++) {
        for (size_t k = 0; k < K; k++) {
            double a_ik = A[i * K + k]; // Giữ cố định trong thanh ghi CPU suốt vòng lặp j
            for (size_t j = 0; j < N; j++) {
                C[i * N + j] += a_ik * B[k * N + j];
            }
        }
    }
    return C;
}
```

3. Phân tích hiệu năng CPU Cache (`Spatial Locality`):
- Trong vòng lặp truyền thống `i-j-k`, vòng trong cùng chạy theo `k` sẽ truy cập `B[k * N + j]`, tức là nhảy cách quãng $N$ phần tử ($N \times 8$ bytes) ở mỗi bước (duyệt theo cột), gây ra **CPU Cache Miss** liên tục.
- Khi đảo thành thứ tự `i-k-j`, vòng trong cùng chạy theo `j` truy cập cả `C[i * N + j]` và `B[k * N + j]` tại các ô nhớ nằm **liền kề nhau liên tiếp trên RAM** (bước nhảy bằng 1 phần tử), tận dụng tối đa một đường Cache Line (64 bytes = 8 số `double`), giúp tốc độ nhân ma trận nhanh hơn từ 3 đến 10 lần.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Trình bày đúng kỹ thuật cấp phát 1 khối nhớ liên tục và công thức ánh xạ r * cols + c** | **3.5đ** | Giải thích rõ ưu điểm 1 lần malloc/free, không phân mảnh Heap và công thức chỉ số Row-major order. |
| **Cài đặt chuẩn hàm nhân ma trận động với calloc và kiểu size_t** | **3.5đ** | Kiểm tra con trỏ NULL, dùng calloc khởi tạo 0.0 và viết đúng công thức tính tích ma trận. |
| **Phân tích nguyên lý Spatial Locality và lợi ích của thứ tự vòng lặp i-k-j** | **3.0đ** | Giải thích rõ vì sao duyệt theo dòng (stride-1) giảm thiểu Cache Miss so với duyệt nhảy cột (stride-N). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 25. [PRF_EQ25] Phân tích bẫy lỗi thiếu ký tự kết thúc '\0' của strncpy() và thiết kế hàm ghép chuỗi an toàn với snprintf()

- **Mã định danh (Question Code):** `PRF_EQ25`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO8** | **DB 3NF CLO8** (`00000000-0000-0000-0000-000000000468`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 47-50 — *String Termination Traps: strncpy Non-Null-Termination & snprintf Truncation Detection*
- **Mục tiêu đánh giá:** Đánh giá khả năng phân tích hành vi biên nguy hiểm của hàm strncpy() và cài đặt hàm ghép đường dẫn tệp có kiểm tra tràn bộ đệm bằng snprintf().

#### ❓ Câu hỏi vấn đáp:
> "Nhiều lập trình viên cho rằng chỉ cần thay `strcpy(dest, src)` bằng `strncpy(dest, src, sizeof(dest))` là chuỗi luôn an toàn tuyệt đối. Hãy phân tích tại sao cách viết này vẫn gây lỗi nghiêm trọng **thiếu ký tự kết thúc chuỗi `'\0'`** khi `strlen(src) >= sizeof(dest)` và gây lãng phí hiệu năng khi `sizeof(dest)` rất lớn? Hãy viết hàm `int buildFilePath(char *out_buf, size_t buf_size, const char *dir, const char *file)` sử dụng `snprintf()` để nối đường dẫn `"dir/file"` an toàn và phát hiện chính xác khi chuỗi bị cắt cụt (Truncation)."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Hàm `strncat(dest, src, n)` khác với `strncpy(dest, src, n)` như thế nào về việc tự động ghi ký tự `'\0'`, và tham số `n` truyền vào `strncat` phải được tính như thế nào để không gây tràn mảng `dest`?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Hai cạm bẫy thiết kế của hàm `strncpy(dest, src, n)` trong chuẩn C:
- **Bẫy 1 — Không tự động ghi ký tự `'\0'` khi nguồn dài bằng hoặc hơn `n`:** Theo đặc tả chuẩn C99/C11, nếu trong `n` byte đầu tiên của `src` không có ký tự `'\0'` (tức là `strlen(src) >= n`), hàm `strncpy()` sẽ sao chép đúng `n` ký tự và **KHÔNG ghi thêm ký tự `'\0'` nào cả**! Khi đó `dest` trở thành mảng ký tự không có null-terminator; mọi lời gọi `printf("%s", dest)` hoặc `strlen(dest)` tiếp theo sẽ đọc tràn qua các biến lân cận trên Stack (**Buffer Over-read**). Nếu bắt buộc dùng `strncpy`, luôn phải ép `dest[sizeof(dest) - 1] = '\0';`.
- **Bẫy 2 — Ghi đè toàn bộ phần dư bằng byte `'\0'` (Zero-Padding Overhead):** Ngược lại, nếu `src` chỉ dài 5 ký tự nhưng `n = 65536`, `strncpy` sẽ sao chép 5 ký tự rồi tiếp tục ghi thêm 65,531 byte `'\0'` vào phần còn lại của `dest`, gây lãng phí chu kỳ CPU không cần thiết.

2. Thiết kế hàm ghép đường dẫn an toàn và phát hiện cắt cụt (Truncation) bằng `snprintf()`:
```c
#include <stdio.h>
#include <stddef.h>

// Trả về 0 nếu thành công trọn vẹn, -1 nếu tham số không hợp lệ hoặc bị cắt cụt (Truncated)
int buildFilePath(char *out_buf, size_t buf_size, const char *dir, const char *file) {
    if (out_buf == NULL || dir == NULL || file == NULL || buf_size == 0) {
        return -1;
    }
    // snprintf LUÔN bảo đảm ghi ký tự '\0' ở cuối (khi buf_size > 0)
    // và trả về số ký tự LẼ RA đã được ghi (không tính '\0') nếu bộ đệm đủ lớn.
    int written = snprintf(out_buf, buf_size, "%s/%s", dir, file);
    if (written < 0 || (size_t)written >= buf_size) {
        // Chuỗi kết quả đã bị cắt cụt vì vượt quá sức chứa của out_buf!
        return -1;
    }
    return 0;
}
```

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân tích đúng 2 cạm bẫy của strncpy (không chèn '\0' khi strlen >= n và zero-padding)** | **4.0đ** | Giải thích chính xác vì sao strncpy(dest, src, sizeof(dest)) làm mất '\0' gây Buffer Over-read. |
| **Cài đặt đúng hàm buildFilePath bằng snprintf()** | **3.0đ** | Kiểm tra tham số NULL/buf_size == 0 và sử dụng snprintf(out_buf, buf_size, "%s/%s", dir, file). |
| **Kiểm tra điều kiện phát hiện cắt cụt chuỗi (size_t)written >= buf_size** | **3.0đ** | Giải thích đúng ý nghĩa giá trị trả về của snprintf và phát hiện chính xác khi chuỗi bị cắt cụt. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 26. [PRF_EQ26] Kỹ thuật cập nhật trực tiếp bản ghi Struct tại chỗ (In-Place Update) trên tệp nhị phân bằng fseek() và ftell()

- **Mã định danh (Question Code):** `PRF_EQ26`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO9** | **DB 3NF CLO9** (`00000000-0000-0000-0000-000000000469`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 56-57 — *Random Access Binary File I/O: In-Place Record Modification with fseek & rb+*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng định vị con trỏ tệp nhị phân với độ phức tạp O(1) để đọc và ghi đè một bản ghi Struct cụ thể mà không cần nạp toàn bộ tệp vào RAM.

#### ❓ Câu hỏi vấn đáp:
> "Giả sử một tệp nhị phân `accounts.dat` lưu trữ hàng triệu bản ghi có kích thước cố định `Account` (`int id; char owner[40]; double balance;`). Làm thế nào để cập nhật số dư mới cho bản ghi tại vị trí chỉ số `index` (0-indexed) với độ phức tạp thời gian $O(1)$ mà không cần đọc toàn bộ tệp vào RAM? Hãy viết hàm C mở tệp ở chế độ cập nhật nhị phân `"rb+"`, kiểm tra `index` có vượt quá số lượng bản ghi thực tế của tệp bằng `fseek()`/`ftell()` hay không, đọc bản ghi lên cập nhật và ghi đè lại đúng vị trí đó."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Theo đặc tả chuẩn ANSI C (C99/C11), khi mở tệp ở chế độ cập nhật (`"rb+"`), tại sao chương trình BẮT BUỘC phải gọi `fseek()` hoặc `fflush()` ở giữa thao tác đọc (`fread`) và thao tác ghi (`fwrite`) liên tiếp trên cùng một luồng `FILE*`?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Nguyên lý truy cập ngẫu nhiên $O(1)$ và quy tắc chuyển đổi Đọc $\leftrightarrow$ Ghi trên luồng `"rb+"`:
- Vì mỗi phần tử `Account` trong tệp nhị phân có kích thước cố định bằng `sizeof(Account)` bytes, bản ghi thứ `index` ($0, 1, 2, \dots$) luôn nằm tại độ lệch byte (Byte Offset) chính xác là: `offset = (long)index * (long)sizeof(Account)`.
- Để xác định tổng số bản ghi hiện có trong tệp: Dịch con trỏ tệp về cuối bằng `fseek(fp, 0L, SEEK_END)`, gọi `long file_size = ftell(fp)`, suy ra tổng số bản ghi là `total_records = file_size / sizeof(Account)`.
- **Quy tắc sống còn của chuẩn C khi dùng `"rb+"`:** Trên một luồng cập nhật (`+`), một thao tác đọc (`fread`) **không được theo sau trực tiếp** bởi một thao tác ghi (`fwrite`) (và ngược lại) nếu không có một lời gọi định vị lại con trỏ tệp (`fseek`, `fsetpos`, `rewind`) hoặc `fflush` ở giữa để đồng bộ hóa bộ đệm đọc/ghi nội bộ của `FILE*`.

2. Cài đặt hàm cập nhật bản ghi tại chỗ (`updateAccountBalanceAt`) chuẩn công nghiệp:
```c
#include <stdio.h>

typedef struct {
    int id;
    char owner[40];
    double balance;
} Account;

int updateAccountBalanceAt(const char *filename, size_t index, double delta) {
    if (filename == NULL) return -1;
    FILE *fp = fopen(filename, "rb+"); // Mở đọc + ghi nhị phân, không xóa nội dung cũ
    if (fp == NULL) return -1;

    // 1. Kiểm tra kích thước tệp để xác nhận index hợp lệ
    if (fseek(fp, 0L, SEEK_END) != 0) { fclose(fp); return -1; }
    long file_size = ftell(fp);
    if (file_size < 0 || (size_t)file_size < (index + 1) * sizeof(Account)) {
        fclose(fp);
        return -1; // Index nằm ngoài phạm vi tệp!
    }

    long target_offset = (long)(index * sizeof(Account));
    Account acc;

    // 2. Nhảy tới vị trí bản ghi để đọc
    if (fseek(fp, target_offset, SEEK_SET) != 0 || fread(&acc, sizeof(Account), 1, fp) != 1) {
        fclose(fp);
        return -1;
    }

    // 3. Cập nhật số dư và gọi fseek lùi lại đúng target_offset trước khi fwrite
    acc.balance += delta;
    if (fseek(fp, target_offset, SEEK_SET) != 0 || fwrite(&acc, sizeof(Account), 1, fp) != 1) {
        fclose(fp);
        return -1;
    }

    fflush(fp);
    fclose(fp);
    return 0;
}
```

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích công thức tính Byte Offset O(1) và kiểm tra giới hạn tệp bằng SEEK_END + ftell** | **3.5đ** | Nêu đúng công thức offset = index * sizeof(Account) và cách tính tổng số bản ghi từ ftell(fp). |
| **Cài đặt đúng quy trình mở tệp rb+, đọc fread, cập nhật và ghi đè fwrite** | **3.5đ** | Sử dụng đúng chế độ rb+ (không dùng wb+ vì sẽ xóa sạch tệp) và kiểm tra giá trị trả về của fread/fwrite. |
| **Giải thích và thực hiện gọi fseek() giữa fread() và fwrite()** | **3.0đ** | Nêu rõ vừa để lùi con trỏ tệp về đầu bản ghi vừa tuân thủ chuẩn C khi chuyển từ trạng thái đọc sang ghi trên luồng rb+. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---
