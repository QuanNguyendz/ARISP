import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import CandidateNav from '@components/layout/CandidateNav';
import CandidateFooter from '@components/layout/CandidateFooter';
import GlassCard from '@components/ui/GlassCard';
import { MapPin, DollarSign, Clock, Calendar, ArrowLeft, Loader2, Send, Languages, ShieldAlert } from 'lucide-react';
import jobService from '@/services/job/jobService';
import type { JobPosting } from '@/types/job';

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
      } catch (err: any) {
        console.error(err);
        setError('Không tìm thấy tin tuyển dụng này hoặc tin tuyển dụng đã ngừng nhận hồ sơ.');
      } finally {
        setLoading(false);
      }
    }
    loadJobDetail();
  }, [id]);

  function formatSalary(j: JobPosting): string {
    if (j.salaryIsNegotiable) return 'Thỏa thuận';
    if (j.salaryMin && j.salaryMax) {
      return `$${j.salaryMin.toLocaleString()} - $${j.salaryMax.toLocaleString()}`;
    }
    if (j.salaryMin) return `Từ $${j.salaryMin.toLocaleString()}`;
    if (j.salaryMax) return `Đến $${j.salaryMax.toLocaleString()}`;
    return 'Thỏa thuận';
  }

  function formatWorkMode(mode?: string): string {
    if (!mode) return 'Full-time';
    const mappings: Record<string, string> = {
      fulltime: 'Toàn thời gian (Full-time)',
      parttime: 'Bán thời gian (Part-time)',
      contract: 'Hợp đồng (Contract)',
      internship: 'Thực tập (Internship)',
    };
    return mappings[mode.toLowerCase()] || mode;
  }

  return (
    <div className="min-h-screen bg-bg-primary flex flex-col">
      <CandidateNav />

      <div className="flex-1 pt-24 pb-12 px-6 lg:px-8 max-w-5xl mx-auto w-full">
        {loading ? (
          <div className="flex flex-col items-center justify-center min-h-[50vh] gap-3">
            <Loader2 className="w-10 h-10 text-accent-primary animate-spin" />
            <p className="text-sm text-text-secondary">Đang tải thông tin chi tiết...</p>
          </div>
        ) : error || !job ? (
          <div className="text-center py-20">
            <ShieldAlert className="w-16 h-16 text-red-400 mx-auto mb-4" />
            <h2 className="text-2xl font-bold text-white mb-2">Đã xảy ra lỗi</h2>
            <p className="text-text-secondary mb-6">{error || 'Không tìm thấy dữ liệu.'}</p>
            <button
              onClick={() => navigate('/jobs')}
              className="inline-flex items-center gap-2 px-6 py-3 rounded-xl bg-white/5 border border-white/10 text-white font-medium hover:bg-white/10 transition-colors"
            >
              <ArrowLeft className="w-4 h-4" />
              Xem việc làm khác
            </button>
          </div>
        ) : (
          <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
            {/* Back button */}
            <button
              onClick={() => navigate('/jobs')}
              className="inline-flex items-center gap-2 text-sm text-text-secondary hover:text-white mb-6 transition-colors"
            >
              <ArrowLeft className="w-4 h-4" />
              Quay lại danh sách việc làm
            </button>

            {/* Job Header */}
            <GlassCard className="p-6 md:p-8 mb-6" hoverEnabled={false}>
              <div className="flex flex-col md:flex-row md:items-start justify-between gap-6">
                <div className="flex items-start gap-6">
                  <div className="w-20 h-20 rounded-2xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-2xl font-bold text-white shadow-lg shadow-accent-primary/20">
                    {job.title.substring(0, 2).toUpperCase()}
                  </div>
                  <div>
                    <div className="flex flex-wrap items-center gap-2 mb-2">
                      <h1 className="text-2xl md:text-3xl font-bold text-white">{job.title}</h1>
                      {job.isUrgent && (
                        <span className="px-2.5 py-0.5 rounded-full bg-red-500/20 text-red-400 text-xs font-semibold">
                          Tuyển gấp
                        </span>
                      )}
                    </div>
                    <p className="text-lg text-text-secondary mb-4">{job.department || 'Phòng ban tuyển dụng'}</p>
                    
                    <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-sm text-text-secondary">
                      <span className="flex items-center gap-1.5">
                        <MapPin className="w-4.5 h-4.5 text-accent-primary" />
                        {job.location || 'Remote'}
                      </span>
                      <span className="flex items-center gap-1.5">
                        <DollarSign className="w-4.5 h-4.5 text-accent-primary" />
                        {formatSalary(job)}
                      </span>
                      <span className="flex items-center gap-1.5">
                        <Clock className="w-4.5 h-4.5 text-accent-primary" />
                        {formatWorkMode(job.workMode || job.employmentType)}
                      </span>
                    </div>
                  </div>
                </div>

                <button
                  onClick={() => navigate(`/candidate/applications/${job.id}`)}
                  className="w-full md:w-auto px-8 py-4 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-semibold hover:opacity-90 transition-opacity flex items-center justify-center gap-2 shadow-lg shadow-accent-primary/25"
                >
                  <Send className="w-4 h-4" />
                  Nộp đơn ứng tuyển
                </button>
              </div>
            </GlassCard>

            {/* Details Grid */}
            <div className="grid lg:grid-cols-3 gap-6">
              {/* Job Description (Left) */}
              <div className="lg:col-span-2 space-y-6">
                <GlassCard className="p-6 md:p-8" hoverEnabled={false}>
                  <h2 className="text-xl font-bold text-white mb-4 border-b border-white/5 pb-2">Mô tả công việc</h2>
                  <div className="text-text-secondary leading-relaxed whitespace-pre-line text-sm md:text-base">
                    {job.jobDescription}
                  </div>
                </GlassCard>

                {job.skills && job.skills.length > 0 && (
                  <GlassCard className="p-6 md:p-8" hoverEnabled={false}>
                    <h2 className="text-xl font-bold text-white mb-4 border-b border-white/5 pb-2">Yêu cầu kỹ năng</h2>
                    <div className="flex flex-wrap gap-2">
                      {job.skills.map((skill) => (
                        <span key={skill} className="px-3.5 py-2 rounded-xl bg-white/5 border border-white/10 text-white text-sm font-medium">
                          {skill}
                        </span>
                      ))}
                    </div>
                  </GlassCard>
                )}
              </div>

              {/* Sidebar Info (Right) */}
              <div className="space-y-6">
                <GlassCard className="p-6" hoverEnabled={false}>
                  <h3 className="text-lg font-bold text-white mb-4 border-b border-white/5 pb-2">Tổng quan vị trí</h3>
                  
                  <div className="space-y-4 text-sm text-text-secondary">
                    <div>
                      <span className="block text-xs text-text-tertiary">Hình thức phỏng vấn</span>
                      <span className="text-white font-medium capitalize">{job.interviewMode === 'both' ? 'Hybrid (Online & Trực tiếp)' : job.interviewMode}</span>
                    </div>

                    <div>
                      <span className="block text-xs text-text-tertiary">Yêu cầu kinh nghiệm</span>
                      <span className="text-white font-medium capitalize">{job.experienceLevel || 'Không yêu cầu'}</span>
                    </div>

                    <div>
                      <span className="block text-xs text-text-tertiary">Ngôn ngữ phỏng vấn chính</span>
                      <span className="text-white font-medium flex items-center gap-1.5 mt-1">
                        <Languages className="w-4 h-4 text-accent-primary" />
                        {job.languageRequirement || 'Tiếng Việt'}
                      </span>
                    </div>

                    <div>
                      <span className="block text-xs text-text-tertiary">Hạn chót ứng tuyển</span>
                      <span className="text-white font-medium flex items-center gap-1.5 mt-1">
                        <Calendar className="w-4 h-4 text-accent-primary" />
                        {job.applicationDeadline ? new Date(job.applicationDeadline).toLocaleDateString('vi-VN') : 'Không giới hạn'}
                      </span>
                    </div>
                  </div>
                </GlassCard>

                <GlassCard className="p-6" hoverEnabled={false}>
                  <h3 className="text-lg font-bold text-white mb-4 border-b border-white/5 pb-2">Quy trình phỏng vấn AI</h3>
                  <div className="space-y-4">
                    {job.roundConfigs && job.roundConfigs.length > 0 ? (
                      job.roundConfigs.map((round) => (
                        <div key={round.roundNumber} className="flex gap-3">
                          <div className="w-8 h-8 rounded-full bg-accent-primary/20 text-accent-primary text-sm font-semibold flex items-center justify-center shrink-0">
                            {round.roundNumber}
                          </div>
                          <div>
                            <p className="text-sm font-medium text-white capitalize">{round.roundType} Round</p>
                            <p className="text-xs text-text-tertiary">Thời lượng: {round.maxDurationMinutes} phút</p>
                          </div>
                        </div>
                      ))
                    ) : (
                      <div className="flex gap-3">
                        <div className="w-8 h-8 rounded-full bg-accent-primary/20 text-accent-primary text-sm font-semibold flex items-center justify-center shrink-0">
                          1
                        </div>
                        <div>
                          <p className="text-sm font-medium text-white">AI Interview Round</p>
                          <p className="text-xs text-text-tertiary">Thời lượng: 30 phút</p>
                        </div>
                      </div>
                    )}
                  </div>
                </GlassCard>
              </div>
            </div>
          </motion.div>
        )}
      </div>

      <CandidateFooter />
    </div>
  );
}
