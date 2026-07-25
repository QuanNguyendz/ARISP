import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { ClipboardList, CheckCircle2, XCircle, ChevronRight } from 'lucide-react'
import { onlineTestService } from '@ari/shared/fservices/onlineTest'

/**
 * Ô "Bài thi trắc nghiệm" trên trang chi tiết hồ sơ ứng tuyển của ứng viên.
 * Chỉ hiển thị khi vị trí CÓ đề thi (totalQuestions > 0). Tự ẩn nếu chưa có đề / lỗi.
 */
export default function OnlineTestEntry({ applicationId }: { applicationId: string }) {
  const { t } = useTranslation('modules/candidate/onlineTest')
  const { data } = useQuery({
    queryKey: ['online-test-entry', applicationId],
    queryFn: () => onlineTestService.getTest(applicationId),
    enabled: !!applicationId,
    retry: false,
  })

  if (!data || data.totalQuestions === 0) return null

  if (data.alreadySubmitted) {
    const passed = !!data.isPassed
    return (
      <div className="rounded-2xl border border-ink-200 bg-white p-4 shadow-card">
        <div className="flex items-center gap-3">
          <span
            className={`grid h-10 w-10 shrink-0 place-items-center rounded-xl ${
              passed ? 'bg-emerald-50 text-emerald-600' : 'bg-red-50 text-red-500'
            }`}
          >
            {passed ? <CheckCircle2 className="h-5 w-5" /> : <XCircle className="h-5 w-5" />}
          </span>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-ink-900">
              {t('entry.resultTitle', { result: passed ? t('passed') : t('notPassed') })}
            </p>
            <p className="text-xs text-ink-500">
              {t('entry.resultDetail', { score: Math.round(data.score ?? 0), pass: data.passScore })}
            </p>
          </div>
        </div>
      </div>
    )
  }

  return (
    <Link
      to={`/candidate/online-test/${applicationId}`}
      className="flex items-center gap-3 rounded-2xl border border-brand-200 bg-brand-50 p-4 shadow-card transition hover:bg-brand-100"
    >
      <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-brand-600 text-white">
        <ClipboardList className="h-5 w-5" />
      </span>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-semibold text-brand-800">{t('entry.cta')}</p>
        <p className="text-xs text-brand-600">
          {t('entry.ctaSub', { count: data.totalQuestions, pass: data.passScore })}
        </p>
      </div>
      <ChevronRight className="h-5 w-5 shrink-0 text-brand-500" />
    </Link>
  )
}
