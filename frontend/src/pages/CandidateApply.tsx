import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import CandidateNav from '@components/layout/CandidateNav'
import GlassCard from '@components/ui/GlassCard'
import { Calendar, Clock, Eye, CheckCircle, XCircle } from 'lucide-react'

const applications = [
  {
    id: '1',
    jobTitle: 'Senior Frontend Developer',
    company: 'TechVision Corp',
    logo: 'TV',
    status: 'interview',
    appliedDate: '20/05/2026',
    interviewDate: '30/05/2026',
    score: null,
  },
  {
    id: '2',
    jobTitle: 'Backend Engineer',
    company: 'DataFlow Systems',
    logo: 'DF',
    status: 'reviewing',
    appliedDate: '18/05/2026',
    interviewDate: null,
    score: null,
  },
  {
    id: '3',
    jobTitle: 'UI/UX Designer',
    company: 'Creative Hub',
    logo: 'CH',
    status: 'pending',
    appliedDate: '15/05/2026',
    interviewDate: null,
    score: null,
  },
  {
    id: '4',
    jobTitle: 'Data Scientist',
    company: 'AI Analytics',
    logo: 'AA',
    status: 'accepted',
    appliedDate: '10/05/2026',
    interviewDate: '25/05/2026',
    score: 87,
  },
  {
    id: '5',
    jobTitle: 'DevOps Engineer',
    company: 'CloudTech',
    logo: 'CT',
    status: 'rejected',
    appliedDate: '05/05/2026',
    interviewDate: null,
    score: 45,
  },
]

const statusConfig = (t: (key: string) => string) => ({
  pending: { label: t('status.pending'), color: 'bg-amber-500/20 text-amber-400', icon: Clock },
  reviewing: { label: t('status.reviewing'), color: 'bg-blue-500/20 text-blue-400', icon: Eye },
  interview: {
    label: t('status.interview'),
    color: 'bg-violet-500/20 text-violet-400',
    icon: Calendar,
  },
  accepted: {
    label: t('status.accepted'),
    color: 'bg-emerald-500/20 text-emerald-400',
    icon: CheckCircle,
  },
  rejected: { label: t('status.rejected'), color: 'bg-red-500/20 text-red-400', icon: XCircle },
})

export default function CandidateApply() {
  const { t } = useTranslation('pages/candidateApply')
  const statusMap = statusConfig(t)

  return (
    <div className="min-h-screen bg-bg-primary">
      <CandidateNav />
      <div className="pt-20 pb-8 px-6 lg:px-8 max-w-6xl mx-auto">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-8">
          <h1 className="text-3xl font-semibold text-white mb-2">{t('title')}</h1>
          <p className="text-text-secondary">{t('subtitle')}</p>
        </motion.div>

        {/* Stats */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
          {[
            {
              label: t('stats.total'),
              value: applications.length.toString(),
              color: 'text-blue-400',
            },
            {
              label: t('stats.pending'),
              value: applications
                .filter((a) => a.status === 'pending' || a.status === 'reviewing')
                .length.toString(),
              color: 'text-amber-400',
            },
            {
              label: t('stats.interview'),
              value: applications.filter((a) => a.status === 'interview').length.toString(),
              color: 'text-violet-400',
            },
            {
              label: t('stats.accepted'),
              value: applications.filter((a) => a.status === 'accepted').length.toString(),
              color: 'text-emerald-400',
            },
          ].map((stat) => (
            <GlassCard key={stat.label} className="p-4" hoverEnabled={false}>
              <p className={`text-2xl font-semibold ${stat.color}`}>{stat.value}</p>
              <p className="text-sm text-text-secondary">{stat.label}</p>
            </GlassCard>
          ))}
        </div>

        {/* Applications List */}
        <div className="space-y-4">
          {applications.map((app, index) => {
            const status = statusMap[app.status as keyof typeof statusMap]
            const StatusIcon = status.icon

            return (
              <motion.div
                key={app.id}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: index * 0.05 }}
              >
                <GlassCard className="p-6" hoverEnabled={false}>
                  <div className="flex items-center gap-6">
                    <div className="w-14 h-14 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-lg font-semibold text-white">
                      {app.logo}
                    </div>
                    <div className="flex-1">
                      <h3 className="text-lg font-semibold text-white mb-1">{app.jobTitle}</h3>
                      <p className="text-sm text-text-secondary mb-2">{app.company}</p>
                      <div className="flex items-center gap-4 text-xs text-text-tertiary">
                        <span>
                          {t('labels.appliedDate')} {app.appliedDate}
                        </span>
                        {app.interviewDate && (
                          <span>
                            {t('labels.interviewDate')} {app.interviewDate}
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="text-right">
                      <span
                        className={`inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full text-sm font-medium ${status.color}`}
                      >
                        <StatusIcon className="w-4 h-4" />
                        {status.label}
                      </span>
                      {app.score && (
                        <p className="text-sm text-white mt-2">
                          {t('labels.score')} {app.score}
                        </p>
                      )}
                    </div>
                  </div>
                </GlassCard>
              </motion.div>
            )
          })}
        </div>
      </div>
    </div>
  )
}
