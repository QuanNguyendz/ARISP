import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Brain, Mail, Lock, ArrowRight, Eye, EyeOff, Loader2, AlertCircle } from 'lucide-react';
import { authService } from '@services/auth/authService';

export default function LoginPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const [oauthLoading, setOauthLoading] = useState(false);
  const [error, setError] = useState('');

  const handleOAuthLogin = () => {
    setOauthLoading(true);
    const returnUrl = `${window.location.origin}/auth/callback`;
    window.location.href = authService.buildOAuthRedirectUrl('Google', returnUrl);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setIsLoading(true);

    try {
      await authService.employerLogin({ email, password });
    } catch (err: any) {
      if (err.message !== 'Redirecting to Google sign-in') {
        setError(err.message || 'Đăng nhập thất bại. Vui lòng thử lại.');
        setOauthLoading(false);
      }
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-bg-primary flex">
      <div className="flex-1 flex items-center justify-center p-8">
        <motion.div
          initial={{ opacity: 0, x: -20 }}
          animate={{ opacity: 1, x: 0 }}
          transition={{ duration: 0.6 }}
          className="w-full max-w-md"
        >
          <div className="flex items-center gap-3 mb-12">
            <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
              <Brain className="w-6 h-6 text-white" />
            </div>
            <span className="text-2xl font-semibold text-white">ARISP</span>
          </div>

          <div className="mb-8">
            <h1 className="text-3xl font-semibold text-white mb-3">Chào mừng trở lại</h1>
            <p className="text-text-secondary">Đăng nhập để quản lý hoạt động tuyển dụng của bạn</p>
          </div>

          <form onSubmit={handleSubmit} className="space-y-6">
            <div>
              <label className="block text-sm font-medium text-white/80 mb-2">Email</label>
              <div className="relative">
                <Mail className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                <input
                  type="email"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  placeholder="you@company.com"
                  className="w-full pl-12 pr-4 py-4 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
                  required
                />
              </div>
            </div>

            <div>
              <div className="flex items-center justify-between mb-2">
                <label className="block text-sm font-medium text-white/80">Mật khẩu</label>
                <button
                  type="button"
                  onClick={() => navigate('/auth/forgot-password')}
                  className="text-sm text-accent-primary hover:text-accent-secondary transition-colors focus:outline-none"
                >
                  Quên mật khẩu?
                </button>
              </div>
              <div className="relative">
                <Lock className="absolute left-4 top-1/2 -translate-y-1/2 w-5 h-5 text-text-tertiary" />
                <input
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="••••••••"
                  className="w-full pl-12 pr-12 py-4 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 transition-colors"
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
            </div>

            {error && (
              <div className="p-3 rounded-xl bg-red-500/10 border border-red-500/20 flex items-center gap-2">
                <AlertCircle className="w-4 h-4 text-red-400 flex-shrink-0" />
                <p className="text-sm text-red-300">{error}</p>
              </div>
            )}

            <div className="flex items-center gap-3">
              <input
                type="checkbox"
                id="remember"
                className="w-5 h-5 rounded border-white/20 bg-white/5 text-accent-primary focus:ring-accent-primary focus:ring-offset-0"
              />
              <label htmlFor="remember" className="text-sm text-text-secondary">
                Ghi nhớ đăng nhập
              </label>
            </div>

            <motion.button
              type="submit"
              disabled={isLoading}
              whileHover={{ scale: 1.02 }}
              whileTap={{ scale: 0.98 }}
              className="w-full py-4 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed"
            >
              {isLoading ? (
                <Loader2 className="w-5 h-5 animate-spin" />
              ) : (
                <>
                  Đăng nhập
                  <ArrowRight className="w-5 h-5" />
                </>
              )}
            </motion.button>
          </form>

          <div className="relative my-8">
            <div className="absolute inset-0 flex items-center">
              <div className="w-full border-t border-white/10" />
            </div>
            <div className="relative flex justify-center text-sm">
              <span className="px-4 bg-bg-primary text-text-tertiary">hoặc</span>
            </div>
          </div>

          <button
            onClick={handleOAuthLogin}
            disabled={oauthLoading}
            className="w-full py-4 rounded-xl bg-white/5 border border-white/10 text-white font-medium hover:bg-white/10 transition-colors flex items-center justify-center gap-3 disabled:opacity-50"
          >
            {oauthLoading ? (
              <Loader2 className="w-5 h-5 animate-spin" />
            ) : (
              <svg className="w-5 h-5" viewBox="0 0 24 24">
                <path fill="currentColor" d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z" />
              </svg>
            )}
            Đăng nhập với Google
          </button>

          <p className="text-center mt-8 text-text-secondary">
            Chưa có tài khoản?{' '}
            <Link
              to="/auth/register"
              className="text-accent-primary hover:text-accent-secondary transition-colors font-medium"
            >
              Đăng ký ngay
            </Link>
          </p>

          <p className="text-center mt-4">
            <Link to="/auth/candidate-login" className="text-sm text-text-tertiary hover:text-white transition-colors">
              ← Dành cho ứng viên
            </Link>
          </p>
        </motion.div>
      </div>

      <div className="hidden lg:flex flex-1 items-center justify-center relative overflow-hidden bg-gradient-to-br from-slate-950 via-slate-900 to-accent-primary/20">
        <div className="absolute inset-0 bg-[radial-gradient(circle_at_20%_20%,rgba(124,58,237,0.25),transparent_25%),radial-gradient(circle_at_80%_60%,rgba(59,130,246,0.2),transparent_30%)]" />
        <motion.div
          initial={{ opacity: 0, scale: 0.95 }}
          animate={{ opacity: 1, scale: 1 }}
          transition={{ duration: 0.8, delay: 0.2 }}
          className="relative z-10 max-w-lg px-12 text-center"
        >
          <div className="w-24 h-24 mx-auto mb-8 rounded-3xl bg-white/10 backdrop-blur-md border border-white/10 flex items-center justify-center">
            <Brain className="w-12 h-12 text-white" />
          </div>
          <h2 className="text-4xl font-semibold text-white mb-4 leading-tight">
            Tuyển dụng thông minh cùng AI
          </h2>
          <p className="text-lg text-white/70 leading-relaxed">
            Tập trung quản lý pipeline tuyển dụng, đánh giá ứng viên và tối ưu trải nghiệm tuyển chọn trên một nền tảng thống nhất.
          </p>
        </motion.div>
      </div>
    </div>
  );
}
