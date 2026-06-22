import { LayoutDashboard, Users, UserCheck, Activity, Shield, UserPlus, List } from 'lucide-react'
import WorkspaceLayout, { type WorkspaceNavItem } from './WorkspaceLayout'

const navItems: WorkspaceNavItem[] = [
  { icon: LayoutDashboard, label: 'Tổng quan', path: '/super-admin/dashboard' },
  {
    icon: Users,
    label: 'Quản lý Users',
    children: [
      { icon: List, label: 'Tất cả người dùng', path: '/super-admin/users', exact: true },
      { icon: UserCheck, label: 'Duyệt User mới', path: '/super-admin/users/pending' },
    ],
  },
  { icon: Activity, label: 'Audit Logs', path: '/super-admin/audit-logs' },
  { icon: Shield, label: 'Cài đặt hệ thống', path: '/super-admin/settings' },
]

export default function SuperAdminLayout() {
  return (
    <WorkspaceLayout
      navItems={navItems}
      workspaceLabel="Super Admin Workspace"
      roleLabel="Super Admin"
      homePath="/super-admin/dashboard"
      searchPlaceholder="Tìm user, audit log..."
      settingsPath="/super-admin/settings"
      primaryAction={{ label: 'Thêm staff', to: '/super-admin/users?create=1', icon: UserPlus }}
    />
  )
}
