using System.Threading.Tasks;

namespace ARISP.Application.Hubs
{
    public interface ISessionClient
    {
        Task ReceiveQuestion(int sequenceNumber, string questionText, string? questionType, int difficultyLevel);
        Task ReceiveAnswerAnalysis(string feedback);
        Task ReceiveSessionStatus(string status);
        Task ReceiveCheatAlert(string description);
        Task ReceiveSpeakingStatus(bool isSpeaking);
    }
}
