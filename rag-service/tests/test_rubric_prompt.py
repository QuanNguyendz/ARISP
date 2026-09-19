"""Prompt chấm điểm phải bám bộ tiêu chí của doanh nghiệp (ADR-060).

Trước đây prompt ép cứng 8 khoá tiếng Anh rồi hỏi luôn ``score`` tổng — doanh nghiệp khai rubric gì
cũng vậy, model vẫn chấm theo danh sách của chúng ta và tự nghĩ ra điểm tổng.
"""
from app.prompts import evaluate_prompt
from app.schemas import RubricCriterion, SessionContext


def _ctx(criteria=None, **kwargs) -> SessionContext:
    return SessionContext(
        session_id="s1",
        job_description="JD",
        candidate_cv="CV",
        session_type="real",
        chat_history=[],
        language="vi",
        criteria=criteria or [],
        **kwargs,
    )


def _company_criteria() -> list[RubricCriterion]:
    return [
        RubricCriterion(key="system_design", name="Thiết kế hệ thống", weight=60,
                        description="Nêu được đánh đổi."),
        RubricCriterion(key="communication", name="Giao tiếp", weight=40),
    ]


def test_company_keys_replace_the_hardcoded_list():
    system, user = evaluate_prompt(_ctx(_company_criteria()))

    assert "system_design, communication" in system
    # Bộ mặc định phải BIẾN MẤT, không được đứng song song để model chọn nhầm.
    assert "problem_solving, culture_fit" not in system
    assert "Thiết kế hệ thống" in user
    assert "weight 60%" in user
    assert "standard: Nêu được đánh đổi." in user


def test_model_is_told_not_to_compute_the_overall_score():
    system, _ = evaluate_prompt(_ctx(_company_criteria()))

    assert "Do NOT compute an overall score yourself" in system


def test_without_a_rubric_the_default_key_set_is_kept():
    """Chưa khai rubric → giữ nguyên hành vi cũ, không để model tự bịa khoá."""
    system, user = evaluate_prompt(_ctx(scoring_rubric="Tự do 100%"))

    assert "technical, communication, problem_solving" in system
    assert "never invent other keys" in system
    assert "Tự do 100%" in user            # rơi về rubric thô của tin tuyển dụng
    assert "COMPANY SCORING RUBRIC" not in user


def test_weight_is_rendered_without_trailing_zeros():
    """33.5 phải ra '33.5', 40 phải ra '40' — không phải '40.00' đọc rối."""
    _, user = evaluate_prompt(_ctx([
        RubricCriterion(key="a_technical", name="Chuyên môn", weight=33.5),
        RubricCriterion(key="b_attitude", name="Thái độ", weight=66.5),
    ]))

    assert "weight 33.5%" in user
    assert "weight 66.5%" in user


def test_criteria_default_to_empty_when_dotnet_omits_them():
    """.NET cũ (chưa gửi 'criteria') không được làm rag-service nổ."""
    ctx = SessionContext.model_validate({
        "sessionId": "s1",
        "jobDescription": "JD",
        "candidateCv": "CV",
        "sessionType": "real",
        "chatHistory": [],
    })

    assert ctx.criteria == []
    system, _ = evaluate_prompt(ctx)
    assert "technical, communication, problem_solving" in system


def test_criteria_round_trip_from_camel_case_wire():
    ctx = SessionContext.model_validate({
        "sessionId": "s1",
        "jobDescription": "JD",
        "candidateCv": "CV",
        "sessionType": "real",
        "chatHistory": [],
        "criteria": [{"key": "system_design", "name": "Thiết kế", "weight": 100,
                      "description": "Chuẩn chấm"}],
    })

    assert ctx.criteria[0].key == "system_design"
    assert ctx.criteria[0].weight == 100
    assert ctx.criteria[0].description == "Chuẩn chấm"


def test_level_anchors_are_rendered_under_their_criterion():
    """ADR-073: mức neo HM khai phải tới được prompt chấm — thiếu nó thì '70 điểm' là cảm tính của model."""
    from app.schemas import RubricLevels

    _, user = evaluate_prompt(_ctx([
        RubricCriterion(key="system_design", name="Thiết kế hệ thống", weight=100,
                        levels=RubricLevels(excellent="Nêu đánh đổi và trường hợp biên", poor="Không trả lời được")),
    ]))

    assert "90-100: Nêu đánh đổi và trường hợp biên" in user
    assert "0-39: Không trả lời được" in user
    assert "MUST fall in the band" in user
    # Dải không khai thì không in dòng rỗng.
    assert "70-89:" not in user


def test_levels_round_trip_from_camel_case_wire():
    ctx = SessionContext.model_validate({
        "sessionId": "s1",
        "chatHistory": [],
        "criteria": [{"key": "a", "name": "A", "weight": 100,
                      "levels": {"excellent": "Xuất sắc", "good": None, "fair": "Tạm", "poor": None}}],
    })

    assert ctx.criteria[0].levels is not None
    assert ctx.criteria[0].levels.excellent == "Xuất sắc"
    assert ctx.criteria[0].levels.fair == "Tạm"


def test_no_band_instruction_when_no_criterion_has_levels():
    _, user = evaluate_prompt(_ctx(_company_criteria()))

    assert "MUST fall in the band" not in user
