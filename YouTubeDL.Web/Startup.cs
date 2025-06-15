using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using YouTubeDL.Web.Data;
using YoutubeDLSharp;

namespace YouTubeDL.Web
{
	public class Startup
	{
		public Startup(IConfiguration configuration)
		{
			Configuration = configuration;
		}

		public IConfiguration Configuration { get; }

		public void ConfigureServices(IServiceCollection services)
		{
			services.AddControllers();
			services.AddSingleton(provider =>
			{
				var ytdl = new YoutubeDL
				{
					OutputFolder = Path.GetTempPath(),
					OutputFileTemplate = "[%(id)s].%(ext)s"
				};
				return ytdl;
			});
			services.AddOptions<AppSettings>()
				.Bind(Configuration)
				.ValidateDataAnnotations()
				.ValidateOnStart();
			services.AddHealthChecks();
		}

		public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
		{
			if (env.IsDevelopment())
			{
				app.UseDeveloperExceptionPage();
			}

			app.UseRouting();

			app.UseEndpoints(endpoints =>
			{
				endpoints.MapControllers();
				endpoints.MapHealthChecks("/healthz");
			});
		}
	}
}
