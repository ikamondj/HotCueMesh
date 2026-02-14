using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StreamDeckCS2DMX
{
	internal interface IPreset
	{
		void Process(PresetInput inp);
	}

	internal class Preset : IPreset
	{
		Action<PresetInput> processFunc;
		public Preset(Action<PresetInput> processFunc)
		{
			this.processFunc = processFunc;
		}

		public void Process(PresetInput inp)
		{
			processFunc(inp);
		}
		static int ValToDmxSlider(float val)
		{
			return (int)Math.Clamp(val * 255.0, 0, 255);
		}
		public static IEnumerable<Preset> GetPresets(ArtnetInterface artnetInterface)
		{
			yield return new Preset((p) => { }); // 0
			yield return new Preset((p) => // 1
			{
				DmxMappings.SetLaserRed1(artnetInterface, p.bass > 0.75);
				DmxMappings.SetLaserRed2(artnetInterface, p.bass > 0.88);
				DmxMappings.SetMotorSpeed(artnetInterface, ValToDmxSlider(p.treb * p.treb));
				DmxMappings.SetLaserBlue(artnetInterface, p.mids > 0.85);
				if (!p.strobeActive)
				{
					DmxMappings.StrobeOn(artnetInterface);
					DmxMappings.SetStrobeColors(artnetInterface, p.mids > 0.75 ? 255 : 0, 0, p.treb > 0.77 ? 255 : 0, p.lows > 0.8 ? 255 : 0);
				}
			}
			);
			yield return new Preset((p) => // 2
			{
				// Lasers: blue only, with occasional green sparkle
				bool greenSpark = p.lows > 0.92f && p.mids > 0.70f;

				DmxMappings.SetLasers(
					artnetInterface,
					green: greenSpark,
					red1: false,
					red2: false,
					blue: true,
					onValue: 255
				);

				// Motor: slow, driven by bass (more bass = slightly faster, still slow)
				// Keep it in a "slow range" rather than full-speed.
				float motor = 0.10f + 0.40f * p.bass; // 0.10..0.50
				DmxMappings.SetMotorSpeed(artnetInterface, ValToDmxSlider(motor));

				// Primary look: blue + white wash
				// Use treble to add sparkle to white, mids to drive blue intensity.
				float blue = Math.Clamp((p.mids * p.mids - 0.15f) * 1.3f, 0f, 1f);
				float white = Math.Clamp((p.treb * p.treb - 0.25f) * 1.5f, 0f, 1f);

				DmxMappings.SetCheckered(
					artnetInterface,
					0,
					0,
					ValToDmxSlider(blue),
					ValToDmxSlider(white)
				);

				// Optional: keep soft lights subtle blue fill
				float softBlue = Math.Clamp((p.lows * p.lows - 0.30f) * 1.2f, 0f, 1f);
				DmxMappings.SetSoft(artnetInterface, 0, 0, ValToDmxSlider(softBlue));

				// Strobe: blue/white/UV "to the beat" when strobe isn't manually active.
				// We don't have a true beat detector, so use a thresholded bass trigger.
				if (!p.strobeActive)
				{
					bool hit = p.bass > 0.82f;

					if (hit)
					{
						// Steady strobe speed (not random), speed follows bass a bit.
						// Must be >= 30 for steady mode.
						int speed = 30 + (int)(p.bass * 200f); // 30..230
						DmxMappings.StrobeSteadySpeed(artnetInterface, speed);

						// Blue + White + UV flash
						int sB = p.treb > 0.60f ? 255 : 180;
						int sU = p.bass > 0.90f ? 255 : 140;

						DmxMappings.SetStrobeColors(artnetInterface, r: 0, g: 0, b: sB, u: sU);
						// If your strobe fixture uses RGB+UV only (no dedicated white), you can emulate white with RGB.
						// Your mapping is RGB+UV, so "white" here is effectively (R,G,B)=255. We'll bias toward blue:
						// If you want actual white flashes, switch to (r:255,g:255,b:255,u:...)
						// Example alternative:
						// DmxMappings.SetStrobeColors(artnetInterface, r: 255, g: 255, b: 255, u: sU);
					}
					else
					{
						// Keep strobes off when no hit
						DmxMappings.StrobeOff(artnetInterface);
						DmxMappings.SetStrobeColors(artnetInterface, 0, 0, 0, 0);
					}
				}
			});
			yield return new Preset((p) => // 3
			{
				// Lasers: blue always on, no red lasers. Keep green off (dim vibe).
				DmxMappings.SetLasers(
					artnetInterface,
					green: false,
					red1: false,
					red2: false,
					blue: true,
					onValue: 255
				);

				// Motor: very slow, slight movement from bass but stays slow.
				float motor = 0.05f + 0.15f * p.bass; // 0.05..0.20
				DmxMappings.SetMotorSpeed(artnetInterface, ValToDmxSlider(motor));

				// Soft lights: dim red + dim blue only
				// Keep overall low intensity. Use mids for blue and lows for red subtly.
				float softR = Math.Clamp((p.lows * p.lows - 0.35f) * 0.8f, 0f, 0.35f);
				float softB = Math.Clamp((p.mids * p.mids - 0.35f) * 0.8f, 0f, 0.35f);

				DmxMappings.SetSoft(
					artnetInterface,
					ValToDmxSlider(softR),
					0,
					ValToDmxSlider(softB)
				);

				// Turn off checkered wash to keep it dim (or set it very low if you want texture)
				DmxMappings.SetCheckered(artnetInterface, 0, 0, 0, 0);

				// UV strobe: only when treble is high, and speed scales with treble.
				if (!p.strobeActive)
				{
					bool sparkle = p.treb > 0.72f;

					if (sparkle)
					{
						int speed = 30 + (int)(p.treb * 200f); // 30..230
						DmxMappings.StrobeSteadySpeed(artnetInterface, speed);

						// UV only, keep RGB off to stay moody
						int u = p.treb > 0.85f ? 255 : 160;
						DmxMappings.SetStrobeColors(artnetInterface, r: 0, g: 0, b: 0, u: u);
					}
					else
					{
						DmxMappings.StrobeOff(artnetInterface);
						DmxMappings.SetStrobeColors(artnetInterface, 0, 0, 0, 0);
					}
				}
			});
			yield return new Preset((p) => // 4
			{
				// Rotation: very slow baseline, instant jump to fast if treble is hot.
				DmxMappings.SetMotorSpeed(artnetInterface, p.treb > 0.70f ? 255 : 22);

				// Harsh logic helpers
				bool hiTreb = p.treb > 0.70f;
				bool popTreb = p.treb > 0.82f;     // pop hits
				bool popBass = p.bass > 0.88f;     // occasional punch
				bool greenGate = p.lows > 0.78f && p.mids > 0.55f;

				// "Always on, but can flip off" using a hard gate.
				// (No smoothness: fully on or fully off only.)
				bool redBaseOn = !(p.bass < 0.18f && p.mids < 0.25f); // hard dropout when energy is low
				bool greenBaseOn = greenGate && !(p.treb < 0.20f);  // hard dropout when treble is dead

				// Lasers: only red + green. No blue lasers.
				// Red lasers are usually on, green laser comes and goes.
				DmxMappings.SetLasers(
					artnetInterface,
					green: greenBaseOn || popTreb,   // green pops with treble too
					red1: redBaseOn,
					red2: redBaseOn && (p.bass > 0.55f), // red2 is "harder" requirement
					blue: false,
					onValue: 255
				);

				// Soft lights: only red/green, harsh states.
				// Red is the foundation; green flashes on pop moments.
				int softR = redBaseOn ? 255 : 0;
				int softG = (greenBaseOn && (p.treb > 0.35f)) ? 255 : 0;

				// Extra "poppy" behavior: treble pop forces green hard-on, otherwise it can be hard-off.
				if (popTreb) softG = 255;
				if (hiTreb && p.bass < 0.40f) softR = 0; // harsh drop of red on high treble + low bass

				DmxMappings.SetSoft(artnetInterface, softR, softG, 0);

				// Checkered RGBW: still only reds+greens (B=0, W=0), harsh patterns.
				// - On treble pops, slam one color fully on.
				// - Otherwise, alternate hard states based on simple gates.
				int checkR = 0, checkG = 0, checkB = 0, checkW = 0;

				if (popTreb)
				{
					// Treble pop: green slam unless bass is also big, then red slam.
					if (popBass) { checkR = 255; checkG = 0; }
					else { checkR = 0; checkG = 255; }
				}
				else
				{
					// Base: harsh on/off combos
					// If lows strong, prefer green; if mids strong, prefer red; else off.
					if (p.lows > 0.65f) { checkR = 0; checkG = 255; }
					else if (p.mids > 0.60f) { checkR = 255; checkG = 0; }
					else { checkR = 0; checkG = 0; }

					// Occasional bass punch forces both on (hard flash)
					if (popBass) { checkR = 255; checkG = 255; }
				}

				DmxMappings.SetCheckered(artnetInterface, checkR, checkG, checkB, checkW);

				// Strobes: leave them alone if user is controlling strobes.
				// If you want a harsh red/green strobe pop tied to treble *only when strobe isn't active*:
				// if (!strobeActive && popTreb) { ... }
			});
			yield return new Preset((p) => // 5
			{
				// "Negative space" preset: mostly darkness, with rare, deliberate accents.
				// Idea: keep wash very low, then do crisp “signature” hits on specific conditions.

				// Lasers: normally off. Allow a short "spark" window when bass+mids align.
				bool laserSpark = p.bass > 0.90f && p.mids > 0.75f;
				DmxMappings.SetLasers(
					artnetInterface,
					green: laserSpark && p.treb > 0.55f,
					red1: laserSpark,
					red2: laserSpark && p.bass > 0.95f,
					blue: laserSpark && p.treb > 0.70f,
					onValue: 255
				);

				// Motor: almost frozen. Kick briefly faster only on treble spikes.
				bool trebleSpike = p.treb > 0.86f;
				float motor = trebleSpike ? 0.80f : 0.06f; // harsh jump
				DmxMappings.SetMotorSpeed(artnetInterface, ValToDmxSlider(motor));

				// Soft lights: deep low-intensity “breathing” purple-ish feel (R+B), but capped.
				float energy = Math.Clamp(0.65f * p.lows + 0.35f * p.mids, 0f, 1f);
				int softR = ValToDmxSlider(Math.Clamp((energy * energy - 0.25f) * 0.35f, 0f, 0.22f));
				int softB = ValToDmxSlider(Math.Clamp((energy * energy - 0.25f) * 0.35f, 0f, 0.22f));
				DmxMappings.SetSoft(artnetInterface, softR, 0, softB);

				// Checkered: usually off; “white cut” flashes on bass hits, but still not full blast.
				bool cut = p.bass > 0.84f && p.treb > 0.55f;
				int w = cut ? (p.bass > 0.92f ? 220 : 140) : 0;
				DmxMappings.SetCheckered(artnetInterface, 0, 0, 0, w);

				// Strobe: single-purpose UV puncture on extreme treble only, otherwise hard off.
				if (!p.strobeActive)
				{
					if (p.treb > 0.92f)
					{
						int speed = 30 + (int)(p.treb * 200f); // 30..230
						DmxMappings.StrobeSteadySpeed(artnetInterface, speed);
						DmxMappings.SetStrobeColors(artnetInterface, r: 0, g: 0, b: 0, u: 255);
					}
					else
					{
						DmxMappings.StrobeOff(artnetInterface);
						DmxMappings.SetStrobeColors(artnetInterface, 0, 0, 0, 0);
					}
				}
			});
			yield return new Preset((p) => // 6
			{
				DmxMappings.SetLaserRed1(artnetInterface, p.bass > 0.75);
				DmxMappings.SetLaserRed2(artnetInterface, p.bass > 0.88);
				DmxMappings.SetMotorSpeed(artnetInterface, ValToDmxSlider(p.treb * p.treb));
				DmxMappings.SetSoft(artnetInterface, ValToDmxSlider((p.mids * p.mids - 0.2f) * 1.2f), 0, 0);
				DmxMappings.SetCheckered(artnetInterface, ValToDmxSlider((p.lows * p.lows - 0.2f) * 1.2f), 0, 0, ValToDmxSlider((p.treb * p.treb - 0.5f) * 2f));
			});
			yield return new Preset((p) => // 7
			{
				// 10-second phase rotation
				long tMs = Environment.TickCount64;
				int phase = (int)((tMs / 10_000) % 4);

				float r, g, b, w;

				switch (phase)
				{
					case 0:
						r = p.bass; g = p.lows; b = p.mids; w = p.treb;
						break;
					case 1:
						r = p.lows; g = p.mids; b = p.treb; w = p.bass;
						break;
					case 2:
						r = p.mids; g = p.treb; b = p.bass; w = p.lows;
						break;
					default: // phase 3
						r = p.treb; g = p.bass; b = p.lows; w = p.mids;
						break;
				}

				DmxMappings.SetCheckered(
					artnetInterface,
					ValToDmxSlider((r * r - 0.2f) * 1.2f),
					ValToDmxSlider((g * g - 0.2f) * 1.2f),
					ValToDmxSlider((b * b - 0.2f) * 1.2f),
					ValToDmxSlider((w * w - 0.2f) * 1.2f)
				);
			});
			yield return new Preset((p) => // 8
			{
				// Time-cycled "rainbow" pattern, quantized to 130 BPM beats.
				// Every beat changes the color mode:
				// 0: G, 1: R, 2: B, 3: R+G, 4: White (RGB), 5: B+G, 6: R+B (+UV)

				const float bpm = 130f;
				const float beatMsF = 60000f / bpm;           // ~461.538 ms
				long tMs = Environment.TickCount64;
				int step = (int)((tMs / (long)beatMsF) % 7);

				// Intensity: keep it musical; use energy to scale, but never go totally dark unless very low.
				float energy = Math.Clamp(0.45f * p.bass + 0.30f * p.lows + 0.25f * p.treb, 0f, 1f);
				int v = ValToDmxSlider(Math.Clamp((energy * energy - 0.10f) * 1.15f, 0.10f, 1.0f));

				int r = 0, g = 0, b = 0, w = 0;

				switch (step)
				{
					case 0: // green only
						g = v;
						break;
					case 1: // red only
						r = v;
						break;
					case 2: // blue only
						b = v;
						break;
					case 3: // red + green
						r = v; g = v;
						break;
					case 4: // white (RGB)
						r = v; g = v; b = v;
						w = v; // you have a W channel too; let it join the party
						break;
					case 5: // blue + green
						b = v; g = v;
						break;
					default: // 6: red + blue (+ UV strobe color when not manually active)
						r = v; b = v;
						break;
				}

				DmxMappings.SetCheckered(artnetInterface, r, g, b, w);

				// Keep soft lights in sync but slightly dimmer (so checkered is the "lead").
				int sv = ValToDmxSlider(Math.Clamp((energy * energy - 0.15f) * 0.75f, 0.05f, 0.65f));
				int sr = 0, sg = 0, sb = 0;

				// Mirror the step but no white channel available on soft.
				switch (step)
				{
					case 0: sg = sv; break;
					case 1: sr = sv; break;
					case 2: sb = sv; break;
					case 3: sr = sv; sg = sv; break;
					case 4: sr = sv; sg = sv; sb = sv; break;
					case 5: sg = sv; sb = sv; break;
					default: sr = sv; sb = sv; break;
				}

				DmxMappings.SetSoft(artnetInterface, sr, sg, sb);

				// Lasers: subtle accent tied to mode (optional but keeps the scene alive).
				// Keep them off when energy is low.
				bool laserOn = energy > 0.35f;
				DmxMappings.SetLasers(
					artnetInterface,
					green: laserOn && (step == 0 || step == 3 || step == 5),
					red1: laserOn && (step == 1 || step == 3 || step == 6 || step == 4),
					red2: laserOn && (step == 3 || step == 6) && p.bass > 0.70f,
					blue: laserOn && (step == 2 || step == 5 || step == 6 || step == 4),
					onValue: 255
				);

				// Motor: slow cruise, bumps a touch on bass.
				float motor = 0.12f + 0.30f * p.bass; // 0.12..0.42
				DmxMappings.SetMotorSpeed(artnetInterface, ValToDmxSlider(motor));

				// UV behavior: only when step is R+B (your request), and only if user isn't manually strobing.
				if (!p.strobeActive)
				{
					if (step == 6 && energy > 0.40f)
					{
						int speed = 30 + (int)(Math.Clamp(p.bass, 0f, 1f) * 200f);
						DmxMappings.StrobeSteadySpeed(artnetInterface, speed);

						// When red+blue is on, include UV; keep it a little gated so it doesn’t dominate.
						int u = (p.treb > 0.60f || p.bass > 0.75f) ? 220 : 140;
						DmxMappings.SetStrobeColors(artnetInterface, r: 0, g: 0, b: 0, u: u);
					}
					else
					{
						DmxMappings.StrobeOff(artnetInterface);
						DmxMappings.SetStrobeColors(artnetInterface, 0, 0, 0, 0);
					}
				}
			});
			yield return new Preset((p) => // 9 - Harsh neon vortex
			{
				// Donut swirl: vivid RGB ring feel + sharp edges.
				// Use motor for slow swirl; use checkered for the "donut", soft as glow fill.

				float swirl = 0.10f + 0.25f * p.mids; // slow
				DmxMappings.SetMotorSpeed(artnetInterface, ValToDmxSlider(swirl));

				// Hard-edged color: quantize to 3 phases to feel "harsh".
				long tMs = Environment.TickCount64;
				int phase = (int)((tMs / 650) % 3);

				// Brightness keyed to energy; keep it punchy.
				float energy = Math.Clamp(0.45f * p.bass + 0.30f * p.mids + 0.25f * p.treb, 0f, 1f);
				int v = ValToDmxSlider(Math.Clamp((energy * energy - 0.05f) * 1.25f, 0.15f, 1.0f));

				int r = 0, g = 0, b = 0, w = 0;

				// Neon triad rotation; white edge comes in on treble.
				switch (phase)
				{
					case 0: r = v; g = (int)(v * 0.25f); b = v; break;        // magenta punch
					case 1: r = (int)(v * 0.20f); g = v; b = v; break;        // cyan punch
					default: r = v; g = v; b = (int)(v * 0.15f); break;       // yellow-ish punch
				}

				w = p.treb > 0.70f ? (int)(v * 0.80f) : (p.treb > 0.55f ? (int)(v * 0.35f) : 0);

				DmxMappings.SetCheckered(artnetInterface, r, g, b, w);

				// Soft glow: subdued complement.
				int s = ValToDmxSlider(Math.Clamp((energy - 0.15f) * 0.35f, 0f, 0.22f));
				DmxMappings.SetSoft(artnetInterface, s, 0, s);

				// Lasers: brief harsh "edges" on bass pops.
				bool pop = p.bass > 0.88f;
				DmxMappings.SetLasers(
					artnetInterface,
					green: pop && p.mids > 0.55f,
					red1: pop,
					red2: pop && p.bass > 0.93f,
					blue: pop && p.treb > 0.55f,
					onValue: 255
				);

				// No strobe here; harshness comes from quantization.
			});

			yield return new Preset((p) => // 10 - Playful pastel bounce
			{
				// Unicorn frolic: pastel RGB, bouncy brightness, gentle motion.

				// Motor: medium-slow, “prancing” via mids.
				float motor = 0.12f + 0.35f * p.mids;
				DmxMappings.SetMotorSpeed(artnetInterface, ValToDmxSlider(motor));

				// Pastel palette: clamp intensity so it never gets harsh.
				float joy = Math.Clamp(0.40f * p.lows + 0.35f * p.mids + 0.25f * p.treb, 0f, 1f);
				int v = ValToDmxSlider(Math.Clamp((joy * joy - 0.10f) * 0.85f, 0.06f, 0.55f));

				// Bounce: brighten on bass hits, but still pastel.
				bool bounce = p.bass > 0.78f;
				int bump = bounce ? (int)(v * 1.40f) : v;
				if (bump > 255) bump = 255;

				// Pastel rainbow-ish mix (mint/pink/lavender)
				int r = (int)(bump * 0.95f);
				int g = (int)(bump * 0.85f);
				int b = (int)(bump * 1.00f);
				int w = (int)(bump * 0.35f);

				DmxMappings.SetCheckered(artnetInterface, r, g, b, w);

				// Soft lights: meadow fill (gentle green/blue).
				int sg = (int)(v * 0.55f);
				int sb = (int)(v * 0.70f);
				DmxMappings.SetSoft(artnetInterface, 0, sg, sb);

				// Lasers: keep off (cartoon vibe).
				DmxMappings.SetLasers(artnetInterface, green: false, red1: false, red2: false, blue: false);
			});

			yield return new Preset((p) => // 11 - Dusty neon marquee
			{
				// Wild west sign: warm amber-ish with bright “bulb pops”.
				// You don't have amber, so fake it with red+green + a little white.

				// Motor: slow “swinging sign”.
				float motor = 0.08f + 0.18f * p.lows;
				DmxMappings.SetMotorSpeed(artnetInterface, ValToDmxSlider(motor));

				// Base dusty warmth.
				float grit = Math.Clamp(0.55f * p.lows + 0.30f * p.mids + 0.15f * p.treb, 0f, 1f);
				int baseV = ValToDmxSlider(Math.Clamp((grit * grit - 0.12f) * 1.0f, 0.10f, 0.75f));

				int r = baseV;
				int g = (int)(baseV * 0.60f);
				int b = (int)(baseV * 0.10f);
				int w = (int)(baseV * 0.35f);

				// “Bulb pop” on treble spikes: brighter white edge + extra red.
				bool bulb = p.treb > 0.80f;
				if (bulb)
				{
					w = Math.Clamp(w + 120, 0, 255);
					r = Math.Clamp(r + 80, 0, 255);
				}

				DmxMappings.SetCheckered(artnetInterface, r, g, b, w);

				// Soft: dusty backlight (warm).
				int sr = (int)(baseV * 0.55f);
				int sg = (int)(baseV * 0.30f);
				DmxMappings.SetSoft(artnetInterface, sr, sg, 0);

				// Lasers: subtle red “sign outline” when energy is present.
				bool on = grit > 0.35f;
				DmxMappings.SetLasers(artnetInterface, green: false, red1: on, red2: on && p.bass > 0.70f, blue: false, onValue: 255);

				// Strobe: tiny “marquee glitter” only if user isn't strobing.
				if (!p.strobeActive)
				{
					if (bulb && p.bass > 0.60f)
					{
						DmxMappings.StrobeSteadySpeed(artnetInterface, 70 + (int)(p.treb * 140f)); // 70..~196
						DmxMappings.SetStrobeColors(artnetInterface, r: 255, g: 180, b: 80, u: 0);
					}
					else
					{
						DmxMappings.StrobeOff(artnetInterface);
						DmxMappings.SetStrobeColors(artnetInterface, 0, 0, 0, 0);
					}
				}
			});

			yield return new Preset((p) => // 12 - Green terminal glow
			{
				// Dark screen, big green letters.
				// Keep everything off except green channels; strong on/off gating.

				// Motor: near-still.
				DmxMappings.SetMotorSpeed(artnetInterface, ValToDmxSlider(0.04f));

				// Gate: "GAME OVER" flashes when bass+treble align; otherwise steady dim green.
				bool flash = p.bass > 0.82f && p.treb > 0.62f;

				int gMain = flash ? 255 : ValToDmxSlider(Math.Clamp((p.mids * p.mids - 0.20f) * 0.55f, 0.04f, 0.18f));
				int gSoft = flash ? 220 : ValToDmxSlider(Math.Clamp((p.lows * p.lows - 0.25f) * 0.45f, 0.02f, 0.12f));

				DmxMappings.SetCheckered(artnetInterface, r: 0, g: gMain, b: 0, w: 0);
				DmxMappings.SetSoft(artnetInterface, r: 0, g: gSoft, b: 0);

				// Lasers: green only on flashes, feels like scanline cut.
				DmxMappings.SetLasers(artnetInterface, green: flash, red1: false, red2: false, blue: false, onValue: 255);

				// Strobe: green-only “error flicker” but only when not manually active.
				if (!p.strobeActive)
				{
					if (flash)
					{
						DmxMappings.StrobeSteadySpeed(artnetInterface, 120 + (int)(p.bass * 100f)); // 120..220
						DmxMappings.SetStrobeColors(artnetInterface, r: 0, g: 255, b: 0, u: 0);
					}
					else
					{
						DmxMappings.StrobeOff(artnetInterface);
						DmxMappings.SetStrobeColors(artnetInterface, 0, 0, 0, 0);
					}
				}
			});

			yield return new Preset((p) => // 13 - Cool navigational trace
			{
				// Blue GPS route line: cool blue with a moving white "cursor".
				// Use time to slide cursor; keep it calm and legible.

				float cruise = 0.10f + 0.20f * p.bass;
				DmxMappings.SetMotorSpeed(artnetInterface, ValToDmxSlider(cruise));

				float nav = Math.Clamp(0.55f * p.mids + 0.45f * p.treb, 0f, 1f);
				int blue = ValToDmxSlider(Math.Clamp((nav * nav - 0.10f) * 1.0f, 0.08f, 0.65f));

				// Cursor pulse (white) glides in steps.
				long tMs = Environment.TickCount64;
				float cursor = (float)((tMs % 1600) / 1600.0); // 0..1
				bool pulse = cursor < 0.15f; // brief pulse window

				int w = pulse ? ValToDmxSlider(Math.Clamp(0.55f + 0.35f * p.treb, 0f, 1f)) : 0;

				DmxMappings.SetCheckered(artnetInterface, r: 0, g: 0, b: blue, w: w);

				// Soft: faint blue ambient.
				int softB = ValToDmxSlider(Math.Clamp((nav - 0.10f) * 0.22f, 0f, 0.18f));
				DmxMappings.SetSoft(artnetInterface, r: 0, g: 0, b: softB);

				// Lasers: blue only as a "beacon" on bass pings.
				bool ping = p.bass > 0.80f;
				DmxMappings.SetLasers(artnetInterface, green: false, red1: false, red2: false, blue: ping, onValue: 255);

				// No strobe; this is clean/technical.
			});

			yield return new Preset((p) => // 14 - Stellar core radiance
			{
				// Spinning star: bright core (white) with blue halo, against black.
				// Use motor speed to imply spin; use treble for sparkle.

				float spin = 0.15f + 0.55f * p.treb; // treble = faster spin
				DmxMappings.SetMotorSpeed(artnetInterface, ValToDmxSlider(spin));

				float shine = Math.Clamp(0.40f * p.treb + 0.35f * p.mids + 0.25f * p.bass, 0f, 1f);
				int core = ValToDmxSlider(Math.Clamp((shine * shine - 0.05f) * 1.20f, 0.10f, 1.0f));

				// Halo is blue-ish and slightly lower than core.
				int haloB = (int)(core * 0.75f);
				int haloW = (int)(core * 0.55f);

				// “Twinkle”: occasional white spike on very high treble.
				bool twinkle = p.treb > 0.90f;
				int tw = twinkle ? 255 : haloW;

				DmxMappings.SetCheckered(artnetInterface, r: haloB, g: 0, b: 0, w: tw);

				// Soft: deep space fill (very dim blue).
				int softB = ValToDmxSlider(Math.Clamp((shine - 0.20f) * 0.20f, 0f, 0.12f));
				DmxMappings.SetSoft(artnetInterface, r: softB, g: (int)(softB*.2f), b: 0);

				// Lasers: blue + occasional green sparkle (like solar flare) but restrained.
				bool flare = p.bass > 0.88f && p.mids > 0.65f;
				DmxMappings.SetLasers(
					artnetInterface,
					green: flare && p.treb > 0.55f,
					red1: false,
					red2: false,
					blue: shine > 0.35f,
					onValue: 255
				);

				// Strobe: micro-sparkle UV only at extreme treble, and only if user isn't strobing.
				if (!p.strobeActive)
				{
					if (p.treb > 0.93f)
					{
						int speed = 30 + (int)(p.treb * 200f); // 30..230
						DmxMappings.StrobeSteadySpeed(artnetInterface, speed);
						DmxMappings.SetStrobeColors(artnetInterface, r: 255, g: 255, b: 0, u: 200);
					}
					else
					{
						DmxMappings.StrobeOff(artnetInterface);
						DmxMappings.SetStrobeColors(artnetInterface, 255, 255, 0, 200);
					}
				}
			});

		}
	}
}
