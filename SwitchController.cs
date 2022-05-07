using SimpleHttp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HpSwitchControlApi
{
	static class SwitchController
	{
		static T SwitchCommand<T>(Func<HP_3488A, T> callback, bool configureAdapter = false)
		{
			using (var device = new HP_3488A(Program.Config.GpibHost, Program.Config.SwitchGpibAddress, configureAdapter))
				return callback(device);
		}

		static void SwitchCommand(Action<HP_3488A> callback, bool configureAdapter = false)
		{
			using (var device = new HP_3488A(Program.Config.GpibHost, Program.Config.SwitchGpibAddress, configureAdapter))
				callback(device);
		}

		public static void Configure(HttpServer server, object lockObj)
		{
			server.Log($"Using switch GPIB address {Program.Config.SwitchGpibAddress}.");

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
	}
}
