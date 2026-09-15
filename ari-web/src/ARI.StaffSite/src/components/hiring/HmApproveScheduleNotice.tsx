import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { AlertCircle, CalendarClock, Info, Loader2 } from 'lucide-react'
import { scheduleService } from '@ari/shared/fservices/schedule'
import { isOnlineTestRound } from '@ari/shared/utils/roundTypes'
import { HIRING_NS } from './hiringConfig'

interface Props {
  jobPostingId: string
  /** Cấu hình vòng của tin — để biết vòng 1 có phải bài trắc nghiệm không. */
  rounds?: { roundNumber: number; roundType?: string | null }[] | null
  /** Đưa HM tới mục "Lịch tôi có mặt được". Không truyền thì không hiện nút. */
  onOpenAvailability?: () => void
}

/**
 * Câu nói kèm nút "Duyệt" của Hiring Manager: duyệt xong thì Recruiter có XẾP LỊCH NGAY được không.
 *
 * Lịch có mặt của HM không còn đi kèm lệnh duyệt (ADR-067, sửa 2026-09-14) — khai riêng ở màn tin.
 * Nên "duyệt khi chưa khai lịch vòng 1" là hợp lệ, và đúng lúc đó hồ sơ sẽ vào hàng chờ xếp lịch rồi
 * ĐỨNG IM chờ chính người vừa bấm. Không chặn việc duyệt, nhưng phải nói ra.
 *
 * Mount = lúc hộp xác nhận mở, nên lịch được đọc mới mỗi lần — HM vừa khai xong ở cột bên cạnh thì
 * mở lại là thấy ngay.
 */
export default function HmApproveScheduleNotice({ jobPostingId, rounds, onOpenAvailability }: Props) {
  const { t } = useTranslation(HIRING_NS)
  const round1IsTest = isOnlineTestRound(rounds?.find((r) => r.roundNumber === 1)?.roundType)

  const { data, isLoading, isError } = useQuery({
    queryKey: ['hm-availability', jobPostingId, 1],
    queryFn: () => scheduleService.getHmAvailability(jobPostingId, 1),
    enabled: !!jobPostingId && !round1IsTest,
  })

  if (round1IsTest)
    return (
      <p className="flex items-start gap-2 rounded-xl border border-sky-200 bg-sky-50 px-3 py-2 text-xs text-sky-800 dark:border-sky-500/25 dark:bg-sky-500/10 dark:text-sky-300">
        <Info className="mt-0.5 h-3.5 w-3.5 shrink-0" />
        <span>{t('gate.approveRound1Test')}</span>
      </p>
    )

  if (isLoading)
    return (
      <p className="flex items-center gap-2 text-xs text-ink-500 dark:text-ink-400">
        <Loader2 className="h-3.5 w-3.5 animate-spin" /> {t('gate.approveChecking')}
      </p>
    )

  // Không đọc được lịch thì không đoán — một cảnh báo "bạn chưa khai" sai còn tệ hơn im lặng.
  if (isError) return null

  if ((data?.length ?? 0) > 0)
    return (
      <p className="flex items-start gap-2 rounded-xl border border-ink-200 bg-ink-50 px-3 py-2 text-xs text-ink-700 dark:border-white/10 dark:bg-white/5 dark:text-ink-300">
        <CalendarClock className="mt-0.5 h-3.5 w-3.5 shrink-0 text-brand-600 dark:text-brand-400" />
        <span>{t('gate.approveHasWindows')}</span>
      </p>
    )

  return (
    <div className="rounded-xl border border-amber-200 bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:border-amber-500/25 dark:bg-amber-500/10 dark:text-amber-300">
      <p className="flex items-start gap-2">
        <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" />
        <span>{t('gate.approveNoWindows')}</span>
      </p>
      {onOpenAvailability && (
        <button
          type="button"
          onClick={onOpenAvailability}
          className="ml-5 mt-1.5 inline-flex items-center gap-1 font-semibold underline-offset-2 hover:underline"
        >
          <CalendarClock className="h-3.5 w-3.5" /> {t('gate.approveOpenAvailability')}
        </button>
      )}
    </div>
  )
}
