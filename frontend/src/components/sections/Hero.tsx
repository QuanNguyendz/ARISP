import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ArrowRight, Play } from 'lucide-react';

export default function Hero() {
  const navigate = useNavigate();

  return (
    <section className="relative min-h-screen flex items-center justify-center overflow-hidden bg-bg-primary">
      {/* Video full screen background */}
      <div className="absolute inset-0 z-0">
        <video
          src="/video/a1.mp4"
          autoPlay
          loop
          muted
          playsInline
          className="w-full h-full object-cover"
        />
        {/* Overlay để text nổi bật */}
        <div className="absolute inset-0 bg-bg-primary/50" />
      </div>

      {/* Text ở trên đỉnh video */}
      <motion.div
        initial={{ opacity: 0, y: -20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 1, delay: 0.4, ease: [0.25, 0.1, 0.25, 1] }}
        className="absolute top-[10%] left-0 right-0 mx-auto z-10 text-center w-full max-w-2xl px-5"
      >
        {/* Status badge */}
        <motion.div
          initial={{ opacity: 0, y: -10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.8, delay: 0.2 }}
          className="mb-4"
        >
          <div className="inline-flex items-center gap-2 px-4 py-2 rounded-full bg-white/[0.03] border border-white/[0.08]">
            <span className="relative flex h-2 w-2">
              <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-accent-primary opacity-40" />
              <span className="relative inline-flex rounded-full h-2 w-2 bg-accent-primary" />
            </span>
            <span className="text-xs text-white/50 tracking-wide">
              Enterprise-ready
            </span>
          </div>
        </motion.div>

        {/* Main heading - compact single line */}
        <h1 className="text-xl sm:text-2xl md:text-3xl lg:text-4xl font-semibold tracking-tight leading-tight">
          <span className="bg-gradient-to-r from-white via-accent-secondary/80 to-white bg-clip-text text-transparent">
            Interview with AI no humans required.
          </span>
        </h1>
      </motion.div>

      {/* Buttons ở dưới đáy video */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.8, delay: 0.6 }}
        className="absolute bottom-[10%] left-0 right-0 mx-auto z-10 w-full max-w-md px-5"
      >
        <div className="flex flex-col sm:flex-row items-center justify-center gap-3">
          <motion.button
            onClick={() => navigate('/dang-ky')}
            className="group relative w-full sm:w-auto px-7 py-3.5 rounded-full bg-white text-bg-primary font-medium text-sm transition-all duration-300 hover:bg-white/90 flex items-center justify-center gap-2"
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.98 }}
          >
            Get started
            <ArrowRight className="w-4 h-4 group-hover:translate-x-0.5 transition-transform" />
          </motion.button>

          <motion.button
            className="group w-full sm:w-auto px-7 py-3.5 rounded-full text-white/60 hover:text-white text-sm font-medium transition-colors duration-300 flex items-center justify-center gap-2"
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.98 }}
          >
            <Play className="w-4 h-4" />
            Watch demo
          </motion.button>
        </div>
      </motion.div>

      {/* Subtle stats ở dưới cùng */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ duration: 1, delay: 0.8 }}
        className="absolute bottom-8 left-0 right-0 mx-auto z-10"
      >
        <div className="flex flex-wrap items-center justify-center gap-12 text-xs text-white/30 font-light tracking-wide">
          <span>2M+ Interviews</span>
          <span className="text-white/10">•</span>
          <span>500+ Enterprises</span>
          <span className="text-white/10">•</span>
          <span>94% Accuracy</span>
        </div>
      </motion.div>

      {/* Scroll indicator */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        transition={{ delay: 1.2, duration: 0.8 }}
        className="absolute bottom-2 left-0 right-0 mx-auto z-10"
      >
        <motion.div
          animate={{ y: [0, 6, 0] }}
          transition={{ duration: 2, repeat: Infinity, ease: 'easeInOut' }}
          className="flex flex-col items-center gap-3"
        >
          <span className="text-[10px] text-white/20 tracking-[0.2em] uppercase">Scroll</span>
          <svg width="12" height="20" viewBox="0 0 12 20" fill="none" className="text-white/20">
            <rect x="0.5" y="0.5" width="11" height="19" rx="5.5" stroke="currentColor" strokeWidth="1" />
            <motion.rect
              x="5"
              y="5"
              width="2"
              height="4"
              rx="1"
              fill="currentColor"
              animate={{ y: [5, 9, 5], opacity: [1, 0.3, 1] }}
              transition={{ duration: 2, repeat: Infinity, ease: 'easeInOut' }}
            />
          </svg>
        </motion.div>
      </motion.div>
    </section>
  );
}
