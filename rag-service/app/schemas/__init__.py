"""Pydantic models — hợp đồng JSON với backend .NET.

Wire dùng camelCase (khớp JsonNamingPolicy.CamelCase phía RagServiceProvider .NET).
Nội bộ Python dùng snake_case; alias_generator=to_camel + populate_by_name cho phép cả hai.
"""
from __future__ import annotations

from pydantic import BaseModel, ConfigDict
from pydantic.alias_generators import to_camel


class CamelModel(BaseModel):
    model_config = ConfigDict(alias_generator=to_camel, populate_by_name=True)


# ---------- Ingest ----------
class IngestRequest(CamelModel):
    source_type: str  # jd | cv | playbook
    source_id: str  # GUID (job_posting_id | application_id | playbook_document_id)
    text: str
    scope: str | None = None  # company | job_posting | round (playbook)
    document_type: str | None = None  # interview_style_guide | question_bank | must_ask | ...
    replace_existing: bool = True  # xoá chunk cũ cùng (source_type, source_id) trước khi ghi


class IngestResponse(CamelModel):
    source_type: str
    source_id: str
    chunks_written: int


# ---------- Embed ----------
class EmbedRequest(CamelModel):
    text: str


class EmbedResponse(CamelModel):
    embedding: list[float]


# ---------- Retrieve ----------
class RetrieveRequest(CamelModel):
    query: str
    source_id: str | None = None
    source_types: list[str] | None = None  # lọc theo loại; None = tất cả
    top_k: int = 5


class RetrievedChunk(CamelModel):
    id: str
    source_type: str
    source_id: str
    chunk_index: int
    chunk_text: str
    score: float


class RetrieveResponse(CamelModel):
    chunks: list[RetrievedChunk]


# ---------- Interview ----------
class QuestionAnswer(CamelModel):
    sequence_number: int = 0
    question_text: str = ""
    answer_text: str = ""


class QuestionContext(CamelModel):
    session_id: str
    job_posting_id: str
    application_id: str
    job_description: str = ""
    candidate_cv: str = ""
    session_type: str = "real"  # practice | real
    chat_history: list[QuestionAnswer] = []
    must_ask_questions: list[str] = []
    playbook_style_guides: list[str] = []
    language: str | None = None


class AnswerContext(CamelModel):
    question_text: str = ""
    answer_transcript: str = ""


class AnswerAnalysis(CamelModel):
    difficulty_level: int = 3
    feedback: str = ""


class SessionContext(CamelModel):
    session_id: str
    job_description: str = ""
    candidate_cv: str = ""
    session_type: str = "real"
    chat_history: list[QuestionAnswer] = []
    scoring_rubric: str = "{}"
    language: str | None = None


class EvaluationReport(CamelModel):
    verdict: str = "not_pass"  # pass | not_pass
    score: float = 0.0
    reasoning: str = ""
    recommended_next_step: str = ""
    criterion_scores_json: str = "{}"
    question_analyses_json: str = "[]"


class LanguageAssessment(CamelModel):
    fluency: float = 0.0
    grammar: float = 0.0
    vocabulary: float = 0.0
    comprehension: float = 0.0
    overall_score: float = 0.0


class DetectLanguageRequest(CamelModel):
    jd_text: str


class DetectLanguageResponse(CamelModel):
    language: str  # "en" | "ja" | "vi" | ...


class CompleteJsonRequest(CamelModel):
    system_instruction: str
    user_content: str
