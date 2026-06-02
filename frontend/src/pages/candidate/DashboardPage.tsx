import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import {
  Briefcase, FileText, Calendar, CheckCircle, Clock,
  Eye, MapPin, DollarSign, ArrowRight,
  TrendingUp, Target, Award
} from 'lucide-react';
import { useAuthStore } from '@store/auth';

const STATS = [
  { icon: Briefcase, label: 'Việc đã ứng tuyển', value: '12', color: 'from-accent-primary to-violet' },
  { icon: Clock, label: 'Đang chờ duyệt', value: '5', color: 'from-amber-500 to-orange-500' },
  { icon: Calendar, label: 'Phỏng vấn sắp tới', value: '2', color: 'from-emerald-500 to-teal-500' },
  { icon: Award, label: 'Nhận việc', value: '1', color: 'from-blue-500 to-cyan-500' },
];

const APPLICATION_STATUS: Record<string, { label: string; color: string; icon: typeof Clock }> = {
  pending: { label: 'Chờ duyệt', color: 'bg-amber-500/20 text-amber-400', icon: Clock },
  reviewing: { label: 'Đang xem', color: 'bg-blue-500/20 text-blue-400', icon: Eye },
  interview: { label: 'Phỏng vấn', color: 'bg-emerald-500/20 text-emerald-400', icon: Calendar },
  rejected: { label: 'Từ chối', color: 'bg-red-500/20 text-red-400', icon: FileText },
  accepted: { label: 'Nhận việc', color: 'bg-violet-500/20 text-violet-400', icon: CheckCircle },
};

const applications = [
  {
    id: '1',
    title: 'Senior Frontend Developer',
    company: 'TechVision Corp',
    logo: 'TV',
    location: 'TP. Hồ Chí Minh',
    salary: '$3,000 - $5,000',
    appliedDate: '20/05/2026',
    status: 'interview',
  },
  {
    id: '2',
    title: 'Backend Engineer',
    company: 'DataFlow Systems',
    logo: 'DF',
    location: 'Hà Nội',
    salary: '$2,500 - $4,000',
    appliedDate: '18/05/2026',
    status: 'reviewing',
  },
  {
    id: '3',
    title: 'UI/UX Designer',
    company: 'Creative Hub',
    logo: 'CH',
    location: 'TP. Hồ Chí Minh',
    salary: '$1,800 - $3,000',
    appliedDate: '15/05/2026',
    status: 'pending',
  },
];

const upcomingInterviews = [
  {
    id: '1',
    title: 'Phỏng vấn vòng 2',
    company: 'TechVision Corp',
    date: '30/05/2026',
    time: '14:00',
    type: 'Online',
  },
  {
    id: '2',
    title: 'Phỏng vấn vòng 1',
    company: 'AI Analytics',
    date: '02/06/2026',
    time: '10:00',
    type: 'Offline',
  },
];

const recommendedJobs = [
  {
    id: '1',
    title: 'Full Stack Developer',
    company: 'StartupHub',
    logo: 'SH',
    location: 'Remote',
    salary: '$2,000 - $3,500',
    tags: ['React', 'Node.js', 'MongoDB'],
    match: 95,
  },
  {
    id: '2',
    title: 'Mobile Developer',
    company: 'AppWorks',
    logo: 'AW',
    location: 'TP. Hồ Chí Minh',
    salary: '$2,500 - $4,000',
    tags: ['React Native', 'Flutter', 'iOS'],
    match: 88,
  },
  {
    id: '3',
    title: 'Data Engineer',
    company: 'DataPro',
    logo: 'DP',
    location: 'Hà Nội',
    salary: '$3,000 - $4,500',
    tags: ['Python', 'Spark', 'SQL'],
    match: 82,
  },
];

function ApplicationCard({ job }: { job: typeof applications[0] }) {
  const status = APPLICATION_STATUS[job.status];
  const StatusIcon = status?.icon || Clock;

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className="p-5 rounded-2xl bg-white/5 border border-white/10 hover:border-accent-primary/30 transition-colors"
    >
      <div className="flex items-start gap-4">
        <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-white font-semibold">
          {job.logo}
        </div>
        <div className="flex-1 min-w-0">
          <h4 className="font-semibold text-white truncate">{job.title}</h4>
          <p className="text-sm text-text-secondary">{job.company}</p>
          <div className="flex flex-wrap items-center gap-3 mt-2 text-xs text-text-tertiary">
            <span className="flex items-center gap-1">
              <MapPin className="w-3 h-3" />
              {job.location}
            </span>
            <span className="flex items-center gap-1">
              <DollarSign className="w-3 h-3" />
              {job.salary}
            </span>
          </div>
        </div>
        <div className="text-right">
          <span className={`inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium ${status?.color || 'bg-white/10 text-white'}`}>
            <StatusIcon className="w-3 h-3" />
            {status?.label || job.status}
          </span>
          <p className="text-xs text-text-tertiary mt-2">{job.appliedDate}</p>
        </div>
      </div>
    </motion.div>
  );
}

