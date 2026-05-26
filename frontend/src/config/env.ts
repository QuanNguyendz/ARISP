export default {
  env: {
    apiBaseUrl: import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api',
    wsBaseUrl: import.meta.env.VITE_WS_BASE_URL || 'ws://localhost:5000',
  },
  features: {
    enableCheatDetection: import.meta.env.VITE_ENABLE_CHEAT_DETECTION === 'true',
    enableAvatar: import.meta.env.VITE_ENABLE_AVATAR !== 'false',
    enableRecording: import.meta.env.VITE_ENABLE_RECORDING !== 'false',
  },
};
