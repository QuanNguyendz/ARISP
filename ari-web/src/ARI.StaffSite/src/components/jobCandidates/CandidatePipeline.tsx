import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { motion } from 'framer-motion'
import {
  Users,
  ChevronRight,
  ChevronLeft,
  FileText,
  Mail,
  Phone,
  Loader2,
  Check,
  X,
  Send,
  ExternalLink,
  BarChart3,
  Download,
} from 'lucide-react'
import { DocumentPreview, downloadName } from '@ari/shared/document/DocumentViewer'
import { resolveAssetUrl } from '@ari/shared/config/constants'
import { cvPreviewUrl } from '@ari/shared/fservices/application'
import { HorizontalScrollArea } from '@ari/shared/ui'
import { formatScore } from '@ari/shared/utils/format'
import { roundTypeKey } from '@ari/shared/utils/roundTypes'
import OnlineTestAnswerSheetModal from './OnlineTestAnswerSheetModal'
import InterviewCodeCard from './InterviewCodeCard'
import InterviewResultsCard from '@/components/evaluations/InterviewResultsCard'
import ApplicationFormCard from '@/components/applications/ApplicationFormCard'
import CvScoreBadge from '@/components/cvScore/CvScoreBadge'
import { cvScoreTextClass } from '@/components/cvScore/cvTier'
import { useCvScoreText } from '@/components/cvScore/useCvScoreText'
import type { HrApplicationItem } from '@ari/shared/types/application'
import {
  buildStages,
  ageFrom,
  inviteTargetRound,
  isOnlineTestRound,
  needsInterviewCode,
  type PipelineStage,
  type RoundConfigLike,
} from './pipelineStages'

/**
 * Khối ứng viên của màn chi tiết tin tuyển dụng, dựng theo lối ATS quen thuộc:
 * **thanh bước quy trình có số đếm → danh sách theo bước → chọn một người thì mở hồ sơ kèm CV**.
 *
 * Vì sao tách khỏi trang: cùng khối này sẽ dùng lại được ở màn tin của HR Leader và Hiring Manager —
 * ba bản sao của cùng một bảng là ba chỗ phải nhớ sửa (bài học gộp hai màn Phỏng vấn ở ADR-058).
 *
 * **Component chỉ lo BỐ CỤC.** Mọi thao tác nghiệp vụ (duyệt hồ sơ, loại, mời phỏng vấn) đi vào qua
 * props và vẫn do trang mẹ thực thi — viết lại chúng ở đây là chép lại luật đã được kiểm chứng.
 */

export interface CandidatePipelineProps {
  apps: HrApplicationItem[]
  rounds: RoundConfigLike[]
  loading?: boolean

  /** Hồ sơ đang chạy một thao tác — để đúng nút đó quay vòng chờ. */
  processingAppId?: string | null

  /** Duyệt hồ sơ ở vòng CV. Không truyền = ẩn nút (vai trò không có quyền). */
  onApprove?: (app: HrApplicationItem) => void
  onReject?: (app: HrApplicationItem) => void

  /**
   * Mời phỏng vấn kèm xếp lịch. `round` là vòng CẦN xếp — do bảng này tính (`inviteTargetRound`)
   * để trang Recruiter và trang HR không mỗi nơi tự suy một kiểu: ở vòng trắc nghiệm, hồ sơ đã có
   * bài được mời sang VÒNG KẾ chứ không phải vòng đang đứng.
   */
  onInvite?: (app: HrApplicationItem, round: number) => void
  isInvitePending?: (app: HrApplicationItem) => boolean

  /** Đường tới trang hồ sơ đầy đủ của ứng viên. */
  candidateHref?: (app: HrApplicationItem) => string

  /**
   * Đường tới màn đánh giá của khu vực đang đứng (ADR-069). Có thì hồ sơ đã vào phễu phỏng vấn hiện khối
   * "Kết quả phỏng vấn" — từng vòng, ca đã gán, báo cáo AI, video, transcript — ngay cạnh CV. Trước đây
   * buổi phỏng vấn thật kết thúc là không còn đường nào từ màn tin tới báo cáo của nó.
   */
  evaluationHref?: (evaluationId: string) => string

  /** Nhãn trạng thái — mỗi khu vực có bộ chữ riêng nên nơi gọi tự truyền. */
  statusLabel: (status: string) => string

  /**
   * Lớp màu của chip trạng thái. Bỏ trống thì dùng bảng màu mặc định bên dưới — để màn tin của ba
   * vai trò tô CÙNG một màu cho cùng một trạng thái, thay vì mỗi nơi một kiểu.
   */
  statusBadge?: (status: string) => string

  /**
   * Nhãn của một VÒNG trên thanh bước. Bỏ trống thì dùng bản mặc định ngay trong component.
   *
   * Vì sao có mặc định: ba màn tin trước đây tự ghép chuỗi này theo ba cách, mỗi cách tra một
   * namespace riêng — và màn Hiring Manager tra nhầm khoá nên thanh bước in ra `detail.round 1`.
   * Nhãn của vòng thuộc về chính khối này, giống bảng màu trạng thái đã gom về đây trước đó.
   */
  roundLabel?: (r: RoundConfigLike) => string

  /**
   * Cổng duyệt shortlist của Hiring Manager (ADR-061/067) — hiện ngay tại hồ sơ ở bước
   * "Chờ HM duyệt", thay cho một màn danh sách riêng.
   *
   * Không truyền = chỉ hiện chữ "đang chờ Hiring Manager duyệt" như trước; đó là điều mà màn tin
   * của Recruiter và HR Leader cần thấy.
   */
  hmDecision?: {
    onApprove: (app: HrApplicationItem) => void
    onReject: (app: HrApplicationItem) => void
  }

  /**
   * Chọn nhiều hồ sơ rồi duyệt/loại một lượt — sàng CV là việc làm theo lô, bắt bấm từng người là
   * đổi một thao tác thành hàng chục. Không truyền = ẩn cột chọn.
   */
  selectedIds?: string[]
  onToggleSelect?: (id: string) => void
  onToggleSelectAll?: (idsOnScreen: string[], allSelected: boolean) => void
  onBatchApprove?: () => void
  onBatchReject?: () => void

  /**
   * Xếp lịch hàng loạt cho một vòng. Chỉ hiện ở các bước VÒNG — ở bước sàng CV thì thao tác đúng
   * là "duyệt" (và duyệt đã kèm xếp lịch theo ADR-059), không phải "mời".
   */
  onBatchInvite?: (roundNumber: number) => void

  batchBusy?: boolean

