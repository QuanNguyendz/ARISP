import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig(({ mode }) => ({
  // react-grid-layout (qua react-draggable) đọc `process.env.NODE_ENV` — không tồn tại
  // trong trình duyệt dưới Vite → thay tĩnh để tránh `ReferenceError: process is not defined`.
  define: {
    'process.env.NODE_ENV': JSON.stringify(mode),
  },
  plugins: [react()],
  resolve: {
    alias: {
      '@ari/shared': path.resolve(__dirname, '../ARI.Shared/src'),
      '@': path.resolve(__dirname, './src'),
    },
    // Đảm bảo chỉ một bản runtime dùng chung cho cả code site và code ARI.Shared.
    dedupe: ['react', 'react-dom', 'react-router-dom', 'zustand', '@tanstack/react-query'],
  },
  server: {
    port: 3001,
    host: true,
  },
  build: {
    outDir: 'dist',
    sourcemap: true,
  },
}));
