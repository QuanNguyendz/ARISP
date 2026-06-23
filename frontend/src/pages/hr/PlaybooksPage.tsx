import { useEffect, useMemo, useRef, useState } from 'react'
import { motion } from 'framer-motion'
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
} from 'lucide-react'
import { PageHeader, ErrorAlert, EmptyState } from '@components/shared'
import { CardGridSkeleton } from './_skeletons'
import { playbookService, type PlaybookItem } from '@services/playbook/playbookService'
import jobService from '@services/job/jobService'
import type { JobPosting } from '@/types/job'

const DOC_TYPES: [string, string][] = [
  ['style_guide', 'Hướng dẫn phong cách'],
  ['question_bank', 'Ngân hàng câu hỏi'],
  ['competency_framework', 'Khung năng lực'],
  ['culture_guide', 'Văn hóa & giá trị'],
  ['compliance', 'Quy định không được hỏi'],
  ['red_flag', 'Red flag guide'],
  ['technical_scenario', 'Kịch bản kỹ thuật'],
  ['expected_answer', 'Đáp án mong đợi'],
  ['must_ask', 'Câu hỏi bắt buộc'],
  ['round_playbook', 'Playbook theo vòng'],
]
const docTypeLabel = (t: string) => DOC_TYPES.find(([v]) => v === t)?.[1] || t

const SCOPES: [string, string][] = [
  ['org', 'Công ty'],
  ['job_posting', 'Theo tin'],
  ['round', 'Theo vòng'],
]
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

