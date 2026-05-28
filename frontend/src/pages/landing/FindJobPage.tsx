import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import CandidateNav from '@components/layout/CandidateNav';
import CandidateFooter from '@components/layout/CandidateFooter';
import { 
  Search, MapPin, Briefcase, Clock, DollarSign, Building2, 
  ChevronDown, Filter, Heart, Bookmark, Share2, ArrowRight,
  Zap, Globe, Users, TrendingUp, CheckCircle
} from 'lucide-react';

const FEATURED_JOBS = [
  {
    id: '1',
    title: 'Senior Frontend Developer',
    company: 'TechVision Corp',
    logo: 'TV',
    location: 'TP. Hồ Chí Minh',
    salary: '$3,000 - $5,000',
    type: 'Full-time',
    mode: 'Remote',
    posted: '2 ngày trước',
    urgent: true,
    saved: false,
    tags: ['React', 'TypeScript', 'Next.js'],
    applicants: 45
  },
  {
    id: '2',
    title: 'Backend Engineer',
    company: 'DataFlow Systems',
    logo: 'DF',
    location: 'Hà Nội',
    salary: '$2,500 - $4,000',
    type: 'Full-time',
    mode: 'Hybrid',
    posted: '3 ngày trước',
    urgent: false,
    saved: true,
    tags: ['Node.js', 'Python', 'AWS'],
    applicants: 32
  },
  {
    id: '3',
    title: 'UI/UX Designer',
    company: 'Creative Hub',
    logo: 'CH',
    location: 'TP. Hồ Chí Minh',
    salary: '$1,800 - $3,000',
    type: 'Full-time',
    mode: 'On-site',
    posted: '5 ngày trước',
    urgent: true,
    saved: false,
    tags: ['Figma', 'UI Design', 'Prototyping'],
    applicants: 67
  },
  {
    id: '4',
    title: 'DevOps Engineer',
    company: 'CloudScale',
    logo: 'CS',
    location: 'Đà Nẵng',
    salary: '$2,800 - $4,500',
    type: 'Full-time',
    mode: 'Remote',
    posted: '1 tuần trước',
    urgent: false,
    saved: false,
    tags: ['Kubernetes', 'Docker', 'CI/CD'],
    applicants: 28
  },
  {
    id: '5',
    title: 'Product Manager',
    company: 'InnovateTech',
    logo: 'IT',
    location: 'TP. Hồ Chí Minh',
    salary: '$3,500 - $5,500',
    type: 'Full-time',
    mode: 'Hybrid',
    posted: '4 ngày trước',
    urgent: false,
    saved: false,
    tags: ['Product Strategy', 'Agile', 'Analytics'],
    applicants: 52
  },
  {
    id: '6',
    title: 'Data Scientist',
    company: 'AI Analytics',
    logo: 'AA',
    location: 'Hà Nội',
    salary: '$3,000 - $5,000',
    type: 'Full-time',
    mode: 'Remote',
    posted: '6 ngày trước',
    urgent: true,
    saved: true,
    tags: ['Python', 'Machine Learning', 'TensorFlow'],
    applicants: 39
  }
];

const JOB_TYPES = ['Tất cả', 'Full-time', 'Part-time', 'Freelance', 'Thực tập'];
const LOCATIONS = ['Tất cả', 'TP. Hồ Chí Minh', 'Hà Nội', 'Đà Nẵng', 'Remote'];
const SALARY_RANGES = ['Tất cả', 'Dưới $1,000', '$1,000 - $2,000', '$2,000 - $3,000', '$3,000 - $5,000', 'Trên $5,000'];

const POPULAR_KEYWORDS = ['Frontend Developer', 'Backend Developer', 'Data Analyst', 'UI/UX Designer', 'Project Manager', 'QA Engineer'];

const STATS = [
  { icon: <Briefcase className="w-5 h-5" />, value: '10,000+', label: 'Việc làm' },
  { icon: <Building2 className="w-5 h-5" />, value: '2,500+', label: 'Công ty' },
  { icon: <Users className="w-5 h-5" />, value: '50,000+', label: 'Ứng viên' },
  { icon: <TrendingUp className="w-5 h-5" />, value: '95%', label: 'Tỷ lệ trúng tuyển' }
];

