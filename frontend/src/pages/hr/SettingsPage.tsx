import { motion } from 'framer-motion'
import { Settings, User, Bell, Lock } from 'lucide-react'
import { useState } from 'react'
import { PageHeader } from '@components/shared'

export default function HrSettingsPage() {
  const [activeTab, setActiveTab] = useState('profile')

  const tabs = [
    { id: 'profile', label: 'Hồ sơ', icon: User },
    { id: 'notifications', label: 'Thông báo', icon: Bell },
    { id: 'security', label: 'Bảo mật', icon: Lock },
  ]

  return (
    <div className="p-6 lg:p-8 bg-ink-50 dark:bg-ink-950 min-h-screen">
      <PageHeader title="Cài đặt" description="Quản lý hồ sơ và cài đặt cá nhân" />

      <div className="flex flex-col lg:flex-row gap-8">
        {/* Tabs Sidebar */}
        <div className="lg:w-64 shrink-0">
          <div className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-2 shadow-card">
            {tabs.map((tab) => (
              <button
                key={tab.id}
                onClick={() => setActiveTab(tab.id)}
                className={`w-full flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-medium transition-all ${
                  activeTab === tab.id
                    ? 'bg-brand-100 dark:bg-brand-500/20 text-brand-700 dark:text-brand-400'
                    : 'text-ink-600 dark:text-ink-400 hover:bg-ink-50 dark:hover:bg-white/5'
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
            className="rounded-2xl border border-ink-200 dark:border-white/10 bg-white dark:bg-white/5 p-6 shadow-card"
          >
            {activeTab === 'profile' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-ink-900 dark:text-white">Hồ sơ</h3>
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      Họ tên
                    </label>
                    <input
                      type="text"
                      defaultValue="HR Admin"
                      className="w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      Email
                    </label>
                    <input
                      type="email"
                      defaultValue="hr@arisp.com"
                      className="w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
                    />
                  </div>
                </div>
                <button className="px-6 py-3 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 text-white font-medium hover:opacity-90 transition-opacity">
                  Lưu thay đổi
                </button>
              </div>
            )}

            {activeTab === 'notifications' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-ink-900 dark:text-white">Thông báo</h3>
                <div className="space-y-4">
                  <div className="flex items-center justify-between p-4 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5">
                    <div>
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        Email thông báo
                      </p>
                      <p className="text-xs text-ink-500 dark:text-ink-400">
                        Nhận email khi có ứng viên mới
                      </p>
                    </div>
                    <button className="relative w-12 h-6 rounded-full bg-brand-600">
                      <span className="absolute right-1 top-1 w-4 h-4 rounded-full bg-white transition-transform" />
                    </button>
                  </div>
                  <div className="flex items-center justify-between p-4 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50/50 dark:bg-white/5">
                    <div>
                      <p className="text-sm font-medium text-ink-900 dark:text-white">
                        Thông báo trình duyệt
                      </p>
                      <p className="text-xs text-ink-500 dark:text-ink-400">
                        Nhận thông báo khi có cập nhật mới
                      </p>
                    </div>
                    <button className="relative w-12 h-6 rounded-full bg-brand-600">
                      <span className="absolute right-1 top-1 w-4 h-4 rounded-full bg-white transition-transform" />
                    </button>
                  </div>
                </div>
              </div>
            )}

            {activeTab === 'security' && (
              <div className="space-y-6">
                <h3 className="text-lg font-semibold text-ink-900 dark:text-white">Bảo mật</h3>
                <div className="space-y-4">
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      Mật khẩu hiện tại
                    </label>
                    <input
                      type="password"
                      placeholder="••••••••"
                      className="w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
                    />
                  </div>
                  <div>
                    <label className="block text-sm font-medium text-ink-600 dark:text-ink-400 mb-2">
                      Mật khẩu mới
                    </label>
                    <input
                      type="password"
                      placeholder="••••••••"
                      className="w-full px-4 py-3 rounded-xl border border-ink-200 dark:border-white/10 bg-ink-50 dark:bg-white/5 text-ink-900 dark:text-white placeholder-ink-400 focus:outline-none focus:border-brand-400 transition-colors"
                    />
                  </div>
                </div>
                <button className="px-6 py-3 rounded-xl bg-gradient-to-r from-brand-600 to-ai-600 text-white font-medium hover:opacity-90 transition-opacity">
                  Đổi mật khẩu
                </button>
              </div>
            )}
          </motion.div>
        </div>
      </div>
    </div>
  )
}
