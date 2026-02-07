using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;
using Jint.Native.Object;
using Jint.Runtime;

namespace Jint.Native;

public abstract partial class JsObjectBase : IEquatable<JsObjectBase>
{
    protected static JsValue Undefined => JsValue.Undefined;
    protected static JsValue Null => JsValue.Null;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal readonly InternalTypes _type;

    protected JsObjectBase(Types type)
    {
        _type = (InternalTypes) type;
    }

    internal JsObjectBase(InternalTypes type)
    {
        _type = type;
    }

    [Pure]
    internal virtual bool IsArray() => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsIntegerIndexedArray => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsConstructor => false;

    // Temporal type checks
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalDuration => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalInstant => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalPlainDate => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalPlainDateTime => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalPlainMonthDay => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalPlainTime => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalPlainYearMonth => false;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsTemporalZonedDateTime => false;


    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public Types Type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type == InternalTypes.Integer
            ? Types.Number
            : (Types) (_type & ~InternalTypes.InternalFlags);
    }

    internal static bool SameValue(JsValue x, JsValue y) => JsValue.SameValue(x, y);

    /// <summary>
    /// Converts a <see cref="JsObjectBase"/> to its underlying CLR value.
    /// </summary>
    /// <returns>The underlying CLR value of the <see cref="JsObjectBase"/> instance.</returns>
    public abstract object? ToObject();

    /// <summary>
    /// Coerces boolean value from <see cref="JsObjectBase"/> instance.
    /// </summary>
    internal virtual bool ToBoolean() => _type > InternalTypes.Null;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getv
    /// </summary>
    internal JsValue GetV(Realm realm, JsValue property)
    {
        var o = TypeConverter.ToObject(realm, this);
        return o.Get(property, this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public JsValue Get(JsValue property)
    {
        return Get(property, this);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-get-o-p
    /// </summary>
    public virtual JsValue Get(JsValue property, JsValue receiver)
    {
        return Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-set-o-p-v-throw
    /// </summary>
    public virtual bool Set(JsValue property, JsValue value, JsValue receiver)
    {
        Throw.NotSupportedException();
        return false;
    }


    public override string ToString()
    {
        return "None";
    }

    public static bool operator ==(JsObjectBase? a, JsObjectBase? b)
    {
        if (a is null)
        {
            return b is null;
        }

        return b is not null && a.Equals(b);
    }

    public static bool operator !=(JsObjectBase? a, JsObjectBase? b)
    {
        return !(a == b);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-islooselyequal
    /// </summary>
    /// <summary>
    /// Strict equality.
    /// </summary>
    public override bool Equals(object? obj) => Equals(obj as JsObjectBase);

    /// <summary>
    /// Strict equality.
    /// </summary>
    public virtual bool Equals(JsObjectBase? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => _type.GetHashCode();

    /// <summary>
    /// Some values need to be cloned in order to be assigned, like ConcatenatedString.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal JsObjectBase Clone()
    {
        // concatenated string and arguments currently may require cloning
        return (_type & InternalTypes.RequiresCloning) == InternalTypes.Empty
            ? this
            : DoClone();
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getmethod
    /// </summary>
    internal static ICallable? GetMethod(Realm realm, JsValue v, JsValue p)
    {
        // GetMethod uses GetV which converts primitives to objects
        // https://tc39.es/ecma262/#sec-getv
        var target = v.Obj is ObjectInstance obj ? obj : TypeConverter.ToObject(realm, v);
        var JsObjectBase = target.Get(p, v);
        if (JsObjectBase.IsNullOrUndefined())
        {
            return null;
        }

        var callable = JsObjectBase.Obj as ICallable;
        if (callable is null)
        {
            Throw.TypeError(realm, $"Value returned for property '{p}' of object is not a function");
        }

        return callable;
    }

    internal virtual JsObjectBase DoClone() => this;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    internal virtual bool IsCallable => this is ICallable;

    /// <summary>
    /// https://tc39.es/ecma262/#sec-ordinaryhasinstance
    /// </summary>
    internal virtual bool OrdinaryHasInstance(JsObjectBase v)
    {
        if (!IsCallable)
        {
            return false;
        }

        var o = v as ObjectInstance;
        if (o is null)
        {
            return false;
        }

        var p = Get(CommonProperties.Prototype);
        if (p.Obj is not ObjectInstance)
        {
            Throw.TypeError(o.Engine.Realm,
                $"Function has non-object prototype '{TypeConverter.ToString(p)}' in instanceof check");
        }

        while (true)
        {
            o = o.Prototype;

            if (o is null)
            {
                return false;
            }

            if (JsValue.SameValue(p, o))
            {
                return true;
            }
        }
    }
}
