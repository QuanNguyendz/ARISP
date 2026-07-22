from app.rag.chunker import chunk_text


def test_empty():
    assert chunk_text("") == []
    assert chunk_text("   \n\n  ") == []


def test_paragraph_split():
    text = "First paragraph.\n\nSecond paragraph."
    chunks = chunk_text(text)
    assert chunks == ["First paragraph.", "Second paragraph."]


def test_long_paragraph_is_split():
    long_para = "a" * 1200
    chunks = chunk_text(long_para)
    # 1200 ký tự, cắt 500 -> 3 mảnh (500/500/200)
    assert len(chunks) == 3
    assert all(len(c) <= 500 for c in chunks)


def test_short_paragraph_kept_whole():
    para = "x" * 999
    assert chunk_text(para) == [para]
