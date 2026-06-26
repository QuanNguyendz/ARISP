"""POST /ingest — chunk + embed + lưu document_chunks. Sở hữu ingestion (ADR-039 mở rộng)."""
from __future__ import annotations

from fastapi import APIRouter

from app.rag.ingest import ingest_document
from app.schemas import IngestRequest, IngestResponse

router = APIRouter(tags=["ingest"])


@router.post("/ingest", response_model=IngestResponse)
async def ingest(req: IngestRequest) -> IngestResponse:
    written = await ingest_document(
        source_type=req.source_type,
        source_id=req.source_id,
        text=req.text,
        scope=req.scope,
        document_type=req.document_type,
        replace_existing=req.replace_existing,
    )
    return IngestResponse(
        source_type=req.source_type,
        source_id=req.source_id,
        chunks_written=written,
    )
