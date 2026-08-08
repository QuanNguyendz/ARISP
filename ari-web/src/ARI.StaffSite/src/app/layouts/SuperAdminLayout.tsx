import { UserPlus } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import WorkspaceLayout from './WorkspaceLayout'
import { useSuperAdminNav } from '@/app/layouts/useWorkspaceNav'

export default function SuperAdminLayout() {
  const { t } = useTranslation('modules/shared/nav')
  const navItems = useSuperAdminNav()

  return (
    <WorkspaceLayout
      navItems={navItems}
      workspaceLabel={t('superAdmin.workspace')}
      roleLabel={t('superAdmin.roleLabel')}
      homePath="/super-admin/dashboard"
      searchPlaceholder={t('superAdmin.searchPlaceholder')}
      searchPath="/super-admin/users"
      settingsPath="/super-admin/settings"
      primaryAction={{
        label: t('superAdmin.addStaff'),
        to: '/super-admin/users?create=1',
        icon: UserPlus,
      }}
    />
  )
}
