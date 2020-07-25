using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace YouTubeDL.Web.Controllers
{
    public class YouTubeController : Controller
    {
        private readonly YoutubeClient _youtubeClient;
        private readonly ILogger<YouTubeController> _logger;

        public YouTubeController(
            YoutubeClient youtubeClient,
            ILogger<YouTubeController> logger
            )
        {
            _youtubeClient = youtubeClient;
            _logger = logger;
        }

        [HttpGet("/")]
        public string Get()
        {
            return "YouTubeDL\n\nUsage:\n/get?id=<youtubeid>\n/play?id=<youtubeid>";
        }

        [HttpGet("/get")]
        public async Task<InfoResponse> GetVideoAsync(string id)
        {
            var response = new InfoResponse();
            try
            {
                if (string.IsNullOrEmpty(id))
                {
                    response.Error = "Required parameter 'id' missing";
                }

                if (string.IsNullOrEmpty(response.Error))
                {
                    var results = await _youtubeClient.Search.GetVideosAsync(id);
                    var video = results.FirstOrDefault(x => x.Id == id);
                    if (video == null)
                    {
                        response.Error = $"Could not find id {id}, does it exist?";
                    }
                    else
                    {
                        _logger.LogInformation($"Got video for id {id}: {video.Title}");
                        response.Title = video.Title;
                        response.Id = video.Id;
                        response.Duration = Convert.ToInt32(video.Duration.TotalSeconds);
                        response.LikeCount = video.Engagement.LikeCount;
                        response.DislikeCount = video.Engagement.DislikeCount;
                        response.Description = video.Description;
                        response.Uploader = video.Author;
                    }
                }

                if (string.IsNullOrEmpty(response.Error))
                {
                    var streamInfo = await GetStreamInfo(id);
                    if (streamInfo == null)
                    {
                        response.Error = $"Could not find suitable audio stream for id {id}";
                    }
                }

                if (string.IsNullOrEmpty(response.Error))
                {
                    response.Success = true;
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to get video for id '{id}'");
                response.Error = e.Message;
            }
            return response;

        }

        [HttpGet("/play")]
        public async Task PlayVideoAsync(string id)
        {
            HttpContext.Response.ContentType = "audio/mpeg";
            var streamInfo = await GetStreamInfo(id);
            if (streamInfo == null)
            {
                HttpContext.Response.StatusCode = 400;
            }
            await _youtubeClient.Videos.Streams.CopyToAsync(streamInfo, HttpContext.Response.Body);
        }

        private async Task<IStreamInfo> GetStreamInfo(string id)
        {
            return (await _youtubeClient.Videos.Streams.GetManifestAsync(id))?.GetAudioOnly().Where(x => x.Container == Container.Mp4).WithHighestBitrate();
        }
    }
}