function InterviewCard({ interview }: { interview: typeof upcomingInterviews[0] }) {
  return (
    <div className="flex items-center gap-4 p-4 rounded-xl bg-gradient-to-r from-accent-primary/10 to-violet/10 border border-white/5">
      <div className="w-12 h-12 rounded-xl bg-accent-primary/20 flex flex-col items-center justify-center">
        <span className="text-xs font-medium text-accent-primary">
          {interview.date.split('/')[1]}
        </span>
        <span className="text-lg font-bold text-white">
          {interview.date.split('/')[0]}
        </span>
      </div>
      <div className="flex-1">
        <h4 className="font-medium text-white">{interview.title}</h4>
        <p className="text-sm text-text-secondary">{interview.company}</p>
        <div className="flex items-center gap-3 mt-1 text-xs text-text-tertiary">
          <span>{interview.time}</span>
          <span>-</span>
          <span>{interview.type}</span>
        </div>
      </div>
      <button className="p-2 rounded-lg hover:bg-white/5 transition-colors text-text-tertiary hover:text-white">
        <ArrowRight className="w-5 h-5" />
      </button>
    </div>
  );
}

function RecommendedJobCard({ job }: { job: typeof recommendedJobs[0] }) {
  return (
    <div className="p-5 rounded-2xl bg-white/5 border border-white/10 hover:border-accent-primary/30 transition-colors cursor-pointer group">
      <div className="flex items-start gap-4">
        <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-violet to-accent-primary flex items-center justify-center text-white font-semibold">
          {job.logo}
        </div>
        <div className="flex-1">
          <h4 className="font-semibold text-white group-hover:text-accent-primary transition-colors">{job.title}</h4>
          <p className="text-sm text-text-secondary">{job.company}</p>
          <div className="flex flex-wrap items-center gap-3 mt-2 text-xs text-text-tertiary">
            <span className="flex items-center gap-1">
              <MapPin className="w-3 h-3" />
              {job.location}
            </span>
            <span className="flex items-center gap-1">
              <DollarSign className="w-3 h-3" />
              {job.salary}
            </span>
          </div>
        </div>
      </div>
      <div className="flex flex-wrap gap-2 mt-4">
        {job.tags.map((tag) => (
          <span key={tag} className="px-2 py-1 rounded bg-white/5 text-text-secondary text-xs">
            {tag}
          </span>
        ))}
      </div>
      <div className="flex items-center justify-between mt-4 pt-4 border-t border-white/5">
        <div className="flex items-center gap-2">
          <Target className="w-4 h-4 text-emerald-400" />
          <span className="text-sm text-emerald-400 font-medium">{job.match}% trùng khớp</span>
        </div>
        <Link
          to={`/jobs/${job.id}`}
          className="px-4 py-2 rounded-lg bg-accent-primary/20 text-accent-primary text-sm font-medium hover:bg-accent-primary hover:text-white transition-colors"
        >
          Ứng tuyển
        </Link>
      </div>
    </div>
  );
}

