using System.Text.Json.Serialization;

namespace OpenWebUiUploader.Models
{
    internal sealed record class FileProcessingStatusResponse
    {
        [JsonPropertyName( "status" )]
        public string? Status { get; init; }

        [JsonPropertyName( "error" )]
        public string? Error { get; init; }
    }
}
