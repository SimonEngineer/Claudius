namespace Weaver.Workflows;

public interface INodeHandlerRegistry
{
    INodeHandler Resolve(string nodeType);
    bool TryResolve(string nodeType, out INodeHandler handler);
    IReadOnlyCollection<string> RegisteredTypes { get; }
}

public class NodeHandlerRegistry : INodeHandlerRegistry
{
    private readonly Dictionary<string, INodeHandler> _handlers;

    public NodeHandlerRegistry(IEnumerable<INodeHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.Type, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> RegisteredTypes => _handlers.Keys;

    public INodeHandler Resolve(string nodeType)
    {
        if (_handlers.TryGetValue(nodeType, out var handler))
        {
            return handler;
        }

        throw new InvalidOperationException($"No node handler registered for type '{nodeType}'.");
    }

    public bool TryResolve(string nodeType, out INodeHandler handler)
    {
        return _handlers.TryGetValue(nodeType, out handler!);
    }
}