export default function CandidateDashboardPage() {
  const user = useAuthStore((state) => state.user);

  return (
    <div className="min-h-screen py-8">
      <div className="max-w-7xl mx-auto px-6">
        {/* Header */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          className="mb-8"
        >
          <h1 className="text-3xl font-bold text-white mb-2">Xin chào, {user?.name || 'Ứng viên'}!</h1>
          <p className="text-text-secondary">Cập nhật hồ sơ để tăng cơ hội nhận được việc làm phù hợp</p>
        </motion.div>

        {/* Stats */}
        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
          {STATS.map((stat, index) => {
            const Icon = stat.icon;
            return (
              <motion.div
                key={stat.label}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: index * 0.1 }}
                className="p-5 rounded-2xl bg-white/5 border border-white/10"
              >
                <div className={`w-12 h-12 rounded-xl bg-gradient-to-br ${stat.color} flex items-center justify-center mb-4`}>
                  <Icon className="w-6 h-6 text-white" />
                </div>
                <p className="text-3xl font-bold text-white">{stat.value}</p>
                <p className="text-sm text-text-secondary">{stat.label}</p>
              </motion.div>
            );
          })}
        </div>

        <div className="grid lg:grid-cols-3 gap-8">
          {/* Main Content */}
          <div className="lg:col-span-2 space-y-8">
            {/* Applications */}
            <section>
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-xl font-semibold text-white">Việc đã ứng tuyển</h2>
                <Link to="/ung-vien/ung-tuyen" className="text-sm text-accent-primary hover:text-accent-secondary transition-colors flex items-center gap-1">
                  Xem tất cả
                  <ArrowRight className="w-4 h-4" />
                </Link>
              </div>
              <div className="space-y-3">
                {applications.map((job) => (
                  <ApplicationCard key={job.id} job={job} />
                ))}
              </div>
            </section>

            {/* Recommended Jobs */}
            <section>
              <div className="flex items-center justify-between mb-4">
                <h2 className="text-xl font-semibold text-white">Việc làm phù hợp với bạn</h2>
                <Link to="/jobs" className="text-sm text-accent-primary hover:text-accent-secondary transition-colors flex items-center gap-1">
                  Tìm thêm
                  <ArrowRight className="w-4 h-4" />
                </Link>
              </div>
              <div className="space-y-3">
                {recommendedJobs.map((job) => (
                  <RecommendedJobCard key={job.id} job={job} />
                ))}
              </div>
            </section>
          </div>

          {/* Sidebar */}
          <div className="space-y-6">
            {/* Profile Progress */}
            <motion.div
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ delay: 0.2 }}
              className="p-6 rounded-2xl bg-white/5 border border-white/10"
            >
              <h3 className="font-semibold text-white mb-4">Hoàn thiện hồ sơ</h3>
              <div className="mb-4">
                <div className="flex items-center justify-between text-sm mb-2">
                  <span className="text-text-secondary">Độ hoàn thiện</span>
                  <span className="text-accent-primary font-medium">75%</span>
                </div>
                <div className="h-2 bg-white/10 rounded-full overflow-hidden">
                  <div className="h-full w-3/4 bg-gradient-to-r from-accent-primary to-violet rounded-full" />
                </div>
              </div>
              <div className="space-y-2">
                {[
                  { label: 'Thông tin cơ bản', done: true },
                  { label: 'Kinh nghiệm làm việc', done: true },
                  { label: 'Học vấn', done: true },
                  { label: 'Kỹ năng', done: false },
                  { label: 'Upload CV', done: false },
                ].map((item) => (
                  <div key={item.label} className="flex items-center gap-2 text-sm">
                    {item.done ? (
                      <CheckCircle className="w-4 h-4 text-emerald-400" />
                    ) : (
                      <div className="w-4 h-4 rounded-full border-2 border-text-tertiary" />
                    )}
                    <span className={item.done ? 'text-white' : 'text-text-secondary'}>
                      {item.label}
                    </span>
                  </div>
                ))}
              </div>
              <Link
                to="/ung-vien/cong-cua"
                className="mt-4 block w-full py-3 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white text-sm font-medium text-center hover:opacity-90 transition-opacity"
              >
                Cập nhật hồ sơ
              </Link>
            </motion.div>

            {/* Upcoming Interviews */}
            <motion.div
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ delay: 0.3 }}
              className="p-6 rounded-2xl bg-white/5 border border-white/10"
            >
              <h3 className="font-semibold text-white mb-4">Phỏng vấn sắp tới</h3>
              {upcomingInterviews.length > 0 ? (
                <div className="space-y-3">
                  {upcomingInterviews.map((interview) => (
                    <InterviewCard key={interview.id} interview={interview} />
                  ))}
                </div>
              ) : (
                <div className="text-center py-8">
                  <Calendar className="w-12 h-12 text-text-tertiary mx-auto mb-3" />
                  <p className="text-text-secondary text-sm">Chưa có lịch phỏng vấn nào</p>
                </div>
              )}
            </motion.div>

            {/* Quick Actions */}
            <motion.div
              initial={{ opacity: 0, x: 20 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ delay: 0.4 }}
              className="p-6 rounded-2xl bg-white/5 border border-white/10"
            >
              <h3 className="font-semibold text-white mb-4">Thao tác nhanh</h3>
              <div className="space-y-2">
                <Link
                  to="/ung-vien/cong-cua"
                  className="flex items-center gap-3 p-3 rounded-xl hover:bg-white/5 transition-colors group"
                >
                  <div className="w-10 h-10 rounded-lg bg-blue-500/20 flex items-center justify-center text-blue-400 group-hover:bg-blue-500/30">
                    <FileText className="w-5 h-5" />
                  </div>
                  <span className="text-sm text-text-secondary group-hover:text-white">Cập nhật hồ sơ</span>
                </Link>
                <Link
                  to="/jobs"
                  className="flex items-center gap-3 p-3 rounded-xl hover:bg-white/5 transition-colors group"
                >
                  <div className="w-10 h-10 rounded-lg bg-emerald-500/20 flex items-center justify-center text-emerald-400 group-hover:bg-emerald-500/30">
                    <Briefcase className="w-5 h-5" />
                  </div>
                  <span className="text-sm text-text-secondary group-hover:text-white">Tìm việc mới</span>
                </Link>
                <Link
                  to="/ung-vien/ket-qua"
                  className="flex items-center gap-3 p-3 rounded-xl hover:bg-white/5 transition-colors group"
                >
                  <div className="w-10 h-10 rounded-lg bg-violet-500/20 flex items-center justify-center text-violet-400 group-hover:bg-violet-500/30">
                    <TrendingUp className="w-5 h-5" />
                  </div>
                  <span className="text-sm text-text-secondary group-hover:text-white">Xem kết quả ứng tuyển</span>
                </Link>
              </div>
            </motion.div>
          </div>
        </div>
      </div>
    </div>
  );
}
