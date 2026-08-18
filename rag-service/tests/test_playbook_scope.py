"""Phạm vi Playbook khi truy hồi (ADR-025).

Trước đây phiên `real` lọc bằng `ScopeFilter("playbook", None)` = TOÀN BỘ playbook của hệ thống:
ngân hàng câu hỏi của tin này lọt vào buổi phỏng vấn của tin khác, và tài liệu đã xoá vẫn tiếp tục
được truy hồi vì chunk không bị dọn. Nay phạm vi đọc từ bảng `playbook_documents`.
"""
import uuid

from app.rag.graph import _build_filters, _split_by_document_type
from app.rag.retriever import Candidate, ScopeFilter, _build_filter_clause
from app.schemas import QuestionContext

JOB_ID = str(uuid.uuid4())
APP_ID = str(uuid.uuid4())


def _ctx(session_type: str = "real", round_number: int = 2) -> QuestionContext:
    return QuestionContext(
        session_id=str(uuid.uuid4()),
        job_posting_id=JOB_ID,
        application_id=APP_ID,
        session_type=session_type,
        round_number=round_number,
    )


def test_practice_khong_dung_playbook():
    """Buổi thử chỉ JD + CV (ADR-015/038/050)."""
    filters = _build_filters(_ctx(session_type="practice"))

    assert [f.source_type for f in filters] == ["cv", "jd"]


def test_real_gioi_han_playbook_theo_tin_va_vong():
    filters = _build_filters(_ctx(session_type="real", round_number=3))

    playbook = next(f for f in filters if f.source_type == "playbook")
    assert playbook.job_posting_id == JOB_ID
    assert playbook.round_number == 3


def test_menh_de_where_cua_playbook_doc_tu_bang_playbook_documents():
    clause, params = _build_filter_clause(
        [ScopeFilter("playbook", None, job_posting_id=JOB_ID, round_number=2)], start=1
    )

    # Xoá tài liệu là hết ảnh hưởng ngay — không phụ thuộc việc dọn chunk.
    assert "deleted_at IS NULL" in clause
    assert "FROM playbook_documents" in clause
    # org áp cho mọi tin; job_posting/round phải khớp tin (và vòng).
    assert "scope = 'org'" in clause
    assert "scope = 'job_posting'" in clause
    assert "scope = 'round' AND round_number = $3" in clause
    assert params == ["playbook", uuid.UUID(JOB_ID), 2]


def test_playbook_khong_co_pham_vi_van_giu_hanh_vi_cu():
    """Không truyền job → giữ nhánh cũ (dùng cho /retrieve thủ công, không phải luồng phỏng vấn)."""
    clause, params = _build_filter_clause([ScopeFilter("playbook", None)], start=1)

    assert clause == "((source_type = $1))"
    assert params == ["playbook"]


def _cand(source_type: str, doc_type: str | None = None, text: str = "t") -> Candidate:
    return Candidate(
        id=str(uuid.uuid4()),
        source_type=source_type,
        source_id=str(uuid.uuid4()),
        chunk_index=0,
        chunk_text=text,
        metadata={"document_type": doc_type} if doc_type else {},
    )


def test_loai_tai_lieu_quyet_dinh_cach_dung():
    """compliance = CẤM hỏi, không được nằm chung rổ ngữ cảnh tham khảo."""
    context, banned, red_flags, expected = _split_by_document_type(
        [
            _cand("jd", text="jd chunk"),
            _cand("playbook", "question_bank", "ngân hàng câu hỏi"),
            _cand("playbook", "compliance", "tình trạng hôn nhân"),
            _cand("playbook", "red_flag", "nhảy việc liên tục"),
            _cand("playbook", "expected_answer", "nêu được CAP theorem"),
        ]
    )

    assert banned == ["tình trạng hôn nhân"]
    assert red_flags == ["nhảy việc liên tục"]
    assert expected == ["nêu được CAP theorem"]
    # Chỉ JD + playbook loại thường mới là ngữ cảnh gợi ý.
    assert context == ["[jd] jd chunk", "[playbook] ngân hàng câu hỏi"]


def test_loai_la_coi_nhu_ngu_canh_thuong():
    context, banned, red_flags, expected = _split_by_document_type(
        [_cand("playbook", "loai_moi_chua_khai_bao", "nội dung")]
    )

    assert context == ["[playbook] nội dung"]
    assert not banned and not red_flags and not expected
