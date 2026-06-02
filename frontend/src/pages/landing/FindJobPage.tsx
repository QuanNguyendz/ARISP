import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import CandidateNav from '@components/layout/CandidateNav';
import CandidateFooter from '@components/layout/CandidateFooter';
import { 
  Search, MapPin, Briefcase, Clock, DollarSign, Building2, 
  Filter, Heart, Bookmark, Share2, ArrowRight,
  Zap, Globe, Users, TrendingUp, Loader2
} from 'lucide-react';
import jobService from '@/services/job/jobService';
import type { JobPosting } from '@/types/job';

const JOB_TYPES = ['Tất cả', 'Full-time', 'Part-time', 'Thực tập'];
const LOCATIONS = ['Tất cả', 'TP. Hồ Chí Minh', 'Hà Nội', 'Đà Nẵng', 'Remote'];

const STATS = [
  { icon: <Briefcase className="w-5 h-5" />, value: '100+', label: 'Việc làm' },
  { icon: <Building2 className="w-5 h-5" />, value: '1', label: 'Doanh nghiệp' },
  { icon: <Users className="w-5 h-5" />, value: '500+', label: 'Ứng viên' },
  { icon: <TrendingUp className="w-5 h-5" />, value: '95%', label: 'Tỷ lệ trúng tuyển' }
];

const POPULAR_KEYWORDS = ['Frontend', 'Backend', 'TypeScript', 'C#', '.NET', 'AI'];

function JobCard({ job }: { job: JobPosting }) {
  const navigate = useNavigate();
  
  function formatSalary(j: JobPosting): string {
    if (j.salaryIsNegotiable) return 'Thỏa thuận';
    if (j.salaryMin && j.salaryMax) {
      return `$${j.salaryMin.toLocaleString()} - $${j.salaryMax.toLocaleString()}`;
    }
    if (j.salaryMin) return `Từ $${j.salaryMin.toLocaleString()}`;
    if (j.salaryMax) return `Đến $${j.salaryMax.toLocaleString()}`;
    return 'Thỏa thuận';
  }

  function formatWorkMode(mode?: string): string {
    if (!mode) return 'Full-time';
    const mappings: Record<string, string> = {
      fulltime: 'Full-time',
      parttime: 'Part-time',
      contract: 'Hợp đồng',
      internship: 'Thực tập',
    };
    return mappings[mode.toLowerCase()] || mode;
  }

  function formatPostedDate(dateStr: string): string {
    try {
      const diffTime = Math.abs(new Date().getTime() - new Date(dateStr).getTime());
      const diffDays = Math.ceil(diffTime / (1000 * 60 * 60 * 24));
      if (diffDays <= 1) return 'Hôm nay';
      if (diffDays <= 2) return 'Hôm qua';
      return `${diffDays} ngày trước`;
    } catch {
      return 'Gần đây';
    }
  }
  
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className="group glass p-6 hover:bg-surface-hover transition-all duration-300"
    >
      <div className="flex items-start justify-between mb-4">
        <div className="flex items-start gap-4">
          <div className="w-14 h-14 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-white font-semibold text-lg">
            {job.title.substring(0, 2).toUpperCase()}
          </div>
          <div>
            <div className="flex items-center gap-2 mb-1">
              {job.isUrgent && (
                <span className="px-2 py-0.5 rounded-full bg-red-500/20 text-red-400 text-xs font-medium">
                  Gấp
                </span>
              )}
              {job.interviewMode === 'remote' && (
                <span className="px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-400 text-xs font-medium flex items-center gap-1">
                  <Globe className="w-3 h-3" />
                  Remote
                </span>
              )}
            </div>
            <h3 
              className="text-lg font-semibold text-text-primary group-hover:text-accent-primary transition-colors cursor-pointer"
              onClick={() => navigate(`/jobs/${job.id}`)}
            >
              {job.title}
            </h3>
            <p className="text-text-secondary text-sm">{job.department || 'Phòng ban tuyển dụng'}</p>
          </div>
        </div>
        
        <div className="flex items-center gap-2">
          <button className="p-2 rounded-lg text-text-tertiary hover:text-red-500 hover:bg-red-500/10 transition-colors">
            <Heart className="w-5 h-5" />
          </button>
          <button className="p-2 rounded-lg text-text-tertiary hover:text-text-primary hover:bg-white/5 transition-colors">
            <Bookmark className="w-5 h-5" />
          </button>
          <button className="p-2 rounded-lg text-text-tertiary hover:text-text-primary hover:bg-white/5 transition-colors">
            <Share2 className="w-5 h-5" />
          </button>
        </div>
      </div>
      
      <div className="flex flex-wrap items-center gap-4 text-sm text-text-secondary mb-4">
        <span className="flex items-center gap-1.5">
          <MapPin className="w-4 h-4" />
          {job.location || 'Remote'}
        </span>
        <span className="flex items-center gap-1.5">
          <DollarSign className="w-4 h-4" />
          {formatSalary(job)}
        </span>
        <span className="flex items-center gap-1.5">
          <Clock className="w-4 h-4" />
          {formatPostedDate(job.createdAt)}
        </span>
      </div>
      
      {job.skills && job.skills.length > 0 && (
        <div className="flex flex-wrap gap-2 mb-4">
          {job.skills.map((tag) => (
            <span key={tag} className="px-3 py-1 rounded-full bg-white/5 text-text-secondary text-xs">
              {tag}
            </span>
          ))}
        </div>
      )}
      
      <div className="flex items-center justify-between pt-4 border-t border-white/5">
        <span className="text-xs text-text-tertiary flex items-center gap-1.5">
          <Users className="w-4 h-4" />
          Loại hình: {formatWorkMode(job.workMode || job.employmentType)}
        </span>
        <button 
          onClick={() => navigate(`/jobs/${job.id}`)}
          className="px-4 py-2 rounded-lg bg-accent-primary/20 text-accent-primary font-medium text-sm hover:bg-accent-primary hover:text-white transition-all flex items-center gap-2"
        >
          Chi tiết tuyển dụng
          <ArrowRight className="w-4 h-4" />
        </button>
      </div>
    </motion.div>
  );
}

