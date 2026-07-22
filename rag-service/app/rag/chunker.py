"""Chia nhỏ văn bản — mirror logic của PlaybookService.ChunkText (.NET) để giữ chunk parity:
tách theo đoạn (double newline); đoạn < 1000 ký tự giữ nguyên, ngược lại cắt ~500 ký tự.
"""
from __future__ import annotations

import re

_PARA_SPLIT = re.compile(r"\r\n\r\n|\n\n")


def chunk_text(text: str, max_para: int = 1000, piece: int = 500) -> list[str]:
    if not text:
        return []
    chunks: list[str] = []
    for para in _PARA_SPLIT.split(text):
        para = para.strip()
        if not para:
            continue
        if len(para) < max_para:
            chunks.append(para)
        else:
            for i in range(0, len(para), piece):
                seg = para[i : i + piece].strip()
                if seg:
                    chunks.append(seg)
    return chunks
