import { motion } from 'framer-motion';
import { Users, BarChart3, Plus, Bell } from 'lucide-react';

const stats = [
  { label: 'Ứng viên đang chờ', value: 12, change: '+3', color: 'text-blue-400' },
  { label: 'Phỏng vấn hôm nay', value: 5, change: '+2', color: 'text-violet-400' },
  { label: 'Chờ xác nhận', value: 8, change: '-2', color: 'text-amber-400' },
  { label: 'Đã tuyển tháng này', value: 23, change: '+15%', color: 'text-emerald-400' },
];

const recentEvaluations = [
  {
    id: '1',
    candidateName: 'Nguyễn Văn An',
    role: 'Senior Backend Developer',
    overallScore: 87,
    date: '19/05/2026',
    metrics: [
      { label: 'Kỹ thuật', score: 92, trend: 'up' },
      { label: 'Giao tiếp', score: 85, trend: 'stable' },
    ],
  },
  {
    id: '2',
    candidateName: 'Trần Thị Minh',
    role: 'Product Designer',
    overallScore: 91,
    date: '19/05/2026',
    metrics: [
      { label: 'Kỹ thuật', score: 88, trend: 'up' },
      { label: 'Giao tiếp', score: 94, trend: 'up' },
    ],
  },
  {
    id: '3',
    candidateName: 'Lê Hoàng Nam',
    role: 'Data Scientist',
    overallScore: 79,
    date: '18/05/2026',
    metrics: [
      { label: 'Kỹ thuật', score: 82, trend: 'stable' },
      { label: 'Giao tiếp', score: 75, trend: 'down' },
    ],
  },
];

const pendingReviews = [
  { id: '1', candidate: 'Phạm Thu Hà', position: 'Frontend Developer', round: 1, timeAgo: '2 giờ trước' },
  { id: '2', candidate: 'Đỗ Minh Tuấn', position: 'DevOps Engineer', round: 2, timeAgo: '4 giờ trước' },
  { id: '3', candidate: 'Vũ Thị Lan', position: 'QA Engineer', round: 1, timeAgo: '1 ngày trước' },
];

