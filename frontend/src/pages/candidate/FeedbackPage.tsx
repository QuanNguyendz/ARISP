import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import {
  CheckCircle, Clock, BarChart3, MessageSquare, Video, Download,
  Home, TrendingUp, TrendingDown, Minus, ArrowRight, Brain
} from 'lucide-react';
import CandidateLayout from '@components/layout/CandidateLayout';

interface InterviewResult {
  id: string;
  jobTitle: string;
  company: string;
  interviewDate: string;
  overallScore: number;
  status: 'passed' | 'pending' | 'failed';
  metrics: Array<{
    label: string;
    score: number;
    trend: 'up' | 'stable' | 'down';
    description: string;
  }>;
  strengths: string[];
  improvements: string[];
  aiFeedback: string;
}

const interviewResults: InterviewResult[] = [
  {
    id: '1',
    jobTitle: 'Senior Frontend Developer',
    company: 'TechVision Corp',
    interviewDate: '25/05/2026',
    overallScore: 87,
    status: 'passed',
    metrics: [
      { label: 'Kỹ thuật', score: 92, trend: 'up', description: 'Kiến thức React, TypeScript vững chắc' },
      { label: 'Giao tiếp', score: 85, trend: 'up', description: 'Trình bày rõ ràng, mạch lạc' },
      { label: 'Giải quyết vấn đề', score: 84, trend: 'stable', description: 'Phân tích bài toán tốt' },
    ],
    strengths: [
      'Nắm vững các thư viện React hiện đại',
      'Có kinh nghiệm với TypeScript và testing',
      'Hiểu biết tốt về performance optimization',
    ],
    improvements: [
      'Cần cải thiện kỹ năng trình bày ý tưởng trước đám đông',
      'Một số câu hỏi về system design còn chưa sâu',
    ],
    aiFeedback: 'Ứng viên thể hiện tốt kiến thức chuyên môn và phong thái chuyên nghiệp. Đề xuất tiếp tục vòng tiếp theo.',
  },
  {
    id: '2',
    jobTitle: 'Backend Engineer',
    company: 'DataFlow Systems',
    interviewDate: '20/05/2026',
    overallScore: 0,
    status: 'pending',
    metrics: [],
    strengths: [],
    improvements: [],
    aiFeedback: 'Kết quả đang được xử lý. Vui lòng chờ trong 24-48 giờ.',
  },
];

function ScoreRing({ score }: { score: number }) {
  const circumference = 2 * Math.PI * 45;
  const strokeDashoffset = circumference - (score / 100) * circumference;
  const color = score >= 80 ? '#10b981' : score >= 60 ? '#f59e0b' : '#ef4444';

  return (
    <div className="relative w-32 h-32 flex-shrink-0">
      <svg className="w-full h-full -rotate-90" viewBox="0 0 100 100">
        <circle cx="50" cy="50" r="45" fill="none" stroke="rgba(255,255,255,0.05)" strokeWidth="6" />
        <circle
          cx="50"
          cy="50"
          r="45"
          fill="none"
          stroke={color}
          strokeWidth="6"
          strokeDasharray={circumference}
          strokeDashoffset={strokeDashoffset}
          strokeLinecap="round"
          className="transition-all duration-1000"
        />
      </svg>
      <div className="absolute inset-0 flex items-center justify-center">
        <span className="text-3xl font-bold text-white">{score}</span>
      </div>
    </div>
  );
}

function TrendIcon({ trend }: { trend: 'up' | 'stable' | 'down' }) {
  if (trend === 'up') return <TrendingUp className="w-4 h-4 text-emerald-400" />;
  if (trend === 'down') return <TrendingDown className="w-4 h-4 text-red-400" />;
  return <Minus className="w-4 h-4 text-white/40" />;
}

