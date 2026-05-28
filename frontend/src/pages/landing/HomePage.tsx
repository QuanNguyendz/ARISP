import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import Navigation from '@components/layout/Navigation';
import Footer from '@components/layout/Footer';
import ThreeBackground from '@components/three/ThreeBackground';
import Hero from '@components/sections/Hero';
import ScrollStorytelling from '@components/sections/ScrollStorytelling';
import Stats from '@components/sections/Stats';
import CTA from '@components/sections/CTA';
import { ArrowRight } from 'lucide-react';

function InterviewKioskSection() {
  const navigate = useNavigate();
  const [code, setCode] = useState('');

  return (
    <section id="interview" className="relative py-32">
      <div className="max-w-6xl mx-auto px-6">
        <div className="max-w-md mx-auto text-center">
          <h2 className="text-3xl sm:text-4xl font-light tracking-tight mb-4">
            Already have an interview code?
          </h2>
          <p className="text-white/40 mb-8 font-light">
            Enter your code to start the interview immediately
          </p>

          <div className="flex gap-3">
            <input
              type="text"
              value={code}
              onChange={(e) => setCode(e.target.value.toUpperCase())}
              placeholder="Enter code"
              className="flex-1 px-5 py-3.5 rounded-xl bg-white/5 border border-white/10 text-white text-center placeholder:text-white/30 focus:outline-none focus:border-white/20 transition-colors"
              maxLength={10}
            />
            <button
              onClick={() => {
                if (code.length > 0) {
                  navigate('/interview');
                }
              }}
              className="px-6 py-3.5 rounded-xl bg-white text-bg-primary font-medium text-sm hover:bg-white/90 transition-colors flex items-center gap-2"
            >
              Start
              <ArrowRight className="w-4 h-4" />
            </button>
          </div>

          <p className="text-xs text-white/20 mt-4">
            Contact HR if you don't have a code
          </p>
        </div>
      </div>
    </section>
  );
}

export default function Home() {
  return (
    <div className="relative min-h-screen bg-bg-primary text-text-primary overflow-x-hidden">
      <div className="fixed inset-0 pointer-events-none">
        <ThreeBackground />
      </div>

      <div className="relative z-10">
        <Navigation />
        <main>
          <Hero />
          <ScrollStorytelling />
          <InterviewKioskSection />
          <Stats />
          <CTA />
        </main>
        <Footer />
      </div>
    </div>
  );
}
