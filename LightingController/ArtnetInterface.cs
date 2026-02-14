using System;
using System.Net;
using System.Net.Sockets;

namespace StreamDeckCS2DMX
{
	internal sealed class ArtnetInterface : IDisposable
	{
		private readonly UdpClient _udp;
		private readonly IPEndPoint _endpoint;

		private readonly byte[] _dmx = new byte[512];
		private readonly byte[] _packet = new byte[18 + 512];

		private ushort _universe;
		private byte _sequence;

		public ArtnetInterface(string artnetAddress, ushort universe = 0, bool enableBroadcast = true)
		{
			if (string.IsNullOrWhiteSpace(artnetAddress))
				throw new ArgumentException("Art-Net address is required.", nameof(artnetAddress));

			_universe = universe;

			var ip = IPAddress.Parse(artnetAddress);
			_endpoint = new IPEndPoint(ip, 6454);

			_udp = new UdpClient();
			if (enableBroadcast)
				_udp.EnableBroadcast = true;

			BuildHeader();
		}

		// channel: 1..512, value: 0..255
		public void SetChannel(int channel, int value)
		{
			if ((uint)(channel - 1) >= 512u)
				throw new ArgumentOutOfRangeException(nameof(channel), "Channel must be 1..512.");

			_dmx[channel - 1] = (byte)Clamp(value, 0, 255);
		}

		public void Submit()
		{
			_sequence++;
			if (_sequence == 0) _sequence = 1;
			_packet[12] = _sequence;

			Buffer.BlockCopy(_dmx, 0, _packet, 18, 512);
			_udp.Send(_packet, _packet.Length, _endpoint);
		}

		public void Clear(int value = 0)
		{
			Array.Fill(_dmx, (byte)Clamp(value, 0, 255));
		}

		private void BuildHeader()
		{
			// "Art-Net\0"
			_packet[0] = (byte)'A';
			_packet[1] = (byte)'r';
			_packet[2] = (byte)'t';
			_packet[3] = (byte)'-';
			_packet[4] = (byte)'N';
			_packet[5] = (byte)'e';
			_packet[6] = (byte)'t';
			_packet[7] = 0x00;

			// OpCode ArtDMX = 0x5000 (little-endian)
			_packet[8] = 0x00;
			_packet[9] = 0x50;

			// ProtVer = 14 (0x000E) big-endian
			_packet[10] = 0x00;
			_packet[11] = 0x0E;

			// Sequence (set in Submit)
			_packet[12] = 0x00;

			// Physical
			_packet[13] = 0x00;

			// Universe (little-endian)
			_packet[14] = (byte)(_universe & 0xFF);
			_packet[15] = (byte)((_universe >> 8) & 0xFF);

			// Length (big-endian): 512 = 0x0200
			_packet[16] = 0x02;
			_packet[17] = 0x00;
		}

		private static int Clamp(int v, int lo, int hi)
		{
			if (v < lo) return lo;
			if (v > hi) return hi;
			return v;
		}

		public void Dispose()
		{
			_udp.Dispose();
		}
	}
}