  /**
   * Đường tới bảng điểm bài trắc nghiệm của tin. Nút chỉ hiện khi đang đứng ở **bước vòng trắc
   * nghiệm** — đó là chỗ duy nhất bảng điểm trả lời một câu hỏi đang đặt ra; nằm trong màn ngân
   * hàng câu hỏi thì nó lạc giữa việc soạn đề, và người cần nó phải nhớ là mình từng thấy nó ở đó.
   *
   * Không truyền = ẩn nút (vai trò không đọc được bảng điểm — Hiring Manager là một ví dụ, cổng
   * `CanManageAsync` của endpoint đòi mức chủ tin).
   */
  onlineTestResultsHref?: string

  /**
   * Hiện thẻ "Mã vào phòng phỏng vấn" ở hồ sơ đang có lịch vòng hội thoại — Recruiter cấp mã ngay tại
   * danh sách. Không truyền = ẩn (Hiring Manager xem hồ sơ nhưng không phát mã; server cũng chặn).
   */
  canIssueInterviewCode?: boolean

  /**
   * Bề rộng tối thiểu của khối HỒ SƠ khi chỗ đặt không đủ rộng (màn tin của Hiring Manager chỉ dành
   * cho nó 2/3 chiều ngang, 1/3 còn lại là dải panel chuyên môn). Có giá trị thì khối hồ sơ giữ
   * nguyên bề rộng đó và **trượt ngang** để xem hết, kèm một thanh kéo ở trên.
   *
   * Chỉ bọc khối hồ sơ — thanh bước quy trình ở trên đã tự cuộn ngang theo cách riêng của nó, lồng
   * thêm một tầng trượt nữa là bắt người dùng kéo hai lần cho cùng một việc.
   */
  bodyMinWidthClass?: string
}

const CARD = 'rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card'

/**
 * Trạng thái mà hồ sơ CÓ THỂ đã có buổi phỏng vấn thật — từ lúc chờ xếp lịch trở đi, kể cả khi đã đóng
 * (không đạt, rút hồ sơ): báo cáo của các vòng đã qua vẫn phải mở được.
 */
const INTERVIEW_STAGES = new Set([
  'screening',
  'interview',
  'pass',
  'not_pass',
  'offer',
  'hired',
  'offer_declined',
  'withdrawn',
])

/**
 * Phần tử đang thực sự cuộn quanh `el`, hoặc `null` nếu đó là cả trang.
 *
 * Không giả định được: khối này dùng ở **cả hai layout** — `WorkspaceLayout` để BODY cuộn, còn
 * `HrLayout` cho chính `<main>` cuộn. Ghi cứng một trong hai là sai ở nửa số màn.
 */
function scrollParentOf(el: HTMLElement | null): HTMLElement | null {
  let node = el?.parentElement ?? null
  while (node) {
    const overflowY = getComputedStyle(node).overflowY
    // Phải cuộn dọc được THẬT. Một khung chỉ đặt `overflow-x: auto` (màn tin của Hiring Manager bọc
    // khối này trong một khung trượt ngang) vẫn bị CSS tính `overflow-y` thành `auto` — nhận nhầm nó
    // là phần tử cuộn thì lệnh bù bên dưới cộng vào một `scrollTop` không bao giờ nhúc nhích, và cả
    // trang lại giật đúng như lỗi mà đoạn này sinh ra để chữa.
    const scrollable = node.scrollHeight > node.clientHeight
    if ((overflowY === 'auto' || overflowY === 'scroll') && scrollable) return node
    node = node.parentElement
  }
  return null
}

/** Bảng màu chip trạng thái dùng chung cho mọi màn tin. */
const STATUS_TONES: Record<string, string> = {
  invited: 'bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400',
  cv_submitted: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
  hm_review: 'bg-sky-100 dark:bg-sky-500/20 text-sky-700 dark:text-sky-400',
  screening: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
  interview: 'bg-ai-100 dark:bg-ai-500/20 text-ai-700 dark:text-ai-400',
  pass: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
  not_pass: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
  cv_rejected: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
  offer: 'bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-400',
  // "Đã nhận việc" là đích của cả phễu — cho nó sắc riêng, không dùng chung với "Đạt".
  hired: 'bg-teal-100 dark:bg-teal-500/20 text-teal-700 dark:text-teal-400',
  offer_declined: 'bg-orange-100 dark:bg-orange-500/20 text-orange-700 dark:text-orange-400',
  withdrawn: 'bg-ink-100 dark:bg-white/10 text-ink-500 dark:text-ink-400',
}

const defaultStatusBadge = (s: string) =>
  STATUS_TONES[s] ?? 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400'

/**
 * Màu cho TRẠNG THÁI CHI TIẾT của vòng đang diễn ra (`stageStatus`, ADR-067).
 *
 * Không dùng chung bảng với `status`: hai thứ trả lời hai câu khác nhau — `status` là "hồ sơ ở khúc
 * nào của phễu", `stageStatus` là "đang chờ việc gì". Cùng một hồ sơ `interview` có thể đang chờ
 * ứng viên xác nhận lịch, đang ngồi làm bài, hay đang trong phòng phỏng vấn.
 */
const STAGE_TONES: Record<string, string> = {
  cv_pending: 'bg-blue-100 dark:bg-blue-500/20 text-blue-700 dark:text-blue-400',
  hm_review: 'bg-sky-100 dark:bg-sky-500/20 text-sky-700 dark:text-sky-400',
  awaiting_schedule: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
  schedule_pending: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
  schedule_confirmed: 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400',
  schedule_declined: 'bg-orange-100 dark:bg-orange-500/20 text-orange-700 dark:text-orange-400',
  test_open: 'bg-ai-100 dark:bg-ai-500/20 text-ai-700 dark:text-ai-400',
  test_submitted: 'bg-violet-100 dark:bg-violet-500/20 text-violet-700 dark:text-violet-400',
  test_expired: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
  interview_waiting: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
  // Đang phỏng vấn = việc đang diễn ra ngay lúc này → sắc mạnh nhất trên bảng.
  interview_active: 'bg-brand-600 text-white dark:bg-brand-500',
  interview_done: 'bg-ai-100 dark:bg-ai-500/20 text-ai-700 dark:text-ai-400',
  pending_result: 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400',
  missed: 'bg-red-100 dark:bg-red-500/20 text-red-700 dark:text-red-400',
}

/**
 * Chip trạng thái: ưu tiên `stageStatus`, rơi về `status` khi server chưa trả (dữ liệu cũ, hoặc
 * bước tính bị lỗi). Nhãn tra qua i18n với `defaultValue` nên một khoá lạ hiện ra chính nó thay vì
 * làm trống ô.
 */