export default function HrPlaybooksPage() {
  const [docs, setDocs] = useState<PlaybookItem[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const [filter, setFilter] = useState('all')
  const [showUpload, setShowUpload] = useState(false)
  const [deletingId, setDeletingId] = useState<string | null>(null)

  const load = async () => {
    setLoading(true)
    setError('')
    try {
      setDocs(await playbookService.getPlaybooks())
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không tải được danh sách playbook.')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    load()
  }, [])

  const filtered = useMemo(
    () => (filter === 'all' ? docs : docs.filter((d) => d.scope === filter)),
    [docs, filter]
  )

  const remove = async (id: string) => {
    setDeletingId(id)
    try {
      await playbookService.deletePlaybook(id)
      setDocs((prev) => prev.filter((d) => d.id !== id))
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Không thể xoá playbook.')
    } finally {
      setDeletingId(null)
    }
  }

  return (
    <div className="min-h-screen bg-ink-50 p-6 dark:bg-ink-950 lg:p-8">
      <PageHeader
        title="Interview Playbook"
        description="Tài liệu phỏng vấn nội bộ — AI nạp vào RAG để hỏi đúng phong cách & nội dung công ty"
        actions={[
          { label: 'Upload playbook', onClick: () => setShowUpload(true), variant: 'primary' },
        ]}
      />

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      <div className="mb-6 flex flex-wrap gap-2">
        {[['all', 'Tất cả'], ...SCOPES].map(([v, l]) => (
          <button
            key={v}
            onClick={() => setFilter(v)}
            className={`rounded-xl px-4 py-2 text-sm font-medium transition-colors ${
              filter === v
                ? 'bg-brand-600 text-white'
                : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-600 dark:text-ink-300 hover:bg-ink-50 dark:hover:bg-white/10'
            }`}
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
          title="Chưa có playbook"
          description="Upload tài liệu phỏng vấn (PDF/DOCX/TXT/MD) để AI phỏng vấn đúng phong cách công ty."
          action={{ label: 'Upload playbook', onClick: () => setShowUpload(true) }}
        />
      ) : (
        <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
          {filtered.map((d, i) => {
            const Icon = scopeIcon(d.scope)
            return (
              <motion.div
                key={d.id}
                initial={{ opacity: 0, y: 16 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: Math.min(i, 9) * 0.04 }}
                className="group rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card"
              >
                <div className="mb-3 flex items-start justify-between gap-2">
                  <span className="grid h-11 w-11 shrink-0 place-items-center rounded-xl bg-brand-100 dark:bg-brand-500/20 text-brand-600 dark:text-brand-400">
                    <BookOpen className="h-5 w-5" />
                  </span>
                  <div className="flex items-center gap-2">
                    <span
                      className={`inline-flex items-center gap-1 rounded-full px-2.5 py-0.5 text-xs font-medium ${scopeBadge(d.scope)}`}
                    >
                      <Icon className="h-3 w-3" /> {scopeLabel(d.scope)}
                      {d.roundNumber ? ` · V${d.roundNumber}` : ''}
                    </span>
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
                  </div>
                </div>
                <h3 className="font-semibold text-ink-900 dark:text-white">
                  {docTypeLabel(d.documentType)}
                </h3>
                <p className="mt-0.5 flex items-center gap-1 truncate text-xs text-ink-500 dark:text-ink-400">
                  <FileText className="h-3 w-3" /> {d.fileName}
                </p>
                <div className="mt-3 flex items-center justify-between border-t border-ink-100 pt-3 text-xs dark:border-white/10">
                  <span
                    className={`rounded-full px-2 py-0.5 ${d.status === 'ready' ? 'bg-emerald-100 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400' : 'bg-amber-100 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400'}`}
                  >
                    {d.status === 'ready' ? 'Đã nạp RAG' : d.status}
                  </span>
                  <span className="uppercase text-ink-400">{d.fileFormat}</span>
                </div>
              </motion.div>
            )
          })}
        </div>
      )}

      {showUpload && (
        <UploadModal
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

function UploadModal({
  onClose,
  onUploaded,
}: {
  onClose: () => void
  onUploaded: (doc: PlaybookItem) => void
}) {
  const fileRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [scope, setScope] = useState('org')
  const [documentType, setDocumentType] = useState('style_guide')
  const [scopeRefId, setScopeRefId] = useState('')
  const [roundNumber, setRoundNumber] = useState<number>(1)
  const [jobs, setJobs] = useState<JobPosting[]>([])
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState('')

  useEffect(() => {
    if (scope !== 'org' && jobs.length === 0) {
      jobService
        .getAdminJobPostings()
        .then(setJobs)
        .catch(() => {
          /* ignore */
        })
    }
  }, [scope, jobs.length])

  const submit = async () => {
    if (!file) {
      setError('Vui lòng chọn file.')
      return
    }
    if (scope !== 'org' && !scopeRefId) {
      setError('Vui lòng chọn tin tuyển dụng.')
      return
    }
    setSubmitting(true)
    setError('')
    try {
      const doc = await playbookService.uploadPlaybook({
        file,
        scope,
        documentType,
        scopeRefId: scope !== 'org' ? scopeRefId : undefined,
        roundNumber: scope === 'round' ? roundNumber : undefined,
      })
      onUploaded(doc)
    } catch (e: any) {
      setError(e?.response?.data?.message || 'Upload thất bại.')
    } finally {
      setSubmitting(false)
    }
  }

  const inputCls =
    'w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none focus:border-brand-400'

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
            <h3 className="text-lg font-semibold text-ink-900 dark:text-white">Upload playbook</h3>
            <p className="mt-1 text-xs text-ink-500 dark:text-ink-400">
              PDF / DOCX / TXT / MD — sẽ được chunk + embed vào RAG.
            </p>
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
          <input
            ref={fileRef}
            type="file"
            accept=".pdf,.docx,.txt,.md"
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
            <span className="truncate">{file ? file.name : 'Chọn file tài liệu...'}</span>
          </button>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-700 dark:text-ink-200">
                Loại tài liệu
              </label>
              <select
                value={documentType}
                onChange={(e) => setDocumentType(e.target.value)}
                className={inputCls}
              >
                {DOC_TYPES.map(([v, l]) => (
                  <option key={v} value={v}>
                    {l}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-700 dark:text-ink-200">
                Phạm vi
              </label>
              <select value={scope} onChange={(e) => setScope(e.target.value)} className={inputCls}>
                {SCOPES.map(([v, l]) => (
                  <option key={v} value={v}>
                    {l}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {scope !== 'org' && (
            <div className="grid grid-cols-2 gap-3">
              <div className={scope === 'round' ? '' : 'col-span-2'}>
                <label className="mb-1.5 block text-sm font-medium text-ink-700 dark:text-ink-200">
                  Tin tuyển dụng
                </label>
                <select
                  value={scopeRefId}
                  onChange={(e) => setScopeRefId(e.target.value)}
                  className={inputCls}
                >
                  <option value="">— Chọn tin —</option>
                  {jobs.map((j) => (
                    <option key={j.id} value={j.id}>
                      {j.title}
                    </option>
                  ))}
                </select>
              </div>
              {scope === 'round' && (
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-ink-700 dark:text-ink-200">
                    Vòng
                  </label>
                  <input
                    type="number"
                    min={1}
                    value={roundNumber}
                    onChange={(e) => setRoundNumber(Number(e.target.value))}
                    className={inputCls}
                  />
                </div>
              )}
            </div>
          )}
        </div>

        <div className="mt-5 flex justify-end gap-2">
          <button
            onClick={onClose}
            className="rounded-xl border border-ink-200 px-4 py-2.5 text-sm font-medium text-ink-700 hover:bg-ink-50 dark:border-white/10 dark:text-ink-200 dark:hover:bg-white/10"
          >
            Hủy
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
            )}{' '}
            Upload
          </button>
        </div>
      </motion.div>
    </div>
  )
}
