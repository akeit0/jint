using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;
using Jint.Native;
using Jint.Native.Object;
using Jint.Runtime;
using Jint.Runtime.Interop;

namespace Jint;

internal static class DefaultObjectConverter
{
    private static Dictionary<Type, Func<Engine, object, JsValue>> _typeMappers = new()
    {
        { typeof(bool), (engine, v) => (bool) v ? JsValue.True : JsValue.False },
        { typeof(byte), (engine, v) => ((byte) v) },
        { typeof(char), (engine, v) => ((char) v) },
        { typeof(DateTime), (engine, v) => engine.Realm.Intrinsics.Date.Construct((DateTime) v) },
        { typeof(DateTimeOffset), (engine, v) => engine.Realm.Intrinsics.Date.Construct((DateTimeOffset) v) },
        { typeof(decimal), (engine, v) => (JsValue) (double) (decimal) v },
        { typeof(double), (engine, v) => (JsValue) (double) v },
        { typeof(short), (engine, v) => ((short) v) },
        { typeof(int), (engine, v) => ((int) v) },
        { typeof(long), (engine, v) => (JsValue) (long) v },
        { typeof(sbyte), (engine, v) => ((sbyte) v) },
        { typeof(float), (engine, v) => (JsValue) (float) v },
        { typeof(string), (engine, v) => ((string) v) },
        { typeof(ushort), (engine, v) => ((ushort) v) },
        { typeof(uint), (engine, v) => ((uint) v) },
        { typeof(ulong), (engine, v) => ((ulong) v) },
        {
            typeof(System.Text.RegularExpressions.Regex),
            (engine, v) => engine.Realm.Intrinsics.RegExp.Construct((System.Text.RegularExpressions.Regex) v,
                ((System.Text.RegularExpressions.Regex) v).ToString(), "")
        }
    };

