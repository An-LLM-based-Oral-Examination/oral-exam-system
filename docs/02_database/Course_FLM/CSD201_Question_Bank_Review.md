# TÀI LIỆU RÀ SOÁT NGÂN HÀNG CÂU HỎI VẤN ĐÁP: CSD201 — DATA STRUCTURES AND ALGORITHMS

> **Học phần:** CSD201 — Data Structures and Algorithms (Java)  
> **Quyết định ban hành:** 358/QĐ-ĐHFPT ngày 04/03/2024  
> **Giáo trình chính:** Michael T. Goodrich, Roberto Tamassia, Michael H. Goldwasser: *Data Structures and Algorithms in Java*, 6th Edition, John Wiley & Sons.  
> **Đơn vị biên soạn:** Tiểu ban Đảm bảo Chất lượng Học thuật & Đánh giá Vấn đáp AI (FA26SE166)  
> **Mã tệp tài liệu:** `CSD201_Question_Bank_Review.md` | **Phiên bản:** v1.0.0 (Chính thức)  
> **Tổng số câu hỏi:** **62 câu hỏi vấn đáp kỹ thuật chuẩn hóa** (36 câu Kho Luyện tập + 26 câu Kho Thi thật)
> **Tiêu chuẩn kỹ thuật bắt buộc:** 100% câu hỏi có Model Answer chuyên sâu (>= 50 ký tự), 100% có Barem Rubric tổng điểm đúng 10.0đ, 100% có Follow-up Prompt, Zero Placeholder.

---

## PHẦN 1: BẢNG TỔNG QUAN HỌC PHẦN VÀ MA TRẬN PHÂN BỔ CÂU HỎI

### 1.1. Thông số học phần CSD201

| Thuộc tính | Giá trị chi tiết từ Syllabus FLM và Hệ thống |
|---|---|
| **Mã môn học (Course Code)** | **CSD201** |
| **Tên môn học (Course Name)** | Data Structures and Algorithms (Cấu trúc dữ liệu và giải thuật) |
| **Số tín chỉ (Credits)** | 3 tín chỉ (45 giờ lý thuyết + thực hành phòng Lab, 102.6 giờ tự học) |
| **Môn tiên quyết (Prerequisite)** | PRO192 (Object-Oriented Programming) |
| **Ngôn ngữ thực hành chính** | **Java (JDK 17/21)**, Eclipse / IntelliJ IDEA |
| **Cơ cấu đánh giá** | Progress Tests (20%) + Assignments (20%) + Practical Exam PE (30%) + Final Exam FE (30%) |
| **Điều kiện đạt môn** | FE >= 4.0 / 10.0 và Điểm tổng kết môn Average >= 5.0 / 10.0 |

### 1.2. Ánh xạ Chuẩn đầu ra (8 FLM CLOs -> 4 Database 3NF CLOs)

| Mã CLO FLM | Mô tả chi tiết theo FLM Syllabus | Mức Bloom | Ánh xạ CLO 3NF (DB) | GUID Khóa chính CSDL (`course_clos`) |
|:---:|---|:---:|:---:|---|
| **CLO1** | Danh sách, mảng và các dạng Danh sách liên kết đơn/đôi/vòng | Understand / Apply | **CLO2** (Linear Structures) | `00000000-0000-0000-0000-000000000562` |
| **CLO2** | Ngăn xếp (Stack), Hàng đợi (Queue), Deque và Priority Queue | Understand / Apply | **CLO2** (Linear Structures) | `00000000-0000-0000-0000-000000000562` |
| **CLO3** | Định nghĩa đệ quy, hàm đệ quy, Call Stack và khử đệ quy đuôi | Understand / Analyze | **CLO1** (Complexity & Recursion) | `00000000-0000-0000-0000-000000000561` |
| **CLO4** | Cây tổng quát, Cây nhị phân, Cây BST, Cây AVL và Binary Heap | Understand / Apply / Analyze | **CLO3** (Trees & Heaps) | `00000000-0000-0000-0000-000000000563` |
| **CLO5** | Đồ thị, duyệt BFS/DFS, đường đi Dijkstra, cây khung Prim/Kruskal | Understand / Apply / Analyze | **CLO4** (Graph Algorithms) | `00000000-0000-0000-0000-000000000564` |
| **CLO6** | Các thuật toán sắp xếp cơ bản và nâng cao (Quick, Merge, Heap) | Understand / Analyze | **CLO1** (Complexity & Sorting) | `00000000-0000-0000-0000-000000000561` |
| **CLO7** | Bảng băm, hàm băm, xử lý xung đột, hệ số tải và Rehashing | Understand / Apply / Analyze | **CLO1** (Search & Hashing) | `00000000-0000-0000-0000-000000000561` |
| **CLO8** | Xử lý chuỗi, so khớp mẫu KMP và thuật toán nén Huffman/LZW | Understand / Apply | **CLO2** (Text & Compression) | `00000000-0000-0000-0000-000000000562` |

### 1.3. Ma trận Phân bổ 62 Câu hỏi Vấn đáp

| Kho câu hỏi (Target Pool) | Quy mô | Phân bổ Cấp độ Bloom | Phân bổ Độ khó (Difficulty) | Mục đích sử dụng nghiệp vụ |
|---|:---:|---|---|---|
| **Kho Luyện tập & Thi thử (`practice_questions`)** | **36 câu** | 12 Understand, 12 Apply, 12 Analyze | **12 Easy, 12 Medium, 12 Hard** (Phủ đều 100%) | Phục vụ sinh viên tự luyện tập on-demand (MF-01) và thi thử tính giờ (MF-02) |
| **Kho Thi thật phòng Lab (`exam_questions`)** | **26 câu** | **6 Remember, 8 Understand, 7 Apply, 5 Analyze** | 6 Easy, 15 Medium, 5 Hard | Phục vụ Cán bộ Khảo thí tổ chức thi vấn đáp phòng Lab Kiosk (MF-04) |
| **TỔNG CỘNG** | **62 câu** | **6 Remember, 20 Understand, 19 Apply, 17 Analyze** | **18 Easy, 27 Medium, 17 Hard** | **Đáp ứng vượt mức yêu cầu >= 60 câu hỏi/môn** |

---

## PHẦN 2: BẢNG CHỈ MỤC DANH MỤC 62 CÂU HỎI VẤN ĐÁP CSD201

### 2.1. Chỉ mục 36 câu hỏi Kho Luyện tập (`practice_questions`: `CSD_PQ01` — `CSD_PQ36`)

| Mã câu hỏi | Tiêu đề câu hỏi vấn đáp kỹ thuật | FLM CLO | DB CLO | Mức Bloom | Độ khó | Buổi học (Session) | Cụm kỹ thuật trọng tâm |
|:---:|---|:---:|:---:|:---:|:---:|:---:|---|
| **CSD_PQ01** | Phân tích Ký pháp Big-O và So sánh Tốc độ Tăng trưởng Thời gian | CLO1 | CLO1 | `Understand` | **Easy** | Session 1 | Big-O Analysis & Algorithm Complexity |
| **CSD_PQ02** | Cơ chế Cấp phát Bộ nhớ và Chi phí Truy xuất giữa Mảng và Singly Linked List | CLO1 | CLO2 | `Understand` | **Easy** | Session 1-2 | Arrays vs Linked Lists |
| **CSD_PQ03** | Cấu trúc Nút và Ưu thế của Doubly Linked List so với Singly Linked List | CLO1 | CLO2 | `Understand` | **Easy** | Session 2 | Doubly Linked Lists |
| **CSD_PQ04** | Nguyên lý LIFO và Ba Thao tác Cơ bản của Ngăn xếp Stack | CLO2 | CLO2 | `Understand` | **Easy** | Session 5 | Stacks |
| **CSD_PQ05** | Cơ chế FIFO của Hàng đợi Queue và Phân biệt Queue với Deque | CLO2 | CLO2 | `Understand` | **Easy** | Session 7 | Queues & Deque |
| **CSD_PQ06** | Điều kiện Dừng (Base Case) và Bước Đệ quy trong Cấu trúc Hàm Đệ quy | CLO3 | CLO1 | `Understand` | **Easy** | Session 11 | Recursion Fundamentals |
| **CSD_PQ07** | Các Khái niệm Cơ bản của Cây Nhị phân: Gốc, Lá, Chiều cao và Bậc | CLO4 | CLO3 | `Understand` | **Easy** | Session 15-16 | Binary Tree Terminology |
| **CSD_PQ08** | Đặc tính Thứ tự của Cây Tìm kiếm Nhị phân (BST) và Phép duyệt In-Order | CLO4 | CLO3 | `Understand` | **Easy** | Session 19 | BST In-Order Traversal |
| **CSD_PQ09** | So sánh Biểu diễn Đồ thị bằng Ma trận Kề và Danh sách Kề | CLO5 | CLO4 | `Understand` | **Easy** | Session 29 | Graph Representation |
| **CSD_PQ10** | Nguyên lý Đổi chỗ và Độ phức tạp Thời gian của Thuật toán Bubble Sort | CLO6 | CLO1 | `Understand` | **Easy** | Session 41 | Bubble Sort |
| **CSD_PQ11** | Khái niệm Bảng băm, Hàm băm và Nguyên nhân Gây ra Hiện tượng Xung đột | CLO7 | CLO1 | `Understand` | **Easy** | Session 49 | Hashing Concepts |
| **CSD_PQ12** | Phân biệt Nén Dữ liệu Không Tổn hao và Nén Có Tổn hao | CLO8 | CLO2 | `Understand` | **Easy** | Session 55 | Text Compression Concepts |
| **CSD_PQ13** | Cài đặt Thuật toán Đảo ngược Danh sách Liên kết Đơn tại chỗ | CLO1 | CLO2 | `Apply` | **Medium** | Session 1 | Linked List Manipulation |
| **CSD_PQ14** | Ứng dụng Stack trong Bài toán Kiểm tra Dãy Ngoặc Hợp lệ | CLO2 | CLO2 | `Apply` | **Medium** | Session 5 | Stack Applications |
| **CSD_PQ15** | Cài đặt Hàng đợi Vòng (Circular Queue) bằng Mảng Cố định với Modulo | CLO2 | CLO2 | `Apply` | **Medium** | Session 7 | Circular Queue Implementation |
| **CSD_PQ16** | Cơ chế Call Stack và Phân tích Tràn Bộ nhớ trong Đệ quy Tính Fibonacci | CLO3 | CLO1 | `Apply` | **Medium** | Session 12 | Call Stack & Recursion Tree |
| **CSD_PQ17** | Cài đặt Thuật toán Duyệt Cây Pre-order, In-order và Post-order bằng Java | CLO4 | CLO3 | `Apply` | **Medium** | Session 16 | Tree Traversals |
| **CSD_PQ18** | Quy trình Xóa Nút có Hai Con trên Cây BST bằng In-order Successor | CLO4 | CLO3 | `Apply` | **Medium** | Session 20 | BST Deletion |
| **CSD_PQ19** | Thao tác Chèn Phần tử vào Min-Heap và Quy trình Vun Đống Ngược | CLO4 | CLO3 | `Apply` | **Medium** | Session 24 | Binary Heap Operations |
| **CSD_PQ20** | Cài đặt Thuật toán Duyệt Đồ thị Theo Chiều Rộng (BFS) | CLO5 | CLO4 | `Apply` | **Medium** | Session 30 | Breadth-First Search (BFS) |
| **CSD_PQ21** | Cài đặt Thuật toán Duyệt Đồ thị Theo Chiều Sâu (DFS) Xác định Thành phần Liên thông | CLO5 | CLO4 | `Apply` | **Medium** | Session 30 | Depth-First Search & Connected Components |
| **CSD_PQ22** | Thuật toán Phân hoạch Lomuto trong QuickSort và Cách Lựa chọn Pivot | CLO6 | CLO1 | `Apply` | **Medium** | Session 42 | QuickSort Lomuto Partition |
| **CSD_PQ23** | Cài đặt Chiến lược Giải quyết Xung đột Bảng băm bằng Separate Chaining | CLO7 | CLO1 | `Apply` | **Medium** | Session 49 | Separate Chaining Collision Handling |
| **CSD_PQ24** | Cài đặt Thuật toán Tìm kiếm Mẫu Chuỗi Đối chiếu Thô (Brute-Force Pattern Matching) | CLO8 | CLO2 | `Apply` | **Medium** | Session 53 | String Pattern Matching |
| **CSD_PQ25** | Ứng dụng Định lý Thợ (Master Theorem) Phân tích Phương trình Truy hồi Đệ quy | CLO1 | CLO1 | `Analyze` | **Hard** | Session 1 | Master Theorem & Recurrence Relations |
| **CSD_PQ26** | Thuật toán Floyd's Tortoise and Hare Phát hiện Chu trình trong Singly Linked List | CLO1 | CLO2 | `Analyze` | **Hard** | Session 2 | Floyd's Cycle-Finding Algorithm |
| **CSD_PQ27** | Phân tích Chi phí Priority Queue Cài đặt bằng Mảng vs Binary Heap | CLO2 | CLO2 | `Analyze` | **Hard** | Session 8 | Priority Queue Implementations |
| **CSD_PQ28** | Kỹ thuật Khử Đệ quy Đuôi Tối ưu hóa Bộ nhớ Call Stack | CLO3 | CLO1 | `Analyze` | **Hard** | Session 12 | Tail Recursion Elimination |
| **CSD_PQ29** | Phân tích Cơ chế Tự Cân bằng và 4 Phép Xoay trên Cây AVL | CLO4 | CLO3 | `Analyze` | **Hard** | Session 23 | AVL Tree Rotations |
| **CSD_PQ30** | Phân tích Thuật toán HeapSort: Giai đoạn Build-Heap Tuyến tính O(n) | CLO4 | CLO3 | `Analyze` | **Hard** | Session 24 | HeapSort & Linear Build-Heap Analysis |
| **CSD_PQ31** | Thuật toán Dijkstra Tìm Đường đi Ngắn nhất: Bản chất Tham lam và Cạnh Âm | CLO5 | CLO4 | `Analyze` | **Hard** | Session 34 | Dijkstra Algorithm & Negative Weights |
| **CSD_PQ32** | So sánh Chuyên sâu Thuật toán Prim và Kruskal Tìm Cây Khung Nhỏ nhất | CLO5 | CLO4 | `Analyze` | **Hard** | Session 37-38 | Minimum Spanning Trees: Prim vs Kruskal |
| **CSD_PQ33** | Phân tích Điều kiện Tồn tại Chu trình Euler và So sánh với Chu trình Hamilton | CLO5 | CLO4 | `Analyze` | **Hard** | Session 38 | Eulerian vs Hamiltonian Cycles |
| **CSD_PQ34** | So sánh Toàn diện QuickSort vs MergeSort | CLO6 | CLO1 | `Analyze` | **Hard** | Session 45 | QuickSort vs MergeSort Comparison |
| **CSD_PQ35** | Hiện tượng Cụm Chính trong Linear Probing, Hệ số Tải và Kỹ thuật Rehashing | CLO7 | CLO1 | `Analyze` | **Hard** | Session 50 | Open Addressing & Rehashing |
| **CSD_PQ36** | Nguyên lý Xây dựng Cây Mã hóa Huffman Tối ưu Tiền tố và Thuật toán Giải nén Bit | CLO8 | CLO2 | `Analyze` | **Hard** | Session 57 | Huffman Coding Algorithm |

### 2.2. Chỉ mục 26 câu hỏi Kho Thi thật (`exam_questions`: `CSD_EQ01` — `CSD_EQ26`)

| Mã câu hỏi | Tiêu đề câu hỏi vấn đáp kỹ thuật | FLM CLO | DB CLO | Mức Bloom | Độ khó | Buổi học (Session) | Cụm kỹ thuật trọng tâm |
|:---:|---|:---:|:---:|:---:|:---:|:---:|---|
| **CSD_EQ01** | Định nghĩa Ký pháp Tiệm cận Big-O, Big-Omega và Big-Theta | CLO1 | CLO1 | `Remember` | **Easy** | Session 1 | Asymptotic Notations |
| **CSD_EQ02** | Định nghĩa Cấu trúc Dữ liệu Stack, Queue và Phân biệt LIFO vs FIFO | CLO2 | CLO2 | `Remember` | **Easy** | Session 5 | Stack and Queue Definitions |
| **CSD_EQ03** | Định nghĩa Cây Nhị phân Đầy đủ và Cây Nhị phân Hoàn chỉnh | CLO4 | CLO3 | `Remember` | **Easy** | Session 15 | Binary Tree Types |
| **CSD_EQ04** | Phát biểu Định lý Bắt tay (Handshaking Lemma) trong Lý thuyết Đồ thị | CLO5 | CLO4 | `Remember` | **Easy** | Session 22 | Graph Handshaking Lemma |
| **CSD_EQ05** | Định nghĩa Tính Ổn định của Thuật toán Sắp xếp và Phân loại | CLO6 | CLO1 | `Remember` | **Easy** | Session 41 | Sorting Stability |
| **CSD_EQ06** | Định nghĩa Hàm Băm Hoàn Hảo và Tiêu chuẩn của Hàm Băm Lý tưởng | CLO7 | CLO1 | `Remember` | **Easy** | Session 49 | Perfect Hash Functions |
| **CSD_EQ07** | Phân tích Chi phí Thao tác Chèn/Xóa ở Đầu giữa Mảng và Singly Linked List | CLO1 | CLO2 | `Understand` | **Easy** | Session 1-2 | Array vs Linked List Insertion Complexity |
| **CSD_EQ08** | Cơ chế Hoạt động của Hàng đợi Ưu tiên và Sự Khác biệt với Hàng đợi FIFO | CLO2 | CLO2 | `Understand` | **Easy** | Session 8 | Priority Queue vs FIFO Queue |
| **CSD_EQ09** | Cơ chế Tràn Ngăn xếp Call Stack và Vai trò của Base Case trong Đệ quy | CLO3 | CLO1 | `Understand` | **Easy** | Session 11 | Call Stack & StackOverflowError |
| **CSD_EQ10** | Hiện tượng Thoái hóa của Cây BST khi Chèn Dãy Dữ liệu Đã Sắp xếp | CLO4 | CLO3 | `Understand` | **Medium** | Session 19 | BST Degeneration |
| **CSD_EQ11** | Công thức Tính Hệ số Cân bằng và Điều kiện Cân bằng của Cây AVL | CLO4 | CLO3 | `Understand` | **Medium** | Session 23 | AVL Balance Factor Condition |
| **CSD_EQ12** | Phân biệt Bản chất Chiến lược Khám phá giữa Duyệt BFS và Duyệt DFS | CLO5 | CLO4 | `Understand` | **Medium** | Session 30 | BFS vs DFS Exploration Strategy |
| **CSD_EQ13** | Nguyên lý Hoạt động Thời gian Tuyến tính của Thuật toán Radix Sort | CLO6 | CLO1 | `Understand` | **Medium** | Session 46 | Radix Sort Linear Time |
| **CSD_EQ14** | Nguyên lý Nén Run-Length Encoding và Các Trường hợp Gây Phình To Dữ liệu | CLO8 | CLO2 | `Understand` | **Medium** | Session 54 | Run-Length Encoding (RLE) |
| **CSD_EQ15** | Cài đặt Thao tác Chèn Nút Mới vào Sau một Nút trong Doubly Linked List | CLO1 | CLO2 | `Apply` | **Medium** | Session 2 | Doubly Linked List Insertion |
| **CSD_EQ16** | Ứng dụng Thuật toán Shunting-Yard Chuyển đổi Biểu thức Infix sang Postfix | CLO2 | CLO2 | `Apply` | **Medium** | Session 6 | Shunting-Yard Infix to Postfix |
| **CSD_EQ17** | Các Bước Tìm Nút Thế thân và Cập nhật Con trỏ khi Xóa Nút 2 Con trên BST | CLO4 | CLO3 | `Apply` | **Medium** | Session 20 | BST Node Deletion Mechanism |
| **CSD_EQ18** | Quy trình Xây dựng Max-Heap từ Mảng và Thao tác Xóa Phần tử Lớn nhất | CLO4 | CLO3 | `Apply` | **Medium** | Session 24 | Max-Heap Construction and Extract-Max |
| **CSD_EQ19** | Thực thi Từng Bước Thuật toán Dijkstra trên Đồ thị Có Trọng số | CLO5 | CLO4 | `Apply` | **Medium** | Session 34 | Dijkstra Algorithm Execution |
| **CSD_EQ20** | Quy trình Phân hoạch Hoare Partition trong QuickSort | CLO6 | CLO1 | `Apply` | **Medium** | Session 42 | Hoare Partition Procedure |
| **CSD_EQ21** | Thực hiện Chèn Dãy Khóa vào Bảng băm theo Linear và Quadratic Probing | CLO7 | CLO1 | `Apply` | **Medium** | Session 50 | Linear Probing vs Quadratic Probing Execution |
| **CSD_EQ22** | Phân tích Ba Phương pháp Cài đặt Tính Số Fibonacci: Đệ quy vs DP | CLO3 | CLO1 | `Analyze` | **Hard** | Session 12 | Fibonacci Algorithm Comparison |
| **CSD_EQ23** | Phân tích Trường hợp Mất Cân bằng Left-Right trên Cây AVL và Phép Xoay Kép | CLO4 | CLO3 | `Analyze` | **Hard** | Session 23 | AVL LR Imbalance & Double Rotation |
| **CSD_EQ24** | Phân tích Hiệu năng Prim vs Kruskal khi Tìm MST trên Đồ thị Dày và Thưa | CLO5 | CLO4 | `Analyze` | **Hard** | Session 37-38 | Prim vs Kruskal Dense/Sparse Performance |
| **CSD_EQ25** | Phân tích Trường hợp Xấu nhất O(n^2) của QuickSort và Tối ưu Hóa Công nghiệp | CLO6 | CLO1 | `Analyze` | **Hard** | Session 45 | QuickSort Worst-Case & Industrial Optimization |
| **CSD_EQ26** | Phân tích Mảng Tiền tố Thất bại T[i] trong KMP và Chứng minh Tuyến tính O(n + m) | CLO8 | CLO2 | `Analyze` | **Hard** | Session 54 | Knuth-Morris-Pratt (KMP) Failure Function |

---

## PHẦN 3: CHI TIẾT 36 CÂU HỎI KHO LUYỆN TẬP & THI THỬ (`practice_questions`)

> Kho câu hỏi này được lưu trữ độc lập trong bảng `practice_questions`, phục vụ hai luồng nghiệp vụ:
> 1. **MF-01:** Luyện tập tương tác tự do (chế độ On-Demand từng câu hoặc Progressive 3–10 câu tăng dần từ Dễ đến Khó).
> 2. **MF-02:** Thi thử tính giờ có hạn ngạch quota theo ngày, tự động sinh điểm số và bảng Scorecard phân tích theo CLO.
> - Barem Rubric chuẩn hóa 10.0đ (gồm 3-4 tiêu chí con) giúp AI chấm điểm tức thì và sinh CoT giải trình minh bạch.
> - 100% câu hỏi có câu hỏi đào sâu phản biện nối tiếp (`FollowUpPrompt`) khi điểm số rơi vào dải phân vân [4.0, 8.0].

### 1. [CSD_PQ01] Phân tích Ký pháp Big-O và So sánh Tốc độ Tăng trưởng Thời gian

- **Mã định danh (Question Code):** `CSD_PQ01`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO1** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 1 — *Big-O Analysis & Algorithm Complexity*
- **Mục tiêu đánh giá:** Đánh giá khả năng hiểu và phân loại các cấp độ tăng trưởng độ phức tạp thời gian theo ký pháp tiệm cận Big-O.

#### ❓ Câu hỏi vấn đáp:
> "Ký pháp Big-O là gì? Hãy sắp xếp các độ phức tạp thời gian sau theo thứ tự tăng dần về tốc độ tăng trưởng khi kích thước dữ liệu n tiến tới vô cùng: O(n^2), O(1), O(n log n), O(2^n), O(log n), O(n). Với mỗi cấp độ, hãy cho biết một ví dụ thuật toán điển hình."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu một thuật toán A có độ phức tạp O(n^2) với hằng số ẩn c = 1, và thuật toán B có độ phức tạp O(n) nhưng hằng số ẩn c = 1,000,000, thì trong thực tế với quy mô dữ liệu n < 1000 thuật toán nào sẽ thực thi nhanh hơn và tại sao phân tích Big-O lý thuyết lại bỏ qua các hằng số này?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Ký pháp Big-O biểu diễn chặn trên tiệm cận (asymptotic upper bound) của thời gian thực thi hoặc không gian bộ nhớ thuật toán khi kích thước đầu vào n tiến tới vô cùng: f(n) = O(g(n)) nếu tồn tại hằng số c > 0 và n0 >= 1 sao cho f(n) <= c * g(n) với mọi n >= n0.

Thứ tự sắp xếp theo tốc độ tăng trưởng tăng dần (từ nhanh nhất/tốt nhất đến chậm nhất/tồi nhất):
1. O(1) - Hằng số: Truy xuất phần tử mảng qua chỉ số `arr[i]`, lấy đỉnh ngăn xếp `stack.peek()`.
2. O(log n) - Lôgarit: Tìm kiếm nhị phân (Binary Search) trên mảng đã sắp xếp.
3. O(n) - Tuyến tính: Tìm kiếm tuyến tính (Linear Search), duyệt qua một danh sách liên kết.
4. O(n log n) - Tuyến tính - lôgarit: Thuật toán sắp xếp MergeSort, HeapSort.
5. O(n^2) - Bậc hai: Thuật toán sắp xếp cơ bản như Bubble Sort, Selection Sort, lồng 2 vòng lặp duyệt mảng.
6. O(2^n) - Cấp số nhân: Đệ quy ngây thơ tính số Fibonacci, bài toán Tháp Hà Nội.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa ký pháp Big-O** | **2.5đ** | Định nghĩa chính xác bản chất toán học chặn trên tiệm cận O(g(n)) và ý nghĩa trong đánh giá thuật toán. |
| **Sắp xếp thứ tự tăng trưởng** | **4.0đ** | Sắp xếp đúng 100% thứ tự: O(1) < O(log n) < O(n) < O(n log n) < O(n^2) < O(2^n). |
| **Ví dụ thuật toán tương ứng** | **3.5đ** | Cung cấp chính xác ví dụ thuật toán kinh điển cho từng cấp độ độ phức tạp. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 2. [CSD_PQ02] Cơ chế Cấp phát Bộ nhớ và Chi phí Truy xuất giữa Mảng và Singly Linked List

- **Mã định danh (Question Code):** `CSD_PQ02`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO1** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 1-2 — *Arrays vs Linked Lists*
- **Mục tiêu đánh giá:** Kiểm tra mức độ nắm vững sự khác biệt giữa cấu trúc nhớ liên tục (Array) và cấu trúc phân mảnh con trỏ (Linked List).

#### ❓ Câu hỏi vấn đáp:
> "Hãy so sánh Mảng (Array) và Danh sách liên kết đơn (Singly Linked List) về cơ chế cấp phát bộ nhớ, chi phí truy xuất phần tử ngẫu nhiên và chi phí chèn một phần tử vào đầu danh sách."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao khi duyệt tuần tự một mảng (Array traversal), tốc độ thực thi đo bằng số chu kỳ CPU thường nhanh hơn gấp nhiều lần so với duyệt Danh sách liên kết đơn dù cả hai cấu trúc cùng có độ phức tạp thời gian lý thuyết là O(n)?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cơ chế cấp phát bộ nhớ:
- Mảng (Array): Cấp phát một vùng nhớ liên tục (contiguous memory blocks) có kích thước cố định tại thời điểm khởi tạo. Tận dụng tối đa bộ nhớ đệm CPU (CPU cache locality).
- Singly Linked List: Cấp phát bộ nhớ động rời rạc (non-contiguous) trên Heap cho từng nút (Node). Mỗi nút chứa dữ liệu và một con trỏ tham chiếu `next`, dẫn đến tốn bộ nhớ phụ (memory overhead) cho các tham chiếu.

2. Chi phí truy xuất ngẫu nhiên (Random Access):
- Mảng: Đạt O(1) do địa chỉ phần tử được tính trực tiếp bằng công thức: `Address(A[i]) = Base_Address + i * sizeof(Element)`.
- Singly Linked List: Chi phí O(n) do không có chỉ số trực tiếp; muốn truy xuất phần tử thứ k, bắt buộc phải duyệt tuần tự từ nút đầu `head` qua k bước.

3. Chi phí chèn vào đầu (Insert at Head):
- Mảng: Chi phí O(n) do phải dịch chuyển toàn bộ n phần tử hiện có sang phải một vị trí.
- Singly Linked List: Chi phí O(1) vì chỉ cần tạo nút mới, gán `newNode.next = head`, sau đó cập nhật `head = newNode`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **So sánh cơ chế cấp phát bộ nhớ** | **3.0đ** | Nêu rõ bộ nhớ liên tục vs bộ nhớ động phân mảnh và overhead con trỏ tham chiếu. |
| **Phân tích chi phí truy xuất ngẫu nhiên** | **3.5đ** | Giải thích công thức tính địa chỉ O(1) của mảng và thao tác duyệt tuần tự O(n) của danh sách liên kết. |
| **Phân tích chi phí chèn đầu danh sách** | **3.5đ** | Chỉ rõ chi phí dịch mảng O(n) so với phép gán con trỏ O(1) trong Singly Linked List. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 3. [CSD_PQ03] Cấu trúc Nút và Ưu thế của Doubly Linked List so với Singly Linked List

- **Mã định danh (Question Code):** `CSD_PQ03`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO1** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 2 — *Doubly Linked Lists*
- **Mục tiêu đánh giá:** Đánh giá sự hiểu biết về cấu trúc nút 2 con trỏ và ưu nhược điểm của Doubly Linked List.

