using MediatR;
using WardMate.SharedKernel.Common;

namespace WardMate.SharedKernel.CQRS;

/// <summary>Marker for commands that return a Result (no value payload).</summary>
public interface ICommand : IRequest<Result>;

/// <summary>Marker for commands that return a typed Result value.</summary>
public interface ICommand<TResponse> : IRequest<Result<TResponse>>;
