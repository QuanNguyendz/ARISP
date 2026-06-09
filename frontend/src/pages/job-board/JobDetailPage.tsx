import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import CandidateNav from '@components/layout/CandidateNav';
import CandidateFooter from '@components/layout/CandidateFooter';
import {
  MapPin,
  DollarSign,
  Clock,
  Calendar,
  ArrowLeft,
  Loader2,
  Send,
  Languages,
  ShieldAlert,
  Briefcase,
  Sparkles,
  Building2,
  CheckCircle2,
} from 'lucide-react';
import jobService from '@/services/job/jobService';
import type { JobPosting } from '@/types/job';

function formatSalary(job: JobPosting): string {
  if (job.salaryIsNegotiable) return 'Thỏa thuận';
  if (job.salaryMin && job.salaryMax) {
    return `$${job.salaryMin.toLocaleString()} - $${job.salaryMax.toLocaleString()}`;
  }
  if (job.salaryMin) return `Từ $${job.salaryMin.toLocaleString()}`;
  if (job.salaryMax) return `Đến $${job.salaryMax.toLocaleString()}`;
  return 'Thỏa thuận';
}

function formatWorkMode(mode?: string): string {
  if (!mode) return 'Toàn thời gian';
  const mappings: Record<string, string> = {
    fulltime: 'Toàn thời gian',
    parttime: 'Bán thời gian',
    contract: 'Hợp đồng',
    internship: 'Thực tập',
  };
  return mappings[mode.toLowerCase()] || mode;
}

function formatInterviewMode(mode?: string): string {
  if (!mode) return 'Chưa cấu hình';
  const mappings: Record<string, string> = {
    onsite: 'Trực tiếp',
    remote: 'Từ xa',
    both: 'Hybrid',
  };
  return mappings[mode.toLowerCase()] || mode;
}

function formatPostedDate(dateStr?: string): string {
  if (!dateStr) return 'Mới đăng';
  try {
    return new Date(dateStr).toLocaleDateString('vi-VN');
  } catch {
    return dateStr;
  }
}

