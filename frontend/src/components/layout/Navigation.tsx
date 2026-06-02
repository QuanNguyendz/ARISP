import { useState, useEffect } from 'react';
import { Link, useLocation } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import { Brain, Briefcase, LogOut, User } from 'lucide-react';
import { useAuthStore } from '@store/auth/authStore';

const navLinks = [
  { label: 'Features', href: '#features' },
  { label: 'How It Works', href: '#how-it-works' },
  { label: 'Interview', href: '#interview' },
  { label: 'Pricing', href: '#pricing' },
];

export default function Navigation() {
  const [isScrolled, setIsScrolled] = useState(false);
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false);
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const location = useLocation();
  const isEmployerPage = location.pathname === '/employer';
  const { user, logout, isAuthenticated } = useAuthStore();

  useEffect(() => {
    const handleScroll = () => {
      setIsScrolled(window.scrollY > 20);
    };
    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  const handleNavClick = (href: string) => {
    setIsMobileMenuOpen(false);
    const element = document.querySelector(href);
    if (element) {
      element.scrollIntoView({ behavior: 'smooth' });
    }
  };

  return (
    <motion.nav
      className={`fixed top-0 left-0 right-0 z-50 transition-all duration-500 ${
        isScrolled
          ? 'bg-black/80 backdrop-blur-2xl border-b border-white/5'
          : 'bg-transparent'
      }`}
      initial={{ y: -20, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      transition={{ duration: 0.8, ease: [0.25, 0.1, 0.25, 1] }}
    >
      <div className="max-w-6xl mx-auto px-6 sm:px-8">
        <div className="flex items-center justify-between h-16 sm:h-20">
          {/* Logo */}
          <Link
            to="/"
            className="flex items-center gap-2.5 group"
          >
            <div className="relative">
              <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
                <Brain className="w-4 h-4 text-white" />
              </div>
            </div>
            <span className="text-base font-medium tracking-tight text-white">
              ARISP
            </span>
          </Link>

          {/* Desktop Navigation */}
          <div className="hidden lg:flex items-center gap-1">
            {navLinks.map((link) => (
              <button
                key={link.label}
                onClick={() => handleNavClick(link.href)}
                className="px-4 py-2 text-sm font-medium text-white/60 hover:text-white transition-colors duration-300 rounded-lg hover:bg-white/5"
              >
                {link.label}
              </button>
            ))}
          </div>

          {/* Right Side Actions */}
          <div className="hidden lg:flex items-center gap-3">
            {/* Mode Switcher */}
            <Link
              to={isEmployerPage ? "/" : "/employer"}
              className="px-4 py-2 rounded-full text-sm font-medium transition-all flex items-center gap-2 bg-white/5 text-text-secondary hover:text-white hover:bg-white/10 border border-white/10"
            >
              <Briefcase className="w-4 h-4" />
              {isEmployerPage ? 'Tìm việc' : 'Nhà tuyển dụng'}
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
                    className="absolute right-0 mt-2 w-48 bg-black/90 backdrop-blur-xl border border-white/10 rounded-xl p-2"
                  >
                    <Link
                      to="/admin/dashboard"
                      onClick={() => setIsDropdownOpen(false)}
                      className="flex items-center gap-3 px-4 py-3 rounded-lg text-sm text-text-secondary hover:text-white hover:bg-white/5 transition-colors"
                    >
                      Dashboard
                    </Link>
                    <hr className="my-2 border-white/10" />
                    <button 
                      onClick={() => { logout(); setIsDropdownOpen(false); }}
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
                  to="/auth/login"
                  className="px-4 py-2 text-sm font-medium text-white/70 hover:text-white transition-colors duration-300"
                >
                  Login
                </Link>

                <Link
                  to="/auth/register"
                  className="px-5 py-2.5 rounded-full bg-white text-bg-primary font-medium text-sm hover:bg-white/90 transition-colors"
                >
                  Get started
                </Link>
              </>
            )}
          </div>

          {/* Mobile Menu Button */}
          <button
            className="lg:hidden p-2 text-white/80 hover:text-white transition-colors"
            onClick={() => setIsMobileMenuOpen(!isMobileMenuOpen)}
            aria-label="Toggle menu"
          >
            <svg
              className="w-6 h-6"
              fill="none"
              viewBox="0 0 24 24"
              stroke="currentColor"
            >
              {isMobileMenuOpen ? (
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M6 18L18 6M6 6l12 12" />
              ) : (
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={1.5} d="M4 6h16M4 12h16M4 18h16" />
              )}
            </svg>
          </button>
        </div>
      </div>

      {/* Mobile Menu */}
      <AnimatePresence>
        {isMobileMenuOpen && (
          <motion.div
            className="lg:hidden absolute top-full left-0 right-0 bg-black/95 backdrop-blur-2xl border-b border-white/5"
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            transition={{ duration: 0.3, ease: [0.25, 0.1, 0.25, 1] }}
          >
            <div className="px-6 py-6 space-y-1">
              {/* Mobile Mode Switcher */}
              <Link
                to={isEmployerPage ? "/" : "/employer"}
                className="flex items-center gap-2 p-2 mb-4 rounded-xl bg-white/5"
                onClick={() => setIsMobileMenuOpen(false)}
              >
                <Briefcase className="w-5 h-5 text-text-secondary" />
                <span className="text-text-secondary">{isEmployerPage ? 'Chuyển sang trang Tìm việc' : 'Chuyển sang NTD'}</span>
              </Link>
              
              {navLinks.map((link) => (
                <button
                  key={link.label}
                  onClick={() => handleNavClick(link.href)}
                  className="block w-full text-left px-4 py-3 text-base font-medium text-white/70 hover:text-white hover:bg-white/5 rounded-lg transition-colors duration-300"
                >
                  {link.label}
                </button>
              ))}

              <div className="pt-4 mt-4 border-t border-white/10 space-y-3">
                {isAuthenticated && user ? (
                  <>
                    <Link
                      to="/admin/dashboard"
                      onClick={() => setIsMobileMenuOpen(false)}
                      className="block w-full px-4 py-3 text-base font-medium text-white/70 hover:text-white text-left hover:bg-white/5 rounded-lg transition-colors duration-300"
                    >
                      Dashboard
                    </Link>
                    <button 
                      onClick={() => { logout(); setIsMobileMenuOpen(false); }}
                      className="flex items-center gap-3 px-4 py-3 rounded-lg text-sm text-red-400 hover:bg-red-500/10 transition-colors w-full"
                    >
                      <LogOut className="w-5 h-5" />
                      Đăng xuất
                    </button>
                  </>
                ) : (
                  <>
                    <Link
                      to="/auth/login"
                      className="block w-full px-4 py-3 text-base font-medium text-white/70 hover:text-white text-left hover:bg-white/5 rounded-lg transition-colors duration-300"
                      onClick={() => setIsMobileMenuOpen(false)}
                    >
                      Login
                    </Link>
                    <Link
                      to="/auth/register"
                      className="block w-full px-5 py-3 rounded-full bg-white text-bg-primary text-sm font-medium text-center hover:bg-white/90 transition-colors"
                      onClick={() => setIsMobileMenuOpen(false)}
                    >
                      Get started
                    </Link>
                  </>
                )}
              </div>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.nav>
  );
}
