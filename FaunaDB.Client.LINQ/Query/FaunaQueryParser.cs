using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FaunaDB.LINQ.Errors;
using FaunaDB.LINQ.Extensions;
using FaunaDB.LINQ.Modeling;
using FaunaDB.LINQ.Types;

namespace FaunaDB.LINQ.Query
{
    public static class FaunaQueryParser
    {
        private static int _lambdaIndex;
        private const string CurrentlyUnsupportedError = "Currently unsupported by FaunaDB.";
        private const string UnsupportedError = "Unsupported by FaunaDB (likely won't change).";

        private static readonly Dictionary<(Type, string), Func<object, object, object>> BuiltInBinaryMethods = new Dictionary<(Type, string), Func<object, object, object>>
        {
            { (typeof(string), "Concat"), (left, right) => Language.Concat(left, right) }
        };

        private static readonly Dictionary<(Type, string), Func<object[], object>> BuiltInFunctions = new Dictionary<(Type, string), Func<object[], object>>
        {
            { (typeof(string), "Concat"), Language.Concat },
            { (typeof(Tuple), "Create"), a => a }
        };

        public static Expr Parse(object selector, Expression expr, IDbContext context)
        {
            _lambdaIndex = 0;
            return (Expr)WalkExpression(selector, expr, context);
        }

        private static object WalkExpression(object selector, Expression expr, IDbContext context)
        {
            if (!(expr is MethodCallExpression)) return selector;
            var methodExpr = (MethodCallExpression) expr;

            var argsArr = methodExpr.Arguments;
            var next = argsArr[0];
            var rest = WalkExpression(selector, next, context);
            var args = argsArr.Skip(1).Take(argsArr.Count - 1).ToArray();

            var method = methodExpr.Method;
            object current;

            switch (method.Name)
            {
                case "Where":
                    current = HandleWhere(args, rest, context);
                    break;
                case "Select":
                    current = HandleSelect(args, rest, context);
                    break;
                case "Paginate":
                    current = HandlePaginate(args, rest);
                    break;
                case "Skip":
                    current = Language.Drop(args[0].GetConstantValue<int>(), rest);
                    break;
                case "Take":
                    current = Language.Take(args[0].GetConstantValue<int>(), rest);
                    break;
                case "Distinct":
                    current = Language.Distinct(rest);
                    break;
                case "Include":
                case "AlsoInclude":
                    current = HandleInclude(args, rest, context);
                    break;
                case "FromQuery":
                    current = (Expr) ((ConstantExpression) args[0]).Value;
                    break;
                case "At":
                    current = Language.At(((DateTime) ((ConstantExpression) args[0]).Value).ToFaunaObjOrPrimitive(context), rest);
                    break;
                default:
                    throw new ArgumentException($"Unsupported method {method}.");
            }

            return current;
        }

        private static object HandleInclude(IReadOnlyList<Expression> args, object rest, IDbContext context)
        {
            var lambdaArgName = $"arg{++_lambdaIndex}";
            var lambda = (LambdaExpression)args[0];
            if(!(lambda.Body is MemberExpression)) throw new ArgumentException("Selector must be member expression.");
            var memberExpr = (MemberExpression) lambda.Body;
            if(!(memberExpr.Member is PropertyInfo)) throw new ArgumentException("Selector must be property.");
            var propInfo = (PropertyInfo) memberExpr.Member;
            var definingType = propInfo.DeclaringType;

            var mappings = context.Mappings[definingType];
            var fields = definingType.GetProperties().Where(a => mappings[a].Type != DbPropertyType.Key && mappings[a].Type != DbPropertyType.Timestamp)
                .ToDictionary(a => mappings[a].Name,
                    a =>
                    {
                        var fieldSelector = Language.Select(mappings[a].GetFaunaFieldPath(), Language.Var(lambdaArgName));
                        return a.Name == propInfo.Name
                            ? typeof(IEnumerable).IsAssignableFrom(propInfo.PropertyType)
                                ? Language.Map(fieldSelector,
                                    Language.Lambda($"arg{++_lambdaIndex}",
                                        Language.If(Language.Exists(Language.Var($"arg{_lambdaIndex}")), Language.Get(Language.Var($"arg{_lambdaIndex}")), null)))
                                : Language.If(Language.Exists(fieldSelector), Language.Get(fieldSelector), null) //perform safe include
                            : fieldSelector;
                    });

            var mappedObj = Language.Obj(new Dictionary<string, object> {{"ref", Language.Select("ref", Language.Var(lambdaArgName))}, {"ts", Language.Select("ts", Language.Var(lambdaArgName))}, {"data", Language.Obj(fields)}});

            return Language.Map(rest, Language.Lambda(lambdaArgName, mappedObj));
        }

        private static object HandlePaginate(IReadOnlyList<Expression> args, object rest)
        {
            var fromRef = (string)((ConstantExpression)args[0]).Value;
            var sortDirection = (ListSortDirection)((ConstantExpression)args[1]).Value;
            var size = (int)((ConstantExpression)args[2]).Value;
            var time = (DateTime)((ConstantExpression)args[3]).Value;
            var ts = time == default(DateTime) ? (DateTime?)null : time;

