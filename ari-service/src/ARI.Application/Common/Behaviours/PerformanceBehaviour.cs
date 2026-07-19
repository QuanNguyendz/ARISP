using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace ARI.Application.Common.Behaviours
{
    /// <summary>Cảnh báo request chậm (&gt; 500ms) kèm tên request + user để soi hot path.</summary>
    public class PerformanceBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
        where TRequest : notnull
    {
        private readonly Stopwatch _timer = new();
        private readonly ILogger<TRequest> _logger;
        private readonly ICurrentUserService _currentUser;

        public PerformanceBehaviour(ILogger<TRequest> logger, ICurrentUserService currentUser)
        {
            _logger = logger;
            _currentUser = currentUser;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            _timer.Start();
            var response = await next();
            _timer.Stop();

            var elapsedMilliseconds = _timer.ElapsedMilliseconds;
            if (elapsedMilliseconds > 500)
            {
                var requestName = typeof(TRequest).Name;
                _logger.LogWarning(
                    "ARI Long Running Request: {Name} ({ElapsedMilliseconds} ms) by {UserId} {@Request}",
                    requestName, elapsedMilliseconds, _currentUser.UserId, request);
            }

            return response;
        }
    }
}
