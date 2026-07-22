# ============================================
# ARISP Web – Dockerfile (npm workspaces: CandidateSite / StaffSite / Shared)
# Multi-stage: development | build | production
# Build context = ../ari-web (workspace root, chứa 1 package-lock.json duy nhất)
# ARG PKG  = npm workspace name (@ari/candidate-site | @ari/staff-site)
# ARG SITE = thư mục package (ARI.CandidateSite | ARI.StaffSite)
# ARG PORT = cổng phục vụ (3000 candidate | 3001 staff)
# ARG VITE_API_BASE_URL = gốc API nhúng vào bundle lúc build (Vite inline lúc compile,
#     KHÔNG đọc được ở runtime). Mặc định "/api" (tương đối) vì Nginx proxy /api/ cùng
#     origin cho cả 2 site → image không khoá cứng vào domain, tái dùng cho staging.
# ============================================

# --- Stage: development (Vite dev server with HMR) ---
FROM node:20-alpine AS development
WORKDIR /app
ARG PKG=@ari/candidate-site
ARG PORT=3000
ENV PKG=$PKG
ENV PORT=$PORT
# Copy manifest + lock + package.json từng workspace TRƯỚC để tận dụng layer cache
COPY package.json package-lock.json ./
COPY src/ARI.Shared/package.json ./src/ARI.Shared/package.json
COPY src/ARI.CandidateSite/package.json ./src/ARI.CandidateSite/package.json
COPY src/ARI.StaffSite/package.json ./src/ARI.StaffSite/package.json
RUN npm ci
COPY . .
EXPOSE $PORT
CMD npm run dev -w $PKG -- --host 0.0.0.0 --port $PORT

# --- Stage: build ---
FROM node:20-alpine AS build
WORKDIR /app
ARG PKG=@ari/candidate-site
COPY package.json package-lock.json ./
COPY src/ARI.Shared/package.json ./src/ARI.Shared/package.json
COPY src/ARI.CandidateSite/package.json ./src/ARI.CandidateSite/package.json
COPY src/ARI.StaffSite/package.json ./src/ARI.StaffSite/package.json
RUN npm ci
COPY . .
# Vite inline import.meta.env.VITE_* lúc build → phải set TRƯỚC khi build, không phải runtime.
# Thiếu ARG này thì constants.ts fallback về http://localhost:5000/api và bundle prod hỏng.
# Hai SignalR hub (usePracticeSession, useAppNotifications) đều dẫn xuất từ API_BASE_URL nên
# giá trị tương đối "/api" cũng làm hub thành relative → tự dùng wss:// theo origin.
ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build -w $PKG

# --- Stage: production (serve static files with nginx) ---
FROM nginx:1.27-alpine AS production
ARG SITE=ARI.CandidateSite
ARG PORT=3000
RUN rm /etc/nginx/conf.d/default.conf
COPY --from=build /app/src/${SITE}/dist /usr/share/nginx/html
# SPA routing config (giữ $uri literal — dùng nháy đơn cho printf)
RUN printf 'server { listen %s; root /usr/share/nginx/html; index index.html; location / { try_files $uri $uri/ /index.html; } }' "$PORT" > /etc/nginx/conf.d/default.conf
EXPOSE $PORT
CMD ["nginx", "-g", "daemon off;"]
