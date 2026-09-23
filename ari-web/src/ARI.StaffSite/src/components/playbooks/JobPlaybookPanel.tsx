import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import {
  BookOpen,
  Plus,
  Trash2,
  UploadCloud,
  X,
  Loader2,
  FileText,
  Scale,
  Download,
  AlertCircle,
  CheckCircle2,
  Layers,
  Briefcase,
} from 'lucide-react'
import { Select } from '@ari/shared/ui'
import { isOnlineTestRound } from '@ari/shared/utils/roundTypes'
import { playbookService, isRubricDocType } from '@/fservices/playbook/playbookService'
import type { PlaybookItem } from '@/fservices/playbook/playbookService'

const JOB_PLAYBOOKS_NS = 'modules/staff/jobPlaybooks'

/**
 * Loại tài liệu — cùng danh sách với server (`PlaybookAccess.DocumentTypes`), TRỪ hai bộ tiêu chí chấm điểm: bộ
 * chấm CV (ADR-070) và bộ chấm phỏng vấn (ADR-073) của tin đều có panel riêng — một bản sống mỗi (tin, vòng), lưu
 * xong là hệ thống chấm lại. Thứ tự theo mức hay dùng ở cấp TIN: ngân hàng câu hỏi và câu bắt buộc lên đầu; văn
 * phong/văn hoá thường là playbook công ty.
 */
const DOC_TYPES = [
  'question_bank',
  'must_ask',
  'expected_answer',
  'technical_scenario',
  'competency_framework',
  'red_flag',
  'round_playbook',
  'style_guide',
  'culture_guide',
  'compliance',
] as const

type ApiError = { response?: { data?: { message?: string } } }
const apiMessage = (e: unknown) => (e as ApiError)?.response?.data?.message

interface JobPlaybookPanelProps {
  jobPostingId: string
  rounds: { roundNumber: number; roundType?: string | null }[]
}

/**
 * Playbook THEO TIN, ngay trong màn tin (ADR-069).
 *
 * Nội dung playbook của một vị trí — hỏi gì, câu nào bắt buộc, đáp án tốt trông ra sao, chấm theo tiêu chí
 * nào — là quyết định CHUYÊN MÔN, nên người thêm/xoá là Hiring Manager chính của tin (HR Leader vẫn làm
 * được). Trước đây việc này nằm ở màn Playbook trong thanh bên của HR, tách khỏi tin: HM không vào được,
 * còn người vào được thì phải chọn tin từ một danh sách thả xuống.
 *
 * Quyền thêm/xoá do SERVER quyết (`canManage`), giao diện chỉ hiện hay ẩn nút. Mọi thành viên đội đọc được
 * — Recruiter cần biết AI sẽ hỏi theo tài liệu nào.
 */
