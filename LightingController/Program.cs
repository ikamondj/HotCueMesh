using StreamDeckCS2DMX;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.IO.Pipes;
using System.Text;

MidiInputManager midiInputManager = new MidiInputManager();
AudioManager audioManager = new AudioManager();
ArtnetInterface artnetInterface = new ArtnetInterface("169.254.0.18", 0);



int ValToDmxSlider(float val)
{
	return (int)Math.Clamp(val * 255.0, 0, 255);
}



//Dictionary<int, Action<PresetInput>> presets = new Dictionary<int, Action<PresetInput>>();
Dictionary<int, IPreset> presets = new Dictionary<int, IPreset>();

using var udp = new UdpFireAndForget("127.0.0.1", 7779);
int prIndex = -1;

foreach (IPreset preset in Preset.GetPresets(artnetInterface))
{
	presets.Add(++prIndex, preset);
}
static void setPreset(ArtnetInterface artnetInterface, int presetIndex, UdpFireAndForget udp, out int currentPreset)
{
	Console.WriteLine($"preset {presetIndex}");
	currentPreset = presetIndex;
	DmxMappings.SetMany(artnetInterface, 0, 0, false, false, false, false, 255, 0, 0, 0, 0, 0, 0, 0, null, 0, 0, 0, 0);


	udp.Send($"pre:{presetIndex}");
}

static void gotMidiMessage(ArtnetInterface artnetInterface, MidiControlChangeEventArgs e, UdpFireAndForget udp, ref int currentPreset, ref bool strobeActive)
{
	Console.WriteLine($"Got Midi Message {e.Controller}:{e.Value}");
	try
	{
		switch (e.Controller)
		{
			case 14:
				DmxMappings.HandleStrobe(artnetInterface, e.Value);
				strobeActive = (e.Value == 16 || e.Value == 32 || e.Value == 48 || e.Value == 64);
				break;
			case 3:
				DmxMappings.CutLight(artnetInterface, e.Value > 64);
				break;
			case 9:
				setPreset(artnetInterface, e.Value, udp, out currentPreset); // clamp/map to 1..7
				break;
		}
	}
	catch (Exception ex)
	{
		Console.WriteLine($"[MIDI CALLBACK EXCEPTION] {ex}");
	}
}




using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (s, ev) =>
{
	ev.Cancel = true;
	cts.Cancel();
};

Console.WriteLine("Starting audio loopback capture...");
audioManager.Initialize();

Console.WriteLine("Listening for MIDI + audio... press Ctrl+C to quit.");
try
{
	// Create a dispatcher that knows how to route to the current preset.
	void DispatchPreset(PresetInput inp, ref int currentPreset)
	{
		int idx = Interlocked.CompareExchange(ref currentPreset, 0, 0);
		if (presets.TryGetValue(idx, out var pres))
			pres.Process(inp);
	}

	await RunAudioLoop(audioManager, DispatchPreset, cts.Token, midiInputManager, artnetInterface, udp, null);
}
finally
{
	audioManager.Dispose();
	artnetInterface.Dispose();
	udp.Dispose();
}

static void FftRadix2(float[] re, float[] im)
{
	int n = re.Length;
	if ((n & (n - 1)) != 0) throw new ArgumentException("FFT size must be power of 2.");

	// Bit-reversal permutation
	int j = 0;
	for (int i = 1; i < n; i++)
	{
		int bit = n >> 1;
		while ((j & bit) != 0) { j ^= bit; bit >>= 1; }
		j ^= bit;

		if (i < j)
		{
			(re[i], re[j]) = (re[j], re[i]);
			(im[i], im[j]) = (im[j], im[i]);
		}
	}

	// Cooley-Tukey
	for (int len = 2; len <= n; len <<= 1)
	{
		double ang = -2.0 * Math.PI / len;
		float wlenRe = (float)Math.Cos(ang);
		float wlenIm = (float)Math.Sin(ang);

		for (int i = 0; i < n; i += len)
		{
			float wRe = 1f;
			float wIm = 0f;

			int half = len >> 1;
			for (int k = 0; k < half; k++)
			{
				int u = i + k;
				int v = u + half;

				float vr = re[v] * wRe - im[v] * wIm;
				float vi = re[v] * wIm + im[v] * wRe;

				float ur = re[u];
				float ui = im[u];

				re[u] = ur + vr;
				im[u] = ui + vi;
				re[v] = ur - vr;
				im[v] = ui - vi;

				float nextWRe = wRe * wlenRe - wIm * wlenIm;
				float nextWIm = wRe * wlenIm + wIm * wlenRe;
				wRe = nextWRe;
				wIm = nextWIm;
			}
		}
	}
}



// Approx A-weighting curve in dB for frequency in Hz.
// Returns ~0 dB around 1–5 kHz, negative for low freq, slightly negative for very high freq.
static float AWeightingDb(float f)
{
	// A-weighting is undefined at f=0; treat DC as heavily de-emphasized.
	if (f <= 0f) return -80f;

	double f2 = f * f;

	// Standard A-weighting formula terms (in Hz)
	double ra =
		(Math.Pow(12200.0, 2) * Math.Pow(f, 4)) /
		((f2 + Math.Pow(20.6, 2)) *
		 Math.Sqrt((f2 + Math.Pow(107.7, 2)) * (f2 + Math.Pow(737.9, 2))) *
		 (f2 + Math.Pow(12200.0, 2)));

	double a = 20.0 * Math.Log10(ra) + 2.0;
	return (float)a;
}

