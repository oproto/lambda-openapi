namespace Oproto.OpenApiGenerator;

public static class DictionaryExtensions
{
    // The GetValueOrDefault method for Dictionary was introduced in .NET Standard 2.1. Since we're targeting .NET Standard 2.0, we need to replace this with a compatible alternative. 
    public static TValue GetValueOrDefault<TKey, TValue>(
        this IDictionary<TKey, TValue> dictionary,
        TKey key,
        TValue defaultValue = default) =>
        dictionary.TryGetValue(key, out var value) ? value : defaultValue;
}
