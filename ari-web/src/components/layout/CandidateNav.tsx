import { useState, useEffect } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { Brain, Briefcase, FileText, Calendar, User, LogOut, Menu, X, Building2 } from 'lucide-react';
import { useAuthStore } from '@store/auth';

const candidateLinks = [
  { label: 'Tìm việc', href: '/jobs', icon: Briefcase },
  { label: 'Việc đã ứng tuyển', href: '/candidate/applications', icon: FileText },
  { label: 'Phỏng vấn', href: '/candidate/interview', icon: Calendar },
];

export default function CandidateNav() {
  const [isScrolled, setIsScrolled] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const { user, logout, isAuthenticated } = useAuthStore();
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const location = useLocation();

  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 20);
    };
    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  return (
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
          <Link to="/" className="flex items-center gap-2.5">
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
              const isActive = location.pathname === link.href;
              return (
                <Link
                  key={link.label}
                  to={link.href}
                  className={`px-4 py-2 rounded-lg text-sm font-medium transition-all flex items-center gap-2 ${
                    isActive
                      ? 'bg-accent-primary/20 text-accent-primary'
                      : 'text-text-secondary hover:text-white hover:bg-white/5'
                  }`}
                >
                  <Icon className="w-4 h-4" />
                  {link.label}
                </Link>
              );
            })}
          </div>

          {/* Right Side - Auth */}
          <div className="hidden lg:flex items-center gap-3">
            {/* Button chuyển sang NTD */}
            <Link
              to="/employer"
              className="px-4 py-2 rounded-lg text-sm font-medium text-text-secondary hover:text-white hover:bg-white/10 transition-all flex items-center gap-2 border border-white/10"
            >
              <Building2 className="w-4 h-4" />
              Nhà tuyển dụng
            </Link>
            
            {isAuthenticated && user ? (
              <div className="relative">
                <button
                  onClick={() => setIsDropdownOpen(!isDropdownOpen)}
                  className="flex items-center gap-3 px-4 py-2 rounded-xl bg-white/5 hover:bg-white/10 transition-colors"
                >
                  <div className="w-8 h-8 rounded-full bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
                    <User className="w-4 h-4 text-white" />
                  </div>
                  <span className="text-sm font-medium text-white">{user.name || 'User'}</span>
                </button>
                
                {isDropdownOpen && (
                  <motion.div
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    className="absolute right-0 mt-2 w-56 glass rounded-xl p-2"
                  >
                    <Link
                      to="/candidate/portal"
                      className="flex items-center gap-3 px-4 py-3 rounded-lg text-sm text-text-secondary hover:text-white hover:bg-white/5 transition-colors"
                    >
                      <User className="w-4 h-4" />
                      Hồ sơ của tôi
                    </Link>
                    <Link
                      to="/candidate/applications"
                      className="flex items-center gap-3 px-4 py-3 rounded-lg text-sm text-text-secondary hover:text-white hover:bg-white/5 transition-colors"
                    >
                      <FileText className="w-4 h-4" />
                      Việc đã ứng tuyển
                    </Link>
                    <hr className="my-2 border-white/10" />
                    <button 
                      onClick={logout}
                      className="flex items-center gap-3 px-4 py-3 rounded-lg text-sm text-red-400 hover:bg-red-500/10 transition-colors w-full"
                    >
                      <LogOut className="w-4 h-4" />
                      Đăng xuất
                    </button>
                  </motion.div>
                )}
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
      <AnimatePresence>
        {isMobileMenuOpen && (
          <motion.div
            className="lg:hidden absolute top-full left-0 right-0 bg-black/95 backdrop-blur-xl border-b border-white/5"
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
          >
            <div className="px-6 py-6 space-y-2">
              {/* Button chuyển sang NTD - Mobile */}
              <Link
                to="/employer"
                className="flex items-center gap-3 px-4 py-3 rounded-lg text-sm font-medium text-text-secondary hover:text-white hover:bg-white/5 transition-colors border border-white/10"
              >
                <Building2 className="w-5 h-5" />
                Nhà tuyển dụng
              </Link>
              
              {candidateLinks.map((link) => {
                const Icon = link.icon;
                const isActive = location.pathname === link.href;
                return (
                  <Link
                    key={link.label}
                    to={link.href}
                    onClick={() => setIsMobileMenuOpen(false)}
                    className={`flex items-center gap-3 px-4 py-3 rounded-lg text-sm font-medium transition-all ${
                      isActive
                        ? 'bg-accent-primary/20 text-accent-primary'
                        : 'text-text-secondary hover:text-white hover:bg-white/5'
                    }`}
                  >
                    <Icon className="w-5 h-5" />
                    {link.label}
                  </Link>
                );
              })}
              
              <hr className="my-4 border-white/10" />
              
              {isAuthenticated ? (
                <div className="space-y-2">
                  <Link
                    to="/candidate/portal"
                    onClick={() => setIsMobileMenuOpen(false)}
                    className="flex items-center gap-3 px-4 py-3 rounded-lg text-sm text-text-secondary hover:text-white hover:bg-white/5 transition-colors"
                  >
                    <User className="w-5 h-5" />
                    Hồ sơ của tôi
                  </Link>
                  <button 
                    onClick={() => { logout(); setIsMobileMenuOpen(false); }}
                    className="flex items-center gap-3 px-4 py-3 rounded-lg text-sm text-red-400 hover:bg-red-500/10 transition-colors w-full"
                  >
                    <LogOut className="w-5 h-5" />
                    Đăng xuất
                  </button>
                </div>
              ) : (
                <div className="space-y-2">
                  <Link
                    to="/auth/candidate-login"
                    onClick={() => setIsMobileMenuOpen(false)}
                    className="block w-full px-4 py-3 text-sm font-medium text-text-secondary hover:text-white text-center rounded-lg hover:bg-white/5 transition-colors"
                  >
                    Đăng nhập
                  </Link>
                  <Link
                    to="/auth/candidate-register"
                    onClick={() => setIsMobileMenuOpen(false)}
                    className="block w-full px-4 py-3 text-sm font-semibold text-white text-center rounded-lg bg-gradient-to-r from-accent-primary to-violet"
                  >
                    Đăng ký
                  </Link>
                </div>
              )}
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.nav>
  );
}
