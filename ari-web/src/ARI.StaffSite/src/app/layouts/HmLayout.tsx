import { useTranslation } from 'react-i18next'
import WorkspaceLayout from './WorkspaceLayout'
import { useHmNav } from '@/app/layouts/useWorkspaceNav'

/**
 * Khu làm việc của Hiring Manager (ADR-061).
 *
 * Wrapper mỏng trên WorkspaceLayout, theo đúng khuôn SuperAdminLayout. CỐ Ý không sao chép
 * HrLayout/RecruiterLayout: hai file đó có trước WorkspaceLayout và lặp ~400 dòng topbar/sidebar/
 * dropdown thông báo mỗi bản — bản thứ tư chắc chắn sẽ trôi khỏi ba bản kia (đúng lỗi mà ADR-058
 * và ADR-059 đã ghi lại cho hai màn Phỏng vấn).
 */
export default function HmLayout() {
  const { t } = useTranslation('modules/shared/nav')
  const navItems = useHmNav()

  return (
    <WorkspaceLayout
      navItems={navItems}
      workspaceLabel={t('hm.workspace')}
      roleLabel={t('hm.roleLabel')}
      homePath="/hm/dashboard"
      searchPlaceholder={t('hm.searchPlaceholder')}
      searchPath="/hm/jobs"
      settingsPath="/hm/settings"
    />
  )
}
