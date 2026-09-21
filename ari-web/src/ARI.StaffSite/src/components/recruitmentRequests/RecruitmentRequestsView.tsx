import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { roundTypeKey } from '@ari/shared/utils/roundTypes'
import {
  ClipboardList,
  Plus,
  Loader2,
  Check,
  X,
  Send,
  RotateCcw,
  Briefcase,
  ChevronLeft,
  Ban,
} from 'lucide-react'
import { PageHeader, EmptyState, ErrorAlert, Select } from '@ari/shared/ui'
import {
  recruitmentRequestService,
  type RecruitmentRequestListItem,
  type RecruitmentRequestDetail,
  type RecruitmentRequestInput,
  type RecruitmentRequestStatus,
  type RecruitmentPriority,
} from '@ari/shared/fservices/recruitmentRequest'
import { ROLE, normalizeRole } from '@ari/shared/utils/roles'
import {
  EMPLOYMENT_TYPES,
  WORK_MODES,
  EXPERIENCE_LEVELS,
  SALARY_CURRENCIES,
  DEFAULT_SALARY_CURRENCY,
  normalizeSalaryCurrency,
  jobOptionLabel,
} from '@ari/shared/utils/jobOptions'
import { useAuthStore } from '@ari/shared/store/auth'
import {
  DEFAULT_CV_SCORING_POLICY,
  clonePolicy,
  cvPolicyProblems,
  cvRubricProblems,
  toPayload,
} from '@ari/shared/fservices/cvRubric'
import CvRubricEditor, { CV_SCORING_NS, ReadOnlyRubric } from '@/components/cvRubric/CvRubricEditor'
import { profileService, type RecruiterOverview } from '@/fservices/profile/profileService'
import { resolveApiError } from '@ari/shared/utils/apiError'
import { useDbTableChanged, touchesRow } from '@ari/shared/realtime/dbTableRealtime'

/**
 * Bảng mà màn này phải nghe. `job_postings` có mặt vì cờ "đã dựng thành tin" (`jobPostingId`,
 * `canCreateJob`) suy từ bảng TIN chứ không nằm trên dòng phiếu (ADR-066): Recruiter dựng tin xong,
 * dòng phiếu không đổi gì.
 */
const REALTIME_TABLES = ['recruitment_requests', 'job_postings'] as const

/**
 * Màn phiếu yêu cầu tuyển dụng (ADR-063) — MỘT view dùng chung cho cả ba vai trò.
 *
 * Vì sao không tách ba trang: server đã lọc phạm vi và trả kèm ba cờ quyền (`canEdit`,
 * `canReview`, `canCreateJob`) cho từng phiếu, nên khác biệt giữa ba vai trò chỉ là hiện nút nào.
 * Ba bản sao của cùng một bảng là ba chỗ phải nhớ sửa — ADR-058 đã gộp hai màn Phỏng vấn vì đúng
 * lý do đó (và đánh rơi i18n khi làm vội, nên ở đây mọi chuỗi đều đi qua `t()`).
 */

const STATUS_STYLES: Record<RecruitmentRequestStatus, string> = {
  pending: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
  approved: 'bg-emerald-100 text-emerald-700 dark:bg-emerald-500/20 dark:text-emerald-400',
  rejected: 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400',
  cancelled: 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300',
}

/**
 * Đường tới màn SOẠN JD theo mẫu công ty, THEO VAI TRÒ người xem (ADR-064).
 *
 * Nút trên phiếu không còn đi thẳng tới màn tạo tin nữa: phiếu chỉ có vài dòng HM gõ vội, dựng tin
 * thẳng từ đó thì JD quá mỏng để đăng ra career site — mà chính file JD mới là thứ Hiring Manager
 * mở ra để ký duyệt. Soạn JD trước, xuất file, rồi file đó tự đi kèm sang màn tạo tin.
 *
 * Route trong StaffSite khoá chặt từng vai trò (`ProtectedRoute allowedRoles={['Recruiter']}`…),
 * không có ngoại lệ cho Super Admin — nên một đường dẫn cứng là sai với mọi người trừ một vai trò.
 * Vai trò không có trong bảng thì KHÔNG hiện nút, thay vì hiện một nút chắc chắn dẫn tới hư không
 * (Super Admin nằm trong số đó: server cho phép họ nhưng StaffSite không có màn nào ở `/super-admin`).
 */
const JD_COMPOSER_PATH: Record<string, ((requestId: string) => string) | undefined> = {
  [ROLE.Recruiter]: (id) => `/recruiter/recruitment-requests/${id}/jd`,
  [ROLE.HRAdmin]: (id) => `/hr/recruitment-requests/${id}/jd`,
}

/**
 * Muc do uu tien xep thu tu hang cho duyet cua HR Leader. Thu tu trong mang NAY la thu tu hien thi;
 * thu tu xep phieu do SQL quyet dinh (`GetRecruitmentRequestsQuery`) chu khong phai giao dien.
 */
const PRIORITY_OPTIONS: RecruitmentPriority[] = ['high', 'medium', 'low']

const PRIORITY_STYLES: Record<RecruitmentPriority, string> = {
  high: 'bg-red-100 text-red-700 dark:bg-red-500/20 dark:text-red-400',
  medium: 'bg-amber-100 text-amber-700 dark:bg-amber-500/20 dark:text-amber-400',
  low: 'bg-ink-100 text-ink-600 dark:bg-white/10 dark:text-ink-300',
}

const inputCls =
  'w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400'
const labelCls = 'mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300'

/** Ba loại vòng một tin có thể cấu hình — cùng thứ tự với `InterviewRoundTypes.All` ở backend. */
const ROUND_TYPES = ['online_test', 'screening', 'technical'] as const

/**
 * Ô `<input type="date">` giữ giá trị dạng `YYYY-MM-DD`, nhưng API nhận một MỐC thời gian.
 *
 * Gửi thẳng chuỗi trần thì **máy chủ** là nơi quyết định múi giờ: máy dev ở UTC+7 và container
 * production chạy UTC sẽ lưu ra hai mốc khác nhau cho cùng một ngày người dùng chọn — cùng kiểu phụ
 * thuộc môi trường âm thầm như bẫy `CultureInfo("vi-VN")` ở ADR-064. Chốt múi giờ của **trình duyệt**
 * ngay tại đây thì ngày hiện ra đúng thứ người lập phiếu vừa chọn, dù server ở đâu.
 *
 * (Server còn chuẩn hoá về UTC một lần nữa — Npgsql chỉ ghi được offset 0 vào `timestamptz`.)
 */
const toInstant = (day?: string | null) => {
  const picked = (day ?? '').slice(0, 10)
  return picked ? new Date(`${picked}T00:00:00`).toISOString() : undefined
}

/**
 * Hôm nay theo múi giờ **trình duyệt**, dạng `YYYY-MM-DD` — đúng khuôn `<input type="date">` nhận.
 *
 * Không dùng `toISOString().slice(0,10)`: chuỗi đó là ngày UTC, nên từ 07:00 sáng giờ Việt Nam trở
 * đi nó vẫn còn là hôm qua ở UTC — chặn "quá khứ" bằng mốc đó thì lại cấm chọn đúng hôm nay. Ghép
 * từ các thành phần giờ local mới ra đúng ngày người dùng đang nhìn thấy trên lịch.
 */
