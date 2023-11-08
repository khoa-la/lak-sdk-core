using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using LAK.Sdk.Core.Models;

namespace LAK.Sdk.Core.Utilities
{
    public static class LinQUtils
    {
        public static IQueryable<TEntity> DynamicFilter<TEntity>(
            this IQueryable<TEntity> source,
            TEntity entity)
        {
            var entityType = entity.GetType();
        var properties = entityType.GetProperties();

        foreach (var propertyInfo in properties)
        {
            var propValue = propertyInfo.GetValue(entity);
            if (propValue == null) continue;

            var propType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
            var dataType = propType.Name;

            switch (dataType)
            {
                case "String":
                    source = source.WhereDynamic(propertyInfo.Name, "LIKE", propValue.ToString());
                    break;
                case "Guid":
                case "Boolean":
                case "Int32":
                case "Double":
                case "Decimal":
                    source = source.WhereDynamic(propertyInfo.Name, "==", propValue.ToString());
                    break;
                case "DateTime":
                    if (propValue is DateTimeRange dateTimeRange)
                    {
                        var startDate = dateTimeRange.From;
                        var endDate = dateTimeRange.To;
                        var param = dateTimeRange.Param;

                        if (startDate != null && endDate != null)
                        {
                            // Use "BETWEEN" for the DateTime range
                            source = source.WhereDynamic(param, ">=", startDate.ToString());
                            source = source.WhereDynamic(param, "<=", endDate.ToString());
                        }
                        else if (startDate != null)
                        {
                            source = source.WhereDynamic(param, ">=", startDate.ToString());
                        }
                        else if (endDate != null)
                        {
                            source = source.WhereDynamic(param, "<=", endDate.ToString());
                        }
                    }
                    else
                    {
                        source = source.WhereDynamic(propertyInfo.Name, ">=", propValue.ToString());
                    }
                    break;
            }
        }

        return source;
        }

        public static IQueryable<TEntity> DynamicSort<TEntity>(
            this IQueryable<TEntity> source, string sort, string order, string defaultOrder = "ascending")
        {
            List<string> listOrder = new List<string>
            {
                "asc",
                "ascending",
                "desc",
                "descending"
            };
            if (String.IsNullOrEmpty(order))
            {
                order = defaultOrder;
            }
            else if (!listOrder.Contains(order.ToLower()))
            {
                order = defaultOrder;
            }

            if (!String.IsNullOrEmpty(sort))
            {
                var propertyGetter = LinQUtils.GetPropertyGetter<TEntity>(sort);
                if (!String.IsNullOrEmpty(order))
                {
                    if (order.ToLower() == "asc" || order.ToLower() == "ascending")
                    {
                        source = source.OrderBy(propertyGetter);
                    }
                    else if (order.ToLower() == "desc" || order.ToLower() == "descending")
                    {
                        source = source.OrderByDescending(propertyGetter);
                    }
                }
            }

            return source;
        }

        public static (int, IQueryable<TResult>) PagingQueryable<TResult>(
            this IQueryable<TResult> source,
            int page,
            int size,
            int limitPaging = 50,
            int defaultPaging = 1)
        {
            if (size > limitPaging)
                size = limitPaging;
            if (size < 1)
                size = defaultPaging;
            if (page < 1)
                page = 1;
            return (source.Count<TResult>(), source.Skip<TResult>((page - 1) * size).Take<TResult>(size));
        }

        private static Expression<Func<TEntity, object>> GetPropertyGetter<TEntity>(string property)
        {
            var param = Expression.Parameter(typeof(TEntity));
            var propertyInfo = typeof(TEntity).GetProperty(property, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo == null)
            {
                throw new ArgumentException($"Property '{property}' does not exist on type '{typeof(TEntity).Name}'.");
            }

            var prop = Expression.Property(param, propertyInfo);
            var convertedProp = Expression.Convert(prop, typeof(object));
            return Expression.Lambda<Func<TEntity, object>>(convertedProp, param);
        }
    
        private static IQueryable<TEntity> WhereDynamic<TEntity>(
            this IQueryable<TEntity> source,
            string propertyName,
            string @operator,
            string value)
        {
            var param = Expression.Parameter(typeof(TEntity));
            var property = NestedExprProp(param, propertyName);
            var propType = property.Type.Name == "Nullable`1"
                ? Nullable.GetUnderlyingType(property.Type)
                : property.Type;
            var constant = ToExprConstant(propType, value);
            var expression = ApplyFilter(@operator, property, Expression.Convert(constant, property.Type));
            var lambda = Expression.Lambda<Func<TEntity, bool>>(expression, param);
            return source.Where(lambda);
        }

