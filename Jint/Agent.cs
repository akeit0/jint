using Jint.Native;

namespace Jint;

/// <summary>
/// https://tc39.es/ecma262/#sec-agents , still a work in progress, mostly placeholder
/// </summary>
internal sealed class Agent
{
    private readonly List<object> _keptAlive = new();

    public void AddToKeptObjects(object target)
    {
        _keptAlive.Add(target);
    }

    public void ClearKeptObjects()
    {
        _keptAlive.Clear();
    }
}
