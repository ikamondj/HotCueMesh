using System;

namespace StreamDeckCS2DMX
{
	internal static class DmxMappings
	{
		// 1-based channel map :contentReference[oaicite:8]{index=8}
		private const int CH_MASTER = 1;

		private const int CH_LASER_G = 2;
		private const int CH_LASER_R1 = 3;
		private const int CH_LASER_B = 4;
		private const int CH_LASER_R2 = 5;

		private const int CH_MOTOR_SPEED = 6;

		private const int CH_CHECKER_R = 7;
		private const int CH_CHECKER_G = 8;
		private const int CH_CHECKER_B = 9;
		private const int CH_CHECKER_W = 10;

		private const int CH_SOFT_R = 11;
		private const int CH_SOFT_G = 12;
		private const int CH_SOFT_B = 13;

		private const int CH_STROBE_CTRL = 14;
		private const int CH_STROBE_R = 15;
		private const int CH_STROBE_G = 16;
		private const int CH_STROBE_B = 17;
		private const int CH_STROBE_U = 18;

		// Master values :contentReference[oaicite:9]{index=9}
		private const int MASTER_NORMAL = 0;
		private const int MASTER_BLACKOUT = 132;

		// Strobe modes :contentReference[oaicite:10]{index=10}
		private const int STROBE_OFF = 0;
		private const int STROBE_ON = 1;
		private const int STROBE_RANDOM = 5;
		private const int STROBE_RANDOM_MIN = 7;
		private const int STROBE_RANDOM_MAX = 20;
		private const int STROBE_STEADY_MIN = 30;

		// Cache only channels you care about (1..18). Mirrors Python behavior. :contentReference[oaicite:11]{index=11}
		private static readonly int[] _cache = InitCache();

		private static int[] InitCache()
		{
			var a = new int[19]; // ignore index 0
			for (int ch = 1; ch <= 18; ch++)
				a[ch] = -1; // seed so first write always "changes" :contentReference[oaicite:12]{index=12}
			return a;
		}

		// -----------------------
		// Public: one-off actions
		// -----------------------

		public static void CutLight(ArtnetInterface artnet, bool cut)
		{
			SetMaster(artnet, cut ? MASTER_BLACKOUT : MASTER_NORMAL);
		}

		public static void MasterNormal(ArtnetInterface artnet) => SetMaster(artnet, MASTER_NORMAL);
		public static void MasterBlackout(ArtnetInterface artnet) => SetMaster(artnet, MASTER_BLACKOUT);

		public static void SetMaster(ArtnetInterface artnet, int value)
		{
			SetAndSubmit(artnet, CH_MASTER, value);
		}

		public static void SetMotorSpeed(ArtnetInterface artnet, int value)
		{
			SetAndSubmit(artnet, CH_MOTOR_SPEED, value);
		}

		public static void SetDmxDirectly(ArtnetInterface artnet, int channel, int value)
		{
			// Python supports 1..512 direct set :contentReference[oaicite:13]{index=13}
			artnet.SetChannel(channel, Clamp(value, 0, 255));
			artnet.Submit();
		}

		// -----------------------
		// Lasers (on/off)
		// -----------------------

		public static void SetLaserGreen(ArtnetInterface artnet, bool on, int onValue = 255) =>
			SetOnOffChannelAndSubmit(artnet, CH_LASER_G, on, onValue);

		public static void SetLaserBlue(ArtnetInterface artnet, bool on, int onValue = 255) =>
			SetOnOffChannelAndSubmit(artnet, CH_LASER_B, on, onValue);

		public static void SetLaserRed1(ArtnetInterface artnet, bool on, int onValue = 255) =>
			SetOnOffChannelAndSubmit(artnet, CH_LASER_R1, on, onValue);

		public static void SetLaserRed2(ArtnetInterface artnet, bool on, int onValue = 255) =>
			SetOnOffChannelAndSubmit(artnet, CH_LASER_R2, on, onValue);

		// Batch lasers then submit once :contentReference[oaicite:14]{index=14}
		public static void SetLasers(
			ArtnetInterface artnet,
			bool? green = null,
			bool? red1 = null,
			bool? red2 = null,
			bool? blue = null,
			int onValue = 255)
		{
			bool changed = false;
			if (green is not null) changed |= SetChannelCached(artnet, CH_LASER_G, OnOffValue(green.Value, onValue));
			if (red1 is not null) changed |= SetChannelCached(artnet, CH_LASER_R1, OnOffValue(red1.Value, onValue));
			if (red2 is not null) changed |= SetChannelCached(artnet, CH_LASER_R2, OnOffValue(red2.Value, onValue));
			if (blue is not null) changed |= SetChannelCached(artnet, CH_LASER_B, OnOffValue(blue.Value, onValue));

			if (changed) artnet.Submit();
		}

