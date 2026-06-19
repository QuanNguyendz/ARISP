import { motion } from 'framer-motion';
import { Settings, Shield, Bell, Database, Key } from 'lucide-react';
import { useState } from 'react';
import { PageHeader } from '@components/shared';

export default function SuperAdminSettingsPage() {
  const [activeTab, setActiveTab] = useState('general');

  const tabs = [
    { id: 'general', label: 'Cài đặt chung', icon: Settings },
    { id: 'security', label: 'Bảo mật', icon: Shield },
    { id: 'notifications', label: 'Thông báo', icon: Bell },
    { id: 'api', label: 'API Keys', icon: Key },
    { id: 'database', label: 'Database', icon: Database },
  ];

  return (
    <div className="p-6 lg:p-8">
      <PageHeader
        title="Cài đặt hệ thống"
        description="Quản lý cấu hình hệ thống ARISP"
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
                    ? 'bg-accent-primary/20 text-accent-primary'
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
            {activeTab === 'general' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-white">Cài đặt chung</h3>
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-medium text-white/70 mb-2">Tên hệ thống</label>
                    <input
                      type="text"
                      defaultValue="ARISP"
                      className="w-full px-4 py-3 rounded-xl bg-white/[0.03] border border-white/10 text-white placeholder-white/30 focus:outline-none focus:border-accent-primary/50 transition-colors"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-white/70 mb-2">Organization mặc định</label>
                    <input
                      type="text"
                      defaultValue="Default Organization"
                      className="w-full px-4 py-3 rounded-xl bg-white/[0.03] border border-white/10 text-white placeholder-white/30 focus:outline-none focus:border-accent-primary/50 transition-colors"
                    />
                  </div>
                </div>
                <button className="px-6 py-3 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity">
                  Lưu thay đổi
                </button>
              </div>
            )}

            {activeTab === 'security' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-white">Bảo mật</h3>
                <div className="space-y-4">
                  <div className="flex items-center justify-between p-4 rounded-xl bg-white/[0.02] border border-white/5">
                    <div>
                      <p className="text-sm font-medium text-white">Xác thực 2FA</p>
                      <p className="text-xs text-white/40">Bắt buộc xác thực 2 lớp cho Super Admin</p>
                    </div>
                    <button className="relative w-12 h-6 rounded-full bg-accent-primary/30">
                      <span className="absolute left-1 top-1 w-4 h-4 rounded-full bg-accent-primary transition-transform" />
                    </button>
                  </div>
                  <div className="flex items-center justify-between p-4 rounded-xl bg-white/[0.02] border border-white/5">
                    <div>
                      <p className="text-sm font-medium text-white">Session Timeout</p>
                      <p className="text-xs text-white/40">Tự động đăng xuất sau 30 phút không hoạt động</p>
                    </div>
                    <button className="relative w-12 h-6 rounded-full bg-accent-primary">
                      <span className="absolute right-1 top-1 w-4 h-4 rounded-full bg-white transition-transform" />
                    </button>
                  </div>
                </div>
              </div>
            )}

            {activeTab === 'notifications' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-white">Thông báo</h3>
                <div className="space-y-4">
                  <div className="flex items-center justify-between p-4 rounded-xl bg-white/[0.02] border border-white/5">
                    <div>
                      <p className="text-sm font-medium text-white">Email thông báo</p>
                      <p className="text-xs text-white/40">Gửi email khi có user mới đăng ký</p>
                    </div>
                    <button className="relative w-12 h-6 rounded-full bg-accent-primary">
                      <span className="absolute right-1 top-1 w-4 h-4 rounded-full bg-white transition-transform" />
                    </button>
                  </div>
                </div>
              </div>
            )}

            {activeTab === 'api' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-white">API Keys</h3>
                <div className="space-y-4">
                  <div className="p-4 rounded-xl bg-white/[0.02] border border-white/5">
                    <div className="flex items-center justify-between mb-2">
                      <p className="text-sm font-medium text-white">Production API Key</p>
                      <button className="text-xs text-accent-primary hover:text-accent-secondary">Regenerate</button>
                    </div>
                    <p className="text-xs font-mono text-white/50 bg-black/30 p-2 rounded-lg">• • • • • • • • • • • • • • • • • • • • •</p>
                  </div>
                </div>
              </div>
            )}

            {activeTab === 'database' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-white">Database</h3>
                <div className="space-y-4">
                  <div className="p-4 rounded-xl bg-white/[0.02] border border-white/5">
                    <p className="text-sm font-medium text-white mb-2">Database Status</p>
                    <p className="text-xs text-emerald-400">Connected - PostgreSQL 15.2</p>
                  </div>
                  <div className="flex gap-3">
                    <button className="px-4 py-2 rounded-xl bg-white/10 text-white/70 hover:text-white hover:bg-white/20 transition-colors text-sm">
                      Backup Now
                    </button>
                    <button className="px-4 py-2 rounded-xl bg-red-500/20 text-red-400 hover:bg-red-500/30 transition-colors text-sm">
                      Clear Cache
                    </button>
                  </div>
                </div>
              </div>
            )}
          </motion.div>
        </div>
      </div>
    </div>
  );
}
