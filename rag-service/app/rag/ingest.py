"""Ingestion: chunk -> embed -> ghi vào document_chunks (pgvector).

Sở hữu toàn bộ vòng đời ingest (ADR-039 mở rộng). Idempotent theo (source_type, source_id):
mặc định xoá chunk cũ trước khi ghi để tránh trùng khi re-ingest.
"""
from __future__ import annotations

import json
import uuid
from datetime import datetime, timezone

from app.core import db
from app.core.embeddings import embed_texts, to_pgvector
from app.rag.chunker import chunk_text


async def ingest_document(
    source_type: str,
    source_id: str,
    text: str,
    scope: str | None = None,
    document_type: str | None = None,
    replace_existing: bool = True,
) -> int:
    """Trả về số chunk đã ghi."""
    chunks = chunk_text(text)
    if not chunks:
        # Vẫn dọn chunk cũ nếu replace_existing để phản ánh "tài liệu rỗng".
        if replace_existing:
            async with db.acquire() as conn:
                await conn.execute(
                    "DELETE FROM document_chunks WHERE source_type = $1 AND source_id = $2",
                    source_type,
                    uuid.UUID(source_id),
                )
        return 0

    vectors = await embed_texts(chunks)
    metadata = json.dumps({"scope": scope, "document_type": document_type})
    now = datetime.now(timezone.utc)
    src_uuid = uuid.UUID(source_id)

    async with db.acquire() as conn:
        async with conn.transaction():
            if replace_existing:
                await conn.execute(
                    "DELETE FROM document_chunks WHERE source_type = $1 AND source_id = $2",
                    source_type,
                    src_uuid,
                )
            records = [
                (
                    uuid.uuid4(),
                    source_type,
                    src_uuid,
                    idx,
                    chunk,
                    to_pgvector(vec),
                    metadata,
                    now,
                )
                for idx, (chunk, vec) in enumerate(zip(chunks, vectors))
            ]
            # executemany với cast ::vector / ::jsonb trong câu lệnh.
            await conn.executemany(
                """
                INSERT INTO document_chunks
                    (id, source_type, source_id, chunk_index, chunk_text, embedding, metadata, created_at)
                VALUES ($1, $2, $3, $4, $5, $6::vector, $7::jsonb, $8)
                """,
                records,
            )
    return len(chunks)
