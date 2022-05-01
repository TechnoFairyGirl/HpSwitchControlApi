using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PrologixGPIB;

namespace HpSwitchControlApi
{
	class HP_3488A : IDisposable
	{
		public enum DigitalPort : int
		{
			LowByte = 0,
			HighByte = 1,
		}

		public string Host { get => gpib.Host; }
		public int Address { get => gpib.Address; }
		public bool Connected { get => gpib.Connected; }

		readonly GPIB gpib;

		public HP_3488A(string host, int address, bool configureAdapter = true)
		{
			gpib = new GPIB(host, address, configureAdapter: configureAdapter);
		}

		public void Dispose() =>
			gpib.Dispose();

		static int ChannelAddress(int slot, int channel) =>
			(100 * slot) + channel;

		public void SetState(int slot, int channel, bool state) =>
			gpib.Query($"{(state ? "OPEN" : "CLOSE")} {ChannelAddress(slot, channel)}");

		public bool GetState(int slot, int channel) =>
			gpib.Query($"VIEW {ChannelAddress(slot, channel)}")
				.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
				[0] == "OPEN";

		public void DigitalWrite(int slot, DigitalPort port, byte val) =>
			gpib.Query($"DWRITE {ChannelAddress(slot, (int)port)},{val}");

		public byte DigitalRead(int slot, DigitalPort port) =>
			byte.Parse(gpib.Query($"DREAD {ChannelAddress(slot, (int)port)}"));

		public void CardReset(int slot) =>
			gpib.Query($"CRESET {slot}");

		public void Local() =>
			gpib.Local();

		public void DisplayString(string str) =>
			gpib.Query($"DISP {str}");

		public void DisplayOn() =>
			gpib.Query($"DON");

		public void DisplayOff() =>
			gpib.Query($"DOFF");

		public void Reset() =>
			gpib.Reset();
	}
}
