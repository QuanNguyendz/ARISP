import { useEffect, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  AlertCircle,
  Copy,
  Check,
  ExternalLink,
  KeyRound,
  Link2,
  Loader2,
  RefreshCw,
} from 'lucide-react'
import { interviewService } from '@ari/shared/fservices/interview'
import { formatDateTime24, formatTime24 } from '@ari/shared/utils/time24'

interface Props {
  applicationId: string
}

/**
 * Mã vào phòng phỏng vấn — cấp NGAY trên danh sách ứng viên, ở vòng hội thoại đã có lịch.
 *
 * Cùng một mã, hai cách dùng tuỳ nơi ứng viên làm vòng đó (server quyết, cờ `isRemote`):
 *  - **Tại văn phòng** (chuyên môn): ứng viên check-in, Recruiter mở sẵn trang Kiosk trên máy phỏng
 *    vấn, cấp mã rồi đưa cho ứng viên nhập. Mã KHÔNG hiện trong Portal của ứng viên.
 *  - **Làm từ nhà** (sơ loại): cấp mã rồi gửi link vào phòng (đã điền sẵn mã) cho ứng viên qua
 *    Zalo/SMS — mã cũng hiện trong Portal kèm nút vào phòng.
 * Nhập mã xong, cả hai đi chung một đường: phòng chờ → Hiring Manager cho vào (ADR-067).
 *
 * **Bấm lại không làm mất mã.** "Cấp mã" khi đang có mã chỉ trả lại mã đó; thay mã là một nút riêng
 * có xác nhận, vì mã cũ hết hiệu lực ngay — ứng viên làm từ nhà có thể đang cầm nó.
 */
