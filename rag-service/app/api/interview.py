"""Interview endpoints.

- POST /next-question  : SSE stream token câu hỏi (chạy LangGraph hybrid RAG).
- POST /analyze-answer : JSON.
- POST /evaluate       : JSON EvaluationReport.
- POST /detect-language: JSON.
- POST /assess-language: JSON.
- POST /complete-json  : generic JSON completion (fallback của .NET).
"""
from __future__ import annotations

import json

from fastapi import APIRouter
from fastapi.responses import StreamingResponse

from app.config import get_settings
from app.core.llm import complete_json as llm_complete_json
from app.rag.graph import get_interview_graph
from app.schemas import (
    AnswerAnalysis,
    AnswerContext,
    CompleteJsonRequest,
    DetectLanguageRequest,
    DetectLanguageResponse,
    EvaluationReport,
    LanguageAssessment,
    QuestionContext,
    SessionContext,
)
from app.services import interview_service as svc

router = APIRouter(tags=["interview"])


def _sse(data: str) -> bytes:
    return f"data: {data}\n\n".encode("utf-8")


async def _stream_question(ctx: QuestionContext):
    if get_settings().use_mock:
        async for token in svc.mock_question_tokens(ctx):
            yield _sse(json.dumps({"token": token}, ensure_ascii=False))
        yield _sse("[DONE]")
        return

    graph = get_interview_graph()
    # astream_events v2: bắt token từ node generate (ChatOpenAI streaming=True).
    async for event in graph.astream_events({"context": ctx}, version="v2"):
        if event["event"] == "on_chat_model_stream":
            chunk = event["data"]["chunk"]
            text = getattr(chunk, "content", "") or ""
            if text:
                yield _sse(json.dumps({"token": text}, ensure_ascii=False))
    yield _sse("[DONE]")


@router.post("/next-question")
async def next_question(ctx: QuestionContext):
    return StreamingResponse(
        _stream_question(ctx),
        media_type="text/event-stream",
        headers={"Cache-Control": "no-cache", "X-Accel-Buffering": "no"},
    )


@router.post("/analyze-answer", response_model=AnswerAnalysis)
async def analyze_answer(ctx: AnswerContext) -> AnswerAnalysis:
    return await svc.analyze_answer(ctx)


@router.post("/evaluate", response_model=EvaluationReport)
async def evaluate(ctx: SessionContext) -> EvaluationReport:
    return await svc.generate_evaluation(ctx)


@router.post("/assess-language", response_model=LanguageAssessment)
async def assess_language(ctx: SessionContext) -> LanguageAssessment:
    return await svc.assess_language(ctx)


@router.post("/detect-language", response_model=DetectLanguageResponse)
async def detect_language(req: DetectLanguageRequest) -> DetectLanguageResponse:
    return await svc.detect_language(req.jd_text)


@router.post("/complete-json")
async def complete_json(req: CompleteJsonRequest) -> dict:
    return await llm_complete_json(req.system_instruction, req.user_content)