const todayInput = () => {
  const now = new Date()
  const pad = (n: number) => String(n).padStart(2, '0')
  return `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`
}

const emptyInput = (): RecruitmentRequestInput => ({
  title: '',
  headcount: 1,
  priority: 'medium',
  expectedStartDate: '',
  reason: '',
  description: '',
  requirements: '',
  // Gợi ý mặc định đúng phễu phổ biến nhất: thi sàng lọc → sơ loại → chuyên môn. HM bỏ bớt
  // hoặc đổi thứ tự được; để trống hoàn toàn thì server chặn.
  requestedRounds: ['online_test', 'screening', 'technical'],
  employmentType: 'full_time',
  workMode: 'onsite',
  location: '',
  experienceLevel: 'middle',
  salaryMin: undefined,
  salaryMax: undefined,
  salaryCurrency: DEFAULT_SALARY_CURRENCY,
  // ADR-070: bắt buộc — bắt đầu trống để HM chọn cách điền (AI gợi ý, mẫu công ty, Excel, gõ tay).
  cvRubric: [],
  // ADR-075: công thức chấm đi cùng bộ tiêu chí — bắt đầu từ mặc định, HM chỉnh nếu vị trí cần.
  cvScoringPolicy: clonePolicy(DEFAULT_CV_SCORING_POLICY),
})

/** Định dạng dải lương cho danh sách. Thoả thuận = chưa điền con số nào. */
function salaryLabel(
  min: number | null | undefined,
  max: number | null | undefined,
  currency: string | null | undefined,
  negotiableLabel: string
): string {
  if (min == null && max == null) return negotiableLabel
  const fmt = (n: number) => n.toLocaleString('vi-VN')
  const unit = currency || 'VND'
  if (min != null && max != null) return `${fmt(min)} – ${fmt(max)} ${unit}`
  return `${fmt((min ?? max) as number)} ${unit}`
}

export default function RecruitmentRequestsView() {
  const { t } = useTranslation('modules/staff/recruitmentRequests')
  const navigate = useNavigate()
  const role = normalizeRole(useAuthStore((s) => s.user?.role))

  const [items, setItems] = useState<RecruitmentRequestListItem[]>([])
  const [statusFilter, setStatusFilter] = useState('all')
  const [priorityFilter, setPriorityFilter] = useState('all')
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [showCreate, setShowCreate] = useState(false)

  // Thông báo trên chuông trỏ tới `/…/recruitment-requests/{id}`. Không đọc `:id` ở đây thì link đó
  // chỉ mở được DANH SÁCH — người dùng phải tự dò lại đúng phiếu vừa được báo.
  const { id: routeId } = useParams<{ id: string }>()
  const [detailId, setDetailId] = useState<string | null>(routeId ?? null)

  // CHỈ Hiring Manager lập phiếu. Recruiter thực thi nhu cầu chứ không phát sinh nó; còn quản trị
  // viên không lập hộ vì người lập phiếu **trở thành HM của tin** sinh ra từ phiếu (ADR-063) —
  // HR Leader lập phiếu là tự đặt mình vào cả hai đầu của các cổng mà ADR-061/063 dựng lên để tách.
  const canAuthor = role === ROLE.HiringManager

  /**
   * `silent` = nạp lại NGẦM do realtime: không bật khung tải (nó thay cả danh sách lẫn cột chi tiết,
   * tức là gỡ luôn ô lý do người dùng đang gõ) và không đè lỗi lên màn vì một lượt nền hỏng.
   */
  const load = useCallback(async (silent = false) => {
    if (!silent) {
      setLoading(true)
      setError('')
    }
    try {
      setItems(await recruitmentRequestService.list({
        ...(statusFilter === 'all' ? {} : { status: statusFilter }),
        ...(priorityFilter === 'all' ? {} : { priority: priorityFilter }),
      }))
    } catch (e: unknown) {
      if (!silent) setError(resolveApiError(e, t, 'errors.loadFailed'))
    } finally {
      if (!silent) setLoading(false)
    }
  }, [statusFilter, priorityFilter, t])

  useEffect(() => {
    void load()
  }, [load])

  // Phiếu vừa được duyệt / trả lại / phân công / dựng thành tin — ba bên đang mở màn này phải thấy ngay.
  useDbTableChanged(REALTIME_TABLES, () => void load(true))

  const statusOptions = useMemo(
    () => [
      { value: 'all', label: t('filters.all') },
      { value: 'pending', label: t('status.pending') },
      { value: 'approved', label: t('status.approved') },
      { value: 'rejected', label: t('status.rejected') },
      { value: 'cancelled', label: t('status.cancelled') },
    ],
    [t]
  )

  const priorityOptions = useMemo(
    () => [
      { value: 'all', label: t('filters.allPriorities') },
      ...PRIORITY_OPTIONS.map((p) => ({ value: p, label: t('priority.' + p) })),
    ],
    [t]
  )

  // Phiếu đang chọn biến mất khỏi danh sách (đổi bộ lọc, vừa bị rút) thì bỏ chọn — nếu không, cột
  // phải hiện chi tiết của một dòng không còn ở cột trái, trông như hai màn khác nhau.
  useEffect(() => {
    if (detailId && items.length > 0 && !items.some((r) => r.id === detailId)) setDetailId(null)
  }, [items, detailId])

  // Tự chọn phiếu đầu tiên trên màn RỘNG (giống cách các trang việc làm vẫn làm). Màn hẹp thì
  // KHÔNG: hai cột xếp chồng nhau nên tự chọn sẽ nhảy thẳng vào chi tiết, người dùng chưa kịp
  // nhìn thấy danh sách.
  useEffect(() => {
    if (detailId || items.length === 0 || loading) return
    if (typeof window !== 'undefined' && window.matchMedia('(min-width: 1024px)').matches) {
      setDetailId(items[0].id)
    }
  }, [items, detailId, loading])

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <PageHeader
        title={t('title')}
        description={t('description')}
        actions={
          canAuthor
            ? [{ label: t('create'), icon: <Plus className="h-4 w-4" />, onClick: () => setShowCreate(true) }]
            : undefined
        }
      />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      <div className="mb-6 flex flex-wrap items-center gap-3">
        <Select
          value={statusFilter}
          onChange={setStatusFilter}
          ariaLabel={t('filters.all')}
          className="min-w-[12rem]"
          buttonClassName="px-4 py-2.5 text-sm"
          options={statusOptions}
        />
        <Select
          value={priorityFilter}
          onChange={setPriorityFilter}
          ariaLabel={t('filters.allPriorities')}
          className="min-w-[12rem]"
          buttonClassName="px-4 py-2.5 text-sm"
          options={priorityOptions}
        />
      </div>

      {loading ? (
        <div className="flex items-center gap-2 py-16 text-sm text-ink-500">
          <Loader2 className="h-4 w-4 animate-spin" /> {t('loading')}
        </div>
      ) : items.length === 0 ? (
        <EmptyState
          icon={<ClipboardList className="h-8 w-8 text-ink-400" />}
          title={t('empty.title')}
          description={canAuthor ? t('empty.descriptionAuthor') : t('empty.description')}
          action={canAuthor ? { label: t('create'), onClick: () => setShowCreate(true) } : undefined}
        />
      ) : (
        /* Bố cục hai cột: danh sách bên trái, chi tiết ngay cạnh — không mở hộp thoại đè lên màn.
           Trên màn hẹp hai cột xếp chồng nên chỉ hiện MỘT: chưa chọn thì thấy danh sách, chọn rồi
           thì thấy chi tiết kèm nút quay lại. */
        <div className="grid grid-cols-1 items-start gap-5 lg:grid-cols-[minmax(0,24rem)_minmax(0,1fr)]">
          <div className={`space-y-3 ${detailId ? 'hidden lg:block' : ''}`}>
            {items.map((r) => {
              const selected = r.id === detailId
              return (
                <button
                  key={r.id}
                  onClick={() => setDetailId(r.id)}
                  aria-current={selected ? 'true' : undefined}
                  className={`w-full rounded-2xl border bg-white p-4 text-left shadow-card transition dark:bg-white/5 ${
                    selected
                      ? 'border-brand-500 ring-1 ring-brand-500/30 dark:border-brand-400'
                      : 'border-ink-200 hover:border-brand-300 dark:border-white/10'
                  }`}
                >
                  <div className="flex flex-wrap items-center gap-2">
                    <h3 className="min-w-0 flex-1 truncate text-base font-semibold text-ink-900 dark:text-white">
                      {r.title}
                    </h3>
                    {r.jobPostingId && (
                      <span
                        title={t('jobCreated')}
                        className="inline-flex items-center gap-1 whitespace-nowrap rounded-full bg-brand-100 px-2 py-0.5 text-xs font-medium text-brand-700 dark:bg-brand-500/20 dark:text-brand-300"
                      >
                        <Briefcase className="h-3 w-3" />
                      </span>
                    )}
                  </div>

                  <div className="mt-1.5 flex flex-wrap items-center gap-1.5">
                    <span
                      className={`inline-flex items-center whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-medium ${STATUS_STYLES[r.status]}`}
                    >
                      {t(`status.${r.status}`)}
                    </span>
                    {/* Phieu `high` da duoc SQL day len dau danh sach; nhan nay de nguoi doc thay
                        vi sao thu tu nhu vay, chu khong phai de tu sap lai o giao dien. */}
                    <span
                      className={`inline-flex items-center whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-medium ${PRIORITY_STYLES[r.priority ?? 'medium']}`}
                    >
                      {t(`priority.${r.priority ?? 'medium'}`)}
                    </span>
                    {r.submissionCount > 1 && (
                      <span className="whitespace-nowrap rounded-full bg-ink-100 px-2 py-0.5 text-xs text-ink-600 dark:bg-white/10 dark:text-ink-300">
                        {t('submissionRound', { count: r.submissionCount })}
                      </span>
                    )}
                  </div>

                  <p className="mt-2 text-sm text-ink-500 dark:text-ink-400">
                    {[
                      r.department,
                      t('headcountValue', { count: r.headcount }),
                      salaryLabel(r.salaryMin, r.salaryMax, r.salaryCurrency, t('negotiable')),
                    ]
                      .filter(Boolean)
                      .join(' · ')}
                  </p>
                  <p className="mt-1 truncate text-xs text-ink-400">
                    {t('requestedBy', { name: r.requestedByName })}
                    {r.assignedRecruiterName ? ` · ${t('assignedTo', { name: r.assignedRecruiterName })}` : ''}
                  </p>
                </button>
              )
            })}
          </div>

          {/* Cột chi tiết: dính theo cuộn và tự cuộn riêng, để danh sách bên trái không bị kéo dài
              theo một phiếu có mô tả rất dài. */}
          <div className={`${detailId ? '' : 'hidden lg:block'} lg:sticky lg:top-[var(--sticky-top,1.5rem)]`}>
            {detailId ? (
              <RequestDetailPanel
                key={detailId}
                id={detailId}
                onClose={() => setDetailId(null)}
                onChanged={() => void load()}
                canComposeJd={JD_COMPOSER_PATH[role] != null}
                onComposeJd={(id) => navigate(JD_COMPOSER_PATH[role]!(id))}
              />
            ) : (
              <div className="grid place-items-center rounded-2xl border border-dashed border-ink-200 bg-white/50 p-12 text-center dark:border-white/10 dark:bg-white/5">
                <ClipboardList className="mb-3 h-8 w-8 text-ink-300" />
                <p className="text-sm text-ink-500 dark:text-ink-400">{t('detail.pickOne')}</p>
              </div>
            )}
          </div>
        </div>
      )}

      {showCreate && (
        <RequestFormModal
          onClose={() => setShowCreate(false)}
          onSaved={() => {
            setShowCreate(false)
            void load()
          }}
        />
      )}
    </div>
  )
}

