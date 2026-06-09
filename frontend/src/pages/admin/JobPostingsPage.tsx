import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Plus, MapPin, DollarSign, Users, MoreVertical, Eye, Edit2, Loader2, Briefcase } from 'lucide-react';
import jobService from '@/services/job/jobService';
import type { JobPosting } from '@/types/job';

type JobActionKey = 'publish' | 'close' | 'edit' | 'delete' | 'archive' | 'reopen';

interface JobActionItem {
  key: JobActionKey;
  label: string;
  enabled: boolean;
}

interface MenuPosition {
  top: number;
  left: number;
}

export default function JobPostingsPage() {
  const navigate = useNavigate();
  const [jobs, setJobs] = useState<JobPosting[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [openMenuJobId, setOpenMenuJobId] = useState<string | null>(null);
  const [menuPosition, setMenuPosition] = useState<MenuPosition | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  useEffect(() => {
    async function fetchJobs() {
      try {
        setLoading(true);
        const data = await jobService.getAdminJobPostings();
        setJobs(data);
      } catch (err) {
        console.error(err);
        setError('Không thể tải danh sách tin tuyển dụng.');
      } finally {
        setLoading(false);
      }
    }
    fetchJobs();
  }, []);

  useEffect(() => {
    function closeMenu() {
      setOpenMenuJobId(null);
      setMenuPosition(null);
    }

    if (!openMenuJobId) return undefined;

    document.addEventListener('click', closeMenu);
    window.addEventListener('resize', closeMenu);
    window.addEventListener('scroll', closeMenu, true);

    return () => {
      document.removeEventListener('click', closeMenu);
      window.removeEventListener('resize', closeMenu);
      window.removeEventListener('scroll', closeMenu, true);
    };
  }, [openMenuJobId]);

  const stats = useMemo(
    () => [
      { label: 'Tổng tin', value: jobs.length.toString(), color: 'text-blue-400' },
      { label: 'Đang active', value: jobs.filter((job) => job.status === 'active').length.toString(), color: 'text-emerald-400' },
      { label: 'Đã đóng', value: jobs.filter((job) => job.status === 'closed').length.toString(), color: 'text-amber-400' },
      { label: 'Bản nháp', value: jobs.filter((job) => job.status === 'draft').length.toString(), color: 'text-white/40' },
    ],
    [jobs],
  );

  function formatSalary(job: JobPosting): string {
    if (job.salaryIsNegotiable) return 'Thỏa thuận';
    if (job.salaryMin && job.salaryMax) {
      return `$${job.salaryMin.toLocaleString()} - $${job.salaryMax.toLocaleString()}`;
    }
    if (job.salaryMin) return `Từ $${job.salaryMin.toLocaleString()}`;
    if (job.salaryMax) return `Đến $${job.salaryMax.toLocaleString()}`;
    return 'Thỏa thuận';
  }

  function formatDate(dateStr: string): string {
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString('vi-VN');
    } catch {
      return dateStr;
    }
  }

  function getStatusLabel(status: JobPosting['status']) {
    if (status === 'active') return 'Active';
    if (status === 'draft') return 'Bản nháp';
    if (status === 'paused') return 'Tạm dừng';
    return 'Đã đóng';
  }

  function getStatusClass(status: JobPosting['status']) {
    if (status === 'active') return 'bg-emerald-500/20 text-emerald-400';
    if (status === 'draft') return 'bg-blue-500/20 text-blue-400';
    if (status === 'paused') return 'bg-amber-500/20 text-amber-400';
    return 'bg-white/10 text-white/40';
  }

  function getActionsForJob(status: JobPosting['status']): JobActionItem[] {
    if (status === 'draft') {
      return [
        { key: 'publish', label: 'Publish', enabled: false },
        { key: 'edit', label: 'Edit', enabled: true },
        { key: 'delete', label: 'Delete', enabled: false },
      ];
    }

    if (status === 'active') {
      return [
        { key: 'close', label: 'Close', enabled: false },
        { key: 'edit', label: 'Edit', enabled: true },
      ];
    }

    return [
      { key: 'archive', label: 'Archive', enabled: false },
      { key: 'reopen', label: 'Reopen', enabled: false },
    ];
  }

  function handleActionClick(job: JobPosting, action: JobActionItem) {
    setOpenMenuJobId(null);
    setMenuPosition(null);

    if (action.key === 'edit') {
      navigate(`/admin/jobs/${job.id}`);
      return;
    }

    setNotice(`Action "${action.label}" cho job này chưa thể chạy vì backend hiện chưa có endpoint tương ứng.`);
  }

  function toggleMenu(jobId: string, event: React.MouseEvent<HTMLButtonElement>) {
    event.stopPropagation();

    if (openMenuJobId === jobId) {
      setOpenMenuJobId(null);
      setMenuPosition(null);
      return;
    }

    const rect = event.currentTarget.getBoundingClientRect();
    const menuWidth = 220;
    const viewportPadding = 16;
    const left = Math.min(rect.right - menuWidth, window.innerWidth - menuWidth - viewportPadding);

    setOpenMenuJobId(jobId);
    setMenuPosition({
      top: rect.bottom + 8,
      left: Math.max(viewportPadding, left),
    });
  }

  const activeJob = jobs.find((job) => job.id === openMenuJobId) ?? null;
  const activeActions = activeJob ? getActionsForJob(activeJob.status) : [];

  return (
    <div className="p-6 lg:p-8">
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="flex items-center justify-between mb-8"
      >
        <div>
          <h1 className="text-2xl font-semibold text-white mb-1">Tin tuyển dụng</h1>
          <p className="text-sm text-white/40">Quản lý các tin tuyển dụng và cấu hình phỏng vấn AI</p>
        </div>
        <a
          href="/admin/jobs/create"
          className="flex items-center gap-2 px-6 py-3 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity"
        >
          <Plus className="w-4 h-4" />
          Tạo tin mới
        </a>
      </motion.div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
        {stats.map((stat) => (
          <motion.div
            key={stat.label}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-4 overflow-hidden"
          >
            <p className={`text-2xl font-semibold ${stat.color}`}>{stat.value}</p>
            <p className="text-sm text-white/40">{stat.label}</p>
          </motion.div>
        ))}
      </div>

      {error && (
        <div className="p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm mb-6">
          {error}
        </div>
      )}

      {notice && (
        <div className="p-4 rounded-xl bg-amber-500/10 border border-amber-500/20 text-amber-300 text-sm mb-6 flex items-start justify-between gap-3">
          <span>{notice}</span>
          <button
            type="button"
            onClick={() => setNotice(null)}
            className="text-amber-200/70 hover:text-amber-100 transition-colors"
          >
            Đóng
          </button>
        </div>
      )}

      {loading ? (
        <div className="flex flex-col items-center justify-center py-20 gap-3">
          <Loader2 className="w-10 h-10 text-accent-primary animate-spin" />
          <p className="text-sm text-white/40">Đang tải dữ liệu từ máy chủ...</p>
        </div>
      ) : jobs.length === 0 ? (
        <div className="bg-white/[0.02] border border-white/[0.05] rounded-2xl p-12 text-center">
          <Briefcase className="w-12 h-12 text-white/20 mx-auto mb-4" />
          <h3 className="text-lg font-semibold text-white mb-2">Chưa có tin tuyển dụng</h3>
          <p className="text-text-secondary text-sm mb-6">Hãy tạo tin tuyển dụng đầu tiên của bạn để bắt đầu tiếp nhận hồ sơ.</p>
          <a
            href="/admin/jobs/create"
            className="inline-flex items-center gap-2 px-5 py-2.5 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity"
          >
            <Plus className="w-4.5 h-4.5" />
            Tạo tin tuyển dụng đầu tiên
          </a>
        </div>
      ) : (
        <div className="space-y-4">
          {jobs.map((job, index) => (
            <motion.div
              key={job.id}
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: index * 0.05 }}
              className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 group hover:bg-white/[0.05] transition-colors"
            >
              <div className="flex items-center justify-between gap-4">
                <div className="flex items-center gap-4 min-w-0">
                  <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-white font-semibold shrink-0">
                    {job.title.charAt(0)}
                  </div>
                  <div className="min-w-0">
                    <div className="flex items-center gap-3 mb-1 flex-wrap">
                      <h3 className="text-lg font-semibold text-white truncate">{job.title}</h3>
                      <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${getStatusClass(job.status)}`}>
                        {getStatusLabel(job.status)}
                      </span>
                    </div>
                    <div className="flex flex-wrap items-center gap-4 text-sm text-white/50">
                      <span className="flex items-center gap-1">
                        <MapPin className="w-4 h-4" />
                        {job.location || 'Chưa cấu hình địa điểm'}
                      </span>
                      <span className="flex items-center gap-1">
                        <DollarSign className="w-4 h-4" />
                        {formatSalary(job)}
                      </span>
                      <span className="flex items-center gap-1">
                        <Users className="w-4 h-4" />
                        Vòng: {job.roundConfigs?.length || 0}
                      </span>
                    </div>
                  </div>
                </div>
                <div className="flex items-center gap-3 shrink-0">
                  <span className="text-xs text-white/30">Tạo ngày: {formatDate(job.createdAt)}</span>
                  <div className="flex items-center gap-2">
                    <a href={`/admin/jobs/${job.id}`} className="p-2 rounded-lg hover:bg-white/10 transition-colors">
                      <Eye className="w-4 h-4 text-white/50" />
                    </a>
                    <button
                      type="button"
                      onClick={() => navigate(`/admin/jobs/${job.id}`)}
                      className="p-2 rounded-lg hover:bg-white/10 transition-colors"
                    >
                      <Edit2 className="w-4 h-4 text-white/50" />
                    </button>
                    <button
                      type="button"
                      onClick={(event) => toggleMenu(job.id, event)}
                      className="p-2 rounded-lg hover:bg-white/10 transition-colors"
                    >
                      <MoreVertical className="w-4 h-4 text-white/50" />
                    </button>
                  </div>
                </div>
              </div>
            </motion.div>
          ))}
        </div>
      )}

      {openMenuJobId && menuPosition && activeJob && (
        <div
          className="fixed inset-0 z-[120]"
          onClick={() => {
            setOpenMenuJobId(null);
            setMenuPosition(null);
          }}
        >
          <div
            className="fixed z-[130] min-w-[220px] rounded-xl border border-white/10 bg-slate-950 p-2 shadow-2xl ring-1 ring-black/30"
            style={{ top: menuPosition.top, left: menuPosition.left }}
            onClick={(event) => event.stopPropagation()}
          >
            {activeActions.map((action) => (
              <button
                key={action.key}
                type="button"
                onClick={() => handleActionClick(activeJob, action)}
                className="flex w-full items-center justify-between rounded-lg px-3 py-2 text-left text-sm text-white hover:bg-white/10 transition-colors disabled:cursor-not-allowed disabled:text-white/30 disabled:hover:bg-transparent"
                disabled={!action.enabled}
                title={!action.enabled ? 'Backend chưa hỗ trợ endpoint cho action này.' : undefined}
              >
                <span>{action.label}</span>
                {!action.enabled && <span className="text-[10px] uppercase tracking-wide text-white/30">BE thiếu API</span>}
              </button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