function ResultCard({ result }: { result: InterviewResult }) {
  const navigate = useNavigate();

  if (result.status === 'pending') {
    return (
      <motion.div
        key={result.id}
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="p-6 rounded-2xl bg-white/5 border border-white/10"
      >
        <div className="flex items-start gap-4 mb-4">
          <div className="w-12 h-12 rounded-xl bg-amber-500/20 flex items-center justify-center flex-shrink-0">
            <Clock className="w-6 h-6 text-amber-400" />
          </div>
          <div className="flex-1">
            <h3 className="text-lg font-semibold text-white">{result.jobTitle}</h3>
            <p className="text-sm text-white/50">{result.company} • {result.interviewDate}</p>
          </div>
          <span className="px-3 py-1 rounded-full bg-amber-500/20 text-amber-400 text-xs font-medium">
            Đang chờ
          </span>
        </div>
        <p className="text-sm text-white/50">Kết quả đang được xử lý bởi hệ thống AI. Thông báo sẽ được gửi qua email.</p>
      </motion.div>
    );
  }

  const isPassed = result.status === 'passed';

  return (
    <motion.div
      key={result.id}
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className="rounded-2xl bg-white/5 border border-white/10 overflow-hidden"
    >
      {/* Header */}
      <div className="p-6">
        <div className="flex items-start gap-4 mb-4">
          <ScoreRing score={result.overallScore} />
          <div className="flex-1">
            <div className="flex items-center gap-2 mb-1">
              <h3 className="text-lg font-semibold text-white">{result.jobTitle}</h3>
              <span className={`px-2 py-0.5 rounded-full text-xs font-medium ${
                isPassed ? 'bg-emerald-500/20 text-emerald-400' : 'bg-red-500/20 text-red-400'
              }`}>
                {isPassed ? 'Đạt' : 'Chưa đạt'}
              </span>
            </div>
            <p className="text-sm text-white/50">{result.company}</p>
            <p className="text-xs text-white/30 mt-1">{result.interviewDate}</p>
          </div>
        </div>

        {/* Metrics */}
        {result.metrics.length > 0 && (
          <div className="grid grid-cols-3 gap-3 mb-4">
            {result.metrics.map((metric) => (
              <div key={metric.label} className="p-3 rounded-xl bg-white/[0.02] text-center">
                <div className="flex items-center justify-center gap-1 mb-1">
                  <span className="text-xs text-white/50">{metric.label}</span>
                  <TrendIcon trend={metric.trend} />
                </div>
                <p className="text-xl font-semibold text-white">{metric.score}%</p>
                <div className="h-1.5 bg-white/10 rounded-full mt-2 overflow-hidden">
                  <div
                    className={`h-full rounded-full ${
                      metric.score >= 80 ? 'bg-emerald-500' :
                      metric.score >= 60 ? 'bg-amber-500' : 'bg-red-500'
                    }`}
                    style={{ width: `${metric.score}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* AI Feedback */}
      <div className="px-6 pb-4">
        <div className="p-4 rounded-xl bg-accent-primary/10 border border-accent-primary/20">
          <div className="flex items-center gap-2 mb-2">
            <Brain className="w-4 h-4 text-accent-primary" />
            <span className="text-sm font-medium text-accent-primary">Phản hồi từ AI</span>
          </div>
          <p className="text-sm text-white/70">{result.aiFeedback}</p>
        </div>
      </div>

      {/* Strengths & Improvements */}
      {isPassed && result.strengths.length > 0 && (
        <div className="px-6 pb-4">
          <div className="grid grid-cols-2 gap-3">
            <div className="p-4 rounded-xl bg-emerald-500/5 border border-emerald-500/10">
              <div className="flex items-center gap-2 mb-2">
                <TrendingUp className="w-4 h-4 text-emerald-400" />
                <span className="text-sm font-medium text-emerald-400">Điểm mạnh</span>
              </div>
              <ul className="space-y-1">
                {result.strengths.map((s, i) => (
                  <li key={i} className="text-xs text-white/60 flex items-start gap-1.5">
                    <span className="text-emerald-400 mt-0.5">•</span>
                    {s}
                  </li>
                ))}
              </ul>
            </div>
            <div className="p-4 rounded-xl bg-amber-500/5 border border-amber-500/10">
              <div className="flex items-center gap-2 mb-2">
                <TrendingDown className="w-4 h-4 text-amber-400" />
                <span className="text-sm font-medium text-amber-400">Cần cải thiện</span>
              </div>
              <ul className="space-y-1">
                {result.improvements.map((s, i) => (
                  <li key={i} className="text-xs text-white/60 flex items-start gap-1.5">
                    <span className="text-amber-400 mt-0.5">•</span>
                    {s}
                  </li>
                ))}
              </ul>
            </div>
          </div>
        </div>
      )}

      {/* Actions */}
      <div className="px-6 pb-6 flex gap-3">
        <button
          onClick={() => navigate('/jobs')}
          className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-white/5 border border-white/10 text-white hover:bg-white/10 transition-colors text-sm"
        >
          <Home className="w-4 h-4" />
          Về trang chủ
        </button>
        <button className="flex-1 flex items-center justify-center gap-2 px-4 py-3 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity text-sm">
          <Download className="w-4 h-4" />
          Tải báo cáo
        </button>
      </div>
    </motion.div>
  );
}

export default function FeedbackPage() {
  const navigate = useNavigate();
  const passedCount = interviewResults.filter(r => r.status === 'passed').length;
  const avgScore = Math.round(
    interviewResults.filter(r => r.status !== 'pending').reduce((sum, r) => sum + r.overallScore, 0) /
    interviewResults.filter(r => r.status !== 'pending').length
  );

  return (
    <CandidateLayout>
      <div className="min-h-screen py-8">
        <div className="max-w-4xl mx-auto px-6">
          {/* Header */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            className="mb-8"
          >
            <h1 className="text-3xl font-bold text-white mb-2">Kết quả phỏng vấn</h1>
            <p className="text-text-secondary">Xem kết quả và phản hồi từ các buổi phỏng vấn AI</p>
          </motion.div>

          {/* Stats */}
          <div className="grid grid-cols-3 gap-4 mb-8">
            {[
              { label: 'Phỏng vấn', value: interviewResults.length.toString(), icon: Video, color: 'text-blue-400', bg: 'bg-blue-500/20' },
              { label: 'Đạt', value: passedCount.toString(), icon: CheckCircle, color: 'text-emerald-400', bg: 'bg-emerald-500/20' },
              { label: 'Điểm TB', value: avgScore.toString(), icon: BarChart3, color: 'text-violet-400', bg: 'bg-violet-500/20' },
            ].map((stat, index) => (
              <motion.div
                key={stat.label}
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                transition={{ delay: index * 0.1 }}
                className={`p-5 rounded-2xl ${stat.bg} border border-white/5`}
              >
                <stat.icon className={`w-5 h-5 ${stat.color} mb-2`} />
                <p className={`text-2xl font-bold ${stat.color}`}>{stat.value}</p>
                <p className="text-sm text-white/50">{stat.label}</p>
              </motion.div>
            ))}
          </div>

          {/* Results */}
          <div className="space-y-4">
            {interviewResults.map((result) => (
              <ResultCard key={result.id} result={result} />
            ))}
          </div>

          {/* CTA */}
          <motion.div
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: 0.3 }}
            className="mt-8 p-6 rounded-2xl bg-gradient-to-r from-accent-primary/10 to-violet/10 border border-accent-primary/20"
          >
            <div className="flex items-center gap-4">
              <div className="w-12 h-12 rounded-xl bg-accent-primary/20 flex items-center justify-center flex-shrink-0">
                <MessageSquare className="w-6 h-6 text-accent-primary" />
              </div>
              <div className="flex-1">
                <h3 className="font-semibold text-white mb-1">Bạn muốn luyện tập thêm?</h3>
                <p className="text-sm text-white/50">Trải nghiệm phỏng vấn thử với AI để cải thiện kỹ năng</p>
              </div>
              <button
                onClick={() => navigate('/interview/room/practice')}
                className="px-5 py-2.5 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white text-sm font-medium hover:opacity-90 transition-opacity flex items-center gap-2 whitespace-nowrap"
              >
                Luyện tập ngay
                <ArrowRight className="w-4 h-4" />
              </button>
            </div>
          </motion.div>
        </div>
      </div>
    </CandidateLayout>
  );
}
