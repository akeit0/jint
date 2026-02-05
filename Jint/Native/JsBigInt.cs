using System.Numerics;
using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Native;

public readonly struct JsBigInt : IEquatable<JsBigInt>
{
    internal readonly BigInteger value => Unsafe.As<JsBigInt, BigInteger>(ref Unsafe.AsRef(this));

#pragma warning disable CS0169 // Field is never used
    private readonly int sign;
    private readonly uint[]? bits;
#pragma warning restore CS0169 // Field is never used
    public static readonly JsBigInt Zero = new(0);
    public static readonly JsBigInt One = new(1);

    public JsBigInt(BigInteger value)
    {
        this = Unsafe.As<BigInteger, JsBigInt>(ref value);
    }

    internal JsBigInt(int sign, uint[]? bits)
    {
        this.sign = sign;
        this.bits = bits;
    }

    internal static JsBigInt Create(BigInteger bigInt)
    {
        return Unsafe.As<BigInteger, JsBigInt>(ref bigInt);
    }

    public JsValue ToJsValue()
    {
        return new JsValue(unchecked((((ulong)(uint)Tag.JS_TAG_BIG_INT)<< 32 | (uint)sign)), bits);
    }

    public object ToObject() => value;

    internal bool ToBoolean() => value != 0;

    public static bool operator ==(JsBigInt a, double b)
    {
        return TypeConverter.IsIntegralNumber(b) && a.value == (long) b;
    }

    public static bool operator !=(JsBigInt a, double b)
    {
        return !(a == b);
    }

    public override string ToString()
    {
        return TypeConverter.ToString(value);
    }

    internal bool IsLooselyEqual(JsValue value)
    {
        if (value.IsBigInt())
        {
            return Equals(value.AsBigInt());
        }

        if (value.IsFloat64 && TypeConverter.IsIntegralNumber(value.GetFloat64Value()) &&
            this.value == new BigInteger(value.GetFloat64Value()))
        {
            return true;
        }

        if (value.IsBoolean())
        {
            return value.GetBoolValue() && this.value == BigInteger.One || !value.GetBoolValue() && this.value == BigInteger.Zero;
        }

        if (value.IsString() && TypeConverter.TryStringToBigInt(value.ToString(), out var temp) && temp == this.value)
        {
            return true;
        }

        if (value.IsObject())
        {
            return IsLooselyEqual(TypeConverter.ToPrimitive(value, Types.Number));
        }

        return false;
    }


    public bool Equals(JsBigInt other)
    {
        return value == other.value;
    }

    public override int GetHashCode() => value.GetHashCode();

    public override bool Equals(object obj)
    {
        return obj is JsBigInt && Equals((JsBigInt) obj);
    }
}
