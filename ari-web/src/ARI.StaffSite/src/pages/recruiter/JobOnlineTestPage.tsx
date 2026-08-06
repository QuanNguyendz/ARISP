import { useCallback, useEffect, useRef, useState } from 'react'
import { useParams, useLocation, Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { motion } from 'framer-motion'
import {
  ArrowLeft,
  ScrollText,
  Plus,
  Trash2,
  Loader2,
  Save,
  Pencil,
  X,
  CheckCircle2,
  AlertCircle,
  BarChart3,
  Upload,
  Download,
  FileSpreadsheet,
} from 'lucide-react'
import { ErrorAlert } from '@ari/shared/ui'
import { onlineTestService } from '@ari/shared/fservices/onlineTest'
import type {
  OnlineTestBank,
  OnlineTestQuestion,
  OnlineTestQuestionType,
  OnlineTestImportResult,
} from '@ari/shared/types/onlineTest'

function errMsg(e: unknown, fallback: string): string {
  const x = e as { response?: { data?: { message?: string } } }
  return x?.response?.data?.message || fallback
}

const DEFAULT_OPTIONS = () => ['', '', '', '']

export default function JobOnlineTestPage() {
  const { t } = useTranslation('modules/recruiter/onlineTest')
  const { id: jobId } = useParams<{ id: string }>()
  const location = useLocation()
  const isHr = location.pathname.startsWith('/hr')
  const backTo = isHr ? `/hr/jobs/${jobId}` : `/recruiter/my-jobs/${jobId}`
  const resultsTo = isHr
    ? `/hr/jobs/${jobId}/online-test/results`
    : `/recruiter/my-jobs/${jobId}/online-test/results`

  const [bank, setBank] = useState<OnlineTestBank | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')

  // Cấu hình bài thi
  const [passScore, setPassScore] = useState(70)
  const [questionsPerTest, setQuestionsPerTest] = useState(20)
  const [durationMinutes, setDurationMinutes] = useState(30)
  const [savingSettings, setSavingSettings] = useState(false)

  // Form câu hỏi (add / edit)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [questionText, setQuestionText] = useState('')
  const [questionType, setQuestionType] = useState<OnlineTestQuestionType>('single')
  const [options, setOptions] = useState<string[]>(DEFAULT_OPTIONS())
  const [correctOptions, setCorrectOptions] = useState<number[]>([0])
  const [formError, setFormError] = useState('')
  const [busy, setBusy] = useState(false)

  // Import file Excel
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [importing, setImporting] = useState(false)
  const [downloadingTemplate, setDownloadingTemplate] = useState(false)
  const [importResult, setImportResult] = useState<OnlineTestImportResult | null>(null)

  const loadBank = useCallback(async () => {
    if (!jobId) return
    try {
      setLoading(true)
      const data = await onlineTestService.getBank(jobId)
      setBank(data)
      setPassScore(data.passScore)
      setQuestionsPerTest(data.questionsPerTest)
      setDurationMinutes(data.durationMinutes)
    } catch (e) {
      setError(errMsg(e, t('bank.errors.loadBank')))
    } finally {
      setLoading(false)
    }
  }, [jobId, t])

  useEffect(() => {
    void loadBank()
  }, [loadBank])

  const resetForm = () => {
    setEditingId(null)
    setQuestionText('')
    setQuestionType('single')
    setOptions(DEFAULT_OPTIONS())
    setCorrectOptions([0])
    setFormError('')
  }

  const startEdit = (q: OnlineTestQuestion) => {
    setEditingId(q.id)
    setQuestionText(q.questionText)
    setQuestionType(q.questionType)
    setOptions(q.options.length >= 2 ? [...q.options] : DEFAULT_OPTIONS())
    setCorrectOptions(q.correctOptions.length ? [...q.correctOptions] : [0])
    setFormError('')
    window.scrollTo({ top: 0, behavior: 'smooth' })
  }

  const setOptionAt = (i: number, val: string) =>
    setOptions((prev) => prev.map((o, idx) => (idx === i ? val : o)))

  const addOption = () => setOptions((prev) => (prev.length >= 6 ? prev : [...prev, '']))

  const removeOptionAt = (i: number) => {
    setOptions((prev) => {
      if (prev.length <= 2) return prev
      return prev.filter((_, idx) => idx !== i)
    })
    // Bỏ index vừa xoá khỏi đáp án đúng và dịch các index lớn hơn xuống 1.
    setCorrectOptions((prev) =>
      prev.filter((c) => c !== i).map((c) => (c > i ? c - 1 : c))
    )
  }

  const changeType = (type: OnlineTestQuestionType) => {
    setQuestionType(type)
    if (type === 'single') setCorrectOptions((prev) => (prev.length ? [prev[0]] : [0]))
  }

  const toggleCorrect = (i: number) => {
    if (questionType === 'single') {
      setCorrectOptions([i])
    } else {
      setCorrectOptions((prev) =>
        prev.includes(i) ? prev.filter((c) => c !== i) : [...prev, i].sort((a, b) => a - b)
      )
    }
  }

  const saveSettings = async () => {
    if (!jobId) return
    if (
      passScore < 0 ||
      passScore > 100 ||
      questionsPerTest < 1 ||
      questionsPerTest > 200 ||
      durationMinutes < 1 ||
      durationMinutes > 300
    ) {
      setError(t('bank.errors.settingsRange'))
      return
    }
    setSavingSettings(true)
    setError('')
    try {
      const updated = await onlineTestService.updateSettings(jobId, {
        passScore,
        questionsPerTest,
        durationMinutes,
      })
      setBank(updated)
    } catch (e) {
      setError(errMsg(e, t('bank.errors.saveSettings')))
    } finally {
      setSavingSettings(false)
    }
  }

  const submitQuestion = async () => {
    if (!jobId) return
    const cleaned = options.map((o) => o.trim())
    const validCount = cleaned.filter((o) => o).length
    if (!questionText.trim()) {
      setFormError(t('bank.validation.emptyQuestion'))
      return
    }
    if (validCount < 2) {
      setFormError(t('bank.validation.minOptions'))
      return
    }
    // Đáp án đúng phải trỏ vào phương án không rỗng.
    const correct = correctOptions.filter((c) => cleaned[c])
    if (correct.length === 0) {
      setFormError(t('bank.validation.noCorrect'))
      return
    }
    if (questionType === 'single' && correct.length !== 1) {
      setFormError(t('bank.validation.singleOne'))
      return
    }
    setBusy(true)
    setFormError('')
    try {
      const payload = { questionText: questionText.trim(), options: cleaned, questionType, correctOptions: correct }
      if (editingId) {
        await onlineTestService.updateQuestion(editingId, payload)
      } else {
        await onlineTestService.createQuestion(jobId, payload)
      }
      resetForm()
      await loadBank()
    } catch (e) {
      setFormError(errMsg(e, t('bank.errors.saveQuestion')))
    } finally {
      setBusy(false)
    }
  }

  const downloadTemplate = async () => {
    if (!jobId) return
    setDownloadingTemplate(true)
    setError('')
    try {
      const { blob, fileName } = await onlineTestService.downloadTemplate(jobId)
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = fileName
      a.click()
      URL.revokeObjectURL(url)
    } catch (e) {
      setError(errMsg(e, t('bank.import.templateError')))
    } finally {
      setDownloadingTemplate(false)
    }
  }

  const onPickFile = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0]
    e.target.value = '' // cho phép chọn lại cùng 1 file
    if (!file || !jobId) return
    setImporting(true)
    setImportResult(null)
    setError('')
    try {
      const res = await onlineTestService.importQuestions(jobId, file)
      setImportResult(res)
      await loadBank()
    } catch (err) {
      setError(errMsg(err, t('bank.import.importError')))
    } finally {
      setImporting(false)
    }
  }

  const removeQuestion = async (id: string) => {
    try {
      await onlineTestService.deleteQuestion(id)
      setBank((prev) => (prev ? { ...prev, questions: prev.questions.filter((q) => q.id !== id) } : prev))
      if (editingId === id) resetForm()
    } catch (e) {
      setError(errMsg(e, t('bank.errors.deleteQuestion')))
    }
  }

  const questions = bank?.questions ?? []
  const numInput =
    'w-full rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm text-ink-900 dark:text-white'

  return (
    <div className="p-6 lg:p-8">
      <Link
        to={backTo}
        className="mb-4 inline-flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400 hover:text-ink-800 dark:hover:text-white"
      >
        <ArrowLeft className="h-4 w-4" /> {t('bank.back')}
      </Link>

      <motion.div
        initial={{ opacity: 0, y: 16 }}
        animate={{ opacity: 1, y: 0 }}
        className="mb-6 flex flex-wrap items-start justify-between gap-3"
      >
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-bold text-ink-900 dark:text-white">
            <ScrollText className="h-6 w-6 text-brand-600 dark:text-brand-400" /> {t('bank.title')}
          </h1>
          <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">
            {bank?.jobTitle ? t('bank.jobLabel', { title: bank.jobTitle }) : t('bank.subtitle')}
          </p>
        </div>
        <Link
          to={resultsTo}
          className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
        >
          <BarChart3 className="h-4 w-4" /> {t('bank.results')}
        </Link>
      </motion.div>

      {error && <ErrorAlert message={error} onDismiss={() => setError('')} />}

      {loading ? (
        <div className="flex items-center justify-center py-16">
          <Loader2 className="h-6 w-6 animate-spin text-brand-600 dark:text-brand-400" />
        </div>
      ) : (
        <div className="grid gap-6 lg:grid-cols-3">
          {/* Cột trái: cấu hình + form câu hỏi */}
          <div className="space-y-6">
            {/* Cấu hình bài thi */}
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
              <h2 className="mb-1 text-base font-semibold text-ink-900 dark:text-white">
                {t('bank.settings.title')}
              </h2>
              <p className="mb-3 text-xs text-ink-500 dark:text-ink-400">{t('bank.settings.hint')}</p>
              <div className="grid grid-cols-3 gap-2">
                <div>
                  <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                    {t('bank.settings.passScore')}
                  </label>
                  <input
                    type="number"
                    min={0}
                    max={100}
                    value={passScore}
                    onChange={(e) => setPassScore(Math.min(100, Math.max(0, Number(e.target.value) || 0)))}
                    className={numInput}
                  />
                </div>
                <div>
                  <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                    {t('bank.settings.questionsPerTest')}
                  </label>
                  <input
                    type="number"
                    min={1}
                    max={200}
                    value={questionsPerTest}
                    onChange={(e) =>
                      setQuestionsPerTest(Math.min(200, Math.max(1, Number(e.target.value) || 1)))
                    }
                    className={numInput}
                  />
                </div>
                <div>
                  <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                    {t('bank.settings.duration')}
                  </label>
                  <input
                    type="number"
                    min={1}
                    max={300}
                    value={durationMinutes}
                    onChange={(e) =>
                      setDurationMinutes(Math.min(300, Math.max(1, Number(e.target.value) || 1)))
                    }
                    className={numInput}
                  />
                </div>
              </div>
              <button
                type="button"
                onClick={saveSettings}
                disabled={
                  savingSettings ||
                  (passScore === bank?.passScore &&
                    questionsPerTest === bank?.questionsPerTest &&
                    durationMinutes === bank?.durationMinutes)
                }
                className="mt-3 inline-flex items-center gap-2 rounded-xl bg-brand-600 px-3.5 py-2 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
              >
                {savingSettings ? <Loader2 className="h-4 w-4 animate-spin" /> : <Save className="h-4 w-4" />}{' '}
                {t('bank.settings.save')}
              </button>
            </div>

            {/* Import file Excel */}
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
              <h2 className="mb-1 flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
                <FileSpreadsheet className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
                {t('bank.import.title')}
              </h2>
              <p className="mb-3 text-xs text-ink-500 dark:text-ink-400">{t('bank.import.hint')}</p>
              <input
                ref={fileInputRef}
                type="file"
                accept=".xlsx"
                onChange={onPickFile}
                className="hidden"
              />
              <div className="flex flex-wrap gap-2">
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={importing}
                  className="inline-flex items-center gap-2 rounded-xl bg-emerald-600 px-3.5 py-2 text-sm font-semibold text-white hover:bg-emerald-700 disabled:opacity-50"
                >
                  {importing ? <Loader2 className="h-4 w-4 animate-spin" /> : <Upload className="h-4 w-4" />}{' '}
                  {t('bank.import.upload')}
                </button>
                <button
                  type="button"
                  onClick={downloadTemplate}
                  disabled={downloadingTemplate}
                  className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3.5 py-2 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 disabled:opacity-50"
                >
                  {downloadingTemplate ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Download className="h-4 w-4" />
                  )}{' '}
                  {t('bank.import.template')}
                </button>
              </div>

              {importResult && (
                <div className="mt-3 rounded-xl border border-ink-100 dark:border-white/10 bg-ink-50 dark:bg-white/5 p-3 text-sm">
                  <p className="flex items-center gap-2 font-medium text-ink-800 dark:text-ink-100">
                    <CheckCircle2 className="h-4 w-4 text-emerald-600" />
                    {t('bank.import.resultSummary', {
                      imported: importResult.imported,
                      failed: importResult.failed,
                    })}
                  </p>
                  {importResult.errors.length > 0 && (
                    <ul className="mt-2 max-h-40 space-y-1 overflow-auto text-xs text-red-600 dark:text-red-400">
                      {importResult.errors.map((err, i) => (
                        <li key={i}>{t('bank.import.rowError', { row: err.row, message: err.message })}</li>
                      ))}
                    </ul>
                  )}
                </div>
              )}
            </div>

            {/* Form câu hỏi */}
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
              <h2 className="mb-4 flex items-center gap-2 text-base font-semibold text-ink-900 dark:text-white">
                {editingId ? (
                  <>
                    <Pencil className="h-4 w-4 text-brand-600 dark:text-brand-400" /> {t('bank.form.editTitle')}
                  </>
                ) : (
                  <>
                    <Plus className="h-4 w-4 text-brand-600 dark:text-brand-400" /> {t('bank.form.addTitle')}
                  </>
                )}
              </h2>

              {formError && (
                <div className="mb-3 flex items-start gap-2 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700 dark:border-red-500/30 dark:bg-red-500/10 dark:text-red-300">
                  <AlertCircle className="mt-0.5 h-4 w-4 shrink-0" /> {formError}
                </div>
              )}

              <div className="space-y-3">
                <div>
                  <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                    {t('bank.form.questionLabel')}
                  </label>
                  <textarea
                    value={questionText}
                    onChange={(e) => setQuestionText(e.target.value)}
                    rows={3}
                    className={numInput}
                    placeholder={t('bank.form.questionPlaceholder')}
                  />
                </div>

                <div>
                  <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                    {t('bank.form.typeLabel')}
                  </label>
                  <div className="flex gap-2">
                    {(['single', 'multiple'] as const).map((tp) => (
                      <button
                        key={tp}
                        type="button"
                        onClick={() => changeType(tp)}
                        className={`flex-1 rounded-xl px-3 py-2 text-xs font-medium transition-colors ${
                          questionType === tp
                            ? 'bg-brand-600 text-white'
                            : 'border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10'
                        }`}
                      >
                        {tp === 'single' ? t('bank.form.typeSingle') : t('bank.form.typeMultiple')}
                      </button>
                    ))}
                  </div>
                </div>

                <div>
                  <label className="mb-1 block text-xs text-ink-500 dark:text-ink-400">
                    {questionType === 'single' ? t('bank.form.optionsSingle') : t('bank.form.optionsMultiple')}
                  </label>
                  <div className="space-y-2">
                    {options.map((opt, i) => (
                      <div key={i} className="flex items-center gap-2">
                        <input
                          type={questionType === 'single' ? 'radio' : 'checkbox'}
                          name="correctOption"
                          checked={correctOptions.includes(i)}
                          onChange={() => toggleCorrect(i)}
                          className="h-4 w-4 accent-brand-600"
                          title={t('bank.form.correctTitle')}
                        />
                        <input
                          type="text"
                          value={opt}
                          onChange={(e) => setOptionAt(i, e.target.value)}
                          className={`flex-1 ${numInput}`}
                          placeholder={t('bank.form.optionPlaceholder', { n: i + 1 })}
                        />
                        <button
                          type="button"
                          onClick={() => removeOptionAt(i)}
                          disabled={options.length <= 2}
                          className="grid h-8 w-8 shrink-0 place-items-center rounded-lg text-ink-400 hover:bg-red-50 hover:text-red-500 dark:hover:bg-red-500/10 disabled:opacity-30"
                          title={t('bank.form.removeOption')}
                        >
                          <X className="h-4 w-4" />
                        </button>
                      </div>
                    ))}
                  </div>
                  {options.length < 6 && (
                    <button
                      type="button"
                      onClick={addOption}
                      className="mt-2 inline-flex items-center gap-1 text-xs font-medium text-brand-600 dark:text-brand-400 hover:underline"
                    >
                      <Plus className="h-3.5 w-3.5" /> {t('bank.form.addOption')}
                    </button>
                  )}
                </div>

                <div className="flex items-center gap-2 pt-1">
                  <button
                    type="button"
                    onClick={submitQuestion}
                    disabled={busy}
                    className="inline-flex flex-1 items-center justify-center gap-2 rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-semibold text-white hover:bg-brand-700 disabled:opacity-50"
                  >
                    {busy ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : editingId ? (
                      <Save className="h-4 w-4" />
                    ) : (
                      <Plus className="h-4 w-4" />
                    )}{' '}
                    {editingId ? t('bank.form.submitEdit') : t('bank.form.submitAdd')}
                  </button>
                  {editingId && (
                    <button
                      type="button"
                      onClick={resetForm}
                      className="rounded-xl border border-ink-200 dark:border-white/10 px-4 py-2.5 text-sm font-medium text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
                    >
                      {t('bank.form.cancel')}
                    </button>
                  )}
                </div>
              </div>
            </div>
          </div>

          {/* Cột phải: danh sách câu hỏi */}
          <div className="lg:col-span-2 rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card">
            <div className="border-b border-ink-100 dark:border-white/10 px-5 py-4">
              <h2 className="text-base font-semibold text-ink-900 dark:text-white">
                {t('bank.list.title', { count: questions.length })}
              </h2>
            </div>
            {questions.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-16 text-center">
                <AlertCircle className="h-8 w-8 text-ink-300" />
                <p className="text-sm text-ink-500 dark:text-ink-400">{t('bank.list.empty')}</p>
              </div>
            ) : (
              <ol className="divide-y divide-ink-100 dark:divide-white/10">
                {questions.map((q, idx) => (
                  <li key={q.id} className="px-5 py-4">
                    <div className="flex items-start justify-between gap-3">
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        <span className="mr-1.5 text-ink-400">{idx + 1}.</span>
                        {q.questionText}
                        <span className="ml-2 rounded bg-ink-100 dark:bg-white/10 px-1.5 py-0.5 text-[10px] font-semibold text-ink-500 dark:text-ink-300">
                          {q.questionType === 'multiple' ? t('bank.list.typeMultiple') : t('bank.list.typeSingle')}
                        </span>
                      </p>
                      <div className="flex shrink-0 items-center gap-1">
                        <button
                          type="button"
                          onClick={() => startEdit(q)}
                          className="grid h-7 w-7 place-items-center rounded-lg text-ink-400 hover:bg-ink-50 hover:text-brand-600 dark:hover:bg-white/10"
                          title={t('bank.list.edit')}
                        >
                          <Pencil className="h-3.5 w-3.5" />
                        </button>
                        <button
                          type="button"
                          onClick={() => removeQuestion(q.id)}
                          className="grid h-7 w-7 place-items-center rounded-lg text-ink-400 hover:bg-red-50 hover:text-red-500 dark:hover:bg-red-500/10"
                          title={t('bank.list.delete')}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </button>
                      </div>
                    </div>
                    <ul className="mt-2 space-y-1">
                      {q.options.map((opt, oi) => {
                        const correct = q.correctOptions.includes(oi)
                        return (
                          <li
                            key={oi}
                            className={`flex items-center gap-2 text-sm ${
                              correct
                                ? 'font-medium text-emerald-700 dark:text-emerald-400'
                                : 'text-ink-600 dark:text-ink-300'
                            }`}
                          >
                            {correct ? (
                              <CheckCircle2 className="h-4 w-4 shrink-0" />
                            ) : (
                              <span className="grid h-4 w-4 shrink-0 place-items-center text-[10px] text-ink-400">
                                {String.fromCharCode(65 + oi)}
                              </span>
                            )}
                            {opt}
                          </li>
                        )
                      })}
                    </ul>
                  </li>
                ))}
              </ol>
            )}
          </div>
        </div>
      )}
    </div>
  )
}
