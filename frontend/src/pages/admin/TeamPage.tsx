import { motion } from 'framer-motion';

export default function TeamPage() {
  return (
    <div className="p-6 lg:p-8">
      <motion.div initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} className="mb-8">
        <h1 className="text-3xl font-semibold text-white mb-2">Nhóm</h1>
        <p className="text-text-secondary">Quản lý thành viên nhóm</p>
      </motion.div>
      <div className="space-y-4">
        {[
          { name: 'Minh Anh', role: 'Admin', email: 'minhanh@company.com' },
          { name: 'Thu Hà', role: 'Recruiter', email: 'thuha@company.com' },
        ].map((member, i) => (
          <motion.div key={member.name} initial={{ opacity: 0, y: 20 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: i * 0.1 }}>
            <div className="bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] p-6 flex items-center justify-between">
              <div className="flex items-center gap-4">
                <div className="w-12 h-12 rounded-full bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center text-sm font-medium text-white">
                  {member.name.split(' ').map((n: string) => n[0]).join('')}
                </div>
                <div>
                  <h3 className="text-lg font-semibold text-white">{member.name}</h3>
                  <p className="text-sm text-text-secondary">{member.email}</p>
                </div>
              </div>
              <span className="px-3 py-1 rounded-full bg-accent-primary/20 text-accent-primary text-xs font-medium">{member.role}</span>
            </div>
          </motion.div>
        ))}
      </div>
    </div>
  );
}
