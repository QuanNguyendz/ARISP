import { motion } from 'framer-motion';
import { useNavigate } from 'react-router-dom';
import { Search, Filter, MoreVertical, Mail, Calendar, Eye, CheckCircle, XCircle, Clock } from 'lucide-react';

const candidates = [
  {
    id: '1',
    name: 'Nguyễn Văn An',
    email: 'an.nguyen@email.com',
    position: 'Senior Backend Developer',
    status: 'interview',
    round: 2,
    score: 87,
    appliedDate: '16/05/2026',
    avatar: null,
  },
  {
    id: '2',
    name: 'Trần Thị Minh',
    email: 'minh.tran@email.com',
    position: 'Product Designer',
    status: 'pending',
    round: 1,
    score: null,
    appliedDate: '16/05/2026',
    avatar: null,
  },
  {
    id: '3',
    name: 'Lê Hoàng Nam',
    email: 'nam.le@email.com',
    position: 'Data Scientist',
    status: 'passed',
    round: 2,
    score: 91,
    appliedDate: '15/05/2026',
    avatar: null,
  },
  {
    id: '4',
    name: 'Phạm Thu Hà',
    email: 'ha.pham@email.com',
    position: 'Frontend Developer',
    status: 'interview',
    round: 1,
    score: 78,
    appliedDate: '15/05/2026',
    avatar: null,
  },
  {
    id: '5',
    name: 'Đỗ Minh Tuấn',
    email: 'tuan.do@email.com',
    position: 'DevOps Engineer',
    status: 'rejected',
    round: 1,
    score: 45,
    appliedDate: '14/05/2026',
    avatar: null,
  },
  {
    id: '6',
    name: 'Vũ Thị Lan',
    email: 'lan.vu@email.com',
    position: 'QA Engineer',
    status: 'pending',
    round: 1,
    score: null,
    appliedDate: '14/05/2026',
    avatar: null,
  },
];

const statusConfig: Record<string, { label: string; color: string; icon: typeof Clock }> = {
  pending: { label: 'Chờ phỏng vấn', color: 'bg-amber-500/20 text-amber-400', icon: Clock },
  interview: { label: 'Đang phỏng vấn', color: 'bg-blue-500/20 text-blue-400', icon: Calendar },
  passed: { label: 'Đạt', color: 'bg-emerald-500/20 text-emerald-400', icon: CheckCircle },
  rejected: { label: 'Không đạt', color: 'bg-red-500/20 text-red-400', icon: XCircle },
};

