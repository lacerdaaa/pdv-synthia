using System.Text.Json;

namespace Synthia.Contracts;

/// <summary>Opções JSON únicas para caixa, servidor e nuvem: camelCase, sem surpresas.</summary>
public static class SynthiaJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static JsonElement ToElement<T>(T value) => JsonSerializer.SerializeToElement(value, Options);

    public static T FromElement<T>(JsonElement element) =>
        element.Deserialize<T>(Options) ?? throw new JsonException($"Payload vazio para {typeof(T).Name}.");
}
