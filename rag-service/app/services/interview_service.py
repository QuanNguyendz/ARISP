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
    if ctx.force_closing:
        text = (
            "[END_INTERVIEW] Cảm ơn bạn đã dành thời gian tham gia buổi phỏng vấn hôm nay. "
            "Kết quả sẽ được gửi tới bạn sớm. Chúc bạn một ngày tốt lành!"
        )
    elif ctx.must_ask_questions:
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


def _num(value, default: float = 0.0) -> float:
    try:
        return float(value)
    except (TypeError, ValueError):
        return default


def normalize_question_analyses(raw, fallback_sequences: list[int] | None = None) -> list[dict]:
    """Chuẩn hoá phân tích từng câu về đúng khoá .NET đọc được (PascalCase).

    LLM hay đổi tên khoá (`sequence_number` / `questionNumber` / `assessment` / `suggestion`…) —
    trước đây .NET parse hụt nên mục "phân tích từng câu" hiện trống. Ở đây quy về một dạng duy
    nhất và bỏ các mục rỗng; câu hỏi/câu trả lời KHÔNG chép lại (client đã có bản gốc từ DB).
    """
    if not isinstance(raw, list):
        return []
    sequences = fallback_sequences or []
    items: list[dict] = []
    for idx, entry in enumerate(raw):
        if not isinstance(entry, dict):
            continue
        seq = entry.get("sequence_number", entry.get("sequenceNumber",
              entry.get("question_number", entry.get("questionNumber", entry.get("index")))))
        seq_int = int(_num(seq, 0))
        if seq_int <= 0:
            seq_int = sequences[idx] if idx < len(sequences) else idx + 1
        analysis = str(entry.get("analysis", entry.get("assessment", "")) or "").strip()
        feedback = str(entry.get("feedback", entry.get("suggestion", "")) or "").strip()
        raw_score = next(
            (entry[k] for k in ("score", "Score", "rating", "points", "overall_score") if k in entry),
            None,
        )
        score = _num(raw_score) if raw_score is not None else 0.0
        if not analysis and not feedback and score <= 0:
            continue
        items.append({
            "SequenceNumber": seq_int,
            # Điểm 0 = "model không chấm", KHÔNG phải "câu trả lời kém" — .NET/FE hiểu 0 là chưa có
            # điểm và ẩn chip thay vì tô đỏ "Cần cải thiện".
            "Score": max(0.0, min(100.0, score)),
            "Analysis": analysis,
            "Feedback": feedback,
        })
    return items


async def generate_evaluation(ctx: SessionContext) -> EvaluationReport:
    if get_settings().use_mock:
        return EvaluationReport(
            verdict="pass",
            score=85.5,
            reasoning="Ứng viên thể hiện năng lực kỹ thuật tốt, giải quyết vấn đề thực tế hợp lý.",
            recommended_next_step="Mời tham gia vòng Technical Deep-dive.",
            criterion_scores_json='{"technical":88,"communication":82,"culture_fit":85}',
            question_analyses_json=json.dumps(
                normalize_question_analyses(
                    [
                        {
                            "sequence_number": qa.sequence_number or i + 1,
                            "score": 85,
                            "analysis": "(mock) Câu trả lời bám sát yêu cầu công việc, có ví dụ cụ thể.",
                            "feedback": "(mock) Bổ sung số liệu đo lường kết quả để thuyết phục hơn.",
                        }
                        for i, qa in enumerate(ctx.chat_history)
                        if (qa.answer_text or "").strip()
                    ]
                ),
                ensure_ascii=False,
            ),
        )
    system, user = evaluate_prompt(ctx)
    data = await complete_json(system, user)
    answered_sequences = [
        qa.sequence_number for qa in ctx.chat_history if (qa.answer_text or "").strip()
    ]
    return EvaluationReport(
        verdict=str(data.get("verdict", "not_pass")),
        score=_num(data.get("score", 0)),
        reasoning=str(data.get("reasoning", "")),
        recommended_next_step=str(data.get("recommended_next_step", "")),
        criterion_scores_json=json.dumps(data.get("criterion_scores", {}), ensure_ascii=False),
        question_analyses_json=json.dumps(
            normalize_question_analyses(data.get("question_analyses", []), answered_sequences),
            ensure_ascii=False,
        ),
    )


async def assess_language(ctx: SessionContext) -> LanguageAssessment:
    if get_settings().use_mock:
        return LanguageAssessment(
            fluency=8.0, grammar=7.5, vocabulary=8.0, comprehension=8.5, overall_score=8.0,
            cefr_level="B2",
            language_adherence="Ứng viên trả lời nhất quán bằng ngôn ngữ phỏng vấn yêu cầu.",
            evidence="(mock) trích dẫn minh hoạ từ câu trả lời của ứng viên.",
        )
    system, user = assess_language_prompt(ctx)
    data = await complete_json(system, user)
    overall = float(data.get("overall_score", 0))
    return LanguageAssessment(
        fluency=float(data.get("fluency", 0)),
        grammar=float(data.get("grammar", 0)),
        vocabulary=float(data.get("vocabulary", 0)),
        comprehension=float(data.get("comprehension", 0)),
        overall_score=overall,
        # LLM quên/ghi sai bậc → suy lại từ overall_score theo đúng neo trong prompt,
        # tránh trường hợp điểm thành phần 7-8 mà bậc lại rơi về A2.
        cefr_level=str(data.get("cefr_level", "")).strip().upper() or cefr_from_score(overall),
        language_adherence=str(data.get("language_adherence", "")),
        evidence=str(data.get("evidence", "")),
    )


def cefr_from_score(overall: float) -> str:
    """Neo bậc CEFR theo thang 0-10 dùng trong assess_language_prompt."""
    if overall >= 9:
        return "C1"
    if overall >= 7:
        return "B2"
    if overall >= 5:
        return "B1"
    if overall >= 3:
        return "A2"
    return "A1"


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
