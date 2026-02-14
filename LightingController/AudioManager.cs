using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Channels;
using NAudio.CoreAudioApi;
using NAudio.Wasapi;
using NAudio.Wave;

namespace StreamDeckCS2DMX
{
	
	internal class AudioManager
	{
		string audioOutputName = "Speakers (Realtek(R) Audio)";
		Channel<float>? channel = null;
		WasapiLoopbackCapture? loopbackCapture = null;

		public float SampleRate = 48_000.0f;

		public AudioManager()
		{
			channel = Channel.CreateBounded<float>(
			new BoundedChannelOptions(1_024)
			{
				SingleWriter = true,
				SingleReader = true,
				AllowSynchronousContinuations = false,
				FullMode = BoundedChannelFullMode.DropOldest
			});
		}

		public void Initialize()
		{
			try
			{
				MMDeviceEnumerator enumerator = new MMDeviceEnumerator();

				// If you truly want by name, only search ACTIVE render devices
				var device = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
				    .FirstOrDefault(d => string.Equals(d.FriendlyName, audioOutputName, StringComparison.OrdinalIgnoreCase))
				    ?? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

				if (device == null)
				{
					throw new InvalidOperationException($"Audio device '{audioOutputName}' not found.");
				}

				// Initialize loopback capture
				loopbackCapture = new WasapiLoopbackCapture(device);
				loopbackCapture.DataAvailable += LoopbackCapture_DataAvailable;
				loopbackCapture.RecordingStopped += LoopbackCapture_RecordingStopped;
				loopbackCapture.StartRecording();
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error initializing audio capture: {ex.Message}");
				throw;
			}
		}

		private void LoopbackCapture_DataAvailable(object? sender, WaveInEventArgs e)
		{
			// Convert byte data to float samples
			float[] samples = ConvertBytesToFloats(e.Buffer, e.BytesRecorded);
			AudioCallback(samples);
		}

		private void LoopbackCapture_RecordingStopped(object? sender, StoppedEventArgs e)
		{
			if (e.Exception != null)
			{
				Console.WriteLine($"Audio recording error: {e.Exception.Message}");
			}
		}

		private float[] ConvertBytesToFloats(byte[] buffer, int bytesRecorded)
		{
			// Assuming 32-bit float format (standard for WASAPI)
			int sampleCount = bytesRecorded / 4;
			float[] samples = new float[sampleCount];

			for (int i = 0; i < sampleCount; i++)
			{
				samples[i] = BitConverter.ToSingle(buffer, i * 4);
			}

			return samples;
		}

		public void AudioCallback(float[] samples)
		{
			if (channel == null)
			{
				return;
			}

			foreach (var sample in samples)
			{
				channel.Writer.TryWrite(sample);
			}
		}


		public async Task<float[]> GetAudioSamplesAsync(int sampleCount)
		{
			if (channel == null)
			{
				throw new InvalidOperationException("Audio channel is not initialized.");
			}

			var samples = new float[sampleCount];
			for (int i = 0; i < sampleCount; i++)
			{
				samples[i] = await channel.Reader.ReadAsync();
			}
			return samples;
		}

		public void Dispose()
		{
			loopbackCapture?.StopRecording();
			loopbackCapture?.Dispose();
			channel?.Writer.Complete();
		}
	}
}