    public static bool TryConvert(Engine engine, object value, Type? type, out JsValue result)
    {
        result = JsValue.Undefined;
        Type valueType = ObjectWrapper.GetClrType(value, type);

        var typeMappers = _typeMappers;

        if (typeMappers.TryGetValue(valueType, out var typeMapper))
        {
            result = typeMapper(engine, value);
        }
        else
        {
            if (value is Array a)
            {
                // racy, we don't care, worst case we'll catch up later
                Interlocked.CompareExchange(ref _typeMappers,
                    new Dictionary<Type, Func<Engine, object, JsValue>>(typeMappers) { [valueType] = ConvertArray },
                    typeMappers);

                result = ConvertArray(engine, a);
                return !result.IsUndefined();
            }

            if (value is IConvertible convertible && TryConvertConvertible(engine, convertible, out result))
            {
                return true;
            }

            if (value is Delegate d)
            {
                result = new DelegateWrapper(engine, d);
                return true;
            }

            if ((engine.Options.ExperimentalFeatures & ExperimentalFeature.TaskInterop) != ExperimentalFeature.None)
            {
                if (value is Task task)
                {
                    result = JsValue.ConvertAwaitableToPromise(engine, task);
                    return !result.IsUndefined();
                }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP
                if (value is ValueTask valueTask)
                {
                    result = JsValue.ConvertAwaitableToPromise(engine, valueTask);
                    return !result.IsUndefined();
                }

                // ValueTask<T> is not derived from ValueTask, so we need to check for it explicitly
                var valueType2 = value.GetType();
                if (valueType2.IsGenericType && valueType2.GetGenericTypeDefinition() == typeof(ValueTask<>))
                {
                    result = JsValue.ConvertAwaitableToPromise(engine, value);
                    return !result.IsUndefined();
                }
#endif
            }

#if NET8_0_OR_GREATER
            if (value is System.Text.Json.Nodes.JsonValue jsonValue)
            {
                result = ConvertSystemTextJsonValue(engine, jsonValue);
                return !result.IsUndefined();
            }
#endif

            var t = value.GetType();

            if (!engine.Options.Interop.AllowSystemReflection
                && t.Namespace?.StartsWith("System.Reflection", StringComparison.Ordinal) == true)
            {
                const string Message = "Cannot access System.Reflection namespace, check Engine's interop options";
                Throw.InvalidOperationException(Message);
            }

            if (t.IsEnum)
            {
                var ut = Enum.GetUnderlyingType(t);

                if (ut == typeof(ulong))
                {
                    result = (Convert.ToDouble(value, CultureInfo.InvariantCulture));
                }
                else
                {
                    if (ut == typeof(uint) || ut == typeof(long))
                    {
                        result = (Convert.ToInt64(value, CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        result = (Convert.ToInt32(value, CultureInfo.InvariantCulture));
                    }
                }
            }
            else
            {
                // check global cache, have we already wrapped the value?
                if (engine._objectWrapperCache?.TryGetValue(value, out var cached) == true)
                {
                    result = cached;
                }
                else
                {
                    var wrapped = engine.Options.Interop.WrapObjectHandler.Invoke(engine, value, type);

                    if (ReferenceEquals(wrapped?.GetPrototypeOf(), engine.Realm.Intrinsics.Object.PrototypeObject)
                        && engine._typeReferences?.TryGetValue(t, out var typeReference) == true)
                    {
                        wrapped.SetPrototypeOf(typeReference);
                    }

                    result = wrapped!;

                    if (engine.Options.Interop.TrackObjectWrapperIdentity && wrapped is not null)
                    {
                        engine._objectWrapperCache ??= new ConditionalWeakTable<object, ObjectInstance>();
                        engine._objectWrapperCache.Add(value, wrapped);
                    }
                }
            }

            // if no known type could be guessed, use the default of wrapping using ObjectWrapper
        }

        return !result.IsUndefined();
    }

#if NET8_0_OR_GREATER
    private static JsValue ConvertSystemTextJsonValue(Engine engine, System.Text.Json.Nodes.JsonNode value)
    {
        return value.GetValueKind() switch
        {
            System.Text.Json.JsonValueKind.Object => JsValue.FromObject(engine, value),
            System.Text.Json.JsonValueKind.Array => JsValue.FromObject(engine, value),
            System.Text.Json.JsonValueKind.String => (value.ToString()),
#pragma warning disable IL2026, IL3050
            System.Text.Json.JsonValueKind.Number => ((System.Text.Json.Nodes.JsonValue) value).TryGetValue<int>(
                out var intValue)
                ? (intValue)
                : System.Text.Json.JsonSerializer.Deserialize<double>(value),
#pragma warning restore IL2026, IL3050
            System.Text.Json.JsonValueKind.True => JsValue.True,
            System.Text.Json.JsonValueKind.False => JsValue.False,
            System.Text.Json.JsonValueKind.Undefined => JsValue.Undefined,
            System.Text.Json.JsonValueKind.Null => JsValue.Null,
            _ => JsValue.Undefined,
        };
    }
#endif

    private static bool TryConvertConvertible(Engine engine, IConvertible convertible, out JsValue result)
    {
        result = convertible.GetTypeCode() switch
        {
            TypeCode.Boolean => convertible.ToBoolean(engine.Options.Culture) ? JsValue.True : JsValue.False,
            TypeCode.Byte => (convertible.ToByte(engine.Options.Culture)),
            TypeCode.Char => (convertible.ToChar(engine.Options.Culture)),
            TypeCode.Double => (convertible.ToDouble(engine.Options.Culture)),
            TypeCode.SByte => (convertible.ToSByte(engine.Options.Culture)),
            TypeCode.Int16 => (convertible.ToInt16(engine.Options.Culture)),
            TypeCode.Int32 => (convertible.ToInt32(engine.Options.Culture)),
            TypeCode.UInt16 => (convertible.ToUInt16(engine.Options.Culture)),
            TypeCode.Int64 => (convertible.ToInt64(engine.Options.Culture)),
            TypeCode.Single => (convertible.ToSingle(engine.Options.Culture)),
            TypeCode.String => (convertible.ToString(engine.Options.Culture)),
            TypeCode.UInt32 => (convertible.ToUInt32(engine.Options.Culture)),
            TypeCode.UInt64 => (convertible.ToUInt64(engine.Options.Culture)),
            TypeCode.DateTime => engine.Realm.Intrinsics.Date.Construct(convertible.ToDateTime(engine.Options.Culture)),
            TypeCode.Decimal => (double) (convertible.ToDecimal(engine.Options.Culture)),
            TypeCode.DBNull => JsValue.Null,
            TypeCode.Empty => JsValue.Null,
            _ => JsValue.Undefined
        };

        return !result.IsUndefined();
    }

    private static JsValue ConvertArray(Engine e, object v)
    {
        var array = (Array) v;
        var arrayLength = (uint) array.Length;

        var values = new JsValue[arrayLength];
        for (uint i = 0; i < arrayLength; ++i)
        {
            values[i] = JsValue.FromObject(e, array.GetValue(i));
        }

        return new JsArray(e, values);
    }
}