function stageChip(
  app: HrApplicationItem,
  statusLabel: (s: string) => string,
  statusBadge: (s: string) => string,
  t: (k: string, o?: Record<string, unknown>) => string
) {
  const stage = app.stageStatus
  if (!stage) return { label: statusLabel(app.status), tone: statusBadge(app.status) }
  // Trạng thái đã đóng thì `stageStatus` chính là `status` — dùng lại nhãn/màu sẵn có của phễu.
  if (stage === app.status) return { label: statusLabel(app.status), tone: statusBadge(app.status) }
  return {
    label: t(`stage.${stage}`, { defaultValue: statusLabel(app.status) }),
    tone: STAGE_TONES[stage] ?? statusBadge(app.status),
  }
}

/** Chữ cái đầu để làm avatar khi ứng viên chưa có ảnh. */
const initials = (name?: string | null) =>
  (name || '?')
    .trim()
    .split(/\s+/)
    .map((w) => w[0])
    .slice(-2)
    .join('')
    .toUpperCase()

/** Màu avatar suy ra từ tên — cùng người thì luôn cùng màu, không nhấp nháy giữa các lần render. */
const AVATAR_TONES = [
  'bg-brand-100 text-brand-700 dark:bg-brand-500/20 dark:text-brand-300',
  'bg-ai-100 text-ai-700 dark:bg-ai-500/20 dark:text-ai-300',
  'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-300',
  'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-300',
  'bg-sky-100 text-sky-700 dark:bg-sky-500/20 dark:text-sky-300',
]
const toneFor = (seed: string) => {
  let sum = 0
  for (let i = 0; i < seed.length; i += 1) sum += seed.charCodeAt(i)
  return AVATAR_TONES[sum % AVATAR_TONES.length]
}

