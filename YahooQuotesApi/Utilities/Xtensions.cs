﻿using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text.Json;

namespace YahooQuotesApi;

internal static partial class Xtensions
{
    internal static string ToPascal(this string source)
    {
        if (source.Length == 0)
            return source;
        char[] chars = source.ToCharArray();
        chars[0] = char.ToUpper(chars[0], CultureInfo.InvariantCulture);
        return new string(chars);
    }

    internal static string Name<T>(this T source) where T : Enum
    {
        string name = source.ToString();
        if (typeof(T).GetMember(name).First().GetCustomAttribute(typeof(EnumMemberAttribute)) is EnumMemberAttribute attr
            && attr.IsValueSetExplicitly && attr.Value is not null)
            name = attr.Value;
        return name;
    }

    internal static IEnumerable<T> NotNull<T>(this IEnumerable<T?> source) where T : class
    {
        foreach (T? item in source)
        {
            if (item is not null)
                yield return item;
        }
    }

    internal static double RoundToSigFigs(this double num, int figs)
    {
        if (num == 0)
            return 0;

        double d = Math.Ceiling(Math.Log10(num < 0 ? -num : num));
        int power = figs - (int)d;

        double magnitude = Math.Pow(10, power);
        double shifted = Math.Round(num * magnitude);
        return shifted / magnitude;
    }

    internal static object? GetValue(this JsonProperty property, Type propertyType, ILogger logger)
    {
        JsonElement value = property.Value;
        try
        {
            return value.Deserialize(propertyType);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not parse json property '{Name}' = '{RawText}', {JsonType} -> {PropertyType}.", property.Name, value.GetRawText(), value.ValueKind, propertyType);
            throw;
        }
    }

    internal static object? DeserializeNewValue(this JsonProperty property)
    {
        JsonElement value = property.Value;
        JsonValueKind kind = value.ValueKind;

        if (kind == JsonValueKind.String)
            return value.GetString(); // may return null

        if (kind is JsonValueKind.True or JsonValueKind.False)
            return value.GetBoolean();

        if (kind == JsonValueKind.Number)
        {
            if (value.TryGetDouble(out double dbl))
                return dbl;
        }
        return value.GetRawText();
        //Array, Object, Undefined, Null
    }

    internal static bool IsCalculated(this PropertyInfo pi)
    {
        if (!pi.CanWrite)
            return true;
        Type type = pi.PropertyType;
        return type.IsGenericType && type.Name.StartsWith("Result", StringComparison.Ordinal);
    }
}

