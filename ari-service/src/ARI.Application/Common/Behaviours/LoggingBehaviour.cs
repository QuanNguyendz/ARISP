using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using MediatR.Pipeline;
using Microsoft.Extensions.Logging;

namespace ARI.Application.Common.Behaviours
{
    /// <summary>Pre-processor: log tên request + user hiện tại trước khi handler chạy.</summary>
    public class LoggingBehaviour<TRequest> : IRequestPreProcessor<TRequest>
        where TRequest : notnull
    {
        private readonly ILogger<TRequest> _logger;
        private readonly ICurrentUserService _currentUser;

        public LoggingBehaviour(ILogger<TRequest> logger, ICurrentUserService currentUser)
        {
            _logger = logger;
            _currentUser = currentUser;
        }

        public Task Process(TRequest request, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogInformation("ARI Request: {Name} by {UserId}", requestName, _currentUser.UserId);
            return Task.CompletedTask;
        }
    }
}
