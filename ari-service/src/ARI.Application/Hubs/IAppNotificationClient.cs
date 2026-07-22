using System.Threading.Tasks;

namespace ARI.Application.Hubs
{
    public interface IAppNotificationClient
    {
        Task ReceiveSystemEvent(string eventType, object payload);
    }
}