export default function InterviewCodeCard({ applicationId }: Props) {
  const { t } = useTranslation('modules/staff/candidatePipeline')
  const queryClient = useQueryClient()
  // Nằm dưới tiền tố ['application', id] — realtime (bảng interview_codes) huỷ đúng tiền tố đó khi mã
  // được dùng, nên thẻ tự chuyển trạng thái lúc ứng viên nhập mã ở Kiosk.
  const queryKey = ['application', applicationId, 'interview-code']

  const { data, isLoading, isError } = useQuery({
    queryKey,
    queryFn: () => interviewService.getApplicationCode(applicationId),
  })

  const [actionError, setActionError] = useState<string | null>(null)
  const [copied, setCopied] = useState<'code' | 'link' | null>(null)

  useEffect(() => {
    if (!copied) return
    const id = window.setTimeout(() => setCopied(null), 1800)
    return () => window.clearTimeout(id)
  }, [copied])

  const issue = useMutation({
    mutationFn: (regenerate: boolean) => interviewService.issueApplicationCode(applicationId, regenerate),
    onSuccess: (dto) => {
      setActionError(null)
      queryClient.setQueryData(queryKey, dto)
      // Cấp mã có thể đổi trạng thái chi tiết của hồ sơ trên bảng.
      queryClient.invalidateQueries({ queryKey: ['applications'] })
    },
    onError: (e: unknown) => {
      const message = (e as { response?: { data?: { message?: string } } })?.response?.data?.message
      setActionError(message ?? t('code.issueError'))
    },
  })

  const copy = (text: string, what: 'code' | 'link') =>
    navigator.clipboard?.writeText(text).then(() => setCopied(what))

  if (isLoading) {
    return (
      <div className="mb-4 flex items-center gap-2 rounded-xl border border-ink-200 px-4 py-3 text-xs text-ink-500 dark:border-white/10">
        <Loader2 className="h-3.5 w-3.5 animate-spin" /> {t('code.loading')}
      </div>
    )
  }

  if (isError || !data) {
    return (
      <p className="mb-4 flex items-center gap-1.5 text-xs text-ink-500 dark:text-ink-400">
        <AlertCircle className="h-3.5 w-3.5" /> {t('code.loadError')}
      </p>
    )
  }

  // Server nói không cấp được (vừa vào phòng, hồ sơ vừa đóng…) — nói lý do thay vì một nút bấm là lỗi.
  if (!data.canIssue) {
    return data.blockedReason ? (
      <p className="mb-4 flex items-start gap-1.5 rounded-xl border border-ink-200 bg-ink-50 px-3 py-2 text-xs text-ink-600 dark:border-white/10 dark:bg-white/5 dark:text-ink-300">
        <KeyRound className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {data.blockedReason}
      </p>
    ) : null
  }

  const expiresAt = data.expiresAt ? new Date(data.expiresAt) : null
  const expired = !!expiresAt && expiresAt.getTime() <= Date.now()
  const code = !expired ? data.code : null
  const sameDay = expiresAt?.toDateString() === new Date().toDateString()
  const busy = issue.isPending

  const regenerate = () => {
    if (code && !window.confirm(t('code.confirmRegenerate', { code }))) return
    issue.mutate(true)
  }

  const SECONDARY =
    'inline-flex items-center gap-1.5 rounded-lg border border-ink-200 bg-white px-2.5 py-1.5 text-xs font-medium text-ink-700 hover:bg-ink-50 disabled:opacity-60 dark:border-white/10 dark:bg-white/5 dark:text-ink-200 dark:hover:bg-white/10'

  return (
    <section className="mb-4 rounded-xl border border-brand-200 bg-brand-50/50 p-4 dark:border-brand-500/30 dark:bg-brand-500/10">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h4 className="flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
          <KeyRound className="h-4 w-4 text-brand-600 dark:text-brand-400" />
          {t('code.title', { round: data.roundNumber ?? 1 })}
        </h4>
        <span
          className={`rounded-full px-2.5 py-0.5 text-[11px] font-semibold ${
            data.isRemote
              ? 'bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300'
              : 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-300'
          }`}
        >
          {data.isRemote ? t('code.remote') : t('code.onsite')}
        </span>
      </div>

      {actionError && (
        <p className="mt-2 flex items-start gap-1.5 text-xs text-red-600 dark:text-red-400">
          <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {actionError}
        </p>
      )}

      {code ? (
        <>
          <div className="mt-3 flex flex-wrap items-end gap-x-5 gap-y-1">
            <span className="select-all font-mono text-3xl font-extrabold tracking-[0.3em] text-ink-900 dark:text-white">
              {code}
            </span>
            {expiresAt && (
              <span className="pb-1 text-xs text-ink-500 dark:text-ink-400">
                {t('code.expiresAt', {
                  time: sameDay ? formatTime24(expiresAt) : formatDateTime24(expiresAt),
                })}
              </span>
            )}
          </div>

          <div className="mt-3 flex flex-wrap gap-2">
            <button type="button" className={SECONDARY} onClick={() => void copy(code, 'code')}>
              {copied === 'code' ? <Check className="h-3.5 w-3.5" /> : <Copy className="h-3.5 w-3.5" />}
              {copied === 'code' ? t('code.copied') : t('code.copyCode')}
            </button>

            {/* Làm từ nhà: thứ cần gửi ứng viên là LINK đã kèm mã — mở ra là ô mã điền sẵn, chỉ còn
                bấm bắt đầu. Tại văn phòng: link Kiosk trống để mở sẵn trên máy phỏng vấn. */}
            {data.isRemote && data.entryUrl && (
              <button
                type="button"
                className={SECONDARY}
                onClick={() => void copy(data.entryUrl!, 'link')}
              >
                {copied === 'link' ? <Check className="h-3.5 w-3.5" /> : <Link2 className="h-3.5 w-3.5" />}
                {copied === 'link' ? t('code.copied') : t('code.copyLink')}
              </button>
            )}
            {!data.isRemote && data.kioskUrl && (
              <a href={data.kioskUrl} target="_blank" rel="noreferrer" className={SECONDARY}>
                <ExternalLink className="h-3.5 w-3.5" /> {t('code.openKiosk')}
              </a>
            )}

            <button type="button" className={SECONDARY} onClick={regenerate} disabled={busy}>
              {busy ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <RefreshCw className="h-3.5 w-3.5" />}
              {t('code.regenerate')}
            </button>
          </div>
        </>
      ) : (
        <>
          <p className="mt-2 text-xs text-ink-600 dark:text-ink-300">
            {expired ? t('code.expired') : data.isRemote ? t('code.remoteHint') : t('code.onsiteHint')}
          </p>
          <div className="mt-3 flex flex-wrap gap-2">
            <button
              type="button"
              onClick={() => issue.mutate(expired)}
              disabled={busy}
              className="inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-3 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60"
            >
              {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <KeyRound className="h-4 w-4" />}
              {t('code.issue')}
            </button>
            {!data.isRemote && data.kioskUrl && (
              <a href={data.kioskUrl} target="_blank" rel="noreferrer" className={SECONDARY}>
                <ExternalLink className="h-3.5 w-3.5" /> {t('code.openKiosk')}
              </a>
            )}
          </div>
        </>
      )}

      <p className="mt-3 text-[11px] leading-relaxed text-ink-500 dark:text-ink-400">
        {t('code.nextStep')}
      </p>
    </section>
  )
}
