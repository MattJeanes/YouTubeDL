using System.Text.Json.Serialization;

namespace YouTubeDL.Web
{
    public class InfoResponse
    {
        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("like_count")]
        public ulong? LikeCount { get; set; }

        [JsonPropertyName("dislike_count")]
        public ulong? DislikeCount { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; }

        [JsonPropertyName("uploader")]
        public string Uploader { get; set; }

        [JsonPropertyName("err")]
        public string Error { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; }
    }
}
