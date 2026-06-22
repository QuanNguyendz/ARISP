import { LayoutDashboard, Briefcase, Users, KeyRound, ClipboardList, Video, Settings, Plus } from 'lucide-react'
import WorkspaceLayout, { type WorkspaceNavItem } from './WorkspaceLayout'

const navItems: WorkspaceNavItem[] = [
  { icon: LayoutDashboard, label: 'Tổng quan', path: '/recruiter/dashboard' },
  { icon: Briefcase, label: 'Tin tuyển dụng', path: '/recruiter/my-jobs' },
  { icon: Users, label: 'Ứng viên', path: '/recruiter/candidates' },
  { icon: KeyRound, label: 'Cấp mã phỏng vấn', path: '/recruiter/code' },
  { icon: ClipboardList, label: 'Đánh giá', path: '/recruiter/evaluations' },
  { icon: Video, label: 'Phỏng vấn', path: '/recruiter/interviews' },
  { icon: Settings, label: 'Cài đặt', path: '/recruiter/settings' },
]

export default function RecruiterLayout() {
  return (
    <WorkspaceLayout
      navItems={navItems}
      workspaceLabel="Recruiter Workspace"
      roleLabel="Recruiter"
      homePath="/recruiter/dashboard"
      searchPlaceholder="Tìm tin tuyển dụng, ứng viên..."
      settingsPath="/recruiter/settings"
      primaryAction={{ label: 'Tạo tin', to: '/recruiter/jobs/create', icon: Plus }}
    />
  )
}
