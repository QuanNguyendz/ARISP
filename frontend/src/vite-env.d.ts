/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL: string;
  readonly VITE_WS_BASE_URL: string;
  readonly VITE_ENABLE_CHEAT_DETECTION: string;
  readonly VITE_ENABLE_AVATAR: string;
  readonly VITE_ENABLE_RECORDING: string;
}

interface ImportMeta {
  readonly env: ImportMetaEnv;
}