#### ❓ Câu hỏi vấn đáp:
> "Mô tả cấu trúc của một nút trong Danh sách liên kết đôi (Doubly Linked List). Cấu trúc này mang lại ưu thế gì vượt trội so với Danh sách liên kết đơn khi thực hiện thao tác xóa một nút đã biết trước tham chiếu?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trong thao tác xóa nút `target` khỏi Doubly Linked List khi đã có sẵn tham chiếu nút đó, những trường hợp biên (Edge Cases) nào bắt buộc phải kiểm tra để tránh ném ngoại lệ NullPointerException?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Cấu trúc của một nút trong Doubly Linked List gồm 3 trường thông tin:
1. `data`: Dữ liệu chứa phần tử của danh sách.
2. `prev`: Tham chiếu trỏ tới nút liền trước (nút tiền nhiệm).
3. `next`: Tham chiếu trỏ tới nút liền sau (nút kế nhiệm).

Ưu thế vượt trội khi xóa một nút `target` đã biết trước tham chiếu:
- Trong Singly Linked List: Mặc dù đã có con trỏ trỏ tới `target`, ta không thể truy cập trực tiếp nút đứng trước nó. Ta bắt buộc phải duyệt từ `head` để tìm nút tiền nhiệm `prevNode` thỏa mãn `prevNode.next == target`, dẫn tới thời gian O(n).
- Trong Doubly Linked List: Ta có thể truy cập ngay nút đứng trước thông qua `target.prev` và nút đứng sau qua `target.next`. Thao tác ngắt liên kết được thực hiện tức thì trong thời gian O(1):
```java
if (target.prev != null) target.prev.next = target.next;
if (target.next != null) target.next.prev = target.prev;
```
Đổi lại, Doubly Linked List tốn thêm bộ nhớ cho tham chiếu `prev` và đòi hỏi quản lý cập nhật đồng thời cả hai con trỏ.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Mô tả cấu trúc nút Doubly Linked List** | **3.0đ** | Nêu đủ 3 thành phần: data, prev pointer, next pointer. |
| **Phân tích thao tác xóa nút đã biết tham chiếu** | **4.0đ** | So sánh rõ ràng chi phí O(n) của Singly Linked List vs O(1) của Doubly Linked List kèm logic cập nhật con trỏ. |
| **Đánh giá trade-off bộ nhớ và bảo trì** | **3.0đ** | Nêu được chi phí phụ trội bộ nhớ và sự phức tạp khi cập nhật liên kết kép. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 4. [CSD_PQ04] Nguyên lý LIFO và Ba Thao tác Cơ bản của Ngăn xếp Stack

- **Mã định danh (Question Code):** `CSD_PQ04`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 5 — *Stacks*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức nền tảng về cấu trúc dữ liệu Stack và nguyên lý vận hành.

#### ❓ Câu hỏi vấn đáp:
> "Ngăn xếp (Stack) hoạt động theo nguyên lý nào? Hãy nêu tên, ý nghĩa và độ phức tạp thời gian của 3 thao tác chính trên Stack. Nêu 2 ứng dụng thực tế của Stack trong khoa học máy tính."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi cài đặt Stack bằng mảng động, thao tác `push` khi mảng bị đầy (Array Resizing) có chi phí xấu nhất là O(n); tại sao trong phân tích thuật toán người ta vẫn kết luận độ phức tạp của `push` là O(1) suy rộng (Amortized O(1))?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Stack hoạt động theo nguyên lý LIFO (Last-In-First-Out) - phần tử nào được thêm vào sau cùng sẽ được lấy ra đầu tiên. Mọi thao tác thêm và xóa đều diễn ra tại một đầu duy nhất gọi là đỉnh ngăn xếp (top).

Ba thao tác cơ bản trên Stack:
1. `push(x)`: Thêm phần tử x vào đỉnh ngăn xếp. Độ phức tạp: O(1).
2. `pop()`: Xóa và trả về phần tử nằm ở đỉnh ngăn xếp. Ném ngoại lệ hoặc trả về rỗng nếu Stack rỗng. Độ phức tạp: O(1).
3. `peek()` (hoặc `top()`): Trả về giá trị của phần tử ở đỉnh ngăn xếp mà không xóa nó. Độ phức tạp: O(1).

Hai ứng dụng thực tế kinh điển trong khoa học máy tính:
1. Quản lý ngăn xếp lời gọi hàm (Call Stack): Lưu trữ địa chỉ hoàn trả và các biến cục bộ khi thực thi chương trình hoặc gọi hàm đệ quy.
2. Kiểm tra tính hợp lệ của dấu ngoặc trong trình biên dịch và chức năng Undo/Redo trong các phần mềm soạn thảo văn bản.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích nguyên lý LIFO** | **2.5đ** | Trình bày chính xác cơ chế Last-In-First-Out và khái niệm đỉnh Top. |
| **Đặc tả 3 thao tác cơ bản và Big-O** | **4.5đ** | Mô tả đầy đủ push, pop, peek kèm độ phức tạp O(1) cho mỗi thao tác. |
| **Ứng dụng thực tế** | **3.0đ** | Nêu đúng ít nhất 2 ứng dụng tiêu biểu (Call Stack, Undo/Redo, biên dịch biểu thức). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 5. [CSD_PQ05] Cơ chế FIFO của Hàng đợi Queue và Phân biệt Queue với Deque

- **Mã định danh (Question Code):** `CSD_PQ05`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 7 — *Queues & Deque*
- **Mục tiêu đánh giá:** Đánh giá khả năng hiểu nguyên tắc FIFO của hàng đợi và mở rộng sang hàng đợi hai đầu Deque.

#### ❓ Câu hỏi vấn đáp:
> "Hàng đợi (Queue) hoạt động theo nguyên tắc nào? Phân biệt Hàng đợi tiêu chuẩn (FIFO Queue) và Hàng đợi hai đầu (Deque - Double-Ended Queue) về các vị trí cho phép chèn và xóa dữ liệu."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Làm thế nào để mô phỏng một Hàng đợi (Queue) bằng cách chỉ sử dụng hai Ngăn xếp (Two Stacks), và chi phí suy rộng (Amortized Time) của mỗi thao tác `dequeue()` trong mô hình này là bao nhiêu?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Hàng đợi (Queue) hoạt động theo nguyên tắc FIFO (First-In-First-Out) - phần tử nào vào hàng đợi trước sẽ được phục vụ và lấy ra trước, tương tự dòng người xếp hàng mua vé.

Phân biệt giữa FIFO Queue và Deque:
1. Hàng đợi tiêu chuẩn (Queue):
- Chỉ cho phép thêm phần tử ở một đầu gọi là đuôi hàng đợi (Rear/Tail - thao tác `enqueue`).
- Chỉ cho phép lấy/xóa phần tử ở đầu đối diện gọi là đầu hàng đợi (Front/Head - thao tác `dequeue`).
- Có tính bất đối xứng nghiêm ngặt.

2. Hàng đợi hai đầu (Deque - Double-Ended Queue):
- Cho phép thao tác chèn và xóa ở cả hai đầu của cấu trúc dữ liệu.
- Cung cấp 4 phương thức chính: `addFirst()`, `addLast()`, `removeFirst()`, `removeLast()`.
- Deque là cấu trúc tổng quát hóa: có thể hoạt động vừa như một FIFO Queue (thêm vào cuối, lấy ở đầu) vừa như một LIFO Stack (thêm và lấy cùng ở một đầu). Mọi thao tác cơ bản trên Deque đều đạt O(1).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích nguyên lý FIFO** | **2.5đ** | Trình bày chính xác cơ chế First-In-First-Out của Queue. |
| **Phân tích cơ chế giới hạn của FIFO Queue** | **3.5đ** | Nêu rõ vị trí chèn ở Rear và xóa ở Front. |
| **Đặc tả và so sánh với Deque** | **4.0đ** | Giải thích khả năng chèn/xóa ở 2 đầu của Deque và khả năng mô phỏng cả Stack lẫn Queue. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 6. [CSD_PQ06] Điều kiện Dừng (Base Case) và Bước Đệ quy trong Cấu trúc Hàm Đệ quy

- **Mã định danh (Question Code):** `CSD_PQ06`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO3** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 11 — *Recursion Fundamentals*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức về cấu tạo căn bản của giải thuật đệ quy và vai trò của điều kiện dừng.

#### ❓ Câu hỏi vấn đáp:
> "Một hàm đệ quy chuẩn mực bắt buộc phải bao gồm những thành phần cơ bản nào? Điều gì sẽ xảy ra nếu lập trình viên quên định nghĩa điều kiện dừng (Base Case) hoặc thiết kế điều kiện dừng không bao giờ đạt được?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao việc hàm đệ quy đã có điều kiện dừng (Base Case) vẫn có thể gây ra lỗi nghiêm trọng StackOverflowError nếu bước đệ quy không đảm bảo quy mô dữ liệu n giảm dần hội tụ về Base Case?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Một hàm đệ quy chuẩn mực bắt buộc phải bao gồm 2 thành phần cốt lõi:
1. Điều kiện dừng (Base Case): Tình huống đơn giản nhất của bài toán mà kết quả có thể xác định ngay lập tức mà không cần gọi thêm bất kỳ lời gọi đệ quy nào. Base case đóng vai trò neo chặn, ngăn chặn đệ quy vô hạn.
2. Bước đệ quy (Recursive Step): Lời gọi hàm chính nó với kích thước dữ liệu đầu vào nhỏ hơn hoặc gần với Base Case hơn, phân rã bài toán lớn thành bài toán con tương tự.

Hậu quả khi thiếu hoặc sai Base Case:
- Hàm sẽ liên tục tự gọi chính nó mà không có điểm dừng (infinite recursion).
- Mỗi lời gọi hàm mới sẽ đẩy một khung ngăn xếp (stack frame) chứa địa chỉ hoàn trả và biến cục bộ vào vùng nhớ Call Stack của hệ điều hành/JVM.
- Do bộ nhớ Call Stack có giới hạn cố định, việc tích tụ không ngừng này sẽ nhanh chóng dẫn đến hiện tượng tràn bộ nhớ ngăn xếp, gây ném ngoại lệ nghiêm trọng `java.lang.StackOverflowError` và làm sập ứng dụng.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Đặc tả 2 thành phần đệ quy** | **4.0đ** | Giải thích chính xác vai trò của Base Case và Recursive Step. |
| **Cơ chế phân rã dữ liệu** | **2.5đ** | Nêu rõ dữ liệu đầu vào phải thu hẹp dần về phía Base Case. |
| **Phân tích lỗi StackOverflowError** | **3.5đ** | Mô tả cơ chế tích lũy stack frame trên Call Stack dẫn tới tràn bộ nhớ. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 7. [CSD_PQ07] Các Khái niệm Cơ bản của Cây Nhị phân: Gốc, Lá, Chiều cao và Bậc

- **Mã định danh (Question Code):** `CSD_PQ07`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 15-16 — *Binary Tree Terminology*
- **Mục tiêu đánh giá:** Kiểm tra mức độ hiểu các thuật ngữ và tính chất hình học cấu trúc của cây nhị phân.

#### ❓ Câu hỏi vấn đáp:
> "Định nghĩa Cây nhị phân (Binary Tree). Hãy giải thích các thuật ngữ: Nút gốc (Root), Nút lá (Leaf), Chiều cao của cây (Height) và Bậc của một nút (Degree). Một nút trong cây nhị phân có bậc tối đa là bao nhiêu?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Một cây nhị phân có n nút thì có thể có chiều cao tối thiểu và chiều cao tối đa là bao nhiêu? Trong trường hợp xấu nhất nào chiều cao cây đạt cực đại h = n?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Định nghĩa Cây nhị phân:
Cây nhị phân là một cấu trúc dữ liệu phân cấp phi tuyến tính, bao gồm một tập hợp hữu hạn các nút, trong đó cây có thể rỗng hoặc bao gồm một nút gốc liên kết với tối đa hai cây con nhị phân rời nhau gọi là cây con trái (left subtree) và cây con phải (right subtree).

2. Các thuật ngữ trọng tâm:
- Nút gốc (Root): Nút duy nhất ở đỉnh cây không có nút cha (parent). Mọi đường đi trong cây đều bắt đầu từ gốc.
- Nút lá (Leaf / External Node): Nút không có bất kỳ nút con nào (bậc bằng 0).
- Chiều cao của cây (Height): Độ dài đường đi dài nhất tính bằng số cạnh (hoặc số nút tùy quy ước giáo trình) từ nút gốc tới một nút lá sâu nhất. Chiều cao của cây rỗng thường quy ước là -1, cây chỉ có nút gốc là 0.
- Bậc của một nút (Degree of a node): Số lượng nút con trực tiếp của nút đó.

Trong cây nhị phân, bậc của một nút chỉ có thể nhận các giá trị: 0 (nút lá), 1 (nút có 1 con), hoặc tối đa là 2 (nút có đủ 2 con trái và phải).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa cây nhị phân** | **3.0đ** | Nêu đúng định nghĩa phân cấp và đặc tính tối đa 2 cây con trái/phải. |
| **Giải thích 4 thuật ngữ** | **4.5đ** | Mô tả chính xác nút gốc, nút lá, chiều cao và bậc của nút. |
| **Xác định bậc tối đa của nút** | **2.5đ** | Khẳng định bậc tối đa là 2 và chỉ nhận giá trị 0, 1 hoặc 2. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 8. [CSD_PQ08] Đặc tính Thứ tự của Cây Tìm kiếm Nhị phân (BST) và Phép duyệt In-Order

- **Mã định danh (Question Code):** `CSD_PQ08`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 19 — *BST In-Order Traversal*
- **Mục tiêu đánh giá:** Đánh giá sự hiểu biết về tính chất bất biến của BST và kết quả của phép duyệt In-Order.

#### ❓ Câu hỏi vấn đáp:
> "Hãy nêu đặc tính cốt lõi (BST Property) của một Cây tìm kiếm nhị phân (Binary Search Tree). Khi thực hiện phép duyệt trung thứ tự (In-Order Traversal) trên một cây BST, kết quả dãy khóa thu được có đặc điểm gì đặc biệt?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu ta biết kết quả thứ tự duyệt In-order và Pre-order của một Cây tìm kiếm nhị phân có các khóa phân biệt, ta có thể khôi phục lại cấu trúc hình học duy nhất của cây ban đầu không? Tại sao?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Đặc tính cốt lõi của Cây tìm kiếm nhị phân (BST Property):
Với mọi nút x bất kỳ trong cây BST:
- Mọi nút y thuộc cây con bên trái (Left Subtree) của x đều có khóa nhỏ hơn khóa của x: `key(y) < key(x)`.
- Mọi nút z thuộc cây con bên phải (Right Subtree) của x đều có khóa lớn hơn khóa của x: `key(z) > key(x)`.
(Nếu cho phép khóa trùng nhau, quy ước khóa trùng nằm bên trái hoặc bên phải nhất quán).

2. Đặc điểm của phép duyệt trung thứ tự (In-Order Traversal):
Phép duyệt In-Order tuân theo quy tắc đệ quy: Duyệt cây con Trái -> Thăm nút Gốc -> Duyệt cây con Phải (L-N-R).
Đặc điểm đặc biệt: Khi áp dụng trên BST, phép duyệt In-Order luôn sinh ra một dãy khóa được sắp xếp theo thứ tự tăng dần nghiêm ngặt (Monotonically Increasing Sequence).
Lý do: Theo đặc tính BST, mọi phần tử ở cây con trái (nhỏ hơn gốc) được in ra trước, sau đó đến gốc, rồi mới đến toàn bộ phần tử ở cây con phải (lớn hơn gốc).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Đặc tả tính chất BST** | **4.0đ** | Nêu rõ ràng điều kiện so sánh khóa giữa cây con trái, nút gốc và cây con phải. |
| **Mô tả quy trình duyệt In-Order** | **3.0đ** | Trình bày đúng thứ tự duyệt L-N-R (Left -> Node -> Right). |
| **Chứng minh dãy tăng dần** | **3.0đ** | Chỉ ra dãy khóa thu được luôn tăng dần và giải thích nguyên nhân logic. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 9. [CSD_PQ09] So sánh Biểu diễn Đồ thị bằng Ma trận Kề và Danh sách Kề

- **Mã định danh (Question Code):** `CSD_PQ09`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000564`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 29 — *Graph Representation*
- **Mục tiêu đánh giá:** Kiểm tra khả năng phân biệt hai cấu trúc lưu trữ đồ thị cơ bản và trường hợp ứng dụng phù hợp.

#### ❓ Câu hỏi vấn đáp:
> "Hãy so sánh hai phương pháp biểu diễn đồ thị: Ma trận kề (Adjacency Matrix) và Danh sách kề (Adjacency List) về không gian bộ nhớ sử dụng và chi phí kiểm tra sự tồn tại của một cạnh nối giữa hai đỉnh u và v. Khi nào nên dùng cấu trúc nào?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Với đồ thị có V = 1,000,000 đỉnh và E = 2,000,000 cạnh, nếu chọn biểu diễn bằng Ma trận kề kiểu boolean trong Java thì hệ thống sẽ tiêu tốn bao nhiêu RAM và có nguy cơ ném ngoại lệ OutOfMemoryError không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. So sánh về không gian bộ nhớ (Space Complexity):
- Ma trận kề (Adjacency Matrix): Sử dụng mảng 2 chiều kích thước V x V. Không gian bộ nhớ luôn là O(V^2), bất kể số lượng cạnh E.
- Danh sách kề (Adjacency List): Sử dụng một mảng gồm V danh sách liên kết (hoặc ArrayList). Không gian bộ nhớ là O(V + E) cho đồ thị có hướng và O(V + 2E) cho đồ thị vô hướng.

2. Chi phí kiểm tra sự tồn tại của cạnh (u, v):
- Ma trận kề: O(1) bằng cách truy xuất trực tiếp ô `matrix[u][v]`.
- Danh sách kề: O(deg(u)) vì phải duyệt tuần tự qua danh sách các đỉnh kề của đỉnh u (trong trường hợp xấu nhất là O(V)).

3. Tiêu chí lựa chọn ứng dụng thực tế:
- Dùng Ma trận kề khi: Đồ thị dày (Dense Graph, E xấp xỉ V^2), hoặc khi kích thước số đỉnh V nhỏ và cần kiểm tra cạnh nhanh O(1).
- Dùng Danh sách kề khi: Đồ thị thưa (Sparse Graph, E << V^2, đa số đồ thị thực tế như mạng xã hội, bản đồ giao thông) để tiết kiệm tối đa bộ nhớ và duyệt nhanh các đỉnh kề trong O(deg(u)).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **So sánh không gian bộ nhớ** | **3.5đ** | Nêu chính xác O(V^2) của Ma trận kề vs O(V + E) của Danh sách kề. |
| **So sánh kiểm tra cạnh (u, v)** | **3.5đ** | Chỉ rõ O(1) của Ma trận kề so với O(deg(u)) của Danh sách kề. |
| **Phân loại Dense vs Sparse Graph** | **3.0đ** | Đưa ra tiêu chí lựa chọn chính xác dựa trên mật độ cạnh của đồ thị. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 10. [CSD_PQ10] Nguyên lý Đổi chỗ và Độ phức tạp Thời gian của Thuật toán Bubble Sort

- **Mã định danh (Question Code):** `CSD_PQ10`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO6** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 41 — *Bubble Sort*
- **Mục tiêu đánh giá:** Đánh giá sự hiểu biết về cơ chế hoạt động, tính ổn định và độ phức tạp của thuật toán sắp xếp nổi bọt.

#### ❓ Câu hỏi vấn đáp:
> "Trình bày nguyên lý vận hành của thuật toán Sắp xếp nổi bọt (Bubble Sort). Phân tích độ phức tạp thời gian trong trường hợp tốt nhất (Best-case) và trường hợp xấu nhất (Worst-case). Thuật toán này có tính ổn định (Stable) không, vì sao?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Làm thế nào để tối ưu hóa thuật toán Bubble Sort bằng cách sử dụng một cờ hiệu (boolean swapped) để thuật toán nhận diện được mảng đã sắp xếp sẵn và kết thúc sớm ngay ở lượt lặp đầu tiên với thời gian O(n)?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Nguyên lý vận hành của Bubble Sort:
Thuật toán duyệt qua mảng nhiều lượt. Ở mỗi lượt, thuật toán so sánh từng cặp phần tử liền kề `a[j]` và `a[j+1]`. Nếu chúng đứng sai thứ tự (ví dụ `a[j] > a[j+1]`), thuật toán sẽ đổi chỗ (swap) chúng. Sau mỗi lượt lặp, phần tử lớn nhất trong đoạn chưa sắp xếp sẽ 'nổi bọt' về đúng vị trí cuối cùng của mảng.

2. Phân tích độ phức tạp thời gian:
- Trường hợp xấu nhất (Worst-case): Khi mảng bị sắp xếp ngược thứ tự hoàn toàn. Số phép so sánh và đổi chỗ là n(n-1)/2, dẫn tới độ phức tạp O(n^2).
- Trường hợp tốt nhất (Best-case): Khi mảng đã có thứ tự sẵn. Nếu thuật toán có cờ hiệu `swapped` để dừng sớm khi không còn phép hoán vị nào trong một lượt, độ phức tạp đạt O(n). Nếu không có cờ hiệu tối ưu, độ phức tạp vẫn là O(n^2).

3. Tính ổn định (Stability):
Bubble Sort là một thuật toán sắp xếp ổn định (Stable). Khi gặp hai phần tử có giá trị bằng nhau (`a[j] == a[j+1]`), điều kiện đổi chỗ không được kích hoạt (`>` chứ không phải `>=`), do đó thứ tự tương đối ban đầu giữa các phần tử bằng nhau được bảo toàn nguyên vẹn.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Trình bày cơ chế nổi bọt** | **3.5đ** | Mô tả chính xác thao tác so sánh cặp phần tử liền kề và đưa phần tử lớn nhất về cuối. |
| **Phân tích Big-O Best/Worst case** | **3.5đ** | Phân tích O(n^2) trường hợp ngược và O(n) trường hợp tốt nhất kèm cờ hiệu swapped. |
| **Đánh giá tính ổn định** | **3.0đ** | Khẳng định thuật toán Stable và giải thích rõ tại sao không đổi chỗ khi hai khóa bằng nhau. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 11. [CSD_PQ11] Khái niệm Bảng băm, Hàm băm và Nguyên nhân Gây ra Hiện tượng Xung đột

- **Mã định danh (Question Code):** `CSD_PQ11`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 49 — *Hashing Concepts*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức cơ bản về bảng băm, hàm băm và bản chất toán học của hiện tượng xung đột.

#### ❓ Câu hỏi vấn đáp:
> "Bảng băm (Hash Table) và Hàm băm (Hash Function) là gì? Hiện tượng xung đột (Collision) trong bảng băm xảy ra khi nào và tại sao hiện tượng này về mặt toán học là không thể tránh khỏi?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nguyên lý toán học Dirichlet (Pigeonhole Principle - Nguyên lý chuồng bồ câu) giải thích như thế nào về tính tất yếu không thể tránh khỏi của hiện tượng xung đột (Collision) khi không gian khóa lớn hơn kích thước bảng băm?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Bảng băm và Hàm băm:
- Bảng băm (Hash Table): Cấu trúc dữ liệu liên kết (associative array) lưu trữ các cặp khóa - giá trị (Key - Value), cho phép các thao tác tìm kiếm, chèn, xóa đạt thời gian trung bình O(1).
- Hàm băm (Hash Function): Hàm toán học `h(k)` chuyển đổi một khóa k bất kỳ (chuỗi, đối tượng, số lớn) thành một số nguyên nằm trong phạm vi chỉ số của mảng băm `[0 .. M-1]`.

2. Hiện tượng xung đột (Collision):
- Xung đột xảy ra khi hai khóa khác nhau k1 != k2 được hàm băm ánh xạ tới cùng một vị trí chỉ số trong bảng băm: `h(k1) == h(k2)`.

3. Lý do toán học không thể tránh khỏi xung đột:
- Dựa trên Nguyên lý lồng chim bồ câu (Pigeonhole Principle): Không gian của tập các khóa có thể có (Domain of Keys) là vô hạn hoặc rất lớn (ví dụ chuỗi ký tự bất kỳ), trong khi kích thước bảng băm M là hữu hạn và thường nhỏ hơn rất nhiều.
- Khi số lượng khóa cần lưu trữ n > M, chắc chắn phải có ít nhất 2 khóa rơi vào cùng một ô lưu trữ. Do đó, mọi bảng băm thực tế đều bắt buộc phải trang bị chiến lược xử lý xung đột (Separate Chaining hoặc Open Addressing).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa bảng băm và hàm băm** | **3.0đ** | Nêu đúng khái niệm Key-Value, vai trò hàm băm và thời gian trung bình O(1). |
| **Đặc tả hiện tượng xung đột** | **3.0đ** | Giải thích chính xác biểu thức h(k1) == h(k2) với k1 != k2. |
| **Giải thích bằng Pigeonhole Principle** | **4.0đ** | Chứng minh bằng toán học lý do không gian khóa lớn hơn kích thước bảng băm dẫn tới xung đột tất yếu. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 12. [CSD_PQ12] Phân biệt Nén Dữ liệu Không Tổn hao và Nén Có Tổn hao

- **Mã định danh (Question Code):** `CSD_PQ12`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO8** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 55 — *Text Compression Concepts*
- **Mục tiêu đánh giá:** Đánh giá khả năng hiểu nguyên lý của 2 trường phái nén dữ liệu và phạm vi ứng dụng trong phần mềm.

#### ❓ Câu hỏi vấn đáp:
> "Hãy phân biệt Nén không tổn hao (Lossless Compression) và Nén có tổn hao (Lossy Compression) về khả năng phục hồi dữ liệu gốc. Với các tệp mã nguồn phần mềm (.java, .cs) và tệp văn bản tài liệu, ta bắt buộc phải sử dụng phương pháp nào, vì sao?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Theo Định lý Shannon về Mã hóa nguồn (Shannon's Source Coding Theorem), giới hạn lý thuyết entropy nào chi phối việc ta không thể nén một tệp dữ liệu không tổn hao xuống kích thước nhỏ tùy ý?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Phân biệt hai phương pháp nén dữ liệu:
- Nén không tổn hao (Lossless Compression):
  + Nguyên lý: Loại bỏ các mẫu dư thừa trong dữ liệu (redundancy) dựa trên thống kê hoặc từ điển.
  + Phục hồi: Quá trình giải nén cam kết phục hồi lại chính xác 100% từng bit dữ liệu gốc mà không có bất kỳ sai lệch nào.
  + Các thuật toán tiêu biểu: Huffman Coding, LZW, Run-Length Encoding, Deflate (trong tệp .zip, .png).
- Nén có tổn hao (Lossy Compression):
  + Nguyên lý: Chấp nhận loại bỏ một phần thông tin mà giác quan con người khó nhận biết (âm thanh tần số cao, chi tiết màu tinh vi).
  + Phục hồi: Dữ liệu sau giải nén chỉ là xấp xỉ dữ liệu gốc, không thể khôi phục lại dữ liệu nguyên bản hoàn hảo. Đổi lại, tỷ lệ nén cao hơn rất nhiều.
  + Các định dạng tiêu biểu: JPEG (ảnh), MP3 (âm thanh), MP4 (video).

2. Yêu cầu với tệp mã nguồn phần mềm (.java, .cs) và tài liệu văn bản:
- Bắt buộc 100% phải sử dụng Nén không tổn hao (Lossless).
- Lý do: Trong mã nguồn chương trình và văn bản, mỗi ký tự, dấu chấm, dấu phẩy hay từ khóa đều mang ngữ nghĩa cú pháp tuyệt đối. Chỉ cần sai lệch hoặc mất đi một ký tự duy nhất (ví dụ mất dấu chấm phẩy hoặc đổi tên biến), chương trình sẽ bị lỗi biên dịch (Compilation Error) hoặc chạy sai logic nghiệp vụ.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân biệt Lossless vs Lossy** | **4.0đ** | Nêu rõ nguyên lý và khả năng khôi phục nguyên vẹn 100% của Lossless so với xấp xỉ của Lossy. |
| **Thuật toán và định dạng ví dụ** | **2.5đ** | Liệt kê chính xác ví dụ minh họa cho từng trường phái (ZIP/PNG vs JPEG/MP3). |
| **Lập luận cho tệp mã nguồn** | **3.5đ** | Chứng minh tính bắt buộc của Lossless trong mã nguồn do yêu cầu bảo toàn cú pháp tuyệt đối. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 13. [CSD_PQ13] Cài đặt Thuật toán Đảo ngược Danh sách Liên kết Đơn tại chỗ

- **Mã định danh (Question Code):** `CSD_PQ13`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO1** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 1 — *Linked List Manipulation*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng thao tác con trỏ và cài đặt thuật toán đảo ngược danh sách liên kết đơn tại chỗ.

#### ❓ Câu hỏi vấn đáp:
> "Hãy trình bày thuật toán và viết đoạn mã Java đảo ngược một Danh sách liên kết đơn (Reverse a Singly Linked List) tại chỗ (in-place) sử dụng 3 con trỏ mà không cấp phát thêm danh sách mới. Phân tích độ phức tạp thời gian và không gian của giải thuật."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu trong quá trình đảo ngược Danh sách liên kết đơn bằng 3 con trỏ, con trỏ tạm `next` không được lưu trước khi thực hiện phép gán `curr.next = prev`, điều gì sẽ xảy ra với toàn bộ các phần tử còn lại của danh sách?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Thuật toán sử dụng 3 con trỏ: `prev` (theo dõi nút đứng trước, khởi tạo null), `curr` (nút hiện tại đang xét, khởi tạo head), và `next` (lưu tạm thời nút tiếp theo để không bị mất liên kết).

Đoạn mã cài đặt Java:
```java
public class ListNode {
    int val;
    ListNode next;
    ListNode(int val) { this.val = val; }
}

