import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Brain, Mail, Lock, Eye, EyeOff, User, Phone, ArrowRight, CheckCircle, Loader2 } from 'lucide-react';
import { authService } from '@services/auth/authService';

export default function CandidateRegisterPage() {
  const navigate = useNavigate();
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirmPassword, setShowConfirmPassword] = useState(false);
  const [fullName, setFullName] = useState('');
  const [email, setEmail] = useState('');
  const [phone, setPhone] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [agreeTerms, setAgreeTerms] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState('');

  const passwordRequirements = [
    { label: 'Ít nhất 8 ký tự', met: password.length >= 8 },
    { label: 'Có chữ hoa', met: /[A-Z]/.test(password) },
    { label: 'Có số', met: /[0-9]/.test(password) },
    { label: 'Có ký tự đặc biệt', met: /[!@#$%^&*]/.test(password) },
  ];

  const allRequirementsMet = passwordRequirements.every((r) => r.met);
  const passwordsMatch = password === confirmPassword && confirmPassword.length > 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');

    if (!allRequirementsMet) {
      setError('Mật khẩu chưa đủ mạnh.');
      return;
    }
    if (!passwordsMatch) {
      setError('Mật khẩu xác nhận không khớp.');
      return;
    }
    if (!agreeTerms) {
      setError('Bạn cần đồng ý với điều khoản sử dụng.');
      return;
    }

    setIsLoading(true);
    try {
      await authService.candidateRegister({ email, password, fullName, phone });
      navigate('/dang-nhap-ung-vien?registered=true');
    } catch (err: any) {
      setError(err.message || 'Đăng ký thất bại. Vui lòng thử lại.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-bg-primary flex">
      {/* Left Side - Form */}
      <div className="flex-1 flex items-center justify-center p-8">
        <motion.div
          initial={{ opacity: 0, x: -20 }}
          animate={{ opacity: 1, x: 0 }}
          className="w-full max-w-md"
        >
          {/* Logo */}
          <Link to="/" className="flex items-center gap-2.5 mb-8">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
              <Brain className="w-5 h-5 text-white" />
            </div>
            <div>
              <span className="text-lg font-semibold text-white">ARISP</span>
              <span className="block text-xs text-text-tertiary">Tìm việc</span>
            </div>
          </Link>

          {/* Heading */}
          <div className="mb-8">
            <h1 className="text-3xl font-bold text-white mb-2">Tạo tài khoản mới</h1>
            <p className="text-text-secondary">
              Đăng ký ngay để bắt đầu tìm kiếm việc làm phù hợp
            </p>
          </div>

          {/* Form */}
          <form onSubmit={handleSubmit} className="space-y-5">
            {/* Full Name */}
            <div>
              <label className="block text-sm font-medium text-text-secondary mb-2">
                Họ và tên
              </label>
              <div className="relative">
                <User className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                <input
                  type="text"
                  value={fullName}
                  onChange={(e) => setFullName(e.target.value)}
                  placeholder="Nguyễn Văn A"
                  className="w-full pl-12 pr-4 py-3.5 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
                  required
                />
              </div>
            </div>

            {/* Email */}
            <div>
              <label className="block text-sm font-medium text-text-secondary mb-2">
                Email
              </label>
              <div className="relative">
                <Mail className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="email@example.com"
                  className="w-full pl-12 pr-4 py-3.5 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
                  required
                />
              </div>
            </div>

            {/* Phone */}
            <div>
              <label className="block text-sm font-medium text-text-secondary mb-2">
                Số điện thoại
              </label>
              <div className="relative">
                <Phone className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                <input
                  type="tel"
                  value={phone}
                  onChange={(e) => setPhone(e.target.value)}
                  placeholder="0901 234 567"
                  className="w-full pl-12 pr-4 py-3.5 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
                />
              </div>
            </div>

            {/* Password */}
            <div>
              <label className="block text-sm font-medium text-text-secondary mb-2">
                Mật khẩu
              </label>
              <div className="relative">
                <Lock className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="Tạo mật khẩu"
                  className="w-full pl-12 pr-12 py-3.5 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
                  required
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  className="absolute right-4 top-1/2 -translate-y-1/2 text-text-tertiary hover:text-white transition-colors"
                >
                  {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                </button>
              </div>

              {/* Password Strength */}
              <div className="mt-3 space-y-2">
                {passwordRequirements.map((req) => (
                  <div
                    key={req.label}
                    className={`flex items-center gap-2 text-xs ${
                      req.met ? 'text-emerald-400' : 'text-text-tertiary'
                    }`}
                  >
                    {req.met ? (
                      <CheckCircle className="w-3.5 h-3.5" />
                    ) : (
                      <div className="w-3.5 h-3.5 rounded-full border border-current" />
                    )}
                    {req.label}
                  </div>
                ))}
              </div>
            </div>

            {/* Confirm Password */}
            <div>
              <label className="block text-sm font-medium text-text-secondary mb-2">
                Xác nhận mật khẩu
              </label>
              <div className="relative">
                <Lock className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                <input
                  type={showConfirmPassword ? 'text' : 'password'}
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  placeholder="Nhập lại mật khẩu"
                  className="w-full pl-12 pr-12 py-3.5 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
                  required
                />
                <button
                  type="button"
                  onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                  className="absolute right-4 top-1/2 -translate-y-1/2 text-text-tertiary hover:text-white transition-colors"
                >
                  {showConfirmPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                </button>
              </div>
              {confirmPassword.length > 0 && !passwordsMatch && (
                <p className="text-red-400 text-xs mt-1">Mật khẩu không khớp</p>
              )}
            </div>

            {/* Terms */}
            <label className="flex items-start gap-3 cursor-pointer">
              <input
                type="checkbox"
                checked={agreeTerms}
                onChange={(e) => setAgreeTerms(e.target.checked)}
                className="w-5 h-5 mt-0.5 rounded border-white/20 bg-white/5 text-accent-primary focus:ring-accent-primary focus:ring-offset-0"
              />
              <span className="text-sm text-text-secondary">
                Tôi đồng ý với{' '}
              <Link to="/dang-nhap-ung-vien" className="text-accent-primary hover:underline">
                  Điều khoản sử dụng
                </Link>{' '}
                và{' '}
                <Link to="/dang-nhap-ung-vien" className="text-accent-primary hover:underline">
                  Chính sách bảo mật
                </Link>
              </span>
            </label>

            {/* Error */}
            {error && (
              <p className="text-red-400 text-sm text-center">{error}</p>
            )}

            {/* Submit */}
            <button
              type="submit"
              disabled={isLoading || !allRequirementsMet || !passwordsMatch || !agreeTerms}
              className="w-full py-3.5 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-semibold hover:opacity-90 transition-opacity flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isLoading ? (
                <>
                  <Loader2 className="w-5 h-5 animate-spin" />
                  Đang đăng ký...
                </>
              ) : (
                <>
                  Tạo tài khoản
                  <ArrowRight className="w-5 h-5" />
                </>
              )}
            </button>
          </form>

          {/* Login Link */}
          <p className="mt-8 text-center text-text-secondary">
            Đã có tài khoản?{' '}
            <Link
              to="/dang-nhap-ung-vien"
              className="text-accent-primary hover:text-accent-secondary font-medium transition-colors"
            >
              Đăng nhập ngay
            </Link>
          </p>

          {/* Back to Employer */}
          <p className="mt-4 text-center">
            <Link
              to="/dang-ky"
              className="text-sm text-text-tertiary hover:text-white transition-colors"
            >
              ← Dành cho nhà tuyển dụng
            </Link>
          </p>
        </motion.div>
      </div>

      {/* Right Side - Benefits */}
      <div className="hidden lg:flex flex-1 items-center justify-center bg-gradient-to-br from-accent-primary/20 via-violet/10 to-bg-primary p-12">
        <motion.div
          initial={{ opacity: 0, scale: 0.9 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ delay: 0.2 }}
          className="max-w-md"
        >
          <h2 className="text-2xl font-bold text-white mb-8 text-center">
            Tại sao chọn ARISP?
          </h2>

          <div className="space-y-6">
            {[
              {
                title: '10,000+ việc làm chất lượng',
                desc: 'Cập nhật việc làm mới nhất từ các công ty hàng đầu',
              },
              {
                title: 'Phỏng vấn với AI',
                desc: 'Trải nghiệm phỏng vấn ảo, luyện tập trước khi thật sự phỏng vấn',
              },
              {
                title: 'Hồ sơ nổi bật',
                desc: 'Tạo hồ sơ chuyên nghiệp, thu hút nhà tuyển dụng',
              },
              {
                title: 'Theo dõi đơn ứng tuyển',
                desc: 'Dễ dàng quản lý và theo dõi tất cả đơn ứng tuyển',
              },
            ].map((benefit, index) => (
              <motion.div
                key={benefit.title}
                initial={{ opacity: 0, x: 20 }}
                animate={{ opacity: 1, x: 0 }}
                transition={{ delay: 0.3 + index * 0.1 }}
                className="flex items-start gap-4 p-4 rounded-xl bg-white/5 border border-white/5"
              >
                <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center flex-shrink-0">
                  <CheckCircle className="w-5 h-5 text-white" />
                </div>
                <div>
                  <h3 className="font-semibold text-white mb-1">{benefit.title}</h3>
                  <p className="text-sm text-text-secondary">{benefit.desc}</p>
                </div>
              </motion.div>
            ))}
          </div>
        </motion.div>
      </div>
    </div>
  );
}
