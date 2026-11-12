using WardMate.Services.ApplicationWorkflow.Domain;

namespace WardMate.UnitTests.Workflow;

public sealed class ConsultationDecisionTests
{
    [Theory]
    [InlineData("PENDING_DISPATCH", "DECLINE_REQUEST", true)]
    [InlineData("PENDING_DISPATCH", "TRANSFER_TO_SPECIALIST", false)]
    [InlineData("OPEN", "TRANSFER_TO_SPECIALIST", false)]
    [InlineData("ANSWERED", "TRANSFER_TO_SPECIALIST", true)]
    [InlineData("ANSWERED", "KEEP_ASSIGNEE", true)]
    [InlineData("CLOSED", "KEEP_ASSIGNEE", false)]
    [InlineData("OPEN", null, true)]
    public void Manager_decision_depends_on_consultation_state(string status, string? decision, bool allowed)
        => Assert.Equal(allowed, new ApplicationConsultation { Status = status }.AllowsDecision(decision));
}
