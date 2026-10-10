# ==============================================================================
# Stage 1: Build & Publish (.NET 8 SDK Alpine)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Sao chép solution và toàn bộ 5 tệp .csproj để tối ưu hóa Docker layer caching
COPY OralExamination.sln ./
COPY src/Domain/Domain.csproj src/Domain/
COPY src/Application/Application.csproj src/Application/
COPY src/Infrastructure/Infrastructure.csproj src/Infrastructure/
COPY src/API/API.csproj src/API/
COPY tests/OralExamination.UnitTests/OralExamination.UnitTests.csproj tests/OralExamination.UnitTests/

# Khôi phục dependencies cho toàn bộ solution
RUN dotnet restore OralExamination.sln

# Sao chép toàn bộ mã nguồn src/ và biên dịch release
COPY src/ src/
WORKDIR /src/src/API
RUN dotnet publish API.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ==============================================================================
# Stage 2: Runtime Environment (.NET 8 ASP.NET Alpine)
# ==============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Tạo non-root user appuser (UID 10001) bảo mật theo chuẩn CIS Benchmark
RUN addgroup -g 10001 -S appgroup && adduser -u 10001 -S appuser -G appgroup

# Cài đặt gói icu-libs (hỗ trợ globalization/tiếng Việt) và tzdata (múi giờ)
RUN apk add --no-cache icu-libs tzdata

# Sao chép build artifacts từ build stage và gán quyền cho appuser
COPY --from=build --chown=appuser:appgroup /app/publish .

# Cấu hình biến môi trường và cổng 5000 chuẩn Capstone Invariant
ENV ASPNETCORE_HTTP_PORTS=5000 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

USER appuser
EXPOSE 5000

# Healthcheck endpoint kiểm tra /health định kỳ qua wget của BusyBox
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:5000/health || exit 1

ENTRYPOINT ["dotnet", "OralExamination.API.dll"]
