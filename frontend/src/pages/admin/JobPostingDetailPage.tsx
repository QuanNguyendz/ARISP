import { motion } from 'framer-motion';
import { MapPin, DollarSign, Clock, Calendar, ArrowLeft, Edit2 } from 'lucide-react';

export default function JobPostingDetailPage() {
  return (
    <div className="p-6 lg:p-8">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }}>
        <a href="/quan-ly/tin-tuyen-dung" className="inline-flex items-center gap-2 text-sm text-white/50 hover:text-white mb-6 transition-colors">
          <ArrowLeft className="w-4 h-4" />
          Quay lại
        </a>
        
        <div className="flex items-start gap-6 mb-8">
          <div className="w-20 h-20 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-2xl font-bold text-white">
            FE
          </div>
          <div className="flex-1">
            <h1 className="text-3xl font-semibold text-white mb-2">Senior Frontend Developer</h1>
            <p className="text-xl text-text-secondary mb-4">TechVision Corp</p>
            <div className="flex flex-wrap items-center gap-4 text-sm text-text-secondary">
              <span className="flex items-center gap-1"><MapPin className="w-4 h-4" />TP. Hồ Chí Minh</span>
              <span className="flex items-center gap-1"><DollarSign className="w-4 h-4" />$3,000 - $5,000</span>
              <span className="flex items-center gap-1"><Clock className="w-4 h-4" />Full-time</span>
              <span className="flex items-center gap-1"><Calendar className="w-4 h-4" />15/05/2026</span>
            </div>
          </div>
          <button className="flex items-center gap-2 px-6 py-3 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity">
            <Edit2 className="w-4 h-4" />
            Chỉnh sửa
          </button>
        </div>

        <div className="grid lg:grid-cols-3 gap-6">
          <div className="lg:col-span-2 space-y-6">
            <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6">
              <h2 className="text-xl font-semibold text-white mb-4">Mô tả công việc</h2>
              <p className="text-text-secondary leading-relaxed">Chúng tôi đang tìm kiếm một Senior Frontend Developer có kinh nghiệm để tham gia đội ngũ phát triển sản phẩm.</p>
            </motion.div>
            <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.2 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6">
              <h2 className="text-xl font-semibold text-white mb-4">Yêu cầu</h2>
              <ul className="space-y-2 text-text-secondary">
                <li className="flex items-start gap-2"><span className="w-1.5 h-1.5 rounded-full bg-accent-primary mt-2" />5+ năm kinh nghiệm với React</li>
                <li className="flex items-start gap-2"><span className="w-1.5 h-1.5 rounded-full bg-accent-primary mt-2" />TypeScript master</li>
                <li className="flex items-start gap-2"><span className="w-1.5 h-1.5 rounded-full bg-accent-primary mt-2" />Kinh nghiệm với state management</li>
              </ul>
            </motion.div>
          </div>
          <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.3 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6">
            <h2 className="text-xl font-semibold text-white mb-4">Ứng viên (24)</h2>
            <div className="space-y-3">
              {[
                { name: 'Nguyễn Văn An', score: 87 },
                { name: 'Trần Thị Minh', score: 91 },
                { name: 'Lê Hoàng Nam', score: 79 },
              ].map((c) => (
                <a key={c.name} href={`/quan-ly/ung-vien/${c.name}`} className="flex items-center justify-between p-3 rounded-xl bg-white/[0.02] border border-white/5 hover:border-white/10 transition-colors">
                  <span className="text-sm text-white">{c.name}</span>
                  <span className="text-sm font-medium text-accent-primary">{c.score}</span>
                </a>
              ))}
            </div>
          </motion.div>
        </div>
      </motion.div>
    </div>
  );
}
