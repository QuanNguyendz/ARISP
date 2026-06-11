import { motion } from 'framer-motion';
import { FileText, Plus, MapPin, DollarSign, Clock, Edit3, Send, Eye } from 'lucide-react';
import { useState } from 'react';
import { PageHeader, StatsGrid, EmptyState, NoticeAlert } from '@components/shared';

const stats = [
  { label: 'Tổng tin', value: '5', color: 'text-blue-400' },
  { label: 'Đã đăng', value: '2', color: 'text-emerald-400' },
  { label: 'Chờ duyệt', value: '2', color: 'text-amber-400' },
  { label: 'Bản nháp', value: '1', color: 'text-white/40' },
];

const myJobs = [
  { id: '1', title: 'Senior Backend Developer', status: 'published', location: 'Hà Nội', salary: '$2000 - $3000', submittedAt: '2026-06-10', applicants: 12 },
  { id: '2', title: 'Frontend Developer', status: 'published', location: 'TP.HCM', salary: '$1500 - $2500', submittedAt: '2026-06-08', applicants: 8 },
  { id: '3', title: 'DevOps Engineer', status: 'pending_approval', location: 'Hà Nội', salary: '$1800 - $2800', submittedAt: '2026-06-11', applicants: 0 },
  { id: '4', title: 'Product Designer', status: 'rejected', location: 'Đà Nẵng', salary: '$1200 - $2000', submittedAt: '2026-06-05', applicants: 0, rejectionReason: 'Thông tin mô tả công việc chưa đầy đủ' },
  { id: '5', title: 'Data Scientist', status: 'draft', location: 'Hà Nội', salary: '$2500 - $4000', submittedAt: null, applicants: 0 },
];

const getStatusBadge = (status: string) => {
  if (status === 'published') return { label: 'Đã đăng', color: 'bg-emerald-500/20 text-emerald-400' };
  if (status === 'pending_approval') return { label: 'Chờ duyệt', color: 'bg-amber-500/20 text-amber-400' };
  if (status === 'rejected') return { label: 'Bị từ chối', color: 'bg-red-500/20 text-red-400' };
  if (status === 'draft') return { label: 'Bản nháp', color: 'bg-white/10 text-white/50' };
  return { label: status, color: 'bg-white/10 text-white/50' };
};

