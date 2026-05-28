import { useRef, useEffect, useState } from 'react';
import { motion, useInView } from 'framer-motion';
import { Container, ContainerItem } from '../ui/Container';

interface StatItemProps {
  value: number;
  suffix: string;
  label: string;
  index: number;
}

function StatItem({ value, suffix, label, index }: StatItemProps) {
  const ref = useRef(null);
  const isInView = useInView(ref, { once: true, margin: '-50px' });
  const [count, setCount] = useState(0);

  useEffect(() => {
    if (isInView) {
      const duration = 2000;
      const steps = 60;
      const increment = value / steps;
      let current = 0;

      const timer = setInterval(() => {
        current += increment;
        if (current >= value) {
          setCount(value);
          clearInterval(timer);
        } else {
          setCount(Math.floor(current));
        }
      }, duration / steps);

      return () => clearInterval(timer);
    }
  }, [isInView, value]);

  return (
    <ContainerItem>
      <motion.div
        ref={ref}
        className="text-center"
        initial={{ opacity: 0, y: 20 }}
        whileInView={{ opacity: 1, y: 0 }}
        viewport={{ once: true }}
        transition={{ duration: 0.6, delay: index * 0.1 }}
      >
        <div className="text-5xl sm:text-6xl md:text-7xl font-light tracking-tight mb-4">
          <span className="text-white">{count}</span>
          <span className="text-white/40">{suffix}</span>
        </div>
        <p className="text-sm text-white/40 font-light tracking-wide">{label}</p>
      </motion.div>
    </ContainerItem>
  );
}

export default function Stats() {
  const stats = [
    { value: 2, suffix: 'M+', label: 'Interviews Completed' },
    { value: 94, suffix: '%', label: 'Accuracy Rate' },
    { value: 60, suffix: '%', label: 'Time Saved' },
    { value: 500, suffix: '+', label: 'Enterprise Clients' },
  ];

  return (
    <Container className="py-40 relative">
      <ContainerItem className="text-center mb-20">
        <motion.span
          initial={{ opacity: 0 }}
          whileInView={{ opacity: 1 }}
          viewport={{ once: true }}
          className="text-xs text-white/30 tracking-[0.2em] uppercase mb-6 block"
        >
          Impact
        </motion.span>
        <motion.h2
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.8 }}
          className="text-4xl sm:text-5xl md:text-6xl font-light tracking-tight"
        >
          Real Results
        </motion.h2>
      </ContainerItem>

      <div className="grid sm:grid-cols-2 lg:grid-cols-4 gap-12 lg:gap-16">
        {stats.map((stat, idx) => (
          <StatItem
            key={stat.label}
            value={stat.value}
            suffix={stat.suffix}
            label={stat.label}
            index={idx}
          />
        ))}
      </div>

      <ContainerItem className="mt-32">
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.8 }}
          className="text-center max-w-2xl mx-auto"
        >
          <blockquote className="text-xl text-white/50 leading-relaxed font-light mb-10">
            "ARISP reduced our hiring time by 45% and helped us find exceptional candidates we would have missed with traditional interviews."
          </blockquote>
          <div className="flex items-center justify-center gap-4">
            <div className="w-10 h-10 rounded-full bg-white/5 flex items-center justify-center">
              <span className="text-sm font-medium text-white/60">NM</span>
            </div>
            <div className="text-left">
              <div className="text-sm font-medium text-white/60">Nguyen Thi Minh</div>
              <div className="text-xs text-white/30">HR Director, TechVietnam</div>
            </div>
          </div>
        </motion.div>
      </ContainerItem>
    </Container>
  );
}
