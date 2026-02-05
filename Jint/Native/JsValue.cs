using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Jint.Native.Generator;
using Jint.Native.Iterator;
using Jint.Native.Number;
using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint.Native;

[StructLayout(LayoutKind.Explicit)]
public readonly struct JsValue : IEquatable<JsValue>
{
    public static JsValue Undefined => new JsValue(null!, Tag.JS_TAG_UNDEFINED);
    public static JsValue Null => new JsValue(null!, Tag.JS_TAG_NULL);
    public static JsValue True => new JsValue(true);
    public static JsValue False => new JsValue(false);
    public static JsValue Empty => new JsValue(unchecked((ulong) ((uint) -4) << 32));
    internal const int JsFloat64TagAddend = (0x7ff80000 - -9 + 1);
    internal const ulong JsFloat64TagAddendShifted = (0x7ff8000A00000000ul);
    internal const ulong JsNan = unchecked((ulong) (long) (0x7ff8000000000000 - 0x7ff8000A00000000));
    internal const ulong PositiveZeroBits = 0x8007fff600000000;
    internal const ulong NegativeZeroBits = 0x7fff600000000;
    internal bool IsNegativeZero => U == NegativeZeroBits;
    internal bool IsPositiveZero => U == PositiveZeroBits;
    public static JsValue PositiveZero => new JsValue(PositiveZeroBits);
    public static JsValue NegativeZero => new JsValue(NegativeZeroBits);

    [FieldOffset(0)] public readonly double D;
    [FieldOffset(0)] public readonly ulong U;
    [FieldOffset(8)] public readonly object? Obj;
    internal Tag Tag => (Tag) (U >> 32);

    public bool IsFloat64 => (U >> 32) >= 8;
    internal JsValue(ulong u) => U = u;

    public JsValue(double d)
    {
#if NET8_0_OR_GREATER
         ulong u64 = Unsafe.BitCast<double, ulong>(d);
#else
        ulong u64 = Unsafe.As<double, ulong>(ref d);
#endif
        if ((u64 & 0x7ffffffffffffff) > 0x7ff0000000000000)
        {
            U = JsNan;
        }
        else
        {
            U = u64 - JsFloat64TagAddendShifted;
        }
    }

    public JsValue(bool v)
    {
        U = (1ul << 32) + (v ? 1ul : 0);
    }

    internal JsValue(object? o, Tag tag)
    {
        U = ((ulong) tag << 32);
        Obj = o;
    }

    internal JsValue(ulong u, object? o)
    {
        U = u;
        Obj = o;
    }

    public double GetFloat64Value()
    {
        ulong u = U;
        u += JsFloat64TagAddendShifted;
#if NET8_0_OR_GREATER
        return Unsafe.BitCast<ulong, double>(u);
#else
        return Unsafe.As<ulong, double>(ref u);
#endif
    }

    internal int GetInt32Value()
    {
        return (int) (uint) (U);
    }

    public bool GetBoolValue()
    {
        return (uint) (U) != 0;
    }

    internal JsBigInt AsBigInt()
    {
        return new JsBigInt((int) U, (uint[]) Obj!);
    }

    public static JsValue FromObject(object o)
    {
        return o switch
        {
            null => Null,
            bool b => new JsValue(b),
            int i => new JsValue(i),
            double d => new JsValue(d),
            _ => new JsValue(o, Tag.JS_TAG_OBJECT)
        };
    }

    internal static JsValue FromObjectReferenceType(object o)
    {
        return o switch
        {
            null => Null,
            string b => b,
            JsSymbol s => s,
            _ => new JsValue(o, Tag.JS_TAG_OBJECT)
        };
    }

    public static implicit operator JsValue(double d)
    {
        return new JsValue(d);
    }

    public static implicit operator JsValue(string obj)
    {
        return new JsValue(obj, Tag.JS_TAG_STRING);
    }

    public static implicit operator JsValue(JsSymbol obj)
    {
        return new JsValue(obj._value!, Tag.JS_TAG_SYMBOL);
    }

    public static implicit operator JsValue(ObjectInstance obj)
    {
        return new JsValue(obj, Tag.JS_TAG_OBJECT);
    }

    internal bool IsLooselyEqual(JsValue value)
    {
        if (Obj != null && ReferenceEquals(Obj, value.Obj))
        {
            return true;
        }

        // TODO move to type specific IsLooselyEqual

        var x = this;
        var y = value;

        var xTag = x.Tag;
        var yTag = y.Tag;
        if (xTag == Tag.JS_TAG_BIG_INT || yTag == Tag.JS_TAG_BIG_INT)
        {
            // TODO BigInt loose equality
            return false;
        }

        if (xTag is Tag.JS_TAG_BOOL or >= Tag.JS_TAG_FLOAT64 || yTag is Tag.JS_TAG_BOOL or >= Tag.JS_TAG_FLOAT64)
        {
            var numX = TypeConverter.ToNumber(x);
            var numY = TypeConverter.ToNumber(y);
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return numX == numY;
        }

        if (y.IsObject() && (x._type & InternalTypes.Primitive) != InternalTypes.Empty)
        {
            return x.IsLooselyEqual(TypeConverter.ToPrimitive(y));
        }

        if (x.IsObject() && (y._type & InternalTypes.Primitive) != InternalTypes.Empty)
        {
            return y.IsLooselyEqual(TypeConverter.ToPrimitive(x));
        }

        return false;
    }

    internal bool ToBoolean()
    {
        switch (Tag)
        {
            case Tag.JS_TAG_BOOL: return GetInt32Value() != 0;
            case >= Tag.JS_TAG_FLOAT64:
                {
                    var d = GetFloat64Value();
                    return !double.IsNaN(d) && d != 0;
                }
            case Tag.JS_TAG_UNDEFINED:
            case Tag.JS_TAG_NULL: return false;
            default: return true;
        }
    }

    public JsValue Clone()
    {
        if (Obj is ICloneable cloneable)
        {
            return new JsValue(cloneable.Clone(), Tag);
        }

        return this;
    }

    [Pure]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IteratorInstance GetIterator(Realm realm, GeneratorKind hint = GeneratorKind.Sync,
        ICallable? method = null)
    {
        if (!TryGetIterator(realm, out var iterator, hint, method))
        {
            Throw.TypeError(realm, "The value is not iterable");
            return null!;
        }

        return iterator;
    }

    [Pure]
    internal bool TryGetIterator(
        Realm realm,
        [NotNullWhen(true)] out IteratorInstance? iterator,
        GeneratorKind hint = GeneratorKind.Sync,
        ICallable? method = null)
    {
        var tag = Tag;
        if (tag is Tag.JS_TAG_STRING or Tag.JS_TAG_STRING_CONCAT &&
            realm.Intrinsics.String.PrototypeObject.HasOriginalIterator)
        {
            iterator = new IteratorInstance.StringIterator(realm.GlobalEnv._engine, Obj!.ToString());
            return true;
        }

        var obj = TypeConverter.ToObject(realm, this);

        if (method is null)
        {
            if (hint == GeneratorKind.Async)
            {
                method = obj.GetMethod(GlobalSymbolRegistry.AsyncIterator);
                if (method is null)
                {
                    var syncMethod = obj.GetMethod(GlobalSymbolRegistry.Iterator);
                    if (syncMethod is null)
                    {
                        iterator = null;
                        return false;
                    }

                    var syncIteratorRecord = new JsValue(obj).GetIterator(realm, GeneratorKind.Sync, syncMethod));
                    // CreateAsyncFromSyncIterator - wrap the sync iterator in an async adapter
                    var asyncFromSync = new AsyncFromSyncIterator(obj.Engine, syncIteratorRecord);
                    iterator = new IteratorInstance.ObjectIterator(asyncFromSync);
                    return true;
                }
            }
            else
            {
                method = obj.GetMethod(GlobalSymbolRegistry.Iterator);
            }
        }

        if (method is null)
        {
            iterator = null;
            return false;
        }

        var iteratorResult = method.Call(obj, Arguments.Empty).Obj as ObjectInstance;
        if (iteratorResult is null)
        {
            Throw.TypeError(realm, "Result of the Symbol.iterator method is not an object");
        }

        if (iteratorResult is IteratorInstance i)
        {
            iterator = i;
        }
        else
        {
            iterator = new IteratorInstance.ObjectIterator(iteratorResult);
        }

        return true;
    }

    internal bool IsEmpty => U == unchecked((ulong) ((uint) -4) << 32);

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
    public JsValue Get(JsValue property, JsValue receiver)
    {
        return Undefined;
    }

    public bool Equals(JsValue other)
    {
        return D.Equals(other.D) && U == other.U && Equals(Obj, other.Obj);
    }

    public override bool Equals(object? obj)
    {
        return obj is JsValue other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(D, U, Obj);
    }

    public static bool operator ==(JsValue left, JsValue right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(JsValue left, JsValue right)
    {
        return !(left == right);
    }

    public override string ToString()
    {
        return base.ToString();
    }
}

