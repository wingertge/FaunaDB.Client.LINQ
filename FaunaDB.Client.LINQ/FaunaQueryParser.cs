using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FaunaDB.Query;
using static FaunaDB.Query.Language;

namespace FaunaDB.Extensions
{
    public static class FaunaQueryParser
    {
        private static int _lambdaIndex;
        private const string CurrentlyUnsupportedError = "Currently unsupported by FaunaDB.";
        private const string UnsupportedError = "Unsupported by FaunaDB (likely won't change).";

        private static readonly Dictionary<(Type, string), Func<Expr, Expr, Expr>> BuiltInBinaryMethods = new Dictionary<(Type, string), Func<Expr, Expr, Expr>>
        {
            { (typeof(string), "Concat"), (left, right) => Concat(Arr(left, right)) }
        };

        private static readonly Dictionary<(Type, string), Func<Expr[], Expr>> BuiltInFunctions = new Dictionary<(Type, string), Func<Expr[], Expr>>
        {
            { (typeof(string), "Concat"), args => Concat(Arr(args)) },
            { (typeof(Tuple), "Create"), Arr }
        };

        public static Expr[] ToExprArray<T>(this IEnumerable<T> collection) => collection.Select(a => (Expr)((dynamic)a)).ToArray();

        public static Expr Parse(Expr selector, Expression expr)
        {
            _lambdaIndex = 0;
            return WalkExpression(selector, expr);
        }

        private static Expr WalkExpression(Expr selector, Expression expr)
        {
            if (!(expr is MethodCallExpression)) return selector;
            var methodExpr = (MethodCallExpression) expr;

            var argsArr = methodExpr.Arguments;
            var next = argsArr[0];
            var rest = WalkExpression(selector, next);
            var args = argsArr.Skip(1).Take(argsArr.Count - 1).ToArray();

            var method = methodExpr.Method;
            Expr current;

            switch (method.Name)
            {
                case "Where":
                    current = HandleWhere(args, rest);
                    break;
                case "Select":
                    current = HandleSelect(args, rest);
                    break;
                case "Paginate":
                    current = HandlePaginate(args, rest);
                    break;
                case "Skip":
                    current = Drop(args[0].GetConstantValue<int>(), rest);
                    break;
                case "Take":
                    current = Take(args[0].GetConstantValue<int>(), rest);
                    break;
                case "Include":
                case "AlsoInclude":
                    current = HandleInclude(args, rest);
                    break;
                case "FromQuery":
                    current = (Expr) ((ConstantExpression) args[0]).Value;
                    break;
                case "At":
                    current = At(((DateTime) ((ConstantExpression) args[0]).Value).ToFaunaObjOrPrimitive(), rest);
                    break;
                default:
                    throw new ArgumentException($"Unsupported method {method}.");
            }

            return current;
        }

        private static Expr HandleInclude(IReadOnlyList<Expression> args, Expr rest)
        {
            var lambdaArgName = $"arg{++_lambdaIndex}";
            var lambda = (LambdaExpression)args[0];
            if(!(lambda.Body is MemberExpression)) throw new ArgumentException("Selector must be member expression.");
            var memberExpr = (MemberExpression) lambda.Body;
            if(!(memberExpr.Member is PropertyInfo)) throw new ArgumentException("Selector must be property.");
            var propInfo = (PropertyInfo) memberExpr.Member;
            var definingType = propInfo.DeclaringType;

            var fields = definingType.GetProperties().Where(a => a.GetFaunaFieldName() != "ref" && a.GetFaunaFieldName() != "ts")
                .ToDictionary(a => a.GetFaunaFieldName().Replace("data.", ""),
                    a =>
                    {
                        var fieldSelector = Select(a.GetFaunaFieldPath(), Var(lambdaArgName));
                        return a.Name == propInfo.Name
                            ? typeof(IEnumerable).IsAssignableFrom(propInfo.PropertyType)
                                ? Map(fieldSelector,
                                    Lambda($"arg{++_lambdaIndex}",
                                        If(Exists(Var($"arg{_lambdaIndex}")), Get(Var($"arg{_lambdaIndex}")), Null())))
                                : If(Exists(fieldSelector), Get(fieldSelector), Null()) //perform safe include
                            : fieldSelector;
                    });

            var mappedObj = Obj("ref", Select("ref", Var(lambdaArgName)), "ts", Select("ts", Var(lambdaArgName)), "data", Obj(fields));

            return Map(rest, Lambda(lambdaArgName, mappedObj));
        }