public ListNode reverseList(ListNode head) {
    ListNode prev = null;
    ListNode curr = head;
    while (curr != null) {
        ListNode nextTemp = curr.next; // 1. Lưu lại liên kết tiếp theo
        curr.next = prev;              // 2. Đảo ngược chiều con trỏ
        prev = curr;                  // 3. Tiến con trỏ prev lên
        curr = nextTemp;              // 4. Tiến con trỏ curr lên
    }
    return prev; // prev trở thành head mới của danh sách đã đảo
}
```

Phân tích độ phức tạp:
- Thời gian (Time Complexity): O(n) do mỗi nút trong danh sách liên kết chỉ được duyệt qua đúng một lần.
- Không gian (Space Complexity): O(1) vì thuật toán thao tác đảo con trỏ trực tiếp tại chỗ (in-place), chỉ sử dụng vài biến con trỏ phụ trợ cố định.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thuật 3 con trỏ** | **3.0đ** | Mô tả chuẩn xác vai trò của 3 con trỏ prev, curr, next và quy trình 4 bước đảo liên kết. |
| **Cài đặt mã nguồn Java** | **4.0đ** | Mã nguồn Java chính xác, xử lý đúng vòng lặp while, cập nhật con trỏ không rò rỉ và trả về prev. |
| **Phân tích Big-O** | **3.0đ** | Chỉ rõ O(n) thời gian và O(1) không gian bộ nhớ phụ. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 14. [CSD_PQ14] Ứng dụng Stack trong Bài toán Kiểm tra Dãy Ngoặc Hợp lệ

- **Mã định danh (Question Code):** `CSD_PQ14`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 5 — *Stack Applications*
- **Mục tiêu đánh giá:** Đánh giá khả năng áp dụng cấu trúc dữ liệu Stack để giải bài toán kiểm tra cú pháp đóng mở ngoặc.

#### ❓ Câu hỏi vấn đáp:
> "Hãy trình bày thuật toán và viết hàm Java sử dụng Stack để kiểm tra một chuỗi chỉ chứa các ký tự ngoặc '()', '[]', '{}' có phải là dãy ngoặc hợp lệ (Valid Parentheses) hay không. Phân tích các trường hợp biên (Edge Cases)."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Thuật toán kiểm tra dãy ngoặc hợp lệ bằng Stack sẽ phản ứng như thế nào khi chuỗi đầu vào chỉ toàn dấu đóng ngoặc như `")]}"` hoặc khi duyệt xong toàn bộ chuỗi mà Stack vẫn chưa rỗng?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Nguyên lý thuật toán:
Duyệt từng ký tự trong chuỗi:
1. Nếu gặp ngoặc mở ('(', '[', '{'), đẩy (push) vào Stack.
2. Nếu gặp ngoặc đóng (')', ']', '}'):
   - Nếu Stack rỗng: Trả về `false` ngay (thừa ngoặc đóng).
   - Lấy phần tử đỉnh Stack (pop) và so sánh: Nếu không khớp loại ngoặc tương ứng, trả về `false`.
3. Sau khi duyệt hết chuỗi: Nếu Stack rỗng thì hợp lệ (`true`), ngược lại còn ngoặc mở chưa đóng (`false`).

Cài đặt Java:
```java
import java.util.ArrayDeque;
import java.util.Deque;

public boolean isValidParentheses(String s) {
    if (s == null || s.length() % 2 != 0) return false;
    Deque<Character> stack = new ArrayDeque<>();
    
    for (char c : s.toCharArray()) {
        if (c == '(' || c == '[' || c == '{') {
            stack.push(c);
        } else {
            if (stack.isEmpty()) return false;
            char top = stack.pop();
            if (c == ')' && top != '(') return false;
            if (c == ']' && top != '[') return false;
            if (c == '}' && top != '{') return false;
        }
    }
    return stack.isEmpty();
}
```

Các ca biên (Edge Cases):
- Chuỗi rỗng: Trả về `true`.
- Chuỗi có độ dài lẻ: Trả về `false` ngay lập tức.
- Chuỗi bắt đầu bằng ngoặc đóng: Bị chặn bởi kiểm tra `stack.isEmpty()`.
- Chuỗi chỉ chứa toàn ngoặc mở: Bị chặn bởi kiểm tra cuối `stack.isEmpty()`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Thiết kế thuật toán với Stack** | **3.0đ** | Trình bày đủ logic push khi gặp ngoặc mở, pop và đối chiếu khi gặp ngoặc đóng. |
| **Cài đặt mã Java hoàn chỉnh** | **4.0đ** | Viết đúng cú pháp Java, sử dụng Deque/Stack chuẩn, bắt đủ các điều kiện so khớp. |
| **Xử lý các ca biên (Edge Cases)** | **3.0đ** | Phân tích đầy đủ chuỗi rỗng, độ dài lẻ, bắt đầu bằng ngoặc đóng, dư ngoặc mở. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 15. [CSD_PQ15] Cài đặt Hàng đợi Vòng (Circular Queue) bằng Mảng Cố định với Modulo

- **Mã định danh (Question Code):** `CSD_PQ15`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 7 — *Circular Queue Implementation*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng cài đặt mảng vòng để tối ưu hóa không gian và khắc phục hiện tượng tràn ảo.

#### ❓ Câu hỏi vấn đáp:
> "Hiện tượng 'tràn ảo' (False Overflow) khi cài đặt Queue bằng mảng tuyến tính là gì? Hãy trình bày giải pháp Hàng đợi vòng (Circular Queue) sử dụng toán tử chia lấy dư (Modulo) và viết phương thức `enqueue(int x)` cùng `dequeue()` bằng Java."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Làm thế nào để phân biệt trạng thái 'Hàng đợi rỗng' (Empty) và 'Hàng đợi đầy' (Full) trong Hàng đợi vòng (Circular Queue) khi cả hai trường hợp đều có thể dẫn tới điều kiện toán học `front == rear`?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Hiện tượng Tràn ảo (False Overflow):
Trong mảng tuyến tính, sau nhiều thao tác enqueue và dequeue, con trỏ `front` và `rear` đều dịch chuyển dần về cuối mảng. Khi `rear == capacity - 1`, mảng báo đầy và không thể chèn thêm, mặc dù ở phần đầu mảng (trước `front`) còn nhiều ô trống đã được dequeue.

2. Giải pháp Circular Queue với toán tử Modulo:
Cho phép con trỏ quay vòng về vị trí chỉ số 0 khi chạm cuối mảng bằng công thức: `index = (index + 1) % capacity`.

Cài đặt Java:
```java
public class CircularQueue {
    private int[] data;
    private int front;
    private int rear;
    private int size;
    private int capacity;

    public CircularQueue(int k) {
        this.capacity = k;
        this.data = new int[k];
        this.front = 0;
        this.rear = -1;
        this.size = 0;
    }

    public boolean enqueue(int value) {
        if (isFull()) return false;
        rear = (rear + 1) % capacity;
        data[rear] = value;
        size++;
        return true;
    }

    public int dequeue() {
        if (isEmpty()) throw new IllegalStateException("Queue is empty");
        int val = data[front];
        front = (front + 1) % capacity;
        size--;
        return val;
    }

    public boolean isEmpty() { return size == 0; }
    public boolean isFull() { return size == capacity; }
}
```
Mọi thao tác enqueue và dequeue đều đạt độ phức tạp O(1) và tái sử dụng 100% dung lượng mảng.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích hiện tượng tràn ảo** | **3.0đ** | Nêu rõ nguyên nhân con trỏ rear chạm cuối mảng khi đầu mảng còn trống. |
| **Công thức toán học Modulo** | **3.0đ** | Áp dụng chính xác công thức (index + 1) % capacity để quay vòng chỉ số. |
| **Cài đặt mã Java enqueue/dequeue** | **4.0đ** | Cài đặt đúng 100% lớp CircularQueue, quản lý front, rear, size và kiểm tra đầy/rỗng. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 16. [CSD_PQ16] Cơ chế Call Stack và Phân tích Tràn Bộ nhớ trong Đệ quy Tính Fibonacci

- **Mã định danh (Question Code):** `CSD_PQ16`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO3** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 12 — *Call Stack & Recursion Tree*
- **Mục tiêu đánh giá:** Đánh giá khả năng phân tích cây đệ quy, cơ chế cấp phát stack frame và nhận diện nguyên nhân lãng phí tính toán.

#### ❓ Câu hỏi vấn đáp:
> "Vẽ sơ đồ cây đệ quy khi tính `fib(4)` với định nghĩa đệ quy `fib(n) = fib(n-1) + fib(n-2)`. Phân tích vì sao giải thuật này có độ phức tạp thời gian cấp số nhân O(2^n) và giải thích tại sao n lớn sẽ gây tràn Call Stack."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao kỹ thuật Ghi nhớ (Top-down Memoization) có thể biến đổi độ phức tạp thời gian tính số Fibonacci từ cấp số nhân O(2^n) xuống tuyến tính O(n) mà không làm tăng độ sâu tối đa của Call Stack?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Sơ đồ Cây đệ quy khi tính `fib(4)`:
```
                    fib(4)
                  /        \
             fib(3)          fib(2)
            /      \        /      \
        fib(2)    fib(1)  fib(1)   fib(0)
        /    \
     fib(1) fib(0)
```

1. Nguyên nhân độ phức tạp thời gian O(2^n):
Cây đệ quy là một cây nhị phân có chiều cao tối đa n. Số lượng nút ở mỗi tầng i tăng gấp đôi (2^i). Tổng số phép gọi hàm xấp xỉ tỉ lệ vàng phi^n ~ O(2^n).
Quan sát trên cây, bài toán con `fib(2)` bị tính toán lặp lại 2 lần độc lập, `fib(1)` bị gọi 3 lần, gây ra sự dư thừa khổng lồ (Overlapping Subproblems).

2. Phân tích tràn Call Stack:
- Độ phức tạp không gian (Space Complexity) phụ thuộc vào chiều sâu tối đa của ngăn xếp lời gọi hàm tại một thời điểm, tương ứng chiều cao cây đệ quy là O(n).
- Khi gọi `fib(50000)`: Mặc dù thời gian chạy là bất khả thi, ngăn xếp Call Stack của máy ảo JVM (mặc định chỉ 1MB - 2MB) sẽ phải chứa hàng chục nghìn stack frames xếp chồng lên nhau. Khi vượt quá ngưỡng dung lượng thread stack, JVM sẽ ngay lập tức ném ra ngoại lệ `StackOverflowError`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Sơ đồ cây đệ quy fib(4)** | **3.0đ** | Vẽ đúng toàn bộ các nhánh rẽ và các lời gọi hàm từ fib(4) đến base cases. |
| **Chứng minh thời gian O(2^n)** | **4.0đ** | Chỉ ra hiện tượng tính lặp lại bài toán con (overlapping) và số nút tăng lũy thừa 2^n. |
| **Phân tích lỗi StackOverflow** | **3.0đ** | Giải thích mối quan hệ giữa độ sâu cây đệ quy O(n) và giới hạn bộ nhớ Call Stack của JVM. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 17. [CSD_PQ17] Cài đặt Thuật toán Duyệt Cây Pre-order, In-order và Post-order bằng Java

- **Mã định danh (Question Code):** `CSD_PQ17`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 16 — *Tree Traversals*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng cài đặt 3 thuật toán duyệt cây theo thứ tự chiều sâu bằng đệ quy trong Java.

#### ❓ Câu hỏi vấn đáp:
> "Hãy viết các phương thức Java cài đặt 3 phép duyệt cây nhị phân bằng đệ quy: Tiền thứ tự (Pre-order), Trung thứ tự (In-order) và Hậu thứ tự (Post-order). Chỉ ra một trường hợp ứng dụng thực tế cho phép duyệt Post-order."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao phép duyệt Hậu thứ tự (Post-order Traversal) lại là lựa chọn bắt buộc khi tính toán dung lượng thư mục trên hệ thống tệp tin (File System) hoặc khi giải phóng bộ nhớ của một cây nhị phân trong C/C++?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Định nghĩa cấu trúc nút:
```java
public class TreeNode {
    int val;
    TreeNode left, right;
    TreeNode(int val) { this.val = val; }
}
```

1. Cài đặt 3 phương thức duyệt bằng Java:
```java
// Pre-order: Node -> Left -> Right
public void preOrder(TreeNode root) {
    if (root == null) return;
    System.out.print(root.val + " ");
    preOrder(root.left);
    preOrder(root.right);
}

// In-order: Left -> Node -> Right
public void inOrder(TreeNode root) {
    if (root == null) return;
    inOrder(root.left);
    System.out.print(root.val + " ");
    inOrder(root.right);
}

// Post-order: Left -> Right -> Node
public void postOrder(TreeNode root) {
    if (root == null) return;
    postOrder(root.left);
    postOrder(root.right);
    System.out.print(root.val + " ");
}
```

2. Ứng dụng thực tế của Post-order Traversal:
- Tính dung lượng thư mục trong hệ thống tệp tin (File System Size Calculation): Phải tính tổng dung lượng toàn bộ các thư mục con và tệp con trước khi có thể tính dung lượng của thư mục cha.
- Xóa giải phóng toàn bộ cây trong các ngôn ngữ không có Garbage Collection (như C++): Cần xóa nút con trái và nút con phải trước khi giải phóng nút cha để tránh mất liên kết con trỏ rò rỉ bộ nhớ.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Cài đặt Pre-order và In-order** | **3.5đ** | Viết đúng thứ tự thăm nút và đệ quy cho Pre-order (NLR) và In-order (LNR). |
| **Cài đặt Post-order** | **3.5đ** | Viết đúng thứ tự thăm nút và đệ quy cho Post-order (LRN). |
| **Ứng dụng thực tế của Post-order** | **3.0đ** | Nêu và giải thích xác đáng ứng dụng tính kích thước thư mục hoặc giải phóng bộ nhớ. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 18. [CSD_PQ18] Quy trình Xóa Nút có Hai Con trên Cây BST bằng In-order Successor

- **Mã định danh (Question Code):** `CSD_PQ18`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 20 — *BST Deletion*
- **Mục tiêu đánh giá:** Đánh giá mức độ thành thạo giải thuật xóa nút trên cây tìm kiếm nhị phân khi nút có 2 con.

#### ❓ Câu hỏi vấn đáp:
> "Trình bày giải thuật xóa một nút z có đầy đủ 2 nút con trên Cây tìm kiếm nhị phân (BST). Nút thế thân In-order Successor là gì, tìm nó ở đâu và tại sao nút thế thân này đảm bảo không bao giờ có 2 con?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu nút thế thân In-order Successor có một cây con bên phải (Right Subtree), ta phải thực hiện cập nhật liên kết của nút cha của Successor như thế nào khi chuyển Successor lên thay thế nút cần xóa?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Quy trình xóa nút z có 2 nút con trên BST:
- Bước 1: Tìm nút thế thân y là In-order Successor của z (hoặc In-order Predecessor).
- Bước 2: Nút In-order Successor là nút có khóa nhỏ nhất nằm trong cây con bên phải của z (nút nằm tận cùng bên trái của cây con phải: đi sang phải 1 bước, sau đó đi sang trái liên tục cho đến khi gặp nút không có con trái).
- Bước 3: Sao chép giá trị khóa của y sang nút z: `z.key = y.key`.
- Bước 4: Xóa nút y khỏi cây con bên phải bằng phương thức xóa nút thông thường.

2. Cài đặt mã Java:
```java
public TreeNode deleteNode(TreeNode root, int key) {
    if (root == null) return null;
    if (key < root.val) {
        root.left = deleteNode(root.left, key);
    } else if (key > root.val) {
        root.right = deleteNode(root.right, key);
    } else {
        // Nút có 0 hoặc 1 con
        if (root.left == null) return root.right;
        if (root.right == null) return root.left;
        
        // Nút có 2 con: tìm In-order Successor (nhỏ nhất bên phải)
        TreeNode successor = findMin(root.right);
        root.val = successor.val;
        root.right = deleteNode(root.right, successor.val);
    }
    return root;
}

private TreeNode findMin(TreeNode node) {
    while (node.left != null) node = node.left;
    return node;
}
```

3. Vì sao In-order Successor không bao giờ có 2 con:
Theo định nghĩa, successor là nút nằm tận cùng bên trái của cây con phải, tức là nó chắc chắn không có con bên trái (`successor.left == null`). Do đó, nút y chỉ có thể có tối đa 1 con (con phải) hoặc là nút lá (0 con), việc xóa y quy về trường hợp cơ bản đơn giản.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Mô tả các bước xóa nút 2 con** | **3.0đ** | Nêu rõ 4 bước tìm thế thân, sao chép khóa và xóa nút thế thân. |
| **Đặc tả vị trí In-order Successor** | **3.5đ** | Chỉ rõ cách tìm phần tử nhỏ nhất nhánh con phải và viết mã cài đặt. |
| **Chứng minh Successor có tối đa 1 con** | **3.5đ** | Lập luận chính xác tính chất node.left == null của nút tận cùng bên trái. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 19. [CSD_PQ19] Thao tác Chèn Phần tử vào Min-Heap và Quy trình Vun Đống Ngược

- **Mã định danh (Question Code):** `CSD_PQ19`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 24 — *Binary Heap Operations*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng cài đặt biểu diễn Heap bằng mảng và thao tác Up-Heap (Sift-Up).

#### ❓ Câu hỏi vấn đáp:
> "Khi biểu diễn một Binary Min-Heap bằng mảng 0-indexed, công thức xác định chỉ số nút cha và nút con là gì? Hãy trình bày các bước chèn một phần tử mới vào Min-Heap và viết mã Java cho hàm `siftUp(int index)`."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi thực hiện chèn liên tiếp một dãy n phần tử đã có thứ tự giảm dần vào một Min-Heap ban đầu rỗng, mỗi thao tác chèn sẽ có chi phí thời gian là bao nhiêu và tổng chi phí xây dựng heap theo cách này là gì?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Công thức quan hệ chỉ số trong mảng (0-indexed):
Đối với một nút tại chỉ số `i`:
- Nút cha (Parent): `parent(i) = (i - 1) / 2`
- Nút con trái (Left Child): `left(i) = 2 * i + 1`
- Nút con phải (Right Child): `right(i) = 2 * i + 2`

2. Các bước chèn phần tử mới vào Min-Heap:
- Bước 1: Thêm phần tử mới vào vị trí cuối cùng của mảng (tương ứng vị trí lá còn trống ngoài cùng bên trái ở tầng dưới cùng của cây).
- Bước 2: Tăng kích thước heap: `size++`.
- Bước 3: Thực hiện thao tác vun đống ngược lên (`siftUp` / `upHeap`): So sánh phần tử vừa chèn với nút cha của nó. Nếu giá trị nhỏ hơn nút cha, hoán vị nó với nút cha. Tiếp tục lặp lại quá trình này cho đến khi thỏa mãn tính chất Min-Heap (nút cha <= nút con) hoặc đã lên đến nút gốc (`index == 0`).

3. Cài đặt Java phương thức siftUp:
```java
private void siftUp(int i) {
    while (i > 0) {
        int parent = (i - 1) / 2;
        if (heap[i] < heap[parent]) {
            // Hoán vị với nút cha
            int temp = heap[i];
            heap[i] = heap[parent];
            heap[parent] = temp;
            i = parent; // Tiếp tục kiểm tra lên tầng trên
        } else {
            break; // Đã thỏa mãn tính chất Min-Heap
        }
    }
}
```
Độ phức tạp thời gian: O(log n) do chiều cao của cây nhị phân hoàn chỉnh là floor(log2 n).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Công thức chỉ số mảng** | **2.5đ** | Nêu đúng công thức Parent: (i-1)/2, Left: 2i+1, Right: 2i+2. |
| **Quy trình chèn phần tử** | **3.5đ** | Mô tả chuẩn xác thao tác chèn vào cuối mảng và gọi siftUp khôi phục heap property. |
| **Cài đặt mã Java siftUp và Big-O** | **4.0đ** | Viết đúng vòng lặp siftUp hoán vị và chỉ ra độ phức tạp O(log n). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 20. [CSD_PQ20] Cài đặt Thuật toán Duyệt Đồ thị Theo Chiều Rộng (BFS)

- **Mã định danh (Question Code):** `CSD_PQ20`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000564`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 30 — *Breadth-First Search (BFS)*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng cài đặt thuật toán BFS sử dụng hàng đợi và ứng dụng tìm đường đi ngắn nhất không trọng số.

#### ❓ Câu hỏi vấn đáp:
> "Hãy trình bày thuật toán và viết phương thức Java cài đặt phép duyệt đồ thị theo chiều rộng (BFS) xuất phát từ đỉnh nguồn s trên đồ thị vô hướng biểu diễn bằng danh sách kề. Phân tích độ phức tạp thời gian và không gian."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu lập trình viên quên đánh dấu mảng `visited[v] = true` ngay tại thời điểm đẩy đỉnh vào Queue mà chỉ đánh dấu khi lấy đỉnh ra khỏi Queue (Poll), điều gì sẽ xảy ra với hiệu năng và bộ nhớ của thuật toán BFS?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Nguyên lý thuật toán BFS:
Sử dụng hàng đợi FIFO Queue để quản lý các đỉnh chờ duyệt và một mảng boolean `visited` để đánh dấu các đỉnh đã thăm, ngăn ngừa lặp vô hạn do chu trình:
1. Đánh dấu đỉnh nguồn s là đã thăm: `visited[s] = true`, đẩy s vào Queue.
2. Khi Queue chưa rỗng: Lấy đỉnh u ra khỏi Queue (`dequeue`), thăm đỉnh u.
3. Duyệt qua tất cả các đỉnh kề v của u: Nếu v chưa thăm (`!visited[v]`), đánh dấu `visited[v] = true` và đẩy v vào Queue.

2. Cài đặt Java:
```java
import java.util.*;

public void bfs(int startVertex, List<List<Integer>> adj, int numVertices) {
    boolean[] visited = new boolean[numVertices];
    Queue<Integer> queue = new ArrayDeque<>();

    visited[startVertex] = true;
    queue.offer(startVertex);

    while (!queue.isEmpty()) {
        int u = queue.poll();
        System.out.print(u + " ");

        for (int v : adj.get(u)) {
            if (!visited[v]) {
                visited[v] = true;
                queue.offer(v);
            }
        }
    }
}
```

3. Phân tích độ phức tạp:
- Thời gian (Time Complexity): O(V + E) trên danh sách kề, do mỗi đỉnh được đưa vào Queue đúng một lần và mỗi cạnh được duyệt qua tối đa 2 lần (đồ thị vô hướng).
- Không gian (Space Complexity): O(V) cho mảng boolean `visited` và hàng đợi `queue`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Nguyên lý thuật toán BFS** | **3.0đ** | Nêu rõ vai trò của Queue FIFO và mảng đánh dấu visited chống lặp chu trình. |
| **Cài đặt mã Java hoàn chỉnh** | **4.0đ** | Mã nguồn Java chính xác, duyệt kề chuẩn, push/poll Queue đúng thời điểm. |
| **Phân tích Big-O thời gian và bộ nhớ** | **3.0đ** | Xác định chuẩn xác O(V + E) thời gian và O(V) không gian bộ nhớ. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 21. [CSD_PQ21] Cài đặt Thuật toán Duyệt Đồ thị Theo Chiều Sâu (DFS) Xác định Thành phần Liên thông

- **Mã định danh (Question Code):** `CSD_PQ21`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000564`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 30 — *Depth-First Search & Connected Components*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng cài đặt DFS bằng đệ quy và ứng dụng đếm số thành phần liên thông của đồ thị.

#### ❓ Câu hỏi vấn đáp:
> "Hãy viết mã Java sử dụng thuật toán Duyệt theo chiều sâu (DFS) đệ quy để đếm số lượng thành phần liên thông (Connected Components) của một đồ thị vô hướng có V đỉnh được biểu diễn bằng Danh sách kề."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trên một đồ thị có 100,000 đỉnh liên thông thành một đường thẳng duy nhất (skewed graph), thuật toán DFS đệ quy cài đặt trên Java với cấu hình mặc định có chạy thành công không và giải pháp khắc phục là gì?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Nguyên lý thuật toán:
Khởi tạo mảng `visited` kích thước V toàn bộ là `false`. Duyệt qua tất cả các đỉnh từ 0 đến V - 1:
- Nếu gặp đỉnh i chưa được thăm (`!visited[i]`), điều đó chứng tỏ ta phát hiện ra một thành phần liên thông mới -> Tăng biến đếm `componentCount++`.
- Kích hoạt hàm đệ quy `dfs(i)` để đi thăm và đánh dấu toàn bộ các đỉnh thuộc cùng thành phần liên thông này.

Cài đặt Java:
```java
import java.util.List;

public class GraphConnectedComponents {
    public int countComponents(int numVertices, List<List<Integer>> adj) {
        boolean[] visited = new boolean[numVertices];
        int count = 0;

        for (int i = 0; i < numVertices; i++) {
            if (!visited[i]) {
                dfs(i, adj, visited);
                count++;
            }
        }
        return count;
    }

    private void dfs(int u, List<List<Integer>> adj, boolean[] visited) {
        visited[u] = true;
        for (int v : adj.get(u)) {
            if (!visited[v]) {
                dfs(v, adj, visited);
            }
        }
    }
}
```

Độ phức tạp:
- Thời gian: O(V + E) vì toàn bộ các đỉnh và cạnh đều được duyệt qua đúng một lần.
- Không gian: O(V) cho mảng `visited` và độ sâu Call Stack của đệ quy trong trường hợp đồ thị suy biến dạng đường thẳng.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Thiết kế thuật toán đếm thành phần** | **3.0đ** | Trình bày đúng vòng lặp duyệt đỉnh ngoài kết hợp gọi DFS đệ quy. |
| **Cài đặt mã Java DFS** | **4.0đ** | Cài đặt hàm dfs đệ quy và hàm countComponents chính xác, xử lý đúng tham số. |
| **Phân tích độ phức tạp** | **3.0đ** | Chỉ rõ O(V + E) thời gian và O(V) độ sâu Call Stack. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 22. [CSD_PQ22] Thuật toán Phân hoạch Lomuto trong QuickSort và Cách Lựa chọn Pivot

- **Mã định danh (Question Code):** `CSD_PQ22`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO6** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 42 — *QuickSort Lomuto Partition*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng cài đặt phân hoạch mảng trong thuật toán QuickSort và hiểu cơ chế con trỏ chỉ số.

#### ❓ Câu hỏi vấn đáp:
> "Trình bày giải thuật phân hoạch Lomuto Partition trong QuickSort khi chọn phần tử cuối mảng làm Pivot. Viết hàm `partition(int[] arr, int low, int high)` bằng Java và giải thích vai trò của hai chỉ số i và j."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu mảng đầu vào chứa tất cả các phần tử hoàn toàn bằng nhau (ví dụ: `[5, 5, 5, 5, 5]`), thuật toán phân hoạch Lomuto Partition trong QuickSort sẽ phân chia mảng như thế nào và độ phức tạp thời gian khi đó là bao nhiêu?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Nguyên lý phân hoạch Lomuto:
- Chọn phần tử cuối cùng `pivot = arr[high]`.
- Dùng con trỏ `i` để duy trì biên giới của vùng các phần tử nhỏ hơn hoặc bằng pivot (khởi tạo `i = low - 1`).
- Dùng con trỏ `j` chạy từ `low` đến `high - 1`. Khi gặp `arr[j] <= pivot`, ta tăng `i++` và hoán vị `arr[i]` với `arr[j]`.
- Cuối cùng, hoán vị `arr[i + 1]` với `arr[high]` để đưa pivot về đúng vị trí phân cách chính giữa hai phân vùng.

2. Cài đặt Java:
```java
public int partitionLomuto(int[] arr, int low, int high) {
    int pivot = arr[high];
    int i = low - 1; // Chỉ số của phần tử nhỏ hơn pivot

    for (int j = low; j < high; j++) {
        if (arr[j] <= pivot) {
            i++;
            // Hoán vị arr[i] và arr[j]
            int temp = arr[i];
            arr[i] = arr[j];
            arr[j] = temp;
        }
    }
    // Đưa pivot về đúng vị trí (i + 1)
    int temp = arr[i + 1];
    arr[i + 1] = arr[high];
    arr[high] = temp;

    return i + 1; // Trả về chỉ số pivot
}
```

3. Vai trò của chỉ số i và j:
- `j`: Con trỏ duyệt mảng, khảo sát từng phần tử từ trái sang phải.
- `i`: Đánh dấu vị trí cuối cùng của đoạn mảng đã xác định nhỏ hơn hoặc bằng pivot. Vùng `[low .. i]` luôn <= pivot, vùng `[i+1 .. j-1]` luôn > pivot. Sau khi duyệt xong, vị trí `i + 1` chính là vị trí hợp lệ duy nhất của pivot.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Nguyên lý phân hoạch Lomuto** | **3.0đ** | Nêu rõ cách chọn pivot và phân chia mảng thành 2 vùng <= pivot và > pivot. |
| **Cài đặt mã Java partition** | **4.0đ** | Cài đặt chính xác vòng for, điều kiện so sánh, các phép hoán vị và return i + 1. |
| **Giải thích vai trò của 2 con trỏ i và j** | **3.0đ** | Mô tả chuẩn xác biên giới vùng nhỏ hơn của con trỏ i và con trỏ quét j. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 23. [CSD_PQ23] Cài đặt Chiến lược Giải quyết Xung đột Bảng băm bằng Separate Chaining

- **Mã định danh (Question Code):** `CSD_PQ23`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 49 — *Separate Chaining Collision Handling*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng cài đặt bảng băm sử dụng danh sách liên kết tại mỗi bucket để chống xung đột.

#### ❓ Câu hỏi vấn đáp:
> "Trình bày cấu trúc và viết một lớp Java đơn giản cài đặt Bảng băm (Hash Table) sử dụng phương pháp Separate Chaining hỗ trợ hai thao tác: `put(int key, String value)` và `get(int key)`. Giả sử kích thước bảng là M = 10 và hàm băm là `h(key) = Math.abs(key) % M`."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi một bucket trong Separate Chaining có quá nhiều phần tử do xung đột (ví dụ vượt quá 8 phần tử), cấu trúc dữ liệu nào được Java HashMap (từ Java 8) sử dụng để thay thế Linked List nhằm duy trì thời gian tra cứu O(log n)?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cấu trúc Separate Chaining:
Bảng băm gồm một mảng kích thước M ô (buckets). Mỗi bucket lưu trữ một danh sách liên kết chứa các cặp khóa - giá trị cùng băm ra chỉ số đó. Khi xảy ra xung đột, nút mới được chèn vào danh sách liên kết của bucket tương ứng.

2. Cài đặt Java:
```java
import java.util.LinkedList;

