"""Cấu hình service đọc từ biến môi trường (pydantic-settings).

Tái dùng các biến DATABASE_* / OPENAI_API_KEY / EMBEDDING_MODEL đã có trong docker/.env
của dự án (không thêm secret mới). Service KHÔNG dùng Supabase SDK — kết nối Postgres
trực tiếp qua asyncpg (ADR-002).
"""
from __future__ import annotations

from functools import lru_cache

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env", extra="ignore")

    # --- Database (Supabase Postgres) ---
    # Ưu tiên DATABASE_URL (DSN libpq) nếu có; nếu không, ghép từ các trường rời.
    database_url: str | None = None
    database_host: str = "localhost"
    database_port: int = 5432
    database_name: str = "postgres"
    database_user: str = "postgres"
    database_password: str = ""
    database_sslmode: str = "require"  # Supabase yêu cầu SSL; "disable" cho Postgres local

    # --- OpenAI ---
    openai_api_key: str | None = None
    embedding_model: str = "text-embedding-3-small"
    embedding_dim: int = 1536
    chat_model: str = "gpt-4o"
    json_model: str = "gpt-4o-mini"

    # --- RAG ---
    retrieve_top_k: int = 5
    # Hệ số trọng số theo scope khi fuse (ADR-025). Khóa = source_type/scope.
    rrf_k: int = 60  # hằng số Reciprocal Rank Fusion

    # --- Service ---
    app_env: str = "development"

    @property
    def use_mock(self) -> bool:
        """Không có OPENAI_API_KEY -> chạy mock (giống OpenAIProvider local mode)
        để dựng & test pipeline mà không cần key thật."""
        return not self.openai_api_key

    def dsn(self) -> str:
        """Trả về DSN libpq cho asyncpg."""
        if self.database_url:
            return self.database_url
        return (
            f"postgresql://{self.database_user}:{self.database_password}"
            f"@{self.database_host}:{self.database_port}/{self.database_name}"
        )


@lru_cache
def get_settings() -> Settings:
    return Settings()
