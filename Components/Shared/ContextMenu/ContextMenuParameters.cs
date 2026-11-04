using System.Collections;

namespace BookHeaven.Reader.Components.Shared.ContextMenu;

public sealed partial class ContextMenuParameters : IEnumerable<KeyValuePair<string, object?>>
{
    private readonly Dictionary<string, object?> _parameters = new();
    
    public void Add(string parameterName, object? value)
    {
        _parameters[parameterName] = value;
    }
    
    public T? Get<T>(string parameterName)
    {
        if (_parameters.TryGetValue(parameterName, out var value))
        {
            return (T?)value;
        }

        throw new KeyNotFoundException($"{parameterName} does not exist in ContextMenu parameters");
    }
    
    public T? TryGet<T>(string parameterName)
    {
        if (_parameters.TryGetValue(parameterName, out var value))
        {
            return (T?)value;
        }

        return default;
    }
    
    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        return _parameters.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}