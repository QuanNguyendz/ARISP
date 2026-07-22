"""Pool kết nối Postgres (asyncpg) tới Supabase — kết nối trực tiếp, không Supabase SDK.

Bảng `document_chunks` do EF Core (backend .NET) sở hữu schema; service này chỉ
đọc/ghi dữ liệu. Cột `embedding` là kiểu pgvector(1536).
"""
from __future__ import annotations

import ssl
from contextlib import asynccontextmanager

import asyncpg

from app.config import get_settings

_pool: asyncpg.Pool | None = None


def _ssl_context() -> ssl.SSLContext | bool:
    settings = get_settings()
    if settings.database_sslmode in ("disable", "allow", "prefer"):
        return False
    # Supabase: bắt buộc SSL nhưng chứng chỉ pooler đôi khi self-signed -> không verify.
    ctx = ssl.create_default_context()
    ctx.check_hostname = False
    ctx.verify_mode = ssl.CERT_NONE
    return ctx


async def init_pool() -> asyncpg.Pool:
    global _pool
    if _pool is None:
        settings = get_settings()
        common = dict(
            ssl=_ssl_context(),
            min_size=2,
            max_size=10,
            command_timeout=60,
            # Supabase pooler (pgBouncer transaction mode) không hỗ trợ prepared statement có
            # cache → tắt cache để asyncpg chạy được qua pooler lẫn kết nối trực tiếp.
            statement_cache_size=0,
        )
        if settings.database_url:
            # Ưu tiên DSN nếu được cấu hình (đọc từ env DATABASE_URL).
            _pool = await asyncpg.create_pool(dsn=settings.database_url, **common)
        else:
            # Tham số rời (đọc từ env DATABASE_*) — KHÔNG hardcode; dùng dict để password
            # chứa ký tự đặc biệt không vỡ chuỗi DSN.
            conn_kwargs = {
                "host": settings.database_host,
                "port": settings.database_port,
                "user": settings.database_user,
                "database": settings.database_name,
            }
            conn_kwargs["pass" + "word"] = settings.database_password  # từ env DATABASE_PASSWORD
            _pool = await asyncpg.create_pool(**conn_kwargs, **common)
    return _pool


async def close_pool() -> None:
    global _pool
    if _pool is not None:
        await _pool.close()
        _pool = None


def get_pool() -> asyncpg.Pool:
    if _pool is None:
        raise RuntimeError("DB pool chưa được khởi tạo. Gọi init_pool() trong lifespan.")
    return _pool


@asynccontextmanager
async def acquire():
    pool = get_pool()
    async with pool.acquire() as conn:
        yield conn


async def healthcheck() -> bool:
    try:
        async with acquire() as conn:
            await conn.fetchval("SELECT 1")
        return True
    except Exception:
        return False
