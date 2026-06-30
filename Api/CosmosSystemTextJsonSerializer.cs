using System.Text.Json;

using Microsoft.Azure.Cosmos;

namespace MttTracker.Api;

/// <summary>
/// Makes the Cosmos SDK serialize/deserialize with System.Text.Json so that the
/// <c>[JsonPropertyName]</c> attributes on <see cref="Shared.TournamentEntry"/>
/// (e.g. <c>id</c>, <c>userId</c>) are honoured the same way the HTTP/client
/// layer honours them. The SDK's default serializer is Newtonsoft, which would
/// ignore those attributes.
/// </summary>
public sealed class CosmosSystemTextJsonSerializer(JsonSerializerOptions options) : CosmosSerializer
{
    public override T FromStream<T>(Stream stream)
    {
        using (stream)
        {
            if (stream.CanSeek && stream.Length == 0)
            {
                return default!;
            }

            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            return JsonSerializer.Deserialize<T>(stream, options)!;
        }
    }

    public override Stream ToStream<T>(T input)
    {
        var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, input, options);
        stream.Position = 0;
        return stream;
    }
}