export default function CandidatePipeline({
  apps,
  rounds,
  loading,
  processingAppId,
  onApprove,
  onReject,
  onInvite,
  isInvitePending,
  candidateHref,
  evaluationHref,
  statusLabel,
  statusBadge = defaultStatusBadge,
  roundLabel,
  hmDecision,
  selectedIds,
  onToggleSelect,
  onToggleSelectAll,
  onBatchApprove,
  onBatchReject,
  onBatchInvite,
  batchBusy,
  onlineTestResultsHref,
  canIssueInterviewCode,
  bodyMinWidthClass,
}: CandidatePipelineProps) {
  const { t } = useTranslation('modules/staff/candidatePipeline')

  /** "Vòng 2 (Chuyên môn)" — một cách gọi duy nhất cho cả ba màn tin. */
  const defaultRoundLabel = useCallback(
    (r: RoundConfigLike) => {
      const key = roundTypeKey(r.roundType)
      const type = key === 'other' ? '' : t(`pipeline.roundTypes.${key}`)
      return `${t('pipeline.round', { number: r.roundNumber })}${type ? ` (${type})` : ''}`
    },
    [t]
  )

  const resolveRoundLabel = roundLabel ?? defaultRoundLabel

  const stages = useMemo(
    () =>
      buildStages(apps, rounds, {
        rejected: t('pipeline.rejected'),
        newApplicants: t('pipeline.newApplicants'),
        hmReview: t('pipeline.hmReview'),
        scheduling: t('pipeline.scheduling'),
        passed: t('pipeline.passed'),
        offer: t('pipeline.offer'),
        hired: t('pipeline.hired'),
        roundLabel: resolveRoundLabel,
      }),
    [apps, rounds, t, resolveRoundLabel]
  )

  /**
   * Bước đang xem. Mặc định là bước ĐẦU TIÊN CÒN HỒ SƠ (bỏ qua thùng loại) — mở màn ra mà thấy một
   * bảng trống trong khi phễu đang có người là câu trả lời sai cho câu hỏi "giờ tôi phải làm gì".
   */
  const [stageId, setStageId] = useState<string | null>(null)
  const activeStage: PipelineStage =
    stages.find((s) => s.id === stageId) ??
    stages.find((s) => !s.isRejectedBucket && s.count > 0) ??
    stages[1] ??
    stages[0]

  /**
   * Vòng mà thao tác "Xếp lịch" hàng loạt sẽ nhắm tới, hoặc `null` nếu bước này không xếp lịch được.
   *
   * Bước "Chờ xếp lịch" (`scheduling`) tính là VÒNG 1: đó chính là chỗ Recruiter nhận lại hồ sơ sau
   * khi Hiring Manager duyệt, và việc duy nhất còn lại là ấn định ca cho vòng đầu (ADR-067).
   */
  const schedulingRound = useMemo(() => {
    const id = activeStage?.id ?? ''
    if (id === 'scheduling') return 1
    const m = /^round_(\d+)$/.exec(id)
    return m ? Number(m[1]) : null
  }, [activeStage])

  /** Đang đứng ở bước vòng TRẮC NGHIỆM — quyết định cột điểm và nút bảng điểm bên dưới. */
  const onTestRound = isOnlineTestRound(activeStage)

  /**
   * Vòng mà nút "Xếp lịch" hàng loạt sẽ xếp cho NHÓM đang chọn — cùng luật với nút mời từng người
   * (`inviteTargetRound`), nên ở bước trắc nghiệm, người đã có bài được xếp sang vòng kế.
   *
   * Nhóm lẫn hai vòng khác nhau (vd một người cần xếp lại bài thi, một người cần sang vòng 2) thì
   * KHÔNG đoán: trả `null` để nút tắt kèm lời giải thích. Một hộp xếp lịch chỉ xếp được một vòng.
   */
  const batchInviteRound = useMemo(() => {
    if (schedulingRound == null) return null
    const chosen = (activeStage?.items ?? []).filter((a) => (selectedIds ?? []).includes(a.id))
    if (chosen.length === 0) return schedulingRound
    const targets = Array.from(new Set(chosen.map((a) => inviteTargetRound(a, rounds))))
    return targets.length === 1 ? targets[0] : null
  }, [schedulingRound, activeStage, selectedIds, rounds])

  /**
   * Hồ sơ đang mở bài làm. Điểm tổng không nói được "sai ở đâu", mà đó mới là câu người sàng
   * lọc hỏi trước khi quyết định giữ hay loại.
   */
  const [answerSheetOf, setAnswerSheetOf] = useState<HrApplicationItem | null>(null)

  const activeStageItemIds = useMemo(
    () => new Set((activeStage?.items ?? []).map((a) => a.id)),
    [activeStage]
  )

  /** Chỉ đếm trong BƯỚC ĐANG XEM: bấm duyệt hàng loạt mà nuốt theo cả người ở bước khác là tai nạn. */
  const selectedCount = useMemo(
    () => (selectedIds ?? []).filter((id) => activeStageItemIds.has(id)).length,
    [selectedIds, activeStageItemIds]
  )

  const [selectedId, setSelectedId] = useState<string | null>(null)

  /**
   * GIỮ NGUYÊN VỊ TRÍ khi đóng/mở khối chi tiết.
   *
   * Bỏ chi tiết thì nội dung ngắn hẳn lại (mất khung CV cao 32rem), chiều cao cuộn tụt xuống và trình
   * duyệt **kẹp `scrollTop`** — cả trang trượt lên, trông như vừa tải lại màn. Ghi lại khoảng cách từ
   * đỉnh khối tới mép trên khung nhìn trước khi đổi, rồi bù lại đúng bằng đó sau khi bố cục mới đã vẽ.
   *
   * `useLayoutEffect` chứ không `useEffect`: phải bù **trước khi trình duyệt vẽ**, nếu không người dùng
   * nhìn thấy một nháy giật rồi mới về chỗ.
   */
  const rootRef = useRef<HTMLDivElement>(null)
  const anchorTop = useRef<number | null>(null)

  const changeSelection = (next: string | null) => {
    anchorTop.current = rootRef.current?.getBoundingClientRect().top ?? null
    setSelectedId(next)
  }

  useLayoutEffect(() => {
    const want = anchorTop.current
    anchorTop.current = null
    if (want == null || !rootRef.current) return

    const delta = rootRef.current.getBoundingClientRect().top - want
    if (Math.abs(delta) < 1) return

    const scroller = scrollParentOf(rootRef.current)
    if (scroller) scroller.scrollTop += delta
    else window.scrollBy(0, delta)
  }, [selectedId])
  const selected = activeStage?.items.find((a) => a.id === selectedId) ?? null

  // Đổi bước thì bỏ chọn: giữ lại hồ sơ của bước cũ sẽ hiện chi tiết một người không còn trong danh
  // sách bên trái — trông như hai màn khác nhau ghép lại.
  useEffect(() => {
    setSelectedId(null)
  }, [activeStage?.id])

  if (loading) {
    return (
      <div className={`${CARD} flex items-center gap-2 p-10 text-sm text-ink-500`}>
        <Loader2 className="h-4 w-4 animate-spin" /> {t('pipeline.loading')}
      </div>
    )
  }

  /* Khối hồ sơ: thanh bước ở trên đứng ngoài, vì chỉ khối này mới cần trượt ngang. */
  const body = (
    <div className={CARD}>
      <div className="flex items-center justify-between gap-3 border-b border-ink-100 px-5 py-4 dark:border-white/10">
        <h2 className="flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
          <Users className="h-5 w-5 text-brand-600 dark:text-brand-400" />
          {activeStage?.label}
          <span className="text-sm font-normal text-ink-400">
            {t('pipeline.resultCount', { count: activeStage?.count ?? 0 })}
          </span>
        </h2>

        <div className="flex items-center gap-2">
          {/* Bảng điểm nằm ở ĐÚNG vòng trắc nghiệm, không phải trong màn ngân hàng câu hỏi: nó
              trả lời câu hỏi của người đang nhìn danh sách ứng viên, không phải của người đang
              soạn đề. */}
          {onTestRound && onlineTestResultsHref && (
            <a
              href={onlineTestResultsHref}
              className="inline-flex items-center gap-1.5 rounded-xl border border-ink-200 px-3 py-1.5 text-xs font-medium text-ink-600 hover:bg-ink-50 dark:border-white/10 dark:text-ink-300 dark:hover:bg-white/10"
            >
              <BarChart3 className="h-3.5 w-3.5" /> {t('pipeline.testResults')}
            </a>
          )}

          {selected && (
            <button
              onClick={() => changeSelection(null)}
              className="inline-flex items-center gap-1.5 rounded-xl border border-ink-200 px-3 py-1.5 text-xs font-medium text-ink-600 hover:bg-ink-50 dark:border-white/10 dark:text-ink-300 dark:hover:bg-white/10"
            >
              <ChevronLeft className="h-3.5 w-3.5" /> {t('pipeline.backToList')}
            </button>
          )}
        </div>
      </div>

      {activeStage?.count === 0 ? (
        <div className="flex flex-col items-center gap-2 py-14 text-center">
          <Users className="h-8 w-8 text-ink-300" />
          <p className="text-sm text-ink-500 dark:text-ink-400">{t('pipeline.emptyStage')}</p>
        </div>
      ) : selected ? (
        /* Đã chọn một người: danh sách thu về cột hẹp bên trái, hồ sơ + CV chiếm phần còn lại —
           vẫn chuyển qua lại giữa các ứng viên bằng một cú bấm, không phải quay ra quay vào. */
        <div className="grid grid-cols-1 lg:grid-cols-[minmax(0,17rem)_minmax(0,1fr)]">
          <CandidateRail
            items={activeStage.items}
            selectedId={selected.id}
            onPick={changeSelection}
          />
          <CandidateDetail
            app={selected}
            statusLabel={statusLabel}
            statusBadge={statusBadge}
            processingAppId={processingAppId}
            onApprove={onApprove}
            onReject={onReject}
            onInvite={onInvite}
            inviteRound={inviteTargetRound(selected, rounds)}
            isInvitePending={isInvitePending}
            candidateHref={candidateHref}
            evaluationHref={evaluationHref}
            hmDecision={hmDecision}
            showInterviewCode={!!canIssueInterviewCode && needsInterviewCode(selected, rounds)}
          />
        </div>
      ) : (
        <>
          {/* Chỉ hiện khi có người được chọn — một thanh luôn nằm đó mà không bấm được chỉ tốn chỗ. */}
          {selectedCount > 0 && (
            <div className="flex flex-wrap items-center gap-2 border-b border-ink-100 bg-brand-50/60 px-5 py-3 dark:border-white/10 dark:bg-brand-500/10">
              <span className="text-sm font-medium text-ink-700 dark:text-ink-200">
                {t('pipeline.selectedCount', { count: selectedCount })}
              </span>
              <div className="ml-auto flex flex-wrap gap-2">
                {/* Xếp lịch có nghĩa ở bước "Chờ xếp lịch" (vòng 1) và ở từng bước VÒNG.
                    Ở bước sàng CV thì thao tác đúng là duyệt, không phải mời. */}
                {schedulingRound != null && onBatchInvite && (
                  <button
                    disabled={batchBusy || batchInviteRound == null}
                    onClick={() => batchInviteRound != null && onBatchInvite(batchInviteRound)}
                    title={batchInviteRound == null ? t('actions.scheduleMixedRounds') : undefined}
                    className="inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
                  >
                    <Send className="h-4 w-4" />{' '}
                    {batchInviteRound != null && batchInviteRound > schedulingRound
                      ? t('actions.scheduleNextRound', { round: batchInviteRound })
                      : t('actions.schedule')}
                  </button>
                )}
                {activeStage?.id === 'new' && onBatchApprove && (
                  <button
                    disabled={batchBusy}
                    onClick={onBatchApprove}
                    className="inline-flex items-center gap-1.5 rounded-xl bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white disabled:opacity-60"
                  >
                    {batchBusy ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Check className="h-4 w-4" />
                    )}
                    {t('actions.approve')}
                  </button>
                )}
                {onBatchReject && (
                  <button
                    disabled={batchBusy}
                    onClick={onBatchReject}
                    className="inline-flex items-center gap-1.5 rounded-xl border border-red-200 px-3 py-1.5 text-sm font-medium text-red-600 disabled:opacity-60 dark:border-red-500/30"
                  >
                    <X className="h-4 w-4" /> {t('actions.reject')}
                  </button>
                )}
              </div>
            </div>
          )}

          <CandidateTable
            items={activeStage.items}
            onPick={changeSelection}
            statusLabel={statusLabel}
            statusBadge={statusBadge}
            selectedIds={selectedIds}
            onToggleSelect={onToggleSelect}
            onToggleSelectAll={onToggleSelectAll}
            showTestScore={onTestRound}
            onViewTest={setAnswerSheetOf}
          />
        </>
      )}
    </div>
  )

  return (
    <div ref={rootRef} className="space-y-4">
      {answerSheetOf && (
        <OnlineTestAnswerSheetModal
          applicationId={answerSheetOf.id}
          candidateName={answerSheetOf.candidateName || t('candidate')}
          onClose={() => setAnswerSheetOf(null)}
        />
      )}

      <StageBar stages={stages} activeId={activeStage?.id} onPick={setStageId} />

      {bodyMinWidthClass ? (
        <HorizontalScrollArea innerClassName={bodyMinWidthClass}>{body}</HorizontalScrollArea>
      ) : (
        body
      )}
    </div>
  )
}

