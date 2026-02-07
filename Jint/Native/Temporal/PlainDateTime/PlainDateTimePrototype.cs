using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plaindatetime-prototype-object
/// </summary>
internal sealed class PlainDateTimePrototype : Prototype
{
    private readonly PlainDateTimeConstructor _constructor;

    internal PlainDateTimePrototype(
        Engine engine,
        Realm realm,
        PlainDateTimeConstructor constructor,
        ObjectPrototype objectPrototype) : base(engine, realm)
    {
        _constructor = constructor;
        _prototype = objectPrototype;
    }

    protected override void Initialize()
    {
        var properties = new PropertyDictionary(1, checkExistingKeys: false)
        {
            ["constructor"] = new PropertyDescriptor(_constructor, PropertyFlag.NonEnumerable),
        };
        SetProperties(properties);

        DefineAccessor("calendarId", GetCalendarId);
        DefineAccessor("year", GetYear);
        DefineAccessor("month", GetMonth);
        DefineAccessor("monthCode", GetMonthCode);
        DefineAccessor("day", GetDay);
        DefineAccessor("hour", GetHour);
        DefineAccessor("minute", GetMinute);
        DefineAccessor("second", GetSecond);
        DefineAccessor("millisecond", GetMillisecond);
        DefineAccessor("microsecond", GetMicrosecond);
        DefineAccessor("nanosecond", GetNanosecond);
        DefineAccessor("dayOfWeek", GetDayOfWeek);
        DefineAccessor("dayOfYear", GetDayOfYear);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.PlainDateTime", PropertyFlag.Configurable)
        };
        SetSymbols(symbols);
    }

    private void DefineAccessor(string name, Func<JsValue, JsCallArguments, JsValue> getter)
    {
        SetProperty(name, new GetSetPropertyDescriptor(
            new ClrFunction(Engine, $"get {name}", getter, 0, PropertyFlag.Configurable),
            Undefined,
            PropertyFlag.Configurable));
    }

    private JsPlainDateTime ValidatePlainDateTime(JsValue thisObject)
    {
        if (thisObject.Obj is JsPlainDateTime plainDateTime)
            return plainDateTime;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainDateTime");
        return null!;
    }

    private JsValue GetCalendarId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidatePlainDateTime(thisObject).Calendar);
    private JsValue GetYear(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDateTime(thisObject).IsoDateTime.Year);
    private JsValue GetMonth(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDateTime(thisObject).IsoDateTime.Month);
    private JsValue GetMonthCode(JsValue thisObject, JsCallArguments arguments) => new JsString($"M{ValidatePlainDateTime(thisObject).IsoDateTime.Month:D2}");
    private JsValue GetDay(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDateTime(thisObject).IsoDateTime.Day);
    private JsValue GetHour(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDateTime(thisObject).IsoDateTime.Hour);
    private JsValue GetMinute(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDateTime(thisObject).IsoDateTime.Minute);
    private JsValue GetSecond(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDateTime(thisObject).IsoDateTime.Second);
    private JsValue GetMillisecond(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDateTime(thisObject).IsoDateTime.Millisecond);
    private JsValue GetMicrosecond(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDateTime(thisObject).IsoDateTime.Microsecond);
    private JsValue GetNanosecond(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDateTime(thisObject).IsoDateTime.Nanosecond);
    private JsValue GetDayOfWeek(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDateTime(thisObject).IsoDateTime.Date.DayOfWeek());
    private JsValue GetDayOfYear(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDateTime(thisObject).IsoDateTime.Date.DayOfYear());
}
