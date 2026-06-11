import { motion } from 'framer-motion';
import { Users, Search, MoreVertical, UserCheck, UserX } from 'lucide-react';
import { useState } from 'react';
import { PageHeader, StatsGrid, EmptyState, LoadingSpinner, ErrorAlert } from '@components/shared';

const stats = [
  { label: 'Tổng Users', value: '156', color: 'text-blue-400' },
  { label: 'HR Admins', value: '12', color: 'text-violet-400' },
  { label: 'Recruiters', value: '45', color: 'text-amber-400' },
  { label: 'Candidates', value: '99', color: 'text-emerald-400' },
];

const mockUsers = [
  { id: '1', name: 'Nguyễn Văn A', email: 'admin@arisp.com', role: 'Super_admin', status: 'active', createdAt: '01/01/2026' },
  { id: '2', name: 'Trần Thị B', email: 'hr.leader@arisp.com', role: 'Hr_admin', status: 'active', createdAt: '15/01/2026' },
  { id: '3', name: 'Lê Minh C', email: 'recruiter1@arisp.com', role: 'Recruiter', status: 'active', createdAt: '20/01/2026' },
  { id: '4', name: 'Phạm Thu D', email: 'recruiter2@arisp.com', role: 'Recruiter', status: 'active', createdAt: '25/01/2026' },
  { id: '5', name: 'Đỗ Hoàng E', email: 'hr.staff@arisp.com', role: 'Recruiter', status: 'inactive', createdAt: '01/02/2026' },
];

const getRoleBadge = (role: string) => {
  if (role === 'Super_admin') return { label: 'Super Admin', color: 'bg-red-500/20 text-red-400' };
  if (role === 'Hr_admin') return { label: 'HR Admin', color: 'bg-violet-500/20 text-violet-400' };
  if (role === 'Recruiter') return { label: 'Recruiter', color: 'bg-amber-500/20 text-amber-400' };
  return { label: role, color: 'bg-white/10 text-white/40' };
};

const getStatusBadge = (status: string) => {
  if (status === 'active') return { label: 'Active', color: 'bg-emerald-500/20 text-emerald-400' };
  return { label: 'Inactive', color: 'bg-white/10 text-white/40' };
};

export default function UsersPage() {
  const [searchQuery, setSearchQuery] = useState('');
  const [roleFilter, setRoleFilter] = useState<string>('all');
  const loading = false;
  const error = null;

  const filteredUsers = mockUsers.filter((user) => {
    const matchesSearch = user.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
                          user.email.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesRole = roleFilter === 'all' || user.role === roleFilter;
    return matchesSearch && matchesRole;
  });

  if (loading) return <div className="p-6 lg:p-8"><LoadingSpinner /></div>;
  if (error) return <div className="p-6 lg:p-8"><ErrorAlert message={error} /></div>;

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Quản lý Users"
        description="Quản lý tài khoản và phân quyền người dùng"
        actions={[
          { label: 'Thêm User', href: '/super-admin/users/create', variant: 'primary' },
        ]}
      />

      <StatsGrid stats={stats} />

      {/* Filters */}
      <div className="flex flex-col sm:flex-row gap-4 mb-6">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-white/40" />
          <input
            type="text"
            placeholder="Tìm kiếm theo tên hoặc email..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full pl-10 pr-4 py-2.5 rounded-xl bg-white/[0.03] border border-white/10 text-white placeholder-white/30 focus:outline-none focus:border-accent-primary/50 transition-colors"
          />
        </div>
        <select
          value={roleFilter}
          onChange={(e) => setRoleFilter(e.target.value)}
          className="px-4 py-2.5 rounded-xl bg-white/[0.03] border border-white/10 text-white focus:outline-none focus:border-accent-primary/50 transition-colors"
        >
          <option value="all">Tất cả vai trò</option>
          <option value="Super_admin">Super Admin</option>
          <option value="Hr_admin">HR Admin</option>
          <option value="Recruiter">Recruiter</option>
        </select>
      </div>

      {/* Users Table */}
      {filteredUsers.length === 0 ? (
        <EmptyState
          icon={<Users className="w-8 h-8 text-white/20" />}
          title="Không tìm thấy user"
          description="Không có user nào phù hợp với điều kiện tìm kiếm."
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
                  <th className="px-6 py-4 text-left text-xs font-medium text-white/40 uppercase tracking-wider">User</th>
                  <th className="px-6 py-4 text-left text-xs font-medium text-white/40 uppercase tracking-wider">Vai trò</th>
                  <th className="px-6 py-4 text-left text-xs font-medium text-white/40 uppercase tracking-wider">Trạng thái</th>
                  <th className="px-6 py-4 text-left text-xs font-medium text-white/40 uppercase tracking-wider">Ngày tạo</th>
                  <th className="px-6 py-4 text-right text-xs font-medium text-white/40 uppercase tracking-wider">Thao tác</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-white/5">
                {filteredUsers.map((user) => {
                  const roleBadge = getRoleBadge(user.role);
                  const statusBadge = getStatusBadge(user.status);
                  return (
                    <tr key={user.id} className="hover:bg-white/[0.02] transition-colors">
                      <td className="px-6 py-4">
                        <div className="flex items-center gap-3">
                          <div className="w-10 h-10 rounded-full bg-gradient-to-br from-accent-primary/30 to-violet/30 flex items-center justify-center text-xs font-medium text-white">
                            {user.name.split(' ').map((n) => n[0]).join('')}
                          </div>
                          <div>
                            <p className="text-sm font-medium text-white">{user.name}</p>
                            <p className="text-xs text-white/40">{user.email}</p>
                          </div>
                        </div>
                      </td>
                      <td className="px-6 py-4">
                        <span className={`px-2.5 py-1 rounded-full text-xs font-medium ${roleBadge.color}`}>
                          {roleBadge.label}
                        </span>
                      </td>
                      <td className="px-6 py-4">
                        <span className={`px-2.5 py-1 rounded-full text-xs font-medium ${statusBadge.color}`}>
                          {statusBadge.label}
                        </span>
                      </td>
                      <td className="px-6 py-4 text-sm text-white/50">{user.createdAt}</td>
                      <td className="px-6 py-4 text-right">
                        <div className="flex items-center justify-end gap-2">
                          <button className="p-2 rounded-lg hover:bg-white/10 transition-colors">
                            <UserCheck className="w-4 h-4 text-emerald-400" />
                          </button>
                          <button className="p-2 rounded-lg hover:bg-white/10 transition-colors">
                            <UserX className="w-4 h-4 text-red-400" />
                          </button>
                          <button className="p-2 rounded-lg hover:bg-white/10 transition-colors">
                            <MoreVertical className="w-4 h-4 text-white/50" />
                          </button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </motion.div>
      )}
    </div>
  );
}
