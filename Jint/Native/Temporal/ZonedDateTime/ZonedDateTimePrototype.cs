using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-zoneddatetime-prototype-object
/// </summary>
internal sealed class ZonedDateTimePrototype : Prototype
{
    private readonly ZonedDateTimeConstructor _constructor;

    internal ZonedDateTimePrototype(
        Engine engine,
        Realm realm,
        ZonedDateTimeConstructor constructor,
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
        DefineAccessor("timeZoneId", GetTimeZoneId);
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
        DefineAccessor("epochMilliseconds", GetEpochMilliseconds);
        DefineAccessor("epochNanoseconds", GetEpochNanoseconds);
        DefineAccessor("dayOfWeek", GetDayOfWeek);
        DefineAccessor("dayOfYear", GetDayOfYear);
        DefineAccessor("offset", GetOffset);
        DefineAccessor("offsetNanoseconds", GetOffsetNanoseconds);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.ZonedDateTime", PropertyFlag.Configurable)
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

    private JsZonedDateTime ValidateZonedDateTime(JsValue thisObject)
    {
        if (thisObject.Obj is JsZonedDateTime zonedDateTime)
            return zonedDateTime;
        Throw.TypeError(_realm, "Value is not a Temporal.ZonedDateTime");
        return null!;
    }

    private JsValue GetCalendarId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidateZonedDateTime(thisObject).Calendar);
    private JsValue GetTimeZoneId(JsValue thisObject, JsCallArguments arguments) => new JsString(ValidateZonedDateTime(thisObject).TimeZone);
    private JsValue GetYear(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).GetIsoDateTime().Year);
    private JsValue GetMonth(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).GetIsoDateTime().Month);
    private JsValue GetMonthCode(JsValue thisObject, JsCallArguments arguments) => new JsString($"M{ValidateZonedDateTime(thisObject).GetIsoDateTime().Month:D2}");
    private JsValue GetDay(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).GetIsoDateTime().Day);
    private JsValue GetHour(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).GetIsoDateTime().Hour);
    private JsValue GetMinute(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).GetIsoDateTime().Minute);
    private JsValue GetSecond(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).GetIsoDateTime().Second);
    private JsValue GetMillisecond(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).GetIsoDateTime().Millisecond);
    private JsValue GetMicrosecond(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).GetIsoDateTime().Microsecond);
    private JsValue GetNanosecond(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).GetIsoDateTime().Nanosecond);
    private JsValue GetEpochMilliseconds(JsValue thisObject, JsCallArguments arguments) => ((double) (ValidateZonedDateTime(thisObject).EpochNanoseconds / 1_000_000));
    private JsValue GetEpochNanoseconds(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).EpochNanoseconds);
    private JsValue GetDayOfWeek(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).GetIsoDateTime().Date.DayOfWeek());
    private JsValue GetDayOfYear(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).GetIsoDateTime().Date.DayOfYear());
    private JsValue GetOffset(JsValue thisObject, JsCallArguments arguments)
    {
        var zdt = ValidateZonedDateTime(thisObject);
        var offsetNs = zdt.OffsetNanoseconds;
        return (TemporalHelpers.FormatOffsetString(offsetNs));
    }
    private JsValue GetOffsetNanoseconds(JsValue thisObject, JsCallArguments arguments) => (ValidateZonedDateTime(thisObject).OffsetNanoseconds);
}
