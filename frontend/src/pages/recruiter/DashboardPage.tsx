import { motion } from 'framer-motion';
import { Users, FileText, Video, Plus, Clock, Edit3 } from 'lucide-react';
import { PageHeader, StatsGrid } from '@components/shared';
import { useAuthStore } from '@store/auth/authStore';

const stats = [
  { label: 'Tin của tôi', value: '5', color: 'text-amber-400' },
  { label: 'Chờ duyệt', value: '2', color: 'text-amber-400' },
  { label: 'Ứng viên', value: '12', color: 'text-blue-400' },
  { label: 'Phỏng vấn tuần này', value: '4', color: 'text-violet-400' },
];

const myJobs = [
  { id: '1', title: 'Senior Backend Developer', status: 'pending_approval', submittedAt: '2 giờ trước' },
  { id: '2', title: 'Frontend Developer', status: 'published', submittedAt: '1 ngày trước' },
  { id: '3', title: 'DevOps Engineer', status: 'draft', submittedAt: null },
];

const recentCandidates = [
  { id: '1', name: 'Nguyễn Văn A', job: 'Senior Backend', appliedAt: '2 giờ trước', status: 'new' },
  { id: '2', name: 'Trần Thị B', job: 'Frontend Developer', appliedAt: '4 giờ trước', status: 'interviewed' },
  { id: '3', name: 'Lê Hoàng C', job: 'DevOps Engineer', appliedAt: '1 ngày trước', status: 'new' },
];

const getStatusBadge = (status: string) => {
  if (status === 'published') return { label: 'Đã đăng', color: 'bg-emerald-500/20 text-emerald-400' };
  if (status === 'pending_approval') return { label: 'Chờ duyệt', color: 'bg-amber-500/20 text-amber-400' };
  if (status === 'draft') return { label: 'Bản nháp', color: 'bg-white/10 text-white/50' };
  if (status === 'rejected') return { label: 'Bị từ chối', color: 'bg-red-500/20 text-red-400' };
  return { label: status, color: 'bg-white/10 text-white/50' };
};

const getCandidateStatusBadge = (status: string) => {
  if (status === 'new') return { label: 'Mới', color: 'bg-blue-500/20 text-blue-400' };
  if (status === 'interviewed') return { label: 'Đã phỏng vấn', color: 'bg-violet-500/20 text-violet-400' };
  return { label: status, color: 'bg-white/10 text-white/50' };
};

