import { Plus } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import WorkspaceLayout from './WorkspaceLayout'
import { useRecruiterNav } from '@/app/layouts/useWorkspaceNav'

export default function RecruiterLayout() {
  const { t } = useTranslation('modules/shared/nav')
  const navItems = useRecruiterNav()

  return (
    <WorkspaceLayout
      navItems={navItems}
      workspaceLabel={t('recruiter.workspace')}
      roleLabel={t('recruiter.roleLabel')}
      homePath="/recruiter/dashboard"
      searchPlaceholder={t('recruiter.searchPlaceholder')}
      searchPath="/recruiter/candidates"
      globalSearchScope="recruiter"
      settingsPath="/recruiter/settings"
      helpPath="/recruiter/help"
      notificationsPath="/recruiter/notifications"
      primaryAction={{ label: t('recruiter.createJob'), to: '/recruiter/jobs/create', icon: Plus }}
    />
  )
}
