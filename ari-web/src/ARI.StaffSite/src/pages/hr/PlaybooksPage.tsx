import { useEffect, useMemo, useRef, useState } from 'react'
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
} from 'lucide-react'
import { PageHeader, ErrorAlert, EmptyState, Pagination, Select } from '@ari/shared/ui'
import { CardGridSkeleton } from './_skeletons'
import { playbookService } from '@/fservices/playbook/playbookService'
import type { PlaybookItem } from '@/fservices/playbook/playbookService'
import jobService from '@ari/shared/fservices/job'
import type { JobPosting } from '@ari/shared/types/job'

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
      setDocs(await playbookService.getPlaybooks())
    } catch (e: any) {
      setError(e?.response?.data?.message || t('loadingError'))
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
      setError(e?.response?.data?.message || t('deleteError'))
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
                    {d.status === 'ready' ? t('status.ready') : d.status}
                  </span>
                  <span className="uppercase text-ink-400">{d.fileFormat}</span>
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
  t,
  onClose,
  onUploaded,
}: {
  t: (key: string) => string
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
        .catch(() => {})
    }
  }, [scope, jobs.length])

  const submit = async () => {
    if (!file) {
      setError(t('selectFileError'))
      return
    }
    if (scope !== 'org' && !scopeRefId) {
      setError(t('selectJobError'))
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
      setError(e?.response?.data?.message || t('uploadError'))
    } finally {
      setSubmitting(false)
    }
  }

  const inputCls =
    'w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2.5 text-sm text-ink-900 dark:text-white outline-none focus:border-brand-400'

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
  ]

  const SCOPES: [string, string][] = [
    ['org', t('scopes.org')],
    ['job_posting', t('scopes.jobPosting')],
    ['round', t('scopes.round')],
  ]

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
            <span className="truncate">{file ? file.name : t('selectFile')}</span>
          </button>

          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-700 dark:text-ink-200">
                {t('documentType')}
              </label>
              <Select
                value={documentType}
                onChange={setDocumentType}
                className="w-full"
                buttonClassName="px-3 py-2.5 text-sm"
                options={DOC_TYPES.map(([v, l]) => ({ value: v, label: l }))}
              />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-700 dark:text-ink-200">
                {t('scope')}
              </label>
              <Select
                value={scope}
                onChange={setScope}
                className="w-full"
                buttonClassName="px-3 py-2.5 text-sm"
                options={SCOPES.map(([v, l]) => ({ value: v, label: l }))}
              />
            </div>
          </div>

          {scope !== 'org' && (
            <div className="grid grid-cols-2 gap-3">
              <div className={scope === 'round' ? '' : 'col-span-2'}>
                <label className="mb-1.5 block text-sm font-medium text-ink-700 dark:text-ink-200">
                  {t('jobPosting')}
                </label>
                <Select
                  value={scopeRefId}
                  onChange={setScopeRefId}
                  placeholder={`— ${t('selectJob')} —`}
                  className="w-full"
                  buttonClassName="px-3 py-2.5 text-sm"
                  options={jobs.map((j) => ({ value: j.id, label: j.title }))}
                />
              </div>
              {scope === 'round' && (
                <div>
                  <label className="mb-1.5 block text-sm font-medium text-ink-700 dark:text-ink-200">
                    {t('round')}
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
