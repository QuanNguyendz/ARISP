import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import {
  ArrowLeft,
  Briefcase,
  MapPin,
  Users,
  Wallet,
  FileText,
  Eye,
  Check,
  X,
  Loader2,
  AlertCircle,
} from 'lucide-react'
import { PageHeader, ErrorAlert, LoadingSpinner } from '@ari/shared/ui'
import { jobService } from '@ari/shared/fservices/job'
import { hiringTeamService } from '@ari/shared/fservices/hiringTeam'
import { scheduleService } from '@ari/shared/fservices/schedule'
import { useAuthStore } from '@ari/shared/store/auth'
import { useDocumentViewer } from '@ari/shared/document/DocumentViewer'
import CandidatePipeline from '@/components/jobCandidates/CandidatePipeline'
import HiringTeamPanel from '@/components/hiring/HiringTeamPanel'
import HmAvailabilityFields, {
  toLocalInput,
  toPayload,
  hasBlockingIssue,
  type DraftWindow,
} from '@/components/hiring/HmAvailabilityFields'
import { formatSalary } from '@/components/hiring/hiringConfig'
import type { HrApplicationItem } from '@ari/shared/types/application'

const CARD =
  'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card'

/**
 * Chi tiết tin dưới góc nhìn Hiring Manager (ADR-061).
 *
 * CỐ Ý là một trang riêng chứ không phải `hr/JobPostingDetailPage` gắn thêm `variant="hm"`: trang
 * đó đã 1567 dòng và gần như toàn bộ nội dung là thao tác vận hành mà Hiring Manager không có
 * quyền làm (sửa tin, đổi trạng thái, cấu hình ca, cấp mã Kiosk, playbook). Luồn thêm một vai vào
 * đó nghĩa là mỗi lần sửa trang phải nghĩ cho ba vai cùng lúc.
 *
 * **Cổng duyệt shortlist nằm NGAY TẠI ĐÂY** (thay màn danh sách riêng trước đây): quyết định của
 * HM là quyết định chuyên môn, mà màn danh sách kia chỉ có tên + điểm CV — duyệt mà không nhìn
 * thấy hồ sơ. Ở đây hai nút nằm cạnh chính khung đọc CV.
 */
