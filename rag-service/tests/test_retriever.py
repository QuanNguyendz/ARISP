from app.rag.retriever import (
    Candidate,
    ScopeFilter,
    _build_filter_clause,
    _scope_weight,
)


def _cand(source_type, metadata=None):
    return Candidate(
        id="x",
        source_type=source_type,
        source_id="y",
        chunk_index=0,
        chunk_text="t",
        metadata=metadata or {},
    )


def test_scope_weight_cv_jd_high():
    assert _scope_weight(_cand("cv")) == 1.0
    assert _scope_weight(_cand("jd")) == 1.0


def test_scope_weight_playbook_by_scope():
    assert _scope_weight(_cand("playbook", {"scope": "company"})) == 0.6
    assert _scope_weight(_cand("playbook", {"scope": "job_posting"})) == 1.0
    assert _scope_weight(_cand("playbook", {"scope": "round"})) == 1.0
    # scope không xác định -> base playbook
    assert _scope_weight(_cand("playbook", {})) == 0.8


def test_build_filter_clause_with_and_without_id():
    filters = [
        ScopeFilter("cv", "11111111-1111-1111-1111-111111111111"),
        ScopeFilter("playbook", None),
    ]
    clause, params = _build_filter_clause(filters, start=2)
    assert "source_type = $2 AND source_id = $3" in clause
    assert "source_type = $4" in clause
    # params: cv-type, cv-uuid, playbook-type
    assert len(params) == 3
    assert params[0] == "cv"
    assert params[2] == "playbook"


def test_build_filter_clause_empty():
    clause, params = _build_filter_clause([], start=2)
    assert clause == "TRUE"
    assert params == []