function JobCard({ job, onSave }: { job: typeof FEATURED_JOBS[0], onSave: (id: string) => void }) {
  const navigate = useNavigate();
  
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className="group glass p-6 hover:bg-surface-hover transition-all duration-300"
    >
      <div className="flex items-start justify-between mb-4">
        <div className="flex items-start gap-4">
          <div className="w-14 h-14 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-white font-semibold text-lg">
            {job.logo}
          </div>
          <div>
            <div className="flex items-center gap-2 mb-1">
              {job.urgent && (
                <span className="px-2 py-0.5 rounded-full bg-red-500/20 text-red-400 text-xs font-medium">
                  Gấp
                </span>
              )}
              {job.mode === 'Remote' && (
                <span className="px-2 py-0.5 rounded-full bg-emerald-500/20 text-emerald-400 text-xs font-medium flex items-center gap-1">
                  <Globe className="w-3 h-3" />
                  Remote
                </span>
              )}
            </div>
            <h3 
              className="text-lg font-semibold text-text-primary group-hover:text-accent-primary transition-colors cursor-pointer"
              onClick={() => navigate(`/ung-vien/ung-tuyen/${job.id}`)}
            >
              {job.title}
            </h3>
            <p className="text-text-secondary text-sm">{job.company}</p>
          </div>
        </div>
        
        <div className="flex items-center gap-2">
          <button 
            onClick={() => onSave(job.id)}
            className={`p-2 rounded-lg transition-colors ${job.saved ? 'text-red-500 bg-red-500/10' : 'text-text-tertiary hover:text-red-500 hover:bg-red-500/10'}`}
          >
            <Heart className={`w-5 h-5 ${job.saved ? 'fill-current' : ''}`} />
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
          {job.location}
        </span>
        <span className="flex items-center gap-1.5">
          <DollarSign className="w-4 h-4" />
          {job.salary}
        </span>
        <span className="flex items-center gap-1.5">
          <Clock className="w-4 h-4" />
          {job.posted}
        </span>
      </div>
      
      <div className="flex flex-wrap gap-2 mb-4">
        {job.tags.map((tag) => (
          <span key={tag} className="px-3 py-1 rounded-full bg-white/5 text-text-secondary text-xs">
            {tag}
          </span>
        ))}
      </div>
      
      <div className="flex items-center justify-between pt-4 border-t border-white/5">
        <span className="text-xs text-text-tertiary flex items-center gap-1.5">
          <Users className="w-4 h-4" />
          {job.applicants} ứng viên
        </span>
        <button 
          onClick={() => navigate(`/ung-vien/ung-tuyen/${job.id}`)}
          className="px-4 py-2 rounded-lg bg-accent-primary/20 text-accent-primary font-medium text-sm hover:bg-accent-primary hover:text-white transition-all flex items-center gap-2"
        >
          Ứng tuyển ngay
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
  const [selectedSalary, setSelectedSalary] = useState('Tất cả');
  const [jobs, setJobs] = useState(FEATURED_JOBS);
  const [showFilters, setShowFilters] = useState(false);

  const handleSaveJob = (jobId: string) => {
    setJobs(jobs.map(job => 
      job.id === jobId ? { ...job, saved: !job.saved } : job
    ));
  };

  const filteredJobs = jobs.filter(job => {
    const matchesSearch = job.title.toLowerCase().includes(searchQuery.toLowerCase()) ||
                         job.company.toLowerCase().includes(searchQuery.toLowerCase());
    const matchesType = selectedType === 'Tất cả' || job.type === selectedType;
    const matchesLocation = selectedLocation === 'Tất cả' || job.location === selectedLocation;
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
              Khám phá hàng nghìn cơ hội việc làm từ các công ty hàng đầu Việt Nam
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
                  placeholder="Tìm kiếm vị trí, công ty, kỹ năng..."
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
                <button className="px-8 py-4 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-semibold hover:opacity-90 transition-opacity flex items-center gap-2">
                  <Search className="w-5 h-5" />
                  <span className="hidden sm:inline">Tìm kiếm</span>
                </button>
              </div>
            </div>
            
            {showFilters && (
              <motion.div 
                initial={{ opacity: 0, height: 0 }}
                animate={{ opacity: 1, height: 'auto' }}
                className="mt-4 pt-4 border-t border-white/10 grid grid-cols-2 md:grid-cols-4 gap-4"
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
                <select 
                  value={selectedSalary}
                  onChange={(e) => setSelectedSalary(e.target.value)}
                  className="px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-text-primary focus:outline-none"
                >
                  {SALARY_RANGES.map(range => (
                    <option key={range} value={range}>{range}</option>
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
            <span className="text-text-tertiary text-sm">Tìm kiếm phổ biến:</span>
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
              <h2 className="text-2xl font-bold text-text-primary mb-1">Việc làm nổi bật</h2>
              <p className="text-text-secondary">Cơ hội việc làm hot nhất tuần này</p>
            </div>
            <div className="flex items-center gap-3">
              <button className="px-4 py-2 rounded-lg bg-accent-primary text-white font-medium text-sm">
                Mới nhất
              </button>
              <button className="px-4 py-2 rounded-lg text-text-secondary hover:bg-white/5 transition-colors text-sm">
                Xem tất cả
              </button>
            </div>
          </div>
          
          {/* Filter Pills */}
          <div className="flex flex-wrap gap-2 mb-8">
            {['Tất cả', 'Remote', 'Gấp', 'Mới', 'Lương cao'].map((filter) => (
              <button
                key={filter}
                className={`px-4 py-2 rounded-full text-sm font-medium transition-colors ${
                  filter === 'Tất cả' 
                    ? 'bg-accent-primary text-white' 
                    : 'bg-white/5 text-text-secondary hover:bg-white/10'
                }`}
              >
                {filter}
              </button>
            ))}
          </div>
          
          {/* Job List */}
          <div className="grid gap-4">
            {filteredJobs.map((job) => (
              <JobCard key={job.id} job={job} onSave={handleSaveJob} />
            ))}
          </div>
          
          {/* Load More */}
          <div className="mt-12 text-center">
            <button className="px-8 py-4 rounded-xl bg-white/5 border border-white/10 text-text-primary font-medium hover:bg-white/10 transition-colors inline-flex items-center gap-2">
              Xem thêm việc làm
              <ChevronDown className="w-5 h-5" />
            </button>
          </div>
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
                Sẵn sàng cho cơ hội tiếp theo?
              </h2>
              <p className="text-text-secondary text-lg max-w-2xl mx-auto mb-8">
                Tạo hồ sơ ngay hôm nay và nhận được đề xuất việc làm phù hợp từ các nhà tuyển dụng hàng đầu
              </p>
              <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
                <button 
                  onClick={() => navigate('/dang-ky')}
                  className="px-8 py-4 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-semibold hover:opacity-90 transition-opacity flex items-center gap-2"
                >
                  Tạo hồ sơ miễn phí
                  <ArrowRight className="w-5 h-5" />
                </button>
                <button className="px-8 py-4 rounded-xl bg-white/5 border border-white/10 text-text-primary font-semibold hover:bg-white/10 transition-colors">
                  Tìm hiểu thêm
                </button>
              </div>
              
              <div className="mt-8 flex flex-wrap items-center justify-center gap-6 text-sm text-text-tertiary">
                <span className="flex items-center gap-2">
                  <CheckCircle className="w-4 h-4 text-emerald-500" />
                  100% miễn phí
                </span>
                <span className="flex items-center gap-2">
                  <CheckCircle className="w-4 h-4 text-emerald-500" />
                  Duy trì hồ sơ ẩn danh
                </span>
                <span className="flex items-center gap-2">
                  <CheckCircle className="w-4 h-4 text-emerald-500" />
                  Nhận thông báo việc mới
                </span>
              </div>
            </div>
          </motion.div>
        </div>
      </section>
      
      {/* For Employers Banner */}
      <section className="py-6 mb-20">
        <div className="max-w-6xl mx-auto px-6">
          <div className="flex flex-col md:flex-row items-center justify-between gap-4 p-4 rounded-2xl bg-gradient-to-r from-violet/20 to-accent-primary/20 border border-white/10">
            <div className="flex items-center gap-4">
              <div className="w-12 h-12 rounded-xl bg-white/10 flex items-center justify-center">
                <Building2 className="w-6 h-6 text-text-primary" />
              </div>
              <div>
                <p className="font-semibold text-text-primary">Bạn là nhà tuyển dụng?</p>
                <p className="text-sm text-text-secondary">Đăng tin tuyển dụng và tìm kiếm ứng viên chất lượng</p>
              </div>
            </div>
            <a 
              href="/nha-tuyen-dung"
              target="_blank"
              rel="noopener noreferrer"
              className="px-6 py-3 rounded-xl bg-white text-bg-primary font-medium hover:bg-white/90 transition-colors flex items-center gap-2 whitespace-nowrap"
            >
              Dành cho NTD
              <ArrowRight className="w-4 h-4" />
            </a>
          </div>
        </div>
      </section>
      
      <CandidateFooter />
    </div>
  );
}