export default function JobPlaybookPanel({ jobPostingId, rounds }: JobPlaybookPanelProps) {
  const { t } = useTranslation(JOB_PLAYBOOKS_NS)
  const queryClient = useQueryClient()
  const [showUpload, setShowUpload] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState<PlaybookItem | null>(null)
  const [error, setError] = useState<string | null>(null)

  const key = ['job-playbooks', jobPostingId]
  const { data, isLoading } = useQuery({
    queryKey: key,
    queryFn: () => playbookService.getJobPlaybooks(jobPostingId),
    enabled: !!jobPostingId,
  })
  const items = data?.items ?? []
  const canManage = data?.canManage ?? false

  const remove = useMutation({
    mutationFn: (id: string) => playbookService.deleteJobPlaybook(jobPostingId, id),
    onSuccess: () => {
      setConfirmDelete(null)
      setError(null)
      queryClient.invalidateQueries({ queryKey: key })
    },
    onError: (e) => setError(apiMessage(e) ?? t('deleteError')),
  })

  // Vòng hội thoại mới gắn playbook được — vòng trắc nghiệm không có AI phỏng vấn (server cũng chặn).
  const conversationalRounds = rounds.filter((r) => !isOnlineTestRound(r.roundType))
  const whole = items.filter((i) => i.scope === 'job_posting')
  const byRound = conversationalRounds
    .map((r) => ({ round: r.roundNumber, docs: items.filter((i) => i.scope === 'round' && i.roundNumber === r.roundNumber) }))
    .filter((g) => g.docs.length > 0)
  // Playbook của một vòng mà tin đã bỏ đi (sửa cấu hình vòng sau khi nạp) — vẫn phải thấy để xoá.
  const orphanRound = items.filter(
    (i) => i.scope === 'round' && !conversationalRounds.some((r) => r.roundNumber === i.roundNumber)
  )

  return (
    <section className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card dark:border-white/10 dark:bg-white/5">
      <div className="mb-1 flex items-start justify-between gap-2">
        <h2 className="flex items-center gap-2 text-sm font-semibold text-ink-900 dark:text-white">
          <BookOpen className="h-4 w-4 text-brand-600 dark:text-brand-400" /> {t('title')}
        </h2>
        {canManage && (
          <button
            type="button"
            onClick={() => {
              setError(null)
              setShowUpload(true)
            }}
            className="inline-flex shrink-0 items-center gap-1 rounded-lg bg-brand-600 px-2.5 py-1.5 text-xs font-semibold text-white hover:bg-brand-700"
          >
            <Plus className="h-3.5 w-3.5" /> {t('add')}
          </button>
        )}
      </div>
      <p className="mb-3 text-xs text-ink-500 dark:text-ink-400">{t('description')}</p>

      {error && (
        <div className="mb-3 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 px-3 py-2 text-xs text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-400">
          <AlertCircle className="mt-0.5 h-3.5 w-3.5 shrink-0" /> {error}
        </div>
      )}

      {isLoading ? (
        <p className="flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400">
          <Loader2 className="h-4 w-4 animate-spin" /> {t('loading')}
        </p>
      ) : items.length === 0 ? (
        <p className="rounded-xl border border-dashed border-ink-200 px-3 py-4 text-center text-xs text-ink-500 dark:border-white/10 dark:text-ink-400">
          {canManage ? t('emptyManage') : t('empty')}
        </p>
      ) : (
        <div className="space-y-3">
          {whole.length > 0 && (
            <Group icon={<Briefcase className="h-3.5 w-3.5" />} label={t('wholeJob')}>
              {whole.map((d) => (
                <DocRow key={d.id} doc={d} canManage={canManage} onDelete={() => setConfirmDelete(d)} />
              ))}
            </Group>
          )}
          {byRound.map((g) => (
            <Group key={g.round} icon={<Layers className="h-3.5 w-3.5" />} label={t('roundLabel', { number: g.round })}>
              {g.docs.map((d) => (
                <DocRow key={d.id} doc={d} canManage={canManage} onDelete={() => setConfirmDelete(d)} />
              ))}
            </Group>
          ))}
          {orphanRound.length > 0 && (
            <Group icon={<Layers className="h-3.5 w-3.5" />} label={t('orphanRound')}>
              {orphanRound.map((d) => (
                <DocRow key={d.id} doc={d} canManage={canManage} onDelete={() => setConfirmDelete(d)} />
              ))}
            </Group>
          )}
        </div>
      )}

      {!canManage && !isLoading && <p className="mt-3 text-xs text-ink-400">{t('readOnlyHint')}</p>}

      {showUpload && (
        <UploadModal
          jobPostingId={jobPostingId}
          rounds={conversationalRounds}
          onClose={() => setShowUpload(false)}
          onUploaded={() => {
            setShowUpload(false)
            queryClient.invalidateQueries({ queryKey: key })
          }}
        />
      )}

      {confirmDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink-950/60 p-4 backdrop-blur-sm">
          <div className="w-full max-w-md rounded-2xl border border-ink-200 bg-white p-5 shadow-2xl dark:border-white/10 dark:bg-ink-900">
            <h3 className="text-base font-bold text-ink-900 dark:text-white">{t('deleteTitle')}</h3>
            <p className="mt-2 text-sm text-ink-600 dark:text-ink-300">
              {t('deleteBody', { name: confirmDelete.fileName })}
            </p>
            <div className="mt-4 flex justify-end gap-2">
              <button
                type="button"
                onClick={() => setConfirmDelete(null)}
                className="rounded-xl px-4 py-2 text-sm font-semibold text-ink-600 hover:bg-ink-100 dark:text-ink-300 dark:hover:bg-white/10"
              >
                {t('cancel')}
              </button>
              <button
                type="button"
                disabled={remove.isPending}
                onClick={() => remove.mutate(confirmDelete.id)}
                className="inline-flex items-center gap-1.5 rounded-xl bg-red-600 px-4 py-2 text-sm font-semibold text-white hover:bg-red-700 disabled:opacity-50"
              >
                {remove.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Trash2 className="h-4 w-4" />}
                {t('confirmDelete')}
              </button>
            </div>
          </div>
        </div>
      )}
    </section>
  )
}

