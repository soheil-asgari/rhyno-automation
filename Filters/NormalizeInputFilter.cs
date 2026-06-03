using Microsoft.AspNetCore.Mvc.Filters;
using OfficeAutomation.Utilities;
using System.Collections;
using System.Reflection;

namespace OfficeAutomation.Filters
{
    public class NormalizeInputFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            foreach (var argument in context.ActionArguments.Values)
            {
                NormalizeObject(argument, new HashSet<object>());
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }

        private static void NormalizeObject(object? instance, HashSet<object> visited)
        {
            if (instance == null)
            {
                return;
            }

            var type = instance.GetType();
            if (type == typeof(string) || IsSimpleType(type))
            {
                return;
            }

            if (!visited.Add(instance))
            {
                return;
            }

            if (instance is IEnumerable enumerable && type != typeof(string))
            {
                foreach (var item in enumerable)
                {
                    NormalizeObject(item, visited);
                }
                return;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || !property.CanWrite)
                {
                    continue;
                }

                if (property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                var propertyType = property.PropertyType;
                if (propertyType == typeof(string))
                {
                    var value = property.GetValue(instance) as string;
                    if (value == null)
                    {
                        continue;
                    }

                    property.SetValue(instance, PersianTextNormalizer.Normalize(value));
                    continue;
                }

                if (propertyType.IsClass && propertyType != typeof(string))
                {
                    var nested = property.GetValue(instance);
                    if (nested != null)
                    {
                        NormalizeObject(nested, visited);
                    }
                }
            }
        }

        private static bool IsSimpleType(Type type)
        {
            return type.IsPrimitive ||
                   type.IsEnum ||
                   type == typeof(decimal) ||
                   type == typeof(DateTime) ||
                   type == typeof(DateTimeOffset) ||
                   type == typeof(Guid) ||
                   type == typeof(TimeSpan);
        }
    }
}
