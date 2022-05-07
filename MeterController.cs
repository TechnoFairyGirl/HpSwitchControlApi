using SimpleHttp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HpSwitchControlApi
{
	static class MeterController
	{
		static T MeterCommand<T>(Func<HP_3437A, T> callback, bool configureAdapter = false)
		{
			using (var device = new HP_3437A(Program.Config.GpibHost, Program.Config.MeterGpibAddress, configureAdapter))
				return callback(device);
		}

		static void MeterCommand(Action<HP_3437A> callback, bool configureAdapter = false)
		{
			using (var device = new HP_3437A(Program.Config.GpibHost, Program.Config.MeterGpibAddress, configureAdapter))
				callback(device);
		}

		public static void Configure(HttpServer server, object lockObj)
		{
			server.Log($"Using meter GPIB address {Program.Config.MeterGpibAddress}.");

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
				double? volts = null;

				double? range = 
					request.QueryParams.ContainsKey("range") ?
					double.Parse(request.QueryParams["range"]) : (double?)null;

				int average =
					request.QueryParams.ContainsKey("average") ?
					int.Parse(request.QueryParams["average"]) : 1;

				if (average < 1 || average > 100)
					throw new ArgumentOutOfRangeException("average");

				lock (lockObj)
					volts = MeterCommand(dev =>
					{
						if (range != null)
							dev.SetRange(range ?? throw new NullReferenceException());

						double sumVolts = 0;
						for (var i = 0; i < average; i++)
							sumVolts += dev.ReadVolts();
						return sumVolts / average;
					});

				response.WriteBodyJson(volts);
			});

			server.AddExactRoute("POST", @"/meter/range", (request, response) =>
			{
				var range = request.ReadBodyJson<double>();

				lock (lockObj)
					MeterCommand(dev => dev.SetRange(range));
			});
		}
	}
}
