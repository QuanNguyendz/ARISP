# ============================================
# ARISP Backend – Dockerfile (ASP.NET Core .NET 8)
# Multi-stage: development | build | production
# ============================================

# --- Stage: base ---
# Dùng bản Debian (KHÔNG -alpine): alpine thiếu ICU đầy đủ nên phần parse tài liệu
# (DocumentParserService) và so khớp ngôn ngữ theo culture chạy sai trên prod.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 5000
ENV ASPNETCORE_HTTP_PORTS=5000

# --- Stage: development (hot-reload with dotnet watch) ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS development
WORKDIR /app/src
# Restore dependencies first (cache layer)
COPY *.sln ./
COPY src/ARI.API/*.csproj ./src/ARI.API/
COPY src/ARI.Application/*.csproj ./src/ARI.Application/
COPY src/ARI.Domain/*.csproj ./src/ARI.Domain/
COPY src/ARI.Infrastructure/*.csproj ./src/ARI.Infrastructure/
RUN dotnet restore src/ARI.API/ARI.API.csproj
# Copy everything else
COPY . .
ENTRYPOINT ["dotnet", "watch", "run", "--project", "src/ARI.API", "--urls", "http://+:5000"]

# --- Stage: build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.sln ./
COPY src/ARI.API/*.csproj ./src/ARI.API/
COPY src/ARI.Application/*.csproj ./src/ARI.Application/
COPY src/ARI.Domain/*.csproj ./src/ARI.Domain/
COPY src/ARI.Infrastructure/*.csproj ./src/ARI.Infrastructure/
RUN dotnet restore src/ARI.API/ARI.API.csproj
COPY . .
RUN dotnet publish src/ARI.API -c Release -o /app/publish --no-restore

# --- Stage: production ---
FROM base AS production
WORKDIR /app
COPY --from=build /app/publish .
# Program.cs:40 gọi Directory.CreateDirectory("uploads") vô điều kiện lúc khởi động.
# /app thuộc root sau COPY nên user không đặc quyền sẽ ném UnauthorizedAccessException
# → tạo sẵn thư mục và trao quyền, thay vì phải chạy container bằng root.
# `app` là user non-root (UID 1654) có sẵn trong image chính thức của .NET 8.
RUN mkdir -p /app/uploads && chown -R app:app /app/uploads
USER app
ENTRYPOINT ["dotnet", "ARI.API.dll"]
