import React from 'react'
import { Card, CardHeader, CardTitle, CardContent, CardFooter } from '@/components/common/Card'
import { Button } from '@/components/common/Button'
import { Badge } from '@/components/common/Badge'
import type { ScorecardPayload } from '@/types/practice.types'
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
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm">
      <Card className="flex max-h-[90vh] w-full max-w-2xl flex-col shadow-2xl">
        <CardHeader className="bg-slate-50 dark:bg-slate-800/50">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-2">
              <Award className="h-6 w-6 text-orange-600" />
              <CardTitle>Bảng Điểm Đánh Giá Tự Động (AI Scorecard)</CardTitle>
            </div>
            <div className="text-right">
              <span className="text-2xl font-black text-orange-600">
                {scorecard.totalScore.toFixed(1)}
              </span>
              <span className="text-sm font-semibold text-slate-400"> / 10.0</span>
            </div>
          </div>
        </CardHeader>

        <CardContent className="space-y-4 overflow-y-auto py-4">
          <div>
            <h4 className="mb-2 text-xs font-bold tracking-wider text-slate-500 uppercase">
              Chi tiết điểm theo tiêu chí Rubric:
            </h4>
            <div className="space-y-2">
              {scorecard.criteriaScores.map((item, idx) => (
                <div
                  key={idx}
                  className="flex items-start justify-between rounded-lg border border-slate-200 bg-white p-3 dark:border-slate-800 dark:bg-slate-900"
                >
                  <div>
                    <p className="text-sm font-semibold text-slate-800 dark:text-slate-200">
                      Tiêu chí: {item.criterionId}
                    </p>
                    <p className="mt-1 text-xs text-slate-600 dark:text-slate-400">
                      {item.comment}
                    </p>
                  </div>
                  <Badge variant="success" className="ml-3 shrink-0 font-mono text-xs">
                    {item.score.toFixed(1)} đ
                  </Badge>
                </div>
              ))}
            </div>
          </div>

          <div className="rounded-lg border border-orange-200 bg-orange-50/70 p-3 dark:border-orange-900 dark:bg-orange-950/20">
            <h4 className="mb-1 text-xs font-bold tracking-wider text-orange-800 uppercase dark:text-orange-300">
              Nhận xét sư phạm định tính:
            </h4>
            <p className="text-xs leading-relaxed text-slate-700 dark:text-slate-300">
              {scorecard.feedback}
            </p>
          </div>

          {scorecard.followUpQuestion && (
            <div className="rounded-lg border border-sky-200 bg-sky-50 p-3 dark:border-sky-900 dark:bg-sky-950/20">
              <div className="mb-1 flex items-center space-x-1.5 text-sky-800 dark:text-sky-300">
                <HelpCircle className="h-4 w-4" />
                <h4 className="text-xs font-bold tracking-wider uppercase">
                  Câu hỏi đào sâu mở rộng (Follow-up A2):
                </h4>
              </div>
              <p className="text-xs text-slate-700 italic dark:text-slate-300">
                "{scorecard.followUpQuestion}"
              </p>
            </div>
          )}
        </CardContent>

        <CardFooter className="justify-between">
          <Button variant="outline" size="sm" onClick={onClose}>
            <Check className="mr-1.5 h-4 w-4" />
            Đóng bảng điểm
          </Button>
          {onNextQuestion && (
            <Button variant="primary" size="md" onClick={onNextQuestion}>
              Luyện tập câu tiếp theo
              <ArrowRight className="ml-1.5 h-4 w-4" />
            </Button>
          )}
        </CardFooter>
      </Card>
    </div>
  )
}
