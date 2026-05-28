import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { KeyRound, User, FileText, CheckCircle, Play, Briefcase, MapPin, DollarSign, Building2, ArrowLeft } from 'lucide-react';
import { LoadingButton } from '@components/common';

const mockInterviewData = {
  code: 'INT001',
  candidate: {
    name: 'Nguyễn Văn An',
    email: 'an.nguyen@email.com',
    phone: '0912 345 678',
    location: 'TP. Hồ Chí Minh',
  },
  job: {
    title: 'Senior Backend Developer',
    company: 'Tech Solutions Vietnam',
    logo: 'TV',
    location: 'TP. Hồ Chí Minh',
    salary: '$3,000 - $5,000',
    type: 'Full-time',
  },
  cv: {
    uploadedAt: '15/05/2026',
    fileName: 'NguyenVanAn_CV.pdf',
  },
  appliedDate: '18/05/2026',
};

export default function InterviewSchedulePage() {
  const navigate = useNavigate();
  const [code, setCode] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [interviewData, setInterviewData] = useState<typeof mockInterviewData | null>(null);
  const [isConfirmed, setIsConfirmed] = useState(false);
  const [error, setError] = useState('');

  const handleVerifyCode = async () => {
    if (!code.trim()) {
      setError('Vui lòng nhập mã phỏng vấn');
      return;
    }

    setIsLoading(true);
    setError('');

    setTimeout(() => {
      if (code.toUpperCase() === 'INT001' || code.toUpperCase() === 'TEST') {
        setInterviewData(mockInterviewData);
      } else {
        setError('Mã phỏng vấn không hợp lệ hoặc đã hết hạn');
      }
      setIsLoading(false);
    }, 1500);
  };

  const handleKeyPress = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleVerifyCode();
    }
  };

  const handleConfirm = () => {
    setIsConfirmed(true);
  };

  const handleStartInterview = () => {
    navigate(`/interview/room/${interviewData?.code || 'session'}`);
  };

  const handlePracticeInterview = () => {
    navigate('/interview/practice/demo');
  };

  return (
    <div className="min-h-screen bg-bg-primary py-8">
      <div className="max-w-3xl mx-auto px-6">
        {/* Header */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          className="mb-8"
        >
          <button
            onClick={() => navigate(-1)}
            className="flex items-center gap-2 text-text-secondary hover:text-white transition-colors mb-4"
          >
            <ArrowLeft className="w-4 h-4" />
            Quay lại
          </button>
          <h1 className="text-3xl font-bold text-white mb-2">Phòng vấn</h1>
          <p className="text-text-secondary">Nhập mã phỏng vấn để bắt đầu</p>
        </motion.div>

        {!interviewData ? (
          /* Bước 1: Nhập mã phỏng vấn */
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.1 }}
            className="bg-bg-secondary border border-white/10 rounded-2xl p-8"
          >
            <div className="text-center mb-8">
              <div className="w-20 h-20 mx-auto mb-6 rounded-2xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
                <KeyRound className="w-10 h-10 text-white" />
              </div>
              <h2 className="text-2xl font-semibold text-white mb-2">Nhập mã phỏng vấn</h2>
              <p className="text-text-secondary">
                Mã phỏng vấn được gửi qua email sau khi bạn ứng tuyển thành công
              </p>
            </div>

            <div className="space-y-4">
              <div>
                <input
                  type="text"
                  value={code}
                  onChange={(e) => setCode(e.target.value.toUpperCase())}
                  onKeyPress={handleKeyPress}
                  placeholder="VD: INT001"
                  className="w-full px-6 py-4 rounded-xl bg-white/5 border border-white/10 text-white text-center text-2xl font-mono placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary transition-colors"
                  maxLength={10}
                />
              </div>

              {error && (
                <motion.p
                  initial={{ opacity: 0, y: -10 }}
                  animate={{ opacity: 1, y: 0 }}
                  className="text-red-400 text-sm text-center"
                >
                  {error}
                </motion.p>
              )}

              <LoadingButton
                loading={isLoading}
                onClick={handleVerifyCode}
                fullWidth
                sx={{
                  background: 'linear-gradient(135deg, #06b6d4, #8b5cf6)',
                  color: 'white',
                  py: 2,
                  borderRadius: '12px',
                  fontSize: '1rem',
                  fontWeight: 600,
                }}
              >
                Xác thực mã
              </LoadingButton>
            </div>

            <div className="mt-6 pt-6 border-t border-white/10 text-center">
              <p className="text-sm text-text-tertiary">
                Bạn chưa có mã phỏng vấn?{' '}
                <a href="/jobs" className="text-accent-primary hover:underline">
                  Tìm việc ngay
                </a>
              </p>
            </div>
          </motion.div>
        ) : !isConfirmed ? (
          /* Bước 2: Hiển thị thông tin & Xác nhận */
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
          >
            {/* Job Info */}
            <div className="bg-bg-secondary border border-white/10 rounded-2xl p-6 mb-4">
              <div className="flex items-center gap-4 mb-4">
                <div className="w-14 h-14 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-white font-bold text-lg">
                  {interviewData.job.logo}
                </div>
                <div>
                  <h2 className="text-xl font-semibold text-white">{interviewData.job.title}</h2>
                  <p className="text-text-secondary">{interviewData.job.company}</p>
                </div>
              </div>
              <div className="flex flex-wrap gap-4 text-sm text-text-secondary">
                <span className="flex items-center gap-1">
                  <MapPin className="w-4 h-4" />
                  {interviewData.job.location}
                </span>
                <span className="flex items-center gap-1">
                  <DollarSign className="w-4 h-4" />
                  {interviewData.job.salary}
                </span>
                <span className="flex items-center gap-1">
                  <Building2 className="w-4 h-4" />
                  {interviewData.job.type}
                </span>
              </div>
            </div>

            {/* Candidate Info */}
            <div className="bg-bg-secondary border border-white/10 rounded-2xl p-6 mb-4">
              <div className="flex items-center gap-2 mb-4">
                <User className="w-5 h-5 text-accent-primary" />
                <h3 className="text-lg font-semibold text-white">Thông tin ứng viên</h3>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div>
                  <p className="text-sm text-text-tertiary mb-1">Họ và tên</p>
                  <p className="text-white">{interviewData.candidate.name}</p>
                </div>
                <div>
                  <p className="text-sm text-text-tertiary mb-1">Email</p>
                  <p className="text-white">{interviewData.candidate.email}</p>
                </div>
                <div>
                  <p className="text-sm text-text-tertiary mb-1">Số điện thoại</p>
                  <p className="text-white">{interviewData.candidate.phone}</p>
                </div>
                <div>
                  <p className="text-sm text-text-tertiary mb-1">Địa điểm</p>
                  <p className="text-white">{interviewData.candidate.location}</p>
                </div>
              </div>
            </div>

            {/* CV Info */}
            <div className="bg-bg-secondary border border-white/10 rounded-2xl p-6 mb-6">
              <div className="flex items-center gap-2 mb-4">
                <FileText className="w-5 h-5 text-accent-primary" />
                <h3 className="text-lg font-semibold text-white">CV đã nộp</h3>
              </div>
              <div className="flex items-center justify-between p-4 rounded-xl bg-white/5">
                <div className="flex items-center gap-3">
                  <FileText className="w-8 h-8 text-text-tertiary" />
                  <div>
                    <p className="text-white font-medium">{interviewData.cv.fileName}</p>
                    <p className="text-sm text-text-tertiary">Tải lên: {interviewData.cv.uploadedAt}</p>
                  </div>
                </div>
              </div>
            </div>

            {/* Confirm */}
            <div className="bg-gradient-to-br from-accent-primary/10 to-violet/10 border border-accent-primary/30 rounded-2xl p-6">
              <div className="flex items-start gap-3 mb-6">
                <CheckCircle className="w-6 h-6 text-accent-primary flex-shrink-0 mt-0.5" />
                <div>
                  <h3 className="text-lg font-semibold text-white mb-1">Xác nhận thông tin</h3>
                  <p className="text-text-secondary text-sm">
                    Vui lòng kiểm tra kỹ thông tin bên trên trước khi bắt đầu phỏng vấn. Thông tin phải chính xác.
                  </p>
                </div>
              </div>

              <button
                onClick={handleConfirm}
                className="w-full py-4 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-semibold hover:opacity-90 transition-opacity"
              >
                Tôi xác nhận thông tin chính xác
              </button>
            </div>
          </motion.div>
        ) : (
          /* Bước 3: Chọn loại phỏng vấn */
          <motion.div
            initial={{ opacity: 0, scale: 0.95 }}
            animate={{ opacity: 1, scale: 1 }}
            className="text-center"
          >
            <div className="mb-8">
              <div className="w-20 h-20 mx-auto mb-6 rounded-full bg-emerald-500/20 flex items-center justify-center">
                <CheckCircle className="w-10 h-10 text-emerald-400" />
              </div>
              <h2 className="text-2xl font-bold text-white mb-2">Xác nhận thành công!</h2>
              <p className="text-text-secondary">
                Bạn đã sẵn sàng để bắt đầu phỏng vấn
              </p>
            </div>

            {/* Thông tin đã xác nhận */}
            <div className="bg-bg-secondary border border-white/10 rounded-2xl p-6 mb-8 text-left">
              <div className="flex items-center gap-3 mb-4">
                <div className="w-10 h-10 rounded-lg bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-white font-bold">
                  {interviewData.job.logo}
                </div>
                <div>
                  <p className="font-semibold text-white">{interviewData.job.title}</p>
                  <p className="text-sm text-text-secondary">{interviewData.job.company}</p>
                </div>
              </div>
              <div className="flex items-center gap-2 text-sm text-emerald-400">
                <CheckCircle className="w-4 h-4" />
                <span>Thông tin đã được xác nhận</span>
              </div>
            </div>

            {/* 2 Button */}
            <div className="grid grid-cols-2 gap-4">
              <button
                onClick={handleStartInterview}
                className="p-6 rounded-2xl bg-gradient-to-br from-accent-primary to-violet hover:opacity-90 transition-opacity text-left group"
              >
                <div className="w-12 h-12 rounded-xl bg-white/20 flex items-center justify-center mb-4 group-hover:scale-110 transition-transform">
                  <Play className="w-6 h-6 text-white" />
                </div>
                <h3 className="text-lg font-semibold text-white mb-1">Bắt đầu phỏng vấn</h3>
                <p className="text-sm text-white/70">
                  Phỏng vấn chính thức với nhà tuyển dụng
                </p>
              </button>

              <button
                onClick={handlePracticeInterview}
                className="p-6 rounded-2xl bg-bg-secondary border border-white/10 hover:border-white/20 transition-colors text-left group"
              >
                <div className="w-12 h-12 rounded-xl bg-white/10 flex items-center justify-center mb-4 group-hover:bg-white/20 transition-colors">
                  <Briefcase className="w-6 h-6 text-white" />
                </div>
                <h3 className="text-lg font-semibold text-white mb-1">Thử phỏng vấn</h3>
                <p className="text-sm text-text-secondary">
                  Luyện tập trước với AI
                </p>
              </button>
            </div>

            {/* Note */}
            <div className="mt-8 p-4 rounded-xl bg-amber-500/10 border border-amber-500/20">
              <p className="text-sm text-amber-400 text-center">
                <strong>Lưu ý:</strong> Phỏng vấn sẽ được ghi hình và ghi âm. Hãy đảm bảo bạn ở trong môi trường yên tĩnh.
              </p>
            </div>
          </motion.div>
        )}
      </div>
    </div>
  );
}
