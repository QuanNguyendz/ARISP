import { useEffect, useRef, useCallback } from 'react';
import type { EyeTrackingData, TabSwitchData } from '../../types/interview';

interface CheatDetectionConfig {
  enableEyeTracking: boolean;
  enableTabSwitch: boolean;
  enableResponseTiming: boolean;
  onEyeTracking?: (data: EyeTrackingData) => void;
  onTabSwitch?: (data: TabSwitchData) => void;
}

export function useCheatDetection(config: CheatDetectionConfig) {
  const eyeTrackingInterval = useRef<number>();
  const lastTabFocus = useRef<boolean>(document.hasFocus());
  const tabSwitchCount = useRef<number>(0);

  const handleVisibilityChange = useCallback(() => {
    if (!config.enableTabSwitch) return;

    const isVisible = document.visibilityState === 'visible';
    const now = Date.now();

    if (!isVisible && lastTabFocus.current) {
      tabSwitchCount.current++;
      config.onTabSwitch?.({
        timestamp: now,
        duration: 0,
        count: tabSwitchCount.current,
      });
    }

    lastTabFocus.current = isVisible;
  }, [config]);

  const handleBlur = useCallback(() => {
    if (!config.enableTabSwitch) return;
    tabSwitchCount.current++;
    config.onTabSwitch?.({
      timestamp: Date.now(),
      duration: 0,
      count: tabSwitchCount.current,
    });
  }, [config]);

  useEffect(() => {
    if (config.enableTabSwitch) {
      document.addEventListener('visibilitychange', handleVisibilityChange);
      window.addEventListener('blur', handleBlur);
    }

    return () => {
      if (config.enableTabSwitch) {
        document.removeEventListener('visibilitychange', handleVisibilityChange);
        window.removeEventListener('blur', handleBlur);
      }
      if (eyeTrackingInterval.current) {
        clearInterval(eyeTrackingInterval.current);
      }
    };
  }, [config.enableTabSwitch, handleVisibilityChange, handleBlur]);

  return {
    tabSwitchCount: tabSwitchCount.current,
  };
}
