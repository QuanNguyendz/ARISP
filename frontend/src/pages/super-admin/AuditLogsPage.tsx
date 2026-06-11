import { motion } from 'framer-motion';
import { Activity, Search, Filter, Download, Eye } from 'lucide-react';
import { useState } from 'react';
import { PageHeader, EmptyState } from '@components/shared';

const auditLogs = [
  { id: '1', action: 'USER_APPROVED', user: 'Super Admin', target: 'Lê Minh C', details: 'Approved recruiter request', timestamp: '2026-06-11 14:30:25' },
  { id: '2', action: 'ROLE_CHANGED', user: 'Super Admin', target: 'Phạm Thu D', details: 'Role changed: Recruiter → HR Admin', timestamp: '2026-06-11 14:15:10' },
  { id: '3', action: 'USER_REJECTED', user: 'Super Admin', target: 'Ngô Văn E', details: 'Rejected with reason: Invalid documents', timestamp: '2026-06-11 13:45:00' },
  { id: '4', action: 'SETTINGS_UPDATED', user: 'Super Admin', target: 'System Config', details: 'Updated AI service endpoint', timestamp: '2026-06-11 12:00:00' },
  { id: '5', action: 'USER_CREATED', user: 'System', target: 'user@example.com', details: 'New user registered via OAuth', timestamp: '2026-06-11 11:30:15' },
  { id: '6', action: 'PASSWORD_RESET', user: 'Super Admin', target: 'Trần Văn F', details: 'Password reset by admin', timestamp: '2026-06-10 16:20:00' },
  { id: '7', action: 'USER_SUSPENDED', user: 'Super Admin', target: 'Lý Thị G', details: 'Account suspended due to policy violation', timestamp: '2026-06-10 10:00:00' },
  { id: '8', action: 'API_KEY_GENERATED', user: 'System', target: 'API Integration', details: 'New API key generated', timestamp: '2026-06-09 15:30:00' },
];

const actionColors: Record<string, string> = {
  USER_APPROVED: 'bg-emerald-500/20 text-emerald-400',
  USER_REJECTED: 'bg-red-500/20 text-red-400',
  ROLE_CHANGED: 'bg-violet-500/20 text-violet-400',
  SETTINGS_UPDATED: 'bg-blue-500/20 text-blue-400',
  USER_CREATED: 'bg-cyan-500/20 text-cyan-400',
  PASSWORD_RESET: 'bg-amber-500/20 text-amber-400',
  USER_SUSPENDED: 'bg-red-500/20 text-red-400',
  API_KEY_GENERATED: 'bg-teal-500/20 text-teal-400',
};

export default function AuditLogsPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [actionFilter, setActionFilter] = useState<string>('all');

  const filteredLogs = auditLogs.filter((log) => {
    const matchesSearch = log.user.toLowerCase().includes(searchQuery.toLowerCase()) ||
                          log.target.toLowerCase().includes(searchQuery.toLowerCase()) ||
                          log.details.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesAction = actionFilter === 'all' || log.action === actionFilter;
    return matchesSearch && matchesAction;
  });

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Audit Logs"
        description="Nhật ký hoạt động hệ thống"
        actions={[
          { label: 'Export', href: '#', variant: 'secondary', icon: <Download className="w-4 h-4" /> },
        ]}
      />

      {/* Filters */}
      <div className="flex flex-col sm:flex-row gap-4 mb-6">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-white/40" />
          <input
            type="text"
            placeholder="Tìm kiếm theo user, target, chi tiết..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full pl-10 pr-4 py-2.5 rounded-xl bg-white/[0.03] border border-white/10 text-white placeholder-white/30 focus:outline-none focus:border-accent-primary/50 transition-colors"
          />
        </div>
        <select
          value={actionFilter}
          onChange={(e) => setActionFilter(e.target.value)}
          className="px-4 py-2.5 rounded-xl bg-white/[0.03] border border-white/10 text-white focus:outline-none focus:border-accent-primary/50 transition-colors"
        >
          <option value="all">Tất cả actions</option>
          <option value="USER_APPROVED">User Approved</option>
          <option value="USER_REJECTED">User Rejected</option>
          <option value="ROLE_CHANGED">Role Changed</option>
          <option value="SETTINGS_UPDATED">Settings Updated</option>
          <option value="USER_CREATED">User Created</option>
          <option value="PASSWORD_RESET">Password Reset</option>
        </select>
      </div>

      {/* Audit Logs Table */}
      {filteredLogs.length === 0 ? (
        <EmptyState
          icon={<Activity className="w-8 h-8 text-white/20" />}
          title="Không có log"
          description="Không có nhật ký nào phù hợp với điều kiện tìm kiếm."
        />
      ) : (
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] overflow-hidden"
        >
          <div className="overflow-x-auto">
            <table className="w-full">
              <thead>
                <tr className="border-b border-white/5">
                  <th className="px-6 py-4 text-left text-xs font-medium text-white/40 uppercase tracking-wider">Timestamp</th>
                  <th className="px-6 py-4 text-left text-xs font-medium text-white/40 uppercase tracking-wider">Action</th>
                  <th className="px-6 py-4 text-left text-xs font-medium text-white/40 uppercase tracking-wider">User</th>
                  <th className="px-6 py-4 text-left text-xs font-medium text-white/40 uppercase tracking-wider">Target</th>
                  <th className="px-6 py-4 text-left text-xs font-medium text-white/40 uppercase tracking-wider">Details</th>
                  <th className="px-6 py-4 text-right text-xs font-medium text-white/40 uppercase tracking-wider">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-white/5">
                {filteredLogs.map((log) => (
                  <tr key={log.id} className="hover:bg-white/[0.02] transition-colors">
                    <td className="px-6 py-4 text-sm text-white/50 font-mono">{log.timestamp}</td>
                    <td className="px-6 py-4">
                      <span className={`px-2.5 py-1 rounded-full text-xs font-medium ${actionColors[log.action] || 'bg-white/10 text-white/40'}`}>
                        {log.action}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-sm text-white">{log.user}</td>
                    <td className="px-6 py-4 text-sm text-white/70">{log.target}</td>
                    <td className="px-6 py-4 text-sm text-white/50">{log.details}</td>
                    <td className="px-6 py-4 text-right">
                      <button className="p-2 rounded-lg hover:bg-white/10 transition-colors">
                        <Eye className="w-4 h-4 text-white/50" />
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </motion.div>
      )}
    </div>
  );
}
