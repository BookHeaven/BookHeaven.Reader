using System.Reflection;
using BookHeaven.Reader.Resources.Localization;

namespace BookHeaven.Reader.Extensions;

[AttributeUsage(AttributeTargets.Field)]
public sealed class StringValueAttribute(string resourceKey) : Attribute
{
    public string Value { get; } = Translations.ResourceManager.GetString(resourceKey) ?? resourceKey;
}
public static class EnumExtensions
{
    public static string StringValue<T>(this T value) where T : Enum
    {
        var fieldName = value.ToString();
        var field = typeof(T).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
        return field?.GetCustomAttribute<StringValueAttribute>()?.Value ?? fieldName;
    }
}
