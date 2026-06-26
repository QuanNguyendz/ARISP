"""Embedding provider — OpenAI text-embedding-3-small (1536 chiều).

Khớp model/đa chiều với cột pgvector(1536) hiện có để tương thích các vector đã/embedding
do .NET sinh trước đây. Có mock mode (không cần API key) giống OpenAIProvider.local của .NET.
"""
from __future__ import annotations

import hashlib
import random

from app.config import get_settings

_embedder = None


def _get_embedder():
    global _embedder
    if _embedder is None:
        from langchain_openai import OpenAIEmbeddings  # import trễ để mock mode khỏi cần package nặng

        settings = get_settings()
        _embedder = OpenAIEmbeddings(
            model=settings.embedding_model,
            api_key=settings.openai_api_key,
        )
    return _embedder


def _mock_vector(text: str) -> list[float]:
    """Vector giả định tất định theo hash của text (mirror OpenAIProvider mock của .NET)."""
    settings = get_settings()
    seed = int(hashlib.sha256(text.encode("utf-8")).hexdigest(), 16) % (2**32)
    rnd = random.Random(seed)
    return [rnd.random() for _ in range(settings.embedding_dim)]


async def embed_text(text: str) -> list[float]:
    settings = get_settings()
    if settings.use_mock:
        return _mock_vector(text)
    return await _get_embedder().aembed_query(text)


async def embed_texts(texts: list[str]) -> list[list[float]]:
    settings = get_settings()
    if settings.use_mock:
        return [_mock_vector(t) for t in texts]
    return await _get_embedder().aembed_documents(texts)


def to_pgvector(vector: list[float]) -> str:
    """Chuỗi biểu diễn pgvector: [0.1,0.2,...] — khớp định dạng .NET dùng."""
    return "[" + ",".join(repr(float(x)) for x in vector) + "]"
