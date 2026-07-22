"""Hợp đồng wire camelCase với .NET phải round-trip đúng."""
from app.schemas import QuestionContext, QuestionAnswer


def test_question_context_accepts_camel_case():
    payload = {
        "sessionId": "s1",
        "jobPostingId": "j1",
        "applicationId": "a1",
        "jobDescription": "JD",
        "candidateCv": "CV",
        "sessionType": "practice",
        "chatHistory": [
            {"sequenceNumber": 1, "questionText": "Q1", "answerText": "A1"}
        ],
        "mustAskQuestions": ["must"],
        "playbookStyleGuides": ["style"],
    }
    ctx = QuestionContext.model_validate(payload)
    assert ctx.session_id == "s1"
    assert ctx.job_posting_id == "j1"
    assert ctx.chat_history[0].question_text == "Q1"
    assert ctx.must_ask_questions == ["must"]


def test_emits_camel_case():
    qa = QuestionAnswer(sequence_number=2, question_text="q", answer_text="a")
    dumped = qa.model_dump(by_alias=True)
    assert dumped["sequenceNumber"] == 2
    assert dumped["questionText"] == "q"
