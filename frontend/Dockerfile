# ==============================================================================
# Stage 1: Build Stage (Node.js 22 Alpine)
# ==============================================================================
FROM node:22-alpine AS builder
WORKDIR /app

# Khai báo build arguments cho Vite environment variables (mặc định dùng relative path qua Nginx reverse proxy)
ARG VITE_API_BASE_URL=/api/v1
ARG VITE_SIGNALR_HUB_URL=/hubs/practice
ARG VITE_GOOGLE_CLIENT_ID=
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL \
    VITE_SIGNALR_HUB_URL=$VITE_SIGNALR_HUB_URL \
    VITE_GOOGLE_CLIENT_ID=$VITE_GOOGLE_CLIENT_ID

# Cài đặt dependencies với npm ci để tận dụng Docker layer caching
COPY package.json package-lock.json ./
RUN npm ci

# Sao chép cấu hình và toàn bộ mã nguồn
COPY tsconfig*.json vite.config.ts index.html ./
COPY public/ ./public/
COPY src/ ./src/

# Biên dịch kiểu tĩnh và đóng gói production bundle vào /app/dist
RUN npm run build

# ==============================================================================
# Stage 2: Production Runner (Nginx 1.27 Alpine)
# ==============================================================================
FROM nginx:1.27-alpine AS runner

# Xóa cấu hình mặc định và chuẩn bị thư mục ghi cho non-root user nginx (UID 101)
RUN rm -rf /etc/nginx/conf.d/default.conf && \
    mkdir -p /var/cache/nginx /var/log/nginx /run/nginx /usr/share/nginx/html && \
    sed -i -e '/^[[:space:]]*user[[:space:]]/d' \
           -e 's|^[[:space:]]*pid[[:space:]][[:space:]]*/var/run/nginx\.pid;|pid /run/nginx/nginx.pid;|g' \
           -e 's|^[[:space:]]*pid[[:space:]][[:space:]]*/run/nginx\.pid;|pid /run/nginx/nginx.pid;|g' \
           /etc/nginx/nginx.conf && \
    grep -Eq '^pid /run/nginx/nginx\.pid;$' /etc/nginx/nginx.conf && \
    ! grep -Eq '^[[:space:]]*user[[:space:]]' /etc/nginx/nginx.conf && \
    chown -R nginx:nginx /var/cache/nginx /var/log/nginx /run/nginx /etc/nginx/conf.d /usr/share/nginx/html

# Sao chép build artifacts và cấu hình Nginx với quyền sở hữu nginx
COPY --chown=nginx:nginx --from=builder /app/dist /usr/share/nginx/html
COPY --chown=nginx:nginx nginx.conf /etc/nginx/conf.d/default.conf

USER nginx
EXPOSE 8080 8443

# Healthcheck xác thực Nginx qua endpoint nội bộ /healthz trên cổng non-root 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=5s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1

CMD ["nginx", "-g", "daemon off;"]