internal enum Tag
{
    /* all tags with a reference count are negative */
    JS_TAG_FIRST = -9, /* first negative tag */
#pragma warning disable CA1069
    JS_TAG_BIG_INT = -9,
#pragma warning restore CA1069
    JS_TAG_SYMBOL = -8,
    JS_TAG_STRING = -7,
    JS_TAG_STRING_CONCAT = -6,
    JS_TAG_REFERENCE = -5, /* used internally */
    JS_EMPTY = -4, /* used internally */
    JS_TAG_MODULE = -3, /* used internally */
    JS_TAG_FUNCTION_BYTECODE = -2, /* used internally */
    JS_TAG_OBJECT = -1,

    //JS_TAG_INT = 0,
    JS_TAG_BOOL = 1,
    JS_TAG_NULL = 2,
    JS_TAG_UNDEFINED = 3,

    // JS_TAG_UNINITIALIZED = 4,
    // JS_TAG_CATCH_OFFSET = 5,
    // JS_TAG_EXCEPTION = 6,
    // JS_TAG_SHORT_BIG_INT = 7,
    JS_TAG_FLOAT64 = 8,
    /* any larger tag is FLOAT64 if JS_NAN_BOXING */
};

public abstract partial class JsObjectBase : IEquatable<JsObjectBase>
{
    protected static JsValue Undefined => JsValue.Undefined;

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

