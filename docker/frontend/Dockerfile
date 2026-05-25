# ============================================
# ARISP Frontend – Dockerfile (React + TypeScript)
# Multi-stage: development | build | production
# ============================================

# --- Stage: development (Vite dev server with HMR) ---
FROM node:20-alpine AS development
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci
COPY . .
EXPOSE 3000
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0", "--port", "3000"]

# --- Stage: build ---
FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci
COPY . .
RUN npm run build

# --- Stage: production (serve static files with nginx) ---
FROM nginx:1.27-alpine AS production
# Remove default nginx config
RUN rm /etc/nginx/conf.d/default.conf
# Copy custom nginx config for SPA
COPY --from=build /app/dist /usr/share/nginx/html
# Simple nginx config for SPA routing
RUN echo 'server { \
    listen 3000; \
    root /usr/share/nginx/html; \
    index index.html; \
    location / { \
        try_files $uri $uri/ /index.html; \
    } \
}' > /etc/nginx/conf.d/default.conf
EXPOSE 3000
CMD ["nginx", "-g", "daemon off;"]
