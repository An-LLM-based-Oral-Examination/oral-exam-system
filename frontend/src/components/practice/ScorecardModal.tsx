import React from 'react'
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '@/components/ui/Card'
import { Button } from '@/components/ui/Button'
import { Badge } from '@/components/ui/Badge'
import type { ScorecardPayload } from '@/types/exam.types'
import { Award, HelpCircle, Check, ArrowRight } from 'lucide-react'

interface ScorecardModalProps {
  scorecard: ScorecardPayload
  onClose: () => void
  onNextQuestion?: () => void
}

export const ScorecardModal: React.FC<ScorecardModalProps> = ({
  scorecard,
  onClose,
  onNextQuestion,
}) => {
  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm z-50 flex items-center justify-center p-4">
      <Card className="w-full max-w-2xl shadow-2xl max-h-[90vh] flex flex-col">
        <CardHeader className="bg-slate-50 dark:bg-slate-800/50">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-2">
              <Award className="w-6 h-6 text-orange-600" />
              <CardTitle>Bảng Điểm Đánh Giá Tự Động (AI Scorecard)</CardTitle>
            </div>
            <div className="text-right">
              <span className="text-2xl font-black text-orange-600">{scorecard.totalScore.toFixed(1)}</span>
              <span className="text-sm font-semibold text-slate-400"> / 10.0</span>
            </div>
          </div>
        </CardHeader>

        <CardContent className="overflow-y-auto space-y-4 py-4">
          <div>
            <h4 className="text-xs font-bold uppercase tracking-wider text-slate-500 mb-2">
              Chi tiết điểm theo tiêu chí Rubric:
            </h4>
            <div className="space-y-2">
              {scorecard.criteriaScores.map((item, idx) => (
                <div key={idx} className="p-3 rounded-lg border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 flex justify-between items-start">
                  <div>
                    <p className="text-sm font-semibold text-slate-800 dark:text-slate-200">
                      Tiêu chí: {item.criterionId}
                    </p>
                    <p className="text-xs text-slate-600 dark:text-slate-400 mt-1">{item.comment}</p>
                  </div>
                  <Badge variant="success" className="font-mono text-xs ml-3 shrink-0">
                    {item.score.toFixed(1)} đ
                  </Badge>
                </div>
              ))}
            </div>
          </div>

          <div className="p-3 rounded-lg bg-orange-50/70 dark:bg-orange-950/20 border border-orange-200 dark:border-orange-900">
            <h4 className="text-xs font-bold uppercase tracking-wider text-orange-800 dark:text-orange-300 mb-1">
              Nhận xét sư phạm định tính:
            </h4>
            <p className="text-xs text-slate-700 dark:text-slate-300 leading-relaxed">
              {scorecard.feedback}
            </p>
          </div>

          {scorecard.followUpQuestion && (
            <div className="p-3 rounded-lg bg-sky-50 dark:bg-sky-950/20 border border-sky-200 dark:border-sky-900">
              <div className="flex items-center space-x-1.5 text-sky-800 dark:text-sky-300 mb-1">
                <HelpCircle className="w-4 h-4" />
                <h4 className="text-xs font-bold uppercase tracking-wider">
                  Câu hỏi đào sâu mở rộng (Follow-up A2):
                </h4>
              </div>
              <p className="text-xs text-slate-700 dark:text-slate-300 italic">
                "{scorecard.followUpQuestion}"
              </p>
            </div>
          )}
        </CardContent>

        <CardFooter className="justify-between">
          <Button variant="outline" size="sm" onClick={onClose}>
            <Check className="w-4 h-4 mr-1.5" />
            Đóng bảng điểm
          </Button>
          {onNextQuestion && (
            <Button variant="primary" size="md" onClick={onNextQuestion}>
              Luyện tập câu tiếp theo
              <ArrowRight className="w-4 h-4 ml-1.5" />
            </Button>
          )}
        </CardFooter>
      </Card>
    </div>
  )
}
