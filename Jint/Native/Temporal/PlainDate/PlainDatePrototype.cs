using Jint.Native.Object;
using Jint.Native.Symbol;
using Jint.Runtime;
using Jint.Runtime.Descriptors;
using Jint.Runtime.Interop;

namespace Jint.Native.Temporal;

/// <summary>
/// https://tc39.es/proposal-temporal/#sec-properties-of-the-temporal-plaindate-prototype-object
/// </summary>
internal sealed class PlainDatePrototype : Prototype
{
    private readonly PlainDateConstructor _constructor;

    internal PlainDatePrototype(
        Engine engine,
        Realm realm,
        PlainDateConstructor constructor,
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
        DefineAccessor("era", GetEra);
        DefineAccessor("eraYear", GetEraYear);
        DefineAccessor("year", GetYear);
        DefineAccessor("month", GetMonth);
        DefineAccessor("monthCode", GetMonthCode);
        DefineAccessor("day", GetDay);
        DefineAccessor("dayOfWeek", GetDayOfWeek);
        DefineAccessor("dayOfYear", GetDayOfYear);
        DefineAccessor("weekOfYear", GetWeekOfYear);
        DefineAccessor("yearOfWeek", GetYearOfWeek);
        DefineAccessor("daysInWeek", GetDaysInWeek);
        DefineAccessor("daysInMonth", GetDaysInMonth);
        DefineAccessor("daysInYear", GetDaysInYear);
        DefineAccessor("monthsInYear", GetMonthsInYear);
        DefineAccessor("inLeapYear", GetInLeapYear);

        var symbols = new SymbolDictionary(1)
        {
            [GlobalSymbolRegistry.ToStringTag] = new("Temporal.PlainDate", PropertyFlag.Configurable)
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

    private JsPlainDate ValidatePlainDate(JsValue thisObject)
    {
        if (thisObject.Obj is JsPlainDate plainDate)
            return plainDate;
        Throw.TypeError(_realm, "Value is not a Temporal.PlainDate");
        return null!;
    }

    private JsValue GetCalendarId(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDate(thisObject).Calendar);
    private JsValue GetEra(JsValue thisObject, JsCallArguments arguments) => Undefined;
    private JsValue GetEraYear(JsValue thisObject, JsCallArguments arguments) => Undefined;
    private JsValue GetYear(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDate(thisObject).IsoDate.Year);
    private JsValue GetMonth(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDate(thisObject).IsoDate.Month);
    private JsValue GetMonthCode(JsValue thisObject, JsCallArguments arguments) => ($"M{ValidatePlainDate(thisObject).IsoDate.Month:D2}");
    private JsValue GetDay(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDate(thisObject).IsoDate.Day);
    private JsValue GetDayOfWeek(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDate(thisObject).IsoDate.DayOfWeek());
    private JsValue GetDayOfYear(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDate(thisObject).IsoDate.DayOfYear());
    private JsValue GetWeekOfYear(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDate(thisObject).IsoDate.WeekOfYear());
    private JsValue GetYearOfWeek(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDate(thisObject).IsoDate.YearOfWeek());
    private JsValue GetDaysInWeek(JsValue thisObject, JsCallArguments arguments) => (7);
    private JsValue GetDaysInMonth(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDate(thisObject).IsoDate.DaysInMonth());
    private JsValue GetDaysInYear(JsValue thisObject, JsCallArguments arguments) => (ValidatePlainDate(thisObject).IsoDate.DaysInYear());
    private JsValue GetMonthsInYear(JsValue thisObject, JsCallArguments arguments) => (12);
    private JsValue GetInLeapYear(JsValue thisObject, JsCallArguments arguments) => IsoDate.IsLeapYear(ValidatePlainDate(thisObject).IsoDate.Year) ? JsValue.True : JsValue.False;
}