		// -----------------------
		// Checkered lights (RGBW)
		// -----------------------
		public static void SetCheckered(ArtnetInterface artnet, int? r = null, int? g = null, int? b = null, int? w = null)
		{
			bool changed = false;
			if (r is not null) changed |= SetChannelCached(artnet, CH_CHECKER_R, r.Value);
			if (g is not null) changed |= SetChannelCached(artnet, CH_CHECKER_G, g.Value);
			if (b is not null) changed |= SetChannelCached(artnet, CH_CHECKER_B, b.Value);
			if (w is not null) changed |= SetChannelCached(artnet, CH_CHECKER_W, w.Value);

			if (changed) artnet.Submit();
		}

		// -----------------------
		// Soft lights (RGB)
		// -----------------------
		public static void SetSoft(ArtnetInterface artnet, int? r = null, int? g = null, int? b = null)
		{
			bool changed = false;
			if (r is not null) changed |= SetChannelCached(artnet, CH_SOFT_R, r.Value);
			if (g is not null) changed |= SetChannelCached(artnet, CH_SOFT_G, g.Value);
			if (b is not null) changed |= SetChannelCached(artnet, CH_SOFT_B, b.Value);

			if (changed) artnet.Submit();
		}

		// -----------------------
		// Strobes
		// -----------------------
		public static void SetStrobeControl(ArtnetInterface artnet, int value)
		{
			SetAndSubmit(artnet, CH_STROBE_CTRL, value);
		}

		public static void StrobeOff(ArtnetInterface artnet) => SetStrobeControl(artnet, STROBE_OFF);
		public static void StrobeOn(ArtnetInterface artnet) => SetStrobeControl(artnet, STROBE_ON);
		public static void StrobeRandom(ArtnetInterface artnet) => SetStrobeControl(artnet, STROBE_RANDOM);

		public static void StrobeRandomSpeed(ArtnetInterface artnet, int speed)
		{
			int v = Clamp(speed, STROBE_RANDOM_MIN, STROBE_RANDOM_MAX); // :contentReference[oaicite:15]{index=15}
			SetStrobeControl(artnet, v);
		}

		public static void StrobeSteadySpeed(ArtnetInterface artnet, int value)
		{
			int v = Clamp(value, STROBE_STEADY_MIN, 255); // :contentReference[oaicite:16]{index=16}
			SetStrobeControl(artnet, v);
		}

		public static void SetStrobeColors(ArtnetInterface artnet, int? r = null, int? g = null, int? b = null, int? u = null)
		{
			bool changed = false;
			if (r is not null) changed |= SetChannelCached(artnet, CH_STROBE_R, r.Value);
			if (g is not null) changed |= SetChannelCached(artnet, CH_STROBE_G, g.Value);
			if (b is not null) changed |= SetChannelCached(artnet, CH_STROBE_B, b.Value);
			if (u is not null) changed |= SetChannelCached(artnet, CH_STROBE_U, u.Value);

			if (changed) artnet.Submit();
		}

		// Your existing "quantized" strobe mapping
		public static void HandleStrobe(ArtnetInterface artnet, int value)
		{
			Console.WriteLine($"Strobe({value})");

			switch (value)
			{
				case 0:
					SetMany(artnet,
						strobe_control: STROBE_OFF,
						strobe_r: 0, strobe_g: 0, strobe_b: 0, strobe_u: 0);
					
					break;

				case 16:
					SetMany(artnet,
						strobe_r: 0, strobe_g: 0, strobe_b: 0, strobe_u: 255,
						strobe_control: 255); // steady speed clamped to >= 30 :contentReference[oaicite:17]{index=17}
					break;

				case 32:
					SetMany(artnet,
						strobe_r: 255, strobe_g: 0, strobe_b: 0, strobe_u: 0,
						strobe_control: 160);
					break;

				case 48:
					SetMany(artnet,
						strobe_r: 255, strobe_g: 255, strobe_b: 255, strobe_u: 255,
						strobe_control: 255);
					break;

				case 64:
					SetMany(artnet,
						strobe_r: 0, strobe_g: 255, strobe_b: 255, strobe_u: 0,
						strobe_control: 220);
					break;

				default:
					break;
			}
		}