function Group({ icon, label, children }: { icon: React.ReactNode; label: string; children: React.ReactNode }) {
  return (
    <div>
      <p className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wide text-ink-400">
        {icon} {label}
      </p>
      <ul className="space-y-1.5">{children}</ul>
    </div>
  )
}

function DocRow({ doc, canManage, onDelete }: { doc: PlaybookItem; canManage: boolean; onDelete: () => void }) {
  const { t } = useTranslation(JOB_PLAYBOOKS_NS)
  const rubric = isRubricDocType(doc.documentType)
  return (
    <li className="flex items-center gap-2 rounded-xl border border-ink-100 px-3 py-2 dark:border-white/10">
      <span
        className={`grid h-8 w-8 shrink-0 place-items-center rounded-lg ${
          rubric
            ? 'bg-ai-100 text-ai-600 dark:bg-ai-500/20 dark:text-ai-400'
            : 'bg-brand-100 text-brand-600 dark:bg-brand-500/20 dark:text-brand-400'
        }`}
      >
        {rubric ? <Scale className="h-4 w-4" /> : <FileText className="h-4 w-4" />}
      </span>
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium text-ink-900 dark:text-white">
          {t(`docTypes.${doc.documentType}`, { defaultValue: doc.documentType })}
          {doc.criteriaCount != null && (
            <span className="ml-1.5 rounded-full bg-ai-100 px-1.5 py-0.5 text-[10px] font-semibold text-ai-700 dark:bg-ai-500/20 dark:text-ai-300">
              {t('criteriaCount', { count: doc.criteriaCount })}
            </span>
          )}
        </p>
        <p className="truncate text-xs text-ink-500 dark:text-ink-400">
          {doc.fileName}
          {doc.uploadedBy ? ` · ${doc.uploadedBy}` : ''}
        </p>
      </div>
      {canManage && (
        <button
          type="button"
          onClick={onDelete}
          aria-label={t('delete')}
          className="grid h-8 w-8 shrink-0 place-items-center rounded-lg text-ink-400 hover:bg-red-50 hover:text-red-500 dark:hover:bg-red-500/10"
        >
          <Trash2 className="h-4 w-4" />
        </button>
      )}
    </li>
  )
}

