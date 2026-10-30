using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record ConsultationInput(Guid SpecialistId, long ExpectedAssignmentVersion, string Question);
public sealed record ConsultationReply(int ExpectedVersion, string Text);
public interface IConsultationService
{
    Task<WorkflowResult<object>> Inbox(ReviewActor actor, int page, int pageSize, CancellationToken ct);
    Task<WorkflowResult<object>> DocumentAccess(ReviewActor actor, Guid applicationId, CancellationToken ct);
    Task<WorkflowResult<ApplicationConsultation[]>> List(ReviewActor actor, Guid applicationId, CancellationToken ct);
    Task<WorkflowResult<ApplicationConsultation>> Create(ReviewActor actor, Guid applicationId, ConsultationInput input, string token, CancellationToken ct);
    Task<WorkflowResult<ApplicationConsultation>> Reply(ReviewActor actor, Guid applicationId, Guid id, ConsultationReply input, bool close, CancellationToken ct);
}
