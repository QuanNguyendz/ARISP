import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { motion } from 'framer-motion'
import {
  BookOpen,
  Rocket,
  Workflow,
  ClipboardList,
  Download,
  CircleHelp,
  LifeBuoy,
  ChevronDown,
  Scale,
  Scissors,
  FileText,
} from 'lucide-react'
import { PageHeader } from '@ari/shared/ui'
import { PLAYBOOK_TEMPLATE, PLAYBOOK_TEMPLATE_FILENAME } from './playbookTemplate'

type HelpRole = 'hr_admin' | 'recruiter'

interface HelpCenterViewProps {
  /** Quyết định phần nội dung riêng của vai trò; chỉ HR Lead có mục Playbook. */
  role: HelpRole
}

/** Bảng trọng số retrieve theo phạm vi — khớp `_PLAYBOOK_SCOPE_WEIGHT` trong rag-service. */
const SCOPE_WEIGHTS: Record<string, string> = {
  org: '0.6',
  jobPosting: '1.0',
  round: '1.0',
}

const DOC_TYPE_KEYS = [
  'styleGuide',
  'questionBank',
  'competencyFramework',
  'cultureGuide',
  'compliance',
  'redFlag',
  'technicalScenario',
  'expectedAnswer',
  'mustAsk',
  'roundPlaybook',
] as const

function Section({
  icon,
  title,
  description,
  children,
  delay,
}: {
  icon: React.ReactNode
  title: string
  description?: string
  children: React.ReactNode
  delay: number
}) {
  return (
    <motion.section
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay }}
      className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 sm:p-6 shadow-card"
    >
      <div className="mb-4 flex items-start gap-3">
        <span className="grid h-10 w-10 shrink-0 place-items-center rounded-xl bg-gradient-to-br from-brand-600 to-ai-600 text-white">
          {icon}
        </span>
        <div className="min-w-0">
          <h2 className="text-lg font-semibold text-ink-900 dark:text-white">{title}</h2>
          {description && (
            <p className="mt-0.5 text-sm text-ink-500 dark:text-ink-400">{description}</p>
          )}
        </div>
      </div>
      {children}
    </motion.section>
  )
}

/** Danh sách bước có đánh số — dùng cho "bắt đầu nhanh" và luồng tuyển dụng. */
function StepList({ steps }: { steps: string[] }) {
  return (
    <ol className="space-y-2.5">
      {steps.map((step, index) => (
        <li key={index} className="flex gap-3 text-sm">
          <span className="mt-0.5 grid h-6 w-6 shrink-0 place-items-center rounded-full bg-brand-100 dark:bg-brand-500/20 text-xs font-semibold text-brand-700 dark:text-brand-400">
            {index + 1}
          </span>
          <span className="text-ink-600 dark:text-ink-300">{step}</span>
        </li>
      ))}
    </ol>
  )
}

function FaqItem({ question, answer }: { question: string; answer: string }) {
  return (
    <details className="group rounded-xl border border-ink-200 dark:border-white/10">
      <summary className="flex cursor-pointer items-center justify-between gap-3 px-4 py-3 text-sm font-medium text-ink-800 dark:text-ink-200">
        {question}
        <ChevronDown className="h-4 w-4 shrink-0 text-ink-400 transition group-open:rotate-180" />
      </summary>
      <p className="border-t border-ink-100 dark:border-white/10 px-4 py-3 text-sm leading-6 text-ink-600 dark:text-ink-300">
        {answer}
      </p>
    </details>
  )
}

