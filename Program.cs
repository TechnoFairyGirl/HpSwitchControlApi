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
		static Config config;

		static T SwitchCommand<T>(Func<HP_3488A, T> callback, bool configureAdapter = false)
		{
			using (var device = new HP_3488A(config.GpibHost, config.SwitchGpibAddress, configureAdapter))
				return callback(device);
		}

		static void SwitchCommand(Action<HP_3488A> callback, bool configureAdapter = false)
		{
			using (var device = new HP_3488A(config.GpibHost, config.SwitchGpibAddress, configureAdapter))
				callback(device);
		}

		static T MeterCommand<T>(Func<HP_3437A, T> callback, bool configureAdapter = false)
		{
			using (var device = new HP_3437A(config.GpibHost, config.MeterGpibAddress, configureAdapter))
				return callback(device);
		}

		static void MeterCommand(Action<HP_3437A> callback, bool configureAdapter = false)
		{
			using (var device = new HP_3437A(config.GpibHost, config.MeterGpibAddress, configureAdapter))
				callback(device);
		}

		static void Main(string[] args)
		{
			var exePath = System.Reflection.Assembly.GetEntryAssembly().Location;
			var configPath = args.Length >= 1 ? args[0] : Path.Combine(Path.GetDirectoryName(exePath), "config.json");
			config = JsonConvert.DeserializeObject<Config>(File.ReadAllText(configPath));

			var lockObj = new object();

			var server = new HttpServer(config.HttpPort);

			server.AddRoute(null, null, (urlArgs, request, response) =>
			{
				if (config.HttpToken != null && request.Headers["authorization"] != $"Bearer {config.HttpToken}")
					throw new UnauthorizedAccessException();

				return true;
			});

			server.Log($"Using adapter host '{config.GpibHost}'.");

			if (config.SwitchGpibAddress != null)
			{
				server.Log($"Using switch GPIB address {config.SwitchGpibAddress}.");

				SwitchCommand(dev => dev.Reset(), configureAdapter: true);

				server.AddRoute("GET", @"/switch/slot/(\d+)/channel/(\d+)", (urlArgs, request, response) =>
				{
					var state = false;

					lock (lockObj)
						state = SwitchCommand(dev => dev.GetState(int.Parse(urlArgs[0]), int.Parse(urlArgs[1])));

					response.WriteBodyJson(state);
				});

				server.AddRoute("POST", @"/switch/slot/(\d+)/channel/(\d+)", (urlArgs, request, response) =>
				{
					var state = request.ReadBodyJson<bool>();

					lock (lockObj)
						SwitchCommand(dev => dev.SetState(int.Parse(urlArgs[0]), int.Parse(urlArgs[1]), state));
				});

				server.AddRoute("GET", @"/switch/slot/(\d+)/digital-port/([^/]+)", (urlArgs, request, response) =>
				{
					var port =
						urlArgs[1] == "low-byte" ? HP_3488A.DigitalPort.LowByte :
						urlArgs[1] == "high-byte" ? HP_3488A.DigitalPort.HighByte :
						throw new ArgumentException();

					var value = 0;

					lock (lockObj)
						value = SwitchCommand(dev => dev.DigitalRead(int.Parse(urlArgs[0]), port));

					response.WriteBodyJson(value);
				});

				server.AddRoute("POST", @"/switch/slot/(\d+)/digital-port/([^/]+)", (urlArgs, request, response) =>
				{
					var port =
						urlArgs[1] == "low-byte" ? HP_3488A.DigitalPort.LowByte :
						urlArgs[1] == "high-byte" ? HP_3488A.DigitalPort.HighByte :
						throw new ArgumentException();

					var value = request.ReadBodyJson<byte>();

					lock (lockObj)
						SwitchCommand(dev => dev.DigitalWrite(int.Parse(urlArgs[0]), port, value));
				});

				server.AddRoute("POST", @"/switch/slot/(\d+)/reset", (urlArgs, request, response) =>
				{
					lock (lockObj)
						SwitchCommand(dev => dev.CardReset(int.Parse(urlArgs[0])));
				});

				server.AddExactRoute("POST", @"/switch/reset", (request, response) =>
				{
					lock (lockObj)
						SwitchCommand(dev => dev.Reset());
				});

				server.AddExactRoute("POST", @"/switch/local", (request, response) =>
				{
					lock (lockObj)
						SwitchCommand(dev => dev.Local());
				});

				server.AddExactRoute("POST", @"/switch/display/text", (request, response) =>
				{
					var text = request.ReadBodyJson<string>();

					lock (lockObj)
						SwitchCommand(dev => dev.DisplayString(text));
				});

				server.AddExactRoute("POST", @"/switch/display/clear", (request, response) =>
				{
					lock (lockObj)
						SwitchCommand(dev => dev.DisplayOn());
				});
			}

			if (config.MeterGpibAddress != null)
			{
				server.Log($"Using meter GPIB address {config.MeterGpibAddress}.");

				MeterCommand(dev => dev.Reset(), configureAdapter: true);

				server.AddExactRoute("POST", @"/meter/reset", (request, response) =>
				{
					lock (lockObj)
						MeterCommand(dev => dev.Reset());
				});

				server.AddExactRoute("POST", @"/meter/local", (request, response) =>
				{
					lock (lockObj)
						MeterCommand(dev => dev.Local());
				});

				server.AddExactRoute("GET", @"/meter/volts", (request, response) =>
				{
					var volts = 0d;

					lock (lockObj)
						volts = MeterCommand(dev => dev.ReadVolts());

					response.WriteBodyJson(volts);
				});

				server.AddExactRoute("POST", @"/meter/range", (request, response) =>
				{
					var range = request.ReadBodyJson<double>();

					lock (lockObj)
						MeterCommand(dev => dev.SetRange(range));
				});
			}

			server.Start();

			Task.Delay(-1).Wait();
		}
	}
}
