import pytest

from app.core.embeddings import _mock_vector, to_pgvector
from app.config import get_settings


def test_mock_vector_deterministic_and_sized():
    v1 = _mock_vector("hello")
    v2 = _mock_vector("hello")
    v3 = _mock_vector("world")
    assert v1 == v2
    assert v1 != v3
    assert len(v1) == get_settings().embedding_dim


def test_to_pgvector_format():
    s = to_pgvector([0.1, 0.2, 0.3])
    assert s.startswith("[") and s.endswith("]")
    assert s.count(",") == 2
