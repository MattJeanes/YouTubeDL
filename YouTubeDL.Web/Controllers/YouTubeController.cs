using Google.Apis.YouTube.v3;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using YouTubeDL.Web.Data;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;

namespace YouTubeDL.Web.Controllers
{
	public class YouTubeController : Controller
	{
		private readonly YoutubeClient _youtubeClient;
		private readonly YouTubeService _youtubeService;
		private readonly ILogger<YouTubeController> _logger;
		private readonly AppSettings _settings;

		public YouTubeController(
			YoutubeClient youtubeClient,
			YouTubeService youtubeService,
			ILogger<YouTubeController> logger,
			IOptions<AppSettings> settings
			)
		{
			_youtubeClient = youtubeClient;
			_youtubeService = youtubeService;
			_logger = logger;
			_settings = settings.Value;
		}

		[HttpGet("/")]
		public string Get()
		{
			var description = "YouTubeDL\n\nUsage:\n/get?id=<youtubeid>\n/play?id=<youtubeid>";

			if (_settings.AllowUserTranscode)
			{
				description += "[&format=<format>]";
			}

			return description;
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
						_logger.LogInformation("Got video for id {id}: {title}", id, video.Snippet.Title);
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
					if (streamInfo.Size.MegaBytes > _settings.MaxSizeMegabytes)
					{
						response.Error = $"Audio stream is too large ({Math.Round(streamInfo.Size.MegaBytes, 1)} MB), max size is {_settings.MaxSizeMegabytes} MB";
					}
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
				_logger.LogError(e, "Failed to get video for id '{id}'", id);
				response.Error = e.Message;
			}
			return response;

		}

		[HttpGet("/play")]
		public async Task PlayVideoAsync(string id, [FromQuery] string format = null)
		{
			var streamInfo = await GetStreamInfo(id);
			if (streamInfo == null)
			{
				HttpContext.Response.StatusCode = 404;
				return;
			}
			if (streamInfo.Size.MegaBytes > _settings.MaxSizeMegabytes)
			{
				HttpContext.Response.StatusCode = 400;
				return;
			}
			if (!string.IsNullOrEmpty(format))
			{
				if (!_settings.AllowUserTranscode)
				{
					HttpContext.Response.StatusCode = 400;
					return;
				}
			}
			else
			{
				format = _settings.TranscodeFormat;
			}
			var mimeType = string.IsNullOrEmpty(format) ? "audio/mpeg" : MimeTypes.GetMimeType($".{format}");
			HttpContext.Response.ContentType = mimeType;
			if (HttpContext.Request.Headers.Accept.Any(x => x.Split(',').Contains("text/html")))
			{
				return; // Browser will request stream again with audio player
			}
			if (string.IsNullOrEmpty(format))
			{
				_logger.LogInformation("Streaming id {id} directly", id);
				await _youtubeClient.Videos.Streams.CopyToAsync(streamInfo, HttpContext.Response.Body);
			}
			else
			{
				_logger.LogInformation("Transcoding id {id} to {format}", id, format);
				var container = new Container(format);
				var tempFilename = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
				try
				{
					await _youtubeClient.Videos.DownloadAsync(new List<IStreamInfo> { streamInfo }, new ConversionRequestBuilder(tempFilename).SetContainer(container).Build());

					_logger.LogInformation("Transcoding id {id} complete, streaming", id);
					await using var fileStream = System.IO.File.OpenRead(tempFilename);
					await fileStream.CopyToAsync(HttpContext.Response.Body);
				}
				finally
				{
					if (System.IO.File.Exists(tempFilename))
					{
						System.IO.File.Delete(tempFilename);
					}
				}
			}
		}

		private async Task<IStreamInfo> GetStreamInfo(string id)
		{
			var manifest = await _youtubeClient.Videos.Streams.GetManifestAsync(id);
			if (manifest == null) { return null; }
			var audioOnlyStreams = manifest.GetAudioOnlyStreams();
			var stream = audioOnlyStreams.Where(x => x.Container == Container.Mp4).TryGetWithHighestBitrate();
			return stream;
		}
	}
}
