using Google.Apis.YouTube.v3;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;

namespace YouTubeDL.Web.Controllers
{
    public class YouTubeController : Controller
    {
        private readonly YoutubeClient _youtubeClient;
        private readonly YouTubeService _youtubeService;
        private readonly ILogger<YouTubeController> _logger;

        public YouTubeController(
            YoutubeClient youtubeClient,
            YouTubeService youtubeService,
            ILogger<YouTubeController> logger
            )
        {
            _youtubeClient = youtubeClient;
            _youtubeService = youtubeService;
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
                    var request = _youtubeService.Videos.List(new List<string> { "snippet", "statistics", "contentDetails" });
                    request.Id = id;
                    var results = await request.ExecuteAsync(HttpContext.RequestAborted);
                    var video = results.Items.FirstOrDefault(x => x.Id == id);
                    if (video == null)
                    {
                        response.Error = $"Could not find id {id}, does it exist?";
                    }
                    else
                    {
                        _logger.LogInformation($"Got video for id {id}: {video.Snippet.Title}");
                        response.Title = video.Snippet.Title;
                        response.Id = video.Id;
                        response.Duration = Convert.ToInt32(XmlConvert.ToTimeSpan(video.ContentDetails.Duration).TotalSeconds);
                        response.LikeCount = video.Statistics.LikeCount;
                        response.DislikeCount = video.Statistics.DislikeCount;
                        response.Description = video.Snippet.Description;
                        response.Uploader = video.Snippet.ChannelTitle;
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
            return (await _youtubeClient.Videos.Streams.GetManifestAsync(id))?.GetAudioOnlyStreams().Where(x => x.Container == Container.Mp4).TryGetWithHighestBitrate();
        }
    }
}
