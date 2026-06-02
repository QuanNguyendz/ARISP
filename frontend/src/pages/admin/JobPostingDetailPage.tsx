import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { MapPin, DollarSign, Clock, Calendar, ArrowLeft, Edit2, Loader2, Sparkles, Languages, Hourglass, ShieldAlert } from 'lucide-react';
import jobService from '@/services/job/jobService';
import type { JobPosting } from '@/types/job';

export default function JobPostingDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [job, setJob] = useState<JobPosting | null>(null);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    async function fetchJobDetail() {
      if (!id) return;
      try {
        setLoading(true);
        const data = await jobService.getJobPostingById(id);
        setJob(data);
      } catch (err: any) {
        console.error(err);
        setError('Không thể tải chi tiết tin tuyển dụng. Tin có thể không tồn tại hoặc bạn không có quyền xem.');
      } finally {
        setLoading(false);
      }
    }
    fetchJobDetail();
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
    if (!mode) return 'Chưa cấu hình';
    const mappings: Record<string, string> = {
      fulltime: 'Toàn thời gian (Full-time)',
      parttime: 'Bán thời gian (Part-time)',
      contract: 'Hợp đồng (Contract)',
      internship: 'Thực tập (Internship)',
    };
    return mappings[mode.toLowerCase()] || mode;
  }

  if (loading) {
    return (
      <div className="flex flex-col items-center justify-center min-h-[50vh] gap-3">
        <Loader2 className="w-10 h-10 text-accent-primary animate-spin" />
        <p className="text-sm text-white/40">Đang tải chi tiết tin tuyển dụng...</p>
      </div>
    );
  }

  if (error || !job) {
    return (
      <div className="p-6 lg:p-8 max-w-4xl mx-auto text-center py-20">
        <ShieldAlert className="w-16 h-16 text-red-400 mx-auto mb-4" />
        <h2 className="text-2xl font-bold text-white mb-2">Đã xảy ra lỗi</h2>
        <p className="text-text-secondary mb-6">{error || 'Không tìm thấy dữ liệu.'}</p>
        <button
          onClick={() => navigate('/admin/jobs')}
          className="inline-flex items-center gap-2 px-6 py-3 rounded-xl bg-white/5 border border-white/10 text-white font-medium hover:bg-white/10 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" />
          Quay lại danh sách
        </button>
      </div>
    );
  }

  return (
    <div className="p-6 lg:p-8">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
        <button
          onClick={() => navigate('/admin/jobs')}
          className="inline-flex items-center gap-2 text-sm text-white/50 hover:text-white mb-6 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" />
          Quay lại danh sách
        </button>
        
        <div className="flex flex-col md:flex-row md:items-start justify-between gap-6 mb-8">
          <div className="flex items-start gap-6">
            <div className="w-20 h-20 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-2xl font-bold text-white">
              {job.title.substring(0, 2).toUpperCase()}
            </div>
            <div>
              <div className="flex items-center gap-3 mb-2">
                <h1 className="text-3xl font-semibold text-white">{job.title}</h1>
                <span className={`px-3 py-1 rounded-full text-xs font-semibold ${
                  job.status === 'active' ? 'bg-emerald-500/20 text-emerald-400' :
                  job.status === 'paused' ? 'bg-amber-500/20 text-amber-400' :
                  job.status === 'draft' ? 'bg-blue-500/20 text-blue-400' :
                  'bg-white/10 text-white/40'
                }`}>
                  {job.status === 'active' ? 'Active' : 
                   job.status === 'paused' ? 'Tạm dừng' : 
                   job.status === 'draft' ? 'Bản nháp' : 'Đã đóng'}
                </span>
              </div>
              <p className="text-xl text-text-secondary mb-4">{job.department || 'Phòng ban tuyển dụng'}</p>
              <div className="flex flex-wrap items-center gap-x-6 gap-y-2 text-sm text-text-secondary">
                <span className="flex items-center gap-1.5"><MapPin className="w-4 h-4 text-accent-primary" />{job.location || 'Chưa cấu hình'}</span>
                <span className="flex items-center gap-1.5"><DollarSign className="w-4 h-4 text-accent-primary" />{formatSalary(job)}</span>
                <span className="flex items-center gap-1.5"><Clock className="w-4 h-4 text-accent-primary" />{formatWorkMode(job.workMode || job.employmentType)}</span>
                <span className="flex items-center gap-1.5">
                  <Calendar className="w-4 h-4 text-accent-primary" />
                  Hạn nộp: {job.applicationDeadline ? new Date(job.applicationDeadline).toLocaleDateString('vi-VN') : 'Không giới hạn'}
                </span>
              </div>
            </div>
          </div>
          <button className="flex items-center justify-center gap-2 px-6 py-3 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity">
            <Edit2 className="w-4 h-4" />
            Chỉnh sửa
          </button>
        </div>

        <div className="grid lg:grid-cols-3 gap-6">
          {/* Main Info */}
          <div className="lg:col-span-2 space-y-6">
            {/* Description */}
            <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6">
              <h2 className="text-xl font-semibold text-white mb-4">Mô tả công việc</h2>
              <div className="text-text-secondary leading-relaxed whitespace-pre-line">{job.jobDescription}</div>
            </motion.div>

            {/* Skills */}
            {job.skills && job.skills.length > 0 && (
              <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.15 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6">
                <h2 className="text-xl font-semibold text-white mb-4">Yêu cầu kỹ năng</h2>
                <div className="flex flex-wrap gap-2">
                  {job.skills.map((skill) => (
                    <span key={skill} className="px-3 py-1.5 rounded-xl bg-white/5 border border-white/10 text-white text-sm">
                      {skill}
                    </span>
                  ))}
                </div>
              </motion.div>
            )}

            {/* Persona and Rubric Settings */}
            <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.2 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6">
              <h2 className="text-xl font-semibold text-white mb-4 flex items-center gap-2">
                <Sparkles className="w-5 h-5 text-accent-primary" />
                Cấu hình AI Interviewer Persona
              </h2>
              <div className="grid md:grid-cols-2 gap-6 text-sm">
                <div className="space-y-3">
                  <p className="text-text-secondary"><strong className="text-white">Ngôn ngữ yêu cầu:</strong> {job.languageRequirement || 'Mặc định theo hệ thống'}</p>
                  <p className="text-text-secondary"><strong className="text-white">Ngôn ngữ tự động phát hiện:</strong> {job.detectedLanguage === 'vi' ? 'Tiếng Việt' : 'Tiếng Anh/Khác'}</p>
                </div>
                <div className="space-y-3">
                  <p className="text-text-secondary"><strong className="text-white">Giờ phỏng vấn khả dụng:</strong> Bắt buộc On-site (`interview_mode: onsite`)</p>
                  <p className="text-text-secondary"><strong className="text-white">Hạn đổi lịch phỏng vấn:</strong> {job.rescheduleDeadlineHours || 24} giờ trước buổi hẹn</p>
                </div>
              </div>
            </motion.div>
          </div>

          {/* Sidebar Round Configs */}
          <div className="space-y-6">
            <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.25 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6">
              <h2 className="text-xl font-semibold text-white mb-4">Các vòng phỏng vấn AI ({job.roundConfigs?.length || 0})</h2>
              {job.roundConfigs && job.roundConfigs.length > 0 ? (
                <div className="space-y-4">
                  {job.roundConfigs.map((round) => (
                    <div key={round.roundNumber} className="p-4 rounded-xl bg-white/[0.02] border border-white/5 space-y-2">
                      <div className="flex items-center justify-between">
                        <span className="text-sm font-semibold text-white">Vòng {round.roundNumber}</span>
                        <span className={`px-2 py-0.5 rounded-full text-xs font-semibold ${
                          round.roundType === 'technical' ? 'bg-violet/20 text-violet-400' : 'bg-blue-500/20 text-blue-400'
                        }`}>
                          {round.roundType === 'technical' ? 'Technical' : 'Screening'}
                        </span>
                      </div>
                      <div className="space-y-1.5 text-xs text-text-secondary">
                        <p className="flex items-center gap-1.5"><Languages className="w-3.5 h-3.5 text-accent-primary" />Ngôn ngữ: {round.interviewLanguage === 'vi' ? 'Tiếng Việt' : 'Tiếng Anh/Khác'}</p>
                        <p className="flex items-center gap-1.5"><Hourglass className="w-3.5 h-3.5 text-accent-primary" />Thời gian: {round.maxDurationMinutes} phút</p>
                        <p className="flex items-center gap-1.5"><Clock className="w-3.5 h-3.5 text-accent-primary" />TTL Code: {round.interviewCodeTtlHours} giờ</p>
                      </div>
                    </div>
                  ))}
                </div>
              ) : (
                <p className="text-sm text-text-tertiary">Chưa cấu hình các vòng phỏng vấn.</p>
              )}
            </motion.div>
          </div>
        </div>
      </motion.div>
    </div>
  );
}