		// -----------------------
		// SetMany: set subset, submit once
		// Mirrors python set_many :contentReference[oaicite:18]{index=18}
		// -----------------------
		public static void SetMany(
			ArtnetInterface artnet,
			int? master = null,
			int? motor_speed = null,
			bool? laser_green = null,
			bool? laser_red1 = null,
			bool? laser_red2 = null,
			bool? laser_blue = null,
			int laser_on_value = 255,
			int? checkered_r = null,
			int? checkered_g = null,
			int? checkered_b = null,
			int? checkered_w = null,
			int? soft_r = null,
			int? soft_g = null,
			int? soft_b = null,
			int? strobe_control = null,
			int? strobe_r = null,
			int? strobe_g = null,
			int? strobe_b = null,
			int? strobe_u = null)
		{
			bool changed = false;

			if (master is not null) changed |= SetChannelCached(artnet, CH_MASTER, master.Value);
			if (motor_speed is not null) changed |= SetChannelCached(artnet, CH_MOTOR_SPEED, motor_speed.Value);

			if (laser_green is not null) changed |= SetChannelCached(artnet, CH_LASER_G, OnOffValue(laser_green.Value, laser_on_value));
			if (laser_red1 is not null) changed |= SetChannelCached(artnet, CH_LASER_R1, OnOffValue(laser_red1.Value, laser_on_value));
			if (laser_red2 is not null) changed |= SetChannelCached(artnet, CH_LASER_R2, OnOffValue(laser_red2.Value, laser_on_value));
			if (laser_blue is not null) changed |= SetChannelCached(artnet, CH_LASER_B, OnOffValue(laser_blue.Value, laser_on_value));

			if (checkered_r is not null) changed |= SetChannelCached(artnet, CH_CHECKER_R, checkered_r.Value);
			if (checkered_g is not null) changed |= SetChannelCached(artnet, CH_CHECKER_G, checkered_g.Value);
			if (checkered_b is not null) changed |= SetChannelCached(artnet, CH_CHECKER_B, checkered_b.Value);
			if (checkered_w is not null) changed |= SetChannelCached(artnet, CH_CHECKER_W, checkered_w.Value);

			if (soft_r is not null) changed |= SetChannelCached(artnet, CH_SOFT_R, soft_r.Value);
			if (soft_g is not null) changed |= SetChannelCached(artnet, CH_SOFT_G, soft_g.Value);
			if (soft_b is not null) changed |= SetChannelCached(artnet, CH_SOFT_B, soft_b.Value);

			if (strobe_control is not null)
			{
				// match python: steady speed clamped to >= 30 only when you call that helper,
				// but raw set_many allows any 0..255. We'll keep raw semantics. :contentReference[oaicite:19]{index=19}
				changed |= SetChannelCached(artnet, CH_STROBE_CTRL, strobe_control.Value);
			}
			if (strobe_r is not null) changed |= SetChannelCached(artnet, CH_STROBE_R, strobe_r.Value);
			if (strobe_g is not null) changed |= SetChannelCached(artnet, CH_STROBE_G, strobe_g.Value);
			if (strobe_b is not null) changed |= SetChannelCached(artnet, CH_STROBE_B, strobe_b.Value);
			if (strobe_u is not null) changed |= SetChannelCached(artnet, CH_STROBE_U, strobe_u.Value);

			if (changed) artnet.Submit();
		}

		// -----------------------
		// Internals
		// -----------------------

		private static void SetAndSubmit(ArtnetInterface artnet, int channel, int value)
		{
			if (SetChannelCached(artnet, channel, value))
				artnet.Submit();
		}

		private static void SetOnOffChannelAndSubmit(ArtnetInterface artnet, int channel, bool on, int onValue)
		{
			int v = OnOffValue(on, onValue); // :contentReference[oaicite:20]{index=20}
			if (SetChannelCached(artnet, channel, v))
				artnet.Submit();
		}

		private static int OnOffValue(bool on, int onValue)
		{
			// Python: off => 0; on => clamp, but ensure nonzero means on :contentReference[oaicite:21]{index=21}
			if (!on) return 0;

			int v = onValue;
			if (v <= 0) return 1;
			if (v > 255) return 255;
			return v;
		}

		private static bool SetChannelCached(ArtnetInterface artnet, int channel, int value)
		{
			int v = Clamp(value, 0, 255);

			// Only cache channels 1..18 (like the python cache initialization)
			// but allow arbitrary channel set without caching.
			if (channel >= 1 && channel <= 18)
			{
				if (_cache[channel] == v) return false;
				_cache[channel] = v;
			}

			artnet.SetChannel(channel, v);
			return true;
		}

		private static int Clamp(int v, int lo, int hi)
		{
			if (v < lo) return lo;
			if (v > hi) return hi;
			return v;
		}
	}
}