    internal bool IsEmpty => ReferenceEquals(this, JsEmpty.Instance);

    [Pure]
    internal IteratorInstance GetIteratorFromMethod(Realm realm, ICallable method)
    {
        var iterator = method.Call(this);
        if (iterator is not ObjectInstance objectInstance)
        {
            Throw.TypeError(realm);
            return null!;
        }

        return new IteratorInstance.ObjectIterator(objectInstance);
    }


    internal static JsValue ConvertAwaitableToPromise(Engine engine, object obj)
    {
        if (obj is Task task)
        {
            return ConvertTaskToPromise(engine, task);
        }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
        if (obj is ValueTask valueTask)
        {
            return ConvertTaskToPromise(engine, valueTask.AsTask());
        }

        // ValueTask<T>
        var asTask = obj.GetType().GetMethod(nameof(ValueTask<object>.AsTask));
        if (asTask is not null)
        {
            return ConvertTaskToPromise(engine, (Task) asTask.Invoke(obj, parameters: null)!);
        }
#endif

        return FromObject(engine, JsValue.Undefined);
    }

    internal static JsValue ConvertTaskToPromise(Engine engine, Task task)
    {
        // Use RegisterPromiseWithClrValue to ensure FromObject is called on the main thread,
        // not on the background thread that completes the Task.
        var (promise, resolveClr, rejectClr) = engine.RegisterPromiseWithClrValue();
        task = task.ContinueWith(continuationAction =>
            {
                if (continuationAction.IsFaulted)
                {
                    rejectClr(continuationAction.Exception);
                }
                else if (continuationAction.IsCanceled)
                {
                    rejectClr(new ExecutionCanceledException());
                }
                else
                {
                    // Special case: Marshal `async Task` as undefined, as this is `Task<VoidTaskResult>` at runtime
                    // See https://github.com/sebastienros/jint/pull/1567#issuecomment-1681987702
                    if (Task.CompletedTask.Equals(continuationAction))
                    {
                        resolveClr(Undefined);
                        return;
                    }

                    var result = continuationAction.GetType().GetProperty(nameof(Task<>.Result));
                    if (result is not null)
                    {
                        resolveClr(result.GetValue(continuationAction));
                    }
                    else
                    {
                        resolveClr(Undefined);
                    }
                }
            },
            // Ensure continuation is completed before unwrapping Promise
            continuationOptions: TaskContinuationOptions.AttachedToParent |
                                 TaskContinuationOptions.ExecuteSynchronously);

        return promise;
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public Types Type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _type == InternalTypes.Integer
            ? Types.Number
            : (Types) (_type & ~InternalTypes.InternalFlags);
    }

    /// <summary>
    /// Creates a valid <see cref="JsObjectBase"/> instance from any <see cref="Object"/> instance
    /// </summary>
    public static JsValue FromObject(Engine engine, object? value)
    {
        return FromObjectWithType(engine, value, null);
    }

    /// <summary>
    /// Creates a valid <see cref="JsObjectBase"/> instance from any <see cref="Object"/> instance, with a type
    /// </summary>
    public static JsValue FromObjectWithType(Engine engine, object? value, Type? type)
    {
        if (value is null)
        {
            return JsValue.Null;
        }

        if (value is JsObjectBase JsObjectBase)
        {
            return new JsValue(JsObjectBase);
        }

        if (engine._objectConverters != null)
        {
            foreach (var converter in engine._objectConverters)
            {
                if (converter.TryConvert(engine, value, out var result))
                {
                    return result;
                }
            }
        }

        if (DefaultObjectConverter.TryConvert(engine, value, type, out var defaultConversion))
        {
            return defaultConversion;
        }

        return null!;
    }

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

