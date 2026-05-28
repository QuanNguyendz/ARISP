import { motion } from 'framer-motion';
import { ArrowLeft, FileText, Eye, CheckCircle, XCircle } from 'lucide-react';

export default function CandidateDetailPage() {
  return (
    <div className="p-6 lg:p-8">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
        <a href="/quan-ly/ung-vien" className="inline-flex items-center gap-2 text-sm text-white/50 hover:text-white mb-6 transition-colors">
          <ArrowLeft className="w-4 h-4" />
          Quay lại
        </a>
        
        <div className="flex items-start gap-6 mb-8">
          <div className="w-20 h-20 rounded-full bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-2xl font-bold text-white">
            NVA
          </div>
          <div className="flex-1">
            <h1 className="text-3xl font-semibold text-white mb-2">Nguyễn Văn An</h1>
            <p className="text-xl text-text-secondary mb-4">Senior Backend Developer</p>
            <div className="flex items-center gap-4 text-sm text-text-secondary">
              <span className="px-2 py-0.5 rounded-full bg-blue-500/20 text-blue-400">Đang phỏng vấn</span>
              <span>Vòng 2</span>
            </div>
          </div>
          <div className="text-right">
            <div className="text-4xl font-semibold bg-gradient-to-r from-accent-primary to-violet bg-clip-text text-transparent">87</div>
            <div className="text-xs text-white/40">Overall Score</div>
          </div>
        </div>

        <div className="grid lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2 space-y-6">
            <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6">
              <h2 className="text-xl font-semibold text-white mb-4">CV</h2>
              <div className="flex items-center gap-3 p-4 rounded-xl bg-white/[0.02] border border-white/5">
                <FileText className="w-5 h-5 text-accent-primary" />
                <span className="text-white">an_nguyen_cv.pdf</span>
                <button className="ml-auto px-4 py-2 rounded-lg bg-accent-primary/20 text-accent-primary text-sm hover:bg-accent-primary/30">Tải xuống</button>
              </div>
            </motion.div>
            <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.2 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6">
              <h2 className="text-xl font-semibold text-white mb-4">Lịch sử phỏng vấn</h2>
              <div className="space-y-4">
                {[
                  { type: 'Technical Interview', date: '19/05/2026', score: 92 },
                  { type: 'Coding Assessment', date: '17/05/2026', score: 85 },
                ].map((interview) => (
                  <div key={interview.type} className="flex items-center justify-between p-4 rounded-xl bg-white/[0.02] border border-white/5">
                    <div>
                      <p className="font-medium text-white">{interview.type}</p>
                      <p className="text-sm text-white/40">{interview.date}</p>
                    </div>
                    <span className="text-xl font-semibold text-accent-primary">{interview.score}</span>
                  </div>
                ))}
              </div>
            </motion.div>
          </div>
          <div className="space-y-6">
            <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.3 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6">
              <h2 className="text-xl font-semibold text-white mb-4">Thao tác</h2>
              <div className="space-y-2">
                <button className="w-full flex items-center gap-3 px-4 py-3 rounded-xl bg-white/[0.02] text-white hover:bg-white/5 transition-colors"><Eye className="w-5 h-5" />Xem chi tiết CV</button>
                <button className="w-full flex items-center gap-3 px-4 py-3 rounded-xl bg-emerald-500/20 text-emerald-400 hover:bg-emerald-500/30 transition-colors"><CheckCircle className="w-5 h-5" />Chấp nhận</button>
                <button className="w-full flex items-center gap-3 px-4 py-3 rounded-xl bg-red-500/20 text-red-400 hover:bg-red-500/30 transition-colors"><XCircle className="w-5 h-5" />Từ chối</button>
              </div>
            </motion.div>
            <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.4 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6">
              <h2 className="text-xl font-semibold text-white mb-4">Điểm số</h2>
              <div className="space-y-4">
                {[
                  { label: 'Technical', score: 92 },
                  { label: 'Communication', score: 85 },
                  { label: 'Problem Solving', score: 84 },
                ].map((m) => (
                  <div key={m.label}>
                    <div className="flex items-center justify-between mb-2">
                      <span className="text-sm text-white/60">{m.label}</span>
                      <span className="text-sm font-medium text-white">{m.score}%</span>
                    </div>
                    <div className="h-2 bg-white/10 rounded-full overflow-hidden">
                      <div className="h-full rounded-full bg-gradient-to-r from-accent-primary to-violet" style={{ width: `${m.score}%` }} />
                    </div>
                  </div>
                ))}
              </div>
            </motion.div>
          </div>
        </div>
      </motion.div>
    </div>
  );
}