/* ===================== Thanh bước quy trình ===================== */

/**
 * Cuộn NGANG chứ không xuống dòng: thứ tự trái → phải chính là dòng chảy của phễu, cho nó rớt dòng
 * là mất đúng thông tin mà thanh này sinh ra để truyền đạt.
 */
function StageBar({
  stages,
  activeId,
  onPick,
}: {
  stages: PipelineStage[]
  activeId?: string
  onPick: (id: string) => void
}) {
  const scroller = useRef<HTMLDivElement>(null)

  return (
    <div className={`${CARD} p-3`}>
      <div
        ref={scroller}
        className="flex items-stretch gap-1 overflow-x-auto pb-1 [scrollbar-width:thin]"
      >
        {stages.map((s, i) => {
          const active = s.id === activeId
          const tone = s.isRejectedBucket
            ? 'text-ink-500 dark:text-ink-400'
            : s.isSuccess
              ? 'text-emerald-700 dark:text-emerald-400'
              : 'text-ink-700 dark:text-ink-200'

          return (
            <div key={s.id} className="flex shrink-0 items-center">
              {/* Thùng "không phù hợp" nằm ngoài dòng chảy nên mũi tên quay NGƯỢC lại. */}
              {i === 1 && <ChevronLeft className="mx-0.5 h-4 w-4 shrink-0 text-ink-300" />}
              {i > 1 && <ChevronRight className="mx-0.5 h-4 w-4 shrink-0 text-ink-300" />}

              <button
                onClick={() => onPick(s.id)}
                aria-current={active ? 'true' : undefined}
                className={`min-w-[7.5rem] rounded-xl px-3 py-2 text-center transition-all ${
                  active
                    ? 'bg-gradient-to-r from-brand-600 to-ai-600 text-white shadow-sm'
                    : 'hover:bg-ink-50 dark:hover:bg-white/10'
                }`}
              >
                <span
                  className={`block truncate text-xs font-semibold ${active ? 'text-white' : tone}`}
                >
                  {s.label}
                </span>
                <span
                  className={`mt-0.5 block text-sm font-bold ${
                    active ? 'text-white' : s.count > 0 ? 'text-ink-900 dark:text-white' : 'text-ink-300'
                  }`}
                >
                  {s.count}
                </span>
              </button>
            </div>
          )
        })}
      </div>
    </div>
  )
}

/* ===================== Bảng ứng viên ===================== */

