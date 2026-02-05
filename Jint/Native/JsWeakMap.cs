using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

internal sealed class JsWeakMap : ObjectInstance
{
    private readonly ConditionalWeakTable<object, object> _table;

    public JsWeakMap(Engine engine) : base(engine)
    {
        _table = new ConditionalWeakTable<object, object>();
    }

    internal bool WeakMapHas(JsValue key)
    {
        return _table.TryGetValue(key.Obj!, out _);
    }

    internal bool WeakMapDelete(JsValue key)
    {
        return _table.Remove(key);
    }

    internal void WeakMapSet(JsValue key, JsValue value)
    {
        if (!key.CanBeHeldWeakly(_engine.GlobalSymbolRegistry))
        {
            Throw.TypeError(_engine.Realm, "WeakMap key must be an object, got " + key);
        }

#if SUPPORTS_WEAK_TABLE_ADD_OR_UPDATE
        _table.AddOrUpdate(key, value.Obj!);
#else
        _table.Remove(key);
        _table.Add(key, value);
#endif
    }

    internal JsValue WeakMapGet(JsValue key)
    {
        if (!_table.TryGetValue(key, out var value))
        {
            return Undefined;
        }

        return JsValue.FromObject(value);
    }

    internal JsValue GetOrInsert(JsValue key, JsValue value)
    {
        if (_table.TryGetValue(key, out var temp))
        {
            return JsValue.FromObject(temp);
        }

        _table.Add(key, value);
        return value;
    }

    internal JsValue GetOrInsertComputed(JsValue key, ICallable callbackfn)
    {
        if (_table.TryGetValue(key, out var temp))
        {
            return JsValue.FromObject(temp);
        }

        var value = callbackfn.Call(Undefined, key);

        // NOTE: The Map may have been modified during execution of callback.
        if (_table.TryGetValue(key, out _))
        {
            _table.Remove(key);
        }

        _table.Add(key, value);
        return value;
    }
}
