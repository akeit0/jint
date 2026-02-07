namespace Jint.Native;

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
    JS_TAG_MODULE = -3, /* used internally */
    JS_TAG_SOME_OBJECT = -2, /* used internally */
    JS_TAG_OBJECT = -1,
    JS_EMPTY = 0, /* used internally */

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