export default function HmJobDetailPage() {
  const { id = '' } = useParams<{ id: string }>()
  const { t } = useTranslation('modules/hm/jobs')
  const { openDocument } = useDocumentViewer()
  const queryClient = useQueryClient()
  const currentUser = useAuthStore((s) => s.user)

  const {
    data: job,
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['job', id],
    queryFn: () => jobService.getJobPostingById(id),
    enabled: !!id,
  })

  const { data: applications = [] } = useQuery({
    queryKey: ['job-applications', id],
    queryFn: () => jobService.getJobApplications(id),
    enabled: !!id,
  })

  /** Hồ sơ đang được quyết định + chế độ. `null` = không mở hộp thoại nào. */
  const [decision, setDecision] = useState<{
    app: HrApplicationItem
    mode: 'approve' | 'reject'
  } | null>(null)
  const [windows, setWindows] = useState<DraftWindow[]>([])
  /**
   * Khung giờ ĐÃ khai và còn hiệu lực nhưng ĐÃ bắt đầu — không nạp vào ô sửa được, vì luật "giờ bắt
   * đầu phải ở tương lai" sẽ báo sai ngay cho một khung hoàn toàn hợp lệ. Chúng vẫn tính là lịch
   * rảnh (lệnh duyệt THÊM khung chứ không thay cả danh sách), nên chỉ cần hiện lại cho HM biết.
   */
  const [runningWindows, setRunningWindows] = useState<string[]>([])
  const [reason, setReason] = useState('')
  const [actionError, setActionError] = useState<string | null>(null)

  const submit = useMutation({
    mutationFn: (input: {
      appId: string
      mode: 'approve' | 'reject'
      note?: string
      availabilities?: { startTime: string; endTime: string; note?: string | null }[]
    }) =>
      hiringTeamService.submitHmDecision(
        input.appId,
        input.mode === 'approve' ? 'approved' : 'rejected',
        input.note,
        input.availabilities
      ),
    onSuccess: () => {
      setDecision(null)
      setActionError(null)
      setReason('')
      queryClient.invalidateQueries({ queryKey: ['job-applications', id] })
      queryClient.invalidateQueries({ queryKey: ['applications'] })
    },
    onError: (e: unknown) => {
      const message = (e as { response?: { data?: { message?: string } } })?.response?.data?.message
      setActionError(message ?? t('detail.decisionError'))
    },
  })

  /**
   * Mở hộp thoại duyệt kèm NẠP LẠI khung giờ đã khai cho vòng 1 của tin này — duyệt người thứ hai
   * trong cùng đợt thì không phải gõ lại, và sửa được nếu lịch đổi.
   */
  const openApprove = async (app: HrApplicationItem) => {
    setActionError(null)
    setDecision({ app, mode: 'approve' })
    try {
      const existing = await scheduleService.getHmAvailability(app.jobPostingId, 1)
      const now = Date.now()
      const upcoming = existing.filter((w) => new Date(w.startTime).getTime() > now)
      const running = existing.filter((w) => new Date(w.startTime).getTime() <= now)

      setWindows(
        upcoming.map((w) => ({
          start: toLocalInput(w.startTime),
          end: toLocalInput(w.endTime),
          note: w.note ?? '',
        }))
      )
      setRunningWindows(
        running.map((w) => {
          const from = new Date(w.startTime)
          const to = new Date(w.endTime)
          const fmt = (d: Date) =>
            d.toLocaleString('vi-VN', {
              day: '2-digit',
              month: '2-digit',
              hour: '2-digit',
              minute: '2-digit',
            })
          return `${fmt(from)} – ${fmt(to)}`
        })
      )
    } catch {
      setWindows([])
      setRunningWindows([])
    }
  }

  if (isLoading) return <LoadingSpinner message={t('loading')} />
  if (error || !job) return <ErrorAlert message={t('loadError')} />

  // Quyền thật do server kiểm; ở đây chỉ là chuyện không bày ra nút bấm không được.
  const isTheHiringManager = job.hiringManagerUserId === currentUser?.id

  const facts = [
    { icon: Briefcase, value: job.department || t('noDepartment') },
    { icon: MapPin, value: job.location || '—' },
    {
      icon: Wallet,
      value: job.salaryIsNegotiable
        ? t('detail.salaryNegotiable')
        : formatSalary(job.salaryMin, job.salaryCurrency),
    },
    { icon: Users, value: t('applicantCount', { count: applications.length }) },
  ]

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <div className="space-y-6">
        <Link
          to="/hm/jobs"
          className="inline-flex items-center gap-1.5 text-sm text-ink-600 dark:text-ink-400 hover:text-brand-600 dark:hover:text-brand-400 transition-colors"
        >
          <ArrowLeft className="h-4 w-4" /> {t('detail.back')}
        </Link>

        <PageHeader title={job.title} description={job.department || t('noDepartment')} />

        {/* Hộp thoại đang mở thì lỗi vẽ BÊN TRONG nó (xem bên dưới) — vẽ cả hai chỗ là một
            thông báo hiện hai lần, mà bản ở đây lại bị lớp phủ che. */}
        {actionError && !decision && (
          <ErrorAlert message={actionError} onDismiss={() => setActionError(null)} />
        )}

        <div className="grid gap-6 lg:grid-cols-3">
          <div className="space-y-6 lg:col-span-2">
            <section className={CARD}>
              <dl className="grid gap-4 sm:grid-cols-2">
                {facts.map(({ icon: Icon, value }) => (
                  <div key={value} className="flex items-center gap-2 text-sm">
                    <Icon className="h-4 w-4 shrink-0 text-brand-600 dark:text-brand-400" />
                    <span className="truncate text-ink-700 dark:text-ink-300">{value}</span>
                  </div>
                ))}
              </dl>
            </section>

            <section className={CARD}>
              <h2 className="mb-3 text-lg font-semibold text-ink-900 dark:text-white">
                {t('detail.jdTitle')}
              </h2>
              {/* Mô tả công việc là HTML do trình soạn sinh ra (ADR-064) — in ra dạng văn bản thuần
                  thì người đọc thấy nguyên thẻ `<li>`, `<h3>`. Cùng cách dựng với màn tin của HR
                  Leader, kể cả lớp `ql-editor-display`, để hai bên nhìn thấy đúng một bản. */}
              <div
                className="ql-editor-display leading-relaxed text-ink-600 dark:text-ink-400"
                dangerouslySetInnerHTML={{ __html: job.jobDescription || '' }}
              />
            </section>

            {/*
              Cùng khối quy trình với màn tin của Recruiter — Hiring Manager phải nhìn thấy phễu y hệt
              người đang vận hành nó, nếu không hai bên bàn về hai bức tranh khác nhau.

              KHÔNG truyền `onApprove`/`onInvite`: sàng CV và xếp lịch là việc của Recruiter. Thứ DUY
              NHẤT thuộc về HM ở đây là cổng duyệt shortlist, đi qua `hmDecision`.
            */}
            <CandidatePipeline
              apps={applications}
              rounds={job.roundConfigs || []}
              processingAppId={submit.isPending ? (decision?.app.id ?? null) : null}
              candidateHref={(a) => `/hm/candidates/${a.id}`}
              statusLabel={(s) => t(`applicationStatus.${s}`, { defaultValue: s })}
              hmDecision={
                isTheHiringManager
                  ? {
                      onApprove: (a) => void openApprove(a),
                      onReject: (a) => {
                        setActionError(null)
                        setReason('')
                        setDecision({ app: a, mode: 'reject' })
                      },
                    }
                  : undefined
              }
            />
          </div>

          <div className="space-y-6">
            {/* File JD — ADR-064. Chữ ký của Hiring Manager LÀ cổng đăng tin (ADR-063), nên họ phải
                xem được đúng thứ mình đang duyệt. Trước đây màn này không có chỗ nào mở file, tức là
                ký duyệt mà không nhìn thấy bản mô tả công việc. */}
            {job.jdFileUrl && (
              <section className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5">
                <h2 className="mb-3 text-sm font-semibold text-ink-900 dark:text-white">
                  {t('detail.jdFileTitle')}
                </h2>
                <button
                  onClick={() => openDocument(job.jdFileUrl!, job.jdFileName || 'JD')}
                  className="flex w-full items-center gap-2 rounded-xl border border-ink-200 px-3 py-2.5 text-left text-sm transition hover:border-brand-300 dark:border-white/10"
                >
                  <FileText className="h-4 w-4 shrink-0 text-brand-600 dark:text-brand-400" />
                  <span className="min-w-0 flex-1 truncate text-ink-700 dark:text-ink-200">
                    {job.jdFileName || t('detail.jdFileFallbackName')}
                  </span>
                  <Eye className="h-3.5 w-3.5 shrink-0 text-ink-400" />
                </button>
                <p className="mt-2 text-xs text-ink-400">{t('detail.jdFileHint')}</p>
              </section>
            )}

            <HiringTeamPanel
              jobPostingId={job.id}
              hmSignOffStatus={job.hmSignOffStatus}
              hmSignOffReason={job.hmSignOffReason}
              // Hiring Manager KHÔNG tự thêm/gỡ người trong đội của mình — đó là việc của chủ tin.
              canManage={false}
              onChanged={() => refetch()}
            />
          </div>
        </div>
      </div>

      {decision && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink-950/60 p-4 backdrop-blur-sm">
          <div className="w-full max-w-lg overflow-hidden rounded-2xl border border-ink-200 bg-white shadow-2xl dark:border-white/10 dark:bg-ink-900">
            <div className="border-b border-ink-100 px-5 py-4 dark:border-white/10">
              <h3 className="text-base font-bold text-ink-900 dark:text-white">
                {decision.mode === 'approve' ? t('detail.approveTitle') : t('detail.rejectTitle')}
              </h3>
              <p className="mt-0.5 text-xs text-ink-500 dark:text-ink-400">
                {decision.app.candidateName}
              </p>
            </div>

            <div className="space-y-3 px-5 py-4">
              {/* Lỗi từ server phải hiện NGAY TRONG hộp thoại. Trước đây nó vẽ ở cấp trang, tức là
                  nằm dưới chính lớp phủ này — bấm "Duyệt" rồi không thấy gì xảy ra. */}
              {actionError && (
                <div className="flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-400">
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" />
                  <span>{actionError}</span>
                </div>
              )}

              {decision.mode === 'approve' ? (
                <>
                  <p className="text-sm font-medium text-ink-900 dark:text-white">
                    {t('detail.availabilityLabel')}
                  </p>
                  <p className="text-xs text-ink-500 dark:text-ink-400">
                    {t('detail.availabilityHint')}
                  </p>

                  {/* Khung đã bắt đầu vẫn còn hiệu lực nhưng không sửa được ở đây — hiện lại để HM
                      biết mình đã có lịch, khỏi gõ trùng. */}
                  {runningWindows.length > 0 && (
                    <div className="rounded-xl border border-sky-200 bg-sky-50 px-3 py-2 text-xs text-sky-800 dark:border-sky-500/25 dark:bg-sky-500/10 dark:text-sky-300">
                      <p className="font-semibold">{t('detail.runningWindows')}</p>
                      <ul className="mt-1 space-y-0.5">
                        {runningWindows.map((w) => (
                          <li key={w}>{w}</li>
                        ))}
                      </ul>
                    </div>
                  )}

                  <HmAvailabilityFields
                    rows={windows}
                    onChange={setWindows}
                    disabled={submit.isPending}
                  />
                </>
              ) : (
                <>
                  <label
                    htmlFor="hm-reject-reason"
                    className="block text-sm font-medium text-ink-900 dark:text-white"
                  >
                    {t('detail.rejectReasonLabel')}
                  </label>
                  <textarea
                    id="hm-reject-reason"
                    rows={3}
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    placeholder={t('detail.rejectReasonPlaceholder')}
                    className="w-full rounded-xl border border-ink-200 bg-white px-3 py-2 text-sm text-ink-900 placeholder:text-ink-400 focus:outline-none focus:ring-2 focus:ring-brand-500 dark:border-white/10 dark:bg-white/5 dark:text-white"
                  />
                </>
              )}
            </div>

            <div className="flex items-center justify-end gap-2 border-t border-ink-100 bg-ink-50/50 px-5 py-4 dark:border-white/10 dark:bg-white/5">
              <button
                type="button"
                onClick={() => setDecision(null)}
                className="rounded-xl px-4 py-2 text-sm font-semibold text-ink-600 hover:bg-ink-100 dark:text-ink-300 dark:hover:bg-white/10"
              >
                {t('detail.cancel')}
              </button>
              <button
                type="button"
                disabled={
                  submit.isPending ||
                  (decision.mode === 'approve'
                    ? // Có dòng SAI thì chặn hẳn; còn "không thêm khung nào" vẫn duyệt được khi lịch
                      // đã khai trước đó còn hiệu lực — bắt khai lại mỗi hồ sơ là biến một đợt duyệt
                      // mười người thành mười lần nhập lịch giống hệt nhau.
                      hasBlockingIssue(windows) ||
                      (toPayload(windows).length === 0 && runningWindows.length === 0)
                    : reason.trim().length === 0)
                }
                onClick={() =>
                  submit.mutate(
                    decision.mode === 'approve'
                      ? {
                          appId: decision.app.id,
                          mode: 'approve',
                          availabilities: toPayload(windows),
                        }
                      : { appId: decision.app.id, mode: 'reject', note: reason.trim() }
                  )
                }
                className={`inline-flex items-center gap-1.5 rounded-xl px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50 ${
                  decision.mode === 'approve'
                    ? 'bg-brand-600 hover:bg-brand-700'
                    : 'bg-red-600 hover:bg-red-700'
                }`}
              >
                {submit.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : decision.mode === 'approve' ? (
                  <Check className="h-4 w-4" />
                ) : (
                  <X className="h-4 w-4" />
                )}
                {decision.mode === 'approve'
                  ? t('detail.confirmApprove')
                  : t('detail.confirmReject')}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
