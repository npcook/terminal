using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace npcook.Terminal
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

	public struct Color : IEquatable<Color>
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

		public override bool Equals(object obj)
		{
			if (obj is Color)
				return this == (Color) obj;
			else
				return false;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public bool Equals(Color other)
		{
			return this == other;
		}

		public static bool operator ==(Color _1, Color _2)
		{
			return
				_1.R == _2.R &&
				_1.G == _2.G &&
				_1.B == _2.B;
		}

		public static bool operator !=(Color _1, Color _2)
		{
			return !(_1 == _2);
		}
	}

	public struct TerminalFont : IEquatable<TerminalFont>
	{
		public Color Foreground;
		public Color Background;
		public bool Bold;
		public bool Faint;
		public bool Underline;
		public bool Italic;
		public bool Strike;
		public bool Hidden;
		public bool Inverse;

		public override bool Equals(object obj)
		{
			if (obj is TerminalFont)
			{
				return this == (TerminalFont) obj;
			}
			else
				return false;
		}

		public override int GetHashCode()
		{
			return base.GetHashCode();
		}

		public bool Equals(TerminalFont other)
		{
			return this == other;
		}

		public static bool operator ==(TerminalFont _1, TerminalFont _2)
		{
			return
				_1.Foreground == _2.Foreground &&
				_1.Background == _2.Background &&
				_1.Bold == _2.Bold &&
				_1.Underline == _2.Underline &&
				_1.Hidden == _2.Hidden && 
				_1.Inverse == _2.Inverse;
		}

		public static bool operator !=(TerminalFont _1, TerminalFont _2)
		{
			return !(_1 == _2);
		}
	}
}
