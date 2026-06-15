import { Link } from 'react-router-dom'
import {
  Download,
  TrendingUp,
  Gavel,
  FileText,
  Sparkles,
  KeyRound,
  ChevronRight,
  CalendarDays,
  ArrowRight,
  Clock,
  Briefcase,
  Users,
  Video,
} from 'lucide-react'

// KPI Card
function KpiCard({
  icon: Icon,
  label,
  value,
  change,
  changeType,
}: {
  icon: any
  label: string
  value: string
  change: string
  changeType?: 'positive' | 'neutral'
}) {
  return (
    <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card transition hover:shadow-card-hover">
      <div className="flex items-center justify-between">
        <span className="flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400">
          <span
            className={`grid h-9 w-9 place-items-center rounded-xl ${
              changeType === 'positive'
                ? 'bg-brand-50 dark:bg-brand-500/20 text-brand-600 dark:text-brand-400'
                : 'bg-emerald-50 dark:bg-emerald-500/20 text-emerald-600 dark:text-emerald-400'
            }`}
          >
            <Icon className="w-[18px] h-[18px]" />
          </span>
          {label}
        </span>
        {changeType === 'positive' && (
          <span className="inline-flex items-center gap-0.5 rounded-full bg-emerald-50 dark:bg-emerald-500/20 px-1.5 py-0.5 text-xs font-semibold text-emerald-600 dark:text-emerald-400">
            <TrendingUp className="w-3 h-3" /> {change}
          </span>
        )}
      </div>
      <div className="mt-4 flex items-end justify-between">
        <div className="font-display text-3xl font-extrabold leading-none text-ink-900 dark:text-white">
          {value}
        </div>
        <svg viewBox="0 0 72 28" className="h-7 w-20 text-brand-500">
          <polyline
            fill="none"
            stroke="currentColor"
            strokeWidth="2.5"
            strokeLinecap="round"
            strokeLinejoin="round"
            points="0,22 12,18 24,20 36,12 48,14 60,8 72,4"
          />
        </svg>
      </div>
    </div>
  )
}

// Pending card requiring action
function PendingCard() {
  return (
    <div className="flex flex-col rounded-2xl border border-amber-300 dark:border-amber-500/30 bg-gradient-to-br from-amber-50 to-white dark:from-amber-500/10 dark:to-transparent p-5 shadow-card">
      <div className="flex items-center justify-between">
        <span className="flex items-center gap-2 text-sm font-medium text-amber-700 dark:text-amber-400">
          <span className="grid h-9 w-9 place-items-center rounded-xl bg-amber-100 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400">
            <Gavel className="w-[18px] h-[18px]" />
          </span>
          Chờ xác nhận
        </span>
        <span className="rounded-full bg-amber-200 dark:bg-amber-500/30 px-2 py-0.5 text-xs font-bold text-amber-800 dark:text-amber-300">
          Cần xử lý
        </span>
      </div>
      <div className="mt-4 font-display text-3xl font-extrabold leading-none text-amber-700 dark:text-amber-400">
        5
      </div>
      <div className="mt-1 text-sm text-amber-700/80 dark:text-amber-400/70">
        Verdict chờ HR xác nhận
      </div>
      <Link
        to="/hr/evaluations"
        className="mt-3 inline-flex items-center gap-1 text-sm font-semibold text-amber-700 dark:text-amber-400 transition-all hover:gap-2"
      >
        Xử lý ngay <ArrowRight className="w-4 h-4" />
      </Link>
    </div>
  )
}

