using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading.Tasks;
using YouTubeDL.Web.Data;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;

namespace YouTubeDL.Web.Controllers
{
	public class YouTubeController : Controller
	{
		private readonly YoutubeDL _youtubeDl;
		private readonly ILogger<YouTubeController> _logger;
		private readonly AppSettings _settings;

		public YouTubeController(
			YoutubeDL youtubeDl,
			ILogger<YouTubeController> logger,
			IOptions<AppSettings> settings
			)
		{
			_youtubeDl = youtubeDl;
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
					var result = await _youtubeDl.RunVideoDataFetch(id);
					if (!result.Success)
					{
						response.Error = $"Failed to fetch video data for id {id}: {string.Join(Environment.NewLine, result.ErrorOutput)}";
					}
					else
					{
						var data = result.Data;
						response.Title = data.Title;
						response.Id = data.ID;
						response.Duration = (int)data.Duration;
						response.Description = data.Description;
						response.Uploader = data.Uploader;
					}
				}

				if (string.IsNullOrEmpty(response.Error))
				{
					var audioFormat = await GetBestAudioFormat(id);
					if (audioFormat == null)
					{
						response.Error = $"Could not find suitable audio stream for id {id}";
					}
					else if ((audioFormat.FileSize ?? 0) > _settings.MaxSizeMegabytes * 1024 * 1024)
					{
						var sizeMB = Math.Round(((audioFormat.FileSize ?? 0) / 1024.0 / 1024.0), 1);
						response.Error = $"Audio stream is too large ({sizeMB} MB), max size is {_settings.MaxSizeMegabytes} MB";
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
			var audioFormat = await GetBestAudioFormat(id);
			if (audioFormat == null)
			{
				HttpContext.Response.StatusCode = 404;
				return;
			}
			if ((audioFormat.FileSize ?? 0) > _settings.MaxSizeMegabytes * 1024 * 1024)
			{
				HttpContext.Response.StatusCode = 400;
				return;
			}
			_logger.LogInformation("Downloading audio from video id {id}", id);
			var audioFormatEnum = AudioConversionFormat.Mp3;
			if (!string.IsNullOrEmpty(format))
			{
				Enum.TryParse(format, true, out audioFormatEnum);
			}
			string filePath = null;
			try
			{
				var result = await _youtubeDl.RunAudioDownload(id, audioFormatEnum);
				if (!result.Success)
				{
					_logger.LogError("Failed to download video id {id}: {error}", id, string.Join(Environment.NewLine, result.ErrorOutput));
					HttpContext.Response.StatusCode = 500;
					return;
				}
				filePath = result.Data;
				await using var fileStream = System.IO.File.OpenRead(filePath);
				await fileStream.CopyToAsync(HttpContext.Response.Body);
			}
			finally
			{
				if (!string.IsNullOrEmpty(filePath) && System.IO.File.Exists(filePath))
				{
					System.IO.File.Delete(filePath);
				}
			}
		}

		private async Task<FormatData> GetBestAudioFormat(string id)
		{
			var result = await _youtubeDl.RunVideoDataFetch(id);
			if (!result.Success || result.Data == null) return null;
			return result.Data.Formats?.Where(f => f.AudioCodec != null && (f.VideoCodec == null || f.VideoCodec == "none"))
				.OrderByDescending(f => f.AudioBitrate ?? 0)
				.FirstOrDefault();
		}
	}
}
