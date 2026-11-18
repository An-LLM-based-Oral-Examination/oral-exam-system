namespace OralExamination.Application.Features.OfficialExams.DTOs;

public sealed class UpdateExamShiftStatusRequest
{
    public string Status { get; set; } = string.Empty;

    public UpdateExamShiftStatusRequest() { }

    public UpdateExamShiftStatusRequest(string status)
    {
        Status = status;
    }
}
