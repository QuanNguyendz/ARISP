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
  Scale,
  FileSignature,
  FileText,
  Building2,
  DoorOpen,
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
    { icon: ClipboardList, label: t('hr.recruitmentRequests'), path: '/hr/recruitment-requests' },
    { icon: FileText, label: t('hr.jdTemplate'), path: '/hr/jd-template' },
    { icon: FileSignature, label: t('hr.offers'), path: '/hr/offers' },
    { icon: BookOpen, label: t('hr.playbooks'), path: '/hr/playbooks' },
    // Hai mục khác nhau, cố ý tách: "Phân công" là vận hành hằng ngày (ai đang gánh gì, chỗ nào
    // tắc, chuyển giao tin); "Yêu cầu tài khoản" là hành chính (xin Super Admin cấp tài khoản mới).
    { icon: Scale, label: t('hr.recruiters'), path: '/hr/recruiters' },
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
    { icon: ClipboardList, label: t('recruiter.recruitmentRequests'), path: '/recruiter/recruitment-requests' },
    { icon: FileSignature, label: t('recruiter.offers'), path: '/recruiter/offers' },
    { icon: Settings, label: t('recruiter.settings'), path: '/recruiter/settings' },
  ]
}

/**
 * Hiring Manager (ADR-061): trưởng bộ phận có nhu cầu tuyển. Điều hướng cố ý NGẮN — họ không vận
 * hành phễu (không xếp lịch, không cấp mã, không sửa tin), chỉ quyết định: duyệt shortlist, ký
 * duyệt JD, chốt kết quả phỏng vấn, duyệt offer.
 */
export function useHmNav(): NavItem[] {
  const { t } = useTranslation('modules/shared/nav')

  return [
    { icon: LayoutDashboard, label: t('hm.dashboard'), path: '/hm/dashboard' },
    { icon: ClipboardList, label: t('hm.recruitmentRequests'), path: '/hm/recruitment-requests' },
    { icon: Briefcase, label: t('hm.jobs'), path: '/hm/jobs' },
    { icon: DoorOpen, label: t('hm.interviewRooms'), path: '/hm/interview-rooms' },
    { icon: ClipboardCheck, label: t('hm.evaluations'), path: '/hm/evaluations' },
    { icon: FileSignature, label: t('hm.offers'), path: '/hm/offers' },
    { icon: Settings, label: t('hm.settings'), path: '/hm/settings' },
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
    { icon: Building2, label: t('superAdmin.departments'), path: '/super-admin/departments' },
    { icon: Activity, label: t('superAdmin.auditLogs'), path: '/super-admin/audit-logs' },
    { icon: Shield, label: t('superAdmin.systemSettings'), path: '/super-admin/settings' },
  ]
}
