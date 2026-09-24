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

# LibreOffice headless — dựng bản PDF để XEM TRƯỚC CV .doc/.docx.
#
# Vì sao phải có một bộ văn phòng đầy đủ trong image API: không thư viện .NET nào dựng được bố cục
# Word cho tài liệu bất kỳ. OpenXML đọc được cấu trúc nhưng không biết chữ rơi xuống đâu trên trang,
# mà bố cục chính là phần hỏng — mẫu CV hiện đại dùng khung nổi và chia cột, bộ dựng phía trình duyệt
# xếp sai chỗ nên khối nền đè lên chữ.
#
# `--no-install-recommends` + chỉ `libreoffice-writer` (KHÔNG phải gói `libreoffice` đầy đủ): bỏ được
# Calc, Impress, Draw và Java. `fonts-liberation` thay cho font Microsoft — thiếu font thì LibreOffice
# thay bằng font khác và mọi khoảng cách chữ lệch đi.
#
# Thiếu gói này thì API VẪN CHẠY: bộ chuyển đổi báo không dùng được và giao diện lùi về bộ dựng phía
# trình duyệt. Đây là tính năng cộng thêm, không phải phụ thuộc bắt buộc.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libreoffice-writer fonts-liberation \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
# Program.cs:40 gọi Directory.CreateDirectory("uploads") vô điều kiện lúc khởi động.
# /app thuộc root sau COPY nên user không đặc quyền sẽ ném UnauthorizedAccessException
# → tạo sẵn thư mục và trao quyền, thay vì phải chạy container bằng root.
# `app` là user non-root (UID 1654) có sẵn trong image chính thức của .NET 8.
RUN mkdir -p /app/uploads && chown -R app:app /app/uploads

# LibreOffice ghi hồ sơ người dùng vào HOME; user `app` không có HOME ghi được nên tiến trình thoát
# lặng lẽ. Bộ chuyển đổi đã trỏ `-env:UserInstallation` vào /tmp, nhưng vẫn cần HOME hợp lệ cho các
# file tạm khác.
ENV HOME=/tmp
USER app
ENTRYPOINT ["dotnet", "ARI.API.dll"]
