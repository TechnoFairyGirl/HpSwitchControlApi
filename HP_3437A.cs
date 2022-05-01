using PrologixGPIB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HpSwitchControlApi
{
	class HP_3437A : IDisposable
	{
		public string Host { get => gpib.Host; }
		public int Address { get => gpib.Address; }
		public bool Connected { get => gpib.Connected; }

		readonly GPIB gpib;

		public HP_3437A(string host, int address, bool configureAdapter = true)
		{
			gpib = new GPIB(host, address, configureAdapter: configureAdapter);
		}

		public void Dispose() =>
			gpib.Dispose();

		public void Reset() =>
			gpib.Reset();

		public void Local() =>
			gpib.Local();

		public double ReadVolts() =>
			double.Parse(gpib.ReceiveLine());

		public void SetRange(double range)
		{
			gpib.Send(
				range == 0.1 ? "R1" :
				range == 1 ? "R2" :
				range == 10 ? "R3" :
				throw new ArgumentException());
		}
	}
}
