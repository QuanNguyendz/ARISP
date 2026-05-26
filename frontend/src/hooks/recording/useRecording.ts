import { useState, useCallback, useRef, useEffect } from 'react';
import { apiClient } from '@services/apiClient';

interface RecordingState {
  isRecording: boolean;
  duration: number;
  chunks: Blob[];
}

export function useRecording() {
  const [state, setState] = useState<RecordingState>({
    isRecording: false,
    duration: 0,
    chunks: [],
  });

  const mediaRecorder = useRef<MediaRecorder | null>(null);
  const stream = useRef<MediaStream | null>(null);
  const timer = useRef<number>();
  const startTime = useRef<number>();

  const startRecording = useCallback(async () => {
    try {
      const mediaStream = await navigator.mediaDevices.getUserMedia({
        audio: true,
        video: true,
      });

      stream.current = mediaStream;
      const recorder = new MediaRecorder(mediaStream, {
        mimeType: 'video/webm;codecs=vp9,opus',
      });

      const chunks: Blob[] = [];
      recorder.ondataavailable = (e) => {
        if (e.data.size > 0) {
          chunks.push(e.data);
        }
      };

      mediaRecorder.current = recorder;
      recorder.start(1000);

      startTime.current = Date.now();
      timer.current = window.setInterval(() => {
        setState((prev) => ({ ...prev, duration: Math.floor((Date.now() - startTime.current!) / 1000) }));
      }, 1000);

      setState({ isRecording: true, duration: 0, chunks: [] });
    } catch (err) {
      console.error('Failed to start recording:', err);
      throw err;
    }
  }, []);

  const stopRecording = useCallback(async (): Promise<string | null> => {
    return new Promise((resolve) => {
      if (!mediaRecorder.current || !stream.current) {
        resolve(null);
        return;
      }

      if (timer.current) {
        clearInterval(timer.current);
      }

      mediaRecorder.current.onstop = async () => {
        const blob = new Blob(state.chunks, { type: 'video/webm' });

        const formData = new FormData();
        formData.append('video', blob, `recording-${Date.now()}.webm`);

        try {
          const response = await apiClient.post('/recordings/upload', formData, {
            headers: { 'Content-Type': 'multipart/form-data' },
          });
          resolve(response.data.url);
        } catch {
          resolve(null);
        }

        stream.current?.getTracks().forEach((track) => track.stop());
        stream.current = null;
        mediaRecorder.current = null;

        setState({ isRecording: false, duration: 0, chunks: [] });
      };

      mediaRecorder.current.stop();
    });
  }, [state.chunks]);

  const pauseRecording = useCallback(() => {
    if (mediaRecorder.current?.state === 'recording') {
      mediaRecorder.current.pause();
    }
  }, []);

  const resumeRecording = useCallback(() => {
    if (mediaRecorder.current?.state === 'paused') {
      mediaRecorder.current.resume();
    }
  }, []);

  useEffect(() => {
    return () => {
      if (timer.current) clearInterval(timer.current);
      stream.current?.getTracks().forEach((track) => track.stop());
    };
  }, []);

  return {
    isRecording: state.isRecording,
    duration: state.duration,
    startRecording,
    stopRecording,
    pauseRecording,
    resumeRecording,
    stream: stream.current,
  };
}
