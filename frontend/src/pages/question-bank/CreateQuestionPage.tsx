import React, { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Card, CardHeader, CardTitle, CardDescription, CardContent, CardFooter } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Input'
import { Plus, Trash2, Sparkles, CheckCircle2, AlertTriangle, ArrowLeft } from 'lucide-react'

export const CreateQuestionPage: React.FC = () => {
  const navigate = useNavigate()
  const [subject, setSubject] = useState('PRN231')
  const [bloom, setBloom] = useState('Apply')
  const [scope, setScope] = useState('SHARED')
  const [title, setTitle] = useState('')
  const [sampleAnswer, setSampleAnswer] = useState('')

  const [criteria, setCriteria] = useState([
    { name: 'Kiến thức cốt lõi & Khái niệm', maxScore: 5.0, description: 'Đúng các khái niệm kỹ thuật chính' },
    { name: 'Khả năng lập luận & Giải trình', maxScore: 3.0, description: 'Trình bày logic, có dẫn chứng' },
    { name: 'Thuật ngữ tiếng Anh CNTT', maxScore: 2.0, description: 'Sử dụng đúng từ mượn chuyên ngành' },
  ])

  const totalScore = criteria.reduce((sum, item) => sum + item.maxScore, 0)
  const isRubricValid = Math.abs(totalScore - 10.0) < 0.001

  const handleAddCriterion = () => {
    setCriteria([...criteria, { name: '', maxScore: 1.0, description: '' }])
  }

  const handleRemoveCriterion = (index: number) => {
    if (criteria.length <= 1) return
    setCriteria(criteria.filter((_, i) => i !== index))
  }

  const handleUpdateCriterion = (index: number, field: string, value: any) => {
    const updated = [...criteria]
    updated[index] = { ...updated[index], [field]: value }
    setCriteria(updated)
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center space-x-3">
        <Button variant="outline" size="sm" onClick={() => navigate('/question-bank')}>
          <ArrowLeft className="w-4 h-4 mr-1" />
          Quay lại
        </Button>
        <h2 className="text-xl font-bold text-slate-900 dark:text-slate-100">
          Tạo Mới Câu Hỏi & Xây Dựng Barem Rubric 10.0
        </h2>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Left Column: Question Details */}
        <div className="lg:col-span-2 space-y-6">
          <Card>
            <CardHeader>
              <CardTitle className="text-base">1. Thông Tin Câu Hỏi</CardTitle>
              <CardDescription>Nhập nội dung câu hỏi vấn đáp và câu trả lời mẫu của Giảng viên.</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-3 gap-3">
                <div>
                  <label className="text-xs font-semibold uppercase text-slate-500">Môn học:</label>
                  <select
                    value={subject}
                    onChange={(e) => setSubject(e.target.value)}
                    className="mt-1 w-full p-2 border rounded-lg text-sm bg-white dark:bg-slate-900"
                  >
                    <option value="PRN231">PRN231 - .NET API</option>
                    <option value="SWD392">SWD392 - Architecture</option>
                  </select>
                </div>
                <div>
                  <label className="text-xs font-semibold uppercase text-slate-500">Cấp độ Bloom:</label>
                  <select
                    value={bloom}
                    onChange={(e) => setBloom(e.target.value)}
                    className="mt-1 w-full p-2 border rounded-lg text-sm bg-white dark:bg-slate-900"
                  >
                    <option value="Remember">Ghi nhớ</option>
                    <option value="Understand">Thông hiểu</option>
                    <option value="Apply">Vận dụng</option>
                    <option value="Analyze">Phân tích</option>
                    <option value="Evaluate">Đánh giá</option>
                    <option value="Create">Sáng tạo</option>
                  </select>
                </div>
                <div>
                  <label className="text-xs font-semibold uppercase text-slate-500">Phạm vi:</label>
                  <select
                    value={scope}
                    onChange={(e) => setScope(e.target.value)}
                    className="mt-1 w-full p-2 border rounded-lg text-sm bg-white dark:bg-slate-900"
                  >
                    <option value="SHARED">Dùng chung (Luyện & Thi)</option>
                    <option value="PRACTICE_ONLY">Chỉ luyện tập</option>
                    <option value="EXAM_ONLY">Chỉ thi thật</option>
                  </select>
                </div>
              </div>

              <div>
                <label className="text-xs font-semibold uppercase text-slate-500">Nội dung câu hỏi vấn đáp:</label>
                <textarea
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  placeholder="Ví dụ: Phân tích cơ chế Garbage Collection trong .NET CLR và các thế hệ Gen 0, Gen 1, Gen 2..."
                  className="mt-1 w-full h-24 p-3 border rounded-lg text-sm focus:ring-2 focus:ring-orange-500 focus:outline-none"
                />
              </div>

              <div>
                <label className="text-xs font-semibold uppercase text-slate-500">Câu trả lời mẫu (Model Answer):</label>
                <textarea
                  value={sampleAnswer}
                  onChange={(e) => setSampleAnswer(e.target.value)}
                  placeholder="Câu trả lời chuẩn mực của Giảng viên để làm mốc đối chuẩn cho AI Gemini..."
                  className="mt-1 w-full h-24 p-3 border rounded-lg text-sm focus:ring-2 focus:ring-orange-500 focus:outline-none"
                />
              </div>
            </CardContent>
          </Card>

          {/* Dynamic Rubric Form */}
          <Card>
            <CardHeader className="flex flex-row items-center justify-between">
              <div>
                <CardTitle className="text-base">2. Barem Tiêu Chí Chấm Điểm Rubric (Bắt buộc = 10.0)</CardTitle>
                <CardDescription>Thiết lập các tiêu chí đánh giá con. Tổng điểm tối đa bắt buộc phải đúng 10.0đ.</CardDescription>
              </div>
              <Button variant="outline" size="sm" onClick={handleAddCriterion}>
                <Plus className="w-3.5 h-3.5 mr-1" />
                Thêm tiêu chí
              </Button>
            </CardHeader>
            <CardContent className="space-y-3">
              {criteria.map((c, index) => (
                <div key={index} className="p-3 border rounded-lg bg-slate-50 dark:bg-slate-800/40 flex items-start gap-3">
                  <div className="flex-1 space-y-2">
                    <Input
                      label={`Tên tiêu chí ${index + 1}`}
                      value={c.name}
                      onChange={(e) => handleUpdateCriterion(index, 'name', e.target.value)}
                      placeholder="Tên tiêu chí..."
                    />
                    <input
                      type="text"
                      value={c.description}
                      onChange={(e) => handleUpdateCriterion(index, 'description', e.target.value)}
                      placeholder="Mô tả tiêu chuẩn đạt điểm..."
                      className="w-full text-xs p-2 border rounded bg-white dark:bg-slate-900"
                    />
                  </div>
                  <div className="w-24">
                    <label className="text-xs font-semibold uppercase text-slate-500">Điểm tối đa:</label>
                    <input
                      type="number"
                      min="0.5"
                      max="10"
                      step="0.5"
                      value={c.maxScore}
                      onChange={(e) => handleUpdateCriterion(index, 'maxScore', parseFloat(e.target.value) || 0)}
                      className="mt-1 w-full p-2 border rounded font-mono font-bold text-center text-sm"
                    />
                  </div>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="mt-6 text-slate-400 hover:text-red-600"
                    onClick={() => handleRemoveCriterion(index)}
                    disabled={criteria.length <= 1}
                  >
                    <Trash2 className="w-4 h-4" />
                  </Button>
                </div>
              ))}
            </CardContent>
          </Card>
        </div>

        {/* Right Column: Validation & AI Simulator */}
        <div className="space-y-6">
          {/* Total Meter Guard */}
          <Card className={`border-2 ${isRubricValid ? 'border-emerald-500' : 'border-rose-500'}`}>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm">Client Guard: Thước Đo Tổng Điểm</CardTitle>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="text-center p-4 rounded-xl bg-slate-50 dark:bg-slate-800/60">
                <span className="text-xs uppercase text-slate-400 font-bold block mb-1">Tổng điểm hiện tại:</span>
                <span className={`text-4xl font-black font-mono ${isRubricValid ? 'text-emerald-600' : 'text-rose-600'}`}>
                  {totalScore.toFixed(1)}
                </span>
                <span className="text-sm font-semibold text-slate-400"> / 10.0 đ</span>
              </div>

              {isRubricValid ? (
                <div className="flex items-center space-x-2 text-xs text-emerald-600 font-semibold p-2 bg-emerald-50 dark:bg-emerald-950/30 rounded-lg">
                  <CheckCircle2 className="w-4 h-4 shrink-0" />
                  <span>Barem hợp lệ! Đạt chuẩn bất biến = 10.0 điểm.</span>
                </div>
              ) : (
                <div className="flex items-center space-x-2 text-xs text-rose-600 font-semibold p-2 bg-rose-50 dark:bg-rose-950/30 rounded-lg">
                  <AlertTriangle className="w-4 h-4 shrink-0" />
                  <span>
                    Tổng điểm đang là {totalScore.toFixed(1)}đ. Vui lòng điều chỉnh để bằng chính xác 10.0đ.
                  </span>
                </div>
              )}
            </CardContent>
            <CardFooter className="flex-col gap-2">
              <Button
                variant="outline"
                size="sm"
                className="w-full text-xs"
                onClick={() => alert('Đang kích hoạt AI Simulator giả lập chấm thử nghiệm...')}
              >
                <Sparkles className="w-3.5 h-3.5 mr-1 text-orange-600" />
                Chấm Thử Bằng AI Simulator
              </Button>
              <Button
                variant="primary"
                size="md"
                className="w-full"
                disabled={!isRubricValid || !title.trim()}
                onClick={() => {
                  alert('Lưu câu hỏi thành công vào Ngân hàng đề!')
                  navigate('/question-bank')
                }}
              >
                Lưu Câu Hỏi Vào Ngân Hàng Đề
              </Button>
            </CardFooter>
          </Card>
        </div>
      </div>
    </div>
  )
}
