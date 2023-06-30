using System.ComponentModel.DataAnnotations;

namespace YouTubeDL.Web.Data
{
	public class AppSettings
	{
		[Required]
		public string ApiKey { get; set; }

		[Required]
		[Range(1, int.MaxValue)]
		public int MaxSizeMegabytes { get; set; }


		public string TranscodeFormat { get; set; }
	}
}