        private static Expr HandlePaginate(IReadOnlyList<Expression> args, Expr rest)
        {
            var fromRef = ((ConstantExpression)args[0]).Value.ToString();
            var sortDirection = (ListSortDirection)((ConstantExpression)args[1]).Value;
            var size = (int)((ConstantExpression)args[2]).Value;
            var ts = (DateTime)((ConstantExpression)args[3]).Value;

            if (string.IsNullOrEmpty(fromRef))
                return ts != default(DateTime)
                    ? Paginate(rest, ts: Time(ts.ToString("O")), size: size)
                    : Paginate(rest, size: size);

            if (ts != default(DateTime))
            {
                return sortDirection == ListSortDirection.Ascending
                    ? Paginate(rest, ts: Time(ts.ToString("O")), after: fromRef, size: size)
                    : Paginate(rest, ts: Time(ts.ToString("O")), before: fromRef, size: size);
            }

            return sortDirection == ListSortDirection.Ascending
                ? Paginate(rest, after: fromRef, size: size)
                : Paginate(rest, before: fromRef, size: size);
        }

        private static Expr HandleSelect(IReadOnlyList<Expression> args, Expr rest)
        {
            var lambda = args[0] is UnaryExpression unary
                ? (LambdaExpression) unary.Operand
                : (LambdaExpression) args[0];
            var argName = $"arg{++_lambdaIndex}";

            return Map(rest, Lambda(argName, WalkComplexExpression(lambda.Body, argName)));
        }

        private static Expr HandleWhere(IReadOnlyList<Expression> args, Expr rest)
        {
            var lambda = args[0] is LambdaExpression lexp ? lexp : (LambdaExpression)((UnaryExpression)args[0]).Operand;
            var body = (BinaryExpression) lambda.Body;
            return Filter(rest, Lambda($"arg{++_lambdaIndex}", WalkComplexExpression(body, $"arg{_lambdaIndex}")));
        }

