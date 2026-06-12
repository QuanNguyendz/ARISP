import { motion } from 'framer-motion';
import { Settings, User, Bell, Lock } from 'lucide-react';
import { useState } from 'react';
import { PageHeader } from '@components/shared';

export default function RecruiterSettingsPage() {
  const [activeTab, setActiveTab] = useState('profile');

  const tabs = [
    { id: 'profile', label: 'Hồ sơ', icon: User },
    { id: 'notifications', label: 'Thông báo', icon: Bell },
    { id: 'security', label: 'Bảo mật', icon: Lock },
  ];

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Cài đặt"
        description="Quản lý hồ sơ và cài đặt cá nhân"
      />

      <div className="flex flex-col lg:flex-row gap-8">
        {/* Tabs Sidebar */}
        <div className="lg:w-64 shrink-0">
          <div className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-2">
            {tabs.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium transition-all ${
                  activeTab === tab.id
                    ? 'bg-amber-500/20 text-amber-400'
                    : 'text-white/50 hover:text-white hover:bg-white/5'
                }`}
              >
                <tab.icon className="w-5 h-5" />
                {tab.label}
              </button>
            ))}
          </div>
        </div>

        {/* Content */}
        <div className="flex-1">
          <motion.div
            key={activeTab}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6"
          >
            {activeTab === 'profile' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-white">Hồ sơ</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-white/70 mb-2">Họ tên</label>
                    <input
                      type="text"
                      defaultValue="Recruiter"
                      className="w-full px-4 py-3 rounded-xl bg-white/[0.03] border border-white/10 text-white placeholder-white/30 focus:outline-none focus:border-amber-500/50 transition-colors"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-white/70 mb-2">Email</label>
                    <input
                      type="email"
                      defaultValue="recruiter@arisp.com"
                      className="w-full px-4 py-3 rounded-xl bg-white/[0.03] border border-white/10 text-white placeholder-white/30 focus:outline-none focus:border-amber-500/50 transition-colors"
                    />
                  </div>
                </div>
                <button className="px-6 py-3 rounded-xl bg-gradient-to-r from-amber-500 to-orange text-white font-medium hover:opacity-90 transition-opacity">
                  Lưu thay đổi
                </button>
              </div>
            )}

            {activeTab === 'notifications' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-white">Thông báo</h3>
                <div className="space-y-4">
                  <div className="flex items-center justify-between p-4 rounded-xl bg-white/[0.02] border border-white/5">
                    <div>
                      <p className="text-sm font-medium text-white">Email thông báo</p>
                      <p className="text-xs text-white/40">Nhận email khi có ứng viên mới ứng tuyển</p>
                    </div>
                    <button className="relative w-12 h-6 rounded-full bg-amber-500">
                      <span className="absolute right-1 top-1 w-4 h-4 rounded-full bg-white transition-transform" />
                    </button>
                  </div>
                </div>
              </div>
            )}

            {activeTab === 'security' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-white">Bảo mật</h3>
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-medium text-white/70 mb-2">Mật khẩu hiện tại</label>
                    <input
                      type="password"
                      placeholder="••••••••"
                      className="w-full px-4 py-3 rounded-xl bg-white/[0.03] border border-white/10 text-white placeholder-white/30 focus:outline-none focus:border-amber-500/50 transition-colors"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-white/70 mb-2">Mật khẩu mới</label>
                    <input
                      type="password"
                      placeholder="••••••••"
                      className="w-full px-4 py-3 rounded-xl bg-white/[0.03] border border-white/10 text-white placeholder-white/30 focus:outline-none focus:border-amber-500/50 transition-colors"
                    />
                  </div>
                </div>
                <button className="px-6 py-3 rounded-xl bg-gradient-to-r from-amber-500 to-orange text-white font-medium hover:opacity-90 transition-opacity">
                  Đổi mật khẩu
                </button>
              </div>
            )}
          </motion.div>
        </div>
      </div>
    </div>
  );
}
