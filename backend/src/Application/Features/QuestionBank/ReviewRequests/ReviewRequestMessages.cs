using MediatR;
using OralExamination.Application.Common.Models;

namespace OralExamination.Application.Features.QuestionBank.ReviewRequests;

public sealed record ListQuestionReviewRequests(Guid ActorId, Guid? CourseId, string? Status, string? Keyword, int Page)
    : IRequest<ReviewRequestPage>;
public sealed record GetQuestionReviewRequest(Guid ActorId, Guid Id)
    : IRequest<Result<ReviewRequestDetail>>;
public sealed record GetMyQuestionReviewRequest(Guid ActorId, Guid Id)
    : IRequest<Result<ReviewRequestDetail>>;
public sealed record ListMyQuestionReviewRequests(Guid ActorId, Guid? CourseId, string? Status, int Page)
    : IRequest<ReviewRequestPage>;

public sealed class GetMyQuestionReviewRequestHandler(QuestionReviewRequestService service)
    : IRequestHandler<GetMyQuestionReviewRequest, Result<ReviewRequestDetail>>
{
    public Task<Result<ReviewRequestDetail>> Handle(GetMyQuestionReviewRequest request, CancellationToken ct) =>
        service.DetailMine(request.ActorId, request.Id, ct);
}

public sealed class ListMyQuestionReviewRequestsHandler(QuestionReviewRequestService service)
    : IRequestHandler<ListMyQuestionReviewRequests, ReviewRequestPage>
{
    public Task<ReviewRequestPage> Handle(ListMyQuestionReviewRequests request, CancellationToken ct) =>
        service.ListMine(request.ActorId, request.CourseId, request.Status, request.Page, ct);
}
public sealed record DecideQuestionReviewRequest(Guid ActorId, Guid Id, ReviewRequestDecision Decision)
    : IRequest<Result<bool>>;

public sealed class ListQuestionReviewRequestsHandler(QuestionReviewRequestService service)
    : IRequestHandler<ListQuestionReviewRequests, ReviewRequestPage>
{
    public Task<ReviewRequestPage> Handle(ListQuestionReviewRequests request, CancellationToken ct) =>
        service.List(request.ActorId, request.CourseId, request.Status, request.Keyword, request.Page, ct);
}

public sealed class GetQuestionReviewRequestHandler(QuestionReviewRequestService service)
    : IRequestHandler<GetQuestionReviewRequest, Result<ReviewRequestDetail>>
{
    public Task<Result<ReviewRequestDetail>> Handle(GetQuestionReviewRequest request, CancellationToken ct) =>
        service.Detail(request.ActorId, request.Id, ct);
}

public sealed class DecideQuestionReviewRequestHandler(QuestionReviewRequestService service)
    : IRequestHandler<DecideQuestionReviewRequest, Result<bool>>
{
    public Task<Result<bool>> Handle(DecideQuestionReviewRequest request, CancellationToken ct) =>
        service.Decide(request.ActorId, request.Id, request.Decision, ct);
}
