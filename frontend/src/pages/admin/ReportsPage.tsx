import { motion } from 'framer-motion';
import { Download, TrendingUp, Users, Clock, FileText } from 'lucide-react';

const reports = [
  { id: '1', title: 'Báo cáo tuyển dụng tháng 5', description: 'Tổng hợp hoạt động tuyển dụng tháng 5/2026', date: '27/05/2026' },
  { id: '2', title: 'Phân tích hiệu suất NTD', description: 'So sánh hiệu suất giữa các nhà tuyển dụng', date: '25/05/2026' },
  { id: '3', title: 'Báo cáo ứng viên', description: 'Thống kê ứng viên theo vị trí', date: '20/05/2026' },
];

export default function ReportsPage() {
  return (
    <div className="p-6 lg:p-8">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-8">
        <h1 className="text-3xl font-semibold text-white mb-2">Báo cáo</h1>
        <p className="text-text-secondary">Xem và tải các báo cáo tuyển dụng</p>
      </motion.div>

      <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8">
        {[
          { label: 'Tổng ứng viên', value: '1,234', icon: Users, color: 'text-blue-400' },
          { label: 'Đã tuyển', value: '89', icon: TrendingUp, color: 'text-emerald-400' },
          { label: 'Tgian TB tuyển', value: '4.2 ngày', icon: Clock, color: 'text-amber-400' },
          { label: 'Tỷ lệ Pass', value: '68%', icon: FileText, color: 'text-violet-400' },
        ].map((stat) => (
          <motion.div key={stat.label} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-4">
            <stat.icon className={`w-5 h-5 ${stat.color} mb-2`} />
            <p className={`text-2xl font-semibold ${stat.color}`}>{stat.value}</p>
            <p className="text-sm text-white/40">{stat.label}</p>
          </motion.div>
        ))}
      </div>

      <div className="space-y-4">
        <h2 className="text-lg font-semibold text-white mb-4">Báo cáo gần đây</h2>
        {reports.map((report, index) => (
          <motion.div key={report.id} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: index * 0.05 }}>
            <div className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 flex items-center justify-between hover:bg-white/[0.05] transition-colors">
              <div className="flex items-center gap-4">
                <div className="w-12 h-12 rounded-xl bg-accent-primary/20 flex items-center justify-center">
                  <FileText className="w-6 h-6 text-accent-primary" />
                </div>
                <div>
                  <h3 className="text-lg font-semibold text-white">{report.title}</h3>
                  <p className="text-sm text-text-secondary">{report.description}</p>
                  <p className="text-xs text-white/40 mt-1">{report.date}</p>
                </div>
              </div>
              <button className="flex items-center gap-2 px-4 py-2 rounded-lg bg-white/5 text-white hover:bg-white/10 transition-colors">
                <Download className="w-4 h-4" />
                Tải xuống
              </button>
            </div>
          </motion.div>
        ))}
      </div>
    </div>
  );
}
