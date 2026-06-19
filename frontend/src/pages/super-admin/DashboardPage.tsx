import { motion } from 'framer-motion';
import { Users, Shield, Activity, UserCheck, Settings, Bell } from 'lucide-react';
import { PageHeader, StatsGrid } from '@components/shared';
import { useAuthStore } from '@store/auth/authStore';

const stats = [
  { label: 'Tổng Users', value: 156, change: '+12', color: 'text-blue-400' },
  { label: 'Users chờ duyệt', value: 8, change: '+3', color: 'text-amber-400' },
  { label: 'HR Admins', value: 12, change: '+2', color: 'text-violet-400' },
  { label: 'Recruiters', value: 45, change: '+5', color: 'text-emerald-400' },
];

const recentAuditLogs = [
  { id: '1', action: 'User approved', user: 'Nguyễn Văn A', target: 'Trần Thị B', time: '5 phút trước' },
  { id: '2', action: 'Role changed', user: 'Admin', target: 'User X → HR Admin', time: '15 phút trước' },
  { id: '3', action: 'New user registered', user: 'System', target: 'user@example.com', time: '30 phút trước' },
  { id: '4', action: 'Settings updated', user: 'Super Admin', target: 'System config', time: '1 giờ trước' },
];

const pendingUsers = [
  { id: '1', name: 'Lê Minh C', email: 'minhc@example.com', role: 'Recruiter', requestedAt: '2 giờ trước' },
  { id: '2', name: 'Phạm Thu D', email: 'thud@example.com', role: 'HR Admin', requestedAt: '4 giờ trước' },
  { id: '3', name: 'Đỗ Hoàng E', email: 'hoange@example.com', role: 'Recruiter', requestedAt: '1 ngày trước' },
];

export default function SuperAdminDashboardPage() {
  const { user } = useAuthStore();

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title={`Xin chào, ${user?.name || 'Super Admin'}`}
        description="Tổng quan hệ thống ARISP"
        actions={[
          { label: 'Cài đặt', href: '/super-admin/settings', variant: 'secondary' },
        ]}
      />

      <StatsGrid stats={stats} />

      <div className="grid lg:grid-cols-3 gap-8">
        {/* Main Content */}
        <div className="lg:col-span-2 space-y-8">
          {/* Pending Users */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.1 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden"
          >
            <div className="flex items-center justify-between mb-6">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-amber-500/20 flex items-center justify-center">
                  <UserCheck className="w-5 h-5 text-amber-400" />
                </div>
                <div>
                  <h2 className="text-base font-semibold text-white">Users chờ duyệt</h2>
                  <p className="text-xs text-white/40">Cần xác minh và phê duyệt</p>
                </div>
              </div>
              <a href="/super-admin/users/pending" className="text-xs text-emerald-400 hover:text-emerald-300 transition-colors">Xem tất cả</a>
            </div>

            <div className="space-y-3">
              {pendingUsers.map((user) => (
                <div key={user.id} className="flex items-center justify-between p-4 rounded-xl bg-white/[0.02] border border-white/5 hover:border-white/10 transition-colors">
                  <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-full bg-gradient-to-br from-amber-500/30 to-orange/30 flex items-center justify-center text-xs font-medium text-white">
                      {user.name.split(' ').map((n) => n[0]).join('')}
                    </div>
                    <div>
                      <h3 className="text-sm font-medium text-white">{user.name}</h3>
                      <p className="text-xs text-white/40">{user.email}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-4">
                    <span className="px-2 py-0.5 rounded-full bg-violet-500/20 text-violet-400 text-xs font-medium">
                      {user.role}
                    </span>
                    <span className="text-xs text-white/30">{user.requestedAt}</span>
                    <a href="/super-admin/users/pending" className="px-3 py-1.5 rounded-lg bg-emerald-500/20 text-emerald-400 text-xs font-medium hover:bg-emerald-500/30 transition-colors">
                      Duyệt
                    </a>
                  </div>
                </div>
              ))}
            </div>
          </motion.div>

          {/* Recent Audit Logs */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.2 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden"
          >
            <div className="flex items-center justify-between mb-6">
              <div className="flex items-center gap-3">
                <div className="w-10 h-10 rounded-xl bg-blue-500/20 flex items-center justify-center">
                  <Activity className="w-5 h-5 text-blue-400" />
                </div>
                <div>
                  <h2 className="text-base font-semibold text-white">Nhật ký hoạt động</h2>
                  <p className="text-xs text-white/40">Theo dõi thay đổi hệ thống</p>
                </div>
              </div>
              <a href="/super-admin/audit-logs" className="text-xs text-emerald-400 hover:text-emerald-300 transition-colors">Xem tất cả</a>
            </div>

            <div className="space-y-3">
              {recentAuditLogs.map((log) => (
                <div key={log.id} className="flex items-center justify-between p-3 rounded-xl bg-white/[0.02] border border-white/5">
                  <div className="flex items-center gap-3">
                    <div className="w-8 h-8 rounded-lg bg-blue-500/20 flex items-center justify-center">
                      <Activity className="w-4 h-4 text-blue-400" />
                    </div>
                    <div>
                      <p className="text-sm text-white">{log.action}</p>
                      <p className="text-xs text-white/40">Target: {log.target}</p>
                    </div>
                  </div>
                  <span className="text-xs text-white/30">{log.time}</span>
                </div>
              ))}
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
              <a href="/super-admin/users/pending" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-emerald-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-emerald-500/20 flex items-center justify-center">
                  <UserCheck className="w-4 h-4 text-emerald-400" />
                </div>
                <span className="text-sm text-white">Duyệt Users mới</span>
              </a>
              <a href="/super-admin/users" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-blue-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-blue-500/20 flex items-center justify-center">
                  <Users className="w-4 h-4 text-blue-400" />
                </div>
                <span className="text-sm text-white">Quản lý Users</span>
              </a>
              <a href="/super-admin/audit-logs" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-violet-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-violet-500/20 flex items-center justify-center">
                  <Activity className="w-4 h-4 text-violet-400" />
                </div>
                <span className="text-sm text-white">Xem Audit Logs</span>
              </a>
              <a href="/super-admin/settings" className="w-full flex items-center gap-3 p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-amber-500/30 transition-colors">
                <div className="w-8 h-8 rounded-lg bg-amber-500/20 flex items-center justify-center">
                  <Settings className="w-4 h-4 text-amber-400" />
                </div>
                <span className="text-sm text-white">Cài đặt hệ thống</span>
              </a>
            </div>
          </motion.div>

          {/* System Status */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.4 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-5 overflow-hidden"
          >
            <h2 className="text-sm font-semibold text-white mb-4">Trạng thái hệ thống</h2>
            <div className="space-y-4">
              <div className="flex items-center justify-between">
                <span className="text-xs text-white/40">Database</span>
                <span className="flex items-center gap-2 text-xs text-emerald-400">
                  <span className="w-2 h-2 rounded-full bg-emerald-400" />
                  Online
                </span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-xs text-white/40">API Server</span>
                <span className="flex items-center gap-2 text-xs text-emerald-400">
                  <span className="w-2 h-2 rounded-full bg-emerald-400" />
                  Online
                </span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-xs text-white/40">AI Service</span>
                <span className="flex items-center gap-2 text-xs text-emerald-400">
                  <span className="w-2 h-2 rounded-full bg-emerald-400" />
                  Online
                </span>
              </div>
            </div>
          </motion.div>
        </div>
      </div>
    </div>
  );
}
