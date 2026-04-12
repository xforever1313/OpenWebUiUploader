using System.Text.Json.Serialization;

namespace OpenWebUiUploader.Models
{
    public sealed class AddFileResponse
    {
        [JsonPropertyName( "id" )]
        public string? FileId { get; init; }
    }
}
