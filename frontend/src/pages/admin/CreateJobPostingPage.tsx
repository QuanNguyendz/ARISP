import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { ArrowLeft, Trash2, Loader2, PlusCircle, Check } from 'lucide-react';
import jobService from '@/services/job/jobService';
import type { CreateJobPostingRequest, RoundConfig } from '@/types/job';

export default function CreateJobPostingPage() {
  const navigate = useNavigate();
  const [submitting, setSubmitting] = useState<boolean>(false);
  const [error, setError] = useState<string | null>(null);

  // Form Fields
  const [title, setTitle] = useState('');
  const [department, setDepartment] = useState('');
  const [jobDescription, setJobDescription] = useState('');
  const [interviewMode, setInterviewMode] = useState<'remote' | 'onsite' | 'both'>('onsite');
  const [location, setLocation] = useState('');
  const [workMode, setWorkMode] = useState('fulltime');
  const [salaryIsNegotiable, setSalaryIsNegotiable] = useState(true);
  const [salaryMin, setSalaryMin] = useState<number | ''>('');
  const [salaryMax, setSalaryMax] = useState<number | ''>('');
  const [salaryCurrency, setSalaryCurrency] = useState('USD');
  const [experienceLevel, setExperienceLevel] = useState('mid');
  const [isUrgent, setIsUrgent] = useState(false);
  const [isPublicListing, setIsPublicListing] = useState(true);
  const [languageRequirement, setLanguageRequirement] = useState('');

  // Skills Tagging
  const [skillInput, setSkillInput] = useState('');
  const [skills, setSkills] = useState<string[]>([]);

  // Dynamic Round Configurations
  const [rounds, setRounds] = useState<RoundConfig[]>([
    {
      roundNumber: 1,
      roundType: 'screening',
      interviewLanguage: 'vi',
      interviewCodeTtlHours: 2,
      maxDurationMinutes: 30,
    },
  ]);

  const handleAddSkill = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && skillInput.trim()) {
      e.preventDefault();
      if (!skills.includes(skillInput.trim())) {
        setSkills([...skills, skillInput.trim()]);
      }
      setSkillInput('');
    }
  };

  const handleRemoveSkill = (tag: string) => {
    setSkills(skills.filter(s => s !== tag));
  };

  const handleAddRound = () => {
    const nextRoundNumber = rounds.length + 1;
    setRounds([
      ...rounds,
      {
        roundNumber: nextRoundNumber,
        roundType: 'technical',
        interviewLanguage: 'vi',
        interviewCodeTtlHours: 2,
        maxDurationMinutes: 45,
      },
    ]);
  };

  const handleRemoveRound = (roundNum: number) => {
    if (rounds.length <= 1) return; // Must have at least 1 round
    const updated = rounds
      .filter(r => r.roundNumber !== roundNum)
      .map((r, i) => ({ ...r, roundNumber: i + 1 })); // Recalculate round numbers sequentially
    setRounds(updated);
  };

  const handleRoundChange = (index: number, field: keyof RoundConfig, value: any) => {
    const updated = [...rounds];
    updated[index] = { ...updated[index], [field]: value };
    setRounds(updated);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim() || !jobDescription.trim()) {
      setError('Tiêu đề và Mô tả công việc là bắt buộc.');
      return;
    }

    if (interviewMode !== 'remote' && !location.trim()) {
      setError('Địa điểm làm việc là bắt buộc khi chế độ phỏng vấn không phải là Remote.');
      return;
    }

    if (rounds.length === 0) {
      setError('Yêu cầu cấu hình ít nhất một vòng phỏng vấn.');
      return;
    }

    try {
      setSubmitting(true);
      setError(null);

      const requestPayload: CreateJobPostingRequest = {
        title: title.trim(),
        department: department.trim() || undefined,
        jobDescription: jobDescription.trim(),
        interviewMode,
        location: interviewMode !== 'remote' ? location.trim() : undefined,
        workMode,
        employmentType: workMode,
        salaryIsNegotiable,
        salaryMin: salaryIsNegotiable || salaryMin === '' ? undefined : Number(salaryMin),
        salaryMax: salaryIsNegotiable || salaryMax === '' ? undefined : Number(salaryMax),
        salaryCurrency: salaryIsNegotiable ? undefined : salaryCurrency,
        experienceLevel,
        isUrgent,
        isPublicListing,
        skills,
        roundConfigs: rounds,
        languageRequirement: languageRequirement.trim() || undefined,
      };

      await jobService.createJobPosting(requestPayload);
      navigate('/admin/jobs');
    } catch (err: any) {
      console.error(err);
      setError(err?.response?.data?.message || err.message || 'Có lỗi xảy ra khi tạo tin tuyển dụng.');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="p-6 lg:p-8">
      {/* Header */}
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-6">
        <button
          onClick={() => navigate('/admin/jobs')}
          className="inline-flex items-center gap-2 text-sm text-white/50 hover:text-white mb-4 transition-colors"
        >
          <ArrowLeft className="w-4 h-4" />
          Quay lại danh sách
        </button>
        <h1 className="text-3xl font-semibold text-white mb-1">Tạo tin tuyển dụng mới</h1>
        <p className="text-text-secondary text-sm">Thiết lập thông tin tuyển dụng & cấu hình quy trình phỏng vấn AI</p>
      </motion.div>

      {error && (
        <div className="p-4 rounded-xl bg-red-500/10 border border-red-500/20 text-red-400 text-sm mb-6 max-w-4xl">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="max-w-4xl grid lg:grid-cols-3 gap-6">
        {/* Left Column - General Info */}
        <div className="lg:col-span-2 space-y-6">
          <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 space-y-5">
            <h2 className="text-lg font-semibold text-white border-b border-white/5 pb-2">Thông tin chung</h2>

            {/* Title */}
            <div>
              <label className="block text-sm font-medium text-white mb-2">Tiêu đề công việc *</label>
              <input
                type="text"
                value={title}
                onChange={e => setTitle(e.target.value)}
                placeholder="VD: Senior Frontend Developer"
                className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50"
                required
              />
            </div>

            {/* Department & Experience */}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-white mb-2">Phòng ban / Department</label>
                <input
                  type="text"
                  value={department}
                  onChange={e => setDepartment(e.target.value)}
                  placeholder="VD: Product Development"
                  className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-white mb-2">Cấp bậc yêu cầu</label>
                <select
                  value={experienceLevel}
                  onChange={e => setExperienceLevel(e.target.value)}
                  className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white focus:outline-none focus:border-accent-primary/50"
                >
                  <option value="intern">Intern / Thực tập sinh</option>
                  <option value="junior">Junior / Nhân viên</option>
                  <option value="mid">Middle / Trưởng nhóm phụ trách</option>
                  <option value="senior">Senior / Kỹ sư cao cấp</option>
                  <option value="lead">Lead / Quản lý / Giám đốc</option>
                </select>
              </div>
            </div>

            {/* Description */}
            <div>
              <label className="block text-sm font-medium text-white mb-2">Mô tả công việc *</label>
              <textarea
                value={jobDescription}
                onChange={e => setJobDescription(e.target.value)}
                rows={6}
                placeholder="Mô tả chi tiết nhiệm vụ, trách nhiệm công việc..."
                className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 resize-none"
                required
              />
            </div>

            {/* Skills Tagging */}
            <div>
              <label className="block text-sm font-medium text-white mb-2">Yêu cầu kỹ năng (Nhấn Enter để thêm tag)</label>
              <input
                type="text"
                value={skillInput}
                onChange={e => setSkillInput(e.target.value)}
                onKeyDown={handleAddSkill}
                placeholder="VD: React, TypeScript, SQL..."
                className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50"
              />
              <div className="flex flex-wrap gap-2 mt-3">
                {skills.map(tag => (
                  <span key={tag} className="flex items-center gap-1 px-3 py-1 rounded-full bg-accent-primary/20 text-accent-primary text-xs font-medium">
                    {tag}
                    <button type="button" onClick={() => handleRemoveSkill(tag)} className="hover:text-red-400 font-bold ml-1">×</button>
                  </span>
                ))}
              </div>
            </div>
          </motion.div>

          {/* Salary and Working Mode Settings */}
          <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 space-y-5">
            <h2 className="text-lg font-semibold text-white border-b border-white/5 pb-2">Chế độ đãi ngộ & Địa điểm</h2>

            {/* Work Mode & Interview Mode */}
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-white mb-2">Hình thức làm việc</label>
                <select
                  value={workMode}
                  onChange={e => setWorkMode(e.target.value)}
                  className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white focus:outline-none focus:border-accent-primary/50"
                >
                  <option value="fulltime">Toàn thời gian (Full-time)</option>
                  <option value="parttime">Bán thời gian (Part-time)</option>
                  <option value="contract">Hợp đồng (Contract)</option>
                  <option value="internship">Thực tập (Internship)</option>
                </select>
              </div>
              <div>
                <label className="block text-sm font-medium text-white mb-2">Chế độ phỏng vấn *</label>
                <select
                  value={interviewMode}
                  onChange={e => setInterviewMode(e.target.value as any)}
                  className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white focus:outline-none focus:border-accent-primary/50"
                >
                  <option value="remote">Online (Remote)</option>
                  <option value="onsite">Trực tiếp tại VP (On-site)</option>
                  <option value="both">Hỗn hợp (Hybrid)</option>
                </select>
              </div>
            </div>

            {/* Location (conditional) */}
            {interviewMode !== 'remote' && (
              <motion.div initial={{ opacity: 0, height: 0 }} animate={{ opacity: 1, height: 'auto' }}>
                <label className="block text-sm font-medium text-white mb-2">Địa điểm làm việc *</label>
                <input
                  type="text"
                  value={location}
                  onChange={e => setLocation(e.target.value)}
                  placeholder="VD: Tòa nhà FPT, Quận 9, TP. Hồ Chí Minh"
                  className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50"
                  required
                />
              </motion.div>
            )}

            {/* Salary */}
            <div>
              <div className="flex items-center justify-between mb-2">
                <label className="text-sm font-medium text-white">Mức lương</label>
                <label className="flex items-center gap-2 text-xs text-text-secondary cursor-pointer">
                  <input
                    type="checkbox"
                    checked={salaryIsNegotiable}
                    onChange={e => setSalaryIsNegotiable(e.target.checked)}
                    className="rounded border-white/10 bg-white/5 accent-accent-primary focus:ring-0"
                  />
                  Lương thỏa thuận
                </label>
              </div>

              {!salaryIsNegotiable && (
                <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="grid grid-cols-3 gap-2">
                  <input
                    type="number"
                    value={salaryMin}
                    onChange={e => setSalaryMin(e.target.value === '' ? '' : Number(e.target.value))}
                    placeholder="Tối thiểu (Min)"
                    className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50"
                  />
                  <input
                    type="number"
                    value={salaryMax}
                    onChange={e => setSalaryMax(e.target.value === '' ? '' : Number(e.target.value))}
                    placeholder="Tối đa (Max)"
                    className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50"
                  />
                  <select
                    value={salaryCurrency}
                    onChange={e => setSalaryCurrency(e.target.value)}
                    className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white focus:outline-none focus:border-accent-primary/50"
                  >
                    <option value="USD">USD ($)</option>
                    <option value="VND">VND (đ)</option>
                  </select>
                </motion.div>
              )}
            </div>
          </motion.div>
        </div>

        {/* Right Column - Rounds & Metadata */}
        <div className="space-y-6">
          {/* Rounds Configuration */}
          <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 space-y-4">
            <div className="flex items-center justify-between border-b border-white/5 pb-2">
              <h2 className="text-lg font-semibold text-white">Vòng phỏng vấn AI</h2>
              <button
                type="button"
                onClick={handleAddRound}
                className="text-xs text-accent-primary hover:text-white flex items-center gap-1 transition-colors"
              >
                <PlusCircle className="w-3.5 h-3.5" />
                Thêm vòng
              </button>
            </div>

            <div className="space-y-4 max-h-[300px] overflow-y-auto pr-1">
              <AnimatePresence initial={false}>
                {rounds.map((round, idx) => (
                  <motion.div
                    key={round.roundNumber}
                    initial={{ opacity: 0, scale: 0.95 }}
                    animate={{ opacity: 1, scale: 1 }}
                    exit={{ opacity: 0, scale: 0.95 }}
                    className="p-4 rounded-xl bg-white/[0.02] border border-white/5 space-y-3 relative group"
                  >
                    {rounds.length > 1 && (
                      <button
                        type="button"
                        onClick={() => handleRemoveRound(round.roundNumber)}
                        className="absolute top-3 right-3 text-white/30 hover:text-red-400 opacity-0 group-hover:opacity-100 transition-opacity"
                      >
                        <Trash2 className="w-4 h-4" />
                      </button>
                    )}

                    <div className="text-sm font-semibold text-white">Vòng {round.roundNumber}</div>

                    {/* Round Type */}
                    <div>
                      <label className="block text-xs font-medium text-white/60 mb-1">Loại vòng phỏng vấn</label>
                      <select
                        value={round.roundType}
                        onChange={e => handleRoundChange(idx, 'roundType', e.target.value)}
                        className="w-full px-3 py-2 rounded-lg bg-white/5 border border-white/10 text-white text-xs focus:outline-none"
                      >
                        <option value="screening">Screening / Sơ loại</option>
                        <option value="technical">Technical / Chuyên môn</option>
                      </select>
                    </div>

                    {/* Language & Duration */}
                    <div className="grid grid-cols-2 gap-2">
                      <div>
                        <label className="block text-xs font-medium text-white/60 mb-1">Ngôn ngữ</label>
                        <select
                          value={round.interviewLanguage}
                          onChange={e => handleRoundChange(idx, 'interviewLanguage', e.target.value)}
                          className="w-full px-3 py-2 rounded-lg bg-white/5 border border-white/10 text-white text-xs focus:outline-none"
                        >
                          <option value="vi">Tiếng Việt</option>
                          <option value="en">Tiếng Anh</option>
                        </select>
                      </div>
                      <div>
                        <label className="block text-xs font-medium text-white/60 mb-1">Thời gian (Phút)</label>
                        <input
                          type="number"
                          value={round.maxDurationMinutes}
                          onChange={e => handleRoundChange(idx, 'maxDurationMinutes', Number(e.target.value))}
                          className="w-full px-3 py-2 rounded-lg bg-white/5 border border-white/10 text-white text-xs focus:outline-none"
                        />
                      </div>
                    </div>
                  </motion.div>
                ))}
              </AnimatePresence>
            </div>
          </motion.div>

          {/* Visibility and Action */}
          <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 space-y-4">
            <h2 className="text-md font-semibold text-white border-b border-white/5 pb-2">Thiết lập hiển thị</h2>

            <div>
              <label className="block text-sm font-medium text-white mb-2">Yêu cầu ngôn ngữ</label>
              <input
                type="text"
                value={languageRequirement}
                onChange={e => setLanguageRequirement(e.target.value)}
                placeholder="VD: Tiếng Anh (IELTS 6.5), Tiếng Nhật (N3)..."
                className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 text-sm"
              />
            </div>

            <div className="space-y-3 text-sm">
              <label className="flex items-center gap-3 text-white/70 cursor-pointer">
                <input
                  type="checkbox"
                  checked={isPublicListing}
                  onChange={e => setIsPublicListing(e.target.checked)}
                  className="rounded border-white/10 bg-white/5 accent-accent-primary focus:ring-0"
                />
                Công khai tin tuyển dụng (Job Board)
              </label>

              <label className="flex items-center gap-3 text-white/70 cursor-pointer">
                <input
                  type="checkbox"
                  checked={isUrgent}
                  onChange={e => setIsUrgent(e.target.checked)}
                  className="rounded border-white/10 bg-white/5 accent-accent-primary focus:ring-0"
                />
                Tuyển gấp (Hiển thị nhãn nổi bật)
              </label>
            </div>

            <div className="flex gap-3 pt-2">
              <button
                type="button"
                onClick={() => navigate('/admin/jobs')}
                className="flex-1 px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white font-medium text-center text-sm hover:bg-white/10 transition-colors"
                disabled={submitting}
              >
                Hủy
              </button>
              <button
                type="submit"
                className="flex-1 px-4 py-3 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium text-sm hover:opacity-90 transition-opacity flex items-center justify-center gap-1.5"
                disabled={submitting}
              >
                {submitting ? (
                  <>
                    <Loader2 className="w-4 h-4 animate-spin" />
                    Đang lưu...
                  </>
                ) : (
                  <>
                    <Check className="w-4 h-4" />
                    Đăng tin
                  </>
                )}
              </button>
            </div>
          </motion.div>
        </div>
      </form>
    </div>
  );
}