static async Task RunAudioLoop(
	AudioManager audioManager,
	PresetDel dispatchPreset,
	CancellationToken ct, 
	MidiInputManager mim, ArtnetInterface artnetInterface, UdpFireAndForget udp, UDPListener? udpListen)
{
	const int N = 2048;                // FFT size (power of 2)
	const int PrintEveryMs = 100;

	var nextPrint = Environment.TickCount64;

	// Precompute Hann window
	var window = new float[N];
	for (int i = 0; i < N; i++)
		window[i] = 0.5f - 0.5f * (float)Math.Cos(2.0 * Math.PI * i / (N - 1));

	// FFT buffers
	var re = new float[N];
	var im = new float[N];

	// Normalization: a slow-moving "typical loudness" reference
	// (prevents everything being pinned at 0 or 1)
	float emaRef = 1e-6f;
	const float emaAlpha = 0.02f; // smaller = slower adaptation
	int currentPreset = 1;
	bool strobeActive = false;

	while (!ct.IsCancellationRequested)
	{
		try
		{
			MidiControlChangeEventArgs? ev = null;
			while (mim.midiControlEvents.Reader.TryRead(out ev))
			{
				if (ev != null)
				{
					gotMidiMessage(artnetInterface, ev, udp, ref currentPreset, ref strobeActive);
				}
				ev = null;
			}
			if (udpListen != null) { 
				while (udpListen.ControlChangeReader.TryRead(out ev))
				{
					if (ev != null)
					{
						gotMidiMessage(artnetInterface, ev, udp, ref currentPreset, ref strobeActive);
					}
					ev = null;
				}
			}
		} catch (Exception e)
		{
			Console.WriteLine(e);
		}
		float[] samples;
		try
		{

			samples = await audioManager.GetAudioSamplesAsync(N);
		}
		catch (OperationCanceledException)
		{
			break;
		}

		// Downmix to mono if interleaved stereo.
		// If it’s actually mono, this still works (it just averages pairs).
		for (int i = 0; i < N; i++)
		{
			float mono;
			int j = i * 2;
			if (j + 1 < samples.Length)
				mono = 0.5f * (samples[j] + samples[j + 1]);
			else
				mono = samples[i];

			re[i] = mono * window[i];
			im[i] = 0f;
		}

		// FFT in-place
		FftRadix2(re, im);

		// Convert to perceptually-weighted magnitudes in [0,1]
		// Only use bins 0..N/2 (real FFT symmetric)
		int half = N / 2;
		var bins = new float[half];

		// Update reference based on current energy (pre-weighting).
		// Use RMS-ish magnitude proxy across a few bins (skip DC).
		float energy = 0f;
		for (int k = 1; k < half; k++)
		{
			float mag = (float)Math.Sqrt(re[k] * re[k] + im[k] * im[k]);
			energy += mag;
		}
		energy /= (half - 1);
		emaRef = (1f - emaAlpha) * emaRef + emaAlpha * Math.Max(energy, 1e-6f);

		// Build weighted, normalized bins
		// freq = k * Fs / N. We need sample rate from AudioManager.
		// If you don’t have it yet, add a property for it (shown below).
		float fs = audioManager.SampleRate;

		for (int k = 0; k < half; k++)
		{
			float freq = (k * fs) / N;

			// Magnitude scaled to something stable
			float mag = (float)Math.Sqrt(re[k] * re[k] + im[k] * im[k]);

			// A-weighting (approx human loudness)
			float aDb = AWeightingDb(freq);              // negative for low freq
			float aLin = (float)Math.Pow(10.0, aDb / 20.0);

			float weighted = mag * aLin;

			// Normalize against reference, then compress to [0,1]
			//  - ref scales with typical signal energy
			//  - sqrt is a light dynamic range compression
			float x = weighted / (emaRef + 1e-9f);
			x = (float)Math.Sqrt(Math.Max(0f, x));

			// Clamp to [0,1]
			if (x > 1f) x = 1f;

			bins[k] = x;
		}

		// 3) cumulative transform: out[i] = sum_{0..i} bins[k]
		// Then renormalize to [0,1] (otherwise it will grow with i).
		var cumulative = new float[half];
		float running = 0f;
		for (int i = 0; i < half; i++)
		{
			running += bins[i];
			cumulative[i] = running;
		}


		var now = Environment.TickCount64;
		if (now >= nextPrint)
		{
			nextPrint = now + PrintEveryMs;
			//Console.WriteLine($"fft cum[0]={cumulative[0]:0.000} cum[8]={cumulative[Math.Min(8, half - 1)]:0.000} cum[last]={cumulative[half - 1]:0.000}");
		}
		float norm = 4.0f / cumulative.Length;
		float bass = (cumulative[cumulative.Length / 4] - cumulative[0]) * norm;
		float lows = (cumulative[cumulative.Length / 2] - cumulative[cumulative.Length / 4]) * norm;
		float mids = (cumulative[3 * cumulative.Length / 4] - cumulative[cumulative.Length / 2]) * norm;
		float high = (cumulative[cumulative.Length - 1] - cumulative[3 * cumulative.Length / 4]) * norm;
		//Console.WriteLine($"Preset #{currentPreset}, blmt{bass},{lows},{mids},{high},{strobeActive}");
		dispatchPreset(new PresetInput() { bass=bass, lows=lows, mids=mids, treb=high, strobeActive=strobeActive}, ref currentPreset);
	}
}

struct PresetInput
{
	public float bass, lows, mids, treb;
	public bool strobeActive;
};

delegate void PresetDel(PresetInput input, ref int curretPreset);