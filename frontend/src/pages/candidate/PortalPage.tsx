import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Video, FileText, MessageSquare, ArrowRight, Play, Clock, Calendar } from 'lucide-react';
import { useAuthStore } from '@store/auth';

interface Application {
  id: string;
  jobTitle: string;
  company: string;
  status: 'pending' | 'interview_scheduled' | 'interviewing' | 'review' | 'pass' | 'fail';
  appliedAt: string;
  nextStep?: string;
  interviewDate?: string;
}

const applications: Application[] = [
  {
    id: '1',
    jobTitle: 'Senior Backend Developer',
    company: 'TechVietnam Corp',
    status: 'interview_scheduled',
    appliedAt: '15/05/2026',
    nextStep: 'Phỏng vấn vòng 1',
    interviewDate: '22/05/2026 - 14:00',
  },
  {
    id: '2',
    jobTitle: 'Product Designer',
    company: 'DesignHub VN',
    status: 'review',
    appliedAt: '12/05/2026',
    nextStep: 'Chờ kết quả vòng 2',
  },
  {
    id: '3',
    jobTitle: 'Frontend Developer',
    company: 'WebSolutions',
    status: 'fail',
    appliedAt: '05/05/2026',
    nextStep: 'Không đạt yêu cầu',
  },
];

const statusConfig: Record<string, { label: string; color: string; bg: string }> = {
  pending: { label: 'Đang chờ', color: 'text-white/70', bg: 'bg-white/10' },
  interview_scheduled: { label: 'Đã lên lịch', color: 'text-accent-primary', bg: 'bg-accent-primary/20' },
  interviewing: { label: 'Đang phỏng vấn', color: 'text-violet', bg: 'bg-violet/20' },
  review: { label: 'Đang xem xét', color: 'text-amber-400', bg: 'bg-amber-500/20' },
  pass: { label: 'Đạt', color: 'text-emerald-400', bg: 'bg-emerald-500/20' },
  fail: { label: 'Không đạt', color: 'text-red-400', bg: 'bg-red-500/20' },
};

