import { motion } from 'framer-motion';

const sessions = [
  { id: '1', candidate: 'Nguyễn Văn An', position: 'Senior Frontend', date: '19/05/2026', status: 'completed' },
  { id: '2', candidate: 'Trần Thị Minh', position: 'Product Designer', date: '19/05/2026', status: 'completed' },
  { id: '3', candidate: 'Lê Hoàng Nam', position: 'Data Scientist', date: '18/05/2026', status: 'pending' },
];

export default function InterviewSessionsPage() {
  return (
    <div className="p-6 lg:p-8">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-8">
        <h1 className="text-3xl font-semibold text-white mb-2">Phiên phỏng vấn</h1>
        <p className="text-text-secondary">Quản lý các phiên phỏng vấn</p>
      </motion.div>
      <div className="space-y-4">
        {sessions.map((session, index) => (
          <motion.div key={session.id} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: index * 0.05 }}>
            <div className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 hover:bg-white/[0.05] transition-colors">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-4">
                  <div className="w-12 h-12 rounded-full bg-gradient-to-br from-accent-primary/30 to-violet/30 flex items-center justify-center text-sm font-medium text-white">
                    {session.candidate.split(' ').map((n: string) => n[0]).join('')}
                  </div>
                  <div>
                    <h3 className="text-lg font-semibold text-white">{session.candidate}</h3>
                    <p className="text-sm text-text-secondary">{session.position}</p>
                  </div>
                </div>
                <div className="flex items-center gap-4">
                  <span className="text-sm text-white/40">{session.date}</span>
                  <span className={`px-3 py-1 rounded-full text-xs font-medium ${session.status === 'completed' ? 'bg-emerald-500/20 text-emerald-400' : 'bg-amber-500/20 text-amber-400'}`}>
                    {session.status === 'completed' ? 'Hoàn thành' : 'Đang chờ'}
                  </span>
                  <a href="/quan-ly/danh-gia" className="px-4 py-2 rounded-lg bg-accent-primary/20 text-accent-primary text-sm hover:bg-accent-primary/30">Xem</a>
                </div>
              </div>
            </div>
          </motion.div>
        ))}
      </div>
    </div>
  );
}
