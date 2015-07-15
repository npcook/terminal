using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Terminal
{
	public enum BasicColors
	{
		Black = 0,
		Red = 1,
		Green = 2,
		Yellow = 3,
		Blue = 4,
		Purple = 5,
		Cyan = 6,
		White = 7
	}

	public struct Color
	{
		public byte R
		{ get; set; }

		public byte G
		{ get; set; }

		public byte B
		{ get; set; }

		public static Color FromRgb(int r, int g, int b)
		{
			if (r < 0 || r > 255)
				throw new ArgumentOutOfRangeException("r", r, "r must be in the range [0, 255]");
			if (r < 0 || r > 255)
				throw new ArgumentOutOfRangeException("g", g, "g must be in the range [0, 255]");
			if (r < 0 || r > 255)
				throw new ArgumentOutOfRangeException("b", b, "b must be in the range [0, 255]");

			Color color = new Color()
			{
				R = (byte) r,
				G = (byte) g,
				B = (byte) b,
			};
			return color;
		}
	}

	public struct TerminalFont
	{
		public Color Foreground;
		public Color Background;
		public bool Bold;
		public bool Underline;
		public bool Hidden;
	}
}