export default function PortalPage() {
  const navigate = useNavigate();
  const user = useAuthStore((state) => state.user);

  return (
    <div className="min-h-screen bg-bg-primary">
      {/* Header */}
      <div className="border-b border-white/5">
        <div className="max-w-4xl mx-auto px-4 md:px-6 py-6 md:py-8">
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="flex items-center gap-4"
          >
            <div className="w-14 h-14 rounded-full bg-gradient-to-br from-accent-primary/30 to-violet/30 flex items-center justify-center text-lg font-medium text-white">
              {user?.name?.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 3) || 'UV'}
            </div>
            <div>
              <h1 className="text-2xl font-semibold text-white">Xin chào, {user?.name || 'Ứng viên'}</h1>
              <p className="text-sm text-white/50">Cổng thông tin ứng viên</p>
            </div>
          </motion.div>
        </div>
      </div>

      <div className="max-w-4xl mx-auto px-4 md:px-6 py-6 md:py-8 space-y-8">
        {/* Quick Actions */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
        >
          <h2 className="text-lg font-semibold text-white mb-4">Thao tác nhanh</h2>
          <div className="grid sm:grid-cols-3 gap-4">
            <button
              onClick={() => navigate('/interview/room/demo-session')}
              className="p-5 rounded-2xl bg-gradient-to-br from-accent-primary/20 to-violet/20 border border-accent-primary/20 hover:border-accent-primary/40 transition-colors text-left group"
            >
              <Video className="w-6 h-6 text-accent-primary mb-3" />
              <div className="text-white font-medium mb-1">Phòng phỏng vấn</div>
              <div className="text-sm text-white/50">Truy cập buổi phỏng vấn</div>
              <ArrowRight className="w-4 h-4 text-white/30 mt-3 group-hover:text-accent-primary group-hover:translate-x-1 transition-all" />
            </button>

            <button className="p-5 rounded-2xl bg-white/5 border border-white/10 hover:border-white/20 transition-colors text-left group">
              <FileText className="w-6 h-6 text-white/50 mb-3" />
              <div className="text-white font-medium mb-1">Hồ sơ của tôi</div>
              <div className="text-sm text-white/50">Xem và cập nhật thông tin</div>
              <ArrowRight className="w-4 h-4 text-white/30 mt-3 group-hover:text-white transition-all" />
            </button>

            <button className="p-5 rounded-2xl bg-white/5 border border-white/10 hover:border-white/20 transition-colors text-left group">
              <MessageSquare className="w-6 h-6 text-white/50 mb-3" />
              <div className="text-white font-medium mb-1">Hỗ trợ</div>
              <div className="text-sm text-white/50">Liên hệ đội ngũ hỗ trợ</div>
              <ArrowRight className="w-4 h-4 text-white/30 mt-3 group-hover:text-white transition-all" />
            </button>
          </div>
        </motion.div>

        {/* Applications */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
        >
          <h2 className="text-lg font-semibold text-white mb-4">Đơn ứng tuyển của tôi</h2>
          <div className="space-y-4">
            {applications.map((app) => {
              const status = statusConfig[app.status];

              return (
                <div key={app.id} className="p-6 rounded-2xl bg-white/5 border border-white/10">
                  <div className="flex items-start justify-between mb-4">
                    <div>
                      <h3 className="text-lg font-semibold text-white mb-1">{app.jobTitle}</h3>
                      <p className="text-sm text-white/50">{app.company}</p>
                    </div>
                    <span className={`px-3 py-1 rounded-full text-xs font-medium ${status?.bg || 'bg-white/10'} ${status?.color || 'text-white'}`}>
                      {status?.label || app.status}
                    </span>
                  </div>

                  <div className="flex flex-wrap items-center gap-4 mb-4 text-sm text-white/50">
                    <span className="flex items-center gap-1.5">
                      <Calendar className="w-4 h-4" />
                      Ứng tuyển: {app.appliedAt}
                    </span>
                    {app.interviewDate && (
                      <span className="flex items-center gap-1.5 text-accent-primary">
                        <Clock className="w-4 h-4" />
                        {app.interviewDate}
                      </span>
                    )}
                  </div>

                  {app.nextStep && (
                    <div className="p-4 rounded-xl bg-white/[0.02] border border-white/5">
                      <div className="text-xs text-white/40 mb-1">Bước tiếp theo</div>
                      <div className="text-sm text-white font-medium">{app.nextStep}</div>
                    </div>
                  )}

                  <div className="flex items-center gap-3 mt-4">
                    {app.status === 'interview_scheduled' && (
                      <button
                        onClick={() => navigate('/interview/room/demo-session')}
                        className="px-4 py-2 rounded-lg bg-gradient-to-r from-accent-primary to-violet text-white text-sm font-medium flex items-center gap-2 hover:opacity-90 transition-opacity"
                      >
                        <Play className="w-4 h-4" />
                        Bắt đầu phỏng vấn
                      </button>
                    )}
                    <button className="px-4 py-2 rounded-lg bg-white/5 text-white/50 text-sm font-medium hover:bg-white/10 transition-colors">
                      Xem chi tiết
                    </button>
                    {app.status === 'review' && (
                      <button
                        onClick={() => navigate('/candidate/feedback')}
                        className="px-4 py-2 rounded-lg bg-white/5 text-white/50 text-sm font-medium hover:bg-white/10 transition-colors"
                      >
                        Xem kết quả
                      </button>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </motion.div>

        {/* Tips */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.3 }}
        >
          <h2 className="text-lg font-semibold text-white mb-4">Mẹo cho bạn</h2>
          <div className="grid sm:grid-cols-2 gap-4">
            <div className="p-5 rounded-2xl bg-white/5 border border-white/10">
              <div className="flex items-start gap-4">
                <div className="w-10 h-10 rounded-xl bg-accent-primary/20 flex items-center justify-center flex-shrink-0">
                  <Video className="w-5 h-5 text-accent-primary" />
                </div>
                <div>
                  <h3 className="font-medium text-white mb-1">Chuẩn bị phỏng vấn</h3>
                  <p className="text-sm text-white/50">Đảm bảo kết nối internet ổn định và môi trường yên tĩnh</p>
                </div>
              </div>
            </div>

            <div className="p-5 rounded-2xl bg-white/5 border border-white/10">
              <div className="flex items-start gap-4">
                <div className="w-10 h-10 rounded-xl bg-violet/20 flex items-center justify-center flex-shrink-0">
                  <FileText className="w-5 h-5 text-violet" />
                </div>
                <div>
                  <h3 className="font-medium text-white mb-1">Cập nhật hồ sơ</h3>
                  <p className="text-sm text-white/50">Hoàn thiện CV để tăng cơ hội được chọn</p>
                </div>
              </div>
            </div>
          </div>
        </motion.div>
      </div>
    </div>
  );
}