            return sortDirection == ListSortDirection.Ascending
                ? Language.Paginate(rest, ts: ts == null ? null : Language.Time(ts.Value.ToString("O")), size: size,
                    after: string.IsNullOrEmpty(fromRef) ? null : Language.Ref(fromRef))
                : Language.Paginate(rest, ts: ts == null ? null : Language.Time(ts.Value.ToString("O")), size: size,
                    before: string.IsNullOrEmpty(fromRef) ? null : Language.Ref(fromRef));
        }

        private static object HandleSelect(IReadOnlyList<Expression> args, object rest, IDbContext context)
        {
            var lambda = args[0] is UnaryExpression unary
                ? (LambdaExpression) unary.Operand
                : (LambdaExpression) args[0];
            var argName = $"arg{++_lambdaIndex}";

            return Language.Map(rest, Language.Lambda(argName, WalkComplexExpression(lambda.Body, argName, context)));
        }

        private static object HandleWhere(IReadOnlyList<Expression> args, object rest, IDbContext context)
        {
            var lambda = args[0] is LambdaExpression lexp ? lexp : (LambdaExpression)((UnaryExpression)args[0]).Operand;
            var body = (BinaryExpression) lambda.Body;
            return Language.Filter(rest, Language.Lambda($"arg{++_lambdaIndex}", WalkComplexExpression(body, $"arg{_lambdaIndex}", context)));
        }