export default function FindJob() {
  const navigate = useNavigate();
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedType, setSelectedType] = useState('Tất cả');
  const [selectedLocation, setSelectedLocation] = useState('Tất cả');
  const [jobs, setJobs] = useState<JobPosting[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [showFilters, setShowFilters] = useState(false);

  useEffect(() => {
    async function loadJobs() {
      try {
        setLoading(true);
        const data = await jobService.getPublicJobPostings();
        setJobs(data);
      } catch (err: any) {
        console.error(err);
        setError('Không thể kết nối máy chủ để tải danh sách công việc.');
      } finally {
        setLoading(false);
      }
    }
    loadJobs();
  }, []);

  const filteredJobs = jobs.filter(job => {
    const matchesSearch = job.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
                          (job.department || '').toLowerCase().includes(searchQuery.toLowerCase()) ||
                          (job.skills || []).some(s => s.toLowerCase().includes(searchQuery.toLowerCase()));

    // Map selected UI filter types to DB values
    const matchesType = selectedType === 'Tất cả' || 
                        (job.workMode || job.employmentType || '').toLowerCase() === (selectedType === 'Full-time' ? 'fulltime' : selectedType === 'Part-time' ? 'parttime' : 'internship');

    const matchesLocation = selectedLocation === 'Tất cả' || 
                            (job.location || '').toLowerCase().includes(selectedLocation.toLowerCase()) ||
                            (selectedLocation === 'Remote' && job.interviewMode === 'remote');

    return matchesSearch && matchesType && matchesLocation;
  });

  return (
    <div className="min-h-screen bg-bg-primary">
      <CandidateNav />
      
      {/* hero Section */}
      <section className="relative pt-24 pb-16 overflow-hidden">
        <div className="absolute inset-0 bg-gradient-to-b from-accent-primary/10 via-transparent to-transparent" />
        <div className="absolute top-0 left-1/2 -translate-x-1/2 w-[800px] h-[400px] bg-accent-primary/20 rounded-full blur-[120px]" />
        
        <div className="relative max-w-6xl mx-auto px-6">
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="text-center mb-10"
          >
            <h1 className="text-4xl md:text-5xl font-bold mb-4">
              <span className="text-text-primary">Tìm việc làm </span>
              <span className="bg-gradient-to-r from-accent-primary to-violet bg-clip-text text-transparent">
                Việc làm tốt nhất
              </span>
            </h1>
            <p className="text-text-secondary text-lg max-w-2xl mx-auto">
              Cơ hội việc làm hot nhất tại các phòng ban thuộc doanh nghiệp của chúng tôi
            </p>
          </motion.div>
          
          {/* Search Box */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.1 }}
            className="glass p-2 rounded-2xl max-w-4xl mx-auto"
          >
            <div className="flex flex-col md:flex-row gap-2">
              <div className="flex-1 relative">
                <Search className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                <input
                  type="text"
                  value={searchQuery}
                  onChange={(e) => setSearchQuery(e.target.value)}
                  placeholder="Tìm kiếm vị trí, phòng ban, kỹ năng..."
                  className="w-full pl-12 pr-4 py-4 rounded-xl bg-transparent text-text-primary placeholder:text-text-tertiary focus:outline-none"
                />
              </div>
              <div className="flex gap-2">
                <select 
                  value={selectedLocation}
                  onChange={(e) => setSelectedLocation(e.target.value)}
                  className="px-4 py-4 rounded-xl bg-white/5 border border-white/10 text-text-primary focus:outline-none focus:border-accent-primary/50 cursor-pointer"
                >
                  {LOCATIONS.map(loc => (
                    <option key={loc} value={loc}>{loc}</option>
                  ))}
                </select>
                <button 
                  onClick={() => setShowFilters(!showFilters)}
                  className="px-4 py-4 rounded-xl bg-white/5 border border-white/10 text-text-primary hover:bg-white/10 transition-colors flex items-center gap-2"
                >
                  <Filter className="w-5 h-5" />
                  <span className="hidden sm:inline">Bộ lọc</span>
                </button>
              </div>
            </div>
            
            {showFilters && (
              <motion.div 
                initial={{ opacity: 0, height: 0 }}
                animate={{ opacity: 1, height: 'auto' }}
                className="mt-4 pt-4 border-t border-white/10 grid grid-cols-2 gap-4"
              >
                <select 
                  value={selectedType}
                  onChange={(e) => setSelectedType(e.target.value)}
                  className="px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-text-primary focus:outline-none"
                >
                  {JOB_TYPES.map(type => (
                    <option key={type} value={type}>{type}</option>
                  ))}
                </select>
              </motion.div>
            )}
          </motion.div>
          
          {/* Popular Keywords */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.2 }}
            className="mt-6 flex flex-wrap items-center justify-center gap-3"
          >
            <span className="text-text-tertiary text-sm">Từ khóa phổ biến:</span>
            {POPULAR_KEYWORDS.map((keyword) => (
              <button
                key={keyword}
                onClick={() => setSearchQuery(keyword)}
                className="px-4 py-2 rounded-full bg-white/5 text-text-secondary text-sm hover:bg-accent-primary/20 hover:text-accent-primary transition-colors"
              >
                {keyword}
              </button>
            ))}
          </motion.div>
        </div>
      </section>
      
      {/* Stats */}
      <section className="py-8 border-y border-white/5">
        <div className="max-w-6xl mx-auto px-6">
          <div className="grid grid-cols-2 md:grid-cols-4 gap-6">
            {STATS.map((stat, index) => (
              <motion.div
                key={stat.label}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: 0.1 * index }}
                className="text-center"
              >
                <div className="w-12 h-12 mx-auto mb-3 rounded-xl bg-accent-primary/20 flex items-center justify-center text-accent-primary">
                  {stat.icon}
                </div>
                <p className="text-2xl font-bold text-text-primary">{stat.value}</p>
                <p className="text-text-secondary text-sm">{stat.label}</p>
              </motion.div>
            ))}
          </div>
        </div>
      </section>
      
      {/* Job Listings */}
      <section className="py-12">
        <div className="max-w-6xl mx-auto px-6">
          {/* Section Header */}
          <div className="flex items-center justify-between mb-8">
            <div>
              <h2 className="text-2xl font-bold text-text-primary mb-1">Việc làm đang tuyển</h2>
              <p className="text-text-secondary">Cơ hội gia nhập đội ngũ chuyên gia công nghệ</p>
            </div>
          </div>
          
          {error && (
            <div className="p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm mb-6">
              {error}
            </div>
          )}

          {/* Job List */}
          {loading ? (
            <div className="flex flex-col items-center justify-center py-20 gap-3">
              <Loader2 className="w-10 h-10 text-accent-primary animate-spin" />
              <p className="text-sm text-text-secondary">Đang tải tin tuyển dụng mới nhất...</p>
            </div>
          ) : filteredJobs.length === 0 ? (
            <div className="glass p-12 rounded-2xl text-center text-text-secondary">
              Không tìm thấy tin tuyển dụng nào phù hợp với bộ lọc tìm kiếm.
            </div>
          ) : (
            <div className="grid gap-4">
              {filteredJobs.map((job) => (
                <JobCard key={job.id} job={job} />
              ))}
            </div>
          )}
        </div>
      </section>
      
      {/* CTA Section */}
      <section className="py-16">
        <div className="max-w-6xl mx-auto px-6">
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            whileInView={{ opacity: 1, y: 0 }}
            viewport={{ once: true }}
            className="glass p-8 md:p-12 rounded-3xl text-center relative overflow-hidden"
          >
            <div className="absolute top-0 right-0 w-64 h-64 bg-accent-primary/20 rounded-full blur-[100px]" />
            <div className="absolute bottom-0 left-0 w-64 h-64 bg-violet/20 rounded-full blur-[100px]" />
            
            <div className="relative">
              <div className="w-16 h-16 mx-auto mb-6 rounded-2xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
                <Zap className="w-8 h-8 text-white" />
              </div>
              <h2 className="text-3xl md:text-4xl font-bold text-text-primary mb-4">
                Sẵn sàng phỏng vấn thử cùng AI?
              </h2>
              <p className="text-text-secondary text-lg max-w-2xl mx-auto mb-8">
                Hệ thống AI Interviewer sẽ phỏng vấn bạn dựa theo JD và trích xuất báo cáo năng lực hoàn toàn miễn phí.
              </p>
              <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
                <button 
                  onClick={() => navigate('/auth/candidate-register')}
                  className="px-8 py-4 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-semibold hover:opacity-90 transition-opacity flex items-center gap-2"
                >
                  Đăng ký ứng tuyển ngay
                  <ArrowRight className="w-5 h-5" />
                </button>
              </div>
            </div>
          </motion.div>
        </div>
      </section>
      
      <CandidateFooter />
    </div>
  );
}
