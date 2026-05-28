import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Brain, Briefcase, FileText, Calendar, User, LogOut, Menu, X } from 'lucide-react';
import { useAuthStore } from '@store/auth/authStore';

const candidateLinks = [
  { label: 'Tìm việc', href: '/jobs', icon: Briefcase },
  { label: 'Việc đã ứng tuyển', href: '/candidate/applications', icon: FileText },
  { label: 'Phỏng vấn', href: '/candidate/interview', icon: Calendar },
];

export default function CandidateLayout({ children }: { children?: React.ReactNode }) {
  const [isScrolled, setIsScrolled] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const { user, logout, isAuthenticated } = useAuthStore();

  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 20);
    };
    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return (
    <div className="min-h-screen bg-bg-primary flex flex-col">
      {/* Navigation */}
      <motion.nav
        className={`fixed top-0 left-0 right-0 z-50 transition-all duration-500 ${
          isScrolled
            ? 'bg-black/90 backdrop-blur-xl border-b border-white/5'
            : 'bg-bg-primary/80 backdrop-blur-md'
        }`}
        initial={{ y: -20, opacity: 0 }}
        animate={{ y: 0, opacity: 1 }}
        transition={{ duration: 0.8 }}
      >
        <div className="max-w-7xl mx-auto px-6">
          <div className="flex items-center justify-between h-16">
            {/* Logo */}
            <Link to="/jobs" className="flex items-center gap-2.5">
              <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
                <Brain className="w-5 h-5 text-white" />
              </div>
              <div>
                <span className="text-base font-semibold text-white">ARISP</span>
                <span className="hidden sm:block text-xs text-text-tertiary">Tìm việc</span>
              </div>
            </Link>

            {/* Desktop Navigation */}
            <div className="hidden lg:flex items-center gap-1">
              {candidateLinks.map((link) => {
                const Icon = link.icon;
                return (
                  <Link
                    key={link.label}
                    to={link.href}
                    className="px-4 py-2 rounded-lg text-sm font-medium transition-all flex items-center gap-2 text-text-secondary hover:text-white hover:bg-white/5"
                  >
                    <Icon className="w-4 h-4" />
                    {link.label}
                  </Link>
                );
              })}
            </div>

            {/* Right Side - Auth */}
            <div className="hidden lg:flex items-center gap-3">
              <Link
                to="/auth/login"
                className="px-4 py-2 rounded-lg text-sm font-medium text-text-secondary hover:text-white hover:bg-white/10 transition-all flex items-center gap-2 border border-white/10"
              >
                Nhà tuyển dụng
              </Link>
              
              {isAuthenticated && user ? (
                <div className="flex items-center gap-3">
                  <Link
                    to="/candidate/dashboard"
                    className="flex items-center gap-2 px-4 py-2 rounded-xl bg-white/5 hover:bg-white/10 transition-colors"
                  >
                    <div className="w-8 h-8 rounded-full bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
                      <User className="w-4 h-4 text-white" />
                    </div>
                    <span className="text-sm font-medium text-white">{user.name || 'User'}</span>
                  </Link>
                  <button 
                    onClick={logout}
                    className="p-2 rounded-lg text-text-secondary hover:text-white hover:bg-white/10 transition-colors"
                  >
                    <LogOut className="w-5 h-5" />
                  </button>
                </div>
              ) : (
                <>
                  <Link
                    to="/auth/candidate-login"
                    className="px-4 py-2 text-sm font-medium text-text-secondary hover:text-white transition-colors"
                  >
                    Đăng nhập
                  </Link>
                  <Link
                    to="/auth/candidate-register"
                    className="px-5 py-2.5 rounded-full bg-gradient-to-r from-accent-primary to-violet text-white text-sm font-semibold hover:opacity-90 transition-opacity"
                  >
                    Đăng ký
                  </Link>
                </>
              )}
            </div>

            {/* Mobile Menu Button */}
            <button
              className="lg:hidden p-2 text-white/80 hover:text-white transition-colors"
              onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
            >
              {isMobileMenuOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
            </button>
          </div>
        </div>

        {/* Mobile Menu */}
        {isMobileMenuOpen && (
          <div className="lg:hidden px-6 py-4 border-t border-white/10">
            <div className="flex flex-col gap-2">
              {candidateLinks.map((link) => {
                const Icon = link.icon;
                return (
                  <Link
                    key={link.label}
                    to={link.href}
                    onClick={() => setIsMobileMenuOpen(false)}
                    className="flex items-center gap-3 px-4 py-3 rounded-lg text-sm font-medium transition-all text-text-secondary hover:text-white hover:bg-white/5"
                  >
                    <Icon className="w-5 h-5" />
                    {link.label}
                  </Link>
                );
              })}
            </div>
          </div>
        )}
      </motion.nav>

      {/* Main Content */}
      <main className="flex-1 pt-16">
        {children}
      </main>

      {/* Footer */}
      <footer className="bg-bg-secondary border-t border-white/5 py-8">
        <div className="max-w-7xl mx-auto px-6 text-center">
          <p className="text-text-tertiary text-sm">
            © 2026 ARISP. Tất cả quyền được bảo lưu.
          </p>
        </div>
      </footer>
    </div>
  );
}