export default function CandidatesPage() {
  const navigate = useNavigate();

  return (
    <div className="min-h-screen bg-bg-primary p-6 lg:p-8">
      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="mb-8"
      >
        <h1 className="text-3xl font-semibold text-white mb-2">Ứng viên</h1>
        <p className="text-text-secondary">Quản lý và theo dõi tất cả ứng viên</p>
      </motion.div>

      {/* Filters */}
      <div className="p-4 rounded-2xl bg-white/5 border border-white/10 mb-6">
        <div className="flex flex-col md:flex-row gap-4">
          <div className="flex-1 relative">
            <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
            <input
              type="text"
              placeholder="Tìm kiếm ứng viên..."
              className="w-full pl-12 pr-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
            />
          </div>
          <div className="flex gap-3">
            <button className="flex items-center gap-2 px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white hover:bg-white/10 transition-colors">
              <Filter className="w-4 h-4" />
              Bộ lọc
            </button>
            <select className="px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white focus:outline-none focus:border-accent-primary/50">
              <option value="all">Tất cả</option>
              <option value="pending">Chờ phỏng vấn</option>
              <option value="interview">Đang phỏng vấn</option>
              <option value="passed">Đạt</option>
              <option value="rejected">Không đạt</option>
            </select>
          </div>
        </div>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
        {[
          { label: 'Tổng ứng viên', value: '156', color: 'text-blue-400' },
          { label: 'Chờ phỏng vấn', value: '42', color: 'text-amber-400' },
          { label: 'Đang phỏng vấn', value: '23', color: 'text-violet-400' },
          { label: 'Đã hoàn thành', value: '91', color: 'text-emerald-400' },
        ].map((stat) => (
          <div key={stat.label} className="p-4 rounded-2xl bg-white/5 border border-white/10">
            <p className={`text-2xl font-semibold ${stat.color}`}>{stat.value}</p>
            <p className="text-sm text-text-secondary">{stat.label}</p>
          </div>
        ))}
      </div>

      {/* Candidates Table */}
      <div className="p-6 rounded-2xl bg-white/5 border border-white/10">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-white/10">
                <th className="text-left py-3 px-4 text-sm font-medium text-text-secondary">Ứng viên</th>
                <th className="text-left py-3 px-4 text-sm font-medium text-text-secondary">Vị trí</th>
                <th className="text-left py-3 px-4 text-sm font-medium text-text-secondary">Trạng thái</th>
                <th className="text-left py-3 px-4 text-sm font-medium text-text-secondary">Vòng</th>
                <th className="text-left py-3 px-4 text-sm font-medium text-text-secondary">Điểm</th>
                <th className="text-left py-3 px-4 text-sm font-medium text-text-secondary">Ngày ứng tuyển</th>
                <th className="text-right py-3 px-4 text-sm font-medium text-text-secondary">Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {candidates.map((candidate, index) => {
                const status = statusConfig[candidate.status];
                const StatusIcon = status?.icon || Clock;

                return (
                  <motion.tr
                    key={candidate.id}
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: index * 0.05 }}
                    onClick={() => navigate(`/recruiter/candidates/${candidate.id}`)}
                    className="border-b border-white/5 hover:bg-white/[0.02] cursor-pointer transition-colors"
                  >
                    <td className="py-4 px-4">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 rounded-full bg-gradient-to-br from-accent-primary/30 to-violet/30 flex items-center justify-center text-sm font-medium text-white">
                          {candidate.name.split(' ').map(n => n[0]).join('')}
                        </div>
                        <div>
                          <p className="font-medium text-white">{candidate.name}</p>
                          <p className="text-sm text-text-secondary">{candidate.email}</p>
                        </div>
                      </div>
                    </td>
                    <td className="py-4 px-4 text-white">{candidate.position}</td>
                    <td className="py-4 px-4">
                      <span className={`inline-flex items-center gap-1 px-2 py-1 rounded-full text-xs font-medium ${status?.color || 'bg-white/10 text-white'}`}>
                        <StatusIcon className="w-3 h-3" />
                        {status?.label || candidate.status}
                      </span>
                    </td>
                    <td className="py-4 px-4 text-text-secondary">Vòng {candidate.round}</td>
                    <td className="py-4 px-4">
                      {candidate.score ? (
                        <span className={`font-medium ${
                          candidate.score >= 80 ? 'text-emerald-400' :
                          candidate.score >= 60 ? 'text-amber-400' : 'text-red-400'
                        }`}>
                          {candidate.score}
                        </span>
                      ) : (
                        <span className="text-text-tertiary">-</span>
                      )}
                    </td>
                    <td className="py-4 px-4 text-text-secondary">{candidate.appliedDate}</td>
                    <td className="py-4 px-4">
                      <div className="flex items-center justify-end gap-2">
                        <button className="p-2 rounded-lg hover:bg-white/10 transition-colors">
                          <Mail className="w-4 h-4 text-text-tertiary" />
                        </button>
                        <button className="p-2 rounded-lg hover:bg-white/10 transition-colors">
                          <Eye className="w-4 h-4 text-text-tertiary" />
                        </button>
                        <button className="p-2 rounded-lg hover:bg-white/10 transition-colors">
                          <MoreVertical className="w-4 h-4 text-text-tertiary" />
                        </button>
                      </div>
                    </td>
                  </motion.tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
