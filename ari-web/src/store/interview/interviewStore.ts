import { create } from 'zustand';
import type { InterviewRoomState, EyeTrackingData, ResponseTimingData, TabSwitchData } from '../../types/interview';

interface InterviewState {
  room: InterviewRoomState | null;
  connectionStatus: 'disconnected' | 'connecting' | 'connected' | 'error';
  error: string | null;
  initRoom: (sessionId: string) => void;
  updateRoom: (partial: Partial<InterviewRoomState>) => void;
  updateStatus: (status: InterviewRoomState['status']) => void;
  setCurrentQuestion: (question: string, index: number) => void;
  setCandidateSpeaking: (speaking: boolean) => void;
  setAiSpeaking: (speaking: boolean) => void;
  updateSignals: (signals: {
    eyeTracking?: EyeTrackingData;
    responseTiming?: ResponseTimingData;
    tabSwitch?: TabSwitchData;
  }) => void;
  setConnectionStatus: (status: InterviewState['connectionStatus']) => void;
  setError: (error: string | null) => void;
  resetRoom: () => void;
}

export const useInterviewStore = create<InterviewState>((set) => ({
  room: null,
  connectionStatus: 'disconnected',
  error: null,

  initRoom: (sessionId) =>
    set({
      room: {
        sessionId,
        status: 'idle',
        currentRound: 1,
        currentQuestionIndex: 0,
        totalQuestions: 0,
        candidateSpeaking: false,
        aiSpeaking: false,
        elapsedTime: 0,
        remainingTime: 0,
        signals: {},
      },
      connectionStatus: 'disconnected',
      error: null,
    }),

  updateRoom: (partial) =>
    set((state) => ({
      room: state.room ? { ...state.room, ...partial } : null,
    })),

  updateStatus: (status) =>
    set((state) => ({
      room: state.room ? { ...state.room, status } : null,
    })),

  setCurrentQuestion: (question, index) =>
    set((state) => ({
      room: state.room
        ? {
            ...state.room,
            currentQuestion: question,
            currentQuestionIndex: index,
          }
        : null,
    })),

  setCandidateSpeaking: (speaking) =>
    set((state) => ({
      room: state.room ? { ...state.room, candidateSpeaking: speaking } : null,
    })),

  setAiSpeaking: (speaking) =>
    set((state) => ({
      room: state.room ? { ...state.room, aiSpeaking: speaking } : null,
    })),

  updateSignals: (signals) =>
    set((state) => ({
      room: state.room
        ? {
            ...state.room,
            signals: { ...state.room.signals, ...signals },
          }
        : null,
    })),

  setConnectionStatus: (status) => set({ connectionStatus: status }),

  setError: (error) => set({ error }),

  resetRoom: () => set({ room: null, connectionStatus: 'disconnected', error: null }),
}));