function CandidateTable({
  items,
  onPick,
  statusLabel,
  statusBadge,
  selectedIds,
  onToggleSelect,
  onToggleSelectAll,
  showTestScore,
  onViewTest,
}: {
  items: HrApplicationItem[]
  onPick: (id: string) => void
  statusLabel: (s: string) => string
  statusBadge: (s: string) => string
  selectedIds?: string[]
  onToggleSelect?: (id: string) => void
  onToggleSelectAll?: (idsOnScreen: string[], allSelected: boolean) => void
  /** Ở vòng trắc nghiệm: thêm cột điểm bài thi — con số cả vòng đó quy về. */
  showTestScore?: boolean
  /** Mở bài làm chi tiết (từng câu, đáp án đã khoanh, đúng/sai). */
  onViewTest?: (app: HrApplicationItem) => void
}) {
  const { t } = useTranslation('modules/staff/candidatePipeline')

  const selectable = onToggleSelect != null
  const ids = items.map((a) => a.id)
  const allSelected = selectable && ids.length > 0 && ids.every((id) => selectedIds?.includes(id))

  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[56rem] text-left">
        <thead>
          <tr className="border-b border-ink-100 text-xs uppercase tracking-wider text-ink-400 dark:border-white/10">
            {selectable && (
              <th className="w-10 px-5 py-3">
                <input
                  type="checkbox"
                  checked={allSelected}
                  onChange={() => onToggleSelectAll?.(ids, allSelected)}
                  aria-label={t('pipeline.selectPage')}
                  className="h-4 w-4 accent-brand-600"
                />
              </th>
            )}
            <th className="px-5 py-3 font-medium">{t('pipeline.colCandidate')}</th>
            <th className="px-4 py-3 font-medium">{t('pipeline.colContact')}</th>
            <th className="px-4 py-3 font-medium">{t('pipeline.colArea')}</th>
            <th className="px-4 py-3 font-medium">{t('pipeline.colAge')}</th>
            <th className="px-4 py-3 font-medium">{t('pipeline.colMatch')}</th>
            {showTestScore && (
              <th className="px-4 py-3 font-medium">{t('pipeline.colTestScore')}</th>
            )}
            <th className="px-4 py-3 font-medium">{t('pipeline.colStatus')}</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-ink-100 dark:divide-white/10">
          {items.map((a, i) => {
            const age = ageFrom(a.candidateDateOfBirth)
            return (
              <motion.tr
                key={a.id}
                initial={{ opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: Math.min(i * 0.03, 0.3) }}
                onClick={() => onPick(a.id)}
                className="cursor-pointer hover:bg-ink-50 dark:hover:bg-white/5"
              >
                {selectable && (
                  <td className="px-5 py-3" onClick={(e) => e.stopPropagation()}>
                    <input
                      type="checkbox"
                      checked={selectedIds?.includes(a.id) ?? false}
                      onChange={() => onToggleSelect?.(a.id)}
                      aria-label={a.candidateName || t('candidate')}
                      className="h-4 w-4 accent-brand-600"
                    />
                  </td>
                )}
                <td className="px-5 py-3">
                  <div className="flex items-center gap-3">
                    <span
                      className={`grid h-9 w-9 shrink-0 place-items-center rounded-full text-xs font-bold ${toneFor(a.candidateName || a.id)}`}
                    >
                      {initials(a.candidateName)}
                    </span>
                    <div className="min-w-0">
                      <p className="truncate text-sm font-medium text-ink-900 dark:text-white">
                        {a.candidateName || t('candidate')}
                      </p>
                      <p className="truncate text-xs text-ink-400">
                        {new Date(a.createdAt).toLocaleDateString('vi-VN')}
                      </p>
                    </div>
                  </div>
                </td>
                <td className="px-4 py-3">
                  <p className="truncate text-sm text-ink-700 dark:text-ink-200">
                    {a.candidatePhone || '—'}
                  </p>
                  <p className="truncate text-xs text-ink-400">{a.candidateEmail}</p>
                </td>
                <td className="px-4 py-3 text-sm text-ink-600 dark:text-ink-300">
                  {a.candidateLocation || '—'}
                </td>
                <td className="whitespace-nowrap px-4 py-3 text-sm text-ink-600 dark:text-ink-300">
                  {age != null ? t('pipeline.ageValue', { count: age }) : '—'}
                </td>
                <td className="px-4 py-3">
                  <CvScoreBadge
                    score={a.matchScore}
                    status={a.cvScoreStatus}
                    retryAt={a.cvScoreRetryAt}
                    gateStatus={a.cvGateStatus}
                    className={`text-sm font-semibold ${cvScoreTextClass(a.cvRecommendation)}`}
                  />
                </td>
                {showTestScore && (
                  <td className="whitespace-nowrap px-4 py-3">
                    {a.onlineTestScore != null ? (
                      <div className="flex flex-col gap-0.5">
                        {/* Điểm và KẾT QUẢ đi cùng nhau: một con số trần bắt người đọc tự nhớ điểm
                            sàn rồi tự so. Đạt hay trượt ở đây KHÔNG tự đổi trạng thái hồ sơ — loại hay
                            giữ vẫn là quyết định của Recruiter. */}
                        <span
                          className={`inline-flex w-fit items-center rounded-full px-2 py-0.5 text-xs font-semibold ${
                            a.onlineTestPassed
                              ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400'
                              : 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400'
                          }`}
                        >
                          {/* `formatScore` ĐÃ kèm "/100" — nối thêm nữa ra "40/100/100". */}
                          {a.onlineTestPassed ? t('pipeline.testPassed') : t('pipeline.testFailed')}{' '}
                          · {formatScore(a.onlineTestScore)}
                        </span>
                        {/* Bài hệ thống nộp thay khi hết hạn: "0 điểm do không làm" khác hẳn "0 điểm
                            do làm sai hết" với người quyết định loại hay giữ. */}
                        {a.onlineTestExpired && (
                          <span className="inline-flex w-fit items-center rounded-full bg-ink-100 px-2 py-0.5 text-[11px] font-semibold text-ink-600 dark:bg-white/10 dark:text-ink-300">
                            {t('pipeline.testExpiredAuto')}
                          </span>
                        )}
                        {onViewTest && (
                          <button
                            type="button"
                            onClick={(e) => {
                              e.stopPropagation()
                              onViewTest(a)
                            }}
                            className="w-fit text-xs font-medium text-brand-600 hover:underline dark:text-brand-400"
                          >
                            {t('pipeline.viewAnswers')}
                          </button>
                        )}
                      </div>
                    ) : (
                      <span className="text-sm text-ink-300">{t('pipeline.testNotTaken')}</span>
                    )}
                  </td>
                )}
                <td className="px-4 py-3">
                  <span
                    className={`inline-flex whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-medium ${stageChip(a, statusLabel, statusBadge, t).tone}`}
                  >
                    {stageChip(a, statusLabel, statusBadge, t).label}
                  </span>
                </td>
              </motion.tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

/* ===================== Cột danh sách khi đã chọn ===================== */

function CandidateRail({
  items,
  selectedId,
  onPick,
}: {
  items: HrApplicationItem[]
  selectedId: string
  onPick: (id: string) => void
}) {
  const { t } = useTranslation('modules/staff/candidatePipeline')

  return (
    <div className="max-h-[40rem] overflow-y-auto border-b border-ink-100 dark:border-white/10 lg:border-b-0 lg:border-r">
      {items.map((a) => {
        const active = a.id === selectedId
        return (
          <button
            key={a.id}
            onClick={() => onPick(a.id)}
            className={`flex w-full items-center gap-3 px-4 py-3 text-left transition-colors ${
              active
                ? 'bg-brand-50 dark:bg-brand-500/10'
                : 'hover:bg-ink-50 dark:hover:bg-white/5'
            }`}
          >
            <span
              className={`grid h-8 w-8 shrink-0 place-items-center rounded-full text-[11px] font-bold ${toneFor(a.candidateName || a.id)}`}
            >
              {initials(a.candidateName)}
            </span>
            <span className="min-w-0">
              <span className="block truncate text-sm font-medium text-ink-900 dark:text-white">
                {a.candidateName || t('candidate')}
              </span>
              <span className="block truncate text-xs text-ink-400">
                {new Date(a.createdAt).toLocaleDateString('vi-VN')}
              </span>
            </span>
          </button>
        )
      })}
    </div>
  )
}

/* ===================== Hồ sơ + CV ===================== */

function CandidateDetail({
  app,
  statusLabel,
  statusBadge,
  processingAppId,
  onApprove,
  onReject,
  onInvite,
  inviteRound,
  isInvitePending,
  candidateHref,
  evaluationHref,
  hmDecision,
  showInterviewCode,
}: {
  app: HrApplicationItem
  statusLabel: (s: string) => string
  statusBadge: (s: string) => string
  processingAppId?: string | null
  onApprove?: (a: HrApplicationItem) => void
  onReject?: (a: HrApplicationItem) => void
  onInvite?: (a: HrApplicationItem, round: number) => void
  /** Vòng mà nút mời sẽ xếp lịch — xem `inviteTargetRound`. */
  inviteRound: number
  isInvitePending?: (a: HrApplicationItem) => boolean
  candidateHref?: (a: HrApplicationItem) => string
  evaluationHref?: (evaluationId: string) => string
  hmDecision?: {
    onApprove: (a: HrApplicationItem) => void
    onReject: (a: HrApplicationItem) => void
  }
  /** Hiện thẻ cấp mã vào phòng — xem `needsInterviewCode`. */
  showInterviewCode?: boolean
}) {
  const { t } = useTranslation('modules/staff/candidatePipeline')
  const busy = processingAppId === app.id
  const cvScoreText = useCvScoreText()

  // Vòng CV thì duyệt/loại; đã vào phễu phỏng vấn thì mời lịch/loại. Cùng luật với bảng cũ, chỉ đổi
  // chỗ đặt nút — trang mẹ vẫn là nơi thực thi.
  /**
   * Thao tác nào có nghĩa ở đây suy từ TRẠNG THÁI hồ sơ, không suy từ `currentRound` (ADR-067).
   *
   * Vì sao đổi: hồ sơ ở bước "Chờ xếp lịch" (`screening`) chưa vào vòng nào nên `currentRound` vẫn
   * rỗng — theo cách cũ nó hiện nút "Duyệt hồ sơ" lần thứ hai cho một hồ sơ đã duyệt xong, còn nút
   * thật sự cần (xếp lịch) thì không thấy đâu.
   */
  const canApproveCv = app.status === 'cv_submitted' || app.status === 'invited'
  const awaitingHm = app.status === 'hm_review'
  const canSchedule = app.status === 'screening' || app.status === 'interview'
  const closed = ['cv_rejected', 'not_pass', 'offer_declined', 'withdrawn', 'failed', 'rejected'].includes(
    app.status
  )

  return (
    <div className="min-w-0 p-5">
      <div className="mb-4 flex flex-wrap items-start justify-between gap-3">
        <div className="flex items-center gap-3">
          <span
            className={`grid h-12 w-12 shrink-0 place-items-center rounded-full text-sm font-bold ${toneFor(app.candidateName || app.id)}`}
          >
            {initials(app.candidateName)}
          </span>
          <div className="min-w-0">
            <h3 className="truncate text-lg font-semibold text-ink-900 dark:text-white">
              {app.candidateName || t('candidate')}
            </h3>
            {app.candidateHeadline && (
              <p className="truncate text-sm text-ink-500 dark:text-ink-400">
                {app.candidateHeadline}
              </p>
            )}
          </div>
        </div>

        <span
          className={`inline-flex whitespace-nowrap rounded-full px-3 py-1 text-xs font-medium ${stageChip(app, statusLabel, statusBadge, t).tone}`}
        >
          {stageChip(app, statusLabel, statusBadge, t).label}
        </span>
      </div>

      {/* ---- Thông tin cá nhân ----
          Khu vực và độ tuổi lấy từ hồ sơ cá nhân của ứng viên — hai ô KHÔNG bắt buộc mà hầu hết
          không ai điền, nên chúng gần như luôn in ra "—" và chỉ tốn chỗ trong một panel vốn đã hẹp.
          Ai cần vẫn xem được ở trang hồ sơ đầy đủ. */}
      <dl className="mb-4 grid grid-cols-1 gap-x-6 gap-y-2 sm:grid-cols-2">
        <Row icon={<Phone className="h-3.5 w-3.5" />} label={t('pipeline.colContact')} value={app.candidatePhone} />
        <Row icon={<Mail className="h-3.5 w-3.5" />} label="Email" value={app.candidateEmail} />
        <Row label={t('pipeline.appliedAt')} value={new Date(app.createdAt).toLocaleDateString('vi-VN')} />
        <Row
          label={t('pipeline.colMatch')}
          value={cvScoreText(app.matchScore, app.cvScoreStatus)}
        />
      </dl>

      {app.candidateSkills && app.candidateSkills.length > 0 && (
        <div className="mb-4 flex flex-wrap gap-1.5">
          {app.candidateSkills.map((s) => (
            <span
              key={s}
              className="rounded-full bg-ink-100 px-2.5 py-1 text-xs text-ink-600 dark:bg-white/10 dark:text-ink-300"
            >
              {s}
            </span>
          ))}
        </div>
      )}

      {/* ---- Ứng viên tự khai gì lúc nộp hồ sơ ----
          Thư giới thiệu, thời gian báo trước khi nghỉ và kết quả đối chiếu thông tin với CV: ba thứ
          nằm sẵn trong DB từ lúc ứng tuyển mà không màn nào của nhân sự kê ra. */}
      <div className="mb-4 rounded-xl border border-ink-200 p-4 dark:border-white/10">
        <ApplicationFormCard app={app} defaultOpen={false} />
      </div>

      {/* ---- Nút thao tác: giữ nguyên luật của trang, chỉ đổi chỗ đặt ---- */}
      {!closed && (
        <div className="mb-4 flex flex-wrap gap-2">
          {canApproveCv && onApprove && (
            <button
              disabled={busy}
              onClick={() => onApprove(app)}
              className="inline-flex items-center gap-1.5 rounded-xl bg-emerald-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-60"
            >
              {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
              {t('actions.approve')}
            </button>
          )}

          {/* Đang ở bàn của Hiring Manager.
              - Chính HM đang xem (`hmDecision` được truyền) → hiện hai nút quyết định NGAY tại hồ sơ,
                cạnh CV mà họ vừa đọc. Trước đây phải sang một màn danh sách riêng, ở đó chỉ có tên
                và điểm CV — quyết định chuyên môn mà không nhìn thấy hồ sơ.
              - Người khác đang xem → nói rõ đang chờ ai, thay vì một hàng nút trống không giải thích. */}
          {awaitingHm && hmDecision && (
            <>
              <button
                disabled={busy}
                onClick={() => hmDecision.onApprove(app)}
                className="inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-3 py-2 text-sm font-medium text-white hover:bg-brand-700 disabled:opacity-60"
              >
                {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
                {t('actions.hmApprove')}
              </button>
              <button
                disabled={busy}
                onClick={() => hmDecision.onReject(app)}
                className="inline-flex items-center gap-1.5 rounded-xl border border-red-200 px-3 py-2 text-sm font-medium text-red-600 disabled:opacity-60 dark:border-red-500/30"
              >
                <X className="h-4 w-4" /> {t('actions.hmReject')}
              </button>
            </>
          )}

          {awaitingHm && !hmDecision && (
            <span className="inline-flex items-center gap-1.5 rounded-xl bg-sky-50 px-3 py-2 text-sm text-sky-700 dark:bg-sky-500/10 dark:text-sky-300">
              {t('pipeline.awaitingHm')}
            </span>
          )}

          {canSchedule && onInvite && (
            <button
              disabled={busy || isInvitePending?.(app)}
              onClick={() => onInvite(app, inviteRound)}
              className="inline-flex items-center gap-1.5 rounded-xl bg-brand-600 px-3 py-2 text-sm font-medium text-white disabled:opacity-60"
            >
              <Send className="h-4 w-4" />{' '}
              {/* Nói rõ vòng khi mời SANG vòng kế — "Mời phỏng vấn" trơn ở vòng trắc nghiệm không cho
                  biết là đang xếp lại bài thi hay đang cho qua vòng. */}
              {inviteRound > (app.currentRound && app.currentRound > 0 ? app.currentRound : 1)
                ? t('actions.inviteNextRound', { round: inviteRound })
                : t('actions.invite')}
            </button>
          )}

          {onReject && (
            <button
              disabled={busy}
              onClick={() => onReject(app)}
              className="inline-flex items-center gap-1.5 rounded-xl border border-red-200 px-3 py-2 text-sm font-medium text-red-600 disabled:opacity-60 dark:border-red-500/30"
            >
              <X className="h-4 w-4" /> {t('actions.reject')}
            </button>
          )}

        </div>
      )}

      {/* Xem hồ sơ đầy đủ: luôn có, kể cả hồ sơ đã đóng — trước đây nút này nằm chung khối thao tác nên hồ
          sơ "Không đạt" mất luôn đường tới trang hồ sơ (và tới báo cáo của các vòng đã phỏng vấn). */}
      {candidateHref && (
        <div className={closed ? 'mb-4' : '-mt-2 mb-4'}>
          <a
            href={candidateHref(app)}
            className="inline-flex items-center gap-1.5 rounded-xl border border-ink-200 px-3 py-2 text-sm text-ink-700 hover:bg-ink-50 dark:border-white/10 dark:text-ink-200 dark:hover:bg-white/10"
          >
            <ExternalLink className="h-4 w-4" /> {t('actions.viewApplication')}
          </a>
        </div>
      )}

      {/* ---- Kết quả phỏng vấn (ADR-069) ----
          Hồ sơ đã vào phễu phỏng vấn: từng vòng · ca đã gán · báo cáo AI · video · transcript, kèm nút tới
          đúng báo cáo. Đặt NGAY đây vì đây là chỗ mọi vai đang đứng khi buổi phỏng vấn vừa xong. */}
      {evaluationHref && INTERVIEW_STAGES.has(app.status) && (
        <div className="mb-4 rounded-xl border border-ink-200 p-4 dark:border-white/10">
          <InterviewResultsCard applicationId={app.id} evaluationHref={evaluationHref} bare />
        </div>
      )}

      {/* ---- Mã vào phòng phỏng vấn ----
          Cấp ngay tại danh sách: đây là lúc ứng viên đang đứng ở quầy (tại văn phòng) hoặc sắp tới giờ
          (làm từ nhà) — bắt Recruiter sang màn Phỏng vấn tìm lại người đó là thêm một vòng đi lạc. */}
      {showInterviewCode && !closed && <InterviewCodeCard applicationId={app.id} />}

      {/* ---- CV đọc NGAY tại đây ----
          Trước đây phải mở lớp phủ cho từng người rồi đóng lại; sàng lọc mười hồ sơ là hai mươi cú
          bấm thừa. Dùng chung `DocumentPreview` với lớp phủ nên không có bộ dựng tài liệu thứ hai. */}
      <div className="rounded-xl border border-ink-200 dark:border-white/10">
        <div className="flex items-center justify-between gap-2 border-b border-ink-100 px-3 py-2 text-sm font-medium text-ink-700 dark:border-white/10 dark:text-ink-200">
          <span className="flex items-center gap-2">
            <FileText className="h-4 w-4 text-brand-600 dark:text-brand-400" /> {t('pipeline.cv')}
          </span>
          {/* Lối thoát khi khung dựng không giống bản gốc. `docx-preview` là bản dựng GẦN ĐÚNG, không
              phải Word: mẫu CV dùng khung nổi hay chia cột thì nó xếp sai chỗ. Người sàng hồ sơ phải
              luôn lấy được file thật mà không cần rời màn hình này. */}
          {app.cvFileUrl && (
            <a
              href={resolveAssetUrl(app.cvFileUrl)}
              download={downloadName(app.candidateName || 'CV', app.cvFileUrl)}
              target="_blank"
              rel="noreferrer"
              className="inline-flex items-center gap-1 rounded-lg border border-ink-200 px-2 py-1 text-xs font-medium text-ink-600 hover:bg-ink-50 dark:border-white/10 dark:text-ink-300 dark:hover:bg-white/10"
            >
              <Download className="h-3.5 w-3.5" /> {t('pipeline.cvDownload')}
            </a>
          )}
        </div>
        <div className="h-[32rem]">
          {app.cvFileUrl ? (
            /* Tên hiển thị KHÔNG kèm đuôi: `DocumentPreview` suy định dạng từ tên trước, URL sau.
               Gắn cứng ".pdf" vào đây là nói dối nó — CV .docx bị dựng bằng nhánh PDF, và một
               `<iframe>` trỏ vào blob kiểu Word thì Chrome TẢI FILE VỀ ngay lúc mở trang. */
            <DocumentPreview
              url={app.cvFileUrl}
              fileName={app.candidateName || 'CV'}
              pdfUrl={cvPreviewUrl(app.id)}
            />
          ) : (
            <div className="flex h-full flex-col items-center justify-center gap-2 text-center">
              <FileText className="h-8 w-8 text-ink-300" />
              <p className="text-sm text-ink-500 dark:text-ink-400">{t('pipeline.noCv')}</p>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

function Row({
  icon,
  label,
  value,
}: {
  icon?: React.ReactNode
  label: string
  value?: string | null
}) {
  return (
    <div className="flex items-baseline gap-2 border-b border-ink-100/70 py-1.5 dark:border-white/5">
      <dt className="flex w-32 shrink-0 items-center gap-1.5 text-xs text-ink-400">
        {icon}
        {label}
      </dt>
      {/* `truncate` ở đây từng cắt cụt email ("phongvg04…") mà không để lại đường nào đọc tiếp —
          ô này hẹp sẵn vì panel chia đôi. Cho xuống dòng: hai dòng email vẫn hơn một dòng vô dụng. */}
      <dd className="min-w-0 flex-1 break-words text-sm text-ink-800 dark:text-ink-100">
        {value || '—'}
      </dd>
    </div>
  )
}
