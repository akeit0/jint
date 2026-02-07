using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// https://tc39.es/ecma262/#sec-properties-of-weak-ref-instances
/// </summary>
internal sealed class JsWeakRef : ObjectInstance
{
    private readonly WeakReference<object> _weakRefTarget;

    public JsWeakRef(Engine engine, JsValue target) : base(engine)
    {
        if (target.IsObject() || target.IsString())
        {
            _weakRefTarget = new WeakReference<object>(target.Obj!);
        }
        else _weakRefTarget = new WeakReference<object>(target);
    }

    public JsValue WeakRefDeref()
    {
        if (_weakRefTarget.TryGetTarget(out var target))
        {
            _engine.AddToKeptObjects(target);
            return JsValue.FromObject(target);
        }

        return Undefined;
    }
}
