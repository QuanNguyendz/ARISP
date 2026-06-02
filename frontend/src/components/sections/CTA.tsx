import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Container, ContainerItem } from '../ui/Container';
import { ArrowRight } from 'lucide-react';

export default function CTA() {
  const navigate = useNavigate();

  return (
    <Container id="pricing" className="py-40 relative">
      <ContainerItem>
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          whileInView={{ opacity: 1, y: 0 }}
          viewport={{ once: true }}
          transition={{ duration: 0.8 }}
          className="relative max-w-3xl mx-auto text-center"
        >
          <motion.span
            initial={{ opacity: 0 }}
            whileInView={{ opacity: 1 }}
            viewport={{ once: true }}
            className="text-xs text-white/30 tracking-[0.2em] uppercase mb-8 block"
          >
            Get Started
          </motion.span>

          <h2 className="text-4xl sm:text-5xl md:text-6xl lg:text-7xl font-light tracking-tight mb-8 leading-tight">
            Ready to transform
            <br />
            <span className="text-white/60">your hiring?</span>
          </h2>

          <p className="text-white/40 text-lg max-w-md mx-auto mb-12 font-light leading-relaxed">
            Join hundreds of enterprises using ARISP to find better talent, faster.
          </p>

          <div className="flex flex-col sm:flex-row items-center justify-center gap-4">
            <motion.button
              onClick={() => navigate('/dang-ky')}
              className="group relative px-8 py-4 rounded-full bg-white text-bg-primary font-medium text-sm transition-all duration-300 hover:bg-white/90 flex items-center gap-2"
              whileHover={{ scale: 1.02 }}
              whileTap={{ scale: 0.98 }}
            >
              Start free trial
              <ArrowRight className="w-4 h-4" />
            </motion.button>
            <motion.button
              onClick={() => navigate('/dang-nhap')}
              className="px-8 py-4 rounded-full text-white/50 hover:text-white text-sm font-medium transition-colors duration-300"
              whileHover={{ scale: 1.02 }}
              whileTap={{ scale: 0.98 }}
            >
              Schedule demo
            </motion.button>
          </div>

          <p className="text-xs text-white/20 mt-12 font-light tracking-wide">
            No credit card required • 14-day free trial • Cancel anytime
          </p>
        </motion.div>
      </ContainerItem>
    </Container>
  );
}
