# ==============================================================================
# Stage 1: Build Stage (Node.js 22 Alpine)
# ==============================================================================
FROM node:22-alpine AS builder
WORKDIR /app

# Khai báo build arguments cho Vite environment variables (mặc định dùng relative path qua Nginx reverse proxy)
ARG VITE_API_BASE_URL=/api/v1
ARG VITE_SIGNALR_HUB_URL=/hubs/practice
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL \
    VITE_SIGNALR_HUB_URL=$VITE_SIGNALR_HUB_URL

# Cài đặt dependencies với npm ci để tận dụng Docker layer caching
COPY package.json package-lock.json ./
RUN npm ci

# Sao chép cấu hình và toàn bộ mã nguồn
COPY tsconfig*.json vite.config.ts index.html .oxlintrc.json ./
COPY public/ ./public/
COPY src/ ./src/

# Biên dịch kiểu tĩnh và đóng gói production bundle vào /app/dist
RUN npm run build

# ==============================================================================
# Stage 2: Production Runner (Nginx 1.27 Alpine)
# ==============================================================================
FROM nginx:1.27-alpine AS runner

# Xóa cấu hình mặc định của Nginx
RUN rm -rf /etc/nginx/conf.d/default.conf

# Sao chép build artifacts từ builder stage
COPY --from=builder /app/dist /usr/share/nginx/html

# Sao chép tệp cấu hình Nginx tối ưu cho SPA React và SignalR WebSocket
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

# Healthcheck xác thực Nginx phục vụ trang chủ
HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
    CMD wget -q -O /dev/null http://localhost/ || exit 1

CMD ["nginx", "-g", "daemon off;"]
