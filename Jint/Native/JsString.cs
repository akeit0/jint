using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Jint.Native.Generator;
using Jint.Native.Iterator;
using Jint.Runtime;

namespace Jint.Native;

[DebuggerDisplay("{ToString()}")]
public readonly struct JsString : IEquatable<JsString>, IEquatable<string>
{
    internal readonly object? _union = null;
    internal readonly bool _isString = true;
    private const int AsciiMax = 126;
    private static readonly string[] _charToStringValue;
    private static readonly string[] _intToStringJsValue;

    public static readonly JsString Empty;

    static JsString()
    {
        _charToStringValue = new string[AsciiMax + 1];

        for (var i = 0; i <= AsciiMax; i++)
        {
            _charToStringValue[i] = (((char) i).ToString());
        }

        _intToStringJsValue = new string[1024];
        for (var i = 0; i < _intToStringJsValue.Length; ++i)
        {
            _intToStringJsValue[i] =(TypeConverter.ToString(i));
        }
    }

    public JsString(string value) : this(value, true)
    {
    }

    private JsString(object value, bool type)
    {
        _union = value;
        _isString = type;
    }

    public JsString(char value)
    {
        _union = value.ToString();
        _isString = true;
    }

    public static bool operator ==(JsString a, JsString b)
    {
        return a.Equals(b);
    }

    public static bool operator ==(JsValue a, JsString b)
    {

        return a.Equals(b);
    }

    public static bool operator ==(JsString a, JsValue b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(JsString a, JsValue b)
    {
        return !(a == b);
    }

    public static bool operator ==(JsString a, string? b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(JsString a, string? b)
    {
        return !(a == b);
    }

    public static bool operator !=(JsValue a, JsString b)
    {
        return !(a == b);
    }

    public static bool operator !=(JsString a, JsString b)
    {
        return !(a == b);
    }

    internal static JsString Create(string value)
    {
        return new JsString(value);
    }

    internal static JsString CachedCreate(string value)
    {
        if (value.Length is < 2 or > 10)
        {
            return Create(value);
        }

        return _stringCache.GetOrAdd(value, static x => new JsString(x));
    }

    internal static JsString Create(char value)
    {
        var temp = _charToStringValue;
        if (value < (uint) temp.Length)
        {
            return new JsString(temp[value]);
        }

        return new JsString(value);
    }

    internal static JsString Create(int value)
    {
        var temp = _intToStringJsValue;
        if (value < (uint) temp.Length)
        {
            return new JsString(temp[value]);
        }

        return new JsString(TypeConverter.ToString(value));
    }

    internal static JsValue Create(uint value)
    {
        var temp = _intToStringJsValue;
        if (value < (uint) temp.Length)
        {
            return temp[value];
        }

        return (TypeConverter.ToString(value));
    }

    internal static JsValue Create(ulong value)
    {
        var temp = _intToStringJsValue;
        if (value < (uint) temp.Length)
        {
            return temp[value];
        }

        return (TypeConverter.ToString(value));
    }


    public char this[int index] => _value[index];

    public int Length => _value.Length;

    internal JsString Append(JsValue jsValue)
    {
        return new ConcatenatedString(string.Concat(ToString(), TypeConverter.ToString(jsValue)));
    }

    internal JsString EnsureCapacity(int capacity)
    {
        return new ConcatenatedString(_value, capacity);
    }

    public sealed override object ToObject() => ToString();

    internal sealed override bool ToBoolean()
    {
        return Length > 0;
    }

    public override string ToString() => _value;

    internal bool Contains(char c)
    {
        if (c == 0)
        {
            return false;
        }
        return ToString().Contains(c);
    }

    internal int IndexOf(string value, int startIndex = 0)
    {
        if (Length - startIndex < value.Length)
        {
            return -1;
        }
        return ToString().IndexOf(value, startIndex, StringComparison.Ordinal);
    }

    internal bool StartsWith(string value, int start = 0)
    {
        return value.Length + start <= Length && ToString().AsSpan(start).StartsWith(value.AsSpan(), StringComparison.Ordinal);
    }

    internal bool EndsWith(string value, int end = 0)
    {
        var start = end - value.Length;
        return start >= 0 && ToString().AsSpan(start, value.Length).EndsWith(value.AsSpan(), StringComparison.Ordinal);
    }

    internal string Substring(int startIndex, int length)
    {
        return ToString().Substring(startIndex, length);
    }

    internal string Substring(int startIndex)
    {
        return ToString().Substring(startIndex);
    }

    internal bool TryGetIterator(
        Realm realm,
        [NotNullWhen(true)] out IteratorInstance? iterator,
        GeneratorKind hint = GeneratorKind.Sync,
        ICallable? method = null)
    {
        if (realm.Intrinsics.String.PrototypeObject.HasOriginalIterator)
        {
            iterator = new IteratorInstance.StringIterator(realm.GlobalEnv._engine, ToString());
            return true;
        }

        return base.TryGetIterator(realm, out iterator, hint, method);
    }

    public sealed override bool Equals(object? obj) => Equals(obj as JsString);

    public sealed override bool Equals(JsValue? other) => Equals(other as JsString);

    public virtual bool Equals(string? other) => other != null && string.Equals(ToString(), other, StringComparison.Ordinal);

    public virtual bool Equals(JsString? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return string.Equals(_value, other.ToString(), StringComparison.Ordinal);
    }

    internal static bool IsLooselyEqual(object o,JsValue value)
    {
        if (value is JsString jsString)
        {
            return Equals(jsString);
        }

        if (value.IsBigInt())
        {
            return TypeConverter.TryStringToBigInt(ToString(), out var temp) && temp == value.AsBigInt();
        }
        return false;
    }

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(_value);
    
    public static bool Equals(string s, JsValue value)
    {
        if (value.Tag is Tag.JS_TAG_STRING or Tag.JS_TAG_STRING_CONCAT)
        {
            return string.Equals(s, value.Obj!.ToString(), StringComparison.Ordinal);
        }

        return false;
    }

    internal sealed class ConcatenatedString
    {
        private StringBuilder? _stringBuilder;
        string _value="";
        private bool _dirty;

        internal ConcatenatedString(string value, int capacity = 0)
        {
            if (capacity > 0)
            {
                _stringBuilder = new StringBuilder(value, capacity);
            }
            else
            {
                _value = value;
            }
        }

        public override string ToString()
        {
            if (_dirty)
            {
                _value = _stringBuilder!.ToString();
                _dirty = false;
            }

            return _value!;
        }

        public char this[int index] => _stringBuilder?[index] ?? _value[index];

        internal void Append(JsValue jsValue)
        {
            var value = TypeConverter.ToString(jsValue);
            if (_stringBuilder == null)
            {
                _stringBuilder = new StringBuilder(_value, _value.Length + value.Length);
            }

            _stringBuilder.Append(value);
            _dirty = true;
        }

        internal void EnsureCapacity(int capacity)
        {
            _stringBuilder!.EnsureCapacity(capacity);
        }

        public int Length => _stringBuilder?.Length ?? _value?.Length ?? 0;

        public bool Equals(string? s)
        {
            if (s is null || Length != s.Length)
            {
                return false;
            }

            // we cannot use StringBuilder.Equals as it also checks Capacity on full framework / pre .NET Core 3
            if (_stringBuilder != null)
            {
                for (var i = 0; i < _stringBuilder.Length; ++i)
                {
                    if (_stringBuilder[i] != s[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            return string.Equals(_value, s, StringComparison.Ordinal);
        }

        public bool Equals(JsString other)
        {
            if (other._union is ConcatenatedString cs)
            {
                var stringBuilder = _stringBuilder;
                var csStringBuilder = cs._stringBuilder;

                // we cannot use StringBuilder.Equals as it also checks Capacity on full framework / pre .NET Core 3
                if (stringBuilder != null && csStringBuilder != null && stringBuilder.Length == csStringBuilder.Length)
                {
                    for (var i = 0; i < stringBuilder.Length; ++i)
                    {
                        if (stringBuilder[i] != csStringBuilder[i])
                        {
                            return false;
                        }
                    }

                    return true;
                }

                return string.Equals(ToString(), cs.ToString(), StringComparison.Ordinal);
            }

            if (other.Length != Length)
            {
                return false;
            }

            return string.Equals(ToString(), other.ToString(), StringComparison.Ordinal);
        }

        public override int GetHashCode() => _stringBuilder?.GetHashCode() ?? StringComparer.Ordinal.GetHashCode(_value);

        internal JsValue DoClone()
        {
            return ToString();
        }
    }
}
