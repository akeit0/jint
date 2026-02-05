using Jint.Runtime;

namespace Jint.Native;

/// <summary>
/// Special null object pattern for spec's EMPTY.
/// </summary>
internal sealed class JsEmpty : JsObjectBase
{
    internal static readonly JsObjectBase Instance = new JsEmpty();

    private JsEmpty() : base(Types.Empty)
    {
    }

    public override object? ToObject() => null;
}
