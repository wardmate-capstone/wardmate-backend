using MediatR;
using WardMate.SharedKernel.Common;

namespace WardMate.SharedKernel.CQRS;

/// <summary>Handler for queries returning a typed Result value.</summary>
public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;
