import { useState } from 'react';
import { Outlet, Link, useLocation } from 'react-router-dom';
import { motion, AnimatePresence } from 'framer-motion';
import {
  LayoutDashboard,
  FileText,
  Users,
  BarChart3,
  Settings,
  Bell,
  ChevronLeft,
  ChevronRight,
  Menu,
  LogOut,
  BookOpen,
  Video,
  ClipboardList,
  UserCog,
  Clock,
} from 'lucide-react';
import { useAuthStore } from '@store/auth/authStore';

const sidebarItems = [
  { icon: LayoutDashboard, label: 'Tổng quan', path: '/hr/dashboard' },
  { icon: Clock, label: 'Chờ duyệt', path: '/hr/jobs/pending', badge: 'pending' },
  { icon: FileText, label: 'Tin tuyển dụng', path: '/hr/jobs' },
  { icon: Users, label: 'Ứng viên', path: '/hr/candidates' },
  { icon: ClipboardList, label: 'Đánh giá', path: '/hr/evaluations' },
  { icon: Video, label: 'Phỏng vấn', path: '/hr/interviews' },
  { icon: BookOpen, label: 'Playbooks', path: '/hr/playbooks' },
  { icon: BarChart3, label: 'Báo cáo', path: '/hr/reports' },
  { icon: UserCog, label: 'Nhóm', path: '/hr/team' },
  { icon: Settings, label: 'Cài đặt', path: '/hr/settings' },
];

const getInitials = (name?: string) => {
  if (!name) return 'U';
  const parts = name.trim().split(/\s+/);
  if (parts.length >= 2) {
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }
  return name.substring(0, Math.min(name.length, 2)).toUpperCase();
};