        private static object WalkComplexExpression(Expression expression, string varName, IDbContext context)
        {
            switch (expression)
            {
                case BinaryExpression binary:
                    var left = WalkComplexExpression(binary.Left, varName, context);
                    var right = WalkComplexExpression(binary.Right, varName, context);
                    if (binary.Method != null)
                    {
                        if (BuiltInBinaryMethods.ContainsKey((binary.Method.DeclaringType, binary.Method.Name)))
                            return BuiltInBinaryMethods[(binary.Method.DeclaringType, binary.Method.Name)](left, right);
                    }
                    switch (binary.NodeType)
                    {
                        case ExpressionType.Add:
                        case ExpressionType.AddChecked:
                            return Language.Add(left, right);
                        case ExpressionType.Divide:
                            return Language.Divide(left, right);
                        case ExpressionType.Modulo:
                            return Language.Modulo(left, right);
                        case ExpressionType.Multiply:
                        case ExpressionType.MultiplyChecked:
                            return Language.Multiply(left, right);
                        case ExpressionType.Subtract:
                        case ExpressionType.SubtractChecked:
                            return Language.Subtract(left, right);
                        case ExpressionType.AndAlso:
                            return Language.And(left, right);
                        case ExpressionType.OrElse:
                            return Language.Or(left, right);
                        case ExpressionType.Equal:
                            return Language.EqualsFn(left, right);
                        case ExpressionType.NotEqual:
                            return Language.Not(Language.EqualsFn(left, right));
                        case ExpressionType.GreaterThanOrEqual:
                            return Language.GTE(left, right);
                        case ExpressionType.GreaterThan:
                            return Language.GT(left, right);
                        case ExpressionType.LessThan:
                            return Language.LT(left, right);
                        case ExpressionType.LessThanOrEqual:
                            return Language.LTE(left, right);
                        case ExpressionType.Coalesce:
                            return Language.If(Language.EqualsFn(left, null), right, left);
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
                    return Language.Do(block.Expressions.Select(a => WalkComplexExpression(a, varName, context)).Cast<Expr>().ToArray());
                case ConditionalExpression conditional:
                    return Language.If(WalkComplexExpression(conditional.Test, varName, context),
                        WalkComplexExpression(conditional.IfTrue, varName, context),
                        WalkComplexExpression(conditional.IfFalse, varName, context));
                case ConstantExpression constant:
                    return constant.Value.ToFaunaObjOrPrimitive(context);
                case DefaultExpression defaultExp:
                    return Expression.Lambda(defaultExp).Compile().DynamicInvoke().ToFaunaObjOrPrimitive(context);
                case GotoExpression _:
                case IndexExpression _:
                    throw new UnsupportedMethodException("Goto", "Seriously?");
                case ListInitExpression listInit:
                    var result = new List<object>();
                    foreach (var initializer in listInit.Initializers)
                    {
                        var args = initializer.Arguments;
                        if(args.Count == 1) result.Add(WalkComplexExpression(args[0], varName, context));
                        else throw new UnsupportedMethodException("Tuple collection initializer not supported.");
                    }
                    return result.ToArray();
                case MemberExpression member:
                    switch (member.Member)
                    {
                        case FieldInfo field:
                            switch (member.Expression)
                            {
                                case ConstantExpression constRoot:
                                    return field.GetValue(constRoot.Value).ToFaunaObjOrPrimitive(context);
                                case ParameterExpression _:
                                    throw new InvalidOperationException("Can't use selector on field of model. Use a property instead.");
                                case MemberExpression memberRoot:
                                    return WalkComplexExpression(memberRoot, varName, context);
                                default:
                                    throw new ArgumentOutOfRangeException(); ;
                            }
                        case PropertyInfo property:
                            var mapping = context.Mappings[property.DeclaringType][property];
                            switch (member.Expression)
                            {
                                case ConstantExpression constRoot:
                                    return property.GetValue(constRoot.Value).ToFaunaObjOrPrimitive(context);
                                case ParameterExpression _:
                                    var isReference = mapping.Type == DbPropertyType.Reference;
                                    if(!isReference) return Language.Select(mapping.GetFaunaFieldPath(), Language.Var(varName));
                                    return typeof(IEnumerable).IsAssignableFrom(property.PropertyType)
                                        ? Language.Map(
                                            Language.Select(mapping.GetFaunaFieldPath(), Language.Var(varName)),
                                            Language.Lambda($"arg{++_lambdaIndex}",
                                                Language.Map(Language.Var($"arg{_lambdaIndex}"),
                                                    Language.Lambda($"arg{++_lambdaIndex}", Language.Get(Language.Var($"arg{_lambdaIndex}")))
                                                )))
                                        : Language.Map(
                                            Language.Select(mapping.GetFaunaFieldPath(), Language.Var(varName)),
                                            Language.Lambda($"arg{++_lambdaIndex}", Language.Get($"arg{_lambdaIndex}")));
                                case MemberExpression memberRoot:
                                    switch (memberRoot.Member)
                                    {
                                        case FieldInfo _:
                                            return WalkComplexExpression(memberRoot, varName, context);
                                        case PropertyInfo prop:
                                            return prop.PropertyType.IsReferenceType(context)
                                                ? Language.Map(
                                                    Language.Select(mapping.GetFaunaFieldPath(),
                                                        WalkComplexExpression(memberRoot, varName, context)),
                                                    Language.Lambda($"arg{++_lambdaIndex}", Language.Get(Language.Var($"arg{_lambdaIndex}"))))
                                                : Language.Select(mapping.GetFaunaFieldPath(), WalkComplexExpression(memberRoot, varName, context));
                                        default:
                                            throw new ArgumentOutOfRangeException();
                                    }
                                default:
                                    return WalkComplexExpression(expression, varName, context);
                            }
                        default:
                            throw new ArgumentOutOfRangeException(); ;
                    }
                case MemberInitExpression memberInit:
                    var baseObj = memberInit.NewExpression.Constructor.Invoke(new object[] { });
                    var baseType = baseObj.GetType();
                    var mappings = context.Mappings[baseType];
                    var members = baseType.GetProperties().Where(a =>
                    {
                        var propType = a.PropertyType;
                        if (propType.Name.StartsWith("CompositeIndex")) return false;
                        var propMapping = mappings[a];
                        return propMapping.Type != DbPropertyType.Key && propMapping.Type != DbPropertyType.Timestamp;
                    }).ToDictionary(a => a.GetFaunaFieldName().Replace("data.", ""), a => a.GetValue(baseObj).ToFaunaObjOrPrimitive(context));
                    foreach (var binding in memberInit.Bindings.OfType<MemberAssignment>())
                    {
                        var propInfo = (PropertyInfo)binding.Member;
                        var mapping = mappings[propInfo];
                        var propType = propInfo.PropertyType;
                        if (propType.Name.StartsWith("CompositeIndex")) continue;
                        var propName = propInfo.GetFaunaFieldName();
                        if (mapping.Type == DbPropertyType.Key || mapping.Type == DbPropertyType.Timestamp) continue;
                        members[propName.Replace("data.", "")] = WalkComplexExpression(binding.Expression, varName, context);
                    }

                    return Language.Obj(members);
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
                        return BuiltInFunctions[(methodInfo.DeclaringType, methodInfo.Name)](methodCall.Arguments.Select(a => WalkComplexExpression(a, varName, context)).ToArray());

                    var dbFunctionAttr = methodInfo.GetCustomAttribute<DbFunctionAttribute>();
                    if(dbFunctionAttr == null) throw new UnsupportedMethodException(methodInfo.Name, "If this is a user defined function, add the DbFunction attribute.");
                    var functionRef = Language.Function(dbFunctionAttr.Name);
                    return Language.Call(functionRef, methodCall.Arguments.Select(a => WalkComplexExpression(a, varName, context)).ToArray());
                case NewArrayExpression newArray:
                    switch (newArray.NodeType)
                    {
                        case ExpressionType.NewArrayBounds:
                            return Activator.CreateInstance(newArray.Type.MakeArrayType(), (int)((ConstantExpression)newArray.Expressions[0]).Value);
                        case ExpressionType.NewArrayInit:
                            return newArray.Expressions.Select(a => WalkComplexExpression(a, varName, context));
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
                    return obj.ToFaunaObjOrPrimitive(context);
                case ParameterExpression _:
                    return Language.Var(varName);
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
                            return Language.Not(WalkComplexExpression(unary.Operand, varName, context));
                        case ExpressionType.Quote:
                        case ExpressionType.Convert:
                        case ExpressionType.ConvertChecked:
                            return WalkComplexExpression(unary.Operand, varName, context);
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