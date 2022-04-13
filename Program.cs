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
		public int GpibAddress { get; set; }
		public int HttpPort { get; set; }
		public string HttpToken { get; set; }
	}

	static class Program
	{
		static Config config;

		static T DoCommand<T>(Func<HP_3488A, T> callback, bool configureAdapter = false)
		{
			using (var device = new HP_3488A(config.GpibHost, config.GpibAddress, configureAdapter))
				return callback(device);
		}

		static void DoCommand(Action<HP_3488A> callback, bool configureAdapter = false)
		{
			using (var device = new HP_3488A(config.GpibHost, config.GpibAddress, configureAdapter))
				callback(device);
		}

		static void Main(string[] args)
		{
			var exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
			var configPath = args.Length >= 1 ? args[0] : Path.Combine(Path.GetDirectoryName(exePath), "config.json");
			config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));

			var lockObj = new object();

			var server = new HttpServer(config.HttpPort);

			server.Log($"Using host '{config.GpibHost}', GPIB address {config.GpibAddress}.");

			DoCommand(dev => dev.Reset(), configureAdapter: true);

			server.AddRoute(null, null, (urlArgs, request, response) =>
			{
				if (config.HttpToken != null && request.Headers["authorization"] != $"Bearer {config.HttpToken}")
					throw new UnauthorizedAccessException();

				return true;
			});

			server.AddRoute("GET", @"/slot/(\d+)/channel/(\d+)", (urlArgs, request, response) =>
			{
				var state = false;

				lock (lockObj)
					state = DoCommand(dev => dev.GetState(int.Parse(urlArgs[0]), int.Parse(urlArgs[1])));

				response.WriteBodyJson(state);
			});

			server.AddRoute("POST", @"/slot/(\d+)/channel/(\d+)", (urlArgs, request, response) =>
			{
				var state = request.ReadBodyJson<bool>();

				lock (lockObj)
					DoCommand(dev => dev.SetState(int.Parse(urlArgs[0]), int.Parse(urlArgs[1]), state));
			});

			server.AddRoute("GET", @"/slot/(\d+)/digital-port/([^/]+)", (urlArgs, request, response) =>
			{
				var port =
					urlArgs[1] == "low-byte" ? HP_3488A.DigitalPort.LowByte :
					urlArgs[1] == "high-byte" ? HP_3488A.DigitalPort.HighByte :
					throw new ArgumentException();

				var value = 0;

				lock (lockObj)
					value = DoCommand(dev => dev.DigitalRead(int.Parse(urlArgs[0]), port));

				response.WriteBodyJson(value);
			});

			server.AddRoute("POST", @"/slot/(\d+)/digital-port/([^/]+)", (urlArgs, request, response) =>
			{
				var port =
					urlArgs[1] == "low-byte" ? HP_3488A.DigitalPort.LowByte :
					urlArgs[1] == "high-byte" ? HP_3488A.DigitalPort.HighByte :
					throw new ArgumentException();

				var value = request.ReadBodyJson<byte>();

				lock (lockObj)
					DoCommand(dev => dev.DigitalWrite(int.Parse(urlArgs[0]), port, value));
			});

			server.AddRoute("POST", @"/slot/(\d+)/reset", (urlArgs, request, response) =>
			{
				lock (lockObj)
					DoCommand(dev => dev.CardReset(int.Parse(urlArgs[0])));
			});

			server.AddExactRoute("POST", @"/reset", (request, response) =>
			{
				lock (lockObj)
					DoCommand(dev => dev.Reset());
			});

			server.AddExactRoute("POST", @"/local", (request, response) =>
			{
				lock (lockObj)
					DoCommand(dev => dev.Local());
			});

			server.AddExactRoute("POST", @"/display/text", (request, response) =>
			{
				var text = request.ReadBodyJson<string>();

				lock (lockObj)
					DoCommand(dev => dev.DisplayString(text));
			});

			server.AddExactRoute("POST", @"/display/clear", (request, response) =>
			{
				lock (lockObj)
					DoCommand(dev => dev.DisplayOn());
			});

			server.Start();

			Task.Delay(-1).Wait();
		}
	}
}
