import { useEffect, useMemo, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import {
  BookOpen,
  Plus,
  Trash2,
  UploadCloud,
  X,
  Loader2,
  FileText,
  Building2,
  Briefcase,
  Layers,
  AlertCircle,
  CheckCircle2,
  Download,
  Scale,
  Info,
  ArrowRight,
} from 'lucide-react'
import { PageHeader, ErrorAlert, EmptyState, Pagination, Select } from '@ari/shared/ui'
import { CardGridSkeleton } from './_skeletons'
import { playbookService, isRubricDocType } from '@/fservices/playbook/playbookService'
import type { PlaybookItem } from '@/fservices/playbook/playbookService'
import jobService from '@ari/shared/fservices/job'
import { resolveApiError } from '@ari/shared/utils/apiError'

/**
 * Playbook của HR Leader (ADR-025 · ADR-069).
 *
 * Màn này quản lý playbook CÔNG TY — thứ áp cho mọi tin (văn phong, văn hoá, điều cấm hỏi, bộ tiêu chí chung).
 * Playbook THEO TIN / THEO VÒNG là nội dung chuyên môn của từng vị trí nên do Hiring Manager chính thêm ngay
 * trong màn tin; ở đây chúng chỉ hiện để HR Leader nhìn toàn cảnh, kèm lối vào đúng tin để sửa. Trước đây màn
 * này là nơi DUY NHẤT thêm được playbook theo tin, trong khi HM — người biết cần hỏi gì — không vào được.
 */
export default function HrPlaybooksPage() {
  const { t } = useTranslation('modules/hr/playbooks')

  const DOC_TYPES: [string, string][] = [
    ['style_guide', t('docTypes.styleGuide')],
    ['question_bank', t('docTypes.questionBank')],
    ['competency_framework', t('docTypes.competencyFramework')],
    ['culture_guide', t('docTypes.cultureGuide')],
    ['compliance', t('docTypes.compliance')],
    ['red_flag', t('docTypes.redFlag')],
    ['technical_scenario', t('docTypes.technicalScenario')],
    ['expected_answer', t('docTypes.expectedAnswer')],
    ['must_ask', t('docTypes.mustAsk')],
    ['round_playbook', t('docTypes.roundPlaybook')],
    ['cv_rubric', t('docTypes.cvRubric')],
    ['interview_rubric', t('docTypes.interviewRubric')],
  ]

  const SCOPES: [string, string][] = [
    ['org', t('scopes.org')],
    ['job_posting', t('scopes.jobPosting')],
    ['round', t('scopes.round')],
  ]

  const docTypeLabel = (dt: string) => DOC_TYPES.find(([v]) => v === dt)?.[1] || dt
  const scopeLabel = (s: string) => SCOPES.find(([v]) => v === s)?.[1] || s
  const scopeBadge = (s: string) =>
    (
      ({
        org: 'bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400',
        job_posting: 'bg-ai-100 dark:bg-ai-500/20 text-ai-700 dark:text-ai-400',
        round: 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400',
      }) as Record<string, string>
    )[s] || 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-300'

  const scopeIcon = (s: string) =>
    s === 'org' ? Building2 : s === 'job_posting' ? Briefcase : Layers

  const [docs, setDocs] = useState<PlaybookItem[]>([])
  const [jobTitles, setJobTitles] = useState<Record<string, string>>({})
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [filter, setFilter] = useState('all')
  const [showUpload, setShowUpload] = useState(false)
  const [deletingId, setDeletingId] = useState<string | null>(null)
  const [page, setPage] = useState(1)

  const load = async () => {
    setLoading(true)
    setError('')
    try {
      const [items, jobs] = await Promise.all([
        playbookService.getPlaybooks(),
        // Tên tin cho các thẻ playbook theo tin — hỏng thì thẻ vẫn hiện, chỉ thiếu tên.
        jobService.getAdminJobPostings().catch(() => []),
      ])
      setDocs(items)
      setJobTitles(Object.fromEntries(jobs.map((j) => [j.id, j.title])))
    } catch (e: any) {
      setError(resolveApiError(e, t, 'loadingError'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [t])

  const filtered = useMemo(
    () => (filter === 'all' ? docs : docs.filter((d) => d.scope === filter)),
    [docs, filter]
  )

  const PAGE_SIZE = 10
  const totalPages = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE))
  const paged = useMemo(
    () => filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE),
    [filtered, page]
  )

  useEffect(() => {
    setPage(1)
  }, [filter])

  const remove = async (id: string) => {
    setDeletingId(id)
    try {
      await playbookService.deletePlaybook(id)
      setDocs((prev) => prev.filter((d) => d.id !== id))
    } catch (e: any) {
      setError(resolveApiError(e, t, 'deleteError'))
    } finally {
      setDeletingId(null)
    }
  }

  return (
    <div className="min-h-screen bg-ink-50 p-6 dark:bg-ink-950 lg:p-8">
      <PageHeader
        title={t('title')}
        description={t('description')}
        actions={[
          { label: t('uploadPlaybook'), onClick: () => setShowUpload(true), variant: 'primary' },
        ]}
      />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      <div className="mb-6 flex items-start gap-2 rounded-xl border border-ai-200 bg-ai-50/60 px-4 py-3 text-sm text-ai-800 dark:border-ai-500/20 dark:bg-ai-500/10 dark:text-ai-300">
        <Info className="mt-0.5 h-4 w-4 shrink-0" /> {t('jobScopedHint')}
      </div>

      <div className="mb-6 flex flex-wrap gap-2">
        {[['all', t('filters.all')], ...SCOPES].map(([v, l]) => (
          <button
            key={v}
            onClick={() => setFilter(v)}
            className={`rounded-xl px-4 py-2 text-sm font-medium transition-colors ${filter === v ? 'bg-brand-600 text-white' : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-600 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10'}`}
          >
            {l}
          </button>
        ))}
      </div>

      {loading ? (
        <CardGridSkeleton count={6} />
      ) : filtered.length === 0 ? (
        <EmptyState
          icon={<BookOpen className="h-8 w-8 text-ink-400" />}
          title={t('noPlaybooks')}
          description={t('noPlaybooksHint')}
          action={{ label: t('uploadPlaybook'), onClick: () => setShowUpload(true) }}
        />
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {paged.map((d, i) => {
            const Icon = scopeIcon(d.scope)
            const isOrg = d.scope === 'org'
            return (
              <motion.div
                key={d.id}
                initial={{ opacity: 0, y: 16 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: Math.min(i, 9) * 0.04 }}
                className="group rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card"
              >
                <div className="mb-3 flex items-start justify-between gap-2">
                  <span
                    className={`grid h-11 w-11 shrink-0 place-items-center rounded-xl ${
                      isRubricDocType(d.documentType)
                        ? 'bg-ai-100 text-ai-600 dark:bg-ai-500/20 dark:text-ai-400'
                        : 'bg-brand-100 text-brand-600 dark:bg-brand-500/20 dark:text-brand-400'
                    }`}
                  >
                    {isRubricDocType(d.documentType) ? (
                      <Scale className="h-5 w-5" />
                    ) : (
                      <BookOpen className="h-5 w-5" />
                    )}
                  </span>
                  <div className="flex items-center gap-2">
                    <span
                      className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium ${scopeBadge(d.scope)}`}
                    >
                      <Icon className="h-3 w-3" /> {scopeLabel(d.scope)}
                      {d.roundNumber ? ` · V${d.roundNumber}` : ''}
                    </span>
                    {/* Xoá ở đây chỉ dành cho playbook công ty — playbook theo tin quản lý tại màn tin. */}
                    {isOrg && (
                      <button
                        onClick={() => remove(d.id)}
                        disabled={deletingId === d.id}
                        className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 opacity-0 transition-opacity hover:bg-red-50 hover:text-red-500 group-hover:opacity-100 dark:hover:bg-red-500/10"
                      >
                        {deletingId === d.id ? (
                          <Loader2 className="h-4 w-4 animate-spin" />
                        ) : (
                          <Trash2 className="h-4 w-4" />
                        )}
                      </button>
                    )}
                  </div>
                </div>
                <h3 className="font-semibold text-ink-900 dark:text-white">
                  {docTypeLabel(d.documentType)}
                </h3>
                <p className="mt-0.5 flex items-center gap-1 truncate text-xs text-ink-500 dark:text-ink-400">
                  <FileText className="h-3 w-3" /> {d.fileName}
                </p>
                {!isOrg && d.scopeRefId && (
                  <Link
                    to={`/hr/jobs/${d.scopeRefId}`}
                    className="mt-2 flex items-center gap-1 truncate text-xs font-medium text-brand-600 hover:underline dark:text-brand-400"
                  >
                    <Briefcase className="h-3 w-3 shrink-0" />
                    <span className="truncate">{jobTitles[d.scopeRefId] ?? t('unknownJob')}</span>
                    <span className="shrink-0">· {t('openJob')}</span>
                    <ArrowRight className="h-3 w-3 shrink-0" />
                  </Link>
                )}
                <div className="mt-3 flex items-center justify-between border-t border-ink-100 pt-3 text-xs dark:border-white/10">
                  <span
                    className={`rounded-full px-2 py-0.5 ${d.status === 'ready' ? 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400' : 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400'}`}
                  >
                    {d.status === 'ready' ? t('status.ready') : d.status}
                  </span>
                  <span className="flex items-center gap-2">
                    {/* Số tiêu chí: phân biệt bộ đã đọc được với file chỉ mới nằm đó. */}
                    {d.criteriaCount != null && (
                      <span className="rounded-full bg-ai-100 px-2 py-0.5 text-ai-700 dark:bg-ai-500/20 dark:text-ai-400">
                        {t('criteriaCount', { count: d.criteriaCount })}
                      </span>
                    )}
                    <span className="uppercase text-ink-400">{d.fileFormat}</span>
                  </span>
                </div>
              </motion.div>
            )
          })}
        </div>
      )}

      {!loading && filtered.length > 0 && (
        <Pagination
          page={page}
          totalPages={totalPages}
          total={filtered.length}
          label={t('paginationLabel')}
          onPageChange={setPage}
        />
      )}

      {showUpload && (
        <UploadModal
          t={t}
          docTypes={DOC_TYPES}
          onClose={() => setShowUpload(false)}
          onUploaded={(doc) => {
            setDocs((prev) => [doc, ...prev])
            setShowUpload(false)
          }}
        />
      )}
    </div>
  )
}

/** Thêm playbook CÔNG TY — phạm vi cố định `org` (playbook theo tin thêm ở màn tin, ADR-069). */
function UploadModal({
  t,
  docTypes,
  onClose,
  onUploaded,
}: {
  t: (key: string) => string
  docTypes: [string, string][]
  onClose: () => void
  onUploaded: (doc: PlaybookItem) => void
}) {
  const fileRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [documentType, setDocumentType] = useState('style_guide')
  const [downloadingTemplate, setDownloadingTemplate] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  const isRubric = isRubricDocType(documentType)

  /**
   * Đổi loại tài liệu thì BỎ file đang chọn nếu phần mở rộng không còn hợp lệ: rubric chỉ nhận
   * .xlsx, tài liệu văn xuôi thì ngược lại — giữ nguyên là người dùng bấm Tải lên rồi mới ăn lỗi
   * từ server mà không hiểu vì sao.
   */
  const changeDocumentType = (next: string) => {
    setDocumentType(next)
    setError('')
    const ext = file?.name.slice(file.name.lastIndexOf('.')).toLowerCase()
    if (ext && (isRubricDocType(next) ? ext !== '.xlsx' : ext === '.xlsx')) {
      setFile(null)
      if (fileRef.current) fileRef.current.value = ''
    }
  }

  const downloadTemplate = async () => {
    setDownloadingTemplate(true)
    setError('')
    try {
      await playbookService.downloadRubricTemplate(documentType)
    } catch {
      setError(t('templateDownloadError'))
    } finally {
      setDownloadingTemplate(false)
    }
  }

  const submit = async () => {
    if (!file) {
      setError(t('selectFileError'))
      return
    }
    setSubmitting(true)
    setError('')
    try {
      const doc = await playbookService.uploadPlaybook({ file, scope: 'org', documentType })
      onUploaded(doc)
    } catch (e: any) {
      setError(resolveApiError(e, t, 'uploadError'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <motion.div
        initial={{ opacity: 0, scale: 0.96, y: 10 }}
        animate={{ opacity: 1, scale: 1, y: 0 }}
        className="relative w-full max-w-lg rounded-2xl border border-ink-200 dark:border-white/10 bg-white p-6 shadow-xl dark:bg-ink-900"
      >
        <div className="mb-4 flex items-start justify-between">
          <div>
            <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
              {t('uploadTitle')}
            </h3>
            <p className="mt-1 text-xs text-ink-500 dark:text-ink-400">{t('uploadHint')}</p>
          </div>
          <button
            onClick={onClose}
            className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {error && (
          <div className="mb-4 flex items-center gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
            <AlertCircle className="h-4 w-4" /> {error}
          </div>
        )}

        <div className="space-y-4">
          {/* Rubric là bảng số liệu: không đưa file mẫu thì HR không có cách nào đoán đúng layout
              (mã tiêu chí / tên / trọng số / chuẩn chấm) và tổng trọng số phải bằng 100. */}
          {isRubric && (
            <div className="rounded-xl border border-ai-200 bg-ai-50/60 p-3 dark:border-ai-500/20 dark:bg-ai-500/10">
              <p className="text-xs text-ai-800 dark:text-ai-300">{t('rubricHint')}</p>
              <button
                type="button"
                onClick={downloadTemplate}
                disabled={downloadingTemplate}
                className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-ai-300 bg-white px-3 py-1.5 text-xs font-semibold text-ai-700 hover:bg-ai-50 disabled:opacity-50 dark:border-ai-500/30 dark:bg-white/5 dark:text-ai-300"
              >
                {downloadingTemplate ? (
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                ) : (
                  <Download className="h-3.5 w-3.5" />
                )}
                {t('downloadTemplate')}
              </button>
            </div>
          )}

          <input
            ref={fileRef}
            type="file"
            accept={isRubric ? '.xlsx' : '.pdf,.docx,.txt,.md'}
            onChange={(e) => setFile(e.target.files?.[0] || null)}
            className="hidden"
          />
          <button
            type="button"
            onClick={() => fileRef.current?.click()}
            className="flex w-full items-center gap-3 rounded-xl border border-dashed border-ink-300 dark:border-white/20 bg-ink-50 dark:bg-white/5 px-4 py-3 text-sm text-ink-600 dark:text-ink-300 hover:border-brand-400"
          >
            {file ? (
              <CheckCircle2 className="h-5 w-5 text-emerald-500" />
            ) : (
              <UploadCloud className="h-5 w-5 text-ink-400" />
            )}
            <span className="truncate">{file ? file.name : t('selectFile')}</span>
          </button>

          <div>
            <label className="mb-1.5 block text-sm font-medium text-ink-700 dark:text-ink-200">
              {t('documentType')}
            </label>
            <Select
              value={documentType}
              onChange={changeDocumentType}
              className="w-full"
              buttonClassName="px-3 py-2.5 text-sm"
              options={docTypes.map(([v, l]) => ({ value: v, label: l }))}
            />
          </div>
        </div>

        <div className="mt-5 flex justify-end gap-2">
          <button
            onClick={onClose}
            className="rounded-xl border border-ink-200 px-4 py-2.5 text-sm font-medium text-ink-700 hover:bg-ink-50 dark:border-white/10 dark:text-ink-200 dark:hover:bg-white/10"
          >
            {t('cancel')}
          </button>
          <button
            onClick={submit}
            disabled={submitting || !file}
            className="flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2.5 text-sm font-semibold text-white hover:opacity-90 disabled:opacity-50"
          >
            {submitting ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Plus className="h-4 w-4" />
            )}
            {t('upload')}
          </button>
        </div>
      </motion.div>
    </div>
  )
}
