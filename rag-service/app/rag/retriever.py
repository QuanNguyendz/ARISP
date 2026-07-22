"""Hybrid retriever — Giai đoạn 1.

Kết hợp:
  - Dense: pgvector cosine distance `embedding <=> $qvec` (gần nhất).
  - Sparse: Postgres full-text với **OR-semantics** — chunk khớp BẤT KỲ từ khóa nào trong
    truy vấn (chuyển `&` của plainto_tsquery thành `|`), `ts_rank` xếp hạng theo độ trùng/độ
    hiếm của từ. Đây là cách chuẩn dùng FTS như sparse retriever: dense lo ngữ nghĩa (recall),
    sparse lo trùng từ hiếm/chính xác (precision); KHÔNG dùng AND vì sẽ loại nhầm chunk chỉ
    thiếu 1 từ.
Hợp nhất 2 danh sách bằng Reciprocal Rank Fusion (RRF), rồi nhân trọng số theo scope
(ADR-025): JD/CV và Job-Posting/Round Playbook = cao; Company Playbook = trung bình.

Index hỗ trợ: GIN trên `to_tsvector('simple', chunk_text)` (migration AddDocumentChunksFtsIndex).

Điểm mở rộng: Giai đoạn 2 (CRAG) sẽ thêm bước chấm điểm chunk; Giai đoạn 3 (Agentic) thêm
công cụ truy hồi khác. Hàm này giữ chữ ký ổn định để các node graph tái dùng.
"""
from __future__ import annotations

import json
from dataclasses import dataclass

from app.config import get_settings
from app.core import db
from app.core.embeddings import embed_text, to_pgvector

# Trọng số scope (ADR-025). playbook tinh chỉnh thêm theo metadata.scope bên dưới.
_BASE_WEIGHT = {"cv": 1.0, "jd": 1.0, "playbook": 0.8}
_PLAYBOOK_SCOPE_WEIGHT = {"company": 0.6, "job_posting": 1.0, "round": 1.0}


@dataclass(frozen=True)
class ScopeFilter:
    source_type: str
    source_id: str | None = None  # None = mọi id của loại đó (vd toàn bộ playbook)


@dataclass
class Candidate:
    id: str
    source_type: str
    source_id: str
    chunk_index: int
    chunk_text: str
    metadata: dict
    dense_rank: int | None = None
    sparse_rank: int | None = None
    score: float = 0.0


def _build_filter_clause(filters: list[ScopeFilter], start: int) -> tuple[str, list]:
    """Trả về (mệnh đề WHERE, params) với placeholder bắt đầu từ $start."""
    if not filters:
        return "TRUE", []
    parts: list[str] = []
    params: list = []
    i = start
    import uuid

    for f in filters:
        if f.source_id is None:
            parts.append(f"(source_type = ${i})")
            params.append(f.source_type)
            i += 1
        else:
            parts.append(f"(source_type = ${i} AND source_id = ${i + 1})")
            params.append(f.source_type)
            params.append(uuid.UUID(f.source_id))
            i += 2
    return "(" + " OR ".join(parts) + ")", params


def _scope_weight(c: Candidate) -> float:
    base = _BASE_WEIGHT.get(c.source_type, 0.8)
    if c.source_type == "playbook":
        scope = (c.metadata or {}).get("scope")
        base = _PLAYBOOK_SCOPE_WEIGHT.get(scope, base)
    return base


def _row_to_candidate(row) -> Candidate:
    meta = row["metadata"]
    if isinstance(meta, str):
        try:
            meta = json.loads(meta)
        except (ValueError, TypeError):
            meta = {}
    return Candidate(
        id=str(row["id"]),
        source_type=row["source_type"],
        source_id=str(row["source_id"]),
        chunk_index=row["chunk_index"],
        chunk_text=row["chunk_text"],
        metadata=meta or {},
    )


async def hybrid_retrieve(
    query: str,
    filters: list[ScopeFilter],
    top_k: int | None = None,
) -> list[Candidate]:
    settings = get_settings()
    top_k = top_k or settings.retrieve_top_k
    pool_size = max(top_k * 4, 20)

    qvec = to_pgvector(await embed_text(query))

    # --- Dense: $1 = qvec, filter bắt đầu từ $2 ---
    dense_clause, dense_params = _build_filter_clause(filters, start=2)
    dense_sql = f"""
        SELECT id, source_type, source_id, chunk_index, chunk_text, metadata
        FROM document_chunks
        WHERE {dense_clause}
        ORDER BY embedding <=> $1::vector
        LIMIT {pool_size}
    """

    # --- Sparse: $1 = query text, filter bắt đầu từ $2 ---
    # OR-semantics: plainto_tsquery sinh "a & b & c" (AND) -> đổi sang "a | b | c" (OR) để
    # chunk khớp bất kỳ từ nào vẫn là ứng viên; ts_rank tự xếp hạng theo độ trùng. tsq tính 1
    # lần trong CTE. plainto_tsquery đã sanitize input nên an toàn injection.
    sparse_clause, sparse_params = _build_filter_clause(filters, start=2)
    sparse_sql = f"""
        WITH q AS (
            SELECT to_tsquery(
                'simple',
                regexp_replace(plainto_tsquery('simple', $1)::text, '\\s*&\\s*', ' | ', 'g')
            ) AS tsq
        )
        SELECT id, source_type, source_id, chunk_index, chunk_text, metadata
        FROM document_chunks, q
        WHERE {sparse_clause}
          AND to_tsvector('simple', chunk_text) @@ q.tsq
        ORDER BY ts_rank(to_tsvector('simple', chunk_text), q.tsq) DESC
        LIMIT {pool_size}
    """

    async with db.acquire() as conn:
        dense_rows = await conn.fetch(dense_sql, qvec, *dense_params)
        sparse_rows = await conn.fetch(sparse_sql, query, *sparse_params)

    merged: dict[str, Candidate] = {}

    for rank, row in enumerate(dense_rows):
        c = _row_to_candidate(row)
        merged[c.id] = c
        c.dense_rank = rank

    for rank, row in enumerate(sparse_rows):
        cid = str(row["id"])
        if cid in merged:
            merged[cid].sparse_rank = rank
        else:
            c = _row_to_candidate(row)
            c.sparse_rank = rank
            merged[cid] = c

    # Reciprocal Rank Fusion + scope weighting.
    rrf_k = settings.rrf_k
    results: list[Candidate] = []
    for c in merged.values():
        rrf = 0.0
        if c.dense_rank is not None:
            rrf += 1.0 / (rrf_k + c.dense_rank + 1)
        if c.sparse_rank is not None:
            rrf += 1.0 / (rrf_k + c.sparse_rank + 1)
        c.score = rrf * _scope_weight(c)
        results.append(c)

    results.sort(key=lambda x: x.score, reverse=True)
    return results[:top_k]
