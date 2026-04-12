using System.Text.Json.Serialization;

namespace OpenWebUiUploader.Models
{
    internal sealed record class AddFileToKnowledgeRequest
    {
        [JsonPropertyName( "file_id" )]
        public required string FileId { get; init; }
    }
}