        private static Expr WalkComplexExpression(Expression expression, string varName)
        {
            switch (expression)
            {
                case BinaryExpression binary:
                    var left = WalkComplexExpression(binary.Left, varName);
                    var right = WalkComplexExpression(binary.Right, varName);
                    if (binary.Method != null)
                    {
                        if (BuiltInBinaryMethods.ContainsKey((binary.Method.DeclaringType, binary.Method.Name)))
                            return BuiltInBinaryMethods[(binary.Method.DeclaringType, binary.Method.Name)](left, right);
                    }
                    switch (binary.NodeType)
                    {
                        case ExpressionType.Add:
                        case ExpressionType.AddChecked:
                            return Add(left, right);
                        case ExpressionType.Divide:
                            return Divide(left, right);
                        case ExpressionType.Modulo:
                            return Modulo(left, right);
                        case ExpressionType.Multiply:
                        case ExpressionType.MultiplyChecked:
                            return Multiply(left, right);
                        case ExpressionType.Subtract:
                        case ExpressionType.SubtractChecked:
                            return Subtract(left, right);
                        case ExpressionType.AndAlso:
                            return And(left, right);
                        case ExpressionType.OrElse:
                            return Or(left, right);
                        case ExpressionType.Equal:
                            return EqualsFn(left, right);
                        case ExpressionType.NotEqual:
                            return Not(EqualsFn(left, right));
                        case ExpressionType.GreaterThanOrEqual:
                            return GTE(left, right);
                        case ExpressionType.GreaterThan:
                            return GT(left, right);
                        case ExpressionType.LessThan:
                            return LT(left, right);
                        case ExpressionType.LessThanOrEqual:
                            return LTE(left, right);
                        case ExpressionType.Coalesce:
                            return If(EqualsFn(left, Null()), right, left);
                        case ExpressionType.Power:
                            throw new UnsupportedMethodException(binary.NodeType.ToString(), CurrentlyUnsupportedError);
                        case ExpressionType.ExclusiveOr:
                        case ExpressionType.LeftShift:
                        case ExpressionType.RightShift:
                        case ExpressionType.Or:
                        case ExpressionType.And:
                        case ExpressionType.ArrayIndex:
                            throw new UnsupportedMethodException(binary.NodeType.ToString(), UnsupportedError);
                        default:
                            throw new UnsupportedMethodException(binary.Method?.Name ?? binary.NodeType.ToString());
                    }
                case BlockExpression block:
                    return Do(block.Expressions.Select(a => WalkComplexExpression(a, varName)).ToArray());
                case ConditionalExpression conditional:
                    return If(WalkComplexExpression(conditional.Test, varName),
                        WalkComplexExpression(conditional.IfTrue, varName),
                        WalkComplexExpression(conditional.IfFalse, varName));
                case ConstantExpression constant:
                    return constant.Value.ToFaunaObjOrPrimitive();
                case DefaultExpression defaultExp:
                    return Expression.Lambda(defaultExp).Compile().DynamicInvoke().ToFaunaObjOrPrimitive();
                case GotoExpression _:
                case IndexExpression _:
                    throw new UnsupportedMethodException("Goto", "Seriously?");
                case ListInitExpression listInit:
                    var result = new List<Expr>();
                    foreach (var initializer in listInit.Initializers)
                    {
                        var args = initializer.Arguments;
                        if(args.Count == 1) result.Add(WalkComplexExpression(args[0], varName));
                        else throw new UnsupportedMethodException("Tuple collection initializer not supported.");
                    }
                    return Arr(result.ToArray());
                case MemberExpression member:
                    switch (member.Member)
                    {
                        case FieldInfo field:
                            switch (member.Expression)
                            {
                                case ConstantExpression constRoot:
                                    return field.GetValue(constRoot.Value).ToFaunaObjOrPrimitive();
                                case ParameterExpression _:
                                    throw new InvalidOperationException("Can't use selector on field of model. Use a property instead.");
                                case MemberExpression memberRoot:
                                    return WalkComplexExpression(memberRoot, varName);
                                default:
                                    throw new ArgumentOutOfRangeException(); ;
                            }
                        case PropertyInfo property:
                            switch (member.Expression)
                            {
                                case ConstantExpression constRoot:
                                    return property.GetValue(constRoot.Value).ToFaunaObjOrPrimitive();
                                case ParameterExpression _:
                                    var refAttr = property.GetCustomAttribute<ReferenceAttribute>();
                                    if(refAttr == null) return Select(property.GetFaunaFieldPath(), Var(varName));
                                    return typeof(IEnumerable).IsAssignableFrom(property.PropertyType)
                                        ? Map(
                                            Select(property.GetFaunaFieldPath(), Var(varName)),
                                            Lambda($"arg{++_lambdaIndex}",
                                                Map(Var($"arg{_lambdaIndex}"),
                                                    Lambda($"arg{++_lambdaIndex}", Get(Var($"arg{_lambdaIndex}")))
                                                )))
                                        : Map(
                                            Select(property.GetFaunaFieldPath(), Var(varName)),
                                            Lambda($"arg{++_lambdaIndex}", Get($"arg{_lambdaIndex}")));
                                case MemberExpression memberRoot:
                                    switch (memberRoot.Member)
                                    {
                                        case FieldInfo _:
                                            return WalkComplexExpression(memberRoot, varName);
                                        case PropertyInfo prop:
                                            return typeof(IReferenceType).IsAssignableFrom(prop.PropertyType)
                                                ? Map(
                                                    Select(property.GetFaunaFieldPath(),
                                                        WalkComplexExpression(memberRoot, varName)),
                                                    Lambda($"arg{++_lambdaIndex}", Get(Var($"arg{_lambdaIndex}"))))
                                                : Select(property.GetFaunaFieldPath(), WalkComplexExpression(memberRoot, varName));
                                        default:
                                            throw new ArgumentOutOfRangeException();
                                    }
                                default:
                                    return WalkComplexExpression(expression, varName);
                            }
                        default:
                            throw new ArgumentOutOfRangeException(); ;
                    }
                case MemberInitExpression memberInit:
                    var baseObj = memberInit.NewExpression.Constructor.Invoke(new object[] { });
                    var members = baseObj.GetType().GetProperties().Where(a =>
                    {
                        var propType = a.PropertyType;
                        if (propType.Name.StartsWith("CompositeIndex")) return false;
                        var propName = a.GetFaunaFieldName();
                        return propName != "@ref" && propName != "ts";
                    }).ToDictionary(a => a.GetFaunaFieldName().Replace("data.", ""), a => a.GetValue(baseObj).ToFaunaObjOrPrimitive());
                    foreach (var binding in memberInit.Bindings.OfType<MemberAssignment>())
                    {
                        var propInfo = (PropertyInfo)binding.Member;
                        var propType = propInfo.PropertyType;
                        if (propType.Name.StartsWith("CompositeIndex")) continue;
                        var propName = propInfo.GetFaunaFieldName();
                        if (propName == "@ref" || propName == "ts") continue;
                        members[propName.Replace("data.", "")] = WalkComplexExpression(binding.Expression, varName);
                    }

                    return Obj(members);
                case MethodCallExpression methodCall:
                    var methodInfo = methodCall.Method;
                    if ((methodCall.Object == null || methodCall.Object is ConstantExpression)
                        && !methodCall.Arguments.Any(a => a is ParameterExpression) 
                        && methodCall.Arguments.OfType<MemberExpression>().All(a => a.Expression is ConstantExpression))
                    {
                        var fixedParams = FixConstantParameters(methodCall.Arguments);
                        var callTarget = (methodCall.Object as ConstantExpression)?.Value;
                        methodInfo.Invoke(callTarget, fixedParams);
                    }
                    if (!methodInfo.IsStatic)
                        throw new ArgumentException("Can't call member method in FaunaDB query.");

                    if (BuiltInFunctions.ContainsKey((methodInfo.DeclaringType, methodInfo.Name)))
                        return BuiltInFunctions[(methodInfo.DeclaringType, methodInfo.Name)](methodCall.Arguments.Select(a => WalkComplexExpression(a, varName)).ToArray());

                    var dbFunctionAttr = methodInfo.GetCustomAttribute<DbFunctionAttribute>();
                    if(dbFunctionAttr == null) throw new UnsupportedMethodException(methodInfo.Name, "If this is a user defined function, add the DbFunction attribute.");
                    var functionRef = Ref(dbFunctionAttr.Name); //Not working, pending API update
                    return Call(functionRef, methodCall.Arguments.Select(a => WalkComplexExpression(a, varName)).ToArray());
                case NewArrayExpression newArray:
                    switch (newArray.NodeType)
                    {
                        case ExpressionType.NewArrayBounds:
                            return Arr();
                        case ExpressionType.NewArrayInit:
                            return Arr(newArray.Expressions.Select(a => WalkComplexExpression(a, varName)));
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                case NewExpression newExpr:
                    if (newExpr.Arguments.Any(a => a is ParameterExpression) || !newExpr.Arguments
                            .OfType<MemberExpression>().All(a => a.Expression is ConstantExpression))
                        throw new UnsupportedMethodException(newExpr.Constructor.DeclaringType + ".ctor",
                            "Parametered constructor with dynamic parameters not supported.");

                    var parameters = FixConstantParameters(newExpr.Arguments);
                    var obj = newExpr.Constructor.Invoke(parameters);
                    return obj.ToFaunaObjOrPrimitive();
                case ParameterExpression _:
                    return Var(varName);
                case SwitchExpression _:
                    throw new UnsupportedMethodException("switch", "Switch expressions are not supported. Use if/else instead.");
                case TypeBinaryExpression typeBinary:
                    switch (typeBinary.Expression)
                    {
                        case ConstantExpression constType:
                            return typeBinary.TypeOperand.IsAssignableFrom((Type) constType.Value);
                        case MemberExpression memberType when memberType.Expression is ConstantExpression cExp:
                            switch (memberType.Member)
                            {
                                case FieldInfo field:
                                    return typeBinary.TypeOperand.IsAssignableFrom((Type)field.GetValue(cExp.Value));
                                case PropertyInfo prop:
                                    return typeBinary.TypeOperand.IsAssignableFrom((Type)prop.GetValue(cExp.Value));
                                default:
                                    throw new ArgumentOutOfRangeException();
                            }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                case UnaryExpression unary:
                    switch (unary.NodeType)
                    {
                        case ExpressionType.Not:
                            return Not(WalkComplexExpression(unary.Operand, varName));
                        case ExpressionType.Quote:
                        case ExpressionType.Convert:
                        case ExpressionType.ConvertChecked:
                            return WalkComplexExpression(unary.Operand, varName);
                        case ExpressionType.Negate:
                        case ExpressionType.NegateChecked:
                            throw new UnsupportedMethodException(unary.NodeType.ToString(), CurrentlyUnsupportedError);
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static object[] FixConstantParameters(IEnumerable<Expression> args)
        {
            var fixedParams = new List<object>();
            foreach (var argument in args)
            {
                switch (argument)
                {
                    case ConstantExpression constantArg:
                        fixedParams.Add(constantArg.Value);
                        break;
                    case MemberExpression memberArg:
                        var obj = ((ConstantExpression) memberArg.Expression).Value;
                        switch (memberArg.Member)
                        {
                            case PropertyInfo propInfo:
                                fixedParams.Add(propInfo.GetValue(obj));
                                break;
                            case FieldInfo fieldInfo:
                                fixedParams.Add(fieldInfo.GetValue(obj));
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                        break;
                }
            }

            return fixedParams.ToArray();
        }
    }
}