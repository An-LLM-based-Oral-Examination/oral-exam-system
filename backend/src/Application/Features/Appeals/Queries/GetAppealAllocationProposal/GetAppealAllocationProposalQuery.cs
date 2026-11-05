using System;
using MediatR;
using OralExamination.Application.Common.Models;
using OralExamination.Application.Features.Appeals.DTOs;

namespace OralExamination.Application.Features.Appeals.Queries.GetAppealAllocationProposal;

public sealed record GetAppealAllocationProposalQuery(Guid? SessionId = null) 
    : IRequest<Result<AppealAllocationProposalDto>>;