export default function JobDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [job, setJob] = useState<JobPosting | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function loadJobDetail() {
      if (!id) return;
      try {
        setLoading(true);
        const data = await jobService.getJobPostingById(id);
        setJob(data);
      } catch (err) {
        console.error(err);
        setError('Không tìm thấy tin tuyển dụng này hoặc tin tuyển dụng đã ngừng nhận hồ sơ.');
      } finally {
        setLoading(false);
      }
    }

    loadJobDetail();
  }, [id]);

  return (
    <div className="min-h-screen bg-bg-primary flex flex-col">
      <CandidateNav />

      <main className="flex-1 pt-24 pb-14 px-4 sm:px-6 lg:px-8">
        <div className="mx-auto w-full max-w-7xl">
          {loading ? (
            <div className="flex min-h-[55vh] flex-col items-center justify-center gap-3">
              <Loader2 className="w-10 h-10 text-accent-primary animate-spin" />
              <p className="text-sm text-text-secondary">Đang tải thông tin chi tiết...</p>
            </div>
          ) : error || !job ? (
            <div className="py-20 text-center">
              <ShieldAlert className="mx-auto mb-4 h-16 w-16 text-red-400" />
              <h2 className="mb-2 text-2xl font-bold text-white">Đã xảy ra lỗi</h2>
              <p className="mb-6 text-text-secondary">{error || 'Không tìm thấy dữ liệu.'}</p>
              <button
                type="button"
                onClick={() => navigate('/jobs')}
                className="inline-flex items-center gap-2 rounded-xl border border-white/10 bg-white/5 px-6 py-3 font-medium text-white transition-colors hover:bg-white/10"
              >
                <ArrowLeft className="h-4 w-4" />
                Xem việc làm khác
              </button>
            </div>
          ) : (
            <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
              <button
                type="button"
                onClick={() => navigate('/jobs')}
                className="mb-6 inline-flex items-center gap-2 text-sm text-text-secondary transition-colors hover:text-white"
              >
                <ArrowLeft className="h-4 w-4" />
                Quay lại danh sách việc làm
              </button>

              <section className="relative overflow-hidden rounded-[28px] border border-white/[0.08] bg-white/[0.03] p-6 backdrop-blur-xl sm:p-8 lg:p-10">
                <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_top_right,rgba(139,92,246,0.18),transparent_32%),radial-gradient(circle_at_left,rgba(59,130,246,0.12),transparent_28%)]" />
                <div className="relative flex flex-col gap-8 xl:flex-row xl:items-start xl:justify-between">
                  <div className="min-w-0 flex-1">
                    <div className="mb-6 flex flex-wrap items-center gap-3">
                      <span className="rounded-full bg-accent-primary/15 px-3 py-1 text-xs font-semibold uppercase tracking-[0.18em] text-accent-primary">
                        Job Detail
                      </span>
                      {job.isUrgent && (
                        <span className="rounded-full bg-red-500/15 px-3 py-1 text-xs font-semibold text-red-300">
                          Tuyển gấp
                        </span>
                      )}
                      <span className="rounded-full bg-white/8 px-3 py-1 text-xs font-medium text-white/60">
                        Đăng ngày {formatPostedDate(job.createdAt)}
                      </span>
                    </div>

                    <div className="flex flex-col gap-6 md:flex-row md:items-start">
                      <div className="flex h-20 w-20 shrink-0 items-center justify-center rounded-2xl bg-gradient-to-br from-accent-primary to-violet text-2xl font-bold text-white shadow-lg shadow-accent-primary/20">
                        {job.title.substring(0, 2).toUpperCase()}
                      </div>

                      <div className="min-w-0 flex-1">
                        <h1 className="mb-3 text-3xl font-semibold tracking-tight text-white md:text-4xl">
                          {job.title}
                        </h1>
                        <div className="mb-5 flex flex-wrap items-center gap-3 text-sm text-white/55">
                          <span className="inline-flex items-center gap-2 rounded-full bg-white/5 px-3 py-1.5">
                            <Building2 className="h-4 w-4 text-accent-primary" />
                            {job.department || 'Phòng ban tuyển dụng'}
                          </span>
                          <span className="inline-flex items-center gap-2 rounded-full bg-white/5 px-3 py-1.5">
                            <MapPin className="h-4 w-4 text-accent-primary" />
                            {job.location || 'Remote'}
                          </span>
                          <span className="inline-flex items-center gap-2 rounded-full bg-white/5 px-3 py-1.5">
                            <DollarSign className="h-4 w-4 text-accent-primary" />
                            {formatSalary(job)}
                          </span>
                          <span className="inline-flex items-center gap-2 rounded-full bg-white/5 px-3 py-1.5">
                            <Clock className="h-4 w-4 text-accent-primary" />
                            {formatWorkMode(job.workMode || job.employmentType)}
                          </span>
                        </div>

                        <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4">
                          <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                            <p className="mb-1 text-xs uppercase tracking-wide text-white/35">Hình thức phỏng vấn</p>
                            <p className="text-sm font-medium text-white">{formatInterviewMode(job.interviewMode)}</p>
                          </div>
                          <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                            <p className="mb-1 text-xs uppercase tracking-wide text-white/35">Kinh nghiệm</p>
                            <p className="text-sm font-medium text-white capitalize">{job.experienceLevel || 'Không yêu cầu'}</p>
                          </div>
                          <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                            <p className="mb-1 text-xs uppercase tracking-wide text-white/35">Ngôn ngữ phỏng vấn</p>
                            <p className="text-sm font-medium text-white">{job.languageRequirement || 'Tiếng Việt'}</p>
                          </div>
                          <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4">
                            <p className="mb-1 text-xs uppercase tracking-wide text-white/35">Hạn nộp hồ sơ</p>
                            <p className="text-sm font-medium text-white">
                              {job.applicationDeadline ? new Date(job.applicationDeadline).toLocaleDateString('vi-VN') : 'Không giới hạn'}
                            </p>
                          </div>
                        </div>
                      </div>
                    </div>
                  </div>

                  <div className="w-full xl:w-[320px] xl:shrink-0">
                    <div className="rounded-3xl border border-white/10 bg-slate-950/50 p-5 shadow-2xl shadow-black/20">
                      <p className="mb-2 text-sm font-medium text-white/60">Sẵn sàng ứng tuyển?</p>
                      <p className="mb-5 text-sm leading-6 text-white/45">
                        Hoàn tất hồ sơ và gửi đơn ngay để bắt đầu quy trình phỏng vấn AI cho vị trí này.
                      </p>
                      <button
                        type="button"
                        onClick={() => navigate(`/candidate/applications/${job.id}`)}
                        className="mb-3 flex w-full items-center justify-center gap-2 rounded-2xl bg-gradient-to-r from-accent-primary to-violet px-6 py-4 text-sm font-semibold text-white shadow-lg shadow-accent-primary/25 transition-opacity hover:opacity-90"
                      >
                        <Send className="h-4 w-4" />
                        Nộp đơn ứng tuyển
                      </button>
                      <div className="rounded-2xl border border-white/10 bg-white/[0.03] p-4 text-sm text-white/55">
                        <div className="mb-2 flex items-center gap-2 text-white/80">
                          <Sparkles className="h-4 w-4 text-accent-primary" />
                          Quy trình nhanh gọn
                        </div>
                        <p>
                          Hệ thống sẽ hướng dẫn bạn qua từng bước: nộp hồ sơ, nhận lịch phỏng vấn, và xem phản hồi sau đánh giá.
                        </p>
                      </div>
                    </div>
                  </div>
                </div>
              </section>

              <section className="mt-8 grid gap-6 xl:grid-cols-[minmax(0,1.65fr)_minmax(320px,0.85fr)]">
                <div className="space-y-6">
                  <div className="rounded-[28px] border border-white/[0.08] bg-white/[0.03] p-6 backdrop-blur-xl sm:p-8">
                    <div className="mb-5 flex items-center gap-3">
                      <Briefcase className="h-5 w-5 text-accent-primary" />
                      <h2 className="text-xl font-semibold text-white">Mô tả công việc</h2>
                    </div>
                    <div className="rounded-2xl border border-white/8 bg-slate-950/35 p-5 text-sm leading-7 text-white/75 whitespace-pre-line sm:text-[15px]">
                      {job.jobDescription}
                    </div>
                  </div>

                  {job.skills && job.skills.length > 0 && (
                    <div className="rounded-[28px] border border-white/[0.08] bg-white/[0.03] p-6 backdrop-blur-xl sm:p-8">
                      <div className="mb-5 flex items-center gap-3">
                        <CheckCircle2 className="h-5 w-5 text-accent-primary" />
                        <h2 className="text-xl font-semibold text-white">Kỹ năng phù hợp</h2>
                      </div>
                      <div className="flex flex-wrap gap-3">
                        {job.skills.map((skill) => (
                          <span
                            key={skill}
                            className="rounded-2xl border border-white/10 bg-white/5 px-4 py-2 text-sm font-medium text-white/85"
                          >
                            {skill}
                          </span>
                        ))}
                      </div>
                    </div>
                  )}
                </div>

                <div className="space-y-6">
                  <div className="rounded-[28px] border border-white/[0.08] bg-white/[0.03] p-6 backdrop-blur-xl">
                    <div className="mb-5 flex items-center gap-3">
                      <Languages className="h-5 w-5 text-accent-primary" />
                      <h3 className="text-lg font-semibold text-white">Tổng quan vị trí</h3>
                    </div>
                    <div className="space-y-4 text-sm text-white/60">
                      <div className="rounded-2xl border border-white/8 bg-white/[0.02] p-4">
                        <p className="mb-1 text-xs uppercase tracking-wide text-white/35">Ngôn ngữ JD</p>
                        <p className="font-medium text-white">{job.detectedLanguage === 'vi' ? 'Tiếng Việt' : 'Tiếng Anh / Khác'}</p>
                      </div>
                      <div className="rounded-2xl border border-white/8 bg-white/[0.02] p-4">
                        <p className="mb-1 text-xs uppercase tracking-wide text-white/35">Loại công việc</p>
                        <p className="font-medium text-white">{formatWorkMode(job.workMode || job.employmentType)}</p>
                      </div>
                      <div className="rounded-2xl border border-white/8 bg-white/[0.02] p-4">
                        <p className="mb-1 text-xs uppercase tracking-wide text-white/35">Mức lương</p>
                        <p className="font-medium text-white">{formatSalary(job)}</p>
                      </div>
                    </div>
                  </div>

                  <div className="rounded-[28px] border border-white/[0.08] bg-white/[0.03] p-6 backdrop-blur-xl">
                    <div className="mb-5 flex items-center gap-3">
                      <Sparkles className="h-5 w-5 text-accent-primary" />
                      <h3 className="text-lg font-semibold text-white">Quy trình phỏng vấn AI</h3>
                    </div>
                    <div className="space-y-4">
                      {job.roundConfigs && job.roundConfigs.length > 0 ? (
                        job.roundConfigs.map((round) => (
                          <div key={round.roundNumber} className="flex gap-4 rounded-2xl border border-white/8 bg-white/[0.02] p-4">
                            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-accent-primary/20 text-sm font-semibold text-accent-primary">
                              {round.roundNumber}
                            </div>
                            <div className="min-w-0">
                              <p className="text-sm font-semibold capitalize text-white">{round.roundType} Round</p>
                              <p className="mt-1 text-sm text-white/50">Ngôn ngữ: {round.interviewLanguage === 'vi' ? 'Tiếng Việt' : 'Tiếng Anh / Khác'}</p>
                              <p className="mt-1 text-sm text-white/50">Thời lượng: {round.maxDurationMinutes} phút</p>
                            </div>
                          </div>
                        ))
                      ) : (
                        <div className="rounded-2xl border border-white/8 bg-white/[0.02] p-4 text-sm text-white/55">
                          Chưa có cấu hình vòng phỏng vấn chi tiết cho vị trí này.
                        </div>
                      )}
                    </div>
                  </div>

                  <div className="rounded-[28px] border border-accent-primary/15 bg-accent-primary/5 p-6">
                    <div className="mb-2 flex items-center gap-2 text-white">
                      <Calendar className="h-4 w-4 text-accent-primary" />
                      <p className="text-sm font-semibold">Lưu ý khi ứng tuyển</p>
                    </div>
                    <p className="text-sm leading-6 text-white/60">
                      Sau khi ứng tuyển, bạn có thể được mời tham gia phỏng vấn AI theo các khung giờ do nhà tuyển dụng cấu hình. Hãy theo dõi email và cổng ứng viên để không bỏ lỡ lịch hẹn.
                    </p>
                  </div>
                </div>
              </section>
            </motion.div>
          )}
        </div>
      </main>

      <CandidateFooter />
    </div>
  );
}
