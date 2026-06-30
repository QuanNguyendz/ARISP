using System.Threading.Tasks;

namespace ARISP.Application.Hubs
{
    public interface IAppNotificationClient
    {
        Task ReceiveSystemEvent(string eventType, object payload);
    }
}
