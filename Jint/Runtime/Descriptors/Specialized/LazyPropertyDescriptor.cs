using System.Runtime.CompilerServices;
using Jint.Native;

namespace Jint.Runtime.Descriptors.Specialized;

internal sealed class LazyPropertyDescriptor<T> : PropertyDescriptor
{
    private readonly T _state;
    private readonly Func<T, JsValue> _resolver;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal LazyPropertyDescriptor(T state, Func<T, JsValue> resolver, PropertyFlag flags)
        : base(default, flags | PropertyFlag.CustomJsValue)
    {
        _flags &= ~PropertyFlag.NonData;
        _state = state;
        _resolver = resolver;
    }

    protected internal override JsValue CustomValue
    {
        get
        {
            if (_value.IsEmpty)
            {
                _value = _resolver(_state);
            }
            return _value;
        }
        set => _value = value;
    }
}
