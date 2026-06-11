import { motion } from 'framer-motion';
import { UserCheck, Clock, XCircle, CheckCircle } from 'lucide-react';
import { PageHeader, StatsGrid, EmptyState, LoadingSpinner, ErrorAlert } from '@components/shared';

const stats = [
  { label: 'Chờ duyệt', value: '8', color: 'text-amber-400' },
  { label: 'Đã duyệt hôm nay', value: '5', color: 'text-emerald-400' },
  { label: 'Từ chối hôm nay', value: '2', color: 'text-red-400' },
  { label: 'Tổng tuần này', value: '23', color: 'text-blue-400' },
];

const pendingUsers = [
  { id: '1', name: 'Lê Minh C', email: 'minhc@example.com', role: 'Recruiter', organization: 'Công ty ABC', requestedAt: '2026-06-10 14:30', avatar: null },
  { id: '2', name: 'Phạm Thu D', email: 'thud@example.com', role: 'Hr_admin', organization: 'Công ty ABC', requestedAt: '2026-06-10 10:15', avatar: null },
  { id: '3', name: 'Đỗ Hoàng E', email: 'hoange@example.com', role: 'Recruiter', organization: 'Công ty XYZ', requestedAt: '2026-06-09 16:45', avatar: null },
  { id: '4', name: 'Vũ Thị F', email: 'thuf@example.com', role: 'Recruiter', organization: 'Công ty ABC', requestedAt: '2026-06-09 09:20', avatar: null },
  { id: '5', name: 'Bùi Minh G', email: 'ming@example.com', role: 'Hr_admin', organization: 'Công ty DEF', requestedAt: '2026-06-08 11:00', avatar: null },
];

const getRoleBadge = (role: string) => {
  if (role === 'Hr_admin') return { label: 'HR Admin', color: 'bg-violet-500/20 text-violet-400' };
  if (role === 'Recruiter') return { label: 'Recruiter', color: 'bg-amber-500/20 text-amber-400' };
  return { label: role, color: 'bg-white/10 text-white/40' };
};

const formatDate = (dateStr: string) => {
  const date = new Date(dateStr);
  return date.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
};

export default function PendingUsersPage() {
  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Users chờ duyệt"
        description="Xác minh và phê duyệt tài khoản người dùng mới"
      />

      <StatsGrid stats={stats} />

      {pendingUsers.length === 0 ? (
        <EmptyState
          icon={<CheckCircle className="w-8 h-8 text-emerald-400" />}
          title="Không có user chờ duyệt"
          description="Tất cả yêu cầu đã được xử lý."
        />
      ) : (
        <div className="space-y-4">
          {pendingUsers.map((user, index) => {
            const roleBadge = getRoleBadge(user.role);
            return (
              <motion.div
                key={user.id}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: index * 0.05 }}
                className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden"
              >
                <div className="flex items-start justify-between gap-4">
                  <div className="flex items-start gap-4">
                    <div className="w-12 h-12 rounded-full bg-gradient-to-br from-accent-primary/30 to-violet/30 flex items-center justify-center text-sm font-medium text-white">
                      {user.name.split(' ').map((n) => n[0]).join('')}
                    </div>
                    <div>
                      <h3 className="text-base font-semibold text-white mb-1">{user.name}</h3>
                      <p className="text-sm text-white/50 mb-2">{user.email}</p>
                      <div className="flex items-center gap-4">
                        <span className={`px-2.5 py-1 rounded-full text-xs font-medium ${roleBadge.color}`}>
                          {roleBadge.label}
                        </span>
                        <span className="flex items-center gap-1 text-xs text-white/40">
                          <Clock className="w-3 h-3" />
                          {formatDate(user.requestedAt)}
                        </span>
                        <span className="text-xs text-white/40">
                          {user.organization}
                        </span>
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    <button className="flex items-center gap-2 px-4 py-2 rounded-xl bg-emerald-500/20 text-emerald-400 text-sm font-medium hover:bg-emerald-500/30 transition-colors">
                      <CheckCircle className="w-4 h-4" />
                      Duyệt
                    </button>
                    <button className="flex items-center gap-2 px-4 py-2 rounded-xl bg-red-500/20 text-red-400 text-sm font-medium hover:bg-red-500/30 transition-colors">
                      <XCircle className="w-4 h-4" />
                      Từ chối
                    </button>
                  </div>
                </div>
              </motion.div>
            );
          })}
        </div>
      )}
    </div>
  );
}
