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
COPY ARISP.API/*.csproj ./ARISP.API/
COPY ARISP.Application/*.csproj ./ARISP.Application/
COPY ARISP.Domain/*.csproj ./ARISP.Domain/
COPY ARISP.Infrastructure/*.csproj ./ARISP.Infrastructure/
RUN dotnet restore
# Copy everything else
COPY . .
ENTRYPOINT ["dotnet", "watch", "run", "--project", "ARISP.API", "--urls", "http://+:5000"]

# --- Stage: build ---
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY *.sln ./
COPY ARISP.API/*.csproj ./ARISP.API/
COPY ARISP.Application/*.csproj ./ARISP.Application/
COPY ARISP.Domain/*.csproj ./ARISP.Domain/
COPY ARISP.Infrastructure/*.csproj ./ARISP.Infrastructure/
RUN dotnet restore
COPY . .
RUN dotnet publish ARISP.API -c Release -o /app/publish --no-restore

# --- Stage: production ---
FROM base AS production
WORKDIR /app
COPY --from=build /app/publish .
# Non-root user for security
RUN addgroup -S appgroup && adduser -S appuser -G appgroup
USER appuser
ENTRYPOINT ["dotnet", "ARISP.API.dll"]
