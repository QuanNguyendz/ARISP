import { motion } from 'framer-motion';

const evaluations = [
  { id: '1', candidateName: 'Nguyễn Văn An', position: 'Senior Backend Developer', overallScore: 87, status: 'completed', date: '19/05/2026' },
  { id: '2', candidateName: 'Trần Thị Minh', position: 'Product Designer', overallScore: 91, status: 'completed', date: '19/05/2026' },
  { id: '3', candidateName: 'Lê Hoàng Nam', position: 'Data Scientist', overallScore: 79, status: 'completed', date: '18/05/2026' },
  { id: '4', candidateName: 'Phạm Thu Hà', position: 'Frontend Developer', overallScore: 0, status: 'pending', date: '20/05/2026' },
];

export default function EvaluationReviewPage() {
  return (
    <div className="p-6 lg:p-8">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-8">
        <h1 className="text-3xl font-semibold text-white mb-2">Đánh giá</h1>
        <p className="text-text-secondary">Xem và quản lý kết quả đánh giá ứng viên</p>
      </motion.div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
        {[
          { label: 'Tổng đánh giá', value: '156', color: 'text-blue-400' },
          { label: 'Hoàn thành', value: '124', color: 'text-emerald-400' },
          { label: 'Đang chờ', value: '32', color: 'text-amber-400' },
          { label: 'Đạt (>80)', value: '89', color: 'text-violet-400' },
        ].map((stat) => (
          <motion.div key={stat.label} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-4">
            <p className={`text-2xl font-semibold ${stat.color}`}>{stat.value}</p>
            <p className="text-sm text-white/40">{stat.label}</p>
          </motion.div>
        ))}
      </div>

      <div className="space-y-4">
        {evaluations.map((evaluation, index) => (
          <motion.div key={evaluation.id} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: index * 0.05 }}>
            <div className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 hover:bg-white/[0.05] transition-colors cursor-pointer">
              <div className="flex items-start justify-between mb-4">
                <div className="flex items-center gap-4">
                  <div className="w-12 h-12 rounded-full bg-gradient-to-br from-accent-primary/30 to-violet/30 flex items-center justify-center text-sm font-medium text-white">
                    {evaluation.candidateName.split(' ').map((n: string) => n[0]).join('')}
                  </div>
                  <div>
                    <h3 className="text-lg font-semibold text-white">{evaluation.candidateName}</h3>
                    <p className="text-sm text-text-secondary">{evaluation.position}</p>
                  </div>
                </div>
                <div className="text-right">
                  {evaluation.status === 'completed' ? (
                    <div className="text-3xl font-semibold bg-gradient-to-r from-accent-primary to-violet bg-clip-text text-transparent">{evaluation.overallScore}</div>
                  ) : (
                    <span className="px-3 py-1 rounded-full bg-amber-500/20 text-amber-400 text-sm">Đang chờ</span>
                  )}
                </div>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-xs text-white/40">{evaluation.date}</span>
                <a href="/admin/evaluations" className="px-4 py-2 rounded-lg bg-accent-primary/20 text-accent-primary text-sm font-medium hover:bg-accent-primary/30 transition-colors">Xem chi tiết</a>
              </div>
            </div>
          </motion.div>
        ))}
      </div>
    </div>
  );
}
