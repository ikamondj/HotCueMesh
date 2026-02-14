
using System;
using System.Collections.Generic;
using NAudio.Midi;
using System.Threading.Channels;

namespace StreamDeckCS2DMX
{
    /// <summary>
    /// Listens to all available MIDI input devices and raises events for control change messages.
    /// Uses NAudio callbacks, so no polling or blocking is required.
    /// </summary>
    public sealed class MidiInputManager : IDisposable
    {
        private readonly List<MidiIn> _inputs = new();
        private readonly Dictionary<MidiIn, InputInfo> _inputInfo = new();

        public event EventHandler<MidiControlChangeEventArgs>? ControlChangeReceived;

        public Channel<MidiControlChangeEventArgs> midiControlEvents { get; private set; }

        /// <summary>
        /// Starts listening to the StreamDeck2DMX loopMIDI device only.
        /// </summary>
        public MidiInputManager()
        {
            const string targetDeviceName = "StreamDeck2DMX";
            var deviceCount = MidiIn.NumberOfDevices;
            midiControlEvents = Channel.CreateUnbounded<MidiControlChangeEventArgs>();

            for (var deviceIndex = 0; deviceIndex < deviceCount; deviceIndex++)
            {
                var info = MidiIn.DeviceInfo(deviceIndex);
                var productName = info.ProductName ?? string.Empty;

                if (productName.IndexOf(targetDeviceName, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var input = new MidiIn(deviceIndex);
                _inputInfo[input] = new InputInfo(deviceIndex, productName.Length > 0 ? productName : $"MIDI In {deviceIndex}");

                input.MessageReceived += OnMessageReceived;
				input.ErrorReceived += OnErrorReceived;
				input.Start();
				

				_inputs.Add(input);
                return;
            }

            throw new InvalidOperationException($"MIDI input device containing '{targetDeviceName}' not found.");
        }

		private void OnErrorReceived(object? sender, MidiInMessageEventArgs e)
		{
			Console.WriteLine($"[MIDI ERROR] {e.RawMessage:X8}");
		}


		private void OnMessageReceived(object? sender, MidiInMessageEventArgs e)
        {
            if (e.MidiEvent is not ControlChangeEvent cc)
            {
                return;
            }

            var meta = sender is MidiIn midiIn && _inputInfo.TryGetValue(midiIn, out var info)
                ? info
                : new InputInfo(-1, "Unknown");

            //ControlChangeReceived?.Invoke(
            //    this,
            //    new MidiControlChangeEventArgs(
            //        meta.Index,
            //        meta.Name,
            //        cc.Channel,
            //        (int)cc.Controller,
            //        cc.ControllerValue));
            bool success = midiControlEvents.Writer.TryWrite(new MidiControlChangeEventArgs(
					meta.Index,
					meta.Name,
					cc.Channel,
					(int)cc.Controller,
					cc.ControllerValue));
            if (success)
            {
				Console.WriteLine($"Sending midi message {e}");

			}

		}

        public void Dispose()
        {
            foreach (var input in _inputs)
            {
                input.MessageReceived -= OnMessageReceived;
                try { input.Stop(); } catch { /* best effort shutdown */ }
                input.Dispose();
            }

            Console.WriteLine("Closing Thread");

            _inputs.Clear();
            _inputInfo.Clear();
        }

        private readonly record struct InputInfo(int Index, string Name);
    }

    public sealed class MidiControlChangeEventArgs : EventArgs
    {
        public int DeviceIndex { get; }
        public string DeviceName { get; }
        public int Channel { get; }
        public int Controller { get; }
        public int Value { get; }

        public MidiControlChangeEventArgs(int deviceIndex, string deviceName, int channel, int controller, int value)
        {
            DeviceIndex = deviceIndex;
            DeviceName = deviceName;
            Channel = channel;
            Controller = controller;
            Value = value;
        }
    }
}
