"""ARISP RAG Interview Microservice — FastAPI app.

Nội bộ (không expose qua Nginx, ADR-039). Backend .NET gọi qua http://rag-service:8000.
"""
from __future__ import annotations

from contextlib import asynccontextmanager

from fastapi import FastAPI

from app.api import ingest, interview, retrieve
from app.config import get_settings
from app.core import db


@asynccontextmanager
async def lifespan(app: FastAPI):
    await db.init_pool()
    try:
        yield
    finally:
        await db.close_pool()


app = FastAPI(
    title="ARISP RAG Interview Service",
    version="0.1.0",
    description="Hybrid RAG (Giai đoạn 1) — embed, hybrid retrieve, sinh câu hỏi & đánh giá phỏng vấn.",
    lifespan=lifespan,
)

app.include_router(ingest.router)
app.include_router(retrieve.router)
app.include_router(interview.router)


@app.get("/health", tags=["health"])
async def health() -> dict:
    settings = get_settings()
    return {
        "status": "ok",
        "env": settings.app_env,
        "mock_mode": settings.use_mock,
        "db": await db.healthcheck(),
        "embedding_model": settings.embedding_model,
        "chat_model": settings.chat_model,
    }
