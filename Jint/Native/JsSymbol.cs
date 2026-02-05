using System.Runtime.CompilerServices;
using Jint.Runtime;

namespace Jint.Native;

public readonly struct JsSymbol : IEquatable<JsSymbol>
{
    internal readonly string? _value;

    internal JsSymbol(string? value)
    {
        _value = value;
    }

    /// <summary>
    /// https://tc39.es/ecma262/#sec-symboldescriptivestring
    /// </summary>
    public override string ToString()
    {
        if (_value == null) return "Symbol()";
        return "Symbol(" + _value + ")";
    }

    internal JsValue ToJsValue()
    {
        if (_value == null) return JsValue.Undefined;
        return _value;
    }

    public bool Equals(JsSymbol other)
    {
        return string.Equals(_value, other._value, StringComparison.Ordinal);
    }

    public override bool Equals(object? obj)
    {
        return obj is JsSymbol other && Equals(other);
    }

    public override int GetHashCode()
    {
        return (_value != null ? StringComparer.Ordinal.GetHashCode(_value) : 0);
    }

    public static bool operator ==(JsSymbol left, JsSymbol right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(JsSymbol left, JsSymbol right)
    {
        return !(left == right);
    }
}
