using MediatR;
using WardMate.SharedKernel.Common;

namespace WardMate.SharedKernel.CQRS;

/// <summary>
/// Marker for queries that return a typed Result value.
/// Queries are read-only — they must not cause side effects.
/// </summary>
public interface IQuery<TResponse> : IRequest<Result<TResponse>>;
