using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace LAK.Sdk.Core.Utilities
{
    public static class LinQUtils
    {
        public static IQueryable<T> DynamicFilter<T>(
            this IQueryable<T> source,
            object filterObject)
        {
            Type? entityType = filterObject.GetType();
            PropertyInfo[]? properties = entityType.GetProperties();
            Type? sourceType = source.ElementType;
            PropertyInfo[]? sourceProperties = sourceType.GetProperties();
            string[] compareOperators = new string[] { "" };

            foreach (var propertyInfo in properties)
            {
                var propValue = propertyInfo.GetValue(filterObject);
                if (propValue != null)
                {
                    var propType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;
                    var dataType = propType.Name;

                    var sourceProperty =
                        sourceProperties.FirstOrDefault(p => p.Name.ToLower() == propertyInfo.Name.ToLower().Trim());

                    switch (dataType.ToLower())
                    {
                        case DataTypes.STRING:
                            string dateRanges =
                                entityType.GetProperty("DateRangeFilters")?.GetValue(filterObject) as string;

                            if (!string.IsNullOrEmpty(dateRanges))
                            {
                                var rangeArray = dateRanges.Split(';');
                                foreach (var dateRange in rangeArray)
                                {
                                    var parts = dateRange.Split(',');
                                    if (parts.Length == 3)
                                    {
                                        var propertyName = parts[0];
                                        if (DateTime.TryParse(parts[1], out var startDate) &&
                                            DateTime.TryParse(parts[2], out var endDate))
                                        {
                                            bool isExistingParams = sourceProperties
                                                .Where(p => propertyName == null ||
                                                            p.Name.ToLower() == propertyName.ToLower().Trim()
                                                            && (p.PropertyType == typeof(DateTime)
                                                                || p.PropertyType.IsGenericType
                                                                && p.PropertyType.GetGenericTypeDefinition() ==
                                                                typeof(Nullable<>)
                                                                && p.PropertyType.GetGenericArguments()[0] ==
                                                                typeof(DateTime)))
                                                .Any();

                                            if (isExistingParams)
                                            {
                                                endDate = endDate.TimeOfDay != TimeSpan.Zero
                                                    ? endDate.AddMinutes(1)
                                                    : new DateTime(endDate.Year, endDate.Month,
                                                        endDate.Day, 23, 59, 59);
                                                // Apply date range filter
                                                source = source.WhereDynamic(propertyName, ">=", startDate.ToString(),
                                                    "&&",
                                                    ToExpression<T>(null, propertyName, "<=", endDate.ToString()));
                                            }
                                        }
                                    }
                                }
                            }

                            if (sourceProperty == null) break;
                            source = source.WhereDynamic(propertyInfo.Name, "LIKE", propValue.ToString());
                            break;
                        case DataTypes.GUID:
                        case DataTypes.BOOLEAN:
                        case DataTypes.INT:
                        case DataTypes.DOUBLE:
                        case DataTypes.DECIMAL:
                            if (sourceProperty == null) break;
                            source = source.WhereDynamic(propertyInfo.Name, "==", propValue.ToString());
                            break;
                        case DataTypes.DATETIME:
                            DateTime? dateTimeValue = propValue as DateTime?;
                            DateTime? dateTimeGTLTE = dateTimeValue.Value.TimeOfDay != TimeSpan.Zero
                                ? dateTimeValue.Value.AddMinutes(1)
                                : new DateTime(dateTimeValue.Value.Year, dateTimeValue.Value.Month,
                                    dateTimeValue.Value.Day, 23, 59, 59);

                            if (dateTimeValue.HasValue)
                            {
                                // Assuming you have a property named "DateType" to specify the filter type (e.g., "Equal", "GreaterThan", "LessThanOrEqual", "Range")
                                var dateOperators =
                                    entityType.GetProperty("DateOperators")?.GetValue(filterObject) as string;

                                compareOperators = compareOperators[0] == "" && dateOperators != null
                                    ? dateOperators!.Split(',')
                                    : compareOperators;

                                switch (compareOperators[0].ToLower().Trim())
                                {
                                    case DateTypes.EQUAL:
                                        if (sourceProperty == null) break;
                                        source = source.WhereDynamic(propertyInfo.Name, ">=",
                                            dateTimeValue.Value.ToString(),
                                            "&&",
                                            ToExpression<T>(null, propertyInfo.Name, "<=",
                                                dateTimeValue.Value.AddMinutes(1).ToString()));
                                        break;
                                    case DateTypes.GREATERTHAN:
                                        if (sourceProperty == null) break;
                                        source = source.WhereDynamic(propertyInfo.Name, ">", dateTimeGTLTE.ToString());
                                        break;
                                    case DateTypes.GREATERTHANOREQUAL:
                                        if (sourceProperty == null) break;
                                        source = source.WhereDynamic(propertyInfo.Name, ">=",
                                            dateTimeValue.Value.ToString());
                                        break;
                                    case DateTypes.LESSTHAN:
                                        if (sourceProperty == null) break;
                                        source = source.WhereDynamic(propertyInfo.Name, "<",
                                            dateTimeValue.Value.ToString());
                                        break;
                                    case DateTypes.LESSTHANOREQUAL:
                                        if (sourceProperty == null) break;
                                        source = source.WhereDynamic(propertyInfo.Name, "<=", dateTimeGTLTE.ToString());
                                        break;
                                    case DateTypes.RANGE:
                                        // Assuming you have properties named "From" and "To", "DateParam" to specify the date range
                                        DateTime? startDate =
                                            entityType.GetProperty("From")?.GetValue(filterObject) as DateTime?;
                                        DateTime? endDate =
                                            entityType.GetProperty("To")?.GetValue(filterObject) as DateTime?;
                                        string? param =
                                            entityType.GetProperty("DateRangeParam")?.GetValue(filterObject) as string;

                                        bool isExistingParam = sourceProperties
                                            .Where(p => param == null || p.Name.ToLower() == param.ToLower().Trim()
                                                && (p.PropertyType == typeof(DateTime)
                                                    || p.PropertyType.IsGenericType
                                                    && p.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>)
                                                    && p.PropertyType.GetGenericArguments()[0] == typeof(DateTime)))
                                            .Any();

                                        if (!isExistingParam) break;

                                        if (startDate.HasValue)
                                            source = source.WhereDynamic(param, ">=", startDate.ToString());
                                        if (endDate.HasValue)
                                        {
                                            endDate = new DateTime(endDate.Value.Year, endDate.Value.Month,
                                                endDate.Value.Day, 23, 59, 59);
                                            source = source.WhereDynamic(param, "<=", endDate.ToString());
                                        }

                                        break;
                                    default:
                                        if (sourceProperty == null) break;
                                        source = source.WhereDynamic(propertyInfo.Name, ">=", dateTimeValue.ToString());
                                        source = source.WhereDynamic(propertyInfo.Name, "<=",
                                            new DateTime(dateTimeValue.Value.Year, dateTimeValue.Value.Month,
                                                dateTimeValue.Value.Day, 23, 59, 59).ToString());
                                        break;
                                }
                            }

                            if (compareOperators.Length > 1)
                            {
                                Array.Copy(compareOperators, 1, compareOperators, 0, compareOperators.Length - 1);
                                Array.Resize(ref compareOperators, compareOperators.Length - 1);
                            }

                            break;
                        default:
                            string[]? dateRangeValues =
                                entityType.GetProperty("DateRangeFilters")?.GetValue(filterObject) as string[];
                            if (dateRangeValues != null)
                            {
                                foreach (var dateRange in dateRangeValues)
                                {
                                    var parts = dateRange.Split(',');
                                    if (parts.Length == 3)
                                    {
                                        var propertyName = parts[0];
                                        if (DateTime.TryParse(parts[1], out var startDate) &&
                                            DateTime.TryParse(parts[2], out var endDate))
                                        {
                                            bool isExistingParams = sourceProperties
                                                .Where(p => propertyName == null ||
                                                            p.Name.ToLower() == propertyName.ToLower().Trim()
                                                            && (p.PropertyType == typeof(DateTime)
                                                                || p.PropertyType.IsGenericType
                                                                && p.PropertyType.GetGenericTypeDefinition() ==
                                                                typeof(Nullable<>)
                                                                && p.PropertyType.GetGenericArguments()[0] ==
                                                                typeof(DateTime)))
                                                .Any();
                                            if (isExistingParams)
                                            {
                                                endDate = endDate.TimeOfDay != TimeSpan.Zero
                                                    ? endDate.AddMinutes(1)
                                                    : new DateTime(endDate.Year, endDate.Month,
                                                        endDate.Day, 23, 59, 59);
                                                // Apply date range filter
                                                source = source.WhereDynamic(propertyName, ">=", startDate.ToString(),
                                                    "&&",
                                                    ToExpression<T>(null, propertyName, "<=", endDate.ToString()));
                                            }
                                        }
                                    }
                                }
                            }

                            if (propType.IsEnum)
                            {
                                var enumType = propValue.GetType();
                                int enumValue = (int)propValue;
                                source = source.WhereDynamic(propertyInfo.Name, "==",
                                    Enum.Format(enumType, propValue, "D"),
                                    "||", ToExpression<T>(null, propertyInfo.Name, "==", propValue.ToString()));
                            }

                            break;
                    }
                }
            }

            return source;
        }

        public static IQueryable<T> DynamicSort<T>(
            this IQueryable<T> source, string sort, string order, string defaultOrder = "ascending")
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
                var propertyGetter = GetPropertyGetter<T>(sort);
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

        public static IQueryable<T> FullTextSearch<T>(
            this IQueryable<T> source,
            string searchTerm)
        {
            if (!string.IsNullOrEmpty(searchTerm))
            {
                Type entityType = typeof(T);
                PropertyInfo[] properties = entityType.GetProperties();

                // Create a parameter expression
                var parameter = Expression.Parameter(entityType, "x");

                // Create an expression to represent the combined OR condition
                Expression combinedCondition = null;

                foreach (var propertyInfo in properties)
                {
                    var propType = Nullable.GetUnderlyingType(propertyInfo.PropertyType) ?? propertyInfo.PropertyType;

                    // Check if the property type is searchable (int, Guid, DateTime, or string)
                    if (propType == typeof(int) || propType == typeof(decimal) || propType == typeof(double) ||
                        propType == typeof(Guid) || propType == typeof(DateTime) || propType == typeof(string))
                    {
                        Expression propertyExpression = Expression.Property(parameter, propertyInfo);

                        // Convert non-string properties to string for search
                        if (propType != typeof(string))
                        {
                            var toStringMethod = typeof(object).GetMethod("ToString");
                            propertyExpression = Expression.Call(propertyExpression, toStringMethod);
                        }

                        // Create an expression to represent x.PropertyName.ToString().Contains(searchTerm)
                        var containsExpression = Expression.Call(propertyExpression, "Contains", null,
                            Expression.Constant(searchTerm));

                        var condition = Expression.Lambda<Func<T, bool>>(containsExpression, parameter);

                        // Combine conditions with OR
                        combinedCondition = combinedCondition == null
                            ? (Expression)condition.Body
                            : Expression.OrElse(combinedCondition, condition.Body);
                    }
                }

                // If there are any searchable properties, apply the combined condition to the source
                if (combinedCondition != null)
                {
                    var lambda = Expression.Lambda<Func<T, bool>>(combinedCondition, parameter);
                    source = source.Where(lambda);
                }
            }

            return source;
        }

        private static Expression<Func<T, object>> GetPropertyGetter<T>(string property)
        {
            var param = Expression.Parameter(typeof(T));
            var propertyInfo = typeof(T).GetProperty(property,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            if (propertyInfo == null)
            {
                throw new ArgumentException($"Property '{property}' does not exist on type '{typeof(T).Name}'.");
            }

            var prop = Expression.Property(param, propertyInfo);
            var convertedProp = Expression.Convert(prop, typeof(object));
            return Expression.Lambda<Func<T, object>>(convertedProp, param);
        }

        private static IQueryable<T> WhereDynamic<T>(
            this IQueryable<T> source,
            string propertyName,
            string @operator,
            string value,
            string andOrOperator = null,
            Expression<Func<T, bool>> expr = null)
        {
            var param = Expression.Parameter(typeof(T));
            var property = NestedExprProp(param, propertyName);
            var propType = property.Type.Name == "Nullable`1"
                ? Nullable.GetUnderlyingType(property.Type)
                : property.Type;
            var constant = ToExprConstant(propType, value);
            var expression = ApplyFilter(@operator, property, Expression.Convert(constant, property.Type));
            var lambda = Expression.Lambda<Func<T, bool>>(expression, param);
            if (expr != null)
            {
                lambda = (String.IsNullOrEmpty(andOrOperator) || andOrOperator == "And" ||
                          andOrOperator == "AND" ||
                          andOrOperator == "&&")
                    ? lambda.And(expr)
                    : lambda.Or(expr);
            }

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