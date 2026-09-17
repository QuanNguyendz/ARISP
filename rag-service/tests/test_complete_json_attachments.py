"""ADR-070 — đường dự phòng chấm CV gửi kèm file PDF gốc tới model."""
import base64

from app.core.llm import build_user_content
from app.schemas import CompleteJsonRequest


def _pdf(name: str = "cv.pdf") -> dict:
    return {"fileName": name, "mimeType": "application/pdf", "data": base64.b64encode(b"%PDF-1.7").decode()}


def test_request_accepts_camel_case_attachments_and_defaults_to_none():
    req = CompleteJsonRequest.model_validate(
        {"systemInstruction": "s", "userContent": "u", "attachments": [_pdf()]}
    )
    assert req.attachments[0].file_name == "cv.pdf"
    assert req.attachments[0].mime_type == "application/pdf"

    plain = CompleteJsonRequest.model_validate({"systemInstruction": "s", "userContent": "u"})
    assert plain.attachments == []


def test_without_attachments_content_stays_plain_text():
    assert build_user_content("hello", []) == "hello"
    assert build_user_content("hello", None) == "hello"


def test_pdf_attachments_become_langchain_file_blocks():
    req = CompleteJsonRequest.model_validate(
        {"systemInstruction": "s", "userContent": "JD + CV", "attachments": [_pdf("jd.pdf"), _pdf("cv.pdf")]}
    )

    content = build_user_content(req.user_content, req.attachments)

    assert content[0] == {"type": "text", "text": "JD + CV"}
    assert [b["filename"] for b in content[1:]] == ["jd.pdf", "cv.pdf"]
    for block in content[1:]:
        assert block["type"] == "file"
        assert block["source_type"] == "base64"
        assert block["mime_type"] == "application/pdf"
        assert base64.b64decode(block["data"]) == b"%PDF-1.7"


def test_unsupported_or_empty_files_are_skipped():
    req = CompleteJsonRequest.model_validate(
        {
            "systemInstruction": "s",
            "userContent": "u",
            "attachments": [
                {"fileName": "cv.docx", "mimeType": "application/msword", "data": "AAAA"},
                {"fileName": "empty.pdf", "mimeType": "application/pdf", "data": ""},
            ],
        }
    )
    assert build_user_content(req.user_content, req.attachments) == "u"


def test_file_block_converts_to_openai_chat_completions_format():
    """Khớp đúng dạng langchain-openai gửi đi, để phát hiện sớm nếu thư viện đổi định dạng."""
    from langchain_core.messages import HumanMessage
    from langchain_openai.chat_models.base import _convert_message_to_dict

    req = CompleteJsonRequest.model_validate(
        {"systemInstruction": "s", "userContent": "u", "attachments": [_pdf()]}
    )
    message = _convert_message_to_dict(HumanMessage(content=build_user_content("u", req.attachments)))

    file_part = message["content"][1]
    assert file_part["type"] == "file"
    assert file_part["file"]["filename"] == "cv.pdf"
    assert file_part["file"]["file_data"].startswith("data:application/pdf;base64,")
