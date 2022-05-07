using Newtonsoft.Json;
using SimpleHttp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HpSwitchControlApi
{
	sealed class Config
	{
		public string GpibHost { get; set; }
		public int SwitchGpibAddress { get; set; }
		public int MeterGpibAddress { get; set; }
		public int HttpPort { get; set; }
		public string HttpToken { get; set; }
	}

	static class Program
	{
		public static Config Config { get; private set; }

		static void Main(string[] args)
		{
			var exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
			var configPath = args.Length >= 1 ? args[0] : Path.Combine(Path.GetDirectoryName(exePath), "config.json");
			Config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));

			var lockObj = new object();

			var server = new HttpServer(Config.HttpPort);

			server.AddRoute(null, null, (urlArgs, request, response) =>
			{
				if (Config.HttpToken != null && request.Headers["authorization"] != $"Bearer {Config.HttpToken}")
					throw new UnauthorizedAccessException();

				return true;
			});

			server.Log($"Using adapter host '{Config.GpibHost}'.");

			if (Config.SwitchGpibAddress != null)
				SwitchController.Configure(server, lockObj);

			if (Config.MeterGpibAddress != null)
				MeterController.Configure(server, lockObj);

			server.Start();

			Task.Delay(-1).Wait();
		}
	}
}
