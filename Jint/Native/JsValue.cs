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
    public static JsValue Empty => default;
    internal const int JsFloat64TagAddend = (0x7ff80000 - -9 + 1);
    internal const ulong JsFloat64TagAddendShifted = (0x7ff8000A00000000ul);
    internal const ulong JsNan = unchecked((ulong) (long) (0x7ff8000000000000 - 0x7ff8000A00000000));
    internal const ulong PositiveZeroBits = 0x8007fff600000000;
    internal const ulong NegativeZeroBits = 0x7fff600000000;
    internal bool IsNegativeZero => U == NegativeZeroBits;
    internal bool IsPositiveZero => U == PositiveZeroBits;
    internal bool IsNaN => U == JsNan;
    public static JsValue NaN => new JsValue(JsNan);
    public static JsValue PositiveZero => new JsValue(PositiveZeroBits);
    public static JsValue NegativeZero => new JsValue(NegativeZeroBits);

    public string? AsString()
    {
        return Obj as string;
    }

    public JsSymbol AsSymbol()
    {
        return new JsSymbol(Obj as string);
    }

    public JsValue UninitializedToUndefined()
    {
        return IsEmpty ? Undefined : this;
    }

    public bool IsEmptyOrUndefined => U is 0 or ((ulong) Tag.JS_TAG_UNDEFINED) << 32;
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
            JsValue jv => jv,
            string s => s,
            JsSymbol sym => sym,
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

    public static implicit operator JsValue(bool b)
    {
        return new JsValue(b);
    }

    public static implicit operator JsValue(double d)
    {
        return new JsValue(d);
    }

    public static implicit operator JsValue(string obj)
    {
        return new JsValue(obj, Tag.JS_TAG_STRING);
    }

    public static implicit operator JsValue(JsString jsString)
    {
        return new JsValue(jsString._value, Tag.JS_TAG_STRING);
    }

    public static implicit operator JsValue(JsSymbol obj)
    {
        return new JsValue(obj._value!, Tag.JS_TAG_SYMBOL);
    }

    public static implicit operator JsValue(JsObjectBase obj)
    {
        return new JsValue(obj, Tag.JS_TAG_OBJECT);
    }

    public static implicit operator JsValue(BigInteger bigInteger)
    {
        return new JsBigInt(bigInteger).ToJsValue();
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

        // if (y.IsObject() && (x._type & InternalTypes.Primitive) != InternalTypes.Empty)
        // {
        //     return x.IsLooselyEqual(TypeConverter.ToPrimitive(y));
        // }
        //
        // if (x.IsObject() && (y._type & InternalTypes.Primitive) != InternalTypes.Empty)
        // {
        //     return y.IsLooselyEqual(TypeConverter.ToPrimitive(x));
        // }

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

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    public Types Type
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return Tag switch
            {
                Tag.JS_TAG_UNDEFINED => Types.Undefined,
                Tag.JS_TAG_NULL => Types.Null,
                Tag.JS_TAG_BOOL => Types.Boolean,
                Tag.JS_TAG_STRING or Tag.JS_TAG_STRING_CONCAT => Types.String,
                Tag.JS_TAG_SYMBOL => Types.Symbol,
                Tag.JS_TAG_BIG_INT => Types.BigInt,
                Tag.JS_TAG_OBJECT => Types.Object,
                _ => Types.Number,
            };
        }
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
            iterator = new IteratorInstance.StringIterator(realm.GlobalEnv._engine, Obj!.ToString() ?? "");
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

                    JsValue jsObj = obj;
                    var syncIteratorRecord = jsObj.GetIterator(realm, GeneratorKind.Sync, syncMethod);
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

    internal bool IsEmpty => U == 0;
    internal bool IsNotEmpty => U != 0;

    public bool TryReadString([NotNullWhen(true)] out string? value)
    {
        if (Tag is Tag.JS_TAG_STRING)
        {
            value = (string) Obj!;
            return true;
        }

        value = null;
        return false;
    }

    public bool TryReadNumber(out double value)
    {
        if (Tag is Tag.JS_TAG_STRING)
        {
            value = GetFloat64Value();
            return true;
        }

        value = 0;
        return false;
    }

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

    public bool ValueEquals(JsValue other)
    {
        return U == other.U && Equals(Obj, other.Obj);
    }

    public bool Equals(JsValue other)
    {
        if (IsNaN || other.IsNaN) return false;
        return U == other.U && Equals(Obj, other.Obj);
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


    [Pure]
    internal IteratorInstance GetIteratorFromMethod(Realm realm, ICallable method)
    {
        var iterator = method.Call(this);
        if (iterator.Obj is not ObjectInstance objectInstance)
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

        return Undefined;
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
            return (JsObjectBase);
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

        return JsValue.Undefined;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-instanceofoperator
    /// </summary>
    internal bool InstanceofOperator(JsValue target)
    {
        var targetObject = target.Obj;
        if (targetObject is not ObjectInstance oi)
        {
            Throw.TypeErrorNoEngine("Right-hand side of 'instanceof' is not an object");
            return false;
        }

        var instOfHandler = oi.GetMethod(GlobalSymbolRegistry.HasInstance);
        if (instOfHandler is not null)
        {
            return TypeConverter.ToBoolean(instOfHandler.Call(target, this));
        }

        if (!oi.IsCallable)
        {
            Throw.TypeErrorNoEngine("Right-hand side of 'instanceof' is not callable");
        }

        return oi.OrdinaryHasInstance(oi);
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

        var callable = JsObjectBase.Obj as ICallable;
        if (callable is null)
        {
            Throw.TypeError(realm, $"Value returned for property '{p}' of object is not a function");
        }

        return callable;
    }

    public bool IsCallable => Obj is ICallable or JsObjectBase { IsCallable: true };
    public bool IsConstructor => Obj is IConstructor or JsObjectBase { IsConstructor: true };

    internal static bool SameValue(JsValue x, JsValue y)
    {
        if (x.U == y.U && ReferenceEquals(x.Obj, y.Obj))
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
                // if (x._type == y._type && x._type == InternalTypes.Integer)
                // {
                //     return x.AsInteger() == y.AsInteger();
                // }

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
                return x.Obj is ObjectWrapper xo && y.Obj is ObjectWrapper yo && ReferenceEquals(xo.Target, yo.Target);
            case Types.BigInt:
                return (x.AsBigInt().Equals(y.AsBigInt()));
            default:
                return false;
        }
    }

    internal static IConstructor AssertConstructor(Engine engine, JsValue c)
    {
        if (!c.IsConstructor)
        {
            Throw.TypeError(engine.Realm, c + " is not a constructor");
        }

        return (IConstructor) c.Obj!;
    }

    public override string ToString()
    {
        switch (Tag)
        {
            case Tag.JS_TAG_UNDEFINED:
                return "undefined";
            case Tag.JS_TAG_NULL:
                return "null";
            case Tag.JS_TAG_BOOL:
                return GetBoolValue() ? "true" : "false";
            case >= Tag.JS_TAG_FLOAT64:
                return TypeConverter.ToString(GetFloat64Value());
            case Tag.JS_TAG_STRING or Tag.JS_TAG_STRING_CONCAT:
                return (string) Obj!;
            case Tag.JS_TAG_SYMBOL:
                return new JsSymbol(Obj as string).ToString();
            case Tag.JS_TAG_OBJECT:
                return Obj!.ToString() ?? "";
            case Tag.JS_TAG_BIG_INT:
                return AsBigInt().ToString();
            default:
                throw new NotImplementedException();
        }
    }
}
