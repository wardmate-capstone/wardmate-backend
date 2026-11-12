using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.Services.ApplicationWorkflow.Application;

public sealed record ConsultationInput(Guid SpecialistId, long ExpectedAssignmentVersion, string Question);
public sealed record ConsultationRequest(long ExpectedAssignmentVersion, string Question);
public sealed record ConsultationDispatch(int ExpectedVersion, Guid SpecialistId);
public sealed record SpecialistCandidate(Guid OfficerId, string? FullName, int[] AssignedCategories, bool Eligible, string[] BlockingReasons);
public sealed record ConsultationReply(int ExpectedVersion, string Text, bool WillingToTakeOver = false, string? Decision = null, long? ExpectedAssignmentVersion = null);
public interface IConsultationService
{
    Task<WorkflowResult<object>> Inbox(ReviewActor actor, int page, int pageSize, CancellationToken ct, string? status = null);
    Task<WorkflowResult<ApplicationConsultation>> Request(ReviewActor actor, Guid applicationId, ConsultationRequest input, CancellationToken ct);
    Task<WorkflowResult<SpecialistCandidate[]>> Candidates(ReviewActor actor, Guid applicationId, string token, CancellationToken ct);
    Task<WorkflowResult<ApplicationConsultation>> Dispatch(ReviewActor actor, Guid applicationId, Guid id, ConsultationDispatch input, string token, CancellationToken ct);
    Task<WorkflowResult<object>> DocumentAccess(ReviewActor actor, Guid applicationId, CancellationToken ct);
    Task<WorkflowResult<ApplicationConsultation[]>> List(ReviewActor actor, Guid applicationId, CancellationToken ct);
    Task<WorkflowResult<ApplicationConsultation>> Create(ReviewActor actor, Guid applicationId, ConsultationInput input, string token, CancellationToken ct);
    Task<WorkflowResult<ApplicationConsultation>> Reply(ReviewActor actor, Guid applicationId, Guid id, ConsultationReply input, bool close, CancellationToken ct, string token = "");
}
