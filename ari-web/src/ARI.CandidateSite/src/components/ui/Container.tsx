import type { ReactNode } from 'react';
import { motion } from 'framer-motion';
import type { Variants } from 'framer-motion';

interface ContainerProps {
  children: ReactNode;
  className?: string;
  id?: string;
}

const containerVariants: Variants = {
  hidden: { opacity: 0 },
  visible: {
    opacity: 1,
    transition: {
      staggerChildren: 0.1,
      delayChildren: 0.1,
    },
  },
};

const itemVariants: Variants = {
  hidden: { opacity: 0, y: 20 },
  visible: {
    opacity: 1,
    y: 0,
    transition: {
      duration: 0.6,
      ease: [0, 0, 0.2, 1],
    },
  },
};

function Container({ children, className = '', id }: ContainerProps) {
  return (
    <motion.section
      id={id}
      className={`relative w-full ${className}`}
      initial="hidden"
      whileInView="visible"
      viewport={{ once: true, margin: '-100px' }}
      variants={containerVariants}
    >
      <div className="max-w-7xl mx-auto px-6 sm:px-8 lg:px-12">
        {children}
      </div>
    </motion.section>
  );
}

function ContainerItem({ children, className = '' }: { children: ReactNode; className?: string }) {
  return (
    <motion.div className={className} variants={itemVariants}>
      {children}
    </motion.div>
  );
}

export { Container, ContainerItem };
