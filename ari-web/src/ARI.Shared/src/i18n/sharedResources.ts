// Namespace dùng chung cho mọi site (CandidateSite, StaffSite).
// Site tự merge object này với namespace riêng rồi gọi initI18n (xem core.ts).

import commonVi from './locales/vi/common.json'
import commonEn from './locales/en/common.json'
import authVi from './locales/vi/auth.json'
import authEn from './locales/en/auth.json'
import errorsVi from './locales/vi/errors.json'
import errorsEn from './locales/en/errors.json'
import jobsVi from './locales/vi/jobs.json'
import jobsEn from './locales/en/jobs.json'
import interviewVi from './locales/vi/interview.json'
import interviewEn from './locales/en/interview.json'
import landingVi from './locales/vi/landing.json'
import landingEn from './locales/en/landing.json'
import sharedNotificationsVi from './locales/vi/modules/shared/notifications.json'
import sharedNotificationsEn from './locales/en/modules/shared/notifications.json'
import sharedLayoutVi from './locales/vi/modules/shared/layout.json'
import sharedLayoutEn from './locales/en/modules/shared/layout.json'
import sharedNavVi from './locales/vi/modules/shared/nav.json'
import sharedNavEn from './locales/en/modules/shared/nav.json'
import sharedDeviceCheckVi from './locales/vi/modules/shared/deviceCheck.json'
import sharedDeviceCheckEn from './locales/en/modules/shared/deviceCheck.json'
import sharedAssignSchedulePanelVi from './locales/vi/modules/shared/assignSchedulePanel.json'
import sharedAssignSchedulePanelEn from './locales/en/modules/shared/assignSchedulePanel.json'
import sharedDesignSystemVi from './locales/vi/modules/shared/designSystem.json'
import sharedDesignSystemEn from './locales/en/modules/shared/designSystem.json'
import sharedDocumentViewerVi from './locales/vi/modules/shared/documentViewer.json'
import sharedDocumentViewerEn from './locales/en/modules/shared/documentViewer.json'
import sharedImageCropVi from './locales/vi/modules/shared/imageCrop.json'
import sharedImageCropEn from './locales/en/modules/shared/imageCrop.json'

export const sharedNamespaces = [
  'common',
  'auth',
  'errors',
  'jobs',
  'interview',
  'landing',
  'modules/shared/notifications',
  'modules/shared/layout',
  'modules/shared/nav',
  'modules/shared/deviceCheck',
  'modules/shared/assignSchedulePanel',
  'modules/shared/designSystem',
  'modules/shared/documentViewer',
  'modules/shared/imageCrop',
]

export const sharedResources = {
  vi: {
    common: commonVi,
    auth: authVi,
    errors: errorsVi,
    jobs: jobsVi,
    interview: interviewVi,
    landing: landingVi,
    'modules/shared/notifications': sharedNotificationsVi,
    'modules/shared/layout': sharedLayoutVi,
    'modules/shared/nav': sharedNavVi,
    'modules/shared/deviceCheck': sharedDeviceCheckVi,
    'modules/shared/assignSchedulePanel': sharedAssignSchedulePanelVi,
    'modules/shared/designSystem': sharedDesignSystemVi,
    'modules/shared/documentViewer': sharedDocumentViewerVi,
    'modules/shared/imageCrop': sharedImageCropVi,
  },
  en: {
    common: commonEn,
    auth: authEn,
    errors: errorsEn,
    jobs: jobsEn,
    interview: interviewEn,
    landing: landingEn,
    'modules/shared/notifications': sharedNotificationsEn,
    'modules/shared/layout': sharedLayoutEn,
    'modules/shared/nav': sharedNavEn,
    'modules/shared/deviceCheck': sharedDeviceCheckEn,
    'modules/shared/assignSchedulePanel': sharedAssignSchedulePanelEn,
    'modules/shared/designSystem': sharedDesignSystemEn,
    'modules/shared/documentViewer': sharedDocumentViewerEn,
    'modules/shared/imageCrop': sharedImageCropEn,
  },
}
