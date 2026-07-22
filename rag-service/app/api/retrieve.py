"""POST /retrieve — hybrid retrieval trả chunks đã xếp hạng."""
from __future__ import annotations

from fastapi import APIRouter

from app.core.embeddings import embed_text
from app.rag.retriever import ScopeFilter, hybrid_retrieve
from app.schemas import (
    EmbedRequest,
    EmbedResponse,
    RetrievedChunk,
    RetrieveRequest,
    RetrieveResponse,
)

router = APIRouter(tags=["retrieve"])


@router.post("/embed", response_model=EmbedResponse)
async def embed(req: EmbedRequest) -> EmbedResponse:
    return EmbedResponse(embedding=await embed_text(req.text))


@router.post("/retrieve", response_model=RetrieveResponse)
async def retrieve(req: RetrieveRequest) -> RetrieveResponse:
    # Dựng bộ lọc scope: nếu có source_types thì lọc theo từng loại (kèm source_id nếu có),
    # ngược lại không giới hạn loại.
    if req.source_types:
        filters = [ScopeFilter(st, req.source_id) for st in req.source_types]
    elif req.source_id:
        filters = [ScopeFilter(st, req.source_id) for st in ("cv", "jd", "playbook")]
    else:
        filters = []

    candidates = await hybrid_retrieve(req.query, filters, top_k=req.top_k)
    return RetrieveResponse(
        chunks=[
            RetrievedChunk(
                id=c.id,
                source_type=c.source_type,
                source_id=c.source_id,
                chunk_index=c.chunk_index,
                chunk_text=c.chunk_text,
                score=round(c.score, 6),
            )
            for c in candidates
        ]
    )
