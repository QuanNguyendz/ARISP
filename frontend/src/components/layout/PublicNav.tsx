import { Link } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Brain, Menu, X } from 'lucide-react';
import { useState } from 'react';

export default function PublicNav() {
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  return (
    <nav className="fixed top-0 left-0 right-0 z-50 bg-bg-primary/80 backdrop-blur-xl border-b border-white/10">
      <div className="max-w-7xl mx-auto px-6">
        <div className="flex items-center justify-between h-16">
          {/* Logo */}
          <Link to="/" className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
              <Brain className="w-5 h-5 text-white" />
            </div>
            <span className="text-xl font-semibold text-white">ARISP</span>
          </Link>

          {/* Desktop Nav */}
          <div className="hidden md:flex items-center gap-8">
            <Link to="/jobs" className="text-text-secondary hover:text-white transition-colors">
              Tìm việc
            </Link>
            <Link to="/nha-tuyen-dung" className="text-text-secondary hover:text-white transition-colors">
              Dành cho NTD
            </Link>
            <Link to="/auth/login" className="text-text-secondary hover:text-white transition-colors">
              Đăng nhập
            </Link>
            <Link
              to="/auth/register"
              className="px-5 py-2 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity"
            >
              Đăng ký
            </Link>
          </div>

          {/* Mobile Menu Button */}
          <button
            onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
            className="md:hidden p-2 text-white"
          >
            {mobileMenuOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
          </button>
        </div>

        {/* Mobile Menu */}
        {mobileMenuOpen && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            className="md:hidden py-4 border-t border-white/10"
          >
            <div className="flex flex-col gap-4">
              <Link
                to="/jobs"
                className="text-text-secondary hover:text-white transition-colors py-2"
                onClick={() => setMobileMenuOpen(false)}
              >
                Tìm việc
              </Link>
              <Link
                to="/nha-tuyen-dung"
                className="text-text-secondary hover:text-white transition-colors py-2"
                onClick={() => setMobileMenuOpen(false)}
              >
                Dành cho NTD
              </Link>
              <Link
                to="/auth/login"
                className="text-text-secondary hover:text-white transition-colors py-2"
                onClick={() => setMobileMenuOpen(false)}
              >
                Đăng nhập
              </Link>
              <Link
                to="/auth/register"
                className="px-5 py-3 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium text-center"
                onClick={() => setMobileMenuOpen(false)}
              >
                Đăng ký
              </Link>
            </div>
          </motion.div>
        )}
      </div>
    </nav>
  );
}
