import { useEffect, useMemo, useState } from 'react';
import { motion } from 'framer-motion';
import { Loader2, X } from 'lucide-react';
import { evaluationService } from '@/services/evaluation/evaluationService';
import type { EvaluationReport } from '@/types/evaluation';

function formatVerdictLabel(verdict?: string) {
  if (verdict === 'pass') return 'Đạt';
  if (verdict === 'not_pass') return 'Không đạt';
  return 'Chưa rõ';
}

function formatDate(dateString?: string) {
  if (!dateString) return '—';

  try {
    return new Date(dateString).toLocaleDateString('vi-VN');
  } catch {
    return dateString;
  }
}

function getInitials(name?: string) {
  if (!name) return 'NA';
  return name
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('');
}

export default function EvaluationReviewPage() {
  const [evaluations, setEvaluations] = useState<EvaluationReport[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedEvaluation, setSelectedEvaluation] = useState<EvaluationReport | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState<string | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [isOverrideMode, setIsOverrideMode] = useState(false);
  const [overrideReason, setOverrideReason] = useState('');
  const [submittingAction, setSubmittingAction] = useState<'confirm' | 'override' | null>(null);
  const [actionError, setActionError] = useState<string | null>(null);

  useEffect(() => {
    async function fetchEvaluations() {
      try {
        setLoading(true);
        setError(null);
        const response = await evaluationService.getEvaluations({ page: 1, pageSize: 10 });
        setEvaluations(response.items);
      } catch (fetchError) {
        console.error(fetchError);
        setError('Không thể tải danh sách đánh giá từ máy chủ.');
      } finally {
        setLoading(false);
      }
    }

    fetchEvaluations();
  }, []);

  const stats = useMemo(() => {
    const total = evaluations.length;
    const completed = evaluations.filter((item) => item.status === 'completed').length;
    const pending = evaluations.filter((item) => item.status === 'pending').length;
    const highScore = evaluations.filter((item) => (item.overallScore ?? 0) >= 80).length;

    return [
      { label: 'Tổng đánh giá', value: total.toString(), color: 'text-blue-400' },
      { label: 'Hoàn thành', value: completed.toString(), color: 'text-emerald-400' },
      { label: 'Đang chờ', value: pending.toString(), color: 'text-amber-400' },
      { label: 'Đạt (>=80)', value: highScore.toString(), color: 'text-violet-400' },
    ];
  }, [evaluations]);

  const criterionEntries = useMemo(
    () => Object.entries(selectedEvaluation?.criterionScores ?? {}),
    [selectedEvaluation],
  );

  async function handleOpenDetail(evaluationId: string) {
    try {
      setIsModalOpen(true);
      setDetailLoading(true);
      setDetailError(null);
      setActionError(null);
      setIsOverrideMode(false);
      setOverrideReason('');

      const detail = await evaluationService.getEvaluationById(evaluationId);
      setSelectedEvaluation(detail);
    } catch (fetchError) {
      console.error(fetchError);
      setDetailError('Không thể tải chi tiết đánh giá.');
      setSelectedEvaluation(null);
    } finally {
      setDetailLoading(false);
    }
  }

  function closeModal() {
    setIsModalOpen(false);
    setSelectedEvaluation(null);
    setDetailError(null);
    setActionError(null);
    setIsOverrideMode(false);
    setOverrideReason('');
    setSubmittingAction(null);
  }

  async function refreshListAndSelection(evaluationId: string) {
    const [listResponse, detailResponse] = await Promise.all([
      evaluationService.getEvaluations({ page: 1, pageSize: 10 }),
      evaluationService.getEvaluationById(evaluationId),
    ]);

    setEvaluations(listResponse.items);
    setSelectedEvaluation(detailResponse);
  }

  async function handleConfirm() {
    if (!selectedEvaluation) return;

    try {
      setSubmittingAction('confirm');
      setActionError(null);
      await evaluationService.confirmEvaluation(selectedEvaluation);
      await refreshListAndSelection(selectedEvaluation.id);
      closeModal();
    } catch (submitError) {
      console.error(submitError);
      setActionError('Không thể xác nhận kết quả lúc này.');
    } finally {
      setSubmittingAction(null);
    }
  }

  async function handleOverride() {
    if (!selectedEvaluation) return;

    const trimmedReason = overrideReason.trim();
    if (!trimmedReason) {
      setActionError('Vui lòng nhập lý do đổi kết quả.');
      return;
    }

    try {
      setSubmittingAction('override');
      setActionError(null);
      await evaluationService.overrideEvaluation(selectedEvaluation, trimmedReason);
      await refreshListAndSelection(selectedEvaluation.id);
      closeModal();
    } catch (submitError) {
      console.error(submitError);
      setActionError('Không thể đổi kết quả lúc này.');
    } finally {
      setSubmittingAction(null);
    }
  }

  return (
    <div className="p-6 lg:p-8">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-8">
        <h1 className="text-3xl font-semibold text-white mb-2">Đánh giá</h1>
        <p className="text-text-secondary">Xem và quản lý kết quả đánh giá ứng viên</p>
      </motion.div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
        {stats.map((stat) => (
          <motion.div key={stat.label} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-4">
            <p className={`text-2xl font-semibold ${stat.color}`}>{stat.value}</p>
            <p className="text-sm text-white/40">{stat.label}</p>
          </motion.div>
        ))}
      </div>

      {error && (
        <div className="mb-6 rounded-xl border border-red-500/20 bg-red-500/10 p-4 text-sm text-red-300">
          {error}
        </div>
      )}

      {loading ? (
        <div className="flex flex-col items-center justify-center py-20 gap-3">
          <Loader2 className="w-10 h-10 text-accent-primary animate-spin" />
          <p className="text-sm text-white/40">Đang tải dữ liệu đánh giá...</p>
        </div>
      ) : evaluations.length === 0 ? (
        <div className="rounded-2xl border border-white/[0.08] bg-white/[0.03] p-10 text-center text-white/60">
          Chưa có đánh giá nào để hiển thị.
        </div>
      ) : (
        <div className="space-y-4">
          {evaluations.map((evaluation, index) => (
            <motion.div key={evaluation.id} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: index * 0.05 }}>
              <div className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 hover:bg-white/[0.05] transition-colors">
                <div className="flex items-start justify-between mb-4 gap-4">
                  <div className="flex items-center gap-4 min-w-0">
                    <div className="w-12 h-12 rounded-full bg-gradient-to-br from-accent-primary/30 to-violet/30 flex items-center justify-center text-sm font-medium text-white shrink-0">
                      {getInitials(evaluation.candidateName)}
                    </div>
                    <div className="min-w-0">
                      <h3 className="text-lg font-semibold text-white truncate">{evaluation.candidateName ?? 'Ứng viên chưa xác định'}</h3>
                      <p className="text-sm text-text-secondary truncate">{evaluation.jobTitle ?? 'Chưa có vị trí ứng tuyển'}</p>
                      <p className="text-xs text-white/40 mt-1">Round {evaluation.roundNumber} • {formatVerdictLabel(evaluation.finalVerdict ?? evaluation.aiVerdict)}</p>
                    </div>
                  </div>
                  <div className="text-right shrink-0">
                    {evaluation.status === 'completed' ? (
                      <div className="text-3xl font-semibold bg-gradient-to-r from-accent-primary to-violet bg-clip-text text-transparent">
                        {evaluation.overallScore ?? '—'}
                      </div>
                    ) : (
                      <span className="px-3 py-1 rounded-full bg-amber-500/20 text-amber-400 text-sm">Đang chờ</span>
                    )}
                  </div>
                </div>
                <div className="flex items-center justify-between gap-4">
                  <span className="text-xs text-white/40">{formatDate(evaluation.createdAt)}</span>
                  <button
                    type="button"
                    onClick={() => handleOpenDetail(evaluation.id)}
                    className="px-4 py-2 rounded-lg bg-accent-primary/20 text-accent-primary text-sm font-medium hover:bg-accent-primary/30 transition-colors"
                  >
                    Xem chi tiết
                  </button>
                </div>
              </div>
            </motion.div>
          ))}
        </div>
      )}

      {isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-950/70 px-4 py-8">
          <div className="relative w-full max-w-3xl max-h-[90vh] overflow-y-auto rounded-3xl border border-white/10 bg-slate-950/95 p-6 shadow-2xl">
            <button
              type="button"
              onClick={closeModal}
              className="absolute right-4 top-4 rounded-lg p-2 text-white/50 hover:bg-white/10 hover:text-white transition-colors"
            >
              <X className="h-5 w-5" />
            </button>

            {detailLoading ? (
              <div className="flex flex-col items-center justify-center py-16 gap-3">
                <Loader2 className="w-10 h-10 text-accent-primary animate-spin" />
                <p className="text-sm text-white/40">Đang tải chi tiết đánh giá...</p>
              </div>
            ) : detailError ? (
              <div className="rounded-xl border border-red-500/20 bg-red-500/10 p-4 text-sm text-red-300">
                {detailError}
              </div>
            ) : selectedEvaluation ? (
              <div>
                <div className="mb-6 pr-10">
                  <h2 className="text-2xl font-semibold text-white mb-2">Chi tiết đánh giá</h2>
                  <p className="text-sm text-white/50">
                    {selectedEvaluation.candidateName} • {selectedEvaluation.jobTitle} • Round {selectedEvaluation.roundNumber}
                  </p>
                </div>

                <div className="grid gap-4 md:grid-cols-3 mb-6">
                  <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                    <p className="text-sm text-white/40 mb-1">AI Verdict</p>
                    <p className="text-lg font-semibold text-white">{formatVerdictLabel(selectedEvaluation.aiVerdict)}</p>
                  </div>
                  <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                    <p className="text-sm text-white/40 mb-1">Điểm tổng</p>
                    <p className="text-lg font-semibold text-white">{selectedEvaluation.overallScore ?? '—'}</p>
                  </div>
                  <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                    <p className="text-sm text-white/40 mb-1">Trạng thái review</p>
                    <p className="text-lg font-semibold text-white">{selectedEvaluation.hrReview ? 'Đã duyệt' : 'Chờ duyệt'}</p>
                  </div>
                </div>

                <div className="space-y-6">
                  <section>
                    <h3 className="text-sm font-semibold uppercase tracking-wide text-white/50 mb-3">Criterion Scores</h3>
                    <div className="grid gap-3 md:grid-cols-2">
                      {criterionEntries.length > 0 ? (
                        criterionEntries.map(([criterion, score]) => (
                          <div key={criterion} className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                            <p className="text-sm text-white/50 mb-1">{criterion}</p>
                            <p className="text-xl font-semibold text-white">{score}</p>
                          </div>
                        ))
                      ) : (
                        <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4 text-sm text-white/50 md:col-span-2">
                          Chưa có dữ liệu criterion scores.
                        </div>
                      )}
                    </div>
                  </section>

                  <section>
                    <h3 className="text-sm font-semibold uppercase tracking-wide text-white/50 mb-3">Reasoning</h3>
                    <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4 text-sm leading-7 text-white/80 whitespace-pre-wrap">
                      {selectedEvaluation.reasoning || 'Chưa có reasoning.'}
                    </div>
                  </section>

                  {selectedEvaluation.languageAssessment && (
                    <section>
                      <h3 className="text-sm font-semibold uppercase tracking-wide text-white/50 mb-3">Language Assessment</h3>
                      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-5">
                        <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                          <p className="text-sm text-white/50 mb-1">Ngôn ngữ</p>
                          <p className="text-base font-semibold text-white">{selectedEvaluation.languageAssessment.language}</p>
                        </div>
                        <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                          <p className="text-sm text-white/50 mb-1">Fluency</p>
                          <p className="text-base font-semibold text-white">{selectedEvaluation.languageAssessment.fluency}</p>
                        </div>
                        <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                          <p className="text-sm text-white/50 mb-1">Grammar</p>
                          <p className="text-base font-semibold text-white">{selectedEvaluation.languageAssessment.grammar}</p>
                        </div>
                        <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                          <p className="text-sm text-white/50 mb-1">Vocabulary</p>
                          <p className="text-base font-semibold text-white">{selectedEvaluation.languageAssessment.vocabulary}</p>
                        </div>
                        <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                          <p className="text-sm text-white/50 mb-1">Comprehension</p>
                          <p className="text-base font-semibold text-white">{selectedEvaluation.languageAssessment.comprehension}</p>
                        </div>
                      </div>
                    </section>
                  )}

                  {selectedEvaluation.questionAnalyses && selectedEvaluation.questionAnalyses.length > 0 && (
                    <section>
                      <h3 className="text-sm font-semibold uppercase tracking-wide text-white/50 mb-3">Per-question Analysis</h3>
                      <div className="space-y-3">
                        {selectedEvaluation.questionAnalyses.map((item, index) => (
                          <div key={`${item.question}-${index}`} className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                            <p className="text-sm text-white/40 mb-2">Câu hỏi {index + 1}</p>
                            <p className="text-white font-medium mb-2">{item.question}</p>
                            <p className="text-sm text-white/70 mb-2">{item.analysis}</p>
                            <p className="text-xs text-white/40">Điểm: {item.score}</p>
                          </div>
                        ))}
                      </div>
                    </section>
                  )}

                  {selectedEvaluation.hrReview && (
                    <section>
                      <h3 className="text-sm font-semibold uppercase tracking-wide text-white/50 mb-3">Kết quả HR đã duyệt</h3>
                      <div className="rounded-2xl border border-emerald-500/20 bg-emerald-500/10 p-4 text-sm text-emerald-200">
                        <p>Kết luận cuối: {formatVerdictLabel(selectedEvaluation.hrReview.finalVerdict)}</p>
                        {selectedEvaluation.hrReview.isOverride && selectedEvaluation.hrReview.overrideReason && (
                          <p className="mt-2 text-emerald-100">Lý do override: {selectedEvaluation.hrReview.overrideReason}</p>
                        )}
                      </div>
                    </section>
                  )}
                </div>

                {!selectedEvaluation.hrReview && (
                  <div className="mt-8 border-t border-white/10 pt-6">
                    <div className="flex flex-wrap gap-3 mb-4">
                      <button
                        type="button"
                        onClick={handleConfirm}
                        disabled={submittingAction !== null}
                        className="px-4 py-2.5 rounded-xl bg-emerald-500/20 text-emerald-300 hover:bg-emerald-500/30 transition-colors disabled:opacity-60"
                      >
                        {submittingAction === 'confirm' ? 'Đang xác nhận...' : 'Xác nhận'}
                      </button>
                      <button
                        type="button"
                        onClick={() => {
                          setIsOverrideMode((prev) => !prev);
                          setActionError(null);
                        }}
                        disabled={submittingAction !== null}
                        className="px-4 py-2.5 rounded-xl bg-amber-500/20 text-amber-300 hover:bg-amber-500/30 transition-colors disabled:opacity-60"
                      >
                        Đổi kết quả
                      </button>
                    </div>

                    {isOverrideMode && (
                      <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                        <label className="block text-sm font-medium text-white mb-2">Lý do đổi kết quả *</label>
                        <textarea
                          value={overrideReason}
                          onChange={(event) => setOverrideReason(event.target.value)}
                          rows={4}
                          placeholder="Nhập lý do override để phục vụ audit trail..."
                          className="w-full rounded-xl border border-white/10 bg-slate-900/60 px-4 py-3 text-sm text-white outline-none placeholder:text-white/30 focus:border-accent-primary"
                        />
                        <div className="mt-3 flex justify-end">
                          <button
                            type="button"
                            onClick={handleOverride}
                            disabled={submittingAction !== null}
                            className="px-4 py-2.5 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white hover:opacity-90 transition-opacity disabled:opacity-60"
                          >
                            {submittingAction === 'override' ? 'Đang gửi...' : 'Gửi override'}
                          </button>
                        </div>
                      </div>
                    )}

                    {actionError && (
                      <div className="mt-4 rounded-xl border border-red-500/20 bg-red-500/10 p-4 text-sm text-red-300">
                        {actionError}
                      </div>
                    )}
                  </div>
                )}
              </div>
            ) : null}
          </div>
        </div>
      )}
    </div>
  );
}
