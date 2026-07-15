"""Prompt builders — language-aware (ADR-018). Tách riêng để dễ chỉnh và test."""
from __future__ import annotations

from app.schemas import QuestionContext, SessionContext

_LANG_NAME = {
    "vi": "Vietnamese (Tiếng Việt)",
    "en": "English",
    "ja": "Japanese (日本語)",
    "ko": "Korean (한국어)",
}


def language_name(code: str | None) -> str:
    if not code:
        return "Vietnamese (Tiếng Việt)"
    return _LANG_NAME.get(code, code)


END_MARKER = "[END_INTERVIEW]"
# Không cho kết thúc quá sớm — dưới ngưỡng này model không được phép chào tạm biệt.
MIN_QUESTIONS_BEFORE_END = 5


def question_system_prompt(ctx: QuestionContext, retrieved: list[str]) -> str:
    lang = language_name(ctx.language)

    # Buộc đóng phiên (cap số câu phía .NET): chỉ sinh lời cảm ơn, không hỏi thêm.
    if ctx.force_closing:
        return (
            "You are a professional HR and Technical AI Interviewer wrapping up an interview. "
            f"The interview must END NOW. Output exactly the marker {END_MARKER} followed by a short, "
            f"warm farewell in {lang}: thank the candidate for their time, mention that the results "
            "will be shared soon. 2-3 sentences. Do NOT ask any question."
        )

    sys = (
        "You are a professional HR and Technical AI Interviewer. "
        "Generate ONE suitable, concise interview question based on the Job Description, "
        "the Candidate CV, the retrieved context, and the current chat history. "
        "Adjust difficulty adaptively to the quality of previous answers. Be polite and concise. "
        f"Ask the question in {lang}. Return ONLY the question text, no preamble."
        # Kiểm soát ngôn ngữ (ADR-018): ứng viên nói sai ngôn ngữ → nhắc ngay trong câu kế.
        f"\n\nLANGUAGE RULE: The interview MUST be conducted in {lang}. "
        f"If the candidate's most recent answer is clearly written in a different language, "
        f"START your output with ONE short, polite reminder (in {lang}) asking them to answer "
        f"in {lang}, then continue with the next question in the same output."
    )
    # AI chủ động kết thúc khi đã đủ độ bao phủ — kèm marker để .NET nhận diện.
    asked = len(ctx.chat_history)
    if asked >= MIN_QUESTIONS_BEFORE_END and not ctx.must_ask_questions:
        sys += (
            f"\n\nENDING RULE: You have asked {asked} questions so far. If the conversation has "
            "already covered the key competencies of the Job Description (technical depth, "
            "experience, soft skills) OR the candidate has clearly stopped engaging (repeated "
            "refusals / empty answers), END the interview instead of asking another question: "
            f"output exactly the marker {END_MARKER} followed by a short, warm farewell in {lang} "
            "thanking the candidate and mentioning that results will be shared soon. "
            "Do not end before covering the essentials — when in doubt, keep interviewing."
        )
    if ctx.playbook_style_guides:
        sys += "\n\nAdhere to the company interview playbook style:\n" + "\n".join(
            ctx.playbook_style_guides
        )
    if retrieved:
        sys += "\n\nRetrieved context (most relevant chunks):\n" + "\n".join(
            f"- {c}" for c in retrieved
        )
    if ctx.must_ask_questions:
        sys += (
            "\n\nYou MUST ask the following mandatory question now (rephrase naturally, keep its intent):\n"
            f"{ctx.must_ask_questions[0]}"
        )
    return sys


def question_user_prompt(ctx: QuestionContext) -> str:
    parts = [
        f"Job Description:\n{ctx.job_description[:4000]}",
        f"Candidate CV:\n{ctx.candidate_cv[:4000]}",
    ]
    return "\n\n".join(parts)


def analyze_prompt(question_text: str, answer: str) -> tuple[str, str]:
    system = (
        "You analyze a candidate's interview answer for accuracy and communication quality. "
        'Return JSON only: {"difficulty_level": <int 1-5>, "feedback": "<short feedback>"}. '
        "difficulty_level is the recommended difficulty for the NEXT question given how well this was answered."
    )
    user = f"Question: {question_text}\nAnswer: {answer}"
    return system, user


def evaluate_prompt(ctx: SessionContext) -> tuple[str, str]:
    import json

    lang = language_name(ctx.language)
    system = (
        "You are an HR director evaluating a full interview session. "
        "Evaluate strictly against the scoring rubric. Return JSON only with keys: "
        '{"verdict": "pass"|"not_pass", "score": <0-100 number>, "reasoning": "<text>", '
        '"recommended_next_step": "<text>", "criterion_scores": {<criterion>: <0-100>}, '
        '"question_analyses": [<optional objects>]}. '
        f"The interview was required to be conducted in {lang}; if the candidate repeatedly "
        "answered in a different language, note this explicitly in the reasoning."
    )
    history = json.dumps([qa.model_dump(by_alias=True) for qa in ctx.chat_history], ensure_ascii=False)
    user = (
        f"Job Description:\n{ctx.job_description[:4000]}\n\n"
        f"Candidate CV:\n{ctx.candidate_cv[:4000]}\n\n"
        f"Scoring Rubric:\n{ctx.scoring_rubric}\n\n"
        f"QA History:\n{history}"
    )
    return system, user


def assess_language_prompt(ctx: SessionContext) -> tuple[str, str]:
    import json

    lang = language_name(ctx.language)
    system = (
        f"Assess the candidate's proficiency in {lang} from the conversation. "
        "Return JSON only: "
        '{"fluency": <0-10>, "grammar": <0-10>, "vocabulary": <0-10>, '
        '"comprehension": <0-10>, "overall_score": <0-10>, '
        f'"language_adherence": "<one short sentence: did the candidate consistently answer in {lang}? '
        'If not, which language did they use and how often>"}.'
    )
    history = json.dumps([qa.model_dump(by_alias=True) for qa in ctx.chat_history], ensure_ascii=False)
    user = f"Required interview language: {lang}\n\nConversation history:\n{history}"
    return system, user


def detect_language_prompt(jd_text: str) -> tuple[str, str]:
    system = (
        "Detect if the job description requires a primary foreign language (e.g. English, Japanese, Korean) "
        'for the interview. Return JSON only: {"language": "<ISO code like en/ja/ko/vi>"}. '
        "Default to 'vi' if no clear foreign-language requirement."
    )
    return system, jd_text[:6000]
