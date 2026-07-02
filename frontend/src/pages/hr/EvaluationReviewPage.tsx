import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { motion, AnimatePresence } from 'framer-motion'
import {
  Code2,
  Layers,
  Calendar,
  Clock,
  Sparkles,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Play,
  FileText,
  Languages,
  ArrowLeft,
  Bell,
} from 'lucide-react'
import { evaluationService } from '@/services/evaluation/evaluationService'
import type { EvaluationReport } from '@/types/evaluation'
import { EvaluationListSkeleton } from './_skeletons'
import { useQuery } from '@tanstack/react-query'

function formatVerdictLabel(verdict?: string) {
  if (verdict === 'pass') return 'Pass'
  if (verdict === 'not_pass') return 'Not Pass'
  return '—'
}

function formatDate(dateString?: string) {
  if (!dateString) return '—'
  try {
    return new Date(dateString).toLocaleDateString('vi-VN')
  } catch {
    return dateString
  }
}

function getInitials(name?: string) {
  if (!name) return 'NA'
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

function getScoreColor(score: number) {
  if (score >= 80) return 'bg-emerald-500'
  if (score >= 60) return 'bg-brand-500'
  return 'bg-amber-500'
}

function getScoreTextColor(score: number) {
  if (score >= 80) return 'text-emerald-700'
  if (score >= 60) return 'text-brand-700'
  return 'text-amber-700'
}

function getScoreBgColor(score: number) {
  if (score >= 80) return 'bg-emerald-50'
  if (score >= 60) return 'bg-brand-50'
  return 'bg-amber-50'
}

export default function EvaluationReviewPage() {
  const [selectedEvaluation, setSelectedEvaluation] = useState<EvaluationReport | null>(null)
  const [isOverrideMode, setIsOverrideMode] = useState(false)
  const [overrideReason, setOverrideReason] = useState('')
  const [submittingAction, setSubmittingAction] = useState<'confirm' | 'override' | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const { data: evaluationsResponse, isLoading: loading, error, refetch } = useQuery({
    queryKey: ['evaluations', 1, 10], // Assuming page 1, pageSize 10 for now
    queryFn: () => evaluationService.getEvaluations({ page: 1, pageSize: 10 }),
    refetchOnWindowFocus: false,
  })

  const evaluations = evaluationsResponse?.items || []
  const displayError = error ? 'Không thể tải danh sách đánh giá từ máy chủ.' : null

  async function handleOpenDetail(evaluationId: string) {
    try {
      setActionError(null)
      setIsOverrideMode(false)
      setOverrideReason('')

      const detail = await evaluationService.getEvaluationById(evaluationId)
      setSelectedEvaluation(detail)
    } catch (fetchError) {
      console.error(fetchError)
      setSelectedEvaluation(null)
    }
  }

  function closeDetail() {
    setSelectedEvaluation(null)
    setActionError(null)
    setIsOverrideMode(false)
    setOverrideReason('')
    setSubmittingAction(null)
  }

  async function refreshListAndSelection(evaluationId: string) {
    await refetch()
    const detailResponse = await evaluationService.getEvaluationById(evaluationId)
    setSelectedEvaluation(detailResponse)
  }

  async function handleConfirm() {
    if (!selectedEvaluation) return

    try {
      setSubmittingAction('confirm')
      setActionError(null)
      await evaluationService.confirmEvaluation(selectedEvaluation)
      await refreshListAndSelection(selectedEvaluation.id)
    } catch (submitError) {
      console.error(submitError)
      setActionError('Không thể xác nhận kết quả lúc này.')
    } finally {
      setSubmittingAction(null)
    }
  }

  async function handleOverride() {
    if (!selectedEvaluation) return

    const trimmedReason = overrideReason.trim()
    if (!trimmedReason) {
      setActionError('Vui lòng nhập lý do đổi kết quả.')
      return
    }

    try {
      setSubmittingAction('override')
      setActionError(null)
      await evaluationService.overrideEvaluation(selectedEvaluation, trimmedReason)
      await refreshListAndSelection(selectedEvaluation.id)
    } catch (submitError) {
      console.error(submitError)
      setActionError('Không thể đổi kết quả lúc này.')
    } finally {
      setSubmittingAction(null)
    }
  }

  const stats = useMemo(() => {
    const total = evaluations.length
    const completed = evaluations.filter((item) => item.status === 'completed').length
    const pending = evaluations.filter((item) => item.status === 'pending').length
    const highScore = evaluations.filter((item) => (item.overallScore ?? 0) >= 80).length

    return [
      { label: 'Tổng đánh giá', value: total.toString() },
      { label: 'Hoàn thành', value: completed.toString() },
      { label: 'Đang chờ', value: pending.toString() },
      { label: 'Đạt (≥80)', value: highScore.toString() },
    ]
  }, [evaluations])

  const criterionEntries = useMemo(
    () => Object.entries(selectedEvaluation?.criterionScores ?? {}),
    [selectedEvaluation]
  )

  // List view
  if (!selectedEvaluation) {
    return (
      <main className="p-6 space-y-6 bg-ink-50 dark:bg-ink-950">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="font-display text-2xl font-extrabold text-ink-900 dark:text-white">
              Đánh giá & Xác nhận
            </h1>
            <p className="mt-1 text-sm text-ink-500 dark:text-ink-400">
              Xem và xác nhận kết quả phỏng vấn AI của ứng viên
            </p>
          </div>
        </div>

        {/* Stats */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          {stats.map((stat) => (
            <div
              key={stat.label}
              className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-card"
            >
              <div className="font-display text-2xl font-extrabold text-ink-900 dark:text-white">
                {stat.value}
              </div>
              <div className="text-sm text-ink-500 dark:text-ink-400 mt-1">{stat.label}</div>
            </div>
          ))}
        </div>

        {/* List */}
        {loading ? (
          <EvaluationListSkeleton rows={4} />
        ) : displayError ? (
          <div className="rounded-2xl border border-red-200 dark:border-red-500/30 bg-red-50 dark:bg-red-500/10 p-4 text-sm text-red-700 dark:text-red-400">
            {displayError}
          </div>
        ) : evaluations.length === 0 ? (
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-10 text-center text-ink-500 dark:text-ink-400">
            Chưa có đánh giá nào để hiển thị.
          </div>
        ) : (
          <div className="space-y-4">
            {evaluations.map((evaluation) => (
              <div
                key={evaluation.id}
                className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card hover:shadow-card-hover transition cursor-pointer"
                onClick={() => handleOpenDetail(evaluation.id)}
              >
                <div className="flex flex-wrap items-start justify-between gap-4">
                  <div className="flex items-center gap-4">
                    <div className="grid h-14 w-14 place-items-center rounded-2xl bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400 font-display text-lg font-extrabold">
                      {getInitials(evaluation.candidateName)}
                    </div>
                    <div>
                      <h3 className="font-display text-lg font-bold text-ink-900 dark:text-white">
                        {evaluation.candidateName ?? 'Ứng viên'}
                      </h3>
                      <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink-500 dark:text-ink-400">
                        <span className="inline-flex items-center gap-1.5">
                          <Code2 className="w-4 h-4" />
                          {evaluation.jobTitle ?? '—'}
                        </span>
                        <span className="inline-flex items-center gap-1.5">
                          <Layers className="w-4 h-4" />
                          Round {evaluation.roundNumber}
                        </span>
                        <span className="inline-flex items-center gap-1.5">
                          <Calendar className="w-4 h-4" />
                          {formatDate(evaluation.createdAt)}
                        </span>
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    {evaluation.status === 'pending' ? (
                      <span className="inline-flex items-center gap-2 rounded-full bg-amber-50 dark:bg-amber-500/20 px-3 py-1.5 text-sm font-semibold text-amber-700 dark:text-amber-400 ring-1 ring-amber-200 dark:ring-amber-500/30">
                        <Clock className="w-4 h-4" /> Chờ HR xác nhận
                      </span>
                    ) : (
                      <span
                        className={`inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-sm font-semibold ${
                          (evaluation.overallScore ?? 0) >= 80
                            ? 'bg-emerald-50 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
                            : (evaluation.overallScore ?? 0) >= 60
                              ? 'bg-brand-50 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400'
                              : 'bg-amber-50 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400'
                        }`}
                      >
                        {formatVerdictLabel(evaluation.finalVerdict ?? evaluation.aiVerdict)}
                      </span>
                    )}
                    {evaluation.status !== 'pending' && (
                      <span className="font-display text-3xl font-extrabold text-ink-900 dark:text-white">
                        {evaluation.overallScore ?? 0}
                      </span>
                    )}
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </main>
    )
  }

  // Detail view - theo mockup hr-evaluation.html
  return (
    <>
      {/* Header */}
      <header className="sticky top-0 z-20 flex items-center gap-4 border-b border-ink-200 dark:border-white/10 bg-white/80 dark:bg-white/5 backdrop-blur px-6 h-16">
        <button
          onClick={closeDetail}
          className="grid h-9 w-9 place-items-center rounded-lg text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10"
        >
          <ArrowLeft className="w-5 h-5" />
        </button>
        <div className="flex items-center gap-2 text-sm text-ink-400">
          <Link to="#" onClick={closeDetail} className="hover:text-brand-600 dark:text-brand-400">
            Đánh giá
          </Link>
          <ChevronRight className="w-4 h-4" />
          <span className="text-ink-600 dark:text-ink-300 font-medium">
            {selectedEvaluation.candidateName} · {selectedEvaluation.jobTitle}
          </span>
        </div>
        <button className="ml-auto relative grid h-10 w-10 place-items-center rounded-xl hover:bg-ink-100 dark:hover:bg-white/10">
          <Bell className="w-5 h-5 text-ink-600 dark:text-ink-400" />
          <span className="absolute top-2 right-2 h-2 w-2 rounded-full bg-red-500"></span>
        </button>
      </header>

      <main className="p-6 grid gap-6 xl:grid-cols-[1fr_360px]">
        {/* LEFT: report */}
        <div className="space-y-6">
          {/* Candidate header */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card">
            <div className="flex flex-wrap items-start justify-between gap-4">
              <div className="flex items-center gap-4">
                <div className="grid h-14 w-14 place-items-center rounded-2xl bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400 font-display text-lg font-extrabold">
                  {getInitials(selectedEvaluation.candidateName)}
                </div>
                <div>
                  <h1 className="font-display text-xl font-extrabold leading-snug text-ink-900 dark:text-white">
                    {selectedEvaluation.candidateName}
                  </h1>
                  <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-ink-500 dark:text-ink-400">
                    <span className="inline-flex items-center gap-1.5">
                      <Code2 className="w-4 h-4" /> {selectedEvaluation.jobTitle}
                    </span>
                    <span className="inline-flex items-center gap-1.5">
                      <Layers className="w-4 h-4" /> Round {selectedEvaluation.roundNumber}
                    </span>
                    <span className="inline-flex items-center gap-1.5">
                      <Calendar className="w-4 h-4" /> {formatDate(selectedEvaluation.createdAt)}
                    </span>
                  </div>
                </div>
              </div>
              {selectedEvaluation.status === 'pending' ? (
                <span className="inline-flex items-center gap-2 rounded-full bg-amber-50 dark:bg-amber-500/20 px-3 py-1.5 text-sm font-semibold text-amber-700 dark:text-amber-400 ring-1 ring-amber-200 dark:ring-amber-500/30">
                  <Clock className="w-4 h-4" /> Chờ HR xác nhận
                </span>
              ) : (
                <span
                  className={`inline-flex items-center gap-2 rounded-full px-3 py-1.5 text-sm font-semibold ${
                    (selectedEvaluation.overallScore ?? 0) >= 80
                      ? 'bg-emerald-50 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
                      : (selectedEvaluation.overallScore ?? 0) >= 60
                        ? 'bg-brand-50 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400'
                        : 'bg-amber-50 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400'
                  }`}
                >
                  {formatVerdictLabel(
                    selectedEvaluation.finalVerdict ?? selectedEvaluation.aiVerdict
                  )}
                </span>
              )}
            </div>
          </div>

          {/* Scores */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card">
            <h2 className="font-display text-lg font-bold mb-4 text-ink-900 dark:text-white">
              Điểm theo tiêu chí
            </h2>
            <div className="space-y-4">
              {criterionEntries.length > 0 ? (
                criterionEntries.map(([criterion, score]) => (
                  <div key={criterion}>
                    <div className="mb-1 flex justify-between text-sm">
                      <span className="text-ink-600 dark:text-ink-400">{criterion}</span>
                      <span className="font-semibold text-ink-900 dark:text-white">{score}</span>
                    </div>
                    <div className="h-2 rounded-full bg-ink-100 dark:bg-white/10">
                      <div
                        className={`h-full rounded-full ${getScoreColor(score)}`}
                        style={{ width: `${score}%` }}
                      ></div>
                    </div>
                  </div>
                ))
              ) : (
                <p className="text-sm text-ink-500 dark:text-ink-400">
                  Chưa có dữ liệu điểm tiêu chí.
                </p>
              )}
            </div>

            {/* Language assessment */}
            {selectedEvaluation.languageAssessment && (
              <div className="mt-6 rounded-xl border border-ai-200 bg-ai-50/50 p-4">
                <div className="flex items-center gap-2 text-sm font-semibold text-ai-700">
                  <Languages className="w-4 h-4" />
                  Đánh giá ngôn ngữ ({selectedEvaluation.languageAssessment.language})
                </div>
                <div className="mt-3 grid grid-cols-3 gap-3 text-center text-sm">
                  <div className="rounded-lg bg-white p-2">
                    <div className="font-display text-lg font-extrabold text-ink-900">B2</div>
                    <div className="text-xs text-ink-400">CEFR</div>
                  </div>
                  <div className="rounded-lg bg-white p-2">
                    <div className="font-display text-lg font-extrabold text-ink-900">
                      {selectedEvaluation.languageAssessment.fluency ?? '—'}
                    </div>
                    <div className="text-xs text-ink-400">Trôi chảy</div>
                  </div>
                  <div className="rounded-lg bg-white p-2">
                    <div className="font-display text-lg font-extrabold text-ink-900">
                      {selectedEvaluation.languageAssessment.vocabulary ?? '—'}
                    </div>
                    <div className="text-xs text-ink-400">Từ vựng CN</div>
                  </div>
                </div>
              </div>
            )}
          </div>

          {/* Per-question */}
          {selectedEvaluation.questionAnalyses &&
            selectedEvaluation.questionAnalyses.length > 0 && (
              <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
                <h2 className="font-display text-lg font-bold mb-4">Phân tích theo câu hỏi</h2>
                <div className="space-y-3">
                  {selectedEvaluation.questionAnalyses.map((item, index) => (
                    <details
                      key={`${item.question}-${index}`}
                      className="group rounded-xl border border-ink-200"
                      open={index === 0}
                    >
                      <summary className="flex cursor-pointer items-center justify-between gap-3 px-4 py-3">
                        <span className="flex items-center gap-2 text-sm font-medium">
                          <span className="grid h-6 w-6 place-items-center rounded-full bg-ink-100 text-xs">
                            {index + 1}
                          </span>
                          {item.question}
                        </span>
                        <span className="flex items-center gap-2">
                          <span
                            className={`rounded-full px-2 py-0.5 text-xs font-semibold ${getScoreBgColor(item.score)} ${getScoreTextColor(item.score)}`}
                          >
                            {item.score}/10
                          </span>
                          <ChevronDown className="w-4 h-4 text-ink-400 group-open:rotate-180 transition" />
                        </span>
                      </summary>
                      <div className="border-t border-ink-100 px-4 py-3 text-sm text-ink-600">
                        <p>
                          <b className="text-ink-800">AI nhận xét:</b> {item.analysis}
                        </p>
                      </div>
                    </details>
                  ))}
                </div>
              </div>
            )}

          {/* Recording / transcript */}
          <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
            <h2 className="font-display text-lg font-bold mb-4">Bản ghi & transcript</h2>
            <div className="aspect-video rounded-xl bg-ink-900 grid place-items-center text-ink-400">
              <button className="grid h-14 w-14 place-items-center rounded-full bg-white/10 text-white hover:bg-white/20">
                <Play className="w-6 h-6" />
              </button>
            </div>
            <button className="mt-3 inline-flex items-center gap-2 text-sm font-medium text-brand-600 hover:underline">
              <FileText className="w-4 h-4" /> Xem transcript đầy đủ
            </button>
          </div>
        </div>

        {/* RIGHT: verdict & decision */}
        <aside className="space-y-5 xl:sticky xl:top-24 self-start">
          {/* AI verdict */}
          <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card text-center">
            <div className="flex items-center justify-center gap-2 text-sm font-semibold text-ai-700">
              <Sparkles className="w-4 h-4" /> Verdict của AI
            </div>
            <div className="mt-3 inline-flex items-center gap-2 rounded-full bg-emerald-50 px-4 py-1.5 text-base font-bold text-emerald-700 ring-1 ring-emerald-200">
              <CheckCircle2 className="w-5 h-5" />{' '}
              {formatVerdictLabel(selectedEvaluation.aiVerdict)}
            </div>
            <div className="mt-4 font-display text-5xl font-extrabold leading-none">
              {selectedEvaluation.overallScore ?? 0}
              <span className="text-lg text-ink-400">/100</span>
            </div>
            <p className="mt-3 text-sm text-ink-500">
              Đề xuất:{' '}
              <b className="text-ink-700">
                Mời phỏng vấn Round {selectedEvaluation.roundNumber + 1}
              </b>
            </p>
          </div>

          {/* HR decision */}
          {!selectedEvaluation.hrReview ? (
            <div className="rounded-2xl border border-ink-200 bg-white p-6 shadow-card">
              <h3 className="font-display font-bold">Quyết định của HR</h3>
              <p className="mt-1 text-sm text-ink-500">
                Xác nhận verdict của AI hoặc ghi đè (bắt buộc nêu lý do).
              </p>

              <div className="mt-4 grid grid-cols-2 gap-2">
                <button
                  onClick={handleConfirm}
                  disabled={submittingAction !== null}
                  className={`rounded-xl border-2 px-3 py-2.5 text-sm font-semibold transition ${
                    submittingAction === 'confirm'
                      ? 'border-brand-500 bg-brand-50 text-brand-700'
                      : 'border-emerald-500 bg-emerald-50 text-emerald-700 hover:bg-emerald-100'
                  } disabled:opacity-50`}
                >
                  {submittingAction === 'confirm' ? 'Đang xác nhận...' : 'Xác nhận AI'}
                </button>
                <button
                  onClick={() => setIsOverrideMode(!isOverrideMode)}
                  disabled={submittingAction !== null}
                  className={`rounded-xl border-2 px-3 py-2.5 text-sm font-semibold transition ${
                    isOverrideMode
                      ? 'border-amber-500 bg-amber-50 text-amber-700'
                      : 'border-ink-200 text-ink-600 hover:border-ink-300'
                  } disabled:opacity-50`}
                >
                  Override
                </button>
              </div>

              {/* Override panel */}
              <AnimatePresence>
                {isOverrideMode && (
                  <motion.div
                    initial={{ opacity: 0, height: 0 }}
                    animate={{ opacity: 1, height: 'auto' }}
                    exit={{ opacity: 0, height: 0 }}
                    className="mt-4 space-y-3 overflow-hidden"
                  >
                    <div>
                      <label className="mb-1.5 block text-sm font-medium text-ink-600">
                        Verdict ghi đè
                      </label>
                      <div className="grid grid-cols-2 gap-2">
                        <button className="rounded-lg border border-emerald-300 bg-emerald-50 px-3 py-2 text-sm font-medium text-emerald-700">
                          Pass
                        </button>
                        <button className="rounded-lg border border-ink-200 px-3 py-2 text-sm font-medium text-ink-600 hover:border-red-300">
                          Not Pass
                        </button>
                      </div>
                    </div>
                    <div>
                      <label className="mb-1.5 block text-sm font-medium text-ink-600">
                        Lý do ghi đè <span className="text-red-500">*</span>
                      </label>
                      <textarea
                        rows={3}
                        value={overrideReason}
                        onChange={(e) => setOverrideReason(e.target.value)}
                        placeholder="Bắt buộc — giải thích vì sao thay đổi verdict của AI..."
                        className="w-full rounded-xl border border-ink-200 px-3 py-2 text-sm outline-none focus:border-brand-500 focus:ring-2 focus:ring-brand-100"
                      />
                    </div>
                  </motion.div>
                )}
              </AnimatePresence>

              {actionError && (
                <div className="mt-4 rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-700">
                  {actionError}
                </div>
              )}

              <div className="mt-4 space-y-2">
                <button
                  onClick={handleOverride}
                  disabled={submittingAction !== null || !isOverrideMode || !overrideReason.trim()}
                  className="w-full rounded-xl bg-brand-600 px-4 py-2.5 text-sm font-bold text-white hover:bg-brand-700 disabled:opacity-50"
                >
                  Lưu & gửi kết quả
                </button>
                <button className="w-full rounded-xl border border-ink-200 px-4 py-2.5 text-sm font-semibold text-ink-700 hover:bg-ink-50">
                  Lưu & mời Round tiếp theo
                </button>
              </div>
            </div>
          ) : (
            <div className="rounded-2xl border border-emerald-200 bg-emerald-50 p-6 shadow-card">
              <h3 className="font-display font-bold text-emerald-800">Đã xác nhận</h3>
              <p className="mt-1 text-sm text-emerald-700">
                Verdict: <b>{formatVerdictLabel(selectedEvaluation.hrReview.finalVerdict)}</b>
              </p>
              {selectedEvaluation.hrReview.isOverride &&
                selectedEvaluation.hrReview.overrideReason && (
                  <div className="mt-3 p-3 rounded-lg bg-white text-sm text-ink-600">
                    <b>Lý do override:</b> {selectedEvaluation.hrReview.overrideReason}
                  </div>
                )}
            </div>
          )}

          {/* CV-JD match */}
          <div className="rounded-2xl border border-ink-200 bg-white p-5 shadow-card">
            <div className="flex items-center justify-between text-sm">
              <span className="flex items-center gap-1.5 font-semibold text-ai-700">
                <Sparkles className="w-4 h-4" /> Match CV–JD
              </span>
              <span className="font-display text-xl font-extrabold text-ai-700">87</span>
            </div>
            <div className="mt-2 h-2 rounded-full bg-ai-100">
              <div className="h-full w-[87%] rounded-full bg-gradient-to-r from-brand-600 to-ai-600"></div>
            </div>
          </div>
        </aside>
      </main>
    </>
  )
}
