import { useRef } from 'react';
import { motion, useScroll, useTransform } from 'framer-motion';
import { Upload, MessageSquare, ArrowRight } from 'lucide-react';

// Section 1: Upload Job Description
function SectionUpload() {
  const ref = useRef(null);
  const { scrollYProgress } = useScroll({
    target: ref,
    offset: ['start end', 'end start'],
  });

  const y = useTransform(scrollYProgress, [0, 1], [60, -60]);
  const opacity = useTransform(scrollYProgress, [0, 0.2, 0.8, 1], [0, 1, 1, 0]);

  return (
    <section ref={ref} className="relative min-h-screen flex items-center justify-center py-40 overflow-hidden">
      <motion.div style={{ y, opacity }} className="max-w-6xl mx-auto px-6">
        <div className="grid lg:grid-cols-2 gap-20 items-center">
          {/* Text Content */}
          <motion.div
            initial={{ opacity: 0, x: -40 }}
            whileInView={{ opacity: 1, x: 0 }}
            viewport={{ once: true, margin: '-100px' }}
            transition={{ duration: 1, ease: [0.25, 0.1, 0.25, 1] }}
            className="space-y-8"
          >
            <div className="flex items-center gap-3">
              <span className="text-[10px] font-mono text-white/20 tracking-widest">01</span>
              <span className="text-xs text-white/30 tracking-wider uppercase">Create</span>
            </div>

            <h2 className="text-4xl sm:text-5xl md:text-6xl font-light tracking-tight leading-[1.1]">
              Define your
              <br />
              <span className="text-white/40">requirements</span>
            </h2>

            <p className="text-white/40 text-lg max-w-sm leading-relaxed font-light">
              Upload job descriptions or paste content. AI analyzes the role, identifies core competencies, and creates an evaluation framework.
            </p>

            <div className="flex items-center gap-2 pt-4">
              <button
                onClick={() => window.location.href = '/auth/register'}
                className="flex items-center gap-2 text-sm text-white/50 hover:text-white transition-colors"
              >
                Try it free
                <ArrowRight className="w-4 h-4" />
              </button>
            </div>
          </motion.div>

          {/* Visual - Minimal */}
          <motion.div
            initial={{ opacity: 0, x: 40 }}
            whileInView={{ opacity: 1, x: 0 }}
            viewport={{ once: true, margin: '-100px' }}
            transition={{ duration: 1, delay: 0.2 }}
            className="relative"
          >
            <div className="aspect-[4/3] rounded-2xl bg-white/[0.02] border border-white/[0.05] flex items-center justify-center">
              <div className="text-center space-y-4">
                <div className="w-14 h-14 rounded-2xl bg-white/[0.03] border border-white/[0.08] flex items-center justify-center mx-auto">
                  <Upload className="w-6 h-6 text-white/30" />
                </div>
                <p className="text-sm text-white/30">Drop files here</p>
                <p className="text-xs text-white/15">PDF, DOCX, TXT</p>
              </div>
            </div>
          </motion.div>
        </div>
      </motion.div>
    </section>
  );
}

