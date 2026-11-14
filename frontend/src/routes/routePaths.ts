export const ROUTE_PATHS = {
  lecturer: {
    dashboard: '/lecturer/dashboard',
    classes: '/lecturer/classes',
    questionBank: '/lecturer/question-bank',
    createQuestion: '/lecturer/question-bank/create',
    audit: '/lecturer/audit',
    appeals: '/lecturer/appeals',
  },
  departmentHead: {
    dashboard: '/department-head/dashboard',
    questionReview: '/department-head/question-review',
    appeals: '/department-head/appeals',
  },
  examinationOfficer: {
    dashboard: '/examination-officer/dashboard',
    exams: '/examination-officer/exams',
    tickets: '/examination-officer/tickets/assign',
    monitor: '/examination-officer/monitor',
    grades: '/examination-officer/grades',
    appeals: '/examination-officer/appeals',
    dlq: '/examination-officer/dlq',
  },
  admin: {
    dashboard: '/admin/dashboard',
    dlq: '/admin/dlq',
  },
  proctor: {
    attendance: '/proctor/attendance',
  },
} as const
