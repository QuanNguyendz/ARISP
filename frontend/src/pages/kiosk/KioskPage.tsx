import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Brain, KeyRound, ArrowRight, AlertCircle, CheckCircle } from 'lucide-react';
import { interviewService } from '@services/interview';

export default function KioskPage() {
  const navigate = useNavigate();
  const [code, setCode] = useState('');
  const [isValidating, setIsValidating] = useState(false);
  const [isValid, setIsValid] = useState<boolean | null>(null);
  const [error, setError] = useState<string | null>(null);

  const handleValidate = async () => {
    if (code.length < 6) return;
    setIsValidating(true);
    setError(null);

    try {
      const result = await interviewService.validateInterviewCode(code.toUpperCase());
      setIsValid(result.valid);
      if (result.valid && result.sessionId) {
        setTimeout(() => {
          navigate(`/interview/room/${result.sessionId}`);
        }, 1000);
      } else {
        setError('Mã không hợp lệ hoặc đã hết hạn');
      }
    } catch {
      setIsValid(false);
      setError('Đã xảy ra lỗi. Vui lòng thử lại.');
    } finally {
      setIsValidating(false);
    }
  };

  return (
    <div className="min-h-screen bg-bg-primary flex items-center justify-center p-8">
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        className="w-full max-w-md"
      >
        {/* Logo */}
        <div className="text-center mb-8">
          <div className="w-20 h-20 rounded-2xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center mx-auto mb-4">
            <Brain className="w-10 h-10 text-white" />
          </div>
          <h1 className="text-2xl font-semibold text-white mb-2">ARISP Interview Kiosk</h1>
          <p className="text-text-secondary">Nhập mã phỏng vấn để bắt đầu</p>
        </div>

        {/* Input */}
        <div className="p-8 rounded-2xl bg-white/5 border border-white/10">
          <div className="mb-6">
            <label className="block text-sm font-medium text-white/80 mb-3">
              Interview Code
            </label>
            <div className="relative">
              <KeyRound className="absolute left-4 top-1/2 -translate-y-1/2 w-6 h-6 text-text-tertiary" />
              <input
                type="text"
                value={code}
                onChange={(e) => {
                  setCode(e.target.value.toUpperCase());
                  setIsValid(null);
                  setError(null);
                }}
                onKeyPress={(e) => {
                  if (e.key === 'Enter') {
                    handleValidate();
                  }
                }}
                placeholder="ARX-XXXX"
                className={`w-full pl-14 pr-14 py-5 rounded-2xl bg-white/5 border-2 text-white text-center text-2xl tracking-widest placeholder:text-text-tertiary focus:outline-none transition-colors ${
                  isValid === true ? 'border-emerald-500' :
                  isValid === false ? 'border-red-500' :
                  'border-white/10 focus:border-accent-primary'
                }`}
                maxLength={10}
              />
              {isValid !== null && (
                <div className="absolute right-4 top-1/2 -translate-y-1/2">
                  {isValid ? (
                    <CheckCircle className="w-6 h-6 text-emerald-400" />
                  ) : (
                    <AlertCircle className="w-6 h-6 text-red-400" />
                  )}
                </div>
              )}
            </div>
            {error && (
              <motion.p
                initial={{ opacity: 0, y: -10 }}
                animate={{ opacity: 1, y: 0 }}
                className="text-sm text-red-400 mt-2"
              >
                {error}
              </motion.p>
            )}
          </div>

          <button
            onClick={handleValidate}
            disabled={code.length < 6 || isValidating}
            className="w-full py-4 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium flex items-center justify-center gap-2 disabled:opacity-50 disabled:cursor-not-allowed hover:opacity-90 transition-opacity"
          >
            {isValidating ? (
              <>
                <div className="w-5 h-5 border-2 border-white/30 border-t-white rounded-full animate-spin" />
                Đang xác thực...
              </>
            ) : (
              <>
                Bắt đầu phỏng vấn
                <ArrowRight className="w-5 h-5" />
              </>
            )}
          </button>

          <p className="text-xs text-text-tertiary text-center mt-4">
            Mã có hiệu lực trong 2 giờ kể từ khi được cấp
          </p>
        </div>

        {/* Demo codes */}
        <div className="mt-6 p-4 rounded-xl bg-white/5 border border-white/10">
          <p className="text-xs text-text-tertiary text-center mb-3">Demo codes để test:</p>
          <div className="flex items-center justify-center gap-4">
            {['ARX-1234', 'ISP-5678', 'DEMO-001'].map((demoCode) => (
              <button
                key={demoCode}
                onClick={() => {
                  setCode(demoCode);
                  setIsValid(null);
                  setError(null);
                }}
                className="px-3 py-1.5 rounded-lg bg-white/5 text-xs text-text-secondary hover:bg-white/10 transition-colors"
              >
                {demoCode}
              </button>
            ))}
          </div>
        </div>
      </motion.div>
    </div>
  );
}
