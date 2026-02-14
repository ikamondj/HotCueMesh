using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

// NOTE: assumes you already have this type (e.g., from NAudio.Midi or your own).
// If it's NAudio.Midi.MidiControlChangeEventArgs, the constructor signature differs.
// This implementation only requires: int channel, int controller, int value.
// Adapt CreateEvent(...) if your MidiControlChangeEventArgs differs.
namespace StreamDeckCS2DMX
{
	internal sealed class UDPListener : IDisposable
	{
		private readonly Channel<MidiControlChangeEventArgs> _controlChange;
		private readonly CancellationTokenSource _cts = new();
		private readonly UdpClient _udp;
		private readonly Task _loopTask;

		private bool _disposed;

		public ChannelReader<MidiControlChangeEventArgs> ControlChangeReader => _controlChange.Reader;

		/// <summary>
		/// Listens on the given UDP port. Messages are expected as UTF-8 text in one of these forms:
		///   1) "cc <midiChannel> <controller> <value>"   (e.g., "cc 1 74 127")
		///   2) "<midiChannel>,<controller>,<value>"      (e.g., "1,74,127")
		///   3) "<midiChannel> <controller> <value>"      (e.g., "1 74 127")
		/// midiChannel is 1..16 (will be converted to 0..15 internally if your args expect 0-based).
		/// </summary>
		public UDPListener(int listenPort, IPAddress? listenAddress = null)
		{
			_controlChange = Channel.CreateUnbounded<MidiControlChangeEventArgs>(
				new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });

			var ep = new IPEndPoint(listenAddress ?? IPAddress.Any, listenPort);
			_udp = new UdpClient(ep);

			_loopTask = Task.Run(() => ListenLoopAsync(_cts.Token));
		}

		private async Task ListenLoopAsync(CancellationToken ct)
		{
			try
			{
				while (!ct.IsCancellationRequested)
				{
					UdpReceiveResult recv;
					try
					{
						// Disposing/closing UdpClient will typically throw here and break out cleanly.
						recv = await _udp.ReceiveAsync(ct).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						break;
					}
					catch (ObjectDisposedException)
					{
						break;
					}
					catch (SocketException)
					{
						// Most commonly raised when the socket is closed during shutdown.
						break;
					}

					var text = Encoding.UTF8.GetString(recv.Buffer).Trim();
					if (text.Length == 0) continue;

					if (!TryParseControlChange(text, out var midiChannel1Based, out var controller, out var value))
						continue;

					// Clamp to typical MIDI ranges.
					midiChannel1Based = Math.Clamp(midiChannel1Based, 1, 16);
					controller = Math.Clamp(controller, 0, 127);
					value = Math.Clamp(value, 0, 127);

					var evt = CreateEvent(midiChannel1Based, controller, value);

					// If the channel is closed (during dispose), TryWrite will return false.
					_controlChange.Writer.TryWrite(evt);
				}
			}
			finally
			{
				_controlChange.Writer.TryComplete();
			}
		}

		private static bool TryParseControlChange(string s, out int midiChannel1Based, out int controller, out int value)
		{
			midiChannel1Based = 0;
			controller = 0;
			value = 0;

			// Accept: "cc 1 74 127"
			var trimmed = s.Trim();
			if (trimmed.StartsWith("cc", StringComparison.OrdinalIgnoreCase))
			{
				trimmed = trimmed[2..].Trim();
			}

			// Accept comma or whitespace separated
			var parts = trimmed
				.Split(new[] { ' ', '\t', '\r', '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length < 3) return false;

			return int.TryParse(parts[0], out midiChannel1Based)
				&& int.TryParse(parts[1], out controller)
				&& int.TryParse(parts[2], out value);
		}

		private static MidiControlChangeEventArgs CreateEvent(int midiChannel1Based, int controller, int value)
		{
			// If your event args expect 0-based MIDI channel (0..15), convert here:
			// int ch0 = midiChannel1Based - 1;

			// TODO: adjust to your actual MidiControlChangeEventArgs type/constructor.
			// Common pattern for a custom type:
			return new MidiControlChangeEventArgs(0, "", 0, controller, value);
		}

		public void Dispose()
		{
			if (_disposed) return;
			_disposed = true;

			// Signal cancellation and unblock ReceiveAsync by closing the socket.
			try { _cts.Cancel(); } catch { /* ignore */ }

			try { _udp.Close(); } catch { /* ignore */ }
			_udp.Dispose();

			try
			{
				// Ensure the background task is not left running.
				_loopTask.Wait(TimeSpan.FromSeconds(2));
			}
			catch { /* ignore */ }

			_cts.Dispose();
		}
	}

	// If you don't already have MidiControlChangeEventArgs, here's a minimal one.
	// Remove this if you're using a library-provided type.
	
}
