import { motion } from 'framer-motion';
import { User, Bell, Shield } from 'lucide-react';

export default function SettingsPage() {
  return (
    <div className="p-6 lg:p-8">
      {/* Header */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="mb-8"
      >
        <h1 className="text-2xl font-semibold text-white mb-1">Cài đặt</h1>
        <p className="text-sm text-white/40">Quản lý tài khoản và cài đặt hệ thống</p>
      </motion.div>

      <div className="max-w-3xl space-y-6">
        {/* Profile */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.1 }}
          className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden"
        >
          <div className="flex items-center gap-3 mb-6">
            <div className="w-10 h-10 rounded-xl bg-accent-primary/20 flex items-center justify-center">
              <User className="w-5 h-5 text-accent-primary" />
            </div>
            <h2 className="text-lg font-semibold text-white">Thông tin cá nhân</h2>
          </div>
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm text-white/60 mb-2">Họ và tên</label>
                <input
                  type="text"
                  defaultValue="Minh Anh"
                  className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white focus:outline-none focus:border-accent-primary/50"
                />
              </div>
              <div>
                <label className="block text-sm text-white/60 mb-2">Email</label>
                <input
                  type="email"
                  defaultValue="minhanh@company.com"
                  className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white focus:outline-none focus:border-accent-primary/50"
                />
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm text-white/60 mb-2">Công ty</label>
                <input
                  type="text"
                  defaultValue="TechVision Corp"
                  className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white focus:outline-none focus:border-accent-primary/50"
                />
              </div>
              <div>
                <label className="block text-sm text-white/60 mb-2">Số điện thoại</label>
                <input
                  type="tel"
                  defaultValue="0912 345 678"
                  className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white focus:outline-none focus:border-accent-primary/50"
                />
              </div>
            </div>
          </div>
        </motion.div>

        {/* Notifications */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.2 }}
          className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden"
        >
          <div className="flex items-center gap-3 mb-6">
            <div className="w-10 h-10 rounded-xl bg-amber-500/20 flex items-center justify-center">
              <Bell className="w-5 h-5 text-amber-400" />
            </div>
            <h2 className="text-lg font-semibold text-white">Thông báo</h2>
          </div>
          <div className="space-y-4">
            {[
              { label: 'Email thông báo ứng viên mới', defaultChecked: true },
              { label: 'Email kết quả phỏng vấn', defaultChecked: true },
              { label: 'Nhắc nhở lịch phỏng vấn', defaultChecked: false },
              { label: 'Báo cáo tuần', defaultChecked: true },
            ].map((item) => (
              <label key={item.label} className="flex items-center justify-between cursor-pointer">
                <span className="text-white">{item.label}</span>
                <input
                  type="checkbox"
                  defaultChecked={item.defaultChecked}
                  className="w-5 h-5 rounded bg-white/10 border-white/20 text-accent-primary focus:ring-accent-primary"
                />
              </label>
            ))}
          </div>
        </motion.div>

        {/* Security */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.3 }}
          className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 overflow-hidden"
        >
          <div className="flex items-center gap-3 mb-6">
            <div className="w-10 h-10 rounded-xl bg-emerald-500/20 flex items-center justify-center">
              <Shield className="w-5 h-5 text-emerald-400" />
            </div>
            <h2 className="text-lg font-semibold text-white">Bảo mật</h2>
          </div>
          <div className="space-y-3">
            <button className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-left text-white hover:bg-white/10 transition-colors">
              Đổi mật khẩu
            </button>
            <button className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-left text-white hover:bg-white/10 transition-colors">
              Xác thực hai yếu tố (2FA)
            </button>
          </div>
        </motion.div>

        <button className="w-full px-6 py-3 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity">
          Lưu thay đổi
        </button>
      </div>
    </div>
  );
}
