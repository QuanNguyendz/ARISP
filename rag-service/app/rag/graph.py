"""LangGraph StateGraph cho sinh câu hỏi phỏng vấn — Giai đoạn 1: retrieve -> generate.

Cấu trúc node tách bạch để mở rộng:
  - Giai đoạn 2 (CRAG): chèn node `grade_documents` giữa retrieve và generate (+ nhánh corrective).
  - Giai đoạn 3 (Agentic): thêm node `router`/tools, dùng conditional edges.

Streaming token: API layer dùng `graph.astream_events(version="v2")` để bắt sự kiện
`on_chat_model_stream` từ node generate (ChatOpenAI streaming=True).
"""
from __future__ import annotations

from typing import TypedDict

from langchain_core.messages import AIMessage, HumanMessage, SystemMessage
from langgraph.graph import END, START, StateGraph

from app.core.llm import get_chat_model
from app.prompts import question_system_prompt, question_user_prompt
from app.rag.retriever import Candidate, ScopeFilter, hybrid_retrieve
from app.schemas import QuestionContext


class InterviewState(TypedDict, total=False):
    context: QuestionContext
    query: str
    retrieved: list[Candidate]
    retrieved_texts: list[str]
    question: str


def _build_filters(ctx: QuestionContext) -> list[ScopeFilter]:
    filters = [
        ScopeFilter("cv", ctx.application_id),
        ScopeFilter("jd", ctx.job_posting_id),
    ]
    if ctx.session_type == "real":
        filters.append(ScopeFilter("playbook", None))  # toàn bộ playbook
    return filters


def _build_query(ctx: QuestionContext) -> str:
    """Câu truy hồi: ưu tiên must-ask, rồi câu trả lời gần nhất, fallback về JD."""
    if ctx.must_ask_questions:
        return ctx.must_ask_questions[0]
    if ctx.chat_history:
        last = ctx.chat_history[-1]
        seed = f"{last.question_text} {last.answer_text}".strip()
        if seed:
            return seed[:1000]
    return ctx.job_description[:1000] or "interview question"


async def _retrieve_node(state: InterviewState) -> InterviewState:
    ctx = state["context"]
    query = _build_query(ctx)
    candidates = await hybrid_retrieve(query, _build_filters(ctx), top_k=None)
    texts = [f"[{c.source_type}] {c.chunk_text}" for c in candidates]
    return {"query": query, "retrieved": candidates, "retrieved_texts": texts}


async def _generate_node(state: InterviewState) -> InterviewState:
    ctx = state["context"]
    retrieved = state.get("retrieved_texts", [])
    messages = [
        SystemMessage(content=question_system_prompt(ctx, retrieved)),
        HumanMessage(content=question_user_prompt(ctx)),
    ]
    # Đưa lịch sử hội thoại vào để model điều chỉnh adaptive
    # (interviewer = assistant, ứng viên = human).
    for qa in ctx.chat_history:
        messages.append(AIMessage(content=qa.question_text))
        messages.append(HumanMessage(content=qa.answer_text or "[No response]"))

    resp = await get_chat_model().ainvoke(messages)
    return {"question": resp.content}


def build_interview_graph():
    g = StateGraph(InterviewState)
    g.add_node("retrieve", _retrieve_node)
    g.add_node("generate", _generate_node)
    g.add_edge(START, "retrieve")
    g.add_edge("retrieve", "generate")
    g.add_edge("generate", END)
    return g.compile()


# Compile 1 lần, tái dùng.
_graph = None


def get_interview_graph():
    global _graph
    if _graph is None:
        _graph = build_interview_graph()
    return _graph
