using Jint.Native;
using Jint.Runtime.Environments;

namespace Jint.Runtime.Interpreter.Expressions;

internal sealed class JintUpdateExpression : JintExpression
{
    private JintExpression _argument = null!;
    private int _change;
    private bool _prefix;

    private JintIdentifierExpression? _leftIdentifier;
    private bool _evalOrArguments;
    private bool _initialized;

    public JintUpdateExpression(UpdateExpression expression) : base(expression)
    {
    }

    private void Initialize()
    {
        var expression = (UpdateExpression) _expression;
        _prefix = expression.Prefix;
        _argument = Build(expression.Argument);
        if (expression.Operator == Operator.Increment)
        {
            _change = 1;
        }
        else if (expression.Operator == Operator.Decrement)
        {
            _change = -1;
        }
        else
        {
            Throw.ArgumentException();
        }

        _leftIdentifier = _argument as JintIdentifierExpression;
        _evalOrArguments = _leftIdentifier?.HasEvalOrArguments == true;
    }

    protected override JsValue EvaluateInternal(EvaluationContext context)
    {
        if (!_initialized)
        {
            Initialize();
            _initialized = true;
        }

        var fastResult = _leftIdentifier != null
            ? UpdateIdentifier(context)
            : null;

        return fastResult ?? UpdateNonIdentifier(context);
    }

    private JsValue UpdateNonIdentifier(EvaluationContext context)
    {
        var engine = context.Engine;
        var reference = _argument.Evaluate(context).Obj as Reference;
        if (reference is null)
        {
            Throw.TypeError(engine.Realm, "Invalid left-hand side expression");
        }

        reference.AssertValid(engine.Realm);

        var value = engine.GetValue(reference, false);
        var isInteger = value._type == InternalTypes.Integer;

        JsValue newValue = default;

        var operatorOverloaded = false;
        // if (context.OperatorOverloadingAllowed)
        // {
        //     if (JintUnaryExpression.TryOperatorOverloading(context, _argument.GetValue(context), _change > 0 ? "op_Increment" : "op_Decrement", out var result))
        //     {
        //         operatorOverloaded = true;
        //         newValue = result;
        //     }
        // }

        if (!operatorOverloaded)
        {
            if (isInteger)
            {
                newValue = (value.AsInteger() + _change);
            }
            else if (!value.IsBigInt())
            {
                newValue = (TypeConverter.ToNumber(value) + _change);
            }
            else
            {
                newValue = (TypeConverter.ToBigInt(value) + _change);
            }
        }

        engine.PutValue(reference, newValue!);
        engine._referencePool.Return(reference);

        if (_prefix)
        {
            return newValue!;
        }
        else
        {
            if (isInteger || operatorOverloaded)
            {
                return value;
            }

            if (!value.IsBigInt())
            {
                return (TypeConverter.ToNumber(value));
            }

            return (value);
        }
    }

    private JsValue? UpdateIdentifier(EvaluationContext context)
    {
        var name = _leftIdentifier!.Identifier;
        var strict = StrictModeScope.IsStrictModeCode;

        if (JintEnvironment.TryGetIdentifierEnvironmentWithBindingValue(
                context.Engine.ExecutionContext.LexicalEnvironment,
                name,
                strict,
                out var environmentRecord,
                out var value))
        {
            if (_evalOrArguments && strict)
            {
                Throw.SyntaxError(context.Engine.Realm);
            }

            var isInteger = value._type == InternalTypes.Integer;

            JsValue newValue = default;

            var operatorOverloaded = false;
            // if (context.OperatorOverloadingAllowed)
            // {
            //     if (JintUnaryExpression.TryOperatorOverloading(context, _argument.GetValue(context), _change > 0 ? "op_Increment" : "op_Decrement", out var result))
            //     {
            //         operatorOverloaded = true;
            //         newValue = result;
            //     }
            // }

            if (!operatorOverloaded)
            {
                if (isInteger)
                {
                    newValue = (value.AsInteger() + _change);
                }
                else if (value._type != InternalTypes.BigInt)
                {
                    newValue = (TypeConverter.ToNumber(value) + _change);
                }
                else
                {
                    newValue = JsBigInt.Create(TypeConverter.ToBigInt(value) + _change);
                }
            }

            environmentRecord.SetMutableBinding(name.Key, newValue!, strict);
            if (_prefix)
            {
                return newValue;
            }

            if (!value.IsBigInt() && !value.IsNumber() && !operatorOverloaded)
            {
                return (TypeConverter.ToNumber(value));
            }

            return value;
        }

        return null;
    }
}
