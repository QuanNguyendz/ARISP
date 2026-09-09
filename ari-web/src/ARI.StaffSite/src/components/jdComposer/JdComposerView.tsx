import { useCallback, useEffect, useMemo, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { ArrowLeft, Loader2, Check, FileDown, Briefcase, FileText, Eye } from 'lucide-react'
import { ErrorAlert, Select } from '@ari/shared/ui'
import { useDocumentViewer } from '@ari/shared/document/DocumentViewer'
import { jdTemplateService, type JdTemplate } from '@ari/shared/fservices/jdTemplate'
import {
  jdDocumentService,
  type JdDocument,
  type JdGeneratedFile,
} from '@ari/shared/fservices/jdDocument'
import { ROLE, normalizeRole } from '@ari/shared/utils/roles'
import { EMPLOYMENT_TYPES, WORK_MODES, EXPERIENCE_LEVELS } from '@ari/shared/utils/jobOptions'
import { useAuthStore } from '@ari/shared/store/auth'

/**
 * Trình soạn bản mô tả công việc theo mẫu công ty (ADR-064).
 *
 * Vì sao tồn tại: phiếu của Hiring Manager chỉ chứa vài dòng gõ vội, nên tin dựng thẳng từ đó quá
 * mỏng để đăng ra career site. Ở đây Recruiter viết bản JD đầy đủ — mở ra đã có sẵn nội dung từ
 * phiếu, không phải trang trắng — rồi xuất file theo mẫu công ty. File đó **gắn thẳng vào tin nháp**
 * và chính là thứ Hiring Manager mở ra để ký duyệt.
 *
 * Một view dùng chung cho Recruiter và HR Leader; server đã lọc phạm vi theo phiếu.
 */

const inputCls =
  'w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none placeholder:text-ink-400 focus:border-brand-400'
const labelCls = 'mb-1.5 block text-sm font-medium text-ink-600 dark:text-ink-300'

/** Nút nào đang chạy — để chỉ đúng nút đó quay vòng chờ. */
type BusyKey = 'save' | 'preview' | 'pdf' | 'docx' | 'job'

/** Đúng phần nội dung gửi lên server — không kèm trường do server quản. */
const toInput = (d: JdDocument) => ({
  title: d.title,
  department: d.department,
  employmentType: d.employmentType,
  workMode: d.workMode,
  location: d.location,
  experienceLevel: d.experienceLevel,
  vacancies: d.vacancies,
  salaryMin: d.salaryMin,
  salaryMax: d.salaryMax,
  salaryCurrency: d.salaryCurrency,
  applicationDeadline: d.applicationDeadline,
  sections: d.sections,
})

/** Chuỗi đại diện nội dung, để so "đã đổi gì chưa" mà không phải so từng trường bằng tay. */
const fingerprint = (d: JdDocument) => JSON.stringify(toInput(d))

/** Màn tạo tin nằm ở khu vực nào — route StaffSite khoá chặt theo vai trò. */
const JOB_CREATE_PATH: Record<string, string | undefined> = {
  [ROLE.Recruiter]: '/recruiter/jobs/create',
  [ROLE.HRAdmin]: '/hr/jobs/create',
}

export default function JdComposerView() {
  const { t } = useTranslation('modules/staff/jdComposer')
  const { id: requestId } = useParams<{ id: string }>()
  const navigate = useNavigate()
  const { openDocument } = useDocumentViewer()
  const role = normalizeRole(useAuthStore((s) => s.user?.role))

  const [template, setTemplate] = useState<JdTemplate | null>(null)
  const [doc, setDoc] = useState<JdDocument | null>(null)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState<BusyKey | null>(null)
  const [error, setError] = useState('')
  const [savedAt, setSavedAt] = useState<number | null>(null)

  /**
   * Dấu vân tay của bản ĐÃ LƯU trên server.
   *
   * Dùng để biết có gì thay đổi chưa: không đổi thì nút Lưu nháp **khoá lại** (lưu một bản y hệt
   * là một lượt ghi DB vô nghĩa), và xem trước biết khi nào được dùng lại file cũ thay vì dựng lại.
   * So bằng chính thứ gửi lên server (`toInput`) chứ không so cả `JdDocument`: các trường do
   * server quản (`generatedAt`, `updatedAt`…) đổi sau mỗi lượt lưu thì nút sẽ không bao giờ khoá lại.
   */
  const [savedFingerprint, setSavedFingerprint] = useState<string | null>(null)

  const backHref =
    role === ROLE.HRAdmin ? '/hr/recruitment-requests' : '/recruiter/recruitment-requests'

  const load = useCallback(async () => {
    if (!requestId) return
    setLoading(true)
    setError('')
    try {
      const [tpl, jd] = await Promise.all([
        jdTemplateService.get(),
        jdDocumentService.get(requestId),
      ])
      setTemplate(tpl)
      setDoc(jd)
      setSavedFingerprint(fingerprint(jd))
    } catch (e: any) {
      setError(e?.response?.data?.message || t('errors.loadFailed'))
    } finally {
      setLoading(false)
    }
  }, [requestId, t])

  useEffect(() => {
    void load()
  }, [load])

  /** Chỉ soạn những mục mẫu đang BẬT — mục tắt vẫn giữ nội dung cũ, bật lại là có ngay. */
  const activeSections = useMemo(
    () => (template?.sections ?? []).filter((s) => s.enabled),
    [template]
  )

  const patch = (changes: Partial<JdDocument>) =>
    setDoc((prev) => (prev ? { ...prev, ...changes } : prev))

  const setSection = (key: string, value: string) =>
    setDoc((prev) => (prev ? { ...prev, sections: { ...prev.sections, [key]: value } } : prev))


  /**
   * Xuất file. **LƯU TRƯỚC rồi mới xuất**: server dựng file từ bản đã lưu, nên bỏ qua bước lưu thì
   * người dùng nhận về file thiếu đúng những gì họ vừa gõ — và không có gì báo.
   *
   * `busyKey` tách khỏi `format` để đúng cái nút vừa bấm quay vòng chờ, thay vì cả cụm nút cùng nhấp nháy.
   */
  const generate = async (
    format: 'pdf' | 'docx',
    busyKey: BusyKey = format
  ): Promise<JdGeneratedFile | null> => {
    if (!doc || !requestId) return null
    setBusy(busyKey)
    setError('')
    try {
      await jdDocumentService.save(requestId, toInput(doc))
      const file = await jdDocumentService.generate(requestId, format)
      setDoc((prev) =>
        prev
          ? {
              ...prev,
              generatedFileStorageKey: file.storageKey,
              generatedFileName: file.fileName,
              generatedFormat: file.format,
              generatedFileViewUrl: file.viewUrl,
              generatedAt: new Date().toISOString(),
            }
          : prev
      )
      setSavedFingerprint(fingerprint(doc))
      return file
    } catch (e: any) {
      setError(e?.response?.data?.message || t('errors.generateFailed'))
      return null
    } finally {
      setBusy(null)
    }
  }

  /**
   * Định dạng của file đang đính kèm. Mọi lần dựng lại đều **giữ nguyên định dạng đó**: người dùng
   * chọn DOCX rồi bấm Lưu nháp mà file âm thầm thành PDF là đổi mất thứ sẽ đính sang tin tuyển dụng.
   */
  const attachedFormat: 'pdf' | 'docx' = doc?.generatedFormat === 'docx' ? 'docx' : 'pdf'

  /**
   * Lưu nháp = lưu nội dung **VÀ** dựng lại file đính kèm trong cùng một thao tác.
   *
   * Bản đầu chỉ lưu nội dung, nên sau khi sửa xong bấm Lưu nháp rồi mở file ra vẫn là **bản cũ** —
   * người dùng đọc đó là "hệ thống lưu hỏng", hoàn toàn hợp lý. File đính kèm là thứ Hiring Manager
   * mở ra để ký duyệt (ADR-064), nên nó phải luôn khớp với bản đã lưu chứ không chờ một nút riêng.
   *
   * Server xoá bản cũ ngay trong lượt dựng này, nên không để lại file mồ côi.
   */
  const saveDraft = async () => {
    const file = await generate(attachedFormat, 'save')
    if (file) setSavedAt(Date.now())
  }

  const download = async (format: 'pdf' | 'docx') => {
    const file = await generate(format)
    if (file) window.open(file.viewUrl, '_blank', 'noopener')
  }

  /** Còn thay đổi chưa lưu hay không. */
  const isDirty = doc != null && savedFingerprint != null && fingerprint(doc) !== savedFingerprint

  /**
   * Xem trước = MỞ CHÍNH FILE sẽ được đính kèm, không phải một bản vẽ lại ở trình duyệt.
   *
   * Bản cũ dựng lại tờ A4 bằng HTML và tự phân trang theo chiều cao đo được trong trình duyệt, trong
   * khi file thật do PdfSharpCore dựng bằng phông và số đo của nó — hai bộ dựng khác nhau thì chỗ
   * ngắt trang, nhãn và khoảng cách **không bao giờ trùng nhau**, và mỗi lần sửa một bên là chúng lại
   * trôi xa nhau thêm. Đây là đúng bài học "một bố cục, hai bộ xuất" của ADR-064, lần này áp cho bản
   * xem trước: bỏ hẳn bộ dựng thứ hai thay vì đi đuổi cho giống.
   *
   * Chưa sửa gì và đã có sẵn file PDF thì mở lại chính nó — không dựng lại một bản y hệt rồi đè lên
   * storage.
   */
  const openPreview = async () => {
    if (!doc) return

    if (!isDirty && doc.generatedFileViewUrl) {
      openDocument(doc.generatedFileViewUrl, doc.generatedFileName || 'JD')
      return
    }

    const file = await generate(attachedFormat, 'preview')
    if (file) openDocument(file.viewUrl, file.fileName)
  }

  /** Xuất PDF rồi sang màn tạo tin — file đi kèm, không phải tải xuống rồi tải lên lại. */
  const buildJob = async () => {
    const path = JOB_CREATE_PATH[role]
    if (!path || !requestId) return
    // Luôn PDF cho đường này (không theo `attachedFormat`): file đi kèm sang màn tạo tin là thứ
    // Hiring Manager mở ra để ký duyệt, và trình xem tài liệu mở PDF nhanh gọn hơn DOCX.
    const file = await generate('pdf', 'job')
    // Không cần tham số riêng cho bản JD: nó tra được bằng chính `requestId`. Màn tạo tin tự nạp
    // và ưu tiên bản JD nếu đã xuất file — thêm một tham số nữa chỉ tạo thêm chỗ để sai lệch.
    if (file) navigate(`${path}?requestId=${requestId}`)
  }

  if (loading || !doc || !template) {
    return (
      <div className="p-6 lg:p-8">
        {error ? (
          <ErrorAlert message={error} />
        ) : (
          <div className="flex items-center gap-2 py-16 text-sm text-ink-500">
            <Loader2 className="h-4 w-4 animate-spin" /> {t('loading')}
          </div>
        )}
      </div>
    )
  }

  return (
    <div className="p-4 sm:p-6 lg:p-8">
      <button
        onClick={() => navigate(backHref)}
        className="mb-3 inline-flex items-center gap-2 text-sm text-ink-500 hover:text-ink-800 dark:text-ink-400 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> {t('back')}
      </button>

      <h1 className="text-2xl font-bold text-ink-900 dark:text-white">{t('title')}</h1>
      <p className="mb-6 mt-1 text-sm text-ink-500 dark:text-ink-400">{t('description')}</p>

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      <div className="grid grid-cols-1 items-start gap-5 xl:grid-cols-[minmax(0,1fr)_minmax(0,24rem)]">
        {/* ------------------------------- Soạn ------------------------------- */}
        <div className="space-y-5">
          <section className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5 sm:p-6">
            <h2 className="mb-4 text-sm font-semibold text-ink-900 dark:text-white">
              {t('basics.title')}
            </h2>

            <div className="space-y-3">
              <div>
                <label className={labelCls}>{t('basics.position')} *</label>
                <input
                  value={doc.title}
                  onChange={(e) => patch({ title: e.target.value })}
                  className={inputCls}
                />
              </div>

              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                <div>
                  <label className={labelCls}>{t('basics.department')}</label>
                  <input
                    value={doc.department ?? ''}
                    onChange={(e) => patch({ department: e.target.value })}
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className={labelCls}>{t('basics.location')}</label>
                  <input
                    value={doc.location ?? ''}
                    onChange={(e) => patch({ location: e.target.value })}
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className={labelCls}>{t('basics.vacancies')}</label>
                  <input
                    type="number"
                    min={1}
                    value={doc.vacancies ?? ''}
                    onChange={(e) =>
                      patch({ vacancies: e.target.value === '' ? null : Number(e.target.value) })
                    }
                    className={inputCls}
                  />
                </div>
              </div>

              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                <div>
                  <label className={labelCls}>{t('basics.salaryMin')}</label>
                  <input
                    type="number"
                    min={0}
                    value={doc.salaryMin ?? ''}
                    onChange={(e) =>
                      patch({ salaryMin: e.target.value === '' ? null : Number(e.target.value) })
                    }
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className={labelCls}>{t('basics.salaryMax')}</label>
                  <input
                    type="number"
                    min={0}
                    value={doc.salaryMax ?? ''}
                    onChange={(e) =>
                      patch({ salaryMax: e.target.value === '' ? null : Number(e.target.value) })
                    }
                    className={inputCls}
                  />
                </div>
                <div>
                  <label className={labelCls}>{t('basics.currency')}</label>
                  <input
                    value={doc.salaryCurrency ?? 'VND'}
                    onChange={(e) => patch({ salaryCurrency: e.target.value })}
                    className={inputCls}
                  />
                </div>
              </div>

              {/*
                Ba ô phân loại + hạn nộp: điền sẵn từ phiếu, nhưng PHẢI sửa được ở đây. Trước đây
                chúng không hiện ra mà vẫn đi thẳng vào file JD, nên bản in ra ghi "Thời gian:
                full_time" — một giá trị mặc định chưa ai từng chọn.
              */}
              <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                <div>
                  <label className={labelCls}>{t('basics.employmentType')}</label>
                  <Select
                    value={doc.employmentType ?? 'full_time'}
                    onChange={(v) => patch({ employmentType: v })}
                    ariaLabel={t('basics.employmentType')}
                    className="w-full"
                    buttonClassName="px-3 py-2.5 text-sm"
                    options={EMPLOYMENT_TYPES}
                  />
                </div>
                <div>
                  <label className={labelCls}>{t('basics.experienceLevel')}</label>
                  <Select
                    value={doc.experienceLevel ?? 'middle'}
                    onChange={(v) => patch({ experienceLevel: v })}
                    ariaLabel={t('basics.experienceLevel')}
                    className="w-full"
                    buttonClassName="px-3 py-2.5 text-sm"
                    options={EXPERIENCE_LEVELS}
                  />
                </div>
                <div>
                  <label className={labelCls}>{t('basics.workMode')}</label>
                  <Select
                    value={doc.workMode ?? 'onsite'}
                    onChange={(v) => patch({ workMode: v })}
                    ariaLabel={t('basics.workMode')}
                    className="w-full"
                    buttonClassName="px-3 py-2.5 text-sm"
                    options={WORK_MODES}
                  />
                </div>
              </div>

              <div className="sm:max-w-xs">
                <label className={labelCls}>{t('basics.deadline')}</label>
                <input
                  type="date"
                  value={doc.applicationDeadline ? doc.applicationDeadline.slice(0, 10) : ''}
                  onChange={(e) =>
                    patch({
                      applicationDeadline: e.target.value
                        ? new Date(e.target.value).toISOString()
                        : null,
                    })
                  }
                  className={inputCls}
                />
              </div>
            </div>
          </section>

          {activeSections.map((section) => (
            <section
              key={section.key}
              className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5 sm:p-6"
            >
              <h2 className="text-sm font-semibold text-ink-900 dark:text-white">
                {section.title}
              </h2>
              {section.hint && <p className="mb-3 mt-1 text-xs text-ink-400">{section.hint}</p>}
              <textarea
                rows={6}
                value={doc.sections[section.key] ?? ''}
                onChange={(e) => setSection(section.key, e.target.value)}
                placeholder={t('sectionPlaceholder')}
                className={inputCls}
              />
              <p className="mt-1.5 text-xs text-ink-400">{t('sectionLineHint')}</p>
            </section>
          ))}
        </div>

        {/* ------------------------------- Thao tác -------------------------------
            Cột này BÁM THEO khi cuộn: kéo xuống thì thẻ thao tác đi theo, kéo lên hết thì nó dừng
            đúng ngang thẻ "Thông tin chung" (vị trí tự nhiên của nó).

            Khuôn hai lớp — cột ngoài giãn hết chiều cao hàng (`self-stretch` để thắng `items-start`
            của lưới), thẻ bên trong mới `sticky` — nên quãng chạy không phụ thuộc vào cách trình
            duyệt tính vùng lưới cho một item đã `align-self: start`.

            Phần cuộn là **BODY** (`WorkspaceLayout` để vỏ ngoài `min-h-screen`, không chặn tràn).
            Sticky ở đây từng chết vì `<main>` có `overflow-auto` thừa: nó tạo ra một scrollport đứng
            yên và sticky bám vào đó. Xem chú thích tại `WorkspaceLayout` trước khi thêm lại `overflow`
            vào bất kỳ tổ tiên nào của trang. */}
        <div className="xl:self-stretch">
          <div className="space-y-4 xl:sticky xl:top-[var(--sticky-top,1.5rem)]">
            <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5">
              <button
                onClick={buildJob}
                disabled={busy !== null}
                className="flex w-full items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-medium text-white disabled:opacity-60"
              >
                {busy === 'job' ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Briefcase className="h-4 w-4" />
                )}
                {t('actions.buildJob')}
              </button>
              <p className="mt-2 text-xs text-ink-400">{t('actions.buildJobHint')}</p>

              <div className="mt-4 grid grid-cols-2 gap-2">
                <button
                  onClick={() => download('pdf')}
                  disabled={busy !== null}
                  className="inline-flex items-center justify-center gap-1.5 rounded-xl border border-ink-200 px-3 py-2.5 text-sm font-medium text-ink-700 disabled:opacity-60 dark:border-white/10 dark:text-ink-200"
                >
                  {busy === 'pdf' ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <FileDown className="h-4 w-4" />
                  )}
                  PDF
                </button>
                <button
                  onClick={() => download('docx')}
                  disabled={busy !== null}
                  className="inline-flex items-center justify-center gap-1.5 rounded-xl border border-ink-200 px-3 py-2.5 text-sm font-medium text-ink-700 disabled:opacity-60 dark:border-white/10 dark:text-ink-200"
                >
                  {busy === 'docx' ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <FileDown className="h-4 w-4" />
                  )}
                  DOCX
                </button>
              </div>

              <button
                onClick={openPreview}
                disabled={busy !== null}
                className="mt-2 inline-flex w-full items-center justify-center gap-2 rounded-xl border border-ink-200 px-4 py-2.5 text-sm text-ink-700 disabled:opacity-60 dark:border-white/10 dark:text-ink-200"
              >
                {busy === 'preview' ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Eye className="h-4 w-4" />
                )}{' '}
                {t('actions.preview')}
              </button>

              {/* Không có thay đổi thì khoá lại: lưu một bản y hệt là một lượt ghi DB vô nghĩa, và
                  câu "Đã lưu nháp" hiện ra sau đó khiến người dùng tưởng vừa có gì được ghi. */}
              <button
                onClick={saveDraft}
                disabled={busy !== null || !isDirty}
                className="mt-2 inline-flex w-full items-center justify-center gap-2 rounded-xl border border-ink-200 px-4 py-2.5 text-sm text-ink-700 disabled:opacity-60 dark:border-white/10 dark:text-ink-200"
              >
                {busy === 'save' ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Check className="h-4 w-4" />
                )}
                {t('actions.saveDraft')}
              </button>

              <p className="mt-2 text-center text-xs text-ink-400">{t('actions.saveDraftHint')}</p>

              {savedAt && (
                <p className="mt-1 text-center text-xs text-emerald-600">{t('actions.savedOk')}</p>
              )}
            </div>

            {doc.generatedFileViewUrl && (
              <div>
                <button
                  onClick={() =>
                    openDocument(doc.generatedFileViewUrl!, doc.generatedFileName || 'JD')
                  }
                  className="flex w-full items-center gap-2 rounded-2xl border border-ink-200 bg-white p-4 text-left text-sm shadow-card hover:border-brand-300 dark:border-white/10 dark:bg-white/5"
                >
                  <FileText className="h-4 w-4 shrink-0 text-brand-600 dark:text-brand-400" />
                  <span className="min-w-0 flex-1 truncate text-ink-700 dark:text-ink-200">
                    {doc.generatedFileName}
                  </span>
                  <span className="shrink-0 text-xs uppercase text-ink-400">
                    {doc.generatedFormat}
                  </span>
                </button>

                {/* File là ẢNH CHỤP tại lần xuất gần nhất, không tự chạy theo ô đang gõ. Im lặng thì
                    người dùng mở ra thấy bản cũ và tưởng hệ thống lưu hỏng — đúng thứ vừa bị báo. */}
                {isDirty && (
                  <p className="mt-2 text-xs text-amber-600 dark:text-amber-400">
                    {t('actions.fileStale')}
                  </p>
                )}
              </div>
            )}
          </div>
        </div>
      </div>

    </div>
  )
}