export default function DashboardPage() {
  return (
    <div className="p-6 lg:p-8">
      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="flex items-center justify-between mb-8"
      >
        <div>
          <h1 className="text-2xl font-semibold text-white mb-1">Xin chào, Minh Anh</h1>
          <p className="text-sm text-white/40">Tổng quan hoạt động tuyển dụng</p>
        </div>
        <div className="flex items-center gap-3">
          <button className="p-3 rounded-xl bg-white/5 border border-white/10 text-white/60 hover:text-white transition-colors">
            <Bell className="w-5 h-5" />
          </button>
          <a
            href="/admin/jobs/create"
            className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white text-sm font-medium hover:opacity-90 transition-opacity"
          >
            <Plus className="w-4 h-4" />
            Tạo tin mới
          </a>
        </div>
      </motion.div>

      {/* Stats Grid */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
        {stats.map((stat) => (
          <motion.div
            key={stat.label}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-5 overflow-hidden group hover:bg-white/[0.05] transition-colors"
          >
            <div className="flex items-center justify-between mb-3">
              <span className={`text-sm font-medium ${stat.color}`}>{stat.label}</span>
              <span className="text-xs text-emerald-400 font-medium">{stat.change}</span>
            </div>
            <p className="text-2xl font-semibold text-white mb-1">{stat.value}</p>
          </motion.div>
        ))}
      </div>

      <div className="grid lg:grid-cols-3 gap-8">
        {/* Main Content */}
        <div className="lg:col-span-2 space-y-8">
          {/* Pending Reviews */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.1 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden"
          >
            <div className="flex items-center justify-between mb-6">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-amber-500/20 flex items-center justify-center">
                  <BarChart3 className="w-5 h-5 text-amber-400" />
                </div>
                <div>
                  <h2 className="text-base font-semibold text-white">Chờ xác nhận</h2>
                  <p className="text-xs text-white/40">Cần HR xem xét</p>
                </div>
              </div>
              <a href="/admin/evaluations" className="text-xs text-accent-primary hover:text-accent-secondary transition-colors">Xem tất cả</a>
            </div>

            <div className="space-y-3">
              {pendingReviews.map((review) => (
                <div key={review.id} className="flex items-center justify-between p-4 rounded-xl bg-white/[0.02] border border-white/5 hover:border-white/10 transition-colors">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-full bg-gradient-to-br from-accent-primary/30 to-violet/30 flex items-center justify-center text-xs font-medium text-white">
                      {review.candidate.split(' ').map((n: string) => n[0]).join('')}
                    </div>
                    <div>
                      <h3 className="text-sm font-medium text-white">{review.candidate}</h3>
                      <p className="text-xs text-white/40">{review.position} • Vòng {review.round}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-4">
                    <span className="text-xs text-white/30">{review.timeAgo}</span>
                    <a href="/admin/evaluations" className="px-3 py-1.5 rounded-lg bg-accent-primary/20 text-accent-primary text-xs font-medium hover:bg-accent-primary/30 transition-colors">Xem</a>
                  </div>
                </div>
              ))}
            </div>
          </motion.div>

          {/* Recent Evaluations */}
          <div>
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-base font-semibold text-white">Đánh giá gần đây</h2>
              <a href="/admin/evaluations" className="text-xs text-accent-primary hover:text-accent-secondary transition-colors">Xem tất cả</a>
            </div>
            <div className="grid md:grid-cols-2 gap-4">
              {recentEvaluations.map((evaluation, index) => (
                <motion.div
                  key={evaluation.candidateName}
                  initial={{ opacity: 0, y: 20 }}
                  animate={{ opacity: 1, y: 0 }}
                  transition={{ delay: 0.1 + index * 0.05 }}
                  className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-5 overflow-hidden group hover:bg-white/[0.05] transition-colors"
                >
                  <div className="flex items-start justify-between mb-4">
                    <div>
                      <h3 className="text-sm font-semibold text-white mb-0.5">{evaluation.candidateName}</h3>
                      <p className="text-xs text-white/40">{evaluation.role}</p>
                    </div>
                    <div className="text-2xl font-semibold bg-gradient-to-r from-accent-primary to-violet bg-clip-text text-transparent">
                      {evaluation.overallScore}
                    </div>
                  </div>
                  <div className="space-y-2">
                    {evaluation.metrics.slice(0, 2).map((metric) => (
                      <div key={metric.label} className="flex items-center justify-between text-xs">
                        <span className="text-white/40">{metric.label}</span>
                        <span className="text-white/60 font-medium">{metric.score}%</span>
                      </div>
                    ))}
                  </div>
                  <div className="flex items-center justify-between mt-4 pt-3 border-t border-white/5">
                    <span className="text-xs text-white/30">{evaluation.date}</span>
                    <a href="/admin/evaluations" className="text-xs text-accent-primary hover:text-accent-secondary transition-colors">Chi tiết →</a>
                  </div>
                </motion.div>
              ))}
            </div>
          </div>
        </div>

        {/* Sidebar */}
        <div className="space-y-6">
          {/* Quick Actions */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.2 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-5 overflow-hidden"
          >
            <h2 className="text-sm font-semibold text-white mb-4">Thao tác nhanh</h2>
            <div className="space-y-2">
              <a href="/admin/jobs/create" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-accent-primary/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-accent-primary/20 flex items-center justify-center">
                  <Plus className="w-4 h-4 text-accent-primary" />
                </div>
                <span className="text-sm text-white">Tạo tin tuyển dụng</span>
              </a>
              <a href="/admin/reports" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-violet/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-violet/20 flex items-center justify-center">
                  <BarChart3 className="w-4 h-4 text-violet" />
                </div>
                <span className="text-sm text-white">Xem báo cáo</span>
              </a>
              <a href="/kiosk" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-emerald-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-emerald-500/20 flex items-center justify-center">
                  <Users className="w-4 h-4 text-emerald-500" />
                </div>
                <span className="text-sm text-white">Mời ứng viên</span>
              </a>
            </div>
          </motion.div>

          {/* Performance Overview */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.3 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-5 overflow-hidden"
          >
            <h2 className="text-sm font-semibold text-white mb-4">Hiệu suất tháng 5</h2>
            <div className="space-y-4">
              <div>
                <div className="flex items-center justify-between mb-2">
                  <span className="text-xs text-white/40">Tỷ lệ Pass</span>
                  <span className="text-xs text-white font-medium">68%</span>
                </div>
                <div className="h-1.5 rounded-full bg-white/10 overflow-hidden">
                  <div className="h-full rounded-full bg-gradient-to-r from-accent-primary to-violet w-[68%]" />
                </div>
              </div>
              <div>
                <div className="flex items-center justify-between mb-2">
                  <span className="text-xs text-white/40">Thời gian tuyển</span>
                  <span className="text-xs text-white font-medium">4.2 ngày</span>
                </div>
                <div className="h-1.5 rounded-full bg-white/10 overflow-hidden">
                  <div className="h-full rounded-full bg-gradient-to-r from-emerald-500 to-teal-500 w-[75%]" />
                </div>
              </div>
            </div>
          </motion.div>
        </div>
      </div>
    </div>
  );
}
