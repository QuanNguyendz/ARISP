import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Mic, MicOff, Video, VideoOff, MessageSquare, Phone, Volume2, Settings, ChevronRight } from 'lucide-react';
import { ErrorAlert } from '@components/common';

const questions = [
  'Xin chào! Rất vui được trò chuyện với bạn. Bạn có thể giới thiệu về bản thân được không?',
  'Bạn có thể cho tôi biết về kinh nghiệm làm việc với Node.js của bạn không?',
  'Bạn đã từng thiết kế một hệ thống scalable chưa? Có thể chia sẻ một chút không?',
];

const answers = [
  'Tôi là Nguyễn Văn An, có 6 năm kinh nghiệm trong lĩnh vực phát triển phần mềm...',
  'Tôi đã làm việc với Node.js trong khoảng 5 năm, xây dựng nhiều ứng dụng từ nhỏ đến lớn...',
  'Vâng, tôi đã thiết kế một hệ thống microservices cho dự án thương mại điện tử với hàng triệu người dùng...',
];

export default function InterviewRoomPage() {
  const { sessionId } = useParams<{ sessionId: string }>();
  const navigate = useNavigate();
  const [isRecording] = useState(false);
  const [isMuted, setIsMuted] = useState(false);
  const [isVideoOn, setIsVideoOn] = useState(true);
  const [currentQuestion, setCurrentQuestion] = useState(0);
  const [showAnswer, setShowAnswer] = useState(false);
  const [conversation, setConversation] = useState<Array<{ type: 'ai' | 'candidate'; text: string }>>([]);
  const [isInterviewEnded, setIsInterviewEnded] = useState(false);

  useEffect(() => {
    if (!sessionId) return;
    setConversation([{ type: 'ai', text: questions[0] }]);
  }, [sessionId]);

  const handleNextQuestion = () => {
    if (currentQuestion < questions.length - 1) {
      setShowAnswer(true);
      setTimeout(() => {
        setConversation(prev => [...prev, { type: 'candidate', text: answers[currentQuestion] }]);
        setTimeout(() => {
          const nextQ = currentQuestion + 1;
          setCurrentQuestion(nextQ);
          setConversation(prev => [...prev, { type: 'ai', text: questions[nextQ] }]);
          setShowAnswer(false);
        }, 500);
      }, 1000);
    } else {
      setIsInterviewEnded(true);
      setConversation(prev => [...prev, { type: 'candidate', text: answers[currentQuestion] }]);
    }
  };

  const handleEndInterview = () => {
    navigate('/candidate/results');
  };

  if (!sessionId) {
    return <ErrorAlert message="Invalid session" />;
  }

  if (isInterviewEnded) {
    return (
      <div className="flex-1 bg-bg-primary flex items-center justify-center min-h-0">
        <motion.div
          initial={{ opacity: 0, scale: 0.9 }}
          animate={{ opacity: 1, scale: 1 }}
          className="text-center max-w-md"
        >
          <div className="w-24 h-24 rounded-full bg-emerald-500/20 flex items-center justify-center mx-auto mb-6">
            <MessageSquare className="w-12 h-12 text-emerald-400" />
          </div>
          <h1 className="text-3xl font-semibold text-white mb-4">Hoàn thành phỏng vấn!</h1>
          <p className="text-text-secondary mb-8">
            Cảm ơn bạn đã tham gia phỏng vấn. Kết quả sẽ được gửi qua email trong vòng 24 giờ.
          </p>
          <button
            onClick={() => navigate('/candidate/results')}
            className="px-8 py-4 rounded-xl bg-gradient-to-r from-accent-primary to-violet text-white font-medium hover:opacity-90 transition-opacity"
          >
            Xem kết quả
          </button>
        </motion.div>
      </div>
    );
  }

  return (
    <div className="flex-1 bg-bg-primary flex flex-col min-h-0">
      {/* Header */}
      <div className="h-16 bg-black/50 backdrop-blur-xl border-b border-white/10 flex items-center justify-between px-6">
        <div className="flex items-center gap-4">
          <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
            <span className="text-white font-semibold">A</span>
          </div>
          <div>
            <p className="text-white font-medium">Alex - AI Interviewer</p>
            <p className="text-xs text-text-tertiary">Vòng 1 - Screening</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          {isRecording && (
            <div className="flex items-center gap-2 px-3 py-1 rounded-full bg-red-500/20 text-red-400">
              <div className="w-2 h-2 rounded-full bg-red-500 animate-pulse" />
              <span className="text-sm">Recording</span>
            </div>
          )}
          <button className="p-2 rounded-lg hover:bg-white/10 transition-colors">
            <Settings className="w-5 h-5 text-text-tertiary" />
          </button>
        </div>
      </div>

      {/* Main Content */}
      <div className="flex-1 flex">
        {/* Left - AI Avatar & Video */}
        <div className="flex-1 flex flex-col items-center justify-center p-8 relative">
          {/* AI Avatar */}
          <motion.div
            className="relative w-80 h-80"
            animate={{ scale: isRecording ? [1, 1.02, 1] : 1 }}
            transition={{ duration: 2, repeat: isRecording ? Infinity : 0 }}
          >
            <div className="absolute inset-0 bg-gradient-to-br from-accent-primary/30 to-violet/30 rounded-full blur-3xl" />
            <div className="relative w-full h-full rounded-full bg-gradient-to-br from-accent-primary to-violet flex items-center justify-center">
              <div className="text-center">
                <p className="text-6xl font-semibold text-white mb-2">AI</p>
                <p className="text-white/70">Alex</p>
              </div>
            </div>
            {/* Speaking indicator */}
            {showAnswer && (
              <motion.div
                className="absolute -bottom-4 left-1/2 -translate-x-1/2 flex items-end gap-1"
                animate={{ height: [16, 24, 16, 20, 16] }}
                transition={{ duration: 0.5, repeat: Infinity }}
              >
                <div className="w-1 bg-accent-primary rounded-full h-4" />
                <div className="w-1 bg-accent-primary rounded-full h-3" />
                <div className="w-1 bg-accent-primary rounded-full h-4" />
                <div className="w-1 bg-accent-primary rounded-full h-2" />
              </motion.div>
            )}
          </motion.div>

          <div className="mt-8 text-center">
            <p className="text-lg text-white font-medium">Câu hỏi {currentQuestion + 1} / {questions.length}</p>
            <div className="flex items-center justify-center gap-2 mt-2">
              {[...Array(questions.length)].map((_, i) => (
                <div
                  key={i}
                  className={`w-2 h-2 rounded-full ${
                    i <= currentQuestion ? 'bg-accent-primary' : 'bg-white/20'
                  }`}
                />
              ))}
            </div>
          </div>
        </div>

        {/* Right - Chat & Controls */}
        <div className="w-96 bg-black/30 border-l border-white/10 flex flex-col">
          {/* Conversation */}
          <div className="flex-1 overflow-y-auto p-4 space-y-4">
            {conversation.map((msg, index) => (
              <motion.div
                key={index}
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                className={`flex ${msg.type === 'candidate' ? 'justify-end' : 'justify-start'}`}
              >
                <div
                  className={`max-w-[85%] p-4 rounded-2xl ${
                    msg.type === 'ai'
                      ? 'bg-white/10 text-white'
                      : 'bg-accent-primary/30 text-white'
                  }`}
                >
                  <p className="text-sm">{msg.text}</p>
                </div>
              </motion.div>
            ))}
            {showAnswer && (
              <motion.div
                initial={{ opacity: 0, y: 10 }}
                animate={{ opacity: 1, y: 0 }}
                className="flex justify-start"
              >
                <div className="max-w-[85%] p-4 rounded-2xl bg-white/10">
                  <div className="flex items-center gap-2 text-text-tertiary">
                    <div className="w-2 h-2 rounded-full bg-white animate-pulse" />
                    <span className="text-sm">Đang nghe...</span>
                  </div>
                </div>
              </motion.div>
            )}
          </div>

          {/* Controls */}
          <div className="p-4 border-t border-white/10">
            <div className="flex items-center justify-center gap-4 mb-4">
              <button
                onClick={() => setIsMuted(!isMuted)}
                className={`w-14 h-14 rounded-full flex items-center justify-center transition-colors ${
                  isMuted ? 'bg-red-500/20 text-red-400' : 'bg-white/10 text-white hover:bg-white/20'
                }`}
              >
                {isMuted ? <MicOff className="w-6 h-6" /> : <Mic className="w-6 h-6" />}
              </button>
              <button
                onClick={() => setIsVideoOn(!isVideoOn)}
                className={`w-14 h-14 rounded-full flex items-center justify-center transition-colors ${
                  !isVideoOn ? 'bg-red-500/20 text-red-400' : 'bg-white/10 text-white hover:bg-white/20'
                }`}
              >
                {isVideoOn ? <Video className="w-6 h-6" /> : <VideoOff className="w-6 h-6" />}
              </button>
              <button className="w-14 h-14 rounded-full bg-white/10 text-white hover:bg-white/20 flex items-center justify-center transition-colors">
                <Volume2 className="w-6 h-6" />
              </button>
              <button
                onClick={handleNextQuestion}
                className="w-14 h-14 rounded-full bg-accent-primary text-white hover:opacity-90 flex items-center justify-center transition-opacity"
              >
                <ChevronRight className="w-6 h-6" />
              </button>
            </div>

            <button
              onClick={handleEndInterview}
              className="w-full py-3 rounded-xl bg-red-500/20 text-red-400 hover:bg-red-500/30 transition-colors flex items-center justify-center gap-2"
            >
              <Phone className="w-5 h-5" />
              Kết thúc phỏng vấn
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
