"""Logic nghiệp vụ phỏng vấn — gọi LangGraph + LLM, có mock mode để chạy không cần API key.

Tách khỏi router để dễ test. Streaming câu hỏi nằm ở router (cần SSE), phần JSON ở đây.
"""
from __future__ import annotations

import asyncio
import json

from app.config import get_settings
from app.core.llm import complete_json
from app.prompts import (
    analyze_prompt,
    assess_language_prompt,
    detect_language_prompt,
    evaluate_prompt,
)
from app.schemas import (
    AnswerAnalysis,
    AnswerContext,
    DetectLanguageResponse,
    EvaluationReport,
    LanguageAssessment,
    QuestionContext,
    SessionContext,
)

_MOCK_QUESTIONS = [
    "Bạn có thể giới thiệu bản thân và kinh nghiệm phát triển nổi bật nhất của mình không?",
    "Trong CV bạn đề cập làm việc với cơ sở dữ liệu — bạn tối ưu các truy vấn chậm như thế nào?",
    "Bạn xử lý thế nào khi bất đồng quan điểm thiết kế hệ thống với Technical Lead?",
    "Mô hình kiến trúc bạn thường dùng mang lại lợi ích và khó khăn gì trong dự án thực tế?",
    "Hãy kể một sự cố nghiêm trọng trên production bạn từng gặp và cách bạn debug, xử lý.",
]


async def mock_question_tokens(ctx: QuestionContext):
    """Sinh token mock (mirror OpenAIProvider.local) để stream khi không có API key."""
    if ctx.must_ask_questions:
        text = f"[Must Ask] {ctx.must_ask_questions[0]}"
    else:
        idx = len(ctx.chat_history) % len(_MOCK_QUESTIONS)
        text = _MOCK_QUESTIONS[idx]
    for word in text.split(" "):
        yield word + " "
        await asyncio.sleep(0.02)


async def analyze_answer(ctx: AnswerContext) -> AnswerAnalysis:
    if get_settings().use_mock:
        return AnswerAnalysis(
            difficulty_level=3,
            feedback="Câu trả lời có cấu trúc tốt, thể hiện kiến thức cơ bản vững.",
        )
    system, user = analyze_prompt(ctx.question_text, ctx.answer_transcript)
    data = await complete_json(system, user)
    return AnswerAnalysis(
        difficulty_level=int(data.get("difficulty_level", 3)),
        feedback=str(data.get("feedback", "")),
    )


async def generate_evaluation(ctx: SessionContext) -> EvaluationReport:
    if get_settings().use_mock:
        return EvaluationReport(
            verdict="pass",
            score=85.5,
            reasoning="Ứng viên thể hiện năng lực kỹ thuật tốt, giải quyết vấn đề thực tế hợp lý.",
            recommended_next_step="Mời tham gia vòng Technical Deep-dive.",
            criterion_scores_json='{"technical":88,"communication":82,"culture_fit":85}',
            question_analyses_json="[]",
        )
    system, user = evaluate_prompt(ctx)
    data = await complete_json(system, user)
    return EvaluationReport(
        verdict=str(data.get("verdict", "not_pass")),
        score=float(data.get("score", 0)),
        reasoning=str(data.get("reasoning", "")),
        recommended_next_step=str(data.get("recommended_next_step", "")),
        criterion_scores_json=json.dumps(data.get("criterion_scores", {}), ensure_ascii=False),
        question_analyses_json=json.dumps(data.get("question_analyses", []), ensure_ascii=False),
    )


async def assess_language(ctx: SessionContext) -> LanguageAssessment:
    if get_settings().use_mock:
        return LanguageAssessment(
            fluency=8.0, grammar=7.5, vocabulary=8.0, comprehension=8.5, overall_score=8.0
        )
    system, user = assess_language_prompt(ctx)
    data = await complete_json(system, user)
    return LanguageAssessment(
        fluency=float(data.get("fluency", 0)),
        grammar=float(data.get("grammar", 0)),
        vocabulary=float(data.get("vocabulary", 0)),
        comprehension=float(data.get("comprehension", 0)),
        overall_score=float(data.get("overall_score", 0)),
    )


async def detect_language(jd_text: str) -> DetectLanguageResponse:
    if get_settings().use_mock:
        low = jd_text.lower()
        if "english" in low or "tiếng anh" in low:
            return DetectLanguageResponse(language="en")
        if "japanese" in low or "tiếng nhật" in low:
            return DetectLanguageResponse(language="ja")
        return DetectLanguageResponse(language="vi")
    system, user = detect_language_prompt(jd_text)
    data = await complete_json(system, user)
    return DetectLanguageResponse(language=str(data.get("language", "vi")))
