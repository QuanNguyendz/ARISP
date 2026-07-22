# ============================================
# ARISP RAG Service – Python 3.11 (FastAPI + LangChain + LangGraph)
# Multi-stage: base -> development | production
# Build context: ../rag-service
# ============================================

FROM python:3.11-slim AS base
ENV PYTHONUNBUFFERED=1 \
    PYTHONDONTWRITEBYTECODE=1 \
    PIP_NO_CACHE_DIR=1
WORKDIR /app
EXPOSE 8000

# --- Development: cài deps + dev tools, chạy --reload (source mount qua compose) ---
FROM base AS development
COPY . .
RUN pip install --upgrade pip && pip install -e ".[dev]"
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--reload"]

# --- Production: cài deps (không dev), chạy uvicorn workers ---
FROM base AS production
COPY . .
RUN pip install --upgrade pip && pip install . \
    && adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000", "--workers", "2"]