// Section 2: AI Conducts Interview
function SectionInterview() {
  const ref = useRef(null);
  const { scrollYProgress } = useScroll({
    target: ref,
    offset: ['start end', 'end start'],
  });

  const y = useTransform(scrollYProgress, [0, 1], [40, -40]);
  const opacity = useTransform(scrollYProgress, [0, 0.2, 0.8, 1], [0, 1, 1, 0]);

  return (
    <section ref={ref} className="relative min-h-screen flex items-center justify-center py-40 overflow-hidden">
      <motion.div style={{ y, opacity }} className="max-w-6xl mx-auto px-6">
        <div className="grid lg:grid-cols-2 gap-20 items-center">
          {/* Visual - Left side */}
          <motion.div
            initial={{ opacity: 0, x: -40 }}
            whileInView={{ opacity: 1, x: 0 }}
            viewport={{ once: true, margin: '-100px' }}
            transition={{ duration: 1 }}
            className="relative order-2 lg:order-1"
          >
            <div className="aspect-square max-w-sm mx-auto relative">
              {/* Subtle concentric circles */}
              <div className="absolute inset-0 rounded-full border border-white/[0.03]" />
              <div className="absolute inset-8 rounded-full border border-white/[0.02]" />
              <div className="absolute inset-16 rounded-full border border-white/[0.02]" />

              {/* Center */}
              <div className="absolute inset-0 flex items-center justify-center">
                <div className="w-24 h-24 rounded-full bg-white/[0.03] border border-white/[0.05] flex items-center justify-center">
                  <MessageSquare className="w-8 h-8 text-white/20" />
                </div>
              </div>
            </div>
          </motion.div>

          {/* Text Content - Right side */}
          <motion.div
            initial={{ opacity: 0, x: 40 }}
            whileInView={{ opacity: 1, x: 0 }}
            viewport={{ once: true, margin: '-100px' }}
            transition={{ duration: 1, delay: 0.2 }}
            className="space-y-8 order-1 lg:order-2"
          >
            <div className="flex items-center gap-3">
              <span className="text-[10px] font-mono text-white/20 tracking-widest">02</span>
              <span className="text-xs text-white/30 tracking-wider uppercase">Interview</span>
            </div>

            <h2 className="text-4xl sm:text-5xl md:text-6xl font-light tracking-tight leading-[1.1]">
              AI conducts
              <br />
              <span className="text-white/40">the interview</span>
            </h2>

            <p className="text-white/40 text-lg max-w-sm leading-relaxed font-light">
              AI autonomously converses with candidates, adapting questions in real-time. Available 24/7 across all time zones.
            </p>
          </motion.div>
        </div>
      </motion.div>
    </section>
  );
}

// Section 3: Evaluation Generated
function SectionEvaluation() {
  const ref = useRef(null);

  return (
    <section ref={ref} className="relative min-h-screen flex items-center justify-center py-40 overflow-hidden">
      <div className="max-w-4xl mx-auto px-6 text-center">
        <motion.div
          initial={{ opacity: 0, y: 40 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true, margin: '-100px' }}
          transition={{ duration: 1 }}
          className="space-y-12"
        >
          {/* Step indicator */}
          <div className="flex items-center justify-center gap-3">
            <span className="text-[10px] font-mono text-white/20 tracking-widest">03</span>
            <span className="text-xs text-white/30 tracking-wider uppercase">Results</span>
          </div>

          {/* Main heading */}
          <h2 className="text-4xl sm:text-5xl md:text-6xl lg:text-7xl font-light tracking-tight leading-[1.1]">
            Comprehensive
            <br />
            <span className="text-white/40">reports generated</span>
          </h2>

          {/* Description */}
          <p className="text-white/40 text-lg max-w-lg mx-auto leading-relaxed font-light">
            Receive detailed analysis with competency scores, behavioral insights, and AI recommendations. Make data-driven hiring decisions.
          </p>

          {/* Score display - minimal */}
          <div className="flex items-center justify-center gap-8 pt-8">
            <div className="text-center">
              <div className="text-4xl font-light text-white/60 mb-1">94</div>
              <div className="text-xs text-white/20 tracking-wide">Overall</div>
            </div>
            <div className="w-px h-12 bg-white/10" />
            <div className="text-center">
              <div className="text-4xl font-light text-white/60 mb-1">91</div>
              <div className="text-xs text-white/20 tracking-wide">Communication</div>
            </div>
            <div className="w-px h-12 bg-white/10" />
            <div className="text-center">
              <div className="text-4xl font-light text-white/60 mb-1">88</div>
              <div className="text-xs text-white/20 tracking-wide">Problem Solving</div>
            </div>
          </div>
        </motion.div>
      </div>
    </section>
  );
}

export default function ScrollStorytelling() {
  return (
    <div className="relative">
      <SectionUpload />
      <SectionInterview />
      <SectionEvaluation />
    </div>
  );
}