// ===================== Form lập / sửa phiếu =====================

function RequestFormModal({
  initial,
  requestId,
  requesterName,
  departmentName,
  onClose,
  onSaved,
}: {
  initial?: RecruitmentRequestInput
  requestId?: string

  /** Ten nguoi lap phieu - khi sua la chu phieu, khi tao moi la nguoi dang dang nhap. */
  requesterName?: string

  /** Ten doi da gan voi phieu (chi khi sua); tao moi thi lay tu tai khoan nguoi dang nhap. */
  departmentName?: string | null

  onClose: () => void
  onSaved: () => void
}) {
  const { t } = useTranslation('modules/staff/recruitmentRequests')
  const currentUser = useAuthStore((s) => s.user)
  const [form, setForm] = useState<RecruitmentRequestInput>(initial ?? emptyInput())
  const [submitting, setSubmitting] = useState(false)
  const [err, setErr] = useState('')
  const [triedSubmit, setTriedSubmit] = useState(false)
  const { t: tRubric } = useTranslation(CV_SCORING_NS)

  // Đội của chính người đang lập phiếu, lấy từ HỒ SƠ chứ không từ form: đây là ô chỉ đọc, giá trị
  // hiện ra phải đúng bằng thứ server sẽ ghi vào phiếu.
  const [myDepartment, setMyDepartment] = useState('')
  useEffect(() => {
    void profileService
      .getProfile()
      .then((profile) => setMyDepartment(profile.department ?? ''))
      .catch(() => setMyDepartment(''))
  }, [])

  /** O tich SUY RA tu du lieu: thoa thuan = chua dien con so nao. */
  const negotiable = form.salaryNegotiable ?? false

  // Ngày dự kiến bắt đầu không được nằm trong quá khứ. Phiếu nằm chờ vài tuần rồi mở ra sửa thì ngày
  // cũ TỰ trôi vào quá khứ mà không ai gõ gì — nên phải kiểm cả giá trị tải từ máy chủ, không chỉ
  // chặn ô chọn. So sánh chuỗi `YYYY-MM-DD` là so sánh đúng thứ tự ngày, không cần dựng `Date`.
  const today = todayInput()
  const startDate = (form.expectedStartDate ?? '').slice(0, 10)
  const startDateInPast = startDate !== '' && startDate < today

  const setNegotiable = (on: boolean) =>
    setForm({
      ...form,
      salaryNegotiable: on,
      // Xoa trang khi tich: de lai con so cu duoi o da khoa thi nguoi dung thay "thoa thuan" ma
      // server van nhan duoc luong - hai thu mau thuan nhau tren cung mot phieu.
      salaryMin: on ? undefined : form.salaryMin,
      salaryMax: on ? undefined : form.salaryMax,
    })

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    setErr('')
    setTriedSubmit(true)
    // Bộ tiêu chí sai thì server cũng chặn — chặn ở đây để lỗi hiện ngay cạnh trình soạn.
    if (
      cvRubricProblems(form.cvRubric ?? []).length > 0 ||
      cvPolicyProblems(form.cvScoringPolicy ?? DEFAULT_CV_SCORING_POLICY).length > 0
    ) {
      setErr(tRubric('editor.warnings'))
      return
    }
    // Máy chủ cũng chặn — chặn lại ở đây để lỗi hiện bằng tiếng của giao diện, thay vì bong bóng
    // mặc định của trình duyệt (theo ngôn ngữ hệ điều hành) khi giá trị cũ rơi dưới `min`.
    if (startDateInPast) {
      setErr(t('errors.startDateInPast'))
      return
    }
    setSubmitting(true)
    try {
      const payload: RecruitmentRequestInput = {
        ...form,
        title: form.title.trim(),
        headcount: Number(form.headcount) || 1,
        expectedStartDate: toInstant(form.expectedStartDate),
        // Điều kiện bắt buộc gửi đi không kèm trọng số / dải / ý kiểm (ADR-075).
        cvRubric: toPayload(form.cvRubric ?? []),
      }
      if (requestId) await recruitmentRequestService.update(requestId, payload)
      else await recruitmentRequestService.create(payload)
      onSaved()
    } catch (e: any) {
      setErr(resolveApiError(e, t, 'errors.saveFailed'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <Modal title={requestId ? t('form.editTitle') : t('form.createTitle')} onClose={onClose}>
      {err && <ErrorAlert message={err} onDismiss={() => setErr('')} />}

      <form onSubmit={submit} className="space-y-4">
        <div>
          <label className={labelCls}>{t('form.position')} *</label>
          <input
            required
            value={form.title}
            onChange={(e) => setForm({ ...form, title: e.target.value })}
            placeholder={t('form.positionPlaceholder')}
            className={inputCls}
          />
        </div>

        {/* Hai o nhan dien nguoi xin tuyen. Hiring Manager KHONG sua duoc: chinh cho nay la lo de
            HM doi A lap phieu ghi doi B. Quan tri vien lap ho thi phai chon doi, vi ho khong thuoc
            doi nao. */}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label className={labelCls}>{t('form.requester')}</label>
            <input
              disabled
              value={requesterName || currentUser?.name || currentUser?.email || ''}
              className={`${inputCls} cursor-not-allowed opacity-70`}
            />
          </div>
          <div>
            <label className={labelCls}>{t('form.department')}</label>
            <input
              disabled
              value={departmentName || myDepartment || ''}
              placeholder={t('form.departmentUnset')}
              className={`${inputCls} cursor-not-allowed opacity-70`}
            />
            {!departmentName && !myDepartment && (
              <p className="mt-1 text-xs text-amber-600 dark:text-amber-400">
                {t('form.departmentUnsetHint')}
              </p>
            )}
          </div>
        </div>

        <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
          <div>
            <label className={labelCls}>{t('form.headcount')} *</label>
            <input
              required
              type="number"
              min={1}
              value={form.headcount}
              onChange={(e) => setForm({ ...form, headcount: Number(e.target.value) })}
              className={inputCls}
            />
          </div>
          <div>
            <label className={labelCls}>{t('form.priority')} *</label>
            <Select
              value={form.priority ?? 'medium'}
              onChange={(v) => setForm({ ...form, priority: v as RecruitmentPriority })}
              ariaLabel={t('form.priority')}
              className="w-full"
              buttonClassName="px-3 py-2.5 text-sm"
              options={PRIORITY_OPTIONS.map((p) => ({ value: p, label: t('priority.' + p) }))}
            />
          </div>
          <div>
            {/* Cot `expected_start_date` da co tu ADR-063 nhung chua tung co o nhap - Recruiter phai
                di hoi lai HM bao gio can nguoi, ma do la du lieu quyet dinh muc do gap. */}
            <label className={labelCls}>{t('form.expectedStartDate')} *</label>
            <input
              required
              type="date"
              // `min` làm lịch xổ xuống mờ hẳn những ngày đã qua — cản ngay lúc chọn, trước khi
              // người dùng phải bấm lưu mới biết sai.
              min={today}
              value={startDate}
              onChange={(e) => setForm({ ...form, expectedStartDate: e.target.value })}
              className={inputCls}
            />
            {startDateInPast && (
              <p className="mt-1 text-xs text-red-600 dark:text-red-400">
                {t('errors.startDateInPast')}
              </p>
            )}
          </div>
        </div>

        {/*
          Bốn ô này TRƯỚC ĐÂY KHÔNG CÓ trên biểu mẫu, nhưng vẫn được gửi lên với giá trị ghi cứng
          (`full_time` / `onsite` / `middle`) — nên bản JD in ra "Thời gian: full_time, Cấp bậc:
          middle" mà chưa ai từng nhập. Một giá trị mặc định trông có vẻ vô hại lại đi thẳng vào
          văn bản gửi ra ngoài công ty. Nay Hiring Manager khai tường minh.
        */}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <div>
            <label className={labelCls}>{t('form.employmentType')}</label>
            <Select
              value={form.employmentType ?? 'full_time'}
              onChange={(v) => setForm({ ...form, employmentType: v })}
              ariaLabel={t('form.employmentType')}
              className="w-full"
              buttonClassName="px-3 py-2.5 text-sm"
              options={EMPLOYMENT_TYPES}
            />
          </div>
          <div>
            <label className={labelCls}>{t('form.experienceLevel')}</label>
            <Select
              value={form.experienceLevel ?? 'middle'}
              onChange={(v) => setForm({ ...form, experienceLevel: v })}
              ariaLabel={t('form.experienceLevel')}
              className="w-full"
              buttonClassName="px-3 py-2.5 text-sm"
              options={EXPERIENCE_LEVELS}
            />
          </div>
          <div>
            <label className={labelCls}>{t('form.workMode')}</label>
            <Select
              value={form.workMode ?? 'onsite'}
              onChange={(v) => setForm({ ...form, workMode: v })}
              ariaLabel={t('form.workMode')}
              className="w-full"
              buttonClassName="px-3 py-2.5 text-sm"
              options={WORK_MODES}
            />
          </div>
          <div>
            <label className={labelCls}>{t('form.location')}</label>
            <input
              value={form.location ?? ''}
              onChange={(e) => setForm({ ...form, location: e.target.value })}
              placeholder={t('form.locationPlaceholder')}
              className={inputCls}
            />
          </div>
        </div>

        {/* Dải lương đề xuất — HR Leader trả phiếu về thường là để chỉnh đúng hai ô này. */}
        <div className="rounded-xl border border-ink-200 p-3 dark:border-white/10">
          <div className="mb-2 flex flex-wrap items-center justify-between gap-2">
            <p className="text-sm font-medium text-ink-700 dark:text-ink-200">{t('form.salaryRange')} *</p>
            {/* Truoc day de trong hai o luong co the la "thoa thuan" ma cung co the la "quen dien" -
                HR Leader khong phan biet duoc, nen tra phieu ve hoi lai. O tich nay la cho de noi ro
                y dinh; khong tich thi bat buoc phai co it nhat mot con so (server chan). */}
            <label className="flex cursor-pointer items-center gap-2 text-sm text-ink-600 dark:text-ink-300">
              <input
                type="checkbox"
                checked={negotiable}
                onChange={(e) => setNegotiable(e.target.checked)}
                className="h-4 w-4 accent-brand-600"
              />
              {t('form.salaryNegotiable')}
            </label>
          </div>
          <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
            <div>
              <label className={labelCls}>{t('form.salaryMin')}</label>
              <input
                type="number"
                min={0}
                disabled={negotiable}
                value={form.salaryMin ?? ''}
                onChange={(e) =>
                  setForm({ ...form, salaryMin: e.target.value === '' ? undefined : Number(e.target.value) })
                }
                className={`${inputCls} disabled:cursor-not-allowed disabled:opacity-50`}
              />
            </div>
            <div>
              <label className={labelCls}>{t('form.salaryMax')}</label>
              <input
                type="number"
                min={0}
                disabled={negotiable}
                value={form.salaryMax ?? ''}
                onChange={(e) =>
                  setForm({ ...form, salaryMax: e.target.value === '' ? undefined : Number(e.target.value) })
                }
                className={`${inputCls} disabled:cursor-not-allowed disabled:opacity-50`}
              />
            </div>
            <div>
              <label className={labelCls}>{t('form.currency')}</label>
              <Select
                disabled={negotiable}
                value={normalizeSalaryCurrency(form.salaryCurrency)}
                onChange={(v) => setForm({ ...form, salaryCurrency: v })}
                ariaLabel={t('form.currency')}
                className="w-full"
                buttonClassName="px-3 py-2.5 text-sm"
                options={SALARY_CURRENCIES}
              />
            </div>
          </div>
          <p className="mt-2 text-xs text-ink-400">
            {negotiable ? t('form.salaryNegotiableHint') : t('form.salaryHint')}
          </p>
        </div>

        <div>
          <label className={labelCls}>{t('form.reason')} *</label>
          <input
            required
            value={form.reason ?? ''}
            onChange={(e) => setForm({ ...form, reason: e.target.value })}
            placeholder={t('form.reasonPlaceholder')}
            className={inputCls}
          />
        </div>

        <div>
          <label className={labelCls}>{t('form.description')} *</label>
          <textarea
            required
            rows={4}
            value={form.description ?? ''}
            onChange={(e) => setForm({ ...form, description: e.target.value })}
            placeholder={t('form.descriptionPlaceholder')}
            className={inputCls}
          />
        </div>

        {/* Tách riêng khỏi mô tả: "làm gì" và "cần gì để làm được" là hai câu hỏi khác nhau, và chỉ
            trưởng bộ phận trả lời được vế thứ hai. Gộp chung một ô thì phần yêu cầu kỹ thuật hay bị
            viết qua loa, và Recruiter phải tự đoán khi dựng JD. */}
        <div>
          <label className={labelCls}>{t('form.requirements')} *</label>
          <textarea
            required
            rows={5}
            value={form.requirements ?? ''}
            onChange={(e) => setForm({ ...form, requirements: e.target.value })}
            placeholder={t('form.requirementsPlaceholder')}
            className={inputCls}
          />
          <p className="mt-1 text-xs text-ink-400">{t('form.requirementsHint')}</p>
        </div>

        {/* Quy trình tuyển của một vị trí là quyết định CHUYÊN MÔN: trưởng bộ phận biết vị trí này
            cần thi trắc nghiệm trước hay phỏng vấn thẳng, cần mấy vòng chuyên môn. Recruiter dựng tin là
            thi hành quyết định đó — không hỏi ở đây thì họ phải tự đoán hoặc đi hỏi lại bằng tay. */}
        <div>
          <label className={labelCls}>{t('form.rounds')} *</label>
          <RoundPicker
            value={form.requestedRounds ?? []}
            onChange={(next) => setForm({ ...form, requestedRounds: next })}
            t={t}
          />
          <p className="mt-1 text-xs text-ink-400">{t('form.roundsHint')}</p>
        </div>

        {/* ADR-070: "ứng viên thế nào là phù hợp" là quyết định của người có nhu cầu tuyển, và tin không có
            bộ tiêu chí thì không chấm được CV nào — nên bộ tiêu chí nằm ngay trên phiếu, bắt buộc. */}
        <div className="rounded-2xl border border-ai-200 bg-ai-50/40 p-4 dark:border-ai-500/20 dark:bg-ai-500/5">
          <label className={labelCls}>{tRubric('editor.title')} *</label>
          <CvRubricEditor
            value={form.cvRubric ?? []}
            onChange={(next) => setForm((f) => ({ ...f, cvRubric: next }))}
            policy={form.cvScoringPolicy ?? DEFAULT_CV_SCORING_POLICY}
            onPolicyChange={(next) => setForm((f) => ({ ...f, cvScoringPolicy: next }))}
            showProblems={triedSubmit}
            suggestSource={() =>
              form.title.trim() && (form.description?.trim() || form.requirements?.trim())
                ? {
                    title: form.title.trim(),
                    description: form.description ?? null,
                    requirements: form.requirements ?? null,
                    experienceLevel: form.experienceLevel ?? null,
                  }
                : null
            }
          />
        </div>

        <div className="flex gap-3 pt-2">
          <button
            type="button"
            onClick={onClose}
            className="flex-1 rounded-xl border border-ink-200 px-4 py-2.5 text-sm font-medium text-ink-700 dark:border-white/10 dark:text-ink-200"
          >
            {t('form.cancel')}
          </button>
          <button
            type="submit"
            disabled={submitting}
            className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-medium text-white disabled:opacity-60"
          >
            {submitting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Check className="h-4 w-4" />}
            {t('form.submit')}
          </button>
        </div>
      </form>
    </Modal>
  )
}

// ===================== Chi tiết + các nút quyết định =====================
//
// Là một THẺ nằm trong cột phải, không phải hộp thoại đè lên màn: người dùng vẫn thấy danh sách và
// chuyển qua lại giữa các phiếu bằng một cú bấm. Nút đóng chỉ có nghĩa trên màn hẹp (nơi hai cột
// xếp chồng) nên nhãn của nó là "quay lại danh sách".

function RequestDetailPanel({
  id,
  onClose,
  onChanged,
  onComposeJd,
  canComposeJd,
}: {
  id: string
  onClose: () => void
  onChanged: () => void
  onComposeJd: (id: string) => void

  /** `false` = vai trò này không có màn soạn JD → ẩn nút thay vì dẫn tới hư không. */
  canComposeJd: boolean
}) {
  const { t } = useTranslation('modules/staff/recruitmentRequests')
  const { t: tRubric } = useTranslation(CV_SCORING_NS)
  const [detail, setDetail] = useState<RecruitmentRequestDetail | null>(null)
  const [recruiters, setRecruiters] = useState<RecruiterOverview[]>([])
  const [chosenRecruiter, setChosenRecruiter] = useState('')
  const [rejectReason, setRejectReason] = useState('')

  /**
   * Hai chế độ thu hồi phê duyệt (ADR-066) dùng chung một ô lý do: cùng ràng buộc ≥ 10 ký tự, chỉ
   * khác câu chữ và đích đến. Hai ô riêng thì lần sửa sau chỉ một ô được sửa.
   */
  const [revokeReason, setRevokeReason] = useState('')
  const [mode, setMode] = useState<'view' | 'edit' | 'reject' | 'reopen' | 'close'>('view')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState('')

  const load = useCallback(async () => {
    try {
      const d = await recruitmentRequestService.getById(id)
      setDetail(d)
      if (d.canReview) {
        const list = await profileService.getRecruiters()
        // Chỉ gợi ý người còn hoạt động — phân công cho tài khoản bị khoá thì phiếu nằm im.
        const usable = list.filter((r) => r.isActive)
        setRecruiters(usable)
        // Xếp gợi ý theo phòng ban (ADR-061: department chỉ để gợi ý, KHÔNG dùng phân quyền).
        const sameDept = usable.find((r) => r.department && r.department === d.department)
        // Giữ lựa chọn HR Leader đã bấm nếu người đó vẫn còn trong danh sách: lượt nạp lại do
        // realtime không được lặng lẽ đổi Recruiter ngay trước khi họ bấm Duyệt.
        setChosenRecruiter((current) =>
          usable.some((r) => r.id === current) ? current : sameDept?.id ?? usable[0]?.id ?? ''
        )
      }
    } catch (e: any) {
      setErr(resolveApiError(e, t, 'errors.loadFailed'))
    }
  }, [id, t])

  useEffect(() => {
    void load()
  }, [load])

  // Chỉ nạp lại khi chính phiếu này đổi (hoặc có tin mới — cờ "đã dựng thành tin" nằm ở bảng tin).
  // Ô lý do trả lại / thu hồi là state riêng nên không bị đè.
  useDbTableChanged(REALTIME_TABLES, (changes) => {
    if (touchesRow(changes, 'recruitment_requests', id) || changes.some((c) => c.table === 'job_postings')) {
      void load()
    }
  })

  /** Mở một ô lý do luôn bắt đầu từ trống — lý do đóng phiếu không được trôi sang lần mở lại sau. */
  const openRevoke = (next: 'reopen' | 'close') => {
    setRevokeReason('')
    setErr('')
    setMode(next)
  }

  const run = async (fn: () => Promise<void>) => {
    setBusy(true)
    setErr('')
    try {
      await fn()
      await load()
      onChanged()
      setRevokeReason('')
      setMode('view')
    } catch (e: any) {
      setErr(resolveApiError(e, t, 'errors.actionFailed'))
    } finally {
      setBusy(false)
    }
  }

  if (!detail) {
    return (
      <Panel>
        {err ? (
          <ErrorAlert message={err} />
        ) : (
          <div className="flex items-center gap-2 py-8 text-sm text-ink-500">
            <Loader2 className="h-4 w-4 animate-spin" /> {t('loading')}
          </div>
        )}
      </Panel>
    )
  }

  if (mode === 'edit') {
    return (
      <RequestFormModal
        requestId={detail.id}
        requesterName={detail.requestedByName}
        departmentName={detail.department}
        initial={{
          title: detail.title,
          headcount: detail.headcount,
          priority: detail.priority ?? 'medium',
          expectedStartDate: detail.expectedStartDate ?? '',
          reason: detail.reason ?? '',
          description: detail.description ?? '',
          requirements: detail.requirements ?? '',
          // Phiếu cũ có thể khai trùng loại vòng (trước khi có luật mỗi loại một lần) — gộp lại ngay khi mở sửa
          // để HM thấy đúng danh sách sẽ được lưu, thay vì bấm lưu rồi mới bị server trả lỗi.
          requestedRounds: [...new Set(detail.requestedRounds ?? [])],
          cvRubric: detail.cvRubric ?? [],
          cvScoringPolicy: clonePolicy(detail.cvScoringPolicy),
          employmentType: detail.employmentType ?? 'full_time',
          workMode: detail.workMode ?? 'onsite',
          location: detail.location ?? '',
          experienceLevel: detail.experienceLevel ?? 'middle',
          // Suy ra tu chinh du lieu: phieu khong co con so luong nao la phieu thoa thuan.
          salaryNegotiable: detail.salaryMin == null && detail.salaryMax == null,
          salaryMin: detail.salaryMin ?? undefined,
          salaryMax: detail.salaryMax ?? undefined,
          salaryCurrency: normalizeSalaryCurrency(detail.salaryCurrency),
        }}
        onClose={() => setMode('view')}
        onSaved={() => {
          void load()
          onChanged()
          setMode('view')
        }}
      />
    )
  }

  return (
    <Panel>
      {/* Nút quay lại chỉ hiện trên màn hẹp — trên màn rộng danh sách vẫn nằm ngay bên trái nên
          "đóng" chi tiết chẳng để làm gì. */}
      <button
        onClick={onClose}
        className="mb-3 inline-flex items-center gap-1.5 text-sm text-ink-500 hover:text-ink-800 dark:text-ink-400 dark:hover:text-white lg:hidden"
      >
        <ChevronLeft className="h-4 w-4" /> {t('detail.back')}
      </button>

      <h3 className="mb-4 text-lg font-semibold text-ink-900 dark:text-white">{detail.title}</h3>

      {err && <ErrorAlert message={err} onDismiss={() => setErr('')} />}

      <div className="space-y-4 text-sm">
        <div className="flex flex-wrap items-center gap-2">
          <span className={`rounded-full px-2.5 py-1 text-xs font-medium ${STATUS_STYLES[detail.status]}`}>
            {t(`status.${detail.status}`)}
          </span>
          <span
            className={`rounded-full px-2.5 py-1 text-xs font-medium ${PRIORITY_STYLES[detail.priority ?? 'medium']}`}
          >
            {t(`priority.${detail.priority ?? 'medium'}`)}
          </span>
          <span className="text-xs text-ink-400">{t('requestedBy', { name: detail.requestedByName })}</span>
        </div>

        <dl className="grid grid-cols-2 gap-3">
          <Field label={t('form.department')} value={detail.department || '—'} />
          <Field label={t('form.headcount')} value={String(detail.headcount)} />
          <Field
            label={t('form.expectedStartDate')}
            value={
              detail.expectedStartDate
                ? new Date(detail.expectedStartDate).toLocaleDateString('vi-VN')
                : '—'
            }
          />
          <Field
            label={t('form.salaryRange')}
            value={salaryLabel(detail.salaryMin, detail.salaryMax, detail.salaryCurrency, t('negotiable'))}
          />
          <Field
            label={t('form.employmentType')}
            value={jobOptionLabel(EMPLOYMENT_TYPES, detail.employmentType)}
          />
          <Field
            label={t('form.experienceLevel')}
            value={jobOptionLabel(EXPERIENCE_LEVELS, detail.experienceLevel)}
          />
          <Field label={t('form.workMode')} value={jobOptionLabel(WORK_MODES, detail.workMode)} />
          <Field label={t('detail.assignedRecruiter')} value={detail.assignedRecruiterName || '—'} />
        </dl>

        {detail.reason && <Field label={t('form.reason')} value={detail.reason} />}
        {detail.description && <Field label={t('form.description')} value={detail.description} />}
        {detail.requirements && <Field label={t('form.requirements')} value={detail.requirements} />}
        {(detail.requestedRounds ?? []).length > 0 && (
          <Field
            label={t('form.rounds')}
            value={(detail.requestedRounds ?? [])
              .map((r, i) => `${i + 1}. ${t(`roundTypes.${roundTypeKey(r)}`)}`)
              .join('  ·  ')}
          />
        )}

        {/* Bộ tiêu chí chấm CV (ADR-070) — HR Leader xem khi duyệt; phiếu lập trước đó thì báo thiếu. */}
        <div>
          <p className="mb-1.5 text-xs font-medium text-ink-500 dark:text-ink-400">{tRubric('editor.title')}</p>
          {(detail.cvRubric ?? []).length > 0 ? (
            <ReadOnlyRubric criteria={detail.cvRubric ?? []} policy={detail.cvScoringPolicy ?? null} />
          ) : (
            <p className="rounded-xl bg-amber-50 px-3 py-2 text-xs text-amber-800 dark:bg-amber-500/10 dark:text-amber-300">
              {tRubric('requestMissing')}
            </p>
          )}
        </div>

        {/* Lý do thu hồi (ADR-066): Recruiter vừa mất việc và HR Leader vừa bị huỷ chữ ký đều phải
            đọc được dòng này ngay trên phiếu, không phải đi tìm trong chuông. */}
        {detail.revokedReason && (
          <div className="rounded-xl bg-amber-50 px-3 py-2 text-amber-800 dark:bg-amber-500/10 dark:text-amber-300">
            {detail.status === 'cancelled'
              ? t('detail.closedNote', { reason: detail.revokedReason })
              : t('detail.reopenedNote', { reason: detail.revokedReason })}
            {detail.revokedByName && (
              <span className="mt-0.5 block text-xs opacity-80">
                {t('detail.revokedBy', { name: detail.revokedByName })}
              </span>
            )}
          </div>
        )}

        {detail.reviewReason && (
          <div
            className={`rounded-xl px-3 py-2 ${
              detail.status === 'rejected'
                ? 'bg-red-50 text-red-700 dark:bg-red-500/10 dark:text-red-300'
                : 'bg-ink-50 text-ink-600 dark:bg-white/5 dark:text-ink-300'
            }`}
          >
            {t('detail.reviewNote', { reason: detail.reviewReason })}
          </div>
        )}

        {/* ---- Cổng duyệt của HR Leader: duyệt LUÔN kèm phân công ---- */}
        {detail.canReview && mode === 'view' && (
          <div className="rounded-xl border border-ink-200 p-3 dark:border-white/10">
            <label className={labelCls}>{t('detail.assignRecruiter')} *</label>
            <Select
              value={chosenRecruiter}
              onChange={setChosenRecruiter}
              ariaLabel={t('detail.assignRecruiter')}
              className="w-full"
              buttonClassName="px-3 py-2.5 text-sm"
              options={recruiters.map((r) => ({
                value: r.id,
                label: `${r.fullName || r.email}${r.department ? ` · ${r.department}` : ''} · ${t('detail.jobsActive', { count: r.jobsActive })}`,
              }))}
            />
            <p className="mt-2 text-xs text-ink-400">{t('detail.assignHint')}</p>

            <div className="mt-3 flex gap-2">
              <button
                disabled={busy || !chosenRecruiter}
                onClick={() => run(() => recruitmentRequestService.approve(detail.id, chosenRecruiter))}
                className="flex flex-1 items-center justify-center gap-2 rounded-xl bg-emerald-600 px-4 py-2.5 text-sm font-medium text-white disabled:opacity-60"
              >
                <Check className="h-4 w-4" /> {t('detail.approve')}
              </button>
              <button
                disabled={busy}
                onClick={() => setMode('reject')}
                className="flex flex-1 items-center justify-center gap-2 rounded-xl border border-red-200 px-4 py-2.5 text-sm font-medium text-red-600 dark:border-red-500/30"
              >
                <X className="h-4 w-4" /> {t('detail.reject')}
              </button>
            </div>
          </div>
        )}

        {mode === 'reject' && (
          <div className="rounded-xl border border-red-200 p-3 dark:border-red-500/30">
            <label className={labelCls}>{t('detail.rejectReason')} *</label>
            <textarea
              rows={3}
              value={rejectReason}
              onChange={(e) => setRejectReason(e.target.value)}
              placeholder={t('detail.rejectReasonPlaceholder')}
              className={inputCls}
            />
            <p className="mt-1 text-xs text-ink-400">{t('detail.rejectReasonHint')}</p>
            <div className="mt-3 flex gap-2">
              <button
                onClick={() => setMode('view')}
                className="flex-1 rounded-xl border border-ink-200 px-4 py-2.5 text-sm dark:border-white/10"
              >
                {t('form.cancel')}
              </button>
              <button
                disabled={busy || rejectReason.trim().length < 10}
                onClick={() => run(() => recruitmentRequestService.reject(detail.id, rejectReason.trim()))}
                className="flex-1 rounded-xl bg-red-600 px-4 py-2.5 text-sm font-medium text-white disabled:opacity-60"
              >
                {t('detail.confirmReject')}
              </button>
            </div>
          </div>
        )}

        {/* ---- Nút của chủ phiếu ---- */}
        {detail.canEdit && mode === 'view' && (
          <div className="flex flex-wrap gap-2">
            <button
              onClick={() => setMode('edit')}
              className="rounded-xl border border-ink-200 px-4 py-2.5 text-sm font-medium dark:border-white/10"
            >
              {t('detail.edit')}
            </button>
            {detail.status === 'rejected' && (
              <button
                disabled={busy}
                onClick={() => run(() => recruitmentRequestService.resubmit(detail.id))}
                className="flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-medium text-white disabled:opacity-60"
              >
                <Send className="h-4 w-4" /> {t('detail.resubmit')}
              </button>
            )}
            <button
              disabled={busy}
              onClick={() => run(() => recruitmentRequestService.cancel(detail.id))}
              className="flex items-center gap-2 rounded-xl border border-ink-200 px-4 py-2.5 text-sm dark:border-white/10"
            >
              <RotateCcw className="h-4 w-4" /> {t('detail.cancel')}
            </button>
          </div>
        )}

        {/* ---- Thu hồi phê duyệt (ADR-066): nhu cầu đổi sau khi phiếu đã duyệt ----
            Một cờ `canRevoke` do SERVER tính gác cả hai nút — trong đó có điều kiện "chưa dựng thành tin"
            mà giao diện không tự biết được. */}
        {detail.canRevoke && mode === 'view' && (
          <div className="rounded-xl border border-ink-200 p-3 dark:border-white/10">
            <p className="text-sm font-medium text-ink-700 dark:text-ink-200">{t('detail.revokeTitle')}</p>
            <p className="mt-1 text-xs text-ink-400">{t('detail.revokeHint')}</p>
            <div className="mt-3 flex flex-wrap gap-2">
              <button
                disabled={busy}
                onClick={() => openRevoke('reopen')}
                className="flex flex-1 items-center justify-center gap-2 rounded-xl border border-ink-200 px-4 py-2.5 text-sm font-medium dark:border-white/10"
              >
                <RotateCcw className="h-4 w-4" /> {t('detail.reopen')}
              </button>
              <button
                disabled={busy}
                onClick={() => openRevoke('close')}
                className="flex flex-1 items-center justify-center gap-2 rounded-xl border border-red-200 px-4 py-2.5 text-sm font-medium text-red-600 dark:border-red-500/30"
              >
                <Ban className="h-4 w-4" /> {t('detail.close')}
              </button>
            </div>
          </div>
        )}

        {(mode === 'reopen' || mode === 'close') && (
          <div
            className={`rounded-xl border p-3 ${
              mode === 'close' ? 'border-red-200 dark:border-red-500/30' : 'border-ink-200 dark:border-white/10'
            }`}
          >
            <label className={labelCls}>
              {mode === 'close' ? t('detail.closeReason') : t('detail.reopenReason')} *
            </label>
            <textarea
              rows={3}
              value={revokeReason}
              onChange={(e) => setRevokeReason(e.target.value)}
              placeholder={t('detail.revokeReasonPlaceholder')}
              className={inputCls}
            />
            <p className="mt-1 text-xs text-ink-400">
              {mode === 'close' ? t('detail.closeHint') : t('detail.reopenHint')}
            </p>
            <div className="mt-3 flex gap-2">
              <button
                onClick={() => setMode('view')}
                className="flex-1 rounded-xl border border-ink-200 px-4 py-2.5 text-sm dark:border-white/10"
              >
                {t('form.cancel')}
              </button>
              <button
                disabled={busy || revokeReason.trim().length < 10}
                onClick={() =>
                  run(() =>
                    mode === 'close'
                      ? recruitmentRequestService.close(detail.id, revokeReason.trim())
                      : recruitmentRequestService.reopen(detail.id, revokeReason.trim())
                  )
                }
                className={`flex-1 rounded-xl px-4 py-2.5 text-sm font-medium text-white disabled:opacity-60 ${
                  mode === 'close' ? 'bg-red-600' : 'bg-brand-600'
                }`}
              >
                {mode === 'close' ? t('detail.confirmClose') : t('detail.confirmReopen')}
              </button>
            </div>
          </div>
        )}

        {/* ---- Recruiter được phân công dựng tin ---- */}
        {detail.canCreateJob && canComposeJd && (
          <button
            onClick={() => onComposeJd(detail.id)}
            className="flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-medium text-white"
          >
            <Briefcase className="h-4 w-4" /> {t('detail.composeJd')}
          </button>
        )}

        {detail.jobPostingId && (
          <p className="text-xs text-ink-400">{t('detail.jobAlreadyCreated')}</p>
        )}
      </div>
    </Panel>
  )
}

// ===================== Mảnh dùng lại =====================

/**
 * Khung của cột chi tiết. Tự cuộn trong chiều cao màn hình để danh sách bên trái không bị kéo dài
 * theo một phiếu có phần mô tả rất dài — đúng vấn đề của bản dùng hộp thoại trước đây.
 */
function Panel({ children }: { children: React.ReactNode }) {
  return (
    <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5 sm:p-6 lg:max-h-[calc(100vh-8rem)] lg:overflow-y-auto">
      {children}
    </div>
  )
}

/**
 * Chọn các vòng phỏng vấn cho vị trí — THỨ TỰ là một phần của câu trả lời.
 *
 * Vì sao không dùng một ô chọn nhiều (multi-select): thứ tự chính là SỐ VÒNG, mà multi-select thì
 * thứ tự do lúc bấm quyết định và người dùng không nhìn thấy nó. Ở đây danh sách đã chọn hiện thành
 * "1. Trắc nghiệm → 2. Sơ loại", đổi chỗ được, nên thứ nhìn thấy đúng bằng thứ sẽ lưu.
 */
function RoundPicker({
  value,
  onChange,
  t,
}: {
  value: string[]
  onChange: (next: string[]) => void
  t: (key: string, opts?: Record<string, unknown>) => string
}) {
  const label = (r: string) => t(`roundTypes.${roundTypeKey(r)}`)
  // Mỗi loại vòng chỉ một lần (server chặn trùng): loại đã có thì ẩn nút thêm, bỏ vòng đó đi thì nút hiện lại.
  const addable = ROUND_TYPES.filter((r) => !value.includes(r))
  const move = (i: number, delta: number) => {
    const j = i + delta
    if (j < 0 || j >= value.length) return
    const next = [...value]
    ;[next[i], next[j]] = [next[j], next[i]]
    onChange(next)
  }

  return (
    <div className="space-y-2">
      {value.length > 0 && (
        <ul className="space-y-1.5">
          {value.map((r, i) => (
            <li
              key={r}
              className="flex items-center gap-2 rounded-xl border border-ink-200 px-3 py-2 dark:border-white/10"
            >
              <span className="grid h-6 w-6 shrink-0 place-items-center rounded-lg bg-brand-50 text-xs font-bold text-brand-700 dark:bg-brand-500/15 dark:text-brand-400">
                {i + 1}
              </span>
              <span className="min-w-0 flex-1 truncate text-sm text-ink-800 dark:text-ink-100">
                {label(r)}
              </span>
              <button
                type="button"
                onClick={() => move(i, -1)}
                disabled={i === 0}
                className="rounded-lg px-1.5 py-1 text-ink-400 hover:bg-ink-100 disabled:opacity-30 dark:hover:bg-white/10"
                aria-label={t('form.roundsMoveUp')}
              >
                ↑
              </button>
              <button
                type="button"
                onClick={() => move(i, 1)}
                disabled={i === value.length - 1}
                className="rounded-lg px-1.5 py-1 text-ink-400 hover:bg-ink-100 disabled:opacity-30 dark:hover:bg-white/10"
                aria-label={t('form.roundsMoveDown')}
              >
                ↓
              </button>
              <button
                type="button"
                onClick={() => onChange(value.filter((_, idx) => idx !== i))}
                className="rounded-lg px-1.5 py-1 text-ink-400 hover:bg-red-50 hover:text-red-600 dark:hover:bg-red-500/10"
                aria-label={t('form.roundsRemove')}
              >
                ×
              </button>
            </li>
          ))}
        </ul>
      )}

      {addable.length > 0 && (
        <div className="flex flex-wrap gap-1.5">
          {addable.map((r) => (
            <button
              key={r}
              type="button"
              onClick={() => onChange([...value, r])}
              className="inline-flex items-center gap-1 rounded-xl border border-dashed border-ink-300 px-2.5 py-1.5 text-xs text-ink-600 hover:bg-ink-50 dark:border-white/20 dark:text-ink-300 dark:hover:bg-white/5"
            >
              + {label(r)}
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs text-ink-400">{label}</dt>
      <dd className="mt-0.5 whitespace-pre-wrap text-sm text-ink-900 dark:text-white">{value}</dd>
    </div>
  )
}

function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: React.ReactNode }) {
  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-2xl border border-ink-200 bg-white p-6 shadow-xl dark:border-white/10 dark:bg-ink-900">
        <div className="mb-5 flex items-start justify-between gap-3">
          <h3 className="text-lg font-semibold text-ink-900 dark:text-white">{title}</h3>
          <button
            onClick={onClose}
            className="grid h-8 w-8 shrink-0 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
          >
            <X className="h-4 w-4" />
          </button>
        </div>
        {children}
      </div>
    </div>
  )
}