        private static Expression<Func<T, bool>> ToExpression<T>(string andOrOperator, string propName, string opr,
            string value, Expression<Func<T, bool>> expr = null)
        {
            Expression<Func<T, bool>> func = null;
            try
            {
                ParameterExpression paramExpr = Expression.Parameter(typeof(T));
                var arrProp = propName.Split('.').ToList();
                Expression binExpr = null;
                string partName = string.Empty;
                arrProp.ForEach(x =>
                {
                    Expression tempExpr = null;
                    partName = String.IsNullOrEmpty(partName) ? x : partName + "." + x;
                    if (partName == propName)
                    {
                        var member = NestedExprProp(paramExpr, partName);
                        var type = member.Type.Name == "Nullable`1"
                            ? Nullable.GetUnderlyingType(member.Type)
                            : member.Type;
                        tempExpr = ApplyFilter(opr, member,
                            Expression.Convert(ToExprConstant(type, value), member.Type));
                    }
                    else
                        tempExpr = ApplyFilter("!=", NestedExprProp(paramExpr, partName), Expression.Constant(null));

                    if (binExpr != null)
                        binExpr = Expression.AndAlso(binExpr, tempExpr);
                    else
                        binExpr = tempExpr;
                });
                Expression<Func<T, bool>> innerExpr = Expression.Lambda<Func<T, bool>>(binExpr, paramExpr);
                if (expr != null)
                    innerExpr = (String.IsNullOrEmpty(andOrOperator) || andOrOperator == "And" ||
                                 andOrOperator == "AND" ||
                                 andOrOperator == "&&")
                        ? innerExpr.And(expr)
                        : innerExpr.Or(expr);
                func = innerExpr;
            }
            catch
            {
                // ignored
            }

            return func;
        }

        private static MemberExpression NestedExprProp(Expression expr, string propName)
        {
            string[] arrProp = propName.Split('.');
            int arrPropCount = arrProp.Length;
            return (arrPropCount > 1)
                ? Expression.Property(
                    NestedExprProp(expr, arrProp.Take(arrPropCount - 1).Aggregate((a, i) => a + "." + i)),
                    arrProp[arrPropCount - 1])
                : Expression.Property(expr, propName);
        }

        private static Expression ToExprConstant(Type prop, string value)
        {
            if (String.IsNullOrEmpty(value))
                return Expression.Constant(value);
            object val = null;
            switch (prop.FullName)
            {
                case "System.Guid":
                    // val = value.ToGuid();
                    val = Guid.Parse(value);
                    break;
                default:
                    val = Convert.ChangeType(value, Type.GetType(prop.FullName));
                    break;
            }

            return Expression.Constant(val);
        }

        private static Expression ApplyFilter(string opr, Expression left, Expression right)
        {
            Expression InnerLambda = null;
            switch (opr)
            {
                case "==":
                case "=":
                    InnerLambda = Expression.Equal(left, right);
                    break;
                case "<":
                    InnerLambda = Expression.LessThan(left, right);
                    break;
                case ">":
                    InnerLambda = Expression.GreaterThan(left, right);
                    break;
                case ">=":
                    InnerLambda = Expression.GreaterThanOrEqual(left, right);
                    break;
                case "<=":
                    InnerLambda = Expression.LessThanOrEqual(left, right);
                    break;
                case "!=":
                    InnerLambda = Expression.NotEqual(left, right);
                    break;
                case "&&":
                    InnerLambda = Expression.And(left, right);
                    break;
                case "||":
                    InnerLambda = Expression.Or(left, right);
                    break;
                case "LIKE":
                    InnerLambda = Expression.Call(left,
                        typeof(string).GetMethod("Contains", new Type[] { typeof(string) })!, right);
                    break;
                case "NOTLIKE":
                    InnerLambda = Expression.Not(Expression.Call(left,
                        typeof(string).GetMethod("Contains", new Type[] { typeof(string) })!, right));
                    break;
            }

            return InnerLambda;
        }

        private static Expression<Func<T, TResult>> And<T, TResult>(this Expression<Func<T, TResult>> expr1,
            Expression<Func<T, TResult>> expr2)
        {
            var invokedExpr = Expression.Invoke(expr2, expr1.Parameters.Cast<Expression>());
            return Expression.Lambda<Func<T, TResult>>(Expression.AndAlso(expr1.Body, invokedExpr), expr1.Parameters);
        }

        private static Expression<Func<T, bool>> Or<T>(this Expression<Func<T, bool>> expr1,
            Expression<Func<T, bool>> expr2)
        {
            var invokedExpr = Expression.Invoke(expr2, expr1.Parameters.Cast<Expression>());
            return Expression.Lambda<Func<T, bool>>(Expression.OrElse(expr1.Body, invokedExpr), expr1.Parameters);
        }
    }
}