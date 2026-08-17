using AngleFlow.Domain.Applications;
using DomainApplicationId = AngleFlow.Domain.Applications.ApplicationId;

namespace AngleFlow.Infrastructure.Workflow;

public interface IWorkflowGateway
{
    Task<string> StartApplicationAsync(
        DomainApplicationId id,
        CreateApplicationCommand command,
        CancellationToken cancellationToken);

    Task CorrelateWithdrawalAsync(DomainApplicationId id, CancellationToken cancellationToken);
}