export default function HrDashboardPage() {
  return (
    <main className="p-6 space-y-6 bg-ink-50 dark:bg-ink-950">
      {/* Page header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="font-display text-2xl font-extrabold leading-snug text-ink-900 dark:text-white">
            Xin chào, Quân 👋
          </h1>
          <p className="mt-1 flex items-center gap-2 text-sm text-ink-500 dark:text-ink-400">
            <CalendarDays className="w-4 h-4" />
            Thứ Hai, 15/06/2026 · Tổng quan tuyển dụng
          </p>
        </div>
        <div className="flex items-center gap-2">
          <div className="inline-flex rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-0.5 text-sm shadow-sm">
            <button className="rounded-lg bg-brand-600 px-3 py-1.5 font-medium text-white">
              Hôm nay
            </button>
            <button className="rounded-lg px-3 py-1.5 font-medium text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10">
              7 ngày
            </button>
            <button className="rounded-lg px-3 py-1.5 font-medium text-ink-600 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10">
              30 ngày
            </button>
          </div>
          <button className="inline-flex items-center gap-2 rounded-xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 px-3 py-2 text-sm font-semibold text-ink-700 dark:text-ink-200 hover:bg-ink-50 dark:hover:bg-white/10">
            <Download className="w-4 h-4" />
            <span className="hidden sm:inline">Xuất báo cáo</span>
          </button>
        </div>
      </div>

      {/* KPI */}
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <KpiCard
          icon={Briefcase}
          label="Tin đang tuyển"
          value="12"
          change="+3"
          changeType="positive"
        />
        <KpiCard
          icon={Users}
          label="Ứng viên đang xử lý"
          value="248"
          change="+12%"
          changeType="positive"
        />
        <KpiCard icon={Video} label="Phỏng vấn AI" value="18" change="" changeType="neutral" />
        <PendingCard />
      </div>

      {/* Recruitment funnel */}
      <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card">
        <div className="mb-5 flex items-center justify-between">
          <div>
            <h2 className="font-display text-lg font-bold text-ink-900 dark:text-white">
              Phễu tuyển dụng
            </h2>
            <p className="text-sm text-ink-400 dark:text-ink-500">
              30 ngày gần nhất · tỉ lệ chuyển đổi qua từng bước
            </p>
          </div>
          <span className="hidden sm:inline-flex items-center gap-1 rounded-lg bg-emerald-50 dark:bg-emerald-500/20 px-2.5 py-1 text-xs font-semibold text-emerald-700 dark:text-emerald-400">
            <TrendingUp className="w-3.5 h-3.5" /> 2.4% offer rate
          </span>
        </div>
        <div className="space-y-3">
          {[
            { label: 'Ứng tuyển', value: 248, percent: 100 },
            { label: 'Sàng lọc CV–JD', value: 132, percent: 53 },
            { label: 'Phỏng vấn AI', value: 64, percent: 26 },
            { label: 'Đề xuất (HR)', value: 18, percent: 9 },
            { label: 'Tuyển', value: 6, percent: 4 },
          ].map((item, i) => (
            <div key={item.label} className="flex items-center gap-3">
              <div className="w-32 shrink-0 text-sm font-medium text-ink-600 dark:text-ink-400">
                {item.label}
              </div>
              <div className="h-8 flex-1 rounded-lg bg-ink-100 dark:bg-white/10">
                <div
                  className={`h-full rounded-lg ${
                    i === 4
                      ? 'bg-gradient-to-r from-emerald-500 to-emerald-600'
                      : 'bg-gradient-to-r from-brand-600 to-ai-600'
                  }`}
                  style={{ width: `${item.percent}%` }}
                ></div>
              </div>
              <div className="w-10 shrink-0 text-right text-sm font-bold text-ink-900 dark:text-white">
                {item.value}
              </div>
              <div className="w-12 shrink-0 text-right text-xs text-ink-400 dark:text-ink-500">
                {i === 0 ? '—' : `${item.percent}%`}
              </div>
            </div>
          ))}
        </div>
      </div>

      <div className="grid gap-6 xl:grid-cols-[1fr_360px]">
        {/* Pipeline table */}
        <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 shadow-card overflow-hidden">
          <div className="flex flex-wrap items-center justify-between gap-3 px-5 py-4 border-b border-ink-100 dark:border-white/10">
            <h2 className="font-display font-bold text-ink-900 dark:text-white">
              Ứng viên gần đây
            </h2>
            <div className="flex items-center gap-1 text-xs">
              <button className="rounded-full bg-brand-600 px-2.5 py-1 font-medium text-white">
                Tất cả
              </button>
              <button className="rounded-full px-2.5 py-1 font-medium text-ink-500 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10">
                Chờ duyệt
              </button>
              <button className="rounded-full px-2.5 py-1 font-medium text-ink-500 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10">
                Pass
              </button>
              <button className="rounded-full px-2.5 py-1 font-medium text-ink-500 dark:text-ink-400 hover:bg-ink-100 dark:hover:bg-white/10">
                Not Pass
              </button>
              <Link
                to="/hr/candidates"
                className="ml-1 font-medium text-brand-600 dark:text-brand-400 hover:underline"
              >
                Xem tất cả
              </Link>
            </div>
          </div>
          <table className="w-full text-sm">
            <thead className="text-left text-ink-400 bg-ink-50/50 dark:bg-white/5">
              <tr>
                <th className="font-medium px-5 py-3 text-ink-600 dark:text-ink-400">Ứng viên</th>
                <th className="font-medium px-5 py-3 text-ink-600 dark:text-ink-400">Vị trí</th>
                <th className="font-medium px-5 py-3 text-ink-600 dark:text-ink-400">Vòng</th>
                <th className="font-medium px-5 py-3 text-ink-600 dark:text-ink-400">Verdict AI</th>
                <th className="font-medium px-5 py-3"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-ink-100 dark:divide-white/10">
              {[
                {
                  initials: 'LH',
                  name: 'Lê Hoàng',
                  role: 'Frontend Engineer',
                  round: 'R2 · Technical',
                  badge: 'brand',
                  verdict: 'Pass',
                  verdictType: 'emerald',
                },
                {
                  initials: 'TM',
                  name: 'Trần Minh',
                  role: 'AI/ML Engineer',
                  round: 'R1 · Screening',
                  badge: 'ink',
                  verdict: 'Chờ duyệt',
                  verdictType: 'amber',
                },
                {
                  initials: 'PA',
                  name: 'Phạm An',
                  role: 'Backend Developer',
                  round: 'R1 · Screening',
                  badge: 'ink',
                  verdict: 'Not Pass',
                  verdictType: 'red',
                },
              ].map((c) => (
                <tr key={c.name} className="hover:bg-ink-50/60 dark:hover:bg-white/5">
                  <td className="px-5 py-3">
                    <div className="flex items-center gap-3">
                      <div
                        className={`grid h-9 w-9 place-items-center rounded-full bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400 text-xs font-bold`}
                      >
                        {c.initials}
                      </div>
                      <div>
                        <div className="font-medium text-ink-900 dark:text-white">{c.name}</div>
                        <div className="text-xs text-ink-400">Match 92</div>
                      </div>
                    </div>
                  </td>
                  <td className="px-5 py-3 text-ink-600 dark:text-ink-400">{c.role}</td>
                  <td className="px-5 py-3">
                    <span
                      className={`rounded-full px-2.5 py-1 text-xs font-semibold ${
                        c.badge === 'brand'
                          ? 'bg-brand-50 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400'
                          : 'bg-ink-100 dark:bg-white/10 text-ink-600 dark:text-ink-400'
                      }`}
                    >
                      {c.round}
                    </span>
                  </td>
                  <td className="px-5 py-3">
                    <span
                      className={`inline-flex items-center gap-1 rounded-full px-2.5 py-1 text-xs font-semibold ${
                        c.verdictType === 'emerald'
                          ? 'bg-emerald-50 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
                          : c.verdictType === 'amber'
                            ? 'bg-amber-50 dark:bg-amber-500/20 text-amber-700 dark:text-amber-400'
                            : 'bg-red-50 dark:bg-red-500/20 text-red-700 dark:text-red-400'
                      }`}
                    >
                      {c.verdictType === 'emerald' && <span className="w-3.5 h-3.5">✓</span>}
                      {c.verdictType === 'amber' && <Clock className="w-3.5 h-3.5" />}
                      {c.verdictType === 'red' && <span className="w-3.5 h-3.5">✕</span>}
                      {c.verdict}
                    </span>
                  </td>
                  <td className="px-5 py-3 text-right">
                    <Link
                      to="/hr/evaluations"
                      className={`rounded-lg px-3 py-1.5 text-xs font-semibold ${
                        c.verdictType === 'amber'
                          ? 'bg-brand-600 text-white hover:bg-brand-700'
                          : 'text-brand-600 hover:bg-brand-50'
                      }`}
                    >
                      {c.verdictType === 'amber' ? 'Duyệt' : 'Xem'}
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        {/* Side */}
        <div className="space-y-5">
          {/* Lịch phỏng vấn hôm nay */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
            <div className="mb-3 flex items-center justify-between">
              <h3 className="font-display font-bold text-ink-900 dark:text-white">
                Phỏng vấn hôm nay
              </h3>
              <span className="rounded-full bg-ink-100 dark:bg-white/10 px-2 py-0.5 text-xs font-semibold text-ink-600 dark:text-ink-400">
                4
              </span>
            </div>
            <div className="space-y-0.5">
              {[
                {
                  time: '09:00',
                  initials: 'LH',
                  name: 'Lê Hoàng',
                  role: 'Frontend · R2 Technical',
                  type: 'Real',
                  typeColor: 'emerald',
                },
                {
                  time: '11:30',
                  initials: 'TM',
                  name: 'Trần Minh',
                  role: 'AI/ML · R1 Screening',
                  type: 'Practice',
                  typeColor: 'ink',
                },
                {
                  time: '14:00',
                  initials: 'NV',
                  name: 'Ngô Vy',
                  role: 'Backend · R1 Screening',
                  type: 'Real',
                  typeColor: 'emerald',
                },
              ].map((item) => (
                <div
                  key={item.name}
                  className="flex items-center gap-3 rounded-xl px-2 py-2 hover:bg-ink-50 dark:hover:bg-white/5"
                >
                  <div className="w-12 shrink-0 text-sm font-semibold text-ink-700 dark:text-ink-300">
                    {item.time}
                  </div>
                  <div
                    className={`grid h-8 w-8 shrink-0 place-items-center rounded-full bg-brand-100 dark:bg-brand-500/20 text-xs font-bold text-brand-700 dark:text-brand-400`}
                  >
                    {item.initials}
                  </div>
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-sm font-medium text-ink-900 dark:text-white">
                      {item.name}
                    </div>
                    <div className="truncate text-xs text-ink-400">{item.role}</div>
                  </div>
                  <span
                    className={`shrink-0 rounded-full px-2 py-0.5 text-[11px] font-semibold ${
                      item.typeColor === 'emerald'
                        ? 'bg-emerald-50 dark:bg-emerald-500/20 text-emerald-700 dark:text-emerald-400'
                        : 'bg-ink-100 dark:bg-white/10 text-ink-500 dark:text-ink-400'
                    }`}
                  >
                    {item.type}
                  </span>
                </div>
              ))}
            </div>
          </div>

          {/* AI Insight */}
          <div className="rounded-2xl border border-ai-200 dark:border-ai-500/30 bg-gradient-to-b from-ai-50/70 to-white dark:from-ai-500/10 dark:to-transparent p-5 shadow-card">
            <div className="flex items-center gap-2 text-sm font-semibold text-ai-700 dark:text-ai-400">
              <Sparkles className="w-4 h-4" />
              AI Insight
            </div>
            <p className="mt-2 text-sm text-ink-600 dark:text-ink-300 leading-relaxed">
              3 ứng viên cho vị trí <b className="text-ink-900 dark:text-white">Frontend</b> đạt
              Match &gt; 85 nhưng chưa được mời phỏng vấn. Cân nhắc gửi magic link.
            </p>
            <button className="mt-3 w-full rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 px-3 py-2 text-sm font-semibold text-white">
              Xem danh sách
            </button>
          </div>

          {/* Cần làm */}
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-card">
            <h3 className="font-display font-bold mb-3 text-ink-900 dark:text-white">Cần làm</h3>
            <div className="space-y-1 text-sm">
              <Link
                to="/hr/evaluations"
                className="flex items-center gap-3 rounded-xl px-2 py-2 hover:bg-ink-50 dark:hover:bg-white/5"
              >
                <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-amber-50 dark:bg-amber-500/20 text-amber-600 dark:text-amber-400">
                  <Gavel className="w-4 h-4" />
                </span>
                <span className="flex-1 text-ink-700 dark:text-ink-200">
                  Xác nhận verdict phỏng vấn
                </span>
                <span className="rounded-full bg-amber-100 dark:bg-amber-500/20 px-1.5 text-xs font-bold text-amber-700 dark:text-amber-400">
                  5
                </span>
                <ChevronRight className="w-4 h-4 text-ink-300 dark:text-ink-500" />
              </Link>
              <Link
                to="/hr/interviews"
                className="flex items-center gap-3 rounded-xl px-2 py-2 hover:bg-ink-50 dark:hover:bg-white/5"
              >
                <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-brand-50 dark:bg-brand-500/20 text-brand-600 dark:text-brand-400">
                  <KeyRound className="w-4 h-4" />
                </span>
                <span className="flex-1 text-ink-700 dark:text-ink-200">
                  Cấp Interview Code (R2)
                </span>
                <span className="rounded-full bg-brand-100 dark:bg-brand-500/20 px-1.5 text-xs font-bold text-brand-700 dark:text-brand-400">
                  2
                </span>
                <ChevronRight className="w-4 h-4 text-ink-300 dark:text-ink-500" />
              </Link>
              <Link
                to="/hr/jobs/pending"
                className="flex items-center gap-3 rounded-xl px-2 py-2 hover:bg-ink-50 dark:hover:bg-white/5"
              >
                <span className="grid h-8 w-8 shrink-0 place-items-center rounded-lg bg-ink-100 dark:bg-white/10 text-ink-500 dark:text-ink-400">
                  <FileText className="w-4 h-4" />
                </span>
                <span className="flex-1 text-ink-700 dark:text-ink-200">
                  Duyệt tin nháp của Recruiter
                </span>
                <span className="rounded-full bg-ink-100 dark:bg-white/10 px-1.5 text-xs font-bold text-ink-600 dark:text-ink-400">
                  1
                </span>
                <ChevronRight className="w-4 h-4 text-ink-300 dark:text-ink-500" />
              </Link>
            </div>
          </div>
        </div>
      </div>
    </main>
  )
}
