using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace RemainingMediator;

public class Mediator : IMediator
{
    private readonly IServiceProvider _serviceProvider;

    private static readonly ConcurrentDictionary<Type, Type> RequestHandlerDictionary = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> MethodInfoDictionary = new();

    public Mediator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        var handlers = _serviceProvider.GetServices<INotificationHandler<TNotification>>() ?? [];
        var tasks = handlers.Select(x => x.HandleAsync(notification, cancellationToken));

        return Task.WhenAll(tasks);
    }

    public Task SendAsync<TRequest>(TRequest request, CancellationToken cancellationToken = default)
        where TRequest : IRequest
    {
        var handler = _serviceProvider.GetRequiredService<IRequestHandler<TRequest>>();
        return handler.HandleAsync(request, cancellationToken);
    }

    public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var handlerType = RequestHandlerDictionary.GetOrAdd(requestType, requestType
            => typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse)));

        var handler = _serviceProvider.GetService(handlerType);
        if (handler == null)
            throw new InvalidOperationException($"Handler not found for {requestType.Name}");

        var handlerMethod = MethodInfoDictionary.GetOrAdd(handlerType, handlerType
            => handlerType.GetMethod(nameof(IRequestHandler<IRequest<TResponse>, TResponse>.HandleAsync), [requestType, typeof(CancellationToken)])!);

        if (handlerMethod == null) throw new InvalidOperationException($"HandleAsync method not found in {handlerType.Name}");

        if (handlerMethod.Invoke(handler, [request, cancellationToken]) is not Task<TResponse> task)
            throw new InvalidOperationException($"Handler {handler.GetType().Name} returned null or an incompatible type.");

        return await task;
    }
}