import { motion } from 'framer-motion'
import { useTranslation } from 'react-i18next'
import { Download, TrendingUp, Users, Clock, FileText } from 'lucide-react'

export default function ReportsPage() {
  const { t } = useTranslation('modules/hr/reports')

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-8">
        <h1 className="text-3xl font-semibold text-ink-900 dark:text-white mb-2">{t('title')}</h1>
        <p className="text-ink-600 dark:text-ink-400">{t('description')}</p>
      </motion.div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
        {[
          {
            label: t('stats.totalCandidates'),
            value: '1,234',
            icon: Users,
            color: 'text-blue-600 dark:text-blue-400',
          },
          {
            label: t('stats.hired'),
            value: '89',
            icon: TrendingUp,
            color: 'text-emerald-600 dark:text-emerald-400',
          },
          {
            label: t('stats.avgTimeToHire'),
            value: '4.2 ' + t('days'),
            icon: Clock,
            color: 'text-amber-600 dark:text-amber-400',
          },
          {
            label: t('stats.passRate'),
            value: '68%',
            icon: FileText,
            color: 'text-violet-600 dark:text-violet-400',
          },
        ].map((stat) => (
          <motion.div
            key={stat.label}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-card"
          >
            <stat.icon className={`w-5 h-5 ${stat.color} mb-2`} />
            <p className={`text-2xl font-semibold ${stat.color}`}>{stat.value}</p>
            <p className="text-sm text-ink-600 dark:text-ink-400">{stat.label}</p>
          </motion.div>
        ))}
      </div>

      <div className="space-y-4">
        <h2 className="text-lg font-semibold text-ink-900 dark:text-white mb-4">
          {t('recentReports')}
        </h2>
        {[
          {
            id: '1',
            title: t('report1.title'),
            description: t('report1.description'),
            date: '27/05/2026',
          },
          {
            id: '2',
            title: t('report2.title'),
            description: t('report2.description'),
            date: '25/05/2026',
          },
          {
            id: '3',
            title: t('report3.title'),
            description: t('report3.description'),
            date: '20/05/2026',
          },
        ].map((report, index) => (
          <motion.div
            key={report.id}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: index * 0.05 }}
          >
            <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card flex items-center justify-between hover:shadow-card-hover transition-all">
              <div className="flex items-center gap-4">
                <div className="w-12 h-12 rounded-xl bg-brand-100 dark:bg-brand-500/20 flex items-center justify-center">
                  <FileText className="w-6 h-6 text-brand-600 dark:text-brand-400" />
                </div>
                <div>
                  <h3 className="text-lg font-semibold text-ink-900 dark:text-white">
                    {report.title}
                  </h3>
                  <p className="text-sm text-ink-500 dark:text-ink-400">{report.description}</p>
                  <p className="text-xs text-ink-400 dark:text-ink-500 mt-1">{report.date}</p>
                </div>
              </div>
              <button className="flex items-center gap-2 px-4 py-2 rounded-lg border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10 transition-colors text-sm font-medium">
                <Download className="w-4 h-4" />
                {t('download')}
              </button>
            </div>
          </motion.div>
        ))}
      </div>
    </div>
  )
}
