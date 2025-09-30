namespace RemainingMediator;

public interface IRequestBase;

public interface IRequest : IRequestBase;

public interface IRequest<TResponse> : IRequestBase;