    /// <summary>
    /// https://tc39.es/ecma262/#sec-instanceofoperator
    /// </summary>
    internal bool InstanceofOperator(JsValue target)
    {
        if (target is not ObjectInstance oi)
        {
            Throw.TypeErrorNoEngine("Right-hand side of 'instanceof' is not an object");
            return false;
        }

        var instOfHandler = oi.GetMethod(GlobalSymbolRegistry.HasInstance);
        if (instOfHandler is not null)
        {
            return TypeConverter.ToBoolean(instOfHandler.Call(target, this));
        }

        if (!target.IsCallable)
        {
            Throw.TypeErrorNoEngine("Right-hand side of 'instanceof' is not callable");
        }

        return target.OrdinaryHasInstance(this);
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-getmethod
    /// </summary>
    internal static ICallable? GetMethod(Realm realm, JsObjectBase v, JsObjectBase p)
    {
        // GetMethod uses GetV which converts primitives to objects
        // https://tc39.es/ecma262/#sec-getv
        var target = v is ObjectInstance obj ? obj : TypeConverter.ToObject(realm, v);
        var JsObjectBase = target.Get(p, v);
        if (JsObjectBase.IsNullOrUndefined())
        {
            return null;
        }

        var callable = JsObjectBase as ICallable;
        if (callable is null)
        {
            Throw.TypeError(realm, $"Value returned for property '{p}' of object is not a function");
        }

        return callable;
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

    public static implicit operator JsObjectBase(char value)
    {
        return JsString.Create(value);
    }

    public static implicit operator JsObjectBase(int value)
    {
        return JsNumber.Create(value);
    }

    public static implicit operator JsObjectBase(uint value)
    {
        return JsNumber.Create(value);
    }

    public static implicit operator JsObjectBase(double value)
    {
        return JsNumber.Create(value);
    }

    public static implicit operator JsObjectBase(long value)
    {
        return JsNumber.Create(value);
    }

    public static implicit operator JsObjectBase(ulong value)
    {
        return JsNumber.Create(value);
    }

    public static implicit operator JsObjectBase(BigInteger value)
    {
        return JsBigInt.Create(value);
    }

    public static implicit operator JsObjectBase(bool value)
    {
        return value ? JsBoolean.True : JsBoolean.False;
    }

    [DebuggerStepThrough]
    public static implicit operator JsObjectBase(string? value)
    {
        return value == null ? Null : JsString.Create(value);
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
        if (p is not ObjectInstance)
        {
            Throw.TypeError(o.Engine.Realm,
                $"Function has non-object prototype '{TypeConverter.ToString(new JsValue(p))}' in instanceof check");
        }

        while (true)
        {
            o = o.Prototype;

            if (o is null)
            {
                return false;
            }

            if (SameValue(p, o))
            {
                return true;
            }
        }
    }

    internal static bool SameValue(JsValue x, JsValue y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        var typea = x.Type;
        var typeb = y.Type;

        if (typea != typeb)
        {
            return false;
        }

        switch (typea)
        {
            case Types.Number:
                if (x._type == y._type && x._type == InternalTypes.Integer)
                {
                    return x.AsInteger() == y.AsInteger();
                }

                var nx = TypeConverter.ToNumber(x);
                var ny = TypeConverter.ToNumber(y);

                if (double.IsNaN(nx) && double.IsNaN(ny))
                {
                    return true;
                }

                if (nx == ny)
                {
                    if (nx == 0)
                    {
                        // +0 !== -0
                        return NumberInstance.IsNegativeZero(nx) == NumberInstance.IsNegativeZero(ny);
                    }

                    return true;
                }

                return false;
            case Types.String:
                return string.Equals(TypeConverter.ToString(x), TypeConverter.ToString(y), StringComparison.Ordinal);
            case Types.Boolean:
                return TypeConverter.ToBoolean(x) == TypeConverter.ToBoolean(y);
            case Types.Undefined:
            case Types.Null:
                return true;
            case Types.Symbol:
                return x == y;
            case Types.Object:
                return x is ObjectWrapper xo && y is ObjectWrapper yo && ReferenceEquals(xo.Target, yo.Target);
            case Types.BigInt:
                return (x is JsBigInt xBigInt && y is JsBigInt yBigInt && xBigInt.Equals(yBigInt));
            default:
                return false;
        }
    }

    internal static IConstructor AssertConstructor(Engine engine, JsObjectBase c)
    {
        if (!c.IsConstructor)
        {
            Throw.TypeError(engine.Realm, c + " is not a constructor");
        }

        return (IConstructor) c;
    }
}
