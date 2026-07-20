# ============================================
# ARISP Backend – Dockerfile (ASP.NET Core .NET 8)
# Multi-stage: development | build | production
# ============================================

# --- Stage: base ---
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS base
WORKDIR /app
EXPOSE 5000

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
# Non-root user for security
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser
ENTRYPOINT ["dotnet", "ARI.API.dll"]
