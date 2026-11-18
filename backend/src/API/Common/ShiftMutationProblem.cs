using Microsoft.AspNetCore.Mvc;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.InClassShifts;
using OralExamination.Application.Features.OfficialExams;

namespace API.Common;

public static class ShiftMutationProblem
{
    public static ObjectResult Create(string error)
    {
        var code = ShiftMutationErrors.Code(error);
        var status = code is not null ? 409
            : error == OfficialExamErrors.Forbidden || error == InClassShiftErrors.LecturerNotAuthorized ? 403
            : error == OfficialExamErrors.ShiftNotFound || error == InClassShiftErrors.ShiftNotFound
                || error == OfficialExamErrors.SessionNotFound || error == InClassShiftErrors.ClassNotFound ? 404
            : InClassShiftErrors.IsUnprocessablePoolError(error) ? 422 : 400;
        code ??= status switch
        {
            403 => "SHIFT_FORBIDDEN",
            404 => "SHIFT_RESOURCE_NOT_FOUND",
            422 => "SHIFT_UNPROCESSABLE_POOL",
            _ => "SHIFT_INVALID_INPUT"
        };
        var problem = new ProblemDetails
        {
            Status = status,
            Title = error,
            Detail = error,
            Type = $"https://oralexam.fpt.edu.vn/errors/{code.ToLowerInvariant().Replace('_', '-')}"
        };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }
}