export default function RecruiterDashboardPage() {
  const { user } = useAuthStore();

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title={`Xin chào, ${user?.name || 'Recruiter'}`}
        description="Tổng quan công việc tuyển dụng của bạn"
        actions={[
          { label: 'Tạo tin mới', href: '/recruiter/jobs/create', variant: 'primary' },
        ]}
      />

      <StatsGrid stats={stats} />

      <div className="grid lg:grid-cols-3 gap-8">
        {/* Main Content */}
        <div className="lg:col-span-2 space-y-8">
          {/* My Jobs */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.1 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden"
          >
            <div className="flex items-center justify-between mb-6">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-amber-500/20 flex items-center justify-center">
                  <FileText className="w-5 h-5 text-amber-400" />
                </div>
                <div>
                  <h2 className="text-base font-semibold text-white">Tin tuyển dụng của tôi</h2>
                  <p className="text-xs text-white/40">Quản lý tin đã tạo</p>
                </div>
              </div>
              <a href="/recruiter/my-jobs" className="text-xs text-amber-400 hover:text-amber-300 transition-colors">Xem tất cả</a>
            </div>

            <div className="space-y-3">
              {myJobs.map((job) => {
                const statusBadge = getStatusBadge(job.status);
                return (
                  <div key={job.id} className="flex items-center justify-between p-4 rounded-xl bg-white/[0.02] border border-white/5 hover:border-white/10 transition-colors">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-amber-500/30 to-orange/30 flex items-center justify-center">
                        <FileText className="w-5 h-5 text-amber-400" />
                      </div>
                      <div>
                        <h3 className="text-sm font-medium text-white">{job.title}</h3>
                        <span className="flex items-center gap-1 text-xs text-white/40">
                          {job.status === 'pending_approval' && <Clock className="w-3 h-3" />}
                          {job.submittedAt || 'Chưa gửi duyệt'}
                        </span>
                      </div>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className={`px-2.5 py-1 rounded-full text-xs font-medium ${statusBadge.color}`}>
                        {statusBadge.label}
                      </span>
                      <a 
                        href={`/recruiter/my-jobs/${job.id}`}
                        className="p-2 rounded-lg hover:bg-white/10 transition-colors"
                      >
                        <Edit3 className="w-4 h-4 text-white/50" />
                      </a>
                    </div>
                  </div>
                );
              })}
            </div>
          </motion.div>

          {/* Recent Candidates */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.2 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden"
          >
            <div className="flex items-center justify-between mb-6">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-blue-500/20 flex items-center justify-center">
                  <Users className="w-5 h-5 text-blue-400" />
                </div>
                <div>
                  <h2 className="text-base font-semibold text-white">Ứng viên gần đây</h2>
                  <p className="text-xs text-white/40">Những ứng viên ứng tuyển gần đây</p>
                </div>
              </div>
              <a href="/recruiter/candidates" className="text-xs text-accent-primary hover:text-accent-secondary transition-colors">Xem tất cả</a>
            </div>

            <div className="space-y-3">
              {recentCandidates.map((candidate) => {
                const statusBadge = getCandidateStatusBadge(candidate.status);
                return (
                  <div key={candidate.id} className="flex items-center justify-between p-4 rounded-xl bg-white/[0.02] border border-white/5 hover:border-white/10 transition-colors">
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 rounded-full bg-gradient-to-br from-blue-500/30 to-cyan/30 flex items-center justify-center text-xs font-medium text-white">
                        {candidate.name.split(' ').map((n) => n[0]).join('')}
                      </div>
                      <div>
                        <h3 className="text-sm font-medium text-white">{candidate.name}</h3>
                        <p className="text-xs text-white/40">{candidate.job} • {candidate.appliedAt}</p>
                      </div>
                    </div>
                    <span className={`px-2.5 py-1 rounded-full text-xs font-medium ${statusBadge.color}`}>
                      {statusBadge.label}
                    </span>
                  </div>
                );
              })}
            </div>
          </motion.div>
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          {/* Quick Actions */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.3 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-5 overflow-hidden"
          >
            <h2 className="text-sm font-semibold text-white mb-4">Thao tác nhanh</h2>
            <div className="space-y-2">
              <a href="/recruiter/jobs/create" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-amber-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-amber-500/20 flex items-center justify-center">
                  <Plus className="w-4 h-4 text-amber-400" />
                </div>
                <span className="text-sm text-white">Tạo tin mới</span>
              </a>
              <a href="/recruiter/my-jobs" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-blue-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-blue-500/20 flex items-center justify-center">
                  <FileText className="w-4 h-4 text-blue-400" />
                </div>
                <span className="text-sm text-white">Xem tin của tôi</span>
              </a>
              <a href="/recruiter/candidates" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-violet-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-violet-500/20 flex items-center justify-center">
                  <Users className="w-4 h-4 text-violet-400" />
                </div>
                <span className="text-sm text-white">Xem ứng viên</span>
              </a>
              <a href="/recruiter/interviews" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-emerald-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-emerald-500/20 flex items-center justify-center">
                  <Video className="w-4 h-4 text-emerald-400" />
                </div>
                <span className="text-sm text-white">Tạo lịch phỏng vấn</span>
              </a>
            </div>
          </motion.div>

          {/* Rejected Jobs with Feedback */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.4 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-red-500/20 p-5 overflow-hidden"
          >
            <h2 className="text-sm font-semibold text-white mb-4">Tin bị từ chối</h2>
            <div className="space-y-3">
              <div className="p-3 rounded-xl bg-red-500/10 border border-red-500/20">
                <p className="text-sm text-white mb-1">Senior Backend Developer</p>
                <p className="text-xs text-white/50">Lý do: Thông tin lương chưa đầy đủ</p>
                <a href="/recruiter/my-jobs/1" className="text-xs text-amber-400 hover:text-amber-300 mt-2 inline-block">
                  Chỉnh sửa và gửi lại
                </a>
              </div>
            </div>
          </motion.div>
        </div>
      </div>
    </div>
  );
}