export default function HelpCenterView({ role }: HelpCenterViewProps) {
  const { t } = useTranslation('modules/shared/help')
  const [downloaded, setDownloaded] = useState(false)

  /**
   * i18next trả `unknown` khi dùng returnObjects; ép về string[] có kiểm tra thay vì dùng `any`
   * (quy ước FE cấm `any`). Khoá thiếu/sai kiểu → mảng rỗng, không làm vỡ trang.
   */
  const list = useMemo(
    () => (key: string): string[] => {
      const value = t(key, { returnObjects: true })
      return Array.isArray(value) ? (value as string[]) : []
    },
    [t]
  )

  const isHrLead = role === 'hr_admin'
  const basePath = isHrLead ? '/hr' : '/recruiter'

  const faqs = useMemo(() => {
    const questions = list(isHrLead ? 'faq.hr.questions' : 'faq.recruiter.questions')
    const answers = list(isHrLead ? 'faq.hr.answers' : 'faq.recruiter.answers')
    return questions.map((question, index) => ({ question, answer: answers[index] ?? '' }))
  }, [list, isHrLead])

  const downloadTemplate = () => {
    // Trình duyệt tải thẳng từ chuỗi trong bundle — không cần endpoint, không cần mạng.
    const blob = new Blob([PLAYBOOK_TEMPLATE], { type: 'text/markdown;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const anchor = document.createElement('a')
    anchor.href = url
    anchor.download = PLAYBOOK_TEMPLATE_FILENAME
    document.body.appendChild(anchor)
    anchor.click()
    document.body.removeChild(anchor)
    URL.revokeObjectURL(url)
    setDownloaded(true)
  }

  return (
    <div className="min-h-screen bg-ink-50 dark:bg-ink-950 p-4 sm:p-6 lg:p-8">
      <PageHeader
        title={t('title')}
        description={t('subtitle')}
        badge={{ text: isHrLead ? t('roles.hrLead') : t('roles.recruiter') }}
      />

      <div className="grid gap-5 lg:grid-cols-2">
        <Section
          icon={<Rocket className="h-5 w-5" />}
          title={t('quickStart.title')}
          description={t('quickStart.subtitle')}
          delay={0.05}
        >
          <StepList steps={list(isHrLead ? 'quickStart.hrSteps' : 'quickStart.recruiterSteps')} />
        </Section>

        <Section
          icon={<Workflow className="h-5 w-5" />}
          title={t('flow.title')}
          description={t('flow.subtitle')}
          delay={0.1}
        >
          <StepList steps={list('flow.phases')} />
        </Section>
      </div>

      <div className="mt-5">
        <Section
          icon={<ClipboardList className="h-5 w-5" />}
          title={t('duties.title')}
          description={isHrLead ? t('duties.hrSubtitle') : t('duties.recruiterSubtitle')}
          delay={0.15}
        >
          <div className="grid gap-3 sm:grid-cols-2">
            {list(isHrLead ? 'duties.hrItems' : 'duties.recruiterItems').map((item) => (
              <div
                key={item}
                className="rounded-xl bg-ink-50 dark:bg-white/5 p-3 text-sm text-ink-600 dark:text-ink-300"
              >
                {item}
              </div>
            ))}
          </div>
        </Section>
      </div>

      {/* Playbook chỉ dành cho HR Lead — Recruiter không có quyền upload (PlaybooksController). */}
      {isHrLead && (
        <div className="mt-5 space-y-5">
          <Section
            icon={<BookOpen className="h-5 w-5" />}
            title={t('playbook.title')}
            description={t('playbook.subtitle')}
            delay={0.2}
          >
            <p className="mb-5 text-sm leading-6 text-ink-600 dark:text-ink-300">
              {t('playbook.intro')}
            </p>

            <h3 className="mb-2 flex items-center gap-2 text-sm font-semibold text-ink-800 dark:text-ink-200">
              <Scale className="h-4 w-4 text-ai-600 dark:text-ai-400" />
              {t('playbook.scopes.title')}
            </h3>
            <p className="mb-3 text-sm text-ink-500 dark:text-ink-400">
              {t('playbook.scopes.note')}
            </p>
            <div className="mb-6 overflow-x-auto">
              <table className="w-full min-w-[520px] text-left text-sm">
                <thead>
                  <tr className="border-b border-ink-200 dark:border-white/10 text-xs uppercase tracking-wide text-ink-400">
                    <th className="pb-2 pr-3 font-medium">{t('playbook.scopes.colScope')}</th>
                    <th className="pb-2 pr-3 font-medium">{t('playbook.scopes.colWhen')}</th>
                    <th className="pb-2 font-medium">{t('playbook.scopes.colWeight')}</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-ink-100 dark:divide-white/5">
                  {(['org', 'jobPosting', 'round'] as const).map((scope) => (
                    <tr key={scope}>
                      <td className="py-2.5 pr-3 font-medium text-ink-800 dark:text-ink-200">
                        {t(`playbook.scopes.${scope}.name`)}
                      </td>
                      <td className="py-2.5 pr-3 text-ink-600 dark:text-ink-300">
                        {t(`playbook.scopes.${scope}.when`)}
                      </td>
                      <td className="py-2.5">
                        <span className="rounded-full bg-ai-50 dark:bg-ai-500/20 px-2.5 py-0.5 text-xs font-semibold text-ai-700 dark:text-ai-400">
                          {SCOPE_WEIGHTS[scope]}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <h3 className="mb-2 flex items-center gap-2 text-sm font-semibold text-ink-800 dark:text-ink-200">
              <FileText className="h-4 w-4 text-ai-600 dark:text-ai-400" />
              {t('playbook.docTypes.title')}
            </h3>
            <p className="mb-3 text-sm text-ink-500 dark:text-ink-400">
              {t('playbook.docTypes.note')}
            </p>
            <div className="grid gap-2 sm:grid-cols-2">
              {DOC_TYPE_KEYS.map((key) => (
                <div
                  key={key}
                  className="rounded-xl border border-ink-200 dark:border-white/10 p-3 text-sm"
                >
                  <div className="font-medium text-ink-800 dark:text-ink-200">
                    {t(`playbook.docTypes.${key}.name`)}
                  </div>
                  <p className="mt-0.5 text-xs leading-5 text-ink-500 dark:text-ink-400">
                    {t(`playbook.docTypes.${key}.desc`)}
                  </p>
                </div>
              ))}
            </div>
          </Section>

          <Section
            icon={<Scissors className="h-5 w-5" />}
            title={t('playbook.chunking.title')}
            description={t('playbook.chunking.subtitle')}
            delay={0.25}
          >
            <div className="mb-5 rounded-xl border border-amber-200 dark:border-amber-500/30 bg-amber-50 dark:bg-amber-500/10 p-4 text-sm leading-6 text-amber-800 dark:text-amber-300">
              {t('playbook.chunking.warning')}
            </div>
            <StepList steps={list('playbook.chunking.rules')} />

            <div className="mt-6 grid gap-4 sm:grid-cols-2">
              <div className="rounded-xl border border-red-200 dark:border-red-500/30 bg-red-50/60 dark:bg-red-500/10 p-4">
                <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-red-700 dark:text-red-400">
                  {t('playbook.chunking.badTitle')}
                </div>
                <pre className="whitespace-pre-wrap break-words font-mono text-xs leading-5 text-ink-700 dark:text-ink-300">
                  {t('playbook.chunking.badExample')}
                </pre>
                <p className="mt-2 text-xs leading-5 text-red-700 dark:text-red-400">
                  {t('playbook.chunking.badWhy')}
                </p>
              </div>
              <div className="rounded-xl border border-emerald-200 dark:border-emerald-500/30 bg-emerald-50/60 dark:bg-emerald-500/10 p-4">
                <div className="mb-2 text-xs font-semibold uppercase tracking-wide text-emerald-700 dark:text-emerald-400">
                  {t('playbook.chunking.goodTitle')}
                </div>
                <pre className="whitespace-pre-wrap break-words font-mono text-xs leading-5 text-ink-700 dark:text-ink-300">
                  {t('playbook.chunking.goodExample')}
                </pre>
                <p className="mt-2 text-xs leading-5 text-emerald-700 dark:text-emerald-400">
                  {t('playbook.chunking.goodWhy')}
                </p>
              </div>
            </div>
          </Section>

          <Section
            icon={<Download className="h-5 w-5" />}
            title={t('playbook.template.title')}
            description={t('playbook.template.subtitle')}
            delay={0.3}
          >
            <p className="mb-4 text-sm leading-6 text-ink-600 dark:text-ink-300">
              {t('playbook.template.intro')}
            </p>
            <ul className="mb-5 space-y-2">
              {list('playbook.template.highlights').map((item) => (
                <li
                  key={item}
                  className="flex gap-2 text-sm text-ink-600 dark:text-ink-300"
                >
                  <span className="mt-2 h-1.5 w-1.5 shrink-0 rounded-full bg-brand-500" />
                  {item}
                </li>
              ))}
            </ul>
            <div className="flex flex-wrap items-center gap-3">
              <button
                type="button"
                onClick={downloadTemplate}
                className="inline-flex items-center gap-2 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-4 py-2.5 text-sm font-semibold text-white transition-opacity hover:opacity-90"
              >
                <Download className="h-4 w-4" />
                {t('playbook.template.download')}
              </button>
              <Link
                to={`${basePath}/playbooks`}
                className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-4 py-2.5 text-sm font-semibold text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10"
              >
                <BookOpen className="h-4 w-4" />
                {t('playbook.template.goToPlaybooks')}
              </Link>
              {downloaded && (
                <span className="text-sm text-emerald-600 dark:text-emerald-400">
                  {t('playbook.template.downloaded')}
                </span>
              )}
            </div>
          </Section>
        </div>
      )}

      <div className="mt-5 grid gap-5 lg:grid-cols-2">
        <Section
          icon={<CircleHelp className="h-5 w-5" />}
          title={t('faq.title')}
          delay={0.35}
        >
          <div className="space-y-2">
            {faqs.map((faq) => (
              <FaqItem key={faq.question} question={faq.question} answer={faq.answer} />
            ))}
          </div>
        </Section>

        <Section
          icon={<LifeBuoy className="h-5 w-5" />}
          title={t('support.title')}
          description={t('support.subtitle')}
          delay={0.4}
        >
          <div className="space-y-2">
            {list('support.items').map((item) => (
              <div
                key={item}
                className="rounded-xl bg-ink-50 dark:bg-white/5 p-3 text-sm text-ink-600 dark:text-ink-300"
              >
                {item}
              </div>
            ))}
          </div>
        </Section>
      </div>
    </div>
  )
}