public class SeparateChainingHashTable {
    private static class Entry {
        int key;
        String value;
        Entry(int key, String value) { this.key = key; this.value = value; }
    }

    private final int M = 10;
    private LinkedList<Entry>[] table;

    @SuppressWarnings("unchecked")
    public SeparateChainingHashTable() {
        table = new LinkedList[M];
        for (int i = 0; i < M; i++) {
            table[i] = new LinkedList<>();
        }
    }

    private int hash(int key) {
        return Math.abs(key) % M;
    }

    public void put(int key, String value) {
        int idx = hash(key);
        for (Entry entry : table[idx]) {
            if (entry.key == key) {
                entry.value = value; // Cập nhật nếu đã tồn tại khóa
                return;
            }
        }
        table[idx].add(new Entry(key, value)); // Chèn mới vào bucket
    }

    public String get(int key) {
        int idx = hash(key);
        for (Entry entry : table[idx]) {
            if (entry.key == key) return entry.value;
        }
        return null; // Không tìm thấy
    }
}
```

Ưu điểm: Bảng không bao giờ bị tràn (kích thước n có thể lớn hơn M), thao tác xóa đơn giản, hiệu năng suy giảm tuyến tính theo hệ số tải alpha = n/M.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Kiến trúc cấu trúc dữ liệu** | **3.0đ** | Mô tả chính xác mảng các LinkedList và cấu trúc Entry(key, value). |
| **Cài đặt phương thức put()** | **3.5đ** | Băm khóa, duyệt tìm cập nhật nếu trùng, chèn mới nếu chưa có. |
| **Cài đặt phương thức get()** | **3.5đ** | Băm khóa, duyệt bucket tương ứng trả về giá trị hoặc null khi không thấy. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 24. [CSD_PQ24] Cài đặt Thuật toán Tìm kiếm Mẫu Chuỗi Đối chiếu Thô (Brute-Force Pattern Matching)

- **Mã định danh (Question Code):** `CSD_PQ24`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO8** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 53 — *String Pattern Matching*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng cài đặt thuật toán đối sánh chuỗi cơ bản và phân tích trường hợp xấu nhất.

#### ❓ Câu hỏi vấn đáp:
> "Hãy viết phương thức Java cài đặt thuật toán Tìm kiếm mẫu chuỗi theo phương pháp vét cạn (Brute-Force String Matching) trả về chỉ số xuất hiện đầu tiên của chuỗi mẫu P độ dài m trong chuỗi văn bản T độ dài n. Hãy đưa ra một ví dụ dữ liệu đầu vào khiến thuật toán suy biến đạt độ phức tạp O(n * m)."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Với chuỗi văn bản T = 'AAAAAAAAAAAAAAAAAB' và chuỗi mẫu P = 'AAAB', thuật toán Brute-Force Pattern Matching thực hiện bao nhiêu phép so sánh ký tự và vì sao con trỏ trên chuỗi văn bản T bị lùi lại nhiều lần?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cài đặt thuật toán Brute-Force String Matching bằng Java:
```java
public int bruteForceSearch(String text, String pattern) {
    int n = text.length();
    int m = pattern.length();

    for (int i = 0; i <= n - m; i++) {
        int j = 0;
        while (j < m && text.charAt(i + j) == pattern.charAt(j)) {
            j++;
        }
        if (j == m) {
            return i; // Tìm thấy mẫu tại vị trí i
        }
    }
    return -1; // Không tìm thấy mẫu trong văn bản
}
```

2. Ví dụ trường hợp suy biến độ phức tạp O(n * m):
- Chuỗi văn bản T: `"AAAAAAAAAAAAAAAAAB"` (độ dài n = 18 gồm toàn chữ 'A' và ký tự cuối là 'B').
- Chuỗi mẫu P: `"AAAB"` (độ dài m = 4).
- Phân tích: Tại mỗi vị trí i từ 0 đến n - m, thuật toán luôn so khớp thành công m - 1 ký tự 'A' đầu tiên, chỉ thất bại ở ký tự cuối cùng 'B'. Sau khi thất bại, con trỏ lại lùi về vị trí `i + 1` và so sánh lại từ đầu. Tổng số phép so sánh ký tự xấp xỉ `(n - m + 1) * m` ~ O(n * m).
- Đây chính là lý do giải thuật Knuth-Morris-Pratt (KMP) ra đời để khắc phục việc lùi con trỏ trên chuỗi văn bản T.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Cài đặt mã Java Brute-Force** | **4.0đ** | Viết đúng 2 vòng lặp lồng nhau, kiểm tra biên i <= n - m và trả về vị trí khớp hoặc -1. |
| **Xây dựng ví dụ trường hợp xấu nhất** | **3.0đ** | Đưa ra ví dụ chuỗi lặp như 'AAAAAB' và 'AAAB' gây so khớp hụt ở ký tự cuối. |
| **Chứng minh Big-O O(n*m)** | **3.0đ** | Giải thích cơ chế lùi con trỏ và số lượng phép so sánh lặp lại (n - m + 1) * m. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 25. [CSD_PQ25] Ứng dụng Định lý Thợ (Master Theorem) Phân tích Phương trình Truy hồi Đệ quy

- **Mã định danh (Question Code):** `CSD_PQ25`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO1** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 1 — *Master Theorem & Recurrence Relations*
- **Mục tiêu đánh giá:** Đánh giá khả năng áp dụng Định lý Thợ để giải phương trình truy hồi trong phân tích thuật toán chia để trị.

#### ❓ Câu hỏi vấn đáp:
> "Phát biểu 3 trường hợp của Định lý Thợ (Master Theorem) đối với phương trình truy hồi dạng T(n) = a*T(n/b) + f(n). Hãy áp dụng định lý để xác định độ phức tạp thời gian tiệm cận của: (1) Thuật toán MergeSort với T(n) = 2*T(n/2) + O(n), và (2) Thuật toán Tìm kiếm nhị phân với T(n) = T(n/2) + O(1)."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Phương trình truy hồi `T(n) = 2*T(n/2) + n*log(n)` có thể áp dụng trực tiếp 3 trường hợp chuẩn của Định lý Thợ (Master Theorem) được không, và nếu không thì dạng mở rộng của định lý cho ra độ phức tạp tiệm cận là bao nhiêu?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Phát biểu Định lý Thợ (Master Theorem):
Phương trình truy hồi có dạng: T(n) = a * T(n/b) + f(n) với a >= 1, b > 1.
Đặt c_crit = log_b(a). Ta so sánh tốc độ tăng trưởng của f(n) với n^(c_crit):
- Trường hợp 1: Nếu f(n) = O(n^c) với c < c_crit (chi phí chia/gộp bị lấn át bởi cây đệ quy), thì:
  T(n) = Theta(n^(log_b(a))).
- Trường hợp 2: Nếu f(n) = Theta(n^(c_crit) * log^k(n)) với k >= 0 (chi phí chia/gộp tương đương cây đệ quy), thì:
  T(n) = Theta(n^(log_b(a)) * log^(k+1)(n)).
- Trường hợp 3: Nếu f(n) = Omega(n^c) với c > c_crit và thỏa mãn điều kiện đều đặn a*f(n/b) <= d*f(n) với d < 1, thì:
  T(n) = Theta(f(n)).

2. Áp dụng giải hai bài toán cụ thể:
- Bài toán 1: MergeSort với T(n) = 2*T(n/2) + O(n)
  + Ta có: a = 2, b = 2, f(n) = O(n) = n^1.
  + Tính c_crit = log_b(a) = log_2(2) = 1.
  + So sánh: f(n) = Theta(n^1 * log^0(n)) tương ứng Trường hợp 2 với k = 0.
  + Kết luận: T(n) = Theta(n^1 * log^(0+1)(n)) = Theta(n log n).

- Bài toán 2: Tìm kiếm nhị phân với T(n) = T(n/2) + O(1)
  + Ta có: a = 1, b = 2, f(n) = O(1) = n^0.
  + Tính c_crit = log_2(1) = 0.
  + So sánh: f(n) = Theta(n^0 * log^0(n)) tương ứng Trường hợp 2 với k = 0.
  + Kết luận: T(n) = Theta(n^0 * log^1(n)) = Theta(log n).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phát biểu 3 trường hợp Master Theorem** | **4.0đ** | Nêu chính xác công thức dạng chuẩn và điều kiện so sánh f(n) với n^(log_b(a)). |
| **Phân tích giải thuật MergeSort** | **3.0đ** | Xác định đúng a=2, b=2, c_crit=1, rơi vào Case 2 và suy ra Theta(n log n). |
| **Phân tích Tìm kiếm nhị phân** | **3.0đ** | Xác định đúng a=1, b=2, c_crit=0, rơi vào Case 2 và suy ra Theta(log n). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 26. [CSD_PQ26] Thuật toán Floyd's Tortoise and Hare Phát hiện Chu trình trong Singly Linked List

- **Mã định danh (Question Code):** `CSD_PQ26`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO1** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 2 — *Floyd's Cycle-Finding Algorithm*
- **Mục tiêu đánh giá:** Kiểm tra năng lực phân tích thuật toán 2 con trỏ rùa và thỏ để phát hiện chu trình và tìm nút bắt đầu chu trình trong danh sách liên kết.

#### ❓ Câu hỏi vấn đáp:
> "Trình bày nguyên lý thuật toán Rùa và Thỏ của Floyd (Floyd's Tortoise and Hare) để phát hiện chu trình trong Danh sách liên kết đơn. Chứng minh về mặt toán học vì sao hai con trỏ chắc chắn sẽ gặp nhau nếu có chu trình và giải thích thuật toán tìm nút bắt đầu chu trình."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trong thuật toán Rùa và Thỏ của Floyd, sau khi hai con trỏ gặp nhau bên trong chu trình, tại sao nếu ta đặt lại con trỏ Chậm về nút đầu (head) và cho cả hai di chuyển cùng tốc độ 1 bước, điểm gặp nhau tiếp theo chính là điểm bắt đầu chu trình?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Nguyên lý thuật toán Floyd:
Sử dụng hai con trỏ cùng xuất phát từ `head`:
- Con trỏ Chậm (Slow/Tortoise): mỗi bước nhảy tiến 1 nút (`slow = slow.next`).
- Con trỏ Nhanh (Fast/Hare): mỗi bước nhảy tiến 2 nút (`fast = fast.next.next`).
Nếu danh sách không có chu trình, `fast` hoặc `fast.next` sẽ chạm tới `null` (kết thúc duyệt). Nếu danh sách có chu trình, `fast` sẽ lọt vào chu trình và liên tục đuổi theo `slow`.

2. Chứng minh toán học hai con trỏ chắc chắn gặp nhau:
- Giả sử chu trình có độ dài L nút. Khi `slow` vừa bước vào nút đầu tiên của chu trình, `fast` đã ở bên trong chu trình và cách `slow` một khoảng cách d bước (0 <= d < L) theo chiều kim đồng hồ.
- Ở mỗi lượt lặp tiếp theo: `slow` đi 1 bước, `fast` đi 2 bước -> khoảng cách giữa `fast` và `slow` bị thu hẹp đúng 1 nút mỗi lượt (hiệu vận tốc tương đối là 2 - 1 = 1 nút/bước).
- Vì khoảng cách d giảm dần: d, d-1, d-2, ..., 0 (modulo L), nên sau tối đa L bước, khoảng cách giữa chúng chắc chắn trở về 0, nghĩa là `fast == slow`. Không thể xảy ra hiện tượng 'nhảy vượt qua mặt' mà không gặp.

3. Thuật toán tìm nút bắt đầu chu trình (Cycle Start Node):
- Khi `slow` và `fast` gặp nhau tại nút giao điểm M:
  + Đặt lại con trỏ `slow = head`, giữ nguyên `fast` tại nút M.
  + Cho cả hai con trỏ cùng tiến với tốc độ bằng nhau: 1 bước/lượt (`slow = slow.next`, `fast = fast.next`).
  + Điểm mà chúng gặp nhau lần thứ hai chính là Nút bắt đầu của chu trình!
- Chứng minh: Gọi khoảng cách từ head tới nút đầu chu trình là a, từ nút đầu chu trình tới M là b, chu trình dài L. Khi gặp nhau: quãng đường fast đi gấp đôi slow: 2(a + b) = a + b + k*L => a + b = k*L => a = k*L - b. Khoảng cách từ M đi tiếp đến đầu chu trình đúng bằng a.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Nguyên lý hai con trỏ Tortoise & Hare** | **3.0đ** | Mô tả đúng vận tốc 1 bước vs 2 bước và điều kiện dừng. |
| **Chứng minh toán học chắc chắn gặp nhau** | **3.5đ** | Phân tích khoảng cách tương đối giảm 1 đơn vị mỗi bước (modulo L). |
| **Thuật toán tìm nút bắt đầu chu trình** | **3.5đ** | Trình bày thuật toán reset con trỏ về head và chứng minh công thức a = k*L - b. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 27. [CSD_PQ27] Phân tích Chi phí Priority Queue Cài đặt bằng Mảng vs Binary Heap

- **Mã định danh (Question Code):** `CSD_PQ27`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 8 — *Priority Queue Implementations*
- **Mục tiêu đánh giá:** Đánh giá năng lực so sánh các cấu trúc dữ liệu nền tảng khi triển khai Priority Queue và lý giải vì sao Heap là lựa chọn tối ưu.

#### ❓ Câu hỏi vấn đáp:
> "Hãy so sánh 3 phương pháp cài đặt Hàng đợi ưu tiên (Priority Queue): (1) Mảng chưa sắp xếp (Unsorted Array), (2) Mảng đã sắp xếp (Sorted Array), và (3) Đống nhị phân (Binary Heap) về độ phức tạp thời gian của hai thao tác: `insert()` và `removeMin()`. Vì sao Binary Heap là lựa chọn tối ưu cho hệ thống thực tế?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trong kịch bản hệ thống yêu cầu thao tác chèn `insert()` cực kỳ thường xuyên nhưng thao tác lấy phần tử ưu tiên `removeMin()` rất hiếm khi được gọi, cài đặt nào giữa Mảng chưa sắp xếp (Unsorted Array) và Binary Heap sẽ cho tổng thông lượng (throughput) tốt hơn?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Bảng so sánh độ phức tạp thời gian:

| Cấu trúc dữ liệu cài đặt | Thao tác `insert(x)` | Thao tác `removeMin()` | Thao tác `min()` (peek) |
|---|---|---|---|
| 1. Mảng chưa sắp xếp (Unsorted Array) | O(1) (chèn vào cuối) | O(n) (phải quét toàn mảng tìm min) | O(n) |
| 2. Mảng đã sắp xếp (Sorted Array) | O(n) (tìm vị trí và dịch chuyển mảng) | O(1) (lấy phần tử ở cuối/đầu mảng) | O(1) |
| 3. Đống nhị phân (Binary Heap) | O(log n) (chèn cuối + Sift-Up) | O(log n) (hoán vị với cuối + Sift-Down) | O(1) (đỉnh heap) |

Phân tích lý do Binary Heap là lựa chọn tối ưu cho hệ thống thực tế:
1. Tránh điểm nghẽn nghiêm trọng O(n):
- Cả mảng chưa sắp xếp và mảng đã sắp xếp đều có một thao tác bị nghẽn ở chi phí O(n). Trong kịch bản xử lý luồng dữ liệu lớn liên tục (như điều phối tiến trình CPU, thuật toán định tuyến mạng Dijkstra), chi phí O(n) cho mỗi phần tử sẽ khiến tổng thời gian xử lý n thao tác lên tới O(n^2), gây đóng băng hệ thống.
2. Cân bằng tối ưu O(log n):
- Binary Heap mang lại sự cân bằng hoàn hảo giữa ghi (`insert`) và đọc/xóa (`removeMin`) với chi phí O(log n). Với n = 1,000,000 phần tử, trong khi O(n) tốn 1,000,000 phép tính thì O(log n) chỉ tốn khoảng 20 phép so sánh.
3. Không gian bộ nhớ liên tục:
- Binary Heap được biểu diễn trực tiếp trên mảng một chiều phẳng (flat array) mà không cần con trỏ tham chiếu, tận dụng tối đa cache locality của CPU.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Bảng so sánh Big-O chính xác** | **4.0đ** | Điền chính xác 100% độ phức tạp của insert, removeMin, peek cho cả 3 cấu trúc. |
| **Phân tích điểm nghẽn O(n)** | **3.0đ** | Chỉ ra sự mất cân đối cực đoan của Unsorted Array và Sorted Array dẫn đến tổng O(n^2). |
| **Lập luận ưu thế của Binary Heap** | **3.0đ** | Chứng minh tính cân bằng O(log n) và hiệu quả bộ nhớ mảng phẳng. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 28. [CSD_PQ28] Kỹ thuật Khử Đệ quy Đuôi Tối ưu hóa Bộ nhớ Call Stack

- **Mã định danh (Question Code):** `CSD_PQ28`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO3** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 12 — *Tail Recursion Elimination*
- **Mục tiêu đánh giá:** Kiểm tra hiểu biết sâu về đệ quy đuôi, cơ chế tối ưu của compiler và kỹ thuật chuyển đổi sang vòng lặp O(1) bộ nhớ.

#### ❓ Câu hỏi vấn đáp:
> "Thế nào là Đệ quy đuôi (Tail Recursion)? Tại sao hàm đệ quy tính giai thừa thông thường `fact(n) = n * fact(n-1)` KHÔNG phải là đệ quy đuôi? Hãy viết lại hàm giai thừa theo dạng đệ quy đuôi chuẩn và chuyển đổi nó thành vòng lặp tối ưu hóa không gian từ O(n) về O(1)."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trình biên dịch JVM của ngôn ngữ Java hiện tại có tự động tối ưu hóa đệ quy đuôi (Tail Call Optimization - TCO) để tái sử dụng Stack Frame như các trình biên dịch C/C++ hay ngôn ngữ hàm (Functional) không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Định nghĩa Đệ quy đuôi (Tail Recursion):
Một hàm được gọi là đệ quy đuôi nếu lời gọi đệ quy chính nó là thao tác cuối cùng được thực thi trước khi hàm trả về kết quả. Kết quả của lời gọi đệ quy được trả thẳng về cho caller mà không cần thực hiện thêm bất kỳ phép tính toán nào khác.

2. Vì sao `fact(n) = n * fact(n-1)` không phải đệ quy đuôi:
Trong biểu thức `return n * fact(n-1);`, sau khi lời gọi đệ quy `fact(n-1)` hoàn tất và trả về giá trị, hàm hiện tại vẫn còn phải thực hiện một phép nhân `n * (kết quả)` trước khi kết thúc. Do đó, hệ thống bắt buộc phải giữ lại Stack Frame của `fact(n)` trên Call Stack để ghi nhớ giá trị của `n`, dẫn tới độ phức tạp không gian O(n).

3. Viết lại hàm giai thừa theo dạng Đệ quy đuôi (sử dụng biến tích lũy Accumulator):
```java
public long factTail(int n, long acc) {
    if (n <= 1) return acc;
    return factTail(n - 1, n * acc); // Lời gọi đệ quy là thao tác cuối cùng tuyệt đối
}
```

4. Chuyển đổi thành vòng lặp loại bỏ hoàn toàn đệ quy (O(1) bộ nhớ):
```java
public long factIterative(int n) {
    long acc = 1;
    while (n > 1) {
        acc *= n;
        n--;
    }
    return acc;
}
```
Kỹ thuật Tail Call Optimization (TCO) trong trình biên dịch sẽ tự động tái sử dụng Stack Frame hiện tại mà không cấp phát thêm, giảm độ phức tạp không gian từ O(n) xuống O(1).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa chính xác đệ quy đuôi** | **3.0đ** | Nêu rõ lời gọi đệ quy là thao tác cuối cùng và không còn phép tính dở dang. |
| **Phân tích phép nhân dở dang n * fact(n-1)** | **3.0đ** | Chỉ ra phép nhân phụ thuộc khiến compiler phải duy trì stack frame O(n). |
| **Cài đặt Accumulator và Vòng lặp O(1)** | **4.0đ** | Viết đúng phiên bản factTail dùng accumulator và phiên bản while loop O(1) space. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 29. [CSD_PQ29] Phân tích Cơ chế Tự Cân bằng và 4 Phép Xoay trên Cây AVL

- **Mã định danh (Question Code):** `CSD_PQ29`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 23 — *AVL Tree Rotations*
- **Mục tiêu đánh giá:** Đánh giá năng lực phân tích điều kiện mất cân bằng trên Cây AVL và nguyên lý thực thi 4 phép xoay LL, RR, LR, RL.

#### ❓ Câu hỏi vấn đáp:
> "Hệ số cân bằng (Balance Factor) của nút trong Cây AVL được định nghĩa như thế nào? Hãy phân tích 4 trường hợp mất cân bằng (Left-Left, Right-Right, Left-Right, Right-Left) và giải thích vì sao trường hợp Left-Right bắt buộc phải sử dụng phép xoay kép (Double Rotation) mà một phép xoay đơn không thể giải quyết được."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trong phép xoay kép Left-Right (LR) trên Cây AVL, thứ tự thực hiện hai phép xoay đơn lần lượt tại các nút nào và hệ số cân bằng của nút gốc mới sau khi xoay sẽ phụ thuộc vào yếu tố nào?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Định nghĩa Hệ số cân bằng (Balance Factor - BF):
`BF(node) = Height(node.left) - Height(node.right)`.
Một cây nhị phân tìm kiếm là Cây AVL nếu với mọi nút trong cây, `BF(node) thuộc {-1, 0, 1}`. Mất cân bằng xảy ra khi một nút có `|BF| >= 2`.

2. Bốn trường hợp mất cân bằng và phép xoay khôi phục:
- Trường hợp LL (Left-of-Left): Nút mới chèn vào cây con trái của con trái. `BF(node) = +2`, `BF(node.left) >= 0`. Khắc phục: Xoay phải đơn (Single Right Rotation) tại `node`.
- Trường hợp RR (Right-of-Right): Nút mới chèn vào cây con phải của con phải. `BF(node) = -2`, `BF(node.right) <= 0`. Khắc phục: Xoay trái đơn (Single Left Rotation) tại `node`.
- Trường hợp LR (Right-of-Left): Nút mới chèn vào cây con phải của con trái. `BF(node) = +2`, `BF(node.left) = -1`. Khắc phục: Xoay kép (Double Rotation LR) = Xoay trái tại `node.left` rồi Xoay phải tại `node`.
- Trường hợp RL (Left-of-Right): Nút mới chèn vào cây con trái của con phải. `BF(node) = -2`, `BF(node.right) = +1`. Khắc phục: Xoay kép (Double Rotation RL) = Xoay phải tại `node.right` rồi Xoay trái tại `node`.

3. Vì sao trường hợp LR bắt buộc phải xoay kép:
Trong trường hợp LR, cấu trúc nút bị gập khúc dạng zigzag (Left -> Right). Nút con trái L có cây con phải R_L nhô ra sâu nhất.
- Nếu cố tình áp dụng phép xoay phải đơn tại gốc A: Cây con R_L sẽ được chuyển sang làm con trái của A. Độ cao nhô ra này không hề suy giảm mà chỉ đổi bên, cây sau khi xoay vẫn bị mất cân bằng (trở thành dạng RR hoặc tiếp tục lệch).
- Giải pháp: Phép xoay trái đầu tiên tại nút con L sẽ kéo nút sâu nhất ở vị trí zigzag thẳng hàng thành dạng LL (Left-Left thẳng trục). Sau đó, phép xoay phải tại A mới đưa nút đó lên làm gốc mới một cách đối xứng, đưa chiều cao hai bên trở về trạng thái cân bằng tuyệt đối.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa Balance Factor** | **2.5đ** | Nêu đúng công thức BF = Height(Left) - Height(Right) và ngưỡng [-1, 0, 1]. |
| **Phân loại 4 trường hợp mất cân bằng** | **3.5đ** | Mô tả đầy đủ LL, RR, LR, RL kèm phép xoay tương ứng. |
| **Chứng minh bản chất phép xoay kép LR** | **4.0đ** | Giải thích hiện tượng zigzag và chứng minh phép xoay đơn chỉ làm đảo bên lệch chứ không giảm chiều cao. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 30. [CSD_PQ30] Phân tích Thuật toán HeapSort: Giai đoạn Build-Heap Tuyến tính O(n)

- **Mã định danh (Question Code):** `CSD_PQ30`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 24 — *HeapSort & Linear Build-Heap Analysis*
- **Mục tiêu đánh giá:** Đánh giá khả năng chứng minh toán học chi phí O(n) của giai đoạn Build-Heap từ dưới lên (Bottom-up).

#### ❓ Câu hỏi vấn đáp:
> "Thuật toán HeapSort gồm 2 giai đoạn: Xây dựng Max-Heap (Build-Heap) và Trích xuất phần tử sắp xếp. Tại sao giai đoạn xây dựng Max-Heap từ dưới lên (Bottom-up) từ một mảng lộn xộn chỉ tốn chi phí thời gian tuyến tính O(n) thay vì O(n log n)? Hãy trình bày chứng minh toán học."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu thay vì dùng giải thuật Bottom-up Build-Heap tuyến tính O(n), ta xây dựng heap bằng cách gọi n lần hàm `insert()` liên tiếp vào một heap ban đầu rỗng thì độ phức tạp thời gian xấu nhất sẽ là bao nhiêu và tại sao phương pháp này kém tối ưu hơn?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Hai giai đoạn của HeapSort:
- Giai đoạn 1 (Build-Heap): Biến mảng n phần tử thành Max-Heap từ dưới lên, bắt đầu từ nút không phải lá cuối cùng `index = (n/2) - 1` lùi dần về gốc 0, gọi `siftDown()`.
- Giai đoạn 2 (Sorting): Hoán vị phần tử gốc `a[0]` với `a[size - 1]`, giảm kích thước heap, gọi `siftDown(0)`. Lặp lại n - 1 lần, tốn O(n log n).

2. Chứng minh toán học giai đoạn Build-Heap tốn O(n):
Một ngộ nhận phổ biến là có n phần tử, mỗi lần siftDown tốn O(log n) nên tổng là O(n log n). Tuy nhiên, chi phí siftDown phụ thuộc vào độ cao của nút so với lá (h), chứ không phụ thuộc vào độ sâu từ gốc!
- Trong một cây nhị phân hoàn chỉnh n nút:
  + Khoảng n/2 nút là nút lá (chiều cao h = 0): Chi phí siftDown = 0.
  + Khoảng n/4 nút ở chiều cao h = 1: Chi phí siftDown tối đa = 1 bước.
  + Khoảng n/8 nút ở chiều cao h = 2: Chi phí siftDown tối đa = 2 bước.
  + Tổng quát, có tối đa ceil(n / 2^(h+1)) nút ở độ cao h.
- Tổng chi phí thời gian S:
  `S = sum_{h=0}^{floor(log n)} (n / 2^(h+1)) * h = (n / 2) * sum_{h=0}^{infinity} (h / 2^h)`.
- Chuỗi lũy thừa kinh điển:
  `sum_{h=0}^{infinity} (h / 2^h) = 1/2 + 2/4 + 3/8 + 4/16 + ... = 2`.
- Thay vào phương trình:
  `S = (n / 2) * 2 = n`.
Do đó, tổng số phép so sánh và hoán vị trong giai đoạn Build-Heap bị chặn trên bởi O(n).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Tổng quan 2 giai đoạn HeapSort** | **2.5đ** | Mô tả đúng Build-Heap Bottom-up và giai đoạn trích xuất gốc O(n log n). |
| **Phân tích số nút theo chiều cao h** | **3.5đ** | Chỉ ra công thức số lượng nút ở chiều cao h là n / 2^(h+1). |
| **Chứng minh chuỗi toán học hội tụ** | **4.0đ** | Tính chính xác tổng chuỗi sum(h / 2^h) = 2 và kết luận chặn trên O(n). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 31. [CSD_PQ31] Thuật toán Dijkstra Tìm Đường đi Ngắn nhất: Bản chất Tham lam và Cạnh Âm

- **Mã định danh (Question Code):** `CSD_PQ31`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000564`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 34 — *Dijkstra Algorithm & Negative Weights*
- **Mục tiêu đánh giá:** Kiểm tra mức độ thấu hiểu bản chất chiến lược Tham lam của Dijkstra và phân tích nguyên nhân thuật toán chạy sai khi có cạnh âm.

#### ❓ Câu hỏi vấn đáp:
> "Thuật toán Dijkstra tìm đường đi ngắn nhất nguồn đơn hoạt động theo chiến lược thiết kế thuật toán nào? Hãy phân tích độ phức tạp thời gian khi sử dụng Mảng đánh dấu vs Min-Heap. Giải thích cặn kẽ vì sao thuật toán Dijkstra thất bại khi đồ thị xuất hiện cạnh có trọng số âm?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu ta cộng thêm một hằng số dương lớn C vào tất cả các trọng số cạnh của một đồ thị có trọng số âm để triệt tiêu số âm rồi chạy thuật toán Dijkstra, liệu đường đi ngắn nhất tìm được có còn đảm bảo chính xác không, vì sao?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Chiến lược thiết kế thuật toán:
Dijkstra hoạt động theo chiến lược Tham lam (Greedy Strategy). Tại mỗi bước, thuật toán luôn chọn đỉnh u chưa thăm có khoảng cách tạm thời `dist[u]` nhỏ nhất từ nguồn để 'chốt' kết quả (đánh dấu u là đã hoàn tất).

