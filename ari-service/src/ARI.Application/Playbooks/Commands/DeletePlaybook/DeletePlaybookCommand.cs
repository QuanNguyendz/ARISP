using System;
using System.Threading;
using System.Threading.Tasks;
using ARI.Application.Common;
using ARI.Application.Interfaces;
using ARI.Domain.Entities;
using MediatR;

namespace ARI.Application.Playbooks.Commands.DeletePlaybook
{
    /// <summary>Xoá mềm một playbook.</summary>
    public record DeletePlaybookCommand(Guid Id) : IRequest<Result>;

    public class DeletePlaybookCommandHandler : IRequestHandler<DeletePlaybookCommand, Result>
    {
        private readonly IUnitOfWork _unitOfWork;

        public DeletePlaybookCommandHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<Result> Handle(DeletePlaybookCommand request, CancellationToken ct)
        {
            var doc = await _unitOfWork.Repository<PlaybookDocument>().GetByIdAsync(request.Id, ct);
            if (doc == null)
                return Result.Failure("Không tìm thấy playbook.", CommonErrorCodes.NotFound);

            doc.DeletedAt = DateTimeOffset.UtcNow;
            doc.UpdatedAt = DateTimeOffset.UtcNow;
            _unitOfWork.Repository<PlaybookDocument>().Update(doc);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success();
        }
    }
}
