import { motion } from 'framer-motion';
import AISphereDemo from '../three/AISphereDemo';
import GlassCard from '../ui/GlassCard';
import { Play } from 'lucide-react';

const features = [
  {
    title: 'AI-Powered Analysis',
    description: 'Advanced natural language processing for accurate candidate evaluation',
    icon: '🧠',
  },
  {
    title: 'Real-time Transcription',
    description: 'Live speech-to-text with 98% accuracy in multiple languages',
    icon: '🎤',
  },
  {
    title: 'Cheat Detection',
    description: 'Advanced monitoring for maintaining interview integrity',
    icon: '🛡️',
  },
];

const steps = [
  {
    number: '01',
    title: 'Upload Requirements',
    description: 'Define job criteria and preferred competencies',
    visual: '📋',
  },
  {
    number: '02',
    title: 'AI Conducts Interview',
    description: 'Candidates interact with our intelligent AI interviewer',
    visual: '💬',
  },
  {
    number: '03',
    title: 'Receive Insights',
    description: 'Get detailed reports and competency scores instantly',
    visual: '📊',
  },
];

export default function Demo() {
  return (
    <section className="py-32 relative overflow-hidden">
      {/* Background */}
      <div className="absolute inset-0">
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[800px] h-[800px] bg-accent-primary/10 rounded-full blur-[150px]" />
      </div>

      <div className="relative max-w-7xl mx-auto px-6">
        {/* Header */}
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          className="text-center mb-20"
        >
          <span className="text-xs text-white/30 tracking-[0.2em] uppercase mb-4 block">
            See it in action
          </span>
          <h2 className="text-4xl sm:text-5xl md:text-6xl font-light tracking-tight mb-6">
            Experience the
            <br />
            <span className="bg-gradient-to-r from-accent-primary to-violet bg-clip-text text-transparent">
              Future of Hiring
            </span>
          </h2>
          <p className="text-white/40 text-lg max-w-2xl mx-auto">
            Watch how our AI transforms the interview process in real-time.
          </p>
        </motion.div>

        {/* Main Demo */}
        <motion.div
          initial={{ opacity: 0, y: 40 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ delay: 0.2 }}
          className="mb-32"
        >
          <GlassCard className="p-8 md:p-12" hoverEnabled={false}>
            <div className="grid lg:grid-cols-2 gap-12 items-center">
              {/* AI Sphere */}
              <div className="relative h-[400px]">
                <AISphereDemo className="absolute inset-0" />
              </div>

              {/* Content */}
              <div className="space-y-8">
                <div>
                  <span className="text-[10px] font-mono text-white/20 tracking-widest">LIVE DEMO</span>
                  <h3 className="text-2xl sm:text-3xl font-semibold text-white mt-4 mb-4">
                    AI Interview in Action
                  </h3>
                  <p className="text-white/50 leading-relaxed">
                    Our AI conducts natural conversations, adapts questions in real-time,
                    and provides instant analysis of candidate responses.
                  </p>
                </div>

                {/* Stats */}
                <div className="grid grid-cols-3 gap-4">
                  {[
                    { value: '< 2min', label: 'Setup Time' },
                    { value: '24/7', label: 'Availability' },
                    { value: '94%', label: 'Accuracy' },
                  ].map((stat) => (
                    <div key={stat.label} className="text-center p-4 rounded-xl bg-white/[0.02]">
                      <div className="text-2xl font-semibold bg-gradient-to-r from-accent-primary to-violet bg-clip-text text-transparent">
                        {stat.value}
                      </div>
                      <div className="text-xs text-white/40 mt-1">{stat.label}</div>
                    </div>
                  ))}
                </div>

                {/* CTA */}
                <button className="w-full flex items-center justify-center gap-3 px-8 py-4 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity">
                  <Play className="w-5 h-5" />
                  Watch Full Demo
                </button>
              </div>
            </div>
          </GlassCard>
        </motion.div>

        {/* Features Grid */}
        <div className="grid md:grid-cols-3 gap-6 mb-32">
          {features.map((feature, index) => (
            <motion.div
              key={feature.title}
              initial={{ opacity: 0, y: 20 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ delay: index * 0.1 }}
            >
              <GlassCard className="p-6 text-center" hoverEnabled={false}>
                <div className="text-4xl mb-4">{feature.icon}</div>
                <h4 className="text-lg font-semibold text-white mb-2">{feature.title}</h4>
                <p className="text-sm text-white/40">{feature.description}</p>
              </GlassCard>
            </motion.div>
          ))}
        </div>

        {/* Steps */}
        <div className="grid md:grid-cols-3 gap-8">
          {steps.map((step, index) => (
            <motion.div
              key={step.number}
              initial={{ opacity: 0, y: 20 }}
              whileInView={{ opacity: 1, y: 0 }}
              viewport={{ once: true }}
              transition={{ delay: index * 0.1 }}
              className="relative"
            >
              <div className="text-6xl font-light text-white/5 mb-4">{step.visual}</div>
              <div className="text-[10px] font-mono text-accent-primary tracking-widest mb-2">
                {step.number}
              </div>
              <h4 className="text-xl font-semibold text-white mb-2">{step.title}</h4>
              <p className="text-sm text-white/40">{step.description}</p>
              
              {index < steps.length - 1 && (
                <div className="hidden md:block absolute top-8 -right-4 w-8 border-t border-white/10" />
              )}
            </motion.div>
          ))}
        </div>
      </div>
    </section>
  );
}