2. Phân tích độ phức tạp thời gian:
- Cài đặt bằng Mảng (Linear Scan): Mỗi lần tìm đỉnh min tốn O(V), lặp V lần -> O(V^2). Thao tác cập nhật khoảng cách relaxation tốn O(1) mỗi cạnh -> O(E). Tổng thời gian: O(V^2 + E) = O(V^2). Phù hợp đồ thị dày.
- Cài đặt bằng Min-Heap / Priority Queue: Mỗi lần lấy đỉnh min tốn O(log V) -> O(V log V). Thao tác giảm khóa (decrease-key / push lại) tốn O(log V) mỗi cạnh -> O(E log V). Tổng thời gian: O((V + E) log V). Phù hợp đồ thị thưa.

3. Vì sao Dijkstra thất bại khi có cạnh trọng số âm:
Giả định nền tảng bất biến của Dijkstra: Khi một đỉnh u được lấy ra khỏi Min-Heap với `dist[u]` nhỏ nhất, khoảng cách này được coi là TỐI ƯU TUYỆT ĐỐI và sẽ không bao giờ bị giảm xuống nữa (vì mọi cạnh đi tiếp đều có trọng số >= 0, nên đi qua bất kỳ đường nào khác chỉ có thể làm tăng hoặc giữ nguyên chi phí).
Khi có cạnh trọng số âm:
- Giả sử có đường đi S -> A (chi phí 3) và S -> B (chi phí 5), từ B có cạnh âm B -> A (chi phí -4).
- Dijkstra sẽ lấy A ra trước (3 < 5) và chốt `dist[A] = 3`. Đỉnh A bị đánh dấu đã hoàn tất.
- Sau đó, khi xét tới B, đường đi thực tế qua S -> B -> A có tổng chi phí là 5 + (-4) = 1 (ngắn hơn 3). Nhưng do A đã bị chốt và không được duyệt lại, Dijkstra bỏ lỡ đường đi tối ưu này và cho ra kết quả sai!
- Để xử lý đồ thị có trọng số âm, bắt buộc phải dùng thuật toán Bellman-Ford (chi phí O(V * E)).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Bản chất chiến lược Greedy** | **2.5đ** | Nêu rõ nguyên lý chọn đỉnh có dist nhỏ nhất chưa thăm để chốt kết quả. |
| **So sánh Big-O Mảng vs Min-Heap** | **3.5đ** | Phân tích chính xác O(V^2) cho mảng và O((V+E) log V) cho Min-Heap. |
| **Chứng minh thất bại với cạnh âm** | **4.0đ** | Chỉ rõ vi phạm giả định bất biến 'khoảng cách đã chốt không giảm' kèm phản ví dụ cụ thể. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 32. [CSD_PQ32] So sánh Chuyên sâu Thuật toán Prim và Kruskal Tìm Cây Khung Nhỏ nhất

- **Mã định danh (Question Code):** `CSD_PQ32`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000564`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 37-38 — *Minimum Spanning Trees: Prim vs Kruskal*
- **Mục tiêu đánh giá:** Đánh giá năng lực so sánh hai thuật toán tìm cây khung nhỏ nhất và phân tích cấu trúc dữ liệu Disjoint Set Union.

#### ❓ Câu hỏi vấn đáp:
> "Hãy so sánh toàn diện hai thuật toán tìm Cây khung nhỏ nhất (MST): Thuật toán Prim và Thuật toán Kruskal về: Cách tiếp cận phát triển cây, Cấu trúc dữ liệu bổ trợ, Độ phức tạp thời gian và Tiêu chuẩn lựa chọn trên đồ thị dày (Dense) vs đồ thị thưa (Sparse)."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trong thuật toán Kruskal, vai trò cốt lõi của cấu trúc dữ liệu Disjoint Set Union (DSU / Union-Find) kết hợp kỹ thuật Nén đường đi (Path Compression) và Hợp nhất theo hạng (Union by Rank) là gì trong việc kiểm soát chu trình đồ thị?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Bảng so sánh toàn diện Prim vs Kruskal:

1. Cách tiếp cận xây dựng MST:
- Prim (Vertex-based / Cụm đỉnh): Bắt đầu từ 1 đỉnh tùy ý, duy trì một cây đơn lẻ và liên tục mở rộng cây bằng cách kết nạp thêm cạnh có trọng số nhỏ nhất nối từ một đỉnh đã thuộc cây sang một đỉnh chưa thuộc cây. Cây luôn liên thông tại mọi bước.
- Kruskal (Edge-based / Cụm cạnh): Sắp xếp toàn bộ E cạnh theo trọng số tăng dần. Duy trì một rừng (forest) gồm V cây rời rạc. Lần lượt xét từng cạnh: nếu cạnh nối 2 thành phần liên thông khác nhau (không tạo chu trình), kết nạp cạnh đó vào MST và gộp hai cây lại.

2. Cấu trúc dữ liệu bổ trợ:
- Prim: Sử dụng Hàng đợi ưu tiên (Min-Heap) để lưu trữ và lấy nhanh cạnh/đỉnh có khoảng cách nhỏ nhất tới cây hiện tại.
- Kruskal: Bắt buộc sử dụng cấu trúc Dữ liệu các tập hợp rời nhau (Disjoint Set Union - DSU / Union-Find) với kỹ thuật nén đường đi (Path Compression) và gộp theo hạng (Union by Rank) để kiểm tra chu trình và gộp tập hợp trong thời gian gần như hằng số O(alpha(V)).

3. Độ phức tạp thời gian:
- Prim (dùng Binary Heap): O((V + E) log V) = O(E log V). Nếu dùng Fibonacci Heap đạt O(E + V log V).
- Kruskal: Bước sắp xếp cạnh tốn O(E log E). Các thao tác DSU tốn O(E * alpha(V)). Vì E <= V^2 nên log E <= 2 log V, tổng thời gian là O(E log E) = O(E log V).

4. Tiêu chí lựa chọn:
- Chọn Prim khi: Đồ thị rất dày (Dense Graph, E xấp xỉ V^2). Đặc biệt Prim cài đặt bằng Ma trận kề O(V^2) chạy nhanh hơn Kruskal vì không phải sắp xếp E cạnh.
- Chọn Kruskal khi: Đồ thị thưa (Sparse Graph, E xấp xỉ V). Kruskal đơn giản, dễ cài đặt và các cạnh đã có thể sắp xếp sẵn.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **So sánh cách tiếp cận Vertex vs Edge** | **3.0đ** | Phân biệt rõ cây liên thông phát triển dần của Prim so với rừng gộp cạnh của Kruskal. |
| **Đặc tả vai trò cấu trúc DSU của Kruskal** | **3.5đ** | Mô tả vai trò của Union-Find chống chu trình trong Kruskal so với Min-Heap của Prim. |
| **Phân tích Big-O và tiêu chí lựa chọn** | **3.5đ** | So sánh chính xác độ phức tạp O(E log V) và đưa ra quyết định Dense vs Sparse. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 33. [CSD_PQ33] Phân tích Điều kiện Tồn tại Chu trình Euler và So sánh với Chu trình Hamilton

- **Mã định danh (Question Code):** `CSD_PQ33`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000564`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 38 — *Eulerian vs Hamiltonian Cycles*
- **Mục tiêu đánh giá:** Đánh giá khả năng phân biệt sự khác biệt bản chất độ phức tạp giữa bài toán Chu trình Euler (đa thức P) và Hamilton (NP-Complete).

#### ❓ Câu hỏi vấn đáp:
> "Định nghĩa Chu trình Euler (Eulerian Cycle) và Chu trình Hamilton (Hamiltonian Cycle). Nêu điều kiện cần và đủ để một đồ thị vô hướng liên thông có Chu trình Euler. Tại sao bài toán kiểm tra chu trình Euler thuộc lớp P trong khi bài toán chu trình Hamilton thuộc lớp NP-Đầy đủ (NP-Complete)?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Định lý Euler-Hierholzer sử dụng điều kiện bậc chẵn của các đỉnh để xây dựng chu trình Euler trong thời gian O(V + E) như thế nào, và tại sao tính chất bậc đỉnh cục bộ lại hoàn toàn bất lực trong việc xác định sự tồn tại của chu trình Hamilton?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Định nghĩa hai loại chu trình:
- Chu trình Euler: Là một chu trình đóng trong đồ thị đi qua MỖI CẠNH của đồ thị đúng một lần và quay trở về đỉnh xuất phát.
- Chu trình Hamilton: Là một chu trình đóng trong đồ thị đi qua MỖI ĐỈNH của đồ thị đúng một lần (ngoại trừ đỉnh xuất phát và kết thúc trùng nhau).

2. Định lý Euler (Điều kiện cần và đủ):
Một đồ thị vô hướng liên thông G có chu trình Euler khi và chỉ khi MỌI ĐỈNH của đồ thị đều có BẬC CHẴN (`deg(v) % 2 == 0` với mọi v thuộc V).

3. Phân tích độ phức tạp: Lớp P vs Lớp NP-Complete:
- Chu trình Euler thuộc lớp P (Thời gian đa thức):
  + Để kiểm tra đồ thị có chu trình Euler hay không, ta chỉ cần: (1) Kiểm tra tính liên thông của các đỉnh có cạnh bằng BFS/DFS tốn O(V + E), và (2) Đếm bậc của từng đỉnh từ danh sách kề tốn O(V).
  + Thuật toán Hierholzer hoặc Fleury tìm chu trình Euler chạy trong thời gian tuyến tính O(E).
  + Điều kiện tồn tại là điều kiện cục bộ, độc lập tại từng đỉnh (chẵn/lẻ) có thể kiểm tra trực tiếp.

- Chu trình Hamilton thuộc lớp NP-Complete:
  + Không tồn tại bất kỳ điều kiện cần và đủ đơn giản mang tính địa phương nào dựa trên bậc của đỉnh để kết luận chắc chắn sự tồn tại của chu trình Hamilton trên đồ thị tổng quát (chỉ có các điều kiện đủ như Định lý Dirac, Ore).
  + Việc tìm chu trình Hamilton đòi hỏi phải giải quyết tính chất toàn cục (Global Property) của đồ thị. Mọi thuật toán chính xác đã biết đều phải vét cạn hoặc quay lui (Backtracking) với độ phức tạp thời gian hàm mũ O(2^n * n^2) hoặc O(n!).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa chính xác 2 chu trình** | **3.0đ** | Nêu rõ Euler đi qua mỗi CẠNH 1 lần, Hamilton đi qua mỗi ĐỈNH 1 lần. |
| **Định lý bậc chẵn của Euler** | **3.0đ** | Phát biểu chuẩn xác điều kiện cần và đủ: liên thông và mọi đỉnh có bậc chẵn. |
| **Lập luận lớp P vs NP-Complete** | **4.0đ** | Giải thích bản chất kiểm tra cục bộ O(V+E) của Euler so với tính chất toàn cục mũ của Hamilton. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 34. [CSD_PQ34] So sánh Toàn diện QuickSort vs MergeSort

- **Mã định danh (Question Code):** `CSD_PQ34`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO6** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 45 — *QuickSort vs MergeSort Comparison*
- **Mục tiêu đánh giá:** Đánh giá năng lực phân tích đa chiều giữa hai thuật toán sắp xếp chia để trị hàng đầu trong ngành công nghiệp phần mềm.

#### ❓ Câu hỏi vấn đáp:
> "Hãy so sánh toàn diện QuickSort và MergeSort trên các phương diện: Nguyên lý chia để trị, Độ phức tạp thời gian (Best, Average, Worst), Bộ nhớ phụ trợ (Space Complexity), Tính ổn định (Stability) và Hiệu ứng bộ nhớ đệm (Cache Locality). Tại sao Java sử dụng Dual-Pivot QuickSort cho kiểu dữ liệu nguyên thủy (primitives) nhưng lại dùng TimSort/MergeSort cho mảng đối tượng (Objects)?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao tính ổn định (Stability) của thuật toán sắp xếp lại là yêu cầu sống còn khi sắp xếp danh sách đối tượng nhiều trường dữ liệu (ví dụ: sắp xếp sinh viên theo Điểm thi sau khi đã sắp xếp theo Tên), khiến Java buộc phải dùng TimSort thay vì Dual-Pivot QuickSort?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Bảng so sánh toàn diện QuickSort vs MergeSort:

| Tiêu chí | QuickSort | MergeSort |
|---|---|---|
| Nguyên lý Chia để trị | Chia khó (phân hoạch Partition theo Pivot) -> Trị dễ (không cần gộp) | Chia dễ (chia đôi mảng n/2) -> Trị khó (gộp Merge hai mảng con có thứ tự) |
| Thời gian Tốt nhất (Best) | O(n log n) | O(n log n) |
| Thời gian Trung bình (Avg) | O(n log n) | O(n log n) |
| Thời gian Xấu nhất (Worst) | O(n^2) (khi chọn pivot tồi) | O(n log n) (luôn bảo đảm tuyệt đối) |
| Bộ nhớ phụ (Space) | O(log n) (ngăn xếp đệ quy) - In-place | O(n) (bắt buộc mảng phụ để Merge) |
| Tính ổn định (Stability) | Không ổn định (Unstable) | Ổn định (Stable) |
| Cache Locality | Rất tốt (truy cập mảng cục bộ liên tục) | Kém hơn (phải sao chép qua lại mảng phụ) |

Lý do thiết kế trong thư viện chuẩn Java (JDK `Arrays.sort()`):
1. Đối với kiểu nguyên thủy (int, double, char):
- Kiểu nguyên thủy chỉ có giá trị thuần túy, tính ổn định (Stability) hoàn toàn không có ý nghĩa (hai số 5 đứng trước hay sau nhau không làm thay đổi kết quả).
- Dual-Pivot QuickSort chạy trực tiếp tại chỗ (in-place), không tốn O(n) RAM, tận dụng tối đa CPU cache và có hằng số ẩn nhỏ hơn MergeSort rất nhiều -> Tốc độ nhanh hơn vượt trội.

2. Đối với mảng Đối tượng (Objects):
- Tính ổn định là yêu cầu sống còn. Ví dụ: Sắp xếp danh sách Sinh viên theo Điểm số, những sinh viên bằng điểm nhau bắt buộc phải giữ nguyên thứ tự sắp xếp theo Tên trước đó.
- MergeSort (và biến thể TimSort) bảo đảm tính ổn định 100% và bảo đảm thời gian O(n log n) ngay cả trong trường hợp xấu nhất.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Bảng so sánh đa tiêu chí** | **4.0đ** | So sánh đầy đủ và chính xác nguyên lý chia, 3 trường hợp Big-O, Space, Stability và Cache. |
| **Giải thích trường hợp kiểu nguyên thủy Java** | **3.0đ** | Chỉ ra tính vô nghĩa của Stability với primitives và ưu thế in-place cache locality của QuickSort. |
| **Giải thích trường hợp đối tượng trong Java** | **3.0đ** | Chứng minh tính bắt buộc của Stability trong nghiệp vụ đối tượng và bảo đảm O(n log n). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 35. [CSD_PQ35] Hiện tượng Cụm Chính trong Linear Probing, Hệ số Tải và Kỹ thuật Rehashing

- **Mã định danh (Question Code):** `CSD_PQ35`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 50 — *Open Addressing & Rehashing*
- **Mục tiêu đánh giá:** Kiểm tra mức độ am hiểu về hiện tượng suy thoái hiệu năng trong phương pháp mở địa chỉ và cơ chế mở rộng bảng băm.

#### ❓ Câu hỏi vấn đáp:
> "Hiện tượng Cụm chính (Primary Clustering) trong phương pháp Dò tuyến tính (Linear Probing) là gì và tại sao nó khiến hiệu năng tìm kiếm suy thoái nghiêm trọng về O(n)? Hãy giải thích ý nghĩa của Hệ số tải (Load Factor alpha = n/M) và mô tả quy trình Rehashing khi alpha vượt ngưỡng ngưỡng cho phép."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Phương pháp Băm kép (Double Hashing) giải quyết triệt để hiện tượng phân cụm (Clustering) như thế nào và hàm băm thứ hai `h2(k)` cần thỏa mãn điều kiện toán học gì đối với kích thước bảng M để đảm bảo duyệt qua được mọi vị trí trong bảng?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Hiện tượng Cụm chính (Primary Clustering):
- Trong Dò tuyến tính, khi xảy ra xung đột tại `h(k)`, thuật toán kiểm tra tuần tự các ô tiếp theo: `h(k) + 1, h(k) + 2, ... (mod M)`.
- Khi nhiều phần tử liên tiếp bị xung đột, chúng sẽ tạo thành một dải ô bị lấp đầy liên tục kéo dài gọi là một 'cụm' (Cluster).
- Vấn đề nghiêm trọng: Bất kỳ khóa mới nào băm trúng vào bất kỳ ô nào thuộc cụm này đều sẽ bị đẩy về cuối cụm và làm cho cụm đó dài thêm ra (hiệu ứng quả cầu tuyết). Khả năng một cụm bị dài ra tỷ lệ thuận với độ dài hiện tại của chính nó.
- Hậu quả: Thời gian dò tìm kiếm cả thành công và thất bại tăng vọt, chi phí trung bình tiệm cận O(n) thay vì O(1).

2. Hệ số tải (Load Factor alpha = n / M):
- Là tỷ lệ giữa số lượng phần tử đang lưu trong bảng (n) và tổng dung lượng mảng băm (M).
- Trong Open Addressing, alpha luôn phải < 1.0 (thường duy trì <= 0.5 hoặc 0.75). Khi alpha tiến gần 1, các ô trống trở nên cực kỳ hiếm hoi, số bước dò tìm kiếm tăng theo hàm `1 / (1 - alpha)`.

3. Quy trình Tái băm (Rehashing):
Khi `alpha >= threshold` (thường là 0.75 trong Java HashMap):
- Bước 1: Cấp phát một mảng băm mới có kích thước M_moi thường gấp đôi kích thước cũ (hoặc chọn số nguyên tố lớn hơn gấp đôi để giảm xung đột).
- Bước 2: Duyệt qua tất cả các phần tử trong mảng cũ. Với mỗi phần tử, KHÔNG THỂ sao chép nguyên chỉ số cũ, mà bắt buộc phải tính toán lại vị trí băm mới: `new_idx = hash(key) % M_moi`.
- Bước 3: Chèn phần tử vào bảng mới theo cơ chế giải quyết xung đột.
- Bước 4: Giải phóng bảng cũ và trỏ tham chiếu sang bảng mới.
Mặc dù thao tác Rehashing tốn O(n), nhưng vì chỉ diễn ra sau mỗi n/2 thao tác chèn, nên theo phân tích Phí tổn khấu hao (Amortized Analysis), chi phí cho mỗi lần chèn vẫn được đảm bảo là O(1).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích hiện tượng Primary Clustering** | **3.5đ** | Mô tả cơ chế quả cầu tuyết của cụm dài làm tăng số bước dò tiệm cận O(n). |
| **Định nghĩa Load Factor và ngưỡng suy thoái** | **2.5đ** | Nêu đúng công thức alpha = n/M và mối quan hệ với số bước dò 1/(1-alpha). |
| **Quy trình 4 bước Rehashing và Amortized O(1)** | **4.0đ** | Mô tả đầy đủ việc tạo mảng gấp đôi, băm lại toàn bộ khóa và phân tích amortized cost. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 36. [CSD_PQ36] Nguyên lý Xây dựng Cây Mã hóa Huffman Tối ưu Tiền tố và Thuật toán Giải nén Bit

- **Mã định danh (Question Code):** `CSD_PQ36`
- **Kho lưu trữ (Target Pool):** `practice_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO8** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 57 — *Huffman Coding Algorithm*
- **Mục tiêu đánh giá:** Đánh giá năng lực phân tích thuật toán nén dữ liệu Huffman, cấu trúc cây tiền tố và cơ chế giải mã chuỗi bit.

#### ❓ Câu hỏi vấn đáp:
> "Thuật toán mã hóa Huffman sử dụng cấu trúc dữ liệu nào để xây dựng cây mã hóa tối ưu? Hãy trình bày các bước xây dựng cây Huffman từ bảng tần số ký tự. Thế nào là Mã tiền tố (Prefix Code) và làm thế nào để giải mã (decode) một luồng bit nén bằng cây Huffman?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu bảng tần số của n ký tự là dãy số Fibonacci (1, 1, 2, 3, 5, 8, ...), cây mã hóa Huffman thu được sẽ có hình dạng đặc biệt gì và độ dài từ mã của ký tự có tần số nhỏ nhất sẽ là bao nhiêu?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cấu trúc dữ liệu sử dụng:
Thuật toán Huffman sử dụng Hàng đợi ưu tiên (Min-Heap / Priority Queue) để liên tục chọn ra hai cây con có tần số xuất hiện nhỏ nhất.

2. Các bước xây dựng Cây Huffman:
- Bước 1: Với mỗi ký tự c có tần số xuất hiện f(c), tạo một nút cây lá duy nhất chứa (c, f(c)).
- Bước 2: Đẩy toàn bộ các nút lá này vào Min-Heap (sắp xếp theo tần số tăng dần).
- Bước 3: Lặp lại thao tác sau cho đến khi trong Min-Heap chỉ còn lại đúng 1 nút duy nhất:
  + Rút ra nút có tần số nhỏ nhất từ heap: đặt là `left`.
  + Rút ra nút có tần số nhỏ nhì từ heap: đặt là `right`.
  + Tạo một nút cha nội bộ mới có tần số bằng tổng hai con: `parent.freq = left.freq + right.freq`. Gán con trái là `left`, con phải là `right`.
  + Đẩy nút `parent` mới này trở lại Min-Heap.
- Nút cuối cùng còn lại trong Min-Heap chính là Gốc của Cây Huffman (Huffman Tree Root).
- Quy ước gán nhãn: Nhánh rẽ sang trái gán bit '0', nhánh rẽ sang phải gán bit '1'. Ký tự có tần suất cao sẽ ở gần gốc hơn (mã bit ngắn hơn).

3. Khái niệm Mã tiền tố (Prefix Code / Prefix-Free Code):
Là tập hợp các từ mã mà trong đó KHÔNG có từ mã của bất kỳ ký tự nào là tiền tố (prefix) của một từ mã khác.
Tính chất này cực kỳ quan trọng vì nó cho phép giải nén tức thì không gây mơ hồ (unambiguous decoding) mà không cần ký tự phân cách giữa các từ mã.

4. Thuật toán giải mã (Decode) luồng bit:
- Bắt đầu từ nút Gốc của Cây Huffman.
- Đọc từng bit trong luồng bit nén:
  + Nếu bit là '0': Đi theo nhánh con trái.
  + Nếu bit là '1': Đi theo nhánh con phải.
  + Khi chạm tới một Nút lá: Ta lập tức xuất ra ký tự lưu tại nút lá đó, sau đó quay trở lại nút Gốc của cây để tiếp tục giải mã ký tự tiếp theo. Lặp lại cho đến khi hết luồng bit.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Thuật toán dựng cây dùng Min-Heap** | **4.0đ** | Trình bày đủ quy trình rút 2 nút nhỏ nhất, gộp tổng tần số và chèn lại heap cho đến khi còn 1 nút. |
| **Định nghĩa Mã tiền tố (Prefix Code)** | **3.0đ** | Giải thích tính chất không có từ mã nào là tiền tố của từ mã khác và ý nghĩa chống mơ hồ. |
| **Thuật toán duyệt bit giải mã** | **3.0đ** | Mô tả chuẩn xác quá trình đi theo bit 0/1 từ gốc đến lá và quay lại gốc. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

## PHẦN 4: CHI TIẾT 26 CÂU HỎI KHO THI THẬT PHÒNG LAB KIOSK (`exam_questions`)

> Kho câu hỏi này được lưu trữ độc lập trong bảng `exam_questions`, phục vụ độc quyền luồng **MF-04**:
> 1. Bám sát ma trận cấu trúc đề thi chính thức (`exam_structures`, `slot_definitions`) do Trưởng Bộ Môn phê duyệt.
> 2. Khảo thí phân bổ đề không trùng (`uq_tickets_shift_exam_set`) cho sinh viên trên máy trạm Kiosk phòng Lab.
> 3. Phân bổ theo 4 bậc nhận thức Bloom: **6 Remember (23%) + 8 Understand (31%) + 7 Apply (27%) + 5 Analyze (19%) = 26 câu (100%)**.

### 1. [CSD_EQ01] Định nghĩa Ký pháp Tiệm cận Big-O, Big-Omega và Big-Theta

- **Mã định danh (Question Code):** `CSD_EQ01`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO1** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 1 — *Asymptotic Notations*
- **Mục tiêu đánh giá:** Đánh giá khả năng ghi nhớ và phát biểu chính xác định nghĩa toán học của 3 ký pháp tiệm cận cơ bản.

#### ❓ Câu hỏi vấn đáp:
> "Hãy phát biểu định nghĩa toán học chính xác của 3 ký pháp tiệm cận: Big-O, Big-Omega (Ω) và Big-Theta (Θ). Ký pháp nào thể hiện chặn trên, chặn dưới và chặn chặt tiệm cận?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Một thuật toán có độ phức tạp thời gian đo được là O(n^2) thì có thể khẳng định nó cũng là O(n^3) và O(2^n) theo định nghĩa toán học thuần túy của Big-O không? Tại sao trong phân tích thuật toán thực tế ta luôn tìm hàm g(n) tiệm cận chặt nhất có thể?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Định nghĩa toán học của 3 ký pháp tiệm cận:
- Big-O (Chặn trên tiệm cận - Asymptotic Upper Bound):
  f(n) = O(g(n)) nếu tồn tại hai hằng số dương c > 0 và n0 >= 1 sao cho:
  0 <= f(n) <= c * g(n) với mọi n >= n0.
  Ý nghĩa: Tốc độ tăng của f(n) không vượt quá g(n).

- Big-Omega (Ω) (Chặn dưới tiệm cận - Asymptotic Lower Bound):
  f(n) = Ω(g(n)) nếu tồn tại hai hằng số dương c > 0 và n0 >= 1 sao cho:
  0 <= c * g(n) <= f(n) với mọi n >= n0.
  Ý nghĩa: Tốc độ tăng của f(n) tối thiểu bằng g(n).

- Big-Theta (Θ) (Chặn chặt tiệm cận - Asymptotically Tight Bound):
  f(n) = Θ(g(n)) nếu tồn tại ba hằng số dương c1 > 0, c2 > 0 và n0 >= 1 sao cho:
  0 <= c1 * g(n) <= f(n) <= c2 * g(n) với mọi n >= n0.
  Điều kiện tương đương: f(n) = Θ(g(n)) khi và chỉ khi f(n) = O(g(n)) và f(n) = Ω(g(n)).
  Ý nghĩa: f(n) và g(n) có cùng bậc tăng trưởng tiệm cận.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa toán học Big-O** | **3.0đ** | Nêu đúng điều kiện f(n) <= c*g(n) với mọi n >= n0 và vai trò chặn trên. |
| **Định nghĩa toán học Big-Omega** | **3.0đ** | Nêu đúng điều kiện c*g(n) <= f(n) với mọi n >= n0 và vai trò chặn dưới. |
| **Định nghĩa toán học Big-Theta** | **4.0đ** | Nêu đúng điều kiện chặn kép c1*g(n) <= f(n) <= c2*g(n) và vai trò chặn chặt. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 2. [CSD_EQ02] Định nghĩa Cấu trúc Dữ liệu Stack, Queue và Phân biệt LIFO vs FIFO

- **Mã định danh (Question Code):** `CSD_EQ02`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 5 — *Stack and Queue Definitions*
- **Mục tiêu đánh giá:** Kiểm tra khả năng định nghĩa cấu trúc dữ liệu tuyến tính và phân biệt nguyên tắc truy cập dữ liệu.

#### ❓ Câu hỏi vấn đáp:
> "Định nghĩa cấu trúc dữ liệu Ngăn xếp (Stack) và Hàng đợi (Queue). Nêu rõ từ viết tắt và ý nghĩa của hai nguyên tắc truy cập dữ liệu LIFO và FIFO tương ứng."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trong kiến trúc hệ điều hành máy tính, cơ chế ngắt phần cứng (Hardware Interrupt Handling) và hàng đợi tin nhắn tiến trình (IPC Message Queue) lần lượt ứng dụng cấu trúc dữ liệu nào trong hai cấu trúc Stack và Queue? Giải thích lý do lựa chọn."*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Ngăn xếp (Stack):
Là một cấu trúc dữ liệu trừu tượng tuyến tính dạng danh sách có thứ tự, trong đó thao tác chèn phần tử mới (push) và thao tác xóa phần tử (pop) chỉ được thực hiện tại một đầu duy nhất gọi là Đỉnh ngăn xếp (Top).
- Nguyên tắc hoạt động: LIFO (Last-In-First-Out) - Phần tử được thêm vào sau cùng sẽ là phần tử đầu tiên được lấy ra khỏi cấu trúc.

2. Hàng đợi (Queue):
Là một cấu trúc dữ liệu trừu tượng tuyến tính trong đó thao tác chèn phần tử (enqueue) chỉ được thực hiện tại một đầu gọi là Đuôi hàng đợi (Rear/Tail), và thao tác xóa phần tử (dequeue) chỉ được thực hiện tại đầu đối diện gọi là Đầu hàng đợi (Front/Head).
- Nguyên tắc hoạt động: FIFO (First-In-First-Out) - Phần tử được đưa vào hàng đợi sớm nhất sẽ là phần tử đầu tiên được lấy ra phục vụ.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa Stack và LIFO** | **5.0đ** | Định nghĩa đúng cấu trúc thao tác tại Top và giải thích nguyên tắc Last-In-First-Out. |
| **Định nghĩa Queue và FIFO** | **5.0đ** | Định nghĩa đúng cấu trúc thao tác ở 2 đầu Front/Rear và giải thích First-In-First-Out. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 3. [CSD_EQ03] Định nghĩa Cây Nhị phân Đầy đủ và Cây Nhị phân Hoàn chỉnh

- **Mã định danh (Question Code):** `CSD_EQ03`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 15 — *Binary Tree Types*
- **Mục tiêu đánh giá:** Đánh giá khả năng phân biệt chính xác hai khái niệm cây nhị phân thường bị nhầm lẫn.

#### ❓ Câu hỏi vấn đáp:
> "Thế nào là một Cây nhị phân đầy đủ (Full Binary Tree / Proper Binary Tree) và thế nào là một Cây nhị phân hoàn chỉnh (Complete Binary Tree)? Cho biết cấu trúc nào là điều kiện tiên quyết của Binary Heap."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao tính chất 'Cây nhị phân hoàn chỉnh' (Complete Binary Tree) lại cho phép ánh xạ hoàn hảo các nút của cây vào một mảng một chiều liên tục mà không gây lãng phí bất kỳ ô nhớ trống nào ở giữa các phần tử?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cây nhị phân đầy đủ (Full Binary Tree / Proper Binary Tree):
Là cây nhị phân mà trong đó MỖI NÚT đều có đúng 0 hoặc 2 nút con. Nói cách khác, không có bất kỳ nút nào trong cây chỉ có duy nhất 1 nút con (bậc của mọi nút đều thuộc {0, 2}).

2. Cây nhị phân hoàn chỉnh (Complete Binary Tree):
Là cây nhị phân thỏa mãn hai điều kiện nghiêm ngặt:
- Mọi tầng của cây đều được lấp đầy hoàn toàn các nút, ngoại trừ có thể tầng cuối cùng (tầng sâu nhất).
- Tại tầng cuối cùng, toàn bộ các nút phải được xếp dồn liên tục về phía bên trái nhất có thể (từ trái qua phải, không để lại bất kỳ khoảng trống nào).

3. Điều kiện tiên quyết của Binary Heap:
Cây nhị phân hoàn chỉnh (Complete Binary Tree) chính là cấu trúc hình học bắt buộc của Binary Heap. Nhờ đặc tính này, Binary Heap có thể biểu diễn hoàn hảo trên mảng một chiều phẳng mà không bị lãng phí ô nhớ.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa Full Binary Tree** | **3.5đ** | Nêu rõ mọi nút đều có 0 hoặc 2 nút con, không có nút bậc 1. |
| **Định nghĩa Complete Binary Tree** | **4.0đ** | Mô tả chuẩn xác lấp đầy các tầng và dồn nút về bên trái ở tầng cuối. |
| **Mối liên hệ với Binary Heap** | **2.5đ** | Chỉ ra Complete Binary Tree là điều kiện hình học nền tảng của Heap. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 4. [CSD_EQ04] Phát biểu Định lý Bắt tay (Handshaking Lemma) trong Lý thuyết Đồ thị

- **Mã định danh (Question Code):** `CSD_EQ04`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000564`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 22 — *Graph Handshaking Lemma*
- **Mục tiêu đánh giá:** Kiểm tra việc ghi nhớ định lý bắt tay và hệ quả về số đỉnh bậc lẻ trong đồ thị vô hướng.

