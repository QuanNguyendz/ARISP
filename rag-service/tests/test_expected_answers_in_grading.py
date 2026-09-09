"""Đáp án mong đợi của doanh nghiệp phải tới được prompt CHẤM ĐIỂM (ADR-025/062).

Bối cảnh: playbook loại ``expected_answer`` được nạp, được truy hồi, được phân loại riêng — nhưng
chỉ tới được prompt SINH CÂU HỎI. Prompt CHẤM không hề nhận nó, nên tài liệu đáp án mà doanh nghiệp
upload lên **không ảnh hưởng một chút nào tới điểm số**, dù chú thích trong chính prompt ghi
*"expected_answer dùng để chấm chứ không đọc cho ứng viên nghe"*.

Đây đúng loại lỗi "thể hiện trong luồng nhưng code không hoạt động".
"""
from app.prompts import evaluate_prompt
from app.schemas import QuestionAnswer, RubricCriterion, SessionContext


def _ctx(**kwargs) -> SessionContext:
    return SessionContext(
        session_id="s1",
        job_posting_id="job-1",
        round_number=1,
        job_description="JD",
        candidate_cv="CV",
        session_type="real",
        chat_history=[
            QuestionAnswer(sequence_number=1, question_text="Rollback migration?", answer_text="Em dùng Down()."),
        ],
        language="vi",
        criteria=[RubricCriterion(key="technical", name="Chuyên môn", weight=100)],
        **kwargs,
    )


def test_dap_an_mong_doi_di_vao_prompt_cham():
    expected = ["Phải nêu được: khoá bảng, mất dữ liệu, và cách kiểm tra trước khi chạy."]

    _, user = evaluate_prompt(_ctx(), expected)

    assert expected[0] in user, "Đáp án mong đợi phải nằm trong prompt chấm điểm"


def test_yeu_cau_cham_theo_dap_an_cua_cong_ty_khong_theo_y_model():
    _, user = evaluate_prompt(_ctx(), ["Đáp án chuẩn của công ty."])

    # Không chỉ nhét văn bản vào: phải nói rõ chấm THEO đáp án đó, và chấp nhận cách diễn đạt khác.
    assert "Grade the candidate against these" in user
    assert "phrase things differently" in user


def test_khong_co_dap_an_thi_prompt_khong_doi():
    """Không khai expected_answer thì prompt giữ nguyên như trước — không thêm khối rỗng lơ lửng."""
    _, without = evaluate_prompt(_ctx(), [])
    _, none_given = evaluate_prompt(_ctx())

    assert without == none_given
    assert "Reference answers" not in without


def test_buoi_thu_khong_dung_playbook():
    """Buổi THỬ không nạp playbook (ADR-015/038) — hàm truy hồi phải trả rỗng trước khi chạm DB."""
    import asyncio

    from app.services.interview_service import _expected_answers

    ctx = _ctx()
    ctx.session_type = "practice"

    assert asyncio.run(_expected_answers(ctx)) == []


def test_thieu_job_posting_id_thi_bo_qua():
    """Thiếu phạm vi thì không truy hồi — tránh kéo playbook của tin khác vào bài chấm."""
    import asyncio

    from app.services.interview_service import _expected_answers

    ctx = _ctx()
    ctx.job_posting_id = ""

    assert asyncio.run(_expected_answers(ctx)) == []
