using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

sealed class UdpFireAndForget : IDisposable
{
	private readonly UdpClient _udp;
	private readonly IPEndPoint _dest;

	public UdpFireAndForget(string host, int port)
	{
		_udp = new UdpClient(); // ephemeral local port
		_dest = new IPEndPoint(IPAddress.Parse(host), port);
	}

	public void Send(string text)
	{
		// Fire-and-forget: ignore errors
		try
		{
			byte[] bytes = Encoding.UTF8.GetBytes(text);
			_udp.Send(bytes, bytes.Length, _dest);
		}
		catch { }
	}

	public void Dispose()
	{
		try { _udp.Dispose(); } catch { }
	}
}
