/** Fallback dialog context; detail pages and selected tabs supply a closer DialogScope. */
const names: Record<string, string> = {
  '/student/overview': 'Tổng quan học tập',
  '/classes': 'Lớp học của tôi',
  '/practice': 'Luyện tập',
  '/mock-exam': 'Thi thử',
  '/results': 'Lịch sử & Kết quả',
  '/in-class-exam': 'Làm kiểm tra',
  '/profile': 'Hồ sơ cá nhân',
  '/lecturer/classes': 'Quản lý lớp học',
  '/lecturer/dashboard': 'Tổng quan',
  '/lecturer/question-bank': 'Câu hỏi của tôi',
  '/question-bank': 'Câu hỏi của tôi',
  '/lecturer/exam-authoring': 'Soạn đề thi',
  '/lecturer/appeals': 'Phúc khảo được giao',
  '/lecturer/audit': 'Hậu kiểm điểm',
  '/proctor/attendance': 'Điểm danh',
  '/department-head/exam-structures': 'Chuẩn bị đề thi',
  '/department-head/courses': 'Quản lý môn học',
  '/department-head/question-review': 'Yêu cầu duyệt',
  '/department-head/question-bank': 'Ngân hàng câu hỏi',
  '/department-head/appeals': 'Quản lý phúc khảo',
  '/admin/users': 'Quản lý người dùng',
  '/admin/courses': 'Môn học, CTĐT & CLO',
  '/admin/semesters': 'Học kỳ & Môn mở',
  '/admin/configs': 'Cấu hình',
  '/admin/notifications': 'Thông báo',
  '/admin/audit-logs': 'Nhật ký kiểm toán',
  '/admin/dlq': 'Lỗi chấm AI',
  '/dlq': 'Lỗi chấm AI',
  '/appeals': 'Phúc khảo',
  '/official-exams/manage': 'Kỳ thi chính thức',
}
export function dialogPageName(path: string) {
  return Object.entries(names)
    .sort(([a], [b]) => b.length - a.length)
    .find(([prefix]) => path === prefix || path.startsWith(`${prefix}/`))?.[1]
}
