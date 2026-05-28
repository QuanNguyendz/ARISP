import { motion } from 'framer-motion';

export default function PlaybooksPage() {
  return (
    <div className="p-6 lg:p-8">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-8">
        <h1 className="text-3xl font-semibold text-white mb-2">Playbooks</h1>
        <p className="text-text-secondary">Quản lý kịch bản phỏng vấn</p>
      </motion.div>
      <div className="grid md:grid-cols-3 gap-6">
        {['Technical Interview', 'Behavioral Interview', 'System Design'].map((item, i) => (
          <motion.div key={item} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: i * 0.1 }}>
            <div className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 hover:bg-white/[0.05] transition-colors cursor-pointer">
              <div className="w-12 h-12 rounded-xl bg-accent-primary/20 flex items-center justify-center mb-4">
                <span className="text-2xl">📋</span>
              </div>
              <h3 className="text-lg font-semibold text-white mb-2">{item}</h3>
              <p className="text-sm text-white/40">10 câu hỏi</p>
            </div>
          </motion.div>
        ))}
      </div>
    </div>
  );
}
