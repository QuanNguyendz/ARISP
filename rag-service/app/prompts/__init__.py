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


def question_system_prompt(
    ctx: QuestionContext,
    retrieved: list[str],
    *,
    banned_topics: list[str] | None = None,
    red_flags: list[str] | None = None,
    expected_answers: list[str] | None = None,
) -> str:
    """Prompt sinh cau hoi.

    Playbook khong phai mot ro tai lieu dong hang: `compliance` la chu de CAM hoi, `red_flag` la dau
    hieu can dao sau, `expected_answer` dung de cham chu khong doc cho ung vien nghe (ADR-025).
    Nhet chung vao "retrieved context" nhu ban cu chinh la dua cho mo hinh danh sach chu de nhay cam
    roi mong no tu hieu la khong duoc hoi.
    """
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
    if expected_answers:
        sys += (
            "\n\nWhat a strong answer covers (use this to judge and to probe deeper - "
            "NEVER read it out or hint at it):\n"
            + "\n".join(f"- {c}" for c in expected_answers)
        )
    if red_flags:
        sys += (
            "\n\nWarning signs - if the candidate shows any of these, probe deeper before moving on:\n"
            + "\n".join(f"- {c}" for c in red_flags)
        )
    # Rang buoc CAM dat SAU moi ngu canh de khong bi cac doan phia tren pha loang.
    combined_banned = list(banned_topics or []) + list(ctx.prohibited_topics or [])
    if combined_banned:
        sys += (
            "\n\nHARD CONSTRAINT - NEVER ask about, hint at, or invite the candidate to discuss any "
            "of the following topics (company compliance playbook). If the candidate raises one, "
            "acknowledge briefly and move on without probing:\n"
            + "\n".join(f"- {c}" for c in combined_banned)
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


def report_language_name(ctx: SessionContext) -> str:
    """Ngôn ngữ VIẾT báo cáo — mặc định theo ngôn ngữ phỏng vấn nếu .NET không truyền."""
    return language_name(ctx.report_language or ctx.language)


def evaluate_prompt(ctx: SessionContext) -> tuple[str, str]:
    import json

    lang = language_name(ctx.language)
    report_lang = report_language_name(ctx)
    system = (
        "You are an HR director evaluating a full interview session. "
        "Evaluate strictly against the scoring rubric. Return JSON only with keys: "
        '{"verdict": "pass"|"not_pass", "score": <0-100 number>, "reasoning": "<text>", '
        '"recommended_next_step": "<text>", "criterion_scores": {<criterion_key>: <0-100>}, '
        # Khoá tiêu chí phải nằm trong bộ cố định: FE dịch khoá sang VI/EN, model tự đặt tên
        # ("Cultural Fit", "Technical Skills") sẽ lọt ra màn hình dưới dạng tiếng Anh thô.
        "criterion_scores keys MUST be chosen ONLY from this fixed snake_case list: "
        "technical, communication, problem_solving, culture_fit, experience, language, attitude, teamwork. "
        "Use 3-6 of them, never invent other keys and never use display names. "
        '"question_analyses": [{"sequence_number": <int, from QA History>, "score": <0-100>, '
        '"analysis": "<what the answer covered and what was missing>", '
        '"feedback": "<one concrete, actionable improvement tip>"}]}. '
        # question_analyses trước đây để "<optional objects>" → model tự bịa khoá, FE parse ra rỗng.
        "Emit EXACTLY ONE question_analyses entry per answered question, in order, reusing its "
        "sequence_number from QA History. Do NOT repeat the question or answer text — the client "
        "already has them. Skip questions with an empty answer. "
        # Chống bịa: chỉ chấm những gì ứng viên thực sự nói.
        "Base every statement strictly on what the candidate actually said; never invent facts. "
        f"The interview was required to be conducted in {lang}; mention a language problem in the "
        "reasoning ONLY if the answers really are in another language. "
        f"Write EVERY human-readable string (reasoning, recommended_next_step, analysis, feedback) "
        f"in {report_lang}, and never mix languages within the report."
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
    report_lang = report_language_name(ctx)
    system = (
        f"You assess a candidate's proficiency in {lang}. "
        # Chỉ lời ứng viên mới là bằng chứng — câu hỏi do AI viết, không phản ánh năng lực ứng viên.
        "Use ONLY the candidate's own answers as evidence; the interviewer's questions prove nothing "
        "about the candidate. "
        "Score each dimension 0-10 with these anchors: 0-2 unusable/no output, 3-4 basic (A1-A2), "
        "5-6 intermediate (B1), 7-8 upper-intermediate (B2), 9-10 advanced (C1-C2). "
        "overall_score MUST be consistent with the four dimensions (roughly their average) and "
        "cefr_level MUST match overall_score using the same anchors. "
        "Return JSON only: "
        '{"fluency": <0-10>, "grammar": <0-10>, "vocabulary": <0-10>, "comprehension": <0-10>, '
        '"overall_score": <0-10>, "cefr_level": "<A1|A2|B1|B2|C1|C2>", '
        f'"language_adherence": "<one short sentence: did the candidate consistently answer in {lang}? '
        'If not, which language did they use and how often>", '
        '"evidence": "<1-2 short fragments quoted verbatim from the answers that justify the scores>"}. '
        f"Write language_adherence and evidence in {report_lang}, but keep quoted fragments verbatim."
    )
    answers = json.dumps(
        [
            {"sequenceNumber": qa.sequence_number, "answerText": qa.answer_text}
            for qa in ctx.chat_history
            if (qa.answer_text or "").strip()
        ],
        ensure_ascii=False,
    )
    user = f"Required interview language: {lang}\n\nCandidate answers only:\n{answers}"
    return system, user


def detect_language_prompt(jd_text: str) -> tuple[str, str]:
    system = (
        "Detect if the job description requires a primary foreign language (e.g. English, Japanese, Korean) "
        'for the interview. Return JSON only: {"language": "<ISO code like en/ja/ko/vi>"}. '
        "Default to 'vi' if no clear foreign-language requirement."
    )
    return system, jd_text[:6000]
