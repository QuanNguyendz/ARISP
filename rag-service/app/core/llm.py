"""LLM provider — OpenAI GPT-4o (streaming) cho sinh câu hỏi; gpt-4o-mini cho JSON.

Bao qua langchain-openai ChatOpenAI. Mock mode (không API key) trả câu trả lời canned để
test pipeline end-to-end mà không tốn token (giống OpenAIProvider.local của .NET).
"""
from __future__ import annotations

import json

from app.config import get_settings

_chat = None
_json_chat = None


def get_chat_model():
    """ChatOpenAI streaming dùng cho node generate của LangGraph."""
    global _chat
    if _chat is None:
        from langchain_openai import ChatOpenAI

        settings = get_settings()
        _chat = ChatOpenAI(
            model=settings.chat_model,
            api_key=settings.openai_api_key,
            streaming=True,
            temperature=0.6,
        )
    return _chat


def get_json_model():
    """ChatOpenAI ép trả JSON object — cho analyze/evaluate/detect/assess."""
    global _json_chat
    if _json_chat is None:
        from langchain_openai import ChatOpenAI

        settings = get_settings()
        _json_chat = ChatOpenAI(
            model=settings.json_model,
            api_key=settings.openai_api_key,
            temperature=0.2,
            model_kwargs={"response_format": {"type": "json_object"}},
        )
    return _json_chat


async def complete_json(system_instruction: str, user_content: str) -> dict:
    """Gọi 1 lần, trả về dict đã parse từ JSON. Raise nếu mock (caller tự lo fallback)."""
    settings = get_settings()
    if settings.use_mock:
        raise RuntimeError("LLM ở mock mode (thiếu OPENAI_API_KEY).")

    from langchain_core.messages import HumanMessage, SystemMessage

    resp = await get_json_model().ainvoke(
        [SystemMessage(content=system_instruction), HumanMessage(content=user_content)]
    )
    return json.loads(resp.content)