#### ❓ Câu hỏi vấn đáp:
> "Hãy phát biểu Định lý Bắt tay (Handshaking Lemma) trong lý thuyết đồ thị vô hướng. Nêu công thức liên hệ giữa tổng bậc của tất cả các đỉnh và số lượng cạnh. Từ định lý này, hãy suy ra tính chất về số lượng các đỉnh có bậc lẻ trong bất kỳ đồ thị vô hướng nào."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Có thể tồn tại một đồ thị đơn vô hướng gồm đúng 7 đỉnh mà mỗi đỉnh đều có bậc bằng 3 không? Hãy sử dụng Định lý Bắt tay (Handshaking Lemma) để chứng minh tính bất khả thi của đồ thị này."*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Phát biểu Định lý Bắt tay (Handshaking Lemma):
Trong bất kỳ đồ thị vô hướng G = (V, E) nào, tổng bậc của tất cả các đỉnh luôn luôn bằng đúng hai lần số lượng cạnh của đồ thị:
`sum_{v thuộc V} deg(v) = 2 * |E|`
Ý nghĩa trực quan: Mỗi một cạnh e = (u, v) bất kỳ luôn nối giữa hai đỉnh u và v, do đó khi xuất hiện một cạnh, nó đóng góp chính xác 1 đơn vị bậc cho đỉnh u và 1 đơn vị bậc cho đỉnh v (tương tự như một cái bắt tay cần đúng 2 bàn tay).

2. Hệ quả về số lượng đỉnh bậc lẻ:
- Vì tổng các bậc là 2 * |E| nên tổng này luôn luôn là một SỐ CHẴN.
- Tách tổng bậc thành hai nhóm: nhóm các đỉnh có bậc chẵn (V_chan) và nhóm các đỉnh có bậc lẻ (V_le):
  `sum_{v thuộc V_chan} deg(v) + sum_{u thuộc V_le} deg(u) = 2 * |E| (chẵn)`.
- Vì `sum_{v thuộc V_chan} deg(v)` là tổng các số chẵn nên bản thân nó là một số chẵn.
- Để tổng hai nhóm là chẵn, bắt buộc `sum_{u thuộc V_le} deg(u)` phải là một số chẵn.
- Tổng của một tập hợp các số lẻ muốn là số chẵn thì số lượng các số hạng trong tập đó bắt buộc phải là một số chẵn.
- Kết luận: Trong mọi đồ thị vô hướng, số lượng các đỉnh có bậc lẻ LUÔN LUÔN LÀ MỘT SỐ CHẴN.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phát biểu và công thức Handshaking Lemma** | **4.0đ** | Nêu đúng công thức sum(deg(v)) = 2*|E| và giải thích ý nghĩa mỗi cạnh đóng góp 2 bậc. |
| **Chứng minh tính chẵn của tổng bậc** | **3.0đ** | Chỉ ra 2*|E| là số chẵn dẫn đến tổng bậc lẻ phải là số chẵn. |
| **Hệ quả số đỉnh bậc lẻ là số chẵn** | **3.0đ** | Kết luận chính xác số lượng đỉnh có bậc lẻ trong đồ thị vô hướng luôn luôn là số chẵn. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 5. [CSD_EQ05] Định nghĩa Tính Ổn định của Thuật toán Sắp xếp và Phân loại

- **Mã định danh (Question Code):** `CSD_EQ05`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO6** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 41 — *Sorting Stability*
- **Mục tiêu đánh giá:** Đánh giá khả năng định nghĩa tính ổn định trong sắp xếp và phân loại chính xác các thuật toán.

#### ❓ Câu hỏi vấn đáp:
> "Tính ổn định (Stability) của một thuật toán sắp xếp là gì? Hãy liệt kê 2 thuật toán sắp xếp có tính ổn định và 2 thuật toán sắp xếp không có tính ổn định."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Thuật toán Selection Sort có tính ổn định không? Hãy đưa ra một mảng ví dụ cụ thể gồm 3 đến 4 phần tử minh chứng rõ nét việc Selection Sort làm đảo lộn thứ tự tương đối ban đầu của các phần tử có cùng giá trị khóa."*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Định nghĩa Tính ổn định (Stability) của thuật toán sắp xếp:
Một thuật toán sắp xếp được gọi là Ổn định (Stable) nếu nó bảo toàn thứ tự xuất hiện ban đầu tương đối giữa các phần tử có khóa sắp xếp bằng nhau.
Cụ thể: Nếu trong mảng ban đầu, phần tử A đứng trước phần tử B và khóa của A bằng khóa của B (`key(A) == key(B)`), thì trong mảng sau khi sắp xếp, phần tử A vẫn chắc chắn đứng trước phần tử B.

2. Phân loại các thuật toán sắp xếp tiêu biểu:
- Hai thuật toán sắp xếp ỔN ĐỊNH (Stable):
  1. MergeSort: Khi gộp 2 mảng con, nếu gặp hai phần tử bằng nhau, phần tử từ mảng con bên trái luôn được chọn trước.
  2. Insertion Sort: Khi dịch chuyển mảng, phần tử chỉ chèn qua các phần tử lớn hơn hẳn, giữ nguyên thứ tự với phần tử bằng nhau.
  (Ngoài ra còn có: Bubble Sort, Radix Sort, Counting Sort).

- Hai thuật toán sắp xếp KHÔNG ỔN ĐỊNH (Unstable):
  1. QuickSort: Các phép hoán vị (swap) qua khoảng cách xa của con trỏ quanh Pivot có thể đảo lộn thứ tự tương đối của các phần tử bằng nhau.
  2. HeapSort: Việc vun đống (siftDown) và hoán vị gốc với phần tử cuối phá vỡ thứ tự gốc của các khóa trùng nhau.
  (Ngoài ra còn có: Selection Sort).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa chuẩn xác tính ổn định** | **4.0đ** | Nêu rõ điều kiện bảo toàn thứ tự tương đối giữa các phần tử có cùng khóa so sánh. |
| **Hai thuật toán ổn định** | **3.0đ** | Liệt kê chính xác MergeSort và Insertion Sort (hoặc Bubble/Radix). |
| **Hai thuật toán không ổn định** | **3.0đ** | Liệt kê chính xác QuickSort và HeapSort (hoặc Selection Sort). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 6. [CSD_EQ06] Định nghĩa Hàm Băm Hoàn Hảo và Tiêu chuẩn của Hàm Băm Lý tưởng

- **Mã định danh (Question Code):** `CSD_EQ06`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Remember` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 49 — *Perfect Hash Functions*
- **Mục tiêu đánh giá:** Kiểm tra kiến thức ghi nhớ về hàm băm hoàn hảo và các tiêu chuẩn thiết kế hàm băm trong kỹ thuật phần mềm.

#### ❓ Câu hỏi vấn đáp:
> "Thế nào là một Hàm băm hoàn hảo (Perfect Hash Function)? Liệt kê 3 tiêu chuẩn bắt buộc của một hàm băm tốt trong thực tế."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Hàm băm mật mã học (như SHA-256) có những đặc tính kỹ thuật vượt trội nào so với các hàm băm thông thường dùng trong Hash Table (như MurmurHash hay polynomial hashing), và tại sao ta không dùng SHA-256 làm hàm băm mặc định cho Hash Table trong bộ nhớ RAM?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Định nghĩa Hàm băm hoàn hảo (Perfect Hash Function):
Một hàm băm h: K -> [0 .. M-1] được gọi là hàm băm hoàn hảo đối với một tập khóa tĩnh K cho trước nếu nó là một ánh xạ đơn ánh (injective mapping), nghĩa là KHÔNG CÓ BẤT KỲ XUNG ĐỘT NÀO xảy ra:
Với mọi k1, k2 thuộc K, nếu k1 != k2 thì h(k1) != h(k2).
(Nếu kích thước bảng băm M bằng đúng số lượng khóa |K|, nó được gọi là Hàm băm hoàn hảo tối thiểu - Minimal Perfect Hash Function).

2. Ba tiêu chuẩn bắt buộc của một hàm băm tốt trong thực tế:
- Tiêu chuẩn 1 - Phân phối đều (Uniform Distribution): Hàm băm phải phân bổ các khóa ngẫu nhiên và đồng đều trên toàn bộ không gian bảng băm [0 .. M-1], tối thiểu hóa số lượng va chạm.
- Tiêu chuẩn 2 - Tính toán nhanh (Fast Computation): Hàm băm phải có chi phí thời gian thực thi là O(1) và sử dụng các phép toán bit (bitwise, shift, XOR, multiplication) hiệu quả trên phần cứng.
- Tiêu chuẩn 3 - Tính tất định (Deterministic): Cùng một giá trị khóa đầu vào k bắt buộc phải luôn luôn sinh ra cùng một giá trị băm h(k) duy nhất trong suốt vòng đời của chương trình.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa Hàm băm hoàn hảo** | **4.0đ** | Nêu rõ tính chất đơn ánh không sinh xung đột h(k1) != h(k2) với k1 != k2. |
| **Tiêu chuẩn phân phối đều và tính toán nhanh** | **3.0đ** | Mô tả tiêu chuẩn Uniform Distribution và thời gian tính O(1). |
| **Tiêu chuẩn tính tất định** | **3.0đ** | Giải thích tính Deterministic của hàm băm đối với cùng một đầu vào. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 7. [CSD_EQ07] Phân tích Chi phí Thao tác Chèn/Xóa ở Đầu giữa Mảng và Singly Linked List

- **Mã định danh (Question Code):** `CSD_EQ07`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO1** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 1-2 — *Array vs Linked List Insertion Complexity*
- **Mục tiêu đánh giá:** Kiểm tra mức độ thông hiểu lý do cơ bản dẫn tới sự khác biệt về độ phức tạp khi chèn/xóa ở đầu danh sách.

#### ❓ Câu hỏi vấn đáp:
> "Tại sao thao tác chèn một phần tử vào vị trí đầu tiên của một Mảng động (Dynamic Array) lại tốn chi phí thời gian O(n), trong khi thao tác tương tự trên Danh sách liên kết đơn (Singly Linked List) chỉ tốn chi phí O(1)? Giải thích chi tiết cơ chế bộ nhớ bên dưới."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu sử dụng cấu trúc Mảng vòng (Circular Buffer / ArrayDeque) để duy trì mảng dữ liệu, thao tác chèn đầu có thể đạt được độ phức tạp thời gian O(1) không và cấu trúc này có điểm hạn chế gì so với Danh sách liên kết?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Đối với Mảng động (Dynamic Array):
- Cơ chế bộ nhớ: Mảng yêu cầu các phần tử phải nằm kề nhau trên một khối nhớ liên tục (contiguous memory block) và phần tử đầu tiên luôn gắn cố định tại chỉ số index = 0.
- Chi phí O(n): Khi muốn chèn một phần tử x vào vị trí đầu mảng (index 0), hệ thống bắt buộc phải giải phóng ô nhớ index 0. Để làm được điều đó, toàn bộ n phần tử hiện có trong mảng bắt buộc phải được sao chép và dịch chuyển lùi sang phải một vị trí (phần tử tại i chuyển sang i + 1, thực hiện từ cuối mảng lùi dần về đầu). Số phép dịch chuyển bằng đúng n, dẫn tới độ phức tạp thời gian O(n).

2. Đối với Danh sách liên kết đơn (Singly Linked List):
- Cơ chế bộ nhớ: Các nút (Nodes) được cấp phát rời rạc trên Heap, không yêu cầu liên tục. Vị trí đầu danh sách được quản lý độc quyền bởi con trỏ `head`.
- Chi phí O(1): Để chèn nút mới `newNode` vào đầu:
  1. `newNode.next = head;` (trỏ nút mới vào nút đầu hiện tại).
  2. `head = newNode;` (cập nhật con trỏ head sang nút mới).
  Chỉ có đúng hai phép gán tham chiếu hằng số được thực thi, hoàn toàn không phụ thuộc vào số lượng phần tử n hiện có trong danh sách. Do đó, thời gian luôn luôn là O(1).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Giải thích cơ chế dịch mảng O(n)** | **5.0đ** | Nêu rõ ràng yêu cầu bộ nhớ liên tục và thao tác dịch n phần tử sang phải. |
| **Giải thích cơ chế cập nhật con trỏ O(1)** | **5.0đ** | Trình bày chuẩn xác 2 bước gán con trỏ head và tính độc lập với kích thước n. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 8. [CSD_EQ08] Cơ chế Hoạt động của Hàng đợi Ưu tiên và Sự Khác biệt với Hàng đợi FIFO

- **Mã định danh (Question Code):** `CSD_EQ08`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 8 — *Priority Queue vs FIFO Queue*
- **Mục tiêu đánh giá:** Đánh giá sự thấu hiểu về nguyên tắc hoạt động dựa trên độ ưu tiên thay vì thời gian đến của Priority Queue.

#### ❓ Câu hỏi vấn đáp:
> "Trình bày cơ chế hoạt động của Hàng đợi ưu tiên (Priority Queue). Sự khác biệt cốt lõi giữa Hàng đợi ưu tiên và Hàng đợi thông thường (FIFO Queue) là gì? Nêu một tình huống thực tế minh họa rõ nét sự khác biệt này."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi cài đặt thuật toán Dijkstra tìm đường đi ngắn nhất, tại sao việc sử dụng Priority Queue (Min-Heap) lại giúp giảm độ phức tạp thời gian từ O(V^2) xuống O((V + E) log V) trên đồ thị thưa?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cơ chế hoạt động của Priority Queue:
Trong Hàng đợi ưu tiên, mỗi phần tử khi đưa vào cấu trúc dữ liệu đều được gắn kèm một mức độ ưu tiên (Priority Key). Thay vì lấy ra theo thứ tự thời gian ai đến trước được phục vụ trước, phần tử có MỨC ĐỘ ƯU TIÊN CAO NHẤT (hoặc giá trị khóa nhỏ nhất đối với Min-Priority Queue) sẽ luôn luôn được trích xuất ra phục vụ trước tiên, bất kể nó mới được thêm vào hay đã đợi từ lâu.

2. Sự khác biệt cốt lõi với FIFO Queue:
- FIFO Queue: Tiêu chí phục vụ duy nhất là THỜI GIAN (Arrival Time). Ai vào trước ra trước.
- Priority Queue: Tiêu chí phục vụ duy nhất là ĐỘ ƯU TIÊN (Priority Value). Phần tử đến sau nhưng có mức ưu tiên cao hơn sẽ vượt lên trước các phần tử đến trước.

3. Tình huống thực tế minh họa:
- Phòng Cấp cứu Bệnh viện (Hospital Emergency Room):
  Nếu sử dụng FIFO Queue: Bệnh nhân đau đầu nhẹ đến lúc 8:00 sẽ được khám trước bệnh nhân bị tai nạn giao thông nguy kịch đến lúc 8:05, điều này là phi lý và nguy hiểm.
  Thực tế bệnh viện sử dụng Priority Queue: Bệnh nhân cấp cứu nặng được gán mã ưu tiên đỏ (Priority 1) và lập tức được đưa vào phòng mổ ngay khi vừa tới viện, trong khi các ca nhẹ (Priority 4) tiếp tục chờ.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Cơ chế phân loại theo Priority Key** | **3.5đ** | Mô tả chính xác việc phục vụ theo mức độ ưu tiên thay vì thời gian. |
| **So sánh bản chất với FIFO Queue** | **3.5đ** | Phân biệt rõ ràng tiêu chí Thời gian (FIFO) vs Tiêu chí Độ ưu tiên (Priority). |
| **Tình huống thực tế minh họa** | **3.0đ** | Đưa ra ví dụ phòng cấp cứu bệnh viện hoặc xe cứu thương thuyết phục. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 9. [CSD_EQ09] Cơ chế Tràn Ngăn xếp Call Stack và Vai trò của Base Case trong Đệ quy

- **Mã định danh (Question Code):** `CSD_EQ09`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO3** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Easy`
- **Chủ đề bài học (Syllabus):** Session 11 — *Call Stack & StackOverflowError*
- **Mục tiêu đánh giá:** Kiểm tra mức độ hiểu sâu cơ chế hoạt động của vùng nhớ Call Stack và tầm quan trọng của điều kiện dừng.

#### ❓ Câu hỏi vấn đáp:
> "Hãy giải thích cơ chế bộ nhớ khi một hàm đệ quy được thực thi trên máy tính. Điều gì xảy ra bên trong Call Stack khi đệ quy rơi vào vòng lặp vô tận và tại sao điều kiện dừng (Base Case) được coi là thành phần sống còn?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Mỗi Stack Frame của một lời gọi hàm đệ quy trong ngôn ngữ Java/C lưu trữ những thông tin cụ thể nào, và kích thước vùng nhớ Call Stack tối đa thường do thành phần nào của hệ thống quy định?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cơ chế bộ nhớ Call Stack trong đệ quy:
Mỗi khi một hàm được gọi (kể cả hàm thông thường hay hàm đệ quy tự gọi chính nó), hệ thống sẽ cấp phát một vùng nhớ gọi là Khung ngăn xếp (Stack Frame / Activation Record) và đẩy (push) lên đỉnh vùng nhớ Call Stack.
Stack Frame này lưu trữ:
- Tham số truyền vào hàm.
- Các biến cục bộ của hàm.
- Địa chỉ hoàn trả (Return Address) để CPU biết quay lại dòng lệnh nào sau khi hàm con kết thúc.

2. Điều gì xảy ra khi đệ quy vô hạn:
Khi đệ quy bị lặp vô tận (do thiếu Base Case hoặc bước đệ quy không tiến dần về Base Case):
- Các Stack Frame mới liên tục được sinh ra và đẩy chồng chất lên Call Stack.
- Vì không có Base Case trả về kết quả, không có bất kỳ Stack Frame nào được giải phóng (`pop`).
- Vùng nhớ Call Stack của mỗi luồng (Thread) trong hệ điều hành/JVM có giới hạn dung lượng cố định (thường từ 1MB đến 2MB). Khi Call Stack bị lấp đầy hoàn toàn, nỗ lực đẩy thêm frame tiếp theo sẽ bị hệ điều hành từ chối và JVM ném ra lỗi nghiêm trọng `java.lang.StackOverflowError`.

3. Vai trò sống còn của Base Case:
Base Case đóng vai trò là 'điểm phanh dừng' duy nhất của giải thuật. Khi chạm Base Case, hàm con cuối cùng bắt đầu trả về kết quả, kích hoạt quá trình thu hồi (unwinding) các Stack Frame từ đỉnh ngăn xếp lùi dần về đáy, hoàn tất tính toán an toàn.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Cơ chế Stack Frame trên Call Stack** | **3.5đ** | Mô tả đầy đủ tham số, biến cục bộ và địa chỉ hoàn trả trong Activation Record. |
| **Hiện tượng StackOverflowError** | **3.5đ** | Giải thích cơ chế tích lũy không hồi kết vượt quá giới hạn 1-2MB của thread stack. |
| **Vai trò điểm dừng của Base Case** | **3.0đ** | Nêu rõ vai trò kích hoạt quá trình unwinding giải phóng các khung ngăn xếp. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 10. [CSD_EQ10] Hiện tượng Thoái hóa của Cây BST khi Chèn Dãy Dữ liệu Đã Sắp xếp

- **Mã định danh (Question Code):** `CSD_EQ10`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 19 — *BST Degeneration*
- **Mục tiêu đánh giá:** Đánh giá khả năng hiểu trường hợp xấu nhất của Cây BST thông thường và sự cần thiết của Cây cân bằng.

#### ❓ Câu hỏi vấn đáp:
> "Điều gì sẽ xảy ra với hình dạng của Cây tìm kiếm nhị phân (BST) nếu ta lần lượt chèn một dãy số đã được sắp xếp tăng dần: 10, 20, 30, 40, 50 vào một cây rỗng ban đầu? Hiện tượng này ảnh hưởng như thế nào đến độ phức tạp thời gian tìm kiếm?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi cây BST bị thoái hóa hoàn toàn thành danh sách liên kết với chiều cao n, các thao tác tìm kiếm, chèn và xóa đều suy thoái hiệu năng về O(n); các cấu trúc cây tìm kiếm tự cân bằng (Self-Balancing BST) ra đời nhằm duy trì bất biến chiều cao ở mức nào?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Hình dạng của Cây BST sau khi chèn dãy tăng dần:
Khi chèn dãy tăng dần `10, 20, 30, 40, 50`:
- Chèn 10: Nút gốc.
- Chèn 20: 20 > 10, nằm bên phải của 10.
- Chèn 30: 30 > 10, 30 > 20, nằm bên phải của 20.
- Chèn 40: Nằm bên phải của 30.
- Chèn 50: Nằm bên phải của 40.
Cây nhị phân hoàn toàn KHÔNG CÓ bất kỳ nút con bên trái nào. Cây bị thoái hóa hoàn toàn thành một đường thẳng lệch phải (Skewed Binary Tree / Degenerate Tree), có hình dạng và cấu trúc hoàn toàn tương đương với một Danh sách liên kết đơn (Singly Linked List).

2. Tác động đến độ phức tạp thời gian:
- Chiều cao của cây h tăng tuyến tính: h = n - 1 (với n là số phần tử).
- Độ phức tạp thời gian tìm kiếm, chèn, xóa trên BST phụ thuộc trực tiếp vào chiều cao h của cây:
  + Trong trường hợp cân bằng lý tưởng: h = O(log n), chi phí tìm kiếm đạt O(log n).
  + Trong trường hợp thoái hóa này: h = O(n), chi phí tìm kiếm bị suy biến nghiêm trọng về O(n) (phải duyệt tuần tự từ gốc 10 xuống 50).
- Kết luận: Cây BST thông thường không đảm bảo hiệu năng trong trường hợp dữ liệu có thứ tự sẵn. Đây là lý do thúc đẩy sự ra đời của các Cấu trúc Cây tự cân bằng như AVL Tree hay Red-Black Tree.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Mô tả hình dạng cây thoái hóa lệch phải** | **4.0đ** | Vẽ/mô tả chính xác cây biến thành đường thẳng lệch phải tương đương Singly Linked List. |
| **Phân tích chiều cao h = O(n)** | **3.0đ** | Chỉ ra chiều cao cây tăng từ O(log n) lên O(n). |
| **Đánh giá suy thoái độ phức tạp O(n)** | **3.0đ** | Khẳng định chi phí tìm kiếm bị suy biến về O(n) và nêu giải pháp Cây cân bằng. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 11. [CSD_EQ11] Công thức Tính Hệ số Cân bằng và Điều kiện Cân bằng của Cây AVL

- **Mã định danh (Question Code):** `CSD_EQ11`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 23 — *AVL Balance Factor Condition*
- **Mục tiêu đánh giá:** Kiểm tra mức độ thông hiểu điều kiện tự cân bằng của Cây AVL và ý nghĩa của hệ số cân bằng.

#### ❓ Câu hỏi vấn đáp:
> "Hệ số cân bằng (Balance Factor) của một nút trên Cây AVL được tính bằng công thức nào? Điều kiện cân bằng nghiêm ngặt của Cây AVL là gì và tại sao điều kiện này bảo đảm chiều cao của cây luôn duy trì ở mức O(log n)?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Số lượng nút tối thiểu N(h) của một cây AVL có chiều cao h liên hệ như thế nào với dãy số Fibonacci, và từ hệ thức truy hồi đó chứng minh trần chiều cao của Cây AVL luôn bị chặn ở mức h <= 1.44 log2(n) như thế nào?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Công thức tính Hệ số cân bằng (Balance Factor - BF):
Với một nút x bất kỳ trên cây:
`BF(x) = Height(Left_Subtree(x)) - Height(Right_Subtree(x))`
(Theo một số tài liệu có thể lấy Right - Left, nhưng nguyên lý toán học là tương đương). Quy ước chiều cao của cây rỗng là -1, nút lá là 0.

2. Điều kiện cân bằng nghiêm ngặt của Cây AVL:
Một cây BST được gọi là Cây AVL nếu với MỌI NÚT x trong cây, độ lệch chiều cao giữa hai cây con trái và phải không vượt quá 1, nghĩa là:
`BF(x) thuộc {-1, 0, 1}` (tức là `|BF(x)| <= 1`).
Ngay khi có một thao tác chèn hoặc xóa làm cho bất kỳ nút nào có `|BF(x)| >= 2`, cây bị coi là mất cân bằng và thuật toán lập tức thực hiện các phép xoay (Rotations) để khôi phục lại điều kiện này.

3. Vì sao chiều cao cây luôn duy trì O(log n):
Gọi N(h) là số lượng nút tối thiểu của một cây AVL có chiều cao h:
- N(0) = 1, N(1) = 2.
- Để đạt chiều cao h với số nút ít nhất, một cây con phải có chiều cao h - 1 và cây con kia có chiều cao h - 2:
  `N(h) = 1 + N(h - 1) + N(h - 2)`.
Phương trình này tương tự chuỗi Fibonacci, có tốc độ tăng trưởng lũy thừa: `N(h) > phi^h` (với phi xấp xỉ 1.618).
Lấy log hai vế: `h < 1.44 * log2(n)`.
Do đó, chiều cao của cây AVL luôn bị chặn trên chặt chẽ bởi O(log n), đảm bảo mọi thao tác tìm kiếm, chèn, xóa luôn hoàn thành trong thời gian O(log n).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Công thức tính Balance Factor** | **3.0đ** | Nêu đúng công thức BF = Height(Left) - Height(Right). |
| **Điều kiện cân bằng |BF| <= 1** | **3.0đ** | Khẳng định điều kiện nghiêm ngặt BF thuộc {-1, 0, 1} cho mọi nút. |
| **Chứng minh chiều cao h < 1.44 log n** | **4.0đ** | Sử dụng công thức cây Fibonacci tối thiểu N(h) = 1 + N(h-1) + N(h-2) để chứng minh O(log n). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 12. [CSD_EQ12] Phân biệt Bản chất Chiến lược Khám phá giữa Duyệt BFS và Duyệt DFS