function UploadModal({
  jobPostingId,
  rounds,
  onClose,
  onUploaded,
}: {
  jobPostingId: string
  rounds: { roundNumber: number; roundType?: string | null }[]
  onClose: () => void
  onUploaded: () => void
}) {
  const { t } = useTranslation(JOB_PLAYBOOKS_NS)
  const fileRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [documentType, setDocumentType] = useState<string>('question_bank')
  const [target, setTarget] = useState<string>('job') // 'job' | số vòng
  const [error, setError] = useState<string | null>(null)
  const [downloading, setDownloading] = useState(false)

  const isRubric = isRubricDocType(documentType)

  // Đổi loại tài liệu thì bỏ file đang chọn nếu đuôi không còn hợp lệ — giữ lại là bấm Tải lên rồi mới ăn
  // lỗi từ server mà không hiểu vì sao.
  const changeDocumentType = (next: string) => {
    setDocumentType(next)
    setError(null)
    const ext = file?.name.slice(file.name.lastIndexOf('.')).toLowerCase()
    if (ext && (isRubricDocType(next) ? ext !== '.xlsx' : ext === '.xlsx')) {
      setFile(null)
      if (fileRef.current) fileRef.current.value = ''
    }
  }

  const upload = useMutation({
    mutationFn: () =>
      playbookService.uploadJobPlaybook(jobPostingId, {
        file: file!,
        documentType,
        scope: target === 'job' ? 'job_posting' : 'round',
        roundNumber: target === 'job' ? undefined : Number(target),
      }),
    onSuccess: onUploaded,
    onError: (e) => setError(apiMessage(e) ?? t('uploadError')),
  })

  const downloadTemplate = async () => {
    setDownloading(true)
    setError(null)
    try {
      await playbookService.downloadRubricTemplate(documentType)
    } catch {
      setError(t('templateError'))
    } finally {
      setDownloading(false)
    }
  }

  const targets = [
    { value: 'job', label: t('targetWholeJob') },
    ...rounds.map((r) => ({ value: String(r.roundNumber), label: t('roundLabel', { number: r.roundNumber }) })),
  ]

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-ink-950/60 p-4 backdrop-blur-sm">
      <div className="w-full max-w-lg rounded-2xl border border-ink-200 bg-white p-6 shadow-2xl dark:border-white/10 dark:bg-ink-900">
        <div className="mb-4 flex items-start justify-between gap-3">
          <div>
            <h3 className="text-base font-bold text-ink-900 dark:text-white">{t('uploadTitle')}</h3>
            <p className="mt-1 text-xs text-ink-500 dark:text-ink-400">{t('uploadHint')}</p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label={t('cancel')}
            className="grid h-8 w-8 place-items-center rounded-lg text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {error && (
          <div className="mb-4 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-500/20 dark:bg-red-500/10 dark:text-red-400">
            <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {error}
          </div>
        )}

        <div className="space-y-4">
          <div className="grid gap-3 sm:grid-cols-2">
            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-700 dark:text-ink-200">
                {t('documentType')}
              </label>
              <Select
                value={documentType}
                onChange={changeDocumentType}
                className="w-full"
                buttonClassName="px-3 py-2.5 text-sm"
                options={DOC_TYPES.map((v) => ({ value: v, label: t(`docTypes.${v}`) }))}
              />
            </div>
            <div>
              <label className="mb-1.5 block text-sm font-medium text-ink-700 dark:text-ink-200">
                {t('target')}
              </label>
              <Select
                value={target}
                onChange={setTarget}
                className="w-full"
                buttonClassName="px-3 py-2.5 text-sm"
                options={targets}
              />
            </div>
          </div>
          <p className="text-xs text-ink-500 dark:text-ink-400">{t(`docTypeHints.${documentType}`)}</p>

          {/* Bộ tiêu chí là bảng số liệu: không có file mẫu thì không đoán nổi bố cục (mã / tên / trọng số /
              chuẩn chấm) và luật tổng trọng số = 100 (ADR-060). */}
          {isRubric && (
            <div className="rounded-xl border border-ai-200 bg-ai-50/60 p-3 dark:border-ai-500/20 dark:bg-ai-500/10">
              <p className="text-xs text-ai-800 dark:text-ai-300">{t('rubricHint')}</p>
              <button
                type="button"
                onClick={downloadTemplate}
                disabled={downloading}
                className="mt-2 inline-flex items-center gap-1.5 rounded-lg border border-ai-300 bg-white px-3 py-1.5 text-xs font-semibold text-ai-700 hover:bg-ai-50 disabled:opacity-50 dark:border-ai-500/30 dark:bg-white/5 dark:text-ai-300"
              >
                {downloading ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Download className="h-3.5 w-3.5" />}
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
            className="flex w-full items-center gap-3 rounded-xl border border-dashed border-ink-300 bg-ink-50 px-4 py-3 text-sm text-ink-600 hover:border-brand-400 dark:border-white/20 dark:bg-white/5 dark:text-ink-300"
          >
            {file ? <CheckCircle2 className="h-5 w-5 text-emerald-500" /> : <UploadCloud className="h-5 w-5 text-ink-400" />}
            <span className="truncate">{file ? file.name : isRubric ? t('selectRubricFile') : t('selectFile')}</span>
          </button>
        </div>

        <div className="mt-5 flex justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="rounded-xl border border-ink-200 px-4 py-2.5 text-sm font-medium text-ink-700 hover:bg-ink-50 dark:border-white/10 dark:text-ink-200 dark:hover:bg-white/10"
          >
            {t('cancel')}
          </button>
          <button
            type="button"
            onClick={() => upload.mutate()}
            disabled={upload.isPending || !file}
            className="inline-flex items-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
          >
            {upload.isPending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />}
            {t('upload')}
          </button>
        </div>
      </div>
    </div>
  )
}