export default function RecruiterMyJobsPage() {
  const [statusFilter, setStatusFilter] = useState<string>('all');

  const filteredJobs = myJobs.filter((job) => {
    return statusFilter === 'all' || job.status === statusFilter;
  });

  const rejectedJobs = myJobs.filter((job) => job.status === 'rejected');

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Tin tuyển dụng của tôi"
        description="Quản lý và theo dõi các tin đã tạo"
        actions={[
          { label: 'Tạo tin mới', href: '/recruiter/jobs/create', variant: 'primary' },
        ]}
      />

      <StatsGrid stats={stats} />

      {/* Rejection Notice */}
      {rejectedJobs.length > 0 && (
        <NoticeAlert
          message={`Có ${rejectedJobs.length} tin bị từ chối. Vui lòng chỉnh sửa và gửi lại để HR duyệt.`}
        />
      )}

      {/* Filters */}
      <div className="flex gap-3 mb-6 flex-wrap">
        {['all', 'published', 'pending_approval', 'rejected', 'draft'].map((status) => (
          <button
            key={status}
            onClick={() => setStatusFilter(status)}
            className={`px-4 py-2 rounded-xl text-sm font-medium transition-colors ${
              statusFilter === status
                ? 'bg-accent-primary text-white'
                : 'bg-white/[0.03] border border-white/10 text-white/50 hover:text-white hover:bg-white/5'
            }`}
          >
            {status === 'all' ? 'Tất cả' :
             status === 'published' ? 'Đã đăng' :
             status === 'pending_approval' ? 'Chờ duyệt' :
             status === 'rejected' ? 'Bị từ chối' : 'Bản nháp'}
          </button>
        ))}
      </div>

      {filteredJobs.length === 0 ? (
        <EmptyState
          icon={<FileText className="w-8 h-8 text-white/20" />}
          title="Không có tin nào"
          description="Hãy tạo tin tuyển dụng đầu tiên của bạn."
          action={{ label: 'Tạo tin mới', href: '/recruiter/jobs/create' }}
        />
      ) : (
        <div className="space-y-4">
          {filteredJobs.map((job, index) => {
            const statusBadge = getStatusBadge(job.status);
            return (
              <motion.div
                key={job.id}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: index * 0.05 }}
                className={`relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden ${
                  job.status === 'rejected' ? 'border-red-500/20' : ''
                }`}
              >
                {job.status === 'rejected' && job.rejectionReason && (
                  <div className="absolute top-0 left-0 right-0 px-4 py-2 bg-red-500/10 border-b border-red-500/20 text-xs text-red-400">
                    Lý do từ chối: {job.rejectionReason}
                  </div>
                )}
                <div className="flex items-start justify-between gap-4 mt-4">
                  <div className="flex items-start gap-4">
                    <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-amber-500/30 to-orange/30 flex items-center justify-center">
                      <FileText className="w-6 h-6 text-amber-400" />
                    </div>
                    <div>
                      <div className="flex items-center gap-3 mb-2">
                        <h3 className="text-lg font-semibold text-white">{job.title}</h3>
                        <span className={`px-2.5 py-1 rounded-full text-xs font-medium ${statusBadge.color}`}>
                          {statusBadge.label}
                        </span>
                      </div>
                      <div className="flex flex-wrap items-center gap-4 text-sm text-white/50 mb-3">
                        <span className="flex items-center gap-1">
                          <MapPin className="w-4 h-4" />
                          {job.location}
                        </span>
                        <span className="flex items-center gap-1">
                          <DollarSign className="w-4 h-4" />
                          {job.salary}
                        </span>
                        {job.submittedAt && (
                          <span className="flex items-center gap-1">
                            <Clock className="w-4 h-4" />
                            {new Date(job.submittedAt).toLocaleDateString('vi-VN')}
                          </span>
                        )}
                        {job.applicants > 0 && (
                          <span className="text-blue-400">
                            {job.applicants} ứng viên
                          </span>
                        )}
                      </div>
                    </div>
                  </div>
                  <div className="flex items-center gap-2">
                    {job.status === 'rejected' && (
                      <a
                        href={`/recruiter/my-jobs/${job.id}`}
                        className="flex items-center gap-2 px-4 py-2 rounded-xl bg-amber-500/20 text-amber-400 text-sm font-medium hover:bg-amber-500/30 transition-colors"
                      >
                        <Edit3 className="w-4 h-4" />
                        Chỉnh sửa
                      </a>
                    )}
                    {job.status === 'draft' && (
                      <>
                        <a
                          href={`/recruiter/my-jobs/${job.id}`}
                          className="p-2 rounded-lg hover:bg-white/10 transition-colors"
                        >
                          <Edit3 className="w-4 h-4 text-white/50" />
                        </a>
                        <button className="flex items-center gap-2 px-4 py-2 rounded-xl bg-emerald-500/20 text-emerald-400 text-sm font-medium hover:bg-emerald-500/30 transition-colors">
                          <Send className="w-4 h-4" />
                          Gửi duyệt
                        </button>
                      </>
                    )}
                    {job.status === 'pending_approval' && (
                      <a
                        href={`/recruiter/my-jobs/${job.id}`}
                        className="p-2 rounded-lg hover:bg-white/10 transition-colors"
                      >
                        <Eye className="w-4 h-4 text-white/50" />
                      </a>
                    )}
                    {job.status === 'published' && (
                      <a
                        href={`/recruiter/my-jobs/${job.id}`}
                        className="p-2 rounded-lg hover:bg-white/10 transition-colors"
                      >
                        <Eye className="w-4 h-4 text-white/50" />
                      </a>
                    )}
                  </div>
                </div>
              </motion.div>
            );
          })}
        </div>
      )}
    </div>
  );
}
