import { motion } from 'framer-motion';

export default function CreateJobPostingPage() {
  return (
    <div className="p-6 lg:p-8">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-8">
        <h1 className="text-3xl font-semibold text-white mb-2">Tạo tin tuyển dụng</h1>
        <p className="text-text-secondary">Tạo tin tuyển dụng mới với AI</p>
      </motion.div>

      <div className="max-w-3xl">
        <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.1 }} className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6">
          <div className="space-y-6">
            <div>
              <label className="block text-sm font-medium text-white mb-2">Tiêu đề công việc</label>
              <input type="text" placeholder="VD: Senior Frontend Developer" className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50" />
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div>
                <label className="block text-sm font-medium text-white mb-2">Địa điểm</label>
                <input type="text" placeholder="VD: TP. Hồ Chí Minh" className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50" />
              </div>
              <div>
                <label className="block text-sm font-medium text-white mb-2">Mức lương</label>
                <input type="text" placeholder="VD: $2,000 - $4,000" className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50" />
              </div>
            </div>
            <div>
              <label className="block text-sm font-medium text-white mb-2">Loại hình công việc</label>
              <select className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white focus:outline-none focus:border-accent-primary/50">
                <option>Full-time</option>
                <option>Part-time</option>
                <option>Contract</option>
                <option>Internship</option>
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-white mb-2">Mô tả công việc</label>
              <textarea rows={4} placeholder="Mô tả chi tiết về công việc..." className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 resize-none" />
            </div>
            <div>
              <label className="block text-sm font-medium text-white mb-2">Yêu cầu</label>
              <textarea rows={4} placeholder="Các yêu cầu về kỹ năng, kinh nghiệm..." className="w-full px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white placeholder:text-text-tertiary focus:outline-none focus:border-accent-primary/50 resize-none" />
            </div>
            <div className="flex gap-4 pt-4">
              <a href="/quan-ly/tin-tuyen-dung" className="flex-1 px-6 py-3 rounded-xl bg-white/5 border border-white/10 text-white font-medium text-center hover:bg-white/10 transition-colors">Hủy</a>
              <button className="flex-1 px-6 py-3 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity">Tạo tin tuyển dụng</button>
            </div>
          </div>
        </motion.div>
      </div>
    </div>
  );
}
