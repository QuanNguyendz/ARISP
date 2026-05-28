import { useRef, type ReactNode } from 'react';
import { motion, useMotionValue, useSpring, useTransform } from 'framer-motion';

interface GlassCardProps {
  children: ReactNode;
  className?: string;
  hoverEnabled?: boolean;
  onClick?: () => void;
}

export function GlassCard({ children, className = '', hoverEnabled = true, onClick }: GlassCardProps) {
  const cardRef = useRef<HTMLDivElement>(null);
  const x = useMotionValue(0);
  const y = useMotionValue(0);

  const mouseXSpring = useSpring(x, { stiffness: 300, damping: 30 });
  const mouseYSpring = useSpring(y, { stiffness: 300, damping: 30 });

  const rotateX = useTransform(mouseYSpring, [-0.5, 0.5], ['7.5deg', '-7.5deg']);
  const rotateY = useTransform(mouseXSpring, [-0.5, 0.5], ['-7.5deg', '7.5deg']);

  const handleMouseMove = (e: React.MouseEvent<HTMLDivElement>) => {
    if (!cardRef.current || !hoverEnabled) return;
    
    const rect = cardRef.current.getBoundingClientRect();
    const width = rect.width;
    const height = rect.height;

    const mouseX = e.clientX - rect.left;
    const mouseY = e.clientY - rect.top;

    const xPct = mouseX / width - 0.5;
    const yPct = mouseY / height - 0.5;

    x.set(xPct);
    y.set(yPct);
  };

  const handleMouseLeave = () => {
    x.set(0);
    y.set(0);
  };

  return (
    <motion.div
      ref={cardRef}
      className={`relative group ${className}`}
      style={{
        rotateX: hoverEnabled ? rotateX : 0,
        rotateY: hoverEnabled ? rotateY : 0,
        transformStyle: 'preserve-3d',
      }}
      onMouseMove={handleMouseMove}
      onMouseLeave={handleMouseLeave}
      onClick={onClick}
      whileHover={{ scale: hoverEnabled ? 1.02 : 1 }}
      transition={{ duration: 0.3, ease: [0.25, 0.1, 0.25, 1] }}
    >
      <div
        className="relative bg-white/[0.03] backdrop-blur-xl rounded-2xl border border-white/[0.08] overflow-hidden transition-all duration-500 group-hover:bg-white/[0.05] group-hover:border-white/[0.12]"
        style={{ transform: 'translateZ(50px)' }}
      >
        <div className="absolute inset-x-0 top-0 h-px bg-gradient-to-r from-transparent via-white/10 to-transparent" />
        <div className="relative z-10">
          {children}
        </div>
        <div className="absolute inset-x-0 bottom-0 h-px bg-gradient-to-r from-transparent via-white/5 to-transparent" />
      </div>
      <div
        className="absolute -inset-px rounded-2xl opacity-0 group-hover:opacity-100 transition-opacity duration-500 pointer-events-none"
        style={{
          background: 'radial-gradient(ellipse at center, rgba(99, 102, 241, 0.15) 0%, transparent 70%)',
          filter: 'blur(20px)',
        }}
      />
    </motion.div>
  );
}

interface FeatureCardProps {
  icon: ReactNode;
  title: string;
  description: string;
  badge?: string;
}

export function FeatureCard({ icon, title, description, badge }: FeatureCardProps) {
  return (
    <GlassCard className="p-6 sm:p-8">
      <div className="space-y-6">
        <div className="w-12 h-12 rounded-xl bg-gradient-to-br from-accent-primary/20 to-violet/20 flex items-center justify-center text-accent-primary border border-accent-primary/20">
          {icon}
        </div>
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h3 className="text-lg font-semibold text-white">{title}</h3>
            {badge && (
              <span className="px-2.5 py-1 rounded-full text-xs font-medium bg-accent-primary/20 text-accent-primary border border-accent-primary/30">
                {badge}
              </span>
            )}
          </div>
          <p className="text-sm text-white/60 leading-relaxed">{description}</p>
        </div>
      </div>
    </GlassCard>
  );
}

export default GlassCard;
