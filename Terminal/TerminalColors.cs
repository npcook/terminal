using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace npcook.Terminal
{
	static public class TerminalColors
	{
		static public Color MakeBold(Color color)
		{
			color.R = (byte) Math.Min(color.R + 50, 255);
			color.G = (byte) Math.Min(color.G + 50, 255);
			color.B = (byte) Math.Min(color.B + 50, 255);
			return color;
		}

		static public Color GetBasicColor(int index)
		{
			if (index < 0 || index > 7)
				throw new ArgumentOutOfRangeException("index", index, "index must be 0 <= index <= 7");

			var color = new Color[]
			{
				Color.FromRgb(0, 0, 0),			// Dull Black
				Color.FromRgb(205, 0, 0),       // Dull Red
				Color.FromRgb(0, 205, 0),       // Dull Green
				Color.FromRgb(205, 205, 0),     // Dull Yellow
				Color.FromRgb(0, 0, 238),       // Dull Blue
				Color.FromRgb(205, 0, 205),     // Dull Purple
				Color.FromRgb(0, 205, 205),     // Dull Cyan
				Color.FromRgb(192, 192, 192),   // Dull White
			}[index];
			return color;
		}

		static public Color GetBrightColor(int index)
		{
			if (index < 0 || index > 7)
				throw new ArgumentOutOfRangeException("index", index, "index must be 0 <= index <= 7");

			var color = new Color[]
			{
				Color.FromRgb(127, 127, 127),   // Bright Black
				Color.FromRgb(255, 0, 0),       // Bright Red
				Color.FromRgb(0, 255, 0),       // Bright Green
				Color.FromRgb(255, 255, 0),     // Bright Yellow
				Color.FromRgb(92, 92, 255),     // Bright Blue
				Color.FromRgb(255, 0, 255),     // Bright Purple
				Color.FromRgb(0, 255, 255),     // Bright Cyan
				Color.FromRgb(255, 255, 255)    // Bright White
			}[index];
			return color;
		}

		static public Color GetXtermColor(int index)
		{
			if (index < 0 || index > 255)
				throw new ArgumentOutOfRangeException("index", index, "index must be 0 <= index <= 255");

			Color color;
			if (index < 0x08)
				color = GetBasicColor(index);
			else if (index < 0x10)
				color = GetBasicColor(index - 0x08);
			else if (index < 0xE8)
			{
				int colorBase = index - 0x10;
				int r = Math.Min(((colorBase / 36) % 6 + 1) * (255 / 5), 255);
				int g = Math.Min(((colorBase / 6) % 6 + 1) * (255 / 5), 255);
				int b = Math.Min((colorBase % 6 + 1) * (255 / 5), 255);
				color = Color.FromRgb(r, g, b);
			}
			else
			{
				int intensity = (index - 0xE8) * 10 + 8;
				color = Color.FromRgb(intensity, intensity, intensity);
			}

			return color;
		}
	}
}
