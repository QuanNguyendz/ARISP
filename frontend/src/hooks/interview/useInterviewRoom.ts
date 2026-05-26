import { useCallback, useEffect, useRef } from 'react';
import { useInterviewStore } from '@store/interview';
import { interviewService } from '@services/interview';
import type { EyeTrackingData, TabSwitchData } from '@types/interview';

export function useInterviewRoom(sessionId: string) {
  const { room, connectionStatus, error, initRoom, setConnectionStatus, setError } = useInterviewStore();
  const signalsBuffer = useRef<unknown[]>([]);
  const flushInterval = useRef<number>();

  useEffect(() => {
    initRoom(sessionId);
    setConnectionStatus('connecting');

    interviewService
      .startSession(sessionId)
      .then(() => {
        setConnectionStatus('connected');
      })
      .catch((err) => {
        setError(err.message || 'Failed to connect to interview room');
        setConnectionStatus('error');
      });

    return () => {
      if (flushInterval.current) {
        clearInterval(flushInterval.current);
      }
    };
  }, [sessionId, initRoom, setConnectionStatus, setError]);

  const collectEyeTracking = useCallback((data: EyeTrackingData) => {
    signalsBuffer.current.push({ type: 'eye_tracking', ...data, timestamp: Date.now() });
  }, []);

  const collectTabSwitch = useCallback((data: TabSwitchData) => {
    signalsBuffer.current.push({ type: 'tab_switch', ...data, timestamp: Date.now() });
  }, []);

  const flushSignals = useCallback(async () => {
    if (signalsBuffer.current.length > 0 && room) {
      const signals = [...signalsBuffer.current];
      signalsBuffer.current = [];
      try {
        await interviewService.submitSignals(room.sessionId, signals);
      } catch {
        signalsBuffer.current = [...signals, ...signalsBuffer.current];
      }
    }
  }, [room]);

  useEffect(() => {
    flushInterval.current = window.setInterval(flushSignals, 5000);
  }, [flushSignals]);

  const endSession = useCallback(async () => {
    if (room) {
      await flushSignals();
      await interviewService.endSession(room.sessionId);
    }
  }, [room, flushSignals]);

  return {
    room,
    connectionStatus,
    error,
    collectEyeTracking,
    collectTabSwitch,
    endSession,
  };
}
