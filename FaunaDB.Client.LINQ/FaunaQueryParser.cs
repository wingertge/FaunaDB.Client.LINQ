using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FaunaDB.Query;
using FaunaDB.Types;
using static FaunaDB.Query.Language;

namespace FaunaDB.Extensions
{
    public static class FaunaQueryParser
    {
        public static Expr[] ToExprArray<T>(this IEnumerable<T> collection) => collection.Select(a => a as Expr).ToArray();

        public static Expr Parse(Expr selector, Expression expr)
        {
            if (!(expr is MethodCallExpression)) return selector;
            var methodExpr = (MethodCallExpression) expr;

            var argsArr = methodExpr.Arguments;
            var next = argsArr[0];
            var rest = Parse(selector, next);
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
                case "Map":
                    current = Map(rest, (dynamic) args[0]);
                    break;
                case "Paginate":
                    var fromRef = ((ConstantExpression) args[0]).Value.ToString();
                    var sortDirection = (ListSortDirection)((ConstantExpression)args[1]).Value;
                    var size = (int) ((ConstantExpression) args[2]).Value;
                    var ts = (DateTime) ((ConstantExpression) args[3]).Value;
                    current = sortDirection == ListSortDirection.Ascending
                        ? Paginate(rest, ts: Date(ts.ToUnixTimeStamp()), after: fromRef, size: size)
                        : Paginate(rest, ts: Date(ts.ToUnixTimeStamp()), before: fromRef, size: size);
                    break;
                case "GetAll":
                    current = Map(rest, @ref => Get(@ref));
                    break;
                default:
                    throw new ArgumentException($"Unsupported method {method}.");
            }

            return current;
        }

        private static Expr HandleSelect(IReadOnlyList<Expression> args, Expr rest)
        {
            var arg = args[0] as Expression<Func<string, string>>;
            if (arg.Body is MethodCallExpression exp)
            {
                if (exp.Method.Name == "Concat")
                {
                    
                }
                var methodInfo = exp.Method;
                var attribute = methodInfo.GetCustomAttribute<DbFunctionAttribute>();
                if (attribute == null) throw new UnsupportedMethodException(methodInfo.Name);
                var methodName = attribute.Name ?? methodInfo.Name;
                var functionRef = Ref(methodName); //Not sure this works, documentation doesn't list it as of yet
                var arguments = new List<Expr>();
                foreach (var argument in exp.Arguments)
                {
                    switch (argument)
                    {
                        case ConstantExpression cexp:
                            arguments.Add(cexp.ToFaunaObjOrPrimitive());
                            continue;
                        case MemberExpression mexp:
                            var type = arg.Type.GetGenericArguments()[0];
                            var info = mexp.GetPropertyInfo();
                            var memberName = info.GetFaunaFieldName();
                            //TODO: Await further info about UDFs to finish this.
                            continue;
                    }
                }
                return Call(functionRef, arguments.ToArray());
            }
            var propInfo = ((MemberExpression) ((dynamic) arg).Body).GetPropertyInfo();
            var propName = propInfo.GetFaunaFieldName();
            var refAttr = propInfo.GetCustomAttribute<ReferenceAttribute>();
            if (refAttr == null)
                return Select(Arr(propName.Split('.').ToExprArray()), rest);

            if (typeof(IEnumerable).IsAssignableFrom(propInfo.PropertyType))
                return Map(
                    Select(Arr(propName.Split('.').ToExprArray()), rest),
                    item => Map(item, @ref => Get(@ref)));
            return Map(
                Select(Arr(propName.Split('.').ToExprArray()), rest),
                @ref => Get(@ref));
        }

        private static Expr HandleWhere(IReadOnlyList<Expression> args, Expr rest)
        {
            var lambda = (dynamic) args[0];
            var body = (BinaryExpression) lambda.Body;
            return Filter(rest, a => WalkLambdaExpression(body, ref a));
        }

        private static Expr WalkLambdaExpression(Expression expression, ref Expr lambdaArg)
        {
            if (!(expression is BinaryExpression))
            {
                switch (expression)
                {
                    case UnaryExpression unary:
                        if(unary.Method.Name != "Not") throw new ArgumentException("Invalid unary operator in boolean clause.");
                        return Not(WalkLambdaExpression(unary.Operand, ref lambdaArg));
                    case ConstantExpression constant:
                        return constant.Value.ToFaunaObjOrPrimitive();
                    case MemberExpression member:
                        var propInfo = member.GetPropertyInfo();
                        var propName = propInfo.GetFaunaFieldName();
                        return Select(Arr(propName.Split('.').ToExprArray()), lambdaArg);
                }
            }
            var binary = (BinaryExpression) expression;
            var left = binary.Left;
            var right = binary.Right;
            var type = binary.Method;
            var leftExpr = WalkLambdaExpression(left, ref lambdaArg);
            var rightExpr = WalkLambdaExpression(right, ref lambdaArg);

            switch (binary.NodeType)
            {
                case ExpressionType.Equal:
                    return EqualsFn(leftExpr, rightExpr);
                case ExpressionType.NotEqual:
                    return Not(EqualsFn(leftExpr, rightExpr));
                case ExpressionType.GreaterThanOrEqual:
                    return GTE(leftExpr, rightExpr);
                case ExpressionType.GreaterThan:
                    return GT(leftExpr, rightExpr);
                case ExpressionType.LessThan:
                    return LT(leftExpr, rightExpr);
                case ExpressionType.LessThanOrEqual:
                    return LTE(leftExpr, rightExpr);
                case ExpressionType.Add:
                case ExpressionType.AddChecked:
                    return Add(leftExpr, rightExpr);
                case ExpressionType.Divide:
                    return Divide(leftExpr, rightExpr);
                case ExpressionType.Modulo:
                    return Modulo(leftExpr, rightExpr);
                case ExpressionType.Multiply:
                case ExpressionType.MultiplyChecked:
                    return Multiply(leftExpr, rightExpr);
                case ExpressionType.Subtract:
                case ExpressionType.SubtractChecked:
                    return Subtract(leftExpr, rightExpr);

                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    return And(leftExpr, rightExpr);
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    return Or(leftExpr, rightExpr);
                default:
                    throw new UnsupportedMethodException(type.Name, "Not available in Fauna API");
            }
        }
    }
}