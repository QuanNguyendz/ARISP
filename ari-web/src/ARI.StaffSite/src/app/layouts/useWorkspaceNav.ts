import { useTranslation } from 'react-i18next'
import {
  LayoutDashboard,
  Briefcase,
  Users,
  Video,
  ClipboardCheck,
  BookOpen,
  UserCog,
  Settings,
  ClipboardList,
  UserCheck,
  Activity,
  Shield,
  List,
  LucideIcon,
} from 'lucide-react'

type Icon = LucideIcon

interface NavItem {
  icon: Icon
  label: string
  path?: string
  badge?: number
  exact?: boolean
  children?: NavItem[]
}

export function useHrNav(): NavItem[] {
  const { t } = useTranslation('modules/shared/nav')

  return [
    { icon: LayoutDashboard, label: t('hr.dashboard'), path: '/hr/dashboard' },
    { icon: Briefcase, label: t('hr.jobs'), path: '/hr/jobs' },
    { icon: Users, label: t('hr.candidates'), path: '/hr/candidates' },
    { icon: Video, label: t('hr.interviews'), path: '/hr/interviews' },
    { icon: ClipboardCheck, label: t('hr.evaluations'), path: '/hr/evaluations' },
    { icon: BookOpen, label: t('hr.playbooks'), path: '/hr/playbooks' },
    { icon: UserCog, label: t('hr.team'), path: '/hr/team' },
    { icon: Settings, label: t('hr.settings'), path: '/hr/settings' },
  ]
}

export function useRecruiterNav(): NavItem[] {
  const { t } = useTranslation('modules/shared/nav')

  return [
    { icon: LayoutDashboard, label: t('recruiter.dashboard'), path: '/recruiter/dashboard' },
    { icon: Briefcase, label: t('recruiter.jobs'), path: '/recruiter/my-jobs' },
    { icon: Users, label: t('recruiter.candidates'), path: '/recruiter/candidates' },
    { icon: ClipboardList, label: t('recruiter.evaluations'), path: '/recruiter/evaluations' },
    { icon: Video, label: t('recruiter.interviews'), path: '/recruiter/interviews' },
    { icon: Settings, label: t('recruiter.settings'), path: '/recruiter/settings' },
  ]
}

export function useSuperAdminNav(): NavItem[] {
  const { t } = useTranslation('modules/shared/nav')

  return [
    { icon: LayoutDashboard, label: t('superAdmin.dashboard'), path: '/super-admin/dashboard' },
    {
      icon: Users,
      label: t('superAdmin.users'),
      children: [
        { icon: List, label: t('superAdmin.allUsers'), path: '/super-admin/users', exact: true },
        {
          icon: UserCheck,
          label: t('superAdmin.pendingUsers'),
          path: '/super-admin/users/pending',
        },
      ],
    },
    { icon: Activity, label: t('superAdmin.auditLogs'), path: '/super-admin/audit-logs' },
    { icon: Shield, label: t('superAdmin.systemSettings'), path: '/super-admin/settings' },
  ]
}