export default function HrLayout() {
  const location = useLocation();
  const [isSidebarCollapsed, setIsSidebarCollapsed] = useState(false);
  const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false);
  const { user } = useAuthStore();

  const toggleSidebar = () => {
    setIsSidebarCollapsed(!isSidebarCollapsed);
  };

  const isActive = (path: string) => {
    return location.pathname === path || location.pathname.startsWith(path + '/');
  };

  return (
    <div className="min-h-screen bg-bg-primary flex">
      {/* Desktop Sidebar */}
      <motion.aside
        animate={{ width: isSidebarCollapsed ? 72 : 260 }}
        transition={{ duration: 0.3, ease: [0.25, 0.1, 0.25, 1] }}
        className="hidden lg:flex flex-col bg-black/40 backdrop-blur-xl border-r border-white/5"
      >
        {/* Logo */}
        <div className="h-16 flex items-center px-4 border-b border-white/5">
          <Link to="/hr/dashboard" className="flex items-center gap-3">
            <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center flex-shrink-0">
              <LayoutDashboard className="w-4 h-4 text-white" />
            </div>
            <AnimatePresence>
              {!isSidebarCollapsed && (
                <motion.span
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  exit={{ opacity: 0 }}
                  className="text-base font-semibold text-white whitespace-nowrap"
                >
                  HR Admin
                </motion.span>
              )}
            </AnimatePresence>
          </Link>
        </div>

        {/* Navigation */}
        <nav className="flex-1 p-3 overflow-y-auto">
          <div className="space-y-1">
            {sidebarItems.map((item) => (
              <button
                key={item.path}
                onClick={() => window.location.href = item.path}
                className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all ${
                  isActive(item.path)
                    ? 'bg-accent-primary/20 text-accent-primary'
                    : 'text-white/50 hover:text-white hover:bg-white/5'
                }`}
              >
                <item.icon className="w-5 h-5 flex-shrink-0" />
                <AnimatePresence>
                  {!isSidebarCollapsed && (
                    <motion.span
                      initial={{ opacity: 0 }}
                      animate={{ opacity: 1 }}
                      exit={{ opacity: 0 }}
                      className="whitespace-nowrap flex items-center gap-2"
                    >
                      {item.label}
                      {item.badge && (
                        <span className="px-1.5 py-0.5 rounded-full bg-amber-500/20 text-amber-400 text-[10px] font-medium">
                          Mới
                        </span>
                      )}
                    </motion.span>
                  )}
                </AnimatePresence>
              </button>
            ))}
          </div>
        </nav>

        {/* Collapse Button */}
        <div className="p-3 border-t border-white/5">
          <button
            onClick={toggleSidebar}
            className="w-full flex items-center justify-center gap-3 px-3 py-2.5 rounded-xl text-sm text-white/50 hover:text-white hover:bg-white/5 transition-colors"
          >
            {isSidebarCollapsed ? (
              <ChevronRight className="w-5 h-5" />
            ) : (
              <>
                <ChevronLeft className="w-5 h-5" />
                <span>Thu gọn</span>
              </>
            )}
          </button>
        </div>

        {/* User */}
        <div className="p-3 border-t border-white/5">
          <div className="flex items-center gap-3 p-2 rounded-xl bg-white/5">
            <div className="w-9 h-9 rounded-full bg-gradient-to-br from-accent-primary/30 to-violet/30 flex items-center justify-center text-xs font-medium text-white flex-shrink-0">
              {getInitials(user?.name)}
            </div>
            <AnimatePresence>
              {!isSidebarCollapsed && (
                <motion.div
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  exit={{ opacity: 0 }}
                  className="flex-1 min-w-0"
                >
                  <p className="text-sm font-medium text-white truncate">{user?.name || 'User'}</p>
                  <p className="text-xs text-accent-primary truncate">HR Admin</p>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
          <AnimatePresence>
            {!isSidebarCollapsed && (
              <motion.button
                initial={{ opacity: 0 }}
                animate={{ opacity: 1 }}
                exit={{ opacity: 0 }}
                onClick={() => { useAuthStore.getState().logout(); window.location.href = '/auth/login'; }}
                className="w-full mt-2 flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm text-white/50 hover:text-red-400 hover:bg-red-500/10 transition-colors"
              >
                <LogOut className="w-5 h-5 flex-shrink-0" />
                <span>Đăng xuất</span>
              </motion.button>
            )}
          </AnimatePresence>
        </div>
      </motion.aside>

      {/* Mobile Sidebar Overlay */}
      <AnimatePresence>
        {isMobileSidebarOpen && (
          <>
            <motion.div
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="lg:hidden fixed inset-0 bg-black/60 z-40"
              onClick={() => setIsMobileSidebarOpen(false)}
            />
            <motion.aside
              initial={{ x: -280 }}
              animate={{ x: 0 }}
              exit={{ x: -280 }}
              transition={{ duration: 0.3, ease: [0.25, 0.1, 0.25, 1] }}
              className="lg:hidden fixed left-0 top-0 bottom-0 w-64 bg-black/95 backdrop-blur-xl border-r border-white/5 z-50 flex flex-col"
            >
              {/* Logo */}
              <div className="h-16 flex items-center justify-between px-4 border-b border-white/5">
                <Link to="/hr/dashboard" className="flex items-center gap-3">
                  <div className="w-9 h-9 rounded-lg bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
                    <LayoutDashboard className="w-4 h-4 text-white" />
                  </div>
                  <span className="text-base font-semibold text-white">HR Admin</span>
                </Link>
                <button
                  onClick={() => setIsMobileSidebarOpen(false)}
                  className="p-2 rounded-lg hover:bg-white/10"
                >
                  <ChevronLeft className="w-5 h-5 text-white/60" />
                </button>
              </div>

              {/* Navigation */}
              <nav className="flex-1 p-3 overflow-y-auto">
                <div className="space-y-1">
                  {sidebarItems.map((item) => (
                    <button
                      key={item.path}
                      onClick={() => {
                        window.location.href = item.path;
                        setIsMobileSidebarOpen(false);
                      }}
                      className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm font-medium transition-all ${
                        isActive(item.path)
                          ? 'bg-accent-primary/20 text-accent-primary'
                          : 'text-white/50 hover:text-white hover:bg-white/5'
                      }`}
                    >
                      <item.icon className="w-5 h-5" />
                      {item.label}
                    </button>
                  ))}
                </div>
              </nav>

              {/* User */}
              <div className="p-3 border-t border-white/5">
                <div className="flex items-center gap-3 p-2 rounded-xl bg-white/5">
                  <div className="w-9 h-9 rounded-full bg-gradient-to-br from-accent-primary/30 to-violet/30 flex items-center justify-center text-xs font-medium text-white">
                    {getInitials(user?.name)}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-sm font-medium text-white truncate">{user?.name || 'User'}</p>
                    <p className="text-xs text-accent-primary truncate">HR Admin</p>
                  </div>
                </div>
                <button
                  onClick={() => { useAuthStore.getState().logout(); window.location.href = '/auth/login'; }}
                  className="w-full mt-2 flex items-center gap-3 px-3 py-2.5 rounded-xl text-sm text-white/50 hover:text-red-400 hover:bg-red-500/10 transition-colors"
                >
                  <LogOut className="w-5 h-5" />
                  Đăng xuất
                </button>
              </div>
            </motion.aside>
          </>
        )}
      </AnimatePresence>

      {/* Main Content */}
      <main className="flex-1 flex flex-col min-w-0">
        {/* Mobile Header */}
        <header className="lg:hidden h-16 flex items-center justify-between px-4 bg-black/40 backdrop-blur-xl border-b border-white/5">
          <button
            onClick={() => setIsMobileSidebarOpen(true)}
            className="p-2 rounded-lg hover:bg-white/10"
          >
            <Menu className="w-6 h-6 text-white/80" />
          </button>
          <Link to="/hr/dashboard" className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
              <LayoutDashboard className="w-4 h-4 text-white" />
            </div>
            <span className="text-base font-semibold text-white">HR Admin</span>
          </Link>
          <button className="p-2 rounded-lg hover:bg-white/10 relative">
            <Bell className="w-6 h-6 text-white/80" />
            <span className="absolute top-1 right-1 w-2 h-2 rounded-full bg-accent-primary" />
          </button>
        </header>

        {/* Page Content */}
        <div className="flex-1 overflow-auto">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