- **Mã định danh (Question Code):** `CSD_EQ12`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000564`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 30 — *BFS vs DFS Exploration Strategy*
- **Mục tiêu đánh giá:** Đánh giá sự hiểu biết về sự khác biệt trong chiến lược lan tỏa và ứng dụng chuyên biệt của BFS và DFS.

#### ❓ Câu hỏi vấn đáp:
> "Hãy phân biệt chiến lược khám phá đồ thị giữa Thuật toán duyệt theo chiều rộng (BFS) và Thuật toán duyệt theo chiều sâu (DFS). Cấu trúc dữ liệu bổ trợ nào được sử dụng trong mỗi thuật toán và trong bài toán tìm đường đi ngắn nhất không trọng số, tại sao ta bắt buộc phải dùng BFS?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao thuật toán DFS KHÔNG thể đảm bảo tìm được đường đi ngắn nhất về số cạnh trên đồ thị không trọng số, và thuật toán DFS có thể được ứng dụng trong các bài toán nào khác của lý thuyết đồ thị (như kiểm tra chu trình hay sắp xếp topo)?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Chiến lược khám phá đồ thị:
- BFS (Breadth-First Search - Khám phá theo mức/chiều rộng):
  Xuất phát từ đỉnh nguồn, BFS khám phá đồ thị theo từng 'vòng sóng lan tỏa' (concentric waves). Thuật toán thăm toàn bộ các đỉnh kề trực tiếp cách nguồn 1 cạnh, sau đó mới thăm các đỉnh cách nguồn 2 cạnh, rồi đến 3 cạnh,... Không bao giờ đi sâu vào một nhánh nếu tầng hiện tại chưa được thăm hết.
- DFS (Depth-First Search - Khám phá theo chiều sâu):
  Xuất phát từ đỉnh nguồn, DFS đi thật sâu dọc theo một nhánh đường đi cho đến khi chạm ngõ cụt (đỉnh không còn cạnh chưa thăm), sau đó mới quay lui (backtrack) lại nút cha để thử rẽ sang nhánh tiếp theo.

2. Cấu trúc dữ liệu bổ trợ:
- BFS: Sử dụng Hàng đợi FIFO Queue (đảm bảo đỉnh nào được phát hiện trước sẽ được duyệt các đỉnh kề trước).
- DFS: Sử dụng Ngăn xếp LIFO Stack hoặc vùng nhớ Call Stack thông qua hàm đệ quy.

3. Vì sao tìm đường đi ngắn nhất không trọng số bắt buộc dùng BFS:
- Trong đồ thị không trọng số, độ dài đường đi được đo bằng số lượng cạnh.
- Do BFS duyệt tuần tự theo đúng khoảng cách k = 1, 2, 3,... cạnh từ nguồn, nên đỉnh đích D lần đầu tiên được lấy ra khỏi Queue chắc chắn thông qua đường đi có ÍT CẠNH NHẤT có thể.
- Ngược lại, DFS có thể lao đi rất sâu vào một nhánh vòng vèo và chạm tới đỉnh đích D với một con đường rất dài, trong khi ngay cạnh đỉnh nguồn có một con đường trực tiếp ngắn hơn.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân biệt chiến lược lan tỏa sóng vs đi sâu** | **3.5đ** | Mô tả chuẩn xác cơ chế duyệt theo từng tầng khoảng cách của BFS so với lao sâu quay lui của DFS. |
| **Cấu trúc dữ liệu bổ trợ Queue vs Stack** | **3.0đ** | Chỉ rõ vai trò của FIFO Queue cho BFS và LIFO Stack/Recursion cho DFS. |
| **Lý do BFS đảm bảo đường đi ngắn nhất** | **3.5đ** | Chứng minh BFS duyệt theo số cạnh tăng dần đảm bảo lần chạm đầu tiên là tối ưu. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 13. [CSD_EQ13] Nguyên lý Hoạt động Thời gian Tuyến tính của Thuật toán Radix Sort

- **Mã định danh (Question Code):** `CSD_EQ13`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO6** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 46 — *Radix Sort Linear Time*
- **Mục tiêu đánh giá:** Kiểm tra mức độ hiểu nguyên lý sắp xếp không so sánh và lý giải vì sao phá vỡ được giới hạn Omega(n log n).

#### ❓ Câu hỏi vấn đáp:
> "Thuật toán Radix Sort hoạt động theo nguyên lý nào? Tại sao Radix Sort có thể đạt được độ phức tạp thời gian tuyến tính O(d * (n + k)) mà không bị giới hạn bởi ranh giới dưới toán học Omega(n log n) của các thuật toán sắp xếp dựa trên so sánh?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao thuật toán sắp xếp con (Sub-routine) được sử dụng trong từng chữ số của Radix Sort (thường là Counting Sort) BẮT BUỘC phải có tính ổn định (Stable) thì thuật toán Radix Sort từ chữ số có trọng số thấp (LSD) mới cho ra kết quả sắp xếp chính xác?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Nguyên lý hoạt động của Radix Sort (Cơ số):
Radix Sort là một thuật toán sắp xếp không dựa trên so sánh (Non-comparison based). Thuật toán phân rã các khóa thành từng chữ số/ký tự (digits) và tiến hành sắp xếp từng chữ số một:
- Cách tiếp cận phổ biến nhất là LSD (Least Significant Digit): Sắp xếp từ chữ số hàng đơn vị, tiến dần lên hàng chục, hàng trăm,... cho đến chữ số có trọng số lớn nhất.
- Tại mỗi chữ số, Radix Sort bắt buộc sử dụng một thuật toán con có TÍNH ỔN ĐỊNH (Stable Sort) như Counting Sort để gom các phần tử vào các thùng (buckets từ 0 đến k - 1). Tính ổn định đảm bảo kết quả sắp xếp của các chữ số trước đó không bị phá vỡ.
- Độ phức tạp: O(d * (n + k)) với d là số chữ số tối đa, n là số phần tử, k là cơ số (ví dụ hệ thập phân k = 10).

2. Vì sao Radix Sort không bị giới hạn bởi ranh giới dưới Omega(n log n):
- Giới hạn toán học Omega(n log n) được chứng minh thông qua Mô hình Cây quyết định (Decision Tree Model), trong đó mọi thuật toán BẮT BUỘC phải thực hiện phép so sánh trực tiếp giữa hai phần tử (`a[i] < a[j]`). Với n phần tử, có n! hoán vị khả dĩ, cây quyết định phải có độ sâu tối thiểu là ceil(log2(n!)) ~ Omega(n log n).
- Radix Sort hoàn toàn KHÔNG THỰC HIỆN so sánh cặp giữa các phần tử! Thay vào đó, nó sử dụng giá trị số của từng chữ số làm chỉ số mảng trực tiếp (trực tiếp đưa vào bucket).
- Do không tuân theo mô hình so sánh cặp, Radix Sort phá vỡ định lý giới hạn biên dưới Omega(n log n) và đạt tốc độ tuyến tính khi số chữ số d là một hằng số nhỏ.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Nguyên lý phân rã chữ số LSD và Counting Sort** | **4.0đ** | Mô tả đúng quá trình duyệt từ hàng đơn vị lên hàng cao và vai trò của thuật toán con ổn định. |
| **Công thức độ phức tạp O(d*(n+k))** | **2.5đ** | Giải thích ý nghĩa các tham số d (chữ số), n (phần tử), k (cơ số). |
| **Chứng minh phá vỡ mô hình cây quyết định** | **3.5đ** | Lập luận sâu sắc mô hình Decision Tree Omega(n log n) chỉ áp dụng cho phép so sánh cặp. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 14. [CSD_EQ14] Nguyên lý Nén Run-Length Encoding và Các Trường hợp Gây Phình To Dữ liệu

- **Mã định danh (Question Code):** `CSD_EQ14`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO8** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Understand` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 54 — *Run-Length Encoding (RLE)*
- **Mục tiêu đánh giá:** Đánh giá khả năng hiểu thuật toán nén RLE và nhận diện trường hợp suy thoái làm tăng dung lượng.

#### ❓ Câu hỏi vấn đáp:
> "Thuật toán nén Run-Length Encoding (RLE) hoạt động dựa trên nguyên lý nào? Hãy cho một ví dụ dữ liệu mà RLE nén rất tốt và một ví dụ dữ liệu đầu vào khiến RLE làm tăng kích thước tệp (phình to dữ liệu) thay vì nén nhỏ."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu tệp dữ liệu đầu vào là chuỗi các ký tự xen kẽ không lặp lại: 'ABCDEFGH...', thuật toán RLE dạng đơn giản sẽ làm kích thước tệp tăng lên bao nhiêu phần trăm và giải pháp cải tiến cờ đánh dấu (Flag/Escape byte) khắc phục hiện tượng này như thế nào?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Nguyên lý hoạt động của Run-Length Encoding (RLE):
RLE là thuật toán nén dữ liệu không tổn hao (Lossless) hoạt động dựa trên việc tìm kiếm các đoạn dữ liệu lặp lại liên tiếp (runs).
Thuật toán thay thế mỗi chuỗi các ký tự giống nhau lặp lại liên tiếp bằng một cặp gồm: [Độ dài đoạn lặp (Count), Ký tự lặp (Character)].

2. Ví dụ trường hợp RLE nén cực tốt:
- Dữ liệu đầu vào: `"AAAAAAAABBBBBCCCCCCCCCCCC"` (25 ký tự).
- Kết quả sau khi nén: `"8A5B12C"` (7 ký tự).
- Đánh giá: Dung lượng giảm từ 25 byte xuống còn 7 byte, tỷ lệ nén đạt gần 72%. RLE cực kỳ hiệu quả với các hình ảnh hoạt hình đơn sắc, ảnh đen trắng (nhiều pixel trắng/đen liên tiếp), hoặc biểu tượng icon.

3. Ví dụ trường hợp RLE làm phình to dữ liệu:
- Dữ liệu đầu vào: `"ABCDEFGHIJ"` (10 ký tự khác nhau hoàn toàn, không có ký tự nào lặp lại liên tiếp).
- Kết quả sau khi nén: `"1A1B1C1D1E1F1G1H1I1J"` (20 ký tự).
- Đánh giá: Kích thước dữ liệu sau nén tăng gấp đôi từ 10 byte lên 20 byte (phình to 100%)!
Lý do: Khi dữ liệu không có tính lặp lại (dữ liệu ngẫu nhiên hoặc văn bản tự nhiên), việc thêm số lượng đếm '1' vào trước mỗi ký tự đã vô tình nhân đôi dung lượng lưu trữ.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Nguyên lý thay thế chuỗi lặp Count-Character** | **3.5đ** | Trình bày chính xác cơ chế gom nhóm các ký tự lặp liên tiếp. |
| **Ví dụ nén tốt** | **3.0đ** | Đưa ra ví dụ chuỗi lặp dài và tính toán tỷ lệ giảm kích thước thuyết phục. |
| **Ví dụ phình to dữ liệu** | **3.5đ** | Phân tích chuỗi không lặp dẫn đến kích thước tăng gấp đôi và giải thích nguyên nhân. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 15. [CSD_EQ15] Cài đặt Thao tác Chèn Nút Mới vào Sau một Nút trong Doubly Linked List

- **Mã định danh (Question Code):** `CSD_EQ15`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO1** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 2 — *Doubly Linked List Insertion*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng thao tác con trỏ kép khi chèn một nút mới vào danh sách liên kết đôi mà không làm đứt liên kết.

#### ❓ Câu hỏi vấn đáp:
> "Cho một nút `prevNode` đã tồn tại trong Danh sách liên kết đôi (Doubly Linked List). Hãy viết phương thức Java `insertAfter(Node prevNode, int newData)` để chèn một nút mới có giá trị `newData` vào ngay sau `prevNode`. Trình bày đúng thứ tự 4 bước gán con trỏ để không làm mất liên kết."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trong phương thức `insertAfter(Node prevNode, int newData)` của Doubly Linked List, nếu `prevNode` chính là nút đuôi (Tail) của danh sách thì bước gán con trỏ nào cần phải được kiểm tra điều kiện rẽ nhánh để tránh ném lỗi NullPointerException?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Định nghĩa cấu trúc nút:
```java
public class Node {
    int data;
    Node prev;
    Node next;
    Node(int data) { this.data = data; }
}
```

1. Thứ tự 4 bước gán con trỏ bắt buộc:
- Bước 1: Khởi tạo nút mới: `Node newNode = new Node(newData);`
- Bước 2: Thiết lập liên kết tiến của nút mới trỏ tới nút đứng sau prevNode:
  `newNode.next = prevNode.next;`
- Bước 3: Thiết lập liên kết lùi của nút mới trỏ về prevNode:
  `newNode.prev = prevNode;`
- Bước 4: Cập nhật liên kết tiến của prevNode trỏ tới newNode:
  `prevNode.next = newNode;`
- Bước 5 (Xử lý biên): Nếu newNode không phải là nút cuối cùng (tức là `newNode.next != null`), ta phải cập nhật liên kết lùi của nút đứng sau trỏ về newNode:
  `newNode.next.prev = newNode;`

2. Cài đặt hoàn chỉnh bằng Java:
```java
public void insertAfter(Node prevNode, int newData) {
    if (prevNode == null) {
        throw new IllegalArgumentException("Nút liền trước không được phép null");
    }
    Node newNode = new Node(newData);
    newNode.next = prevNode.next;
    newNode.prev = prevNode;
    prevNode.next = newNode;
    if (newNode.next != null) {
        newNode.next.prev = newNode;
    }
}
```
Độ phức tạp thời gian: O(1) vì chỉ thực hiện các phép gán con trỏ trực tiếp.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Kiểm tra tiền điều kiện prevNode != null** | **2.0đ** | Bắt ngoại lệ IllegalArgumentException khi prevNode null. |
| **Trình bày đúng thứ tự gán con trỏ** | **4.0đ** | Gán đúng newNode.next, newNode.prev trước khi cập nhật prevNode.next để không đứt chuỗi. |
| **Xử lý nút kế tiếp newNode.next.prev** | **4.0đ** | Kiểm tra điều kiện newNode.next != null trước khi gán liên kết lùi. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 16. [CSD_EQ16] Ứng dụng Thuật toán Shunting-Yard Chuyển đổi Biểu thức Infix sang Postfix

- **Mã định danh (Question Code):** `CSD_EQ16`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO2** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 6 — *Shunting-Yard Infix to Postfix*
- **Mục tiêu đánh giá:** Kiểm tra khả năng vận dụng Stack để thực thi giải thuật Shunting-Yard của Dijkstra chuyển đổi biểu thức toán học.

#### ❓ Câu hỏi vấn đáp:
> "Hãy trình bày thuật toán sử dụng Stack để chuyển đổi biểu thức toán học từ dạng trung tố (Infix) sang dạng hậu tố (Postfix - RPN). Áp dụng thuật toán từng bước trên biểu thức: `A + B * (C - D)` và chỉ ra trạng thái của Stack cùng chuỗi kết quả Output sau mỗi ký tự."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi gặp dấu đóng ngoặc ')' trong thuật toán Shunting-Yard, tại sao ta phải pop liên tiếp các toán tử khỏi Stack cho đến khi gặp dấu mở ngoặc '(', và hai dấu ngoặc này có được đưa vào chuỗi kết quả Postfix cuối cùng không?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Nguyên lý thuật toán Shunting-Yard:
Quy tắc độ ưu tiên toán tử: `*`, `/` (ưu tiên 2) > `+`, `-` (ưu tiên 1).
Duyệt từng ký hiệu trong biểu thức từ trái sang phải:
- Nếu là Toán hạng (A, B, C...): Đưa thẳng vào chuỗi kết quả Output.
- Nếu là Dấu mở ngoặc `(`: Đẩy (push) vào Stack.
- Nếu là Dấu đóng ngoặc `)`: Liên tục lấy toán tử từ Stack (pop) đưa vào Output cho đến khi gặp dấu `(`, sau đó loại bỏ `(` khỏi Stack mà không đưa vào Output.
- Nếu là Toán tử (+, -, *, /): Khi Stack chưa rỗng và toán tử ở đỉnh Stack có độ ưu tiên >= toán tử hiện tại (với toán tử kết hợp trái), pop toán tử đỉnh đưa vào Output. Sau đó push toán tử hiện tại vào Stack.
- Sau khi duyệt hết chuỗi: Pop toàn bộ các toán tử còn lại trong Stack đưa vào Output.

2. Áp dụng từng bước trên biểu thức `A + B * (C - D)`:

| Ký hiệu | Hành động xử lý | Trạng thái Stack (đỉnh bên phải) | Chuỗi Output |
|---|---|---|---|
| A | Toán hạng -> Output | [ ] | A |
| + | Toán tử -> Push | [ + ] | A |
| B | Toán hạng -> Output | [ + ] | A B |
| * | Toán tử (ưu tiên cao hơn +) -> Push | [ +, * ] | A B |
| ( | Mở ngoặc -> Push | [ +, *, ( ] | A B |
| C | Toán hạng -> Output | [ +, *, ( ] | A B C |
| - | Toán tử (sau mở ngoặc) -> Push | [ +, *, (, - ] | A B C |
| D | Toán hạng -> Output | [ +, *, (, - ] | A B C D |
| ) | Đóng ngoặc -> Pop đến `(` | [ +, * ] | A B C D - |
| Hết chuỗi | Pop toàn bộ Stack | [ ] | A B C D - * + |

Kết quả biểu thức Postfix cuối cùng: `A B C D - * +`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Quy tắc xử lý toán tử và dấu ngoặc** | **3.5đ** | Trình bày đủ 4 quy tắc: toán hạng ra output, mở ngoặc push, đóng ngoặc xả, so sánh độ ưu tiên. |
| **Bảng mô phỏng từng bước chính xác** | **4.5đ** | Mô phỏng đúng 100% từng bước biến đổi trạng thái Stack và Output qua từng ký tự. |
| **Kết quả biểu thức Postfix chuẩn xác** | **2.0đ** | Đưa ra đáp án đúng: A B C D - * +. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 17. [CSD_EQ17] Các Bước Tìm Nút Thế thân và Cập nhật Con trỏ khi Xóa Nút 2 Con trên BST

- **Mã định danh (Question Code):** `CSD_EQ17`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 20 — *BST Node Deletion Mechanism*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng áp dụng thuật toán xóa nút có 2 con trên cây BST và chứng minh tính bảo toàn tính chất thứ tự.

#### ❓ Câu hỏi vấn đáp:
> "Cho một cây tìm kiếm nhị phân (BST) với nút gốc có giá trị 50, nhánh con trái có nút 30 và nhánh con phải có cây con chứa các giá trị {70, 60, 80, 65}. Hãy mô tả chi tiết các bước xóa nút 50 bằng cách sử dụng phần tử thế thân nhỏ nhất bên phải (In-order Successor). Vẽ cấu trúc cây trước và sau khi xóa."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Trong bài toán xóa nút 50 trên BST bằng In-order Successor từ cây con phải {70, 60, 80, 65}, giá trị của nút thế thân là bao nhiêu và nút cha của nó (nút 70) phải cập nhật con trỏ bên trái thành giá trị nào sau khi trích xuất nút thế thân?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Cấu trúc cây ban đầu trước khi xóa:
```
         50 (cần xóa)
       /    \
     30      70
            /  \
          60    80
            \
             65
```

2. Các bước thực hiện xóa nút 50:
- Bước 1: Nhận diện nút 50 có đủ 2 con (con trái là 30, con phải là 70).
- Bước 2: Tìm nút thế thân In-order Successor:
  Đi sang cây con phải (gốc 70), sau đó đi sang trái liên tục tới nút tận cùng: từ 70 sang trái gặp 60. Tại nút 60, con trái bằng null. Vậy Nút thế thân là 60 (giá trị nhỏ nhất bên cây con phải).
- Bước 3: Sao chép giá trị của nút thế thân vào nút cần xóa: `root.val = 60`.
- Bước 4: Xóa nút 60 khỏi vị trí cũ trong cây con phải:
  Quan sát nút 60 chỉ có 1 con bên phải là nút 65 (`60.right = 65`). Ta nối trực tiếp nút con trái của 70 trỏ vào 65 (`70.left = 65`).

3. Cấu trúc cây sau khi xóa nút 50:
```
         60 (gốc mới)
       /    \
     30      70
            /  \
          65    80
```
Tính chất BST được bảo toàn hoàn hảo: 30 < 60 < {65, 70, 80}.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Xác định chính xác In-order Successor** | **3.0đ** | Tìm đúng nút thế thân là 60 (nhỏ nhất bên cây con phải). |
| **Các bước cập nhật con trỏ** | **4.0đ** | Mô tả sao chép giá trị 60 lên gốc và nối 70.left vào 65. |
| **Sơ đồ cây trước và sau khi xóa** | **3.0đ** | Vẽ đúng hình dạng cấu trúc cây trước và sau, khẳng định tính chất BST bảo toàn. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 18. [CSD_EQ18] Quy trình Xây dựng Max-Heap từ Mảng và Thao tác Xóa Phần tử Lớn nhất

- **Mã định danh (Question Code):** `CSD_EQ18`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 24 — *Max-Heap Construction and Extract-Max*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng xây dựng Heap từ mảng và thực hiện thao tác xóa phần tử gốc kèm Sift-Down.

#### ❓ Câu hỏi vấn đáp:
> "Cho mảng số nguyên ban đầu: `[4, 10, 3, 5, 1]`. Hãy trình bày từng bước biến đổi mảng này thành Max-Heap theo phương pháp Bottom-up (Build-Heap). Sau đó, thực hiện thao tác xóa phần tử lớn nhất (`extractMax()`) và mô tả quá trình vun đống xuống (Sift-Down) để khôi phục cấu trúc heap."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại sao trong thuật toán xây dựng Max-Heap từ dưới lên (Bottom-up Build-Heap), ta lại bắt đầu vòng lặp duyệt từ nút có chỉ số `(n/2) - 1` lùi dần về 0 thay vì duyệt từ các nút lá ở cuối mảng?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Giai đoạn Xây dựng Max-Heap (Bottom-up Build-Heap) từ mảng `[4, 10, 3, 5, 1]`:
Kích thước n = 5. Nút không phải lá cuối cùng có chỉ số: `(n / 2) - 1 = (5 / 2) - 1 = 1` (phần tử `a[1] = 10`).
- Xét chỉ số i = 1 (giá trị 10):
  Con trái: `2*1 + 1 = 3` (giá trị 5). Con phải: `2*1 + 2 = 4` (giá trị 1).
  Vì 10 > 5 và 10 > 1, nút 10 đã thỏa mãn Max-Heap. Mảng không đổi: `[4, 10, 3, 5, 1]`.
- Xét chỉ số i = 0 (giá trị 4):
  Con trái: `2*0 + 1 = 1` (giá trị 10). Con phải: `2*0 + 2 = 2` (giá trị 3).
  Con lớn nhất là 10 (tại chỉ số 1) > 4. Hoán vị `a[0]` và `a[1]`:
  Mảng trở thành: `[10, 4, 3, 5, 1]`.
  Tiếp tục siftDown giá trị 4 tại chỉ số 1: Con trái của 1 là chỉ số 3 (giá trị 5) > 4. Hoán vị `a[1]` và `a[3]`:
  Mảng Max-Heap hoàn thiện: `[10, 5, 3, 4, 1]`.

2. Thao tác Xóa phần tử lớn nhất (`extractMax()`):
- Bước 1: Trích xuất phần tử gốc lớn nhất `max = a[0] = 10`.
- Bước 2: Đưa phần tử cuối cùng của mảng là `a[4] = 1` lên thay thế vị trí gốc 0. Giảm kích thước heap: `size = 4`.
  Mảng tạm thời: `[1, 5, 3, 4]`.
- Bước 3: Thực hiện `siftDown(0)` trên giá trị 1:
  Nút tại chỉ số 0 (giá trị 1) có con trái tại 1 (giá trị 5) và con phải tại 2 (giá trị 3).
  Con lớn nhất là 5 (chỉ số 1) > 1. Hoán vị `a[0]` và `a[1]`:
  Mảng thành: `[5, 1, 3, 4]`.
  Tiếp tục kiểm tra tại chỉ số 1: có con trái tại 3 (giá trị 4) > 1. Hoán vị `a[1]` và `a[3]`:
  Mảng Max-Heap sau khi xóa phần tử lớn nhất: `[5, 4, 3, 1]`.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Quy trình Build-Heap Bottom-up** | **4.0đ** | Bắt đầu từ nút i = 1, duyệt lùi về 0, siftDown chính xác tạo mảng [10, 5, 3, 4, 1]. |
| **Thao tác đưa phần tử cuối lên gốc** | **2.5đ** | Lấy max = 10, đưa 1 lên gốc và giảm kích thước heap thành 4. |
| **Quá trình Sift-Down khôi phục heap** | **3.5đ** | Mô tả chuẩn xác 2 lượt hoán vị đưa 1 xuống đáy và thu được [5, 4, 3, 1]. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 19. [CSD_EQ19] Thực thi Từng Bước Thuật toán Dijkstra trên Đồ thị Có Trọng số

- **Mã định danh (Question Code):** `CSD_EQ19`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000564`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 34 — *Dijkstra Algorithm Execution*
- **Mục tiêu đánh giá:** Đánh giá kỹ năng vận dụng thuật toán Dijkstra từng bước và cập nhật bảng khoảng cách khoảng cách tối ưu.

#### ❓ Câu hỏi vấn đáp:
> "Cho đồ thị có hướng có trọng số gồm 4 đỉnh {A, B, C, D} với các cạnh: (A->B, trọng số 2), (A->C, 5), (B->C, 1), (B->D, 7), (C->D, 3). Hãy thực thi thuật toán Dijkstra từng bước xuất phát từ đỉnh nguồn A để tìm khoảng cách ngắn nhất tới tất cả các đỉnh còn lại. Lập bảng mô phỏng chi tiết từng lượt lặp."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Tại thời điểm một đỉnh v được chốt nhãn (Finalized / Visited) trong thuật toán Dijkstra, tính chất toán học nào của thuật toán đảm bảo khoảng cách d[v] đã là đường đi ngắn nhất tuyệt đối và không bao giờ bị cập nhật giảm xuống nữa trên đồ thị không có cạnh âm?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Khởi tạo thuật toán Dijkstra với nguồn S = A:
- Mảng khoảng cách: `dist[A] = 0`, `dist[B] = infinity`, `dist[C] = infinity`, `dist[D] = infinity`.
- Tập đỉnh đã hoàn tất chốt kết quả: `Visited = {}`.

2. Bảng mô phỏng từng bước lặp thuật toán:

| Lượt lặp | Đỉnh chọn (min dist chưa thăm) | Thao tác Relaxation (Cập nhật khoảng cách cạnh kề) | Mảng dist sau lượt [A, B, C, D] | Visited |
|---|---|---|---|---|
| Khởi tạo | - | - | [0, inf, inf, inf] | {} |
| Lượt 1 | **A** (dist = 0) | Cạnh A->B: dist[B] = min(inf, 0 + 2) = 2<br/>Cạnh A->C: dist[C] = min(inf, 0 + 5) = 5 | [0, **2**, 5, inf] | {A} |
| Lượt 2 | **B** (dist = 2) | Cạnh B->C: dist[C] = min(5, 2 + 1) = 3 (giảm từ 5 xuống 3!)<br/>Cạnh B->D: dist[D] = min(inf, 2 + 7) = 9 | [0, 2, **3**, 9] | {A, B} |
| Lượt 3 | **C** (dist = 3) | Cạnh C->D: dist[D] = min(9, 3 + 3) = 6 (giảm từ 9 xuống 6!) | [0, 2, 3, **6**] | {A, B, C} |
| Lượt 4 | **D** (dist = 6) | Đỉnh D không có cạnh đi ra | [0, 2, 3, 6] | {A, B, C, D} |

3. Kết quả khoảng cách ngắn nhất từ nguồn A:
- A -> A: 0
- A -> B: 2 (đường đi: A -> B)
- A -> C: 3 (đường đi: A -> B -> C, tối ưu hơn đường trực tiếp A -> C có trọng số 5)
- A -> D: 6 (đường đi: A -> B -> C -> D, tối ưu hơn đường qua B->D có trọng số 9).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Khởi tạo khoảng cách và đỉnh nguồn** | **2.0đ** | Đặt đúng dist[A] = 0 và các đỉnh còn lại bằng infinity. |
| **Bảng mô phỏng 4 lượt lặp chính xác** | **5.0đ** | Thể hiện chính xác việc chọn đỉnh min và 2 lần giảm khoảng cách relaxation tại C (từ 5 xuống 3) và D (từ 9 xuống 6). |
| **Kết luận đường đi và khoảng cách tối ưu** | **3.0đ** | Đưa ra đáp án chuẩn xác: dist(B)=2, dist(C)=3, dist(D)=6 kèm vết đường đi. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 20. [CSD_EQ20] Quy trình Phân hoạch Hoare Partition trong QuickSort

- **Mã định danh (Question Code):** `CSD_EQ20`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO6** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 42 — *Hoare Partition Procedure*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng cài đặt và theo dõi trạng thái con trỏ hai đầu trong thuật toán phân hoạch Hoare.

#### ❓ Câu hỏi vấn đáp:
> "Hãy trình bày thuật toán phân hoạch Hoare (Hoare Partition) trong QuickSort khi chọn phần tử ở giữa `arr[(low + high) / 2]` làm Pivot. Mô phỏng từng bước chạy của thuật toán trên mảng: `[5, 2, 9, 3, 7, 6, 1]` và cho biết trạng thái của mảng cùng vị trí phân hoạch sau khi hoàn tất."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"So với phân hoạch Lomuto, phân hoạch Hoare thực hiện ít hơn trung bình bao nhiêu lần phép hoán đổi (swap) và tại sao phần tử Pivot trong phân hoạch Hoare không nhất thiết nằm ở vị trí phân hoạch cuối cùng sau khi hoàn tất?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Nguyên lý thuật toán phân hoạch Hoare:
- Chọn pivot: `pivot = arr[(low + high) / 2]`.
- Sử dụng hai con trỏ: `i` chạy từ trái sang (`low - 1`) và `j` chạy từ phải sang (`high + 1`).
- Trong vòng lặp vô hạn:
  + Tăng `i` liên tục chừng nào `arr[i] < pivot`.
  + Giảm `j` liên tục chừng nào `arr[j] > pivot`.
  + Nếu `i >= j`, thuật toán kết thúc và trả về chỉ số `j` làm ranh giới phân hoạch.
  + Ngược lại (`i < j`), hoán vị `arr[i]` với `arr[j]`.

2. Mô phỏng trên mảng `[5, 2, 9, 3, 7, 6, 1]` (low = 0, high = 6):
- Chỉ số giữa: `(0 + 6) / 2 = 3` -> `pivot = arr[3] = 3`.
- Khởi tạo con trỏ: `i = -1`, `j = 7`.

- Vòng 1:
  + Tiến `i`: `i` dừng tại 0 vì `arr[0] = 5 >= 3`.
  + Lùi `j`: `j` lùi qua 6 (giá trị 1 < 3) -> dừng tại 6 vì `arr[6] = 1 <= 3`.
  + Vì `i = 0 < j = 6`, hoán vị `arr[0]` và `arr[6]`.
  + Mảng trở thành: `[1, 2, 9, 3, 7, 6, 5]`.

- Vòng 2:
  + Tiến `i`: `arr[1] = 2 < 3` (tiếp tục), `arr[2] = 9 >= 3` -> dừng tại `i = 2`.
  + Lùi `j`: `arr[6]=5 > 3`, `arr[5]=6 > 3`, `arr[4]=7 > 3`, `arr[3]=3 <= 3` -> dừng tại `j = 3`.
  + Vì `i = 2 < j = 3`, hoán vị `arr[2]` và `arr[3]`.
  + Mảng trở thành: `[1, 2, 3, 9, 7, 6, 5]`.

- Vòng 3:
  + Tiến `i`: dừng tại `i = 3` vì `arr[3] = 9 >= 3`.
  + Lùi `j`: `arr[3]=9 > 3`, `arr[2]=3 <= 3` -> dừng tại `j = 2`.
  + Kiểm tra: `i = 3 >= j = 2` -> Hai con trỏ đã gặp và vượt qua nhau!
  + Dừng thuật toán, trả về ranh giới phân hoạch `j = 2`.

3. Kết quả:
Mảng sau phân hoạch: `[1, 2, 3, 9, 7, 6, 5]`.
Phân vùng trái `[0 .. 2]` là `[1, 2, 3]` (toàn bộ <= pivot 3).
Phân vùng phải `[3 .. 6]` là `[9, 7, 6, 5]` (toàn bộ >= pivot 3).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Nguyên lý hai con trỏ i và j của Hoare** | **3.0đ** | Nêu đúng cơ chế con trỏ chạy ngược chiều vào giữa và điều kiện dừng i >= j. |
| **Mô phỏng chi tiết 3 vòng lặp** | **4.5đ** | Ghi nhận đúng từng vị trí dừng của i và j, các bước hoán vị và giá trị mảng qua từng vòng. |
| **Kết quả ranh giới phân hoạch j = 2** | **2.5đ** | Chỉ ra mảng chia làm 2 nửa [1, 2, 3] và [9, 7, 6, 5] chính xác. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 21. [CSD_EQ21] Thực hiện Chèn Dãy Khóa vào Bảng băm theo Linear và Quadratic Probing

- **Mã định danh (Question Code):** `CSD_EQ21`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO7** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Apply` | **Mức độ khó:** `Medium`
- **Chủ đề bài học (Syllabus):** Session 50 — *Linear Probing vs Quadratic Probing Execution*
- **Mục tiêu đánh giá:** Kiểm tra kỹ năng tính toán chỉ số băm và vận dụng công thức dò tuyến tính và dò bậc hai khi xử lý va chạm.

#### ❓ Câu hỏi vấn đáp:
> "Cho bảng băm có kích thước M = 7 với hàm băm `h(k) = k mod 7`. Hãy mô phỏng từng bước chèn dãy khóa sau vào bảng băm: `[10, 17, 24, 38]` bằng hai phương pháp: (1) Dò tuyến tính (Linear Probing), và (2) Dò bậc hai (Quadratic Probing với c1 = 0, c2 = 1: `h(k, i) = (h(k) + i^2) mod 7`)."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Hiện tượng phân cụm thứ cấp (Secondary Clustering) trong Quadratic Probing xảy ra khi nào và tại sao nó vẫn có thể phát sinh ngay cả khi hai khóa ban đầu khác nhau nếu chúng có cùng giá trị băm ban đầu `h(k1) == h(k2)`?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Phương pháp 1: Dò tuyến tính (Linear Probing: `h(k, i) = (h(k) + i) mod 7`):
Khởi tạo bảng băm M = 7 ô rỗng: `[-, -, -, -, -, -, -]`.
- Chèn khóa 10: `h(10) = 10 mod 7 = 3`. Ô 3 trống -> Gán `table[3] = 10`.
- Chèn khóa 17: `h(17) = 17 mod 7 = 3`. Xung đột tại ô 3!
  Dò i = 1: `(3 + 1) mod 7 = 4`. Ô 4 trống -> Gán `table[4] = 17`.
- Chèn khóa 24: `h(24) = 24 mod 7 = 3`. Xung đột tại ô 3!
  Dò i = 1: ô 4 đã có 17 (xung đột tiếp).
  Dò i = 2: `(3 + 2) mod 7 = 5`. Ô 5 trống -> Gán `table[5] = 24`.
- Chèn khóa 38: `h(38) = 38 mod 7 = 3`. Xung đột tại ô 3!
  Dò i = 1: ô 4 có 17.
  Dò i = 2: ô 5 có 24.
  Dò i = 3: `(3 + 3) mod 7 = 6`. Ô 6 trống -> Gán `table[6] = 38`.
Kết quả bảng băm sau Linear Probing:
`[0: -, 1: -, 2: -, 3: 10, 4: 17, 5: 24, 6: 38]` (hình thành cụm chính liên tục từ 3 đến 6).

2. Phương pháp 2: Dò bậc hai (Quadratic Probing: `h(k, i) = (h(k) + i^2) mod 7`):
Khởi tạo bảng băm M = 7 ô rỗng: `[-, -, -, -, -, -, -]`.
- Chèn khóa 10: `h(10) = 3`. Ô 3 trống -> Gán `table[3] = 10`.
- Chèn khóa 17: `h(17) = 3`. Xung đột tại 3!
  Dò i = 1: `(3 + 1^2) mod 7 = 4 mod 7 = 4`. Ô 4 trống -> Gán `table[4] = 17`.
- Chèn khóa 24: `h(24) = 3`. Xung đột tại 3!
  Dò i = 1: `(3 + 1) mod 7 = 4` (đã có 17).
  Dò i = 2: `(3 + 2^2) mod 7 = (3 + 4) mod 7 = 7 mod 7 = 0`. Ô 0 trống -> Gán `table[0] = 24`.
- Chèn khóa 38: `h(38) = 3`. Xung đột tại 3!
  Dò i = 1: ô 4 có 17.
  Dò i = 2: ô 0 có 24.
  Dò i = 3: `(3 + 3^2) mod 7 = (3 + 9) mod 7 = 12 mod 7 = 5`. Ô 5 trống -> Gán `table[5] = 38`.
Kết quả bảng băm sau Quadratic Probing:
`[0: 24, 1: -, 2: -, 3: 10, 4: 17, 5: 38, 6: -]` (các phần tử được phân tán rộng, triệt tiêu hiện tượng cụm chính).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Mô phỏng Linear Probing** | **5.0đ** | Tính đúng chỉ số băm cơ sở 3, các bước dò +1, +2, +3 và thu được bảng [-, -, -, 10, 17, 24, 38]. |
| **Mô phỏng Quadratic Probing** | **5.0đ** | Tính đúng công thức bước nhảy +1, +4, +9 và phân tán chính xác vào các ô 3, 4, 0, 5. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 22. [CSD_EQ22] Phân tích Ba Phương pháp Cài đặt Tính Số Fibonacci: Đệ quy vs DP

- **Mã định danh (Question Code):** `CSD_EQ22`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO3** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 12 — *Fibonacci Algorithm Comparison*
- **Mục tiêu đánh giá:** Đánh giá năng lực phân tích độ phức tạp thời gian/không gian của 3 cách tiếp cận bài toán Fibonacci và cơ chế tối ưu bộ nhớ.

#### ❓ Câu hỏi vấn đáp:
> "Hãy phân tích và so sánh 3 phương pháp giải bài toán tính số Fibonacci thứ n: (1) Đệ quy ngây thơ (Naive Recursion), (2) Đệ quy có nhớ (Top-down Memoization), và (3) Quy hoạch động lặp (Bottom-up Iterative DP) về độ phức tạp thời gian và không gian. Tại sao phương pháp lặp có thể tối ưu không gian xuống O(1)?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Ngoài 3 phương pháp đệ quy, memoization và quy hoạch động lặp, phương pháp Lũy thừa ma trận (Matrix Exponentiation) có thể tính số Fibonacci thứ n với độ phức tạp thời gian O(log n) dựa trên đẳng thức ma trận nào?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
Bảng so sánh 3 phương pháp tính số Fibonacci thứ n:

| Phương pháp | Độ phức tạp Thời gian | Độ phức tạp Không gian | Điểm nghẽn / Đặc tính kỹ thuật |
|---|---|---|---|
| 1. Đệ quy ngây thơ (Naive Recursion) | O(2^n) (chính xác xấp xỉ 1.618^n) | O(n) (chiều sâu Call Stack) | Tính toán lặp lại trùng lặp theo cấp số nhân (Overlapping Subproblems), treo máy khi n >= 50. |
| 2. Đệ quy có nhớ (Memoization DP) | O(n) | O(n) (Mảng cache + Call Stack) | Dùng mảng hoặc HashMap lưu lại kết quả đã tính. Mỗi bài toán con chỉ tính đúng 1 lần. Vẫn tốn stack frame. |
| 3. Quy hoạch động lặp (Iterative DP) | O(n) | O(1) (tối ưu 2 biến) | Duyệt vòng lặp từ 2 lên n, hoàn toàn không đệ quy, không dùng Call Stack. |

Phân tích lý do Quy hoạch động lặp tối ưu không gian xuống O(1):
- Công thức truy hồi: `fib(n) = fib(n - 1) + fib(n - 2)`.
- Quan sát bản chất quan hệ phụ thuộc dữ liệu: Để tính toán giá trị `fib(i)` tại bước hiện tại, thuật toán CHỈ CẦN DUY NHẤT 2 giá trị liền trước là `fib(i - 1)` và `fib(i - 2)`, hoàn toàn không cần quan tâm đến toàn bộ lịch sử các số `fib(0) .. fib(i - 3)`.
- Cài đặt tối ưu 2 biến trong Java:
```java
public long fibOptimal(int n) {
    if (n <= 1) return n;
    long prev2 = 0, prev1 = 1;
    for (int i = 2; i <= n; i++) {
        long current = prev1 + prev2;
        prev2 = prev1;
        prev1 = current;
    }
    return prev1;
}
```
Nhờ loại bỏ mảng `dp[]` kích thước n và chỉ duy trì hai biến số thực thể `prev1`, `prev2`, độ phức tạp bộ nhớ phụ giảm triệt để từ O(n) xuống O(1).

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **So sánh đa chiều 3 phương pháp** | **4.0đ** | Nêu đúng thời gian và không gian: O(2^n)/O(n) vs O(n)/O(n) vs O(n)/O(1). |
| **Phân tích cơ chế bộ nhớ cache Memoization** | **3.0đ** | Giải thích việc lưu kết quả khử lặp overlapping subproblems. |
| **Chứng minh và mã Java tối ưu O(1) không gian** | **3.0đ** | Chỉ ra tính chất chỉ phụ thuộc 2 trạng thái kề và viết mã Java 2 biến prev1, prev2. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 23. [CSD_EQ23] Phân tích Trường hợp Mất Cân bằng Left-Right trên Cây AVL và Phép Xoay Kép

- **Mã định danh (Question Code):** `CSD_EQ23`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO4** | **DB 3NF CLO3** (`00000000-0000-0000-0000-000000000563`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 23 — *AVL LR Imbalance & Double Rotation*
- **Mục tiêu đánh giá:** Đánh giá năng lực phân tích hình học cây và chứng minh toán học tại sao phép xoay kép là bắt buộc cho trường hợp Left-Right.

#### ❓ Câu hỏi vấn đáp:
> "Vẽ sơ đồ hình học và phân tích trường hợp mất cân bằng Left-Right (LR) trên Cây AVL. Hãy chứng minh vì sao nếu chỉ áp dụng một phép xoay đơn (Single Right Rotation) tại nút gốc mất cân bằng thì cây vẫn không thể đạt được trạng thái cân bằng. Phép xoay kép (Double Rotation) giải quyết triệt để vấn đề này như thế nào?"

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Sau phép xoay kép Left-Right (LR) gồm xoay trái tại con trái (Left Child) rồi xoay phải tại nút gốc mất cân bằng, nút nào trong cụm 3 nút tham gia phép xoay sẽ trở thành nút gốc mới của cây con và chiều cao của cây con giảm đi bao nhiêu?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Sơ đồ hình học trường hợp mất cân bằng Left-Right (LR):
Giả sử nút A bị mất cân bằng với `BF(A) = +2`.
Nút con trái B có `BF(B) = -1` (do nút mới được chèn vào cây con C của B).
```
       A (BF = +2)
      /
     B (BF = -1)
      \
       C (chiều cao h)
      / \
     C1  C2 (chiều cao h-1)
```
Cấu trúc có dạng gấp khúc (zigzag): từ A rẽ sang trái tới B, rồi từ B rẽ sang phải tới C.

2. Chứng minh phép xoay đơn (Single Right Rotation tại A) thất bại:
- Khi thực hiện xoay phải đơn tại A: Nút B được nâng lên làm gốc mới, A trở thành con phải của B, và cây con phải của B (là C) được chuyển sang làm cây con trái của A.
- Phân tích chiều cao sau khi xoay đơn:
  + Cây con trái của B là B_left (chiều cao h - 1).
  + Cây con phải của B chứa A, mà con trái của A là C (chiều cao h). Chiều cao của nhánh phải của B là `Height(C) + 1 = h + 1`.
  + Tính lại hệ số cân bằng của B sau xoay: `BF(B) = Height(B_left) - Height(A) = (h - 1) - (h + 1) = -2`!
- Kết luận: Cây không những không cân bằng mà còn bị lệch sang phải nghiêm trọng hơn (`BF(B) = -2`). Phép xoay đơn chỉ chuyển độ lệch từ bên này sang bên kia mà không làm giảm được chiều cao nhánh nhô ra.

3. Cơ chế giải quyết triệt để của Phép xoay kép (Double Rotation LR):
Phép xoay kép gồm 2 bước độc lập:
- Bước 1 (Xoay trái tại B): Biến đổi nhánh gấp khúc thành đường thẳng: C được nâng lên làm cha của B, B trở thành con trái của C. Cấu trúc chuyển hóa hoàn hảo về dạng LL (Left-Left thẳng hàng: A -> C -> B).
- Bước 2 (Xoay phải tại A): Đưa C lên làm gốc mới của toàn bộ cây. B làm con trái của C, A làm con phải của C. C1 gắn vào con phải của B, C2 gắn vào con trái của A.
Kết quả: Chiều cao hai bên cân bằng tuyệt đối với `BF(C) = 0`, `BF(A) thuộc {0, 1}`, `BF(B) thuộc {0, -1}`, khôi phục 100% tính chất AVL.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Sơ đồ cấu trúc zigzag Left-Right** | **3.0đ** | Vẽ đúng hình thái A(+2) -> B(-1) -> C và các cây con C1, C2. |
| **Chứng minh phép xoay đơn tạo BF = -2** | **4.0đ** | Tính toán chiều cao hình học sau xoay đơn chứng minh B có BF = -2 vẫn mất cân bằng. |
| **Cơ chế 2 bước của phép xoay kép** | **3.0đ** | Giải thích bước xoay trái tại B nắn thẳng trục thành LL và xoay phải tại A cân bằng gốc C. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 24. [CSD_EQ24] Phân tích Hiệu năng Prim vs Kruskal khi Tìm MST trên Đồ thị Dày và Thưa

- **Mã định danh (Question Code):** `CSD_EQ24`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO5** | **DB 3NF CLO4** (`00000000-0000-0000-0000-000000000564`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 37-38 — *Prim vs Kruskal Dense/Sparse Performance*
- **Mục tiêu đánh giá:** Đánh giá khả năng phân tích sự phụ thuộc hiệu năng của Prim và Kruskal vào số lượng cạnh E và cấu trúc dữ liệu lưu trữ.

#### ❓ Câu hỏi vấn đáp:
> "Hãy phân tích chi phí thời gian của Thuật toán Prim và Thuật toán Kruskal khi tìm Cây khung nhỏ nhất (MST) trên hai kịch bản cực đoan: (1) Đồ thị cực thưa (Sparse Graph với E xấp xỉ V), và (2) Đồ thị cực dày (Dense Graph với E xấp xỉ V^2). Dựa trên phân tích đó, hãy đưa ra khuyến nghị kỹ thuật chọn thuật toán cho từng trường hợp."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Nếu đồ thị là đồ thị đầy đủ cực dày (Complete Graph với E = V*(V-1)/2), cài đặt thuật toán Prim bằng Ma trận kề không dùng Heap đạt độ phức tạp O(V^2); tại sao cách cài đặt này lại chạy nhanh hơn cài đặt Prim sử dụng Min-Heap O(E log V) trong trường hợp này?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Phân tích chi phí thuật toán trên hai cấu trúc:
- Thuật toán Prim:
  + Cài đặt bằng Ma trận kề (Adjacency Matrix): O(V^2), độc lập hoàn toàn với số cạnh E.
  + Cài đặt bằng Danh sách kề + Binary Heap: O((V + E) log V) = O(E log V).
- Thuật toán Kruskal:
  + Bước 1: Sắp xếp E cạnh tốn O(E log E).
  + Bước 2: Duyệt E cạnh kết hợp kiểm tra chu trình và gộp tập hợp bằng Disjoint Set Union (DSU) tốn O(E * alpha(V)).
  + Vì E <= V^2 nên log E <= 2 log V, tổng chi phí luôn là O(E log V).

2. Phân tích Kịch bản 1: Đồ thị cực thưa (Sparse Graph, E xấp xỉ V):
- Chi phí Kruskal: O(V log V) (bước sắp xếp chỉ có khoảng V cạnh). DSU chạy cực nhanh.
- Chi phí Prim (Binary Heap): O(V log V).
- Chi phí Prim (Ma trận kề): O(V^2) (quá chậm, lãng phí thời gian quét các ô 0).
- Đánh giá: Trên đồ thị thưa, Kruskal có lợi thế vượt trội nhờ cài đặt đơn giản, thao tác mảng phẳng cạnh liên tục và không có overhead quản lý heap phức tạp.

3. Phân tích Kịch bản 2: Đồ thị cực dày (Dense Graph, E xấp xỉ V^2):
- Chi phí Kruskal: O(V^2 log(V^2)) = O(2 * V^2 log V). Việc sắp xếp một mảng khổng lồ V^2 phần tử gây quá tải bộ nhớ và tốn nhiều phép so sánh.
- Chi phí Prim (Binary Heap): O(V^2 log V).
- Chi phí Prim (Ma trận kề): Đạt chính xác O(V^2)!
- Đánh giá: Trên đồ thị dày, Prim cài đặt bằng Ma trận kề đạt O(V^2) NHANH HƠN HẲN Kruskal O(V^2 log V) một thừa số log V và hoàn toàn không phải sắp xếp cạnh.

4. Khuyến nghị kỹ thuật:
- Đồ thị thưa (E ~ V): Chọn Kruskal kết hợp DSU.
- Đồ thị dày (E ~ V^2): Chọn Prim kết hợp Ma trận kề.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Phân tích Big-O tổng quát của cả hai thuật toán** | **3.0đ** | Nêu rõ O(E log V) của Kruskal và O(V^2) vs O(E log V) của Prim. |
| **Phân tích kịch bản đồ thị cực thưa (E ~ V)** | **3.5đ** | So sánh hiệu năng O(V log V) và chỉ ra ưu thế đơn giản của Kruskal. |
| **Phân tích kịch bản đồ thị cực dày (E ~ V^2)** | **3.5đ** | Chứng minh Prim Ma trận kề O(V^2) đánh bại Kruskal O(V^2 log V) và đưa ra khuyến nghị. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 25. [CSD_EQ25] Phân tích Trường hợp Xấu nhất O(n^2) của QuickSort và Tối ưu Hóa Công nghiệp

- **Mã định danh (Question Code):** `CSD_EQ25`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO6** | **DB 3NF CLO1** (`00000000-0000-0000-0000-000000000561`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 45 — *QuickSort Worst-Case & Industrial Optimization*
- **Mục tiêu đánh giá:** Đánh giá khả năng phân tích nguyên nhân thoái hóa của QuickSort và các kỹ thuật tối ưu hóa trong thư viện công nghiệp.

#### ❓ Câu hỏi vấn đáp:
> "Kịch bản dữ liệu đầu vào nào sẽ khiến thuật toán QuickSort kinh điển (chọn phần tử đầu hoặc cuối làm Pivot) bị suy thoái hiệu năng về O(n^2)? Hãy phân tích cơ chế suy thoái và trình bày 3 kỹ thuật tối ưu hóa được sử dụng trong các thư viện thực tế (như Java JDK) để triệt tiêu rủi ro này."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Thuật toán sắp xếp lai ghép Introsort (kết hợp QuickSort, HeapSort và Insertion Sort) giám sát độ sâu đệ quy như thế nào để vừa tận dụng tốc độ vượt trội của QuickSort vừa triệt tiêu hoàn toàn nguy cơ suy thoái về O(n^2) trong trường hợp xấu nhất?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Kịch bản dữ liệu khiến QuickSort kinh điển suy thoái về O(n^2):
Khi thuật toán luôn chọn phần tử đầu tiên (`arr[low]`) hoặc phần tử cuối cùng (`arr[high]`) làm Pivot, các kịch bản sau sẽ gây ra thảm họa O(n^2):
- Mảng đã được sắp xếp tăng dần sẵn.
- Mảng đã được sắp xếp giảm dần ngược hoàn toàn.
- Mảng chứa toàn bộ các phần tử có giá trị bằng nhau.

2. Cơ chế suy thoái:
- Trong trường hợp lý tưởng: Pivot chia đôi mảng thành 2 nửa cân bằng n/2, cây đệ quy có chiều sâu log2(n), tổng thời gian là O(n log n).
- Trong kịch bản suy thoái: Tại mỗi bước phân hoạch, Pivot được chọn luôn là phần tử nhỏ nhất (hoặc lớn nhất). Kết quả phân hoạch tạo ra một mảng con rỗng (0 phần tử) và một mảng con còn lại chứa n - 1 phần tử.
- Cây đệ quy bị thoái hóa thành đường thẳng có chiều sâu n.
- Tổng số phép so sánh: `(n - 1) + (n - 2) + ... + 1 = n*(n - 1)/2 ~ O(n^2)`. Đồng thời không gian Call Stack cũng tăng vọt từ O(log n) lên O(n), dễ gây lỗi `StackOverflowError`.

3. Ba kỹ thuật tối ưu hóa trong thư viện công nghiệp (như Java JDK):
- Kỹ thuật 1 - Median-of-Three (Trung vị của 3 phần tử):
  Lấy 3 phần tử ở đầu `arr[low]`, giữa `arr[mid]` và cuối `arr[high]`. Tìm giá trị trung vị giữa 3 số này và dùng nó làm Pivot. Kỹ thuật này triệt tiêu hoàn toàn trường hợp mảng đã sắp xếp hoặc sắp xếp ngược.
- Kỹ thuật 2 - Dual-Pivot QuickSort (Sử dụng 2 Pivot của Vladimir Yaroslavskiy trong Java 7+):
  Chọn 2 pivot P1 và P2 (với P1 <= P2). Phân hoạch mảng thành 3 vùng: vùng < P1, vùng P1 <= x <= P2, và vùng > P2. Giảm số lượng phép so sánh trung bình xuống 1.9 * n * ln(n), giảm đáng kể chi phí bộ nhớ đệm.
- Kỹ thuật 3 - Chuyển sang Insertion Sort cho mảng con nhỏ:
  Khi kích thước mảng con giảm xuống dưới một ngưỡng nhỏ (thường là n <= 27 hoặc 47), dừng gọi đệ quy QuickSort và chuyển sang chạy Insertion Sort vì Insertion Sort chạy tại chỗ cực nhanh và có overhead thấp trên dữ liệu nhỏ.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Xác định kịch bản dữ liệu suy thoái** | **3.0đ** | Chỉ ra mảng đã có thứ tự tăng/giảm hoặc toàn bộ phần tử bằng nhau khi chọn pivot đầu/cuối. |
| **Phân tích cơ chế cây đệ quy n tầng** | **3.5đ** | Chứng minh phân hoạch lệch 0 vs n-1 dẫn tới tổng so sánh n(n-1)/2 ~ O(n^2) và stack O(n). |
| **Trình bày 3 kỹ thuật tối ưu công nghiệp** | **3.5đ** | Mô tả đầy đủ Median-of-Three, Dual-Pivot của Java và chuyển sang Insertion Sort trên mảng nhỏ. |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---

### 26. [CSD_EQ26] Phân tích Mảng Tiền tố Thất bại T[i] trong KMP và Chứng minh Tuyến tính O(n + m)

- **Mã định danh (Question Code):** `CSD_EQ26`
- **Kho lưu trữ (Target Pool):** `exam_questions`
- **Ánh xạ Chuẩn đầu ra:** **FLM CLO8** | **DB 3NF CLO2** (`00000000-0000-0000-0000-000000000562`)
- **Thang nhận thức Bloom:** `Analyze` | **Mức độ khó:** `Hard`
- **Chủ đề bài học (Syllabus):** Session 54 — *Knuth-Morris-Pratt (KMP) Failure Function*
- **Mục tiêu đánh giá:** Đánh giá năng lực phân tích thuật toán so khớp chuỗi KMP, ý nghĩa của mảng tiền tố pi / failure table và chứng minh thời gian tuyến tính.

#### ❓ Câu hỏi vấn đáp:
> "Trong thuật toán Knuth-Morris-Pratt (KMP), mảng tiền tố thất bại (Failure Function / LPS Array T[i]) lưu trữ thông tin gì? Hãy tính mảng T[i] cho chuỗi mẫu `P = "ABABCABAB"`. Chứng minh vì sao thuật toán KMP không bao giờ phải lùi con trỏ trên chuỗi văn bản chính và đạt độ phức tạp thời gian tuyến tính O(n + m)."

#### 🔄 Câu hỏi đào sâu / phản biện nối tiếp (Follow-up Prompt):
> *"Khi xảy ra sự không khớp ký tự (Mismatch) tại vị trí `j` của chuỗi mẫu P và vị trí `i` của văn bản T, mảng tiền tố thất bại LPS trong thuật toán KMP cho phép ta dịch con trỏ mẫu `j` về vị trí nào mà không cần kiểm tra lại các ký tự đã khớp trước đó?"*

#### 💡 Câu trả lời mẫu chuẩn mực (Model Answer):
1. Ý nghĩa của mảng tiền tố thất bại (LPS / Failure Function T[i]):
Mảng `T[i]` (Longest Proper Prefix which is also Suffix) lưu trữ: Độ dài của tiền tố thực sự dài nhất của chuỗi con `P[0 .. i]` mà đồng thời cũng là hậu tố của chuỗi con đó.
Khi xảy ra sự không khớp (mismatch) giữa ký tự văn bản `text[i]` và mẫu `pattern[j]`, ta không cần phải quay lui con trỏ `i` trên chuỗi văn bản về đầu, mà chỉ cần nhảy con trỏ mẫu về vị trí `j = T[j - 1]`.

2. Tính mảng T[i] cho chuỗi mẫu `P = "ABABCABAB"` (độ dài m = 9):

| Chỉ số i | Ký tự P[i] | Chuỗi con P[0 .. i] | Tiền tố cũng là hậu tố dài nhất | Giá trị T[i] |
|---|---|---|---|---|
| 0 | A | "A" | Không có | 0 |
| 1 | B | "AB" | Không có | 0 |
| 2 | A | "ABA" | "A" | 1 |
| 3 | B | "ABAB" | "AB" | 2 |
| 4 | C | "ABABC" | Không có | 0 |
| 5 | A | "ABABCA" | "A" | 1 |
| 6 | B | "ABABCAB" | "AB" | 2 |
| 7 | A | "ABABCABA" | "ABA" | 3 |
| 8 | B | "ABABCABAB" | "ABAB" | 4 |

Mảng kết quả T = `[0, 0, 1, 2, 0, 1, 2, 3, 4]`.

3. Chứng minh KMP không lùi con trỏ văn bản và đạt O(n + m):
- Cơ chế không lùi con trỏ:
  Thông tin về các ký tự đã so khớp thành công trước đó đã được mã hóa trọn vẹn trong mảng `T[]`. Nhờ biết rằng đoạn hậu tố của đoạn đã khớp trùng với tiền tố của mẫu, KMP 'trượt' mẫu sang phải mà không cần đọc lại bất kỳ ký tự nào trên văn bản `text`. Con trỏ `i` trên chuỗi văn bản LUÔN TIẾN ĐƠN ĐIỆU từ 0 đến n - 1, không bao giờ giảm.
- Chứng minh độ phức tạp O(n + m):
  + Giai đoạn tiền xử lý tính mảng T: Tốn O(m).
  + Giai đoạn tìm kiếm: Xét biến thế vị (potential function) `2*i - j`. Ở mỗi bước so sánh, con trỏ `i` tăng hoặc `j` giảm (qua bảng T), đảm bảo số phép so sánh không vượt quá 2*n.
  + Tổng chi phí: O(m) + O(n) = O(n + m), hoàn toàn độc lập với trường hợp dữ liệu lặp lại.

#### 📊 Barem Rubric Đánh giá (Tổng điểm: 10.0 / 10.0):

| Tiêu chí thành phần | Điểm tối đa | Mô tả chi tiết mức độ đạt yêu cầu kỹ thuật |
|---|:---:|---|
| **Định nghĩa LPS Array T[i]** | **3.0đ** | Giải thích chuẩn xác khái niệm tiền tố thực sự dài nhất trùng với hậu tố. |
| **Tính toán đúng 100% mảng T cho ABABCABAB** | **3.5đ** | Điền chính xác mảng T = [0, 0, 1, 2, 0, 1, 2, 3, 4] kèm giải thích từng bước. |
| **Chứng minh con trỏ đơn điệu và Big-O O(n+m)** | **3.5đ** | Chứng minh con trỏ i không bao giờ lùi và tổng số bước bị chặn trên bởi O(n+m). |
| **TỔNG ĐIỂM BAREM** | **10.0đ** | **Bắt buộc bằng đúng 10.0 điểm theo Invariant của hệ thống** |

---
