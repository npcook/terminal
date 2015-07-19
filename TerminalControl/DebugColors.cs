using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace npcook.Terminal.Controls
{
	using Color = System.Windows.Media.Color;

	static class DebugColors
	{
		static Color[] colors;

		static DebugColors()
		{
			const byte alpha = 255;
			const byte intensity = 128;
			colors = new[]
			{
				Color.FromArgb(alpha, intensity, 0, 0),
				Color.FromArgb(alpha, 0, intensity, 0),
				Color.FromArgb(alpha, 0, 0, intensity),
				Color.FromArgb(alpha, intensity, intensity, 0),
				Color.FromArgb(alpha, intensity, 0, intensity),
				Color.FromArgb(alpha, 0, intensity, intensity),
				Color.FromArgb(alpha, intensity, intensity, intensity),
			};
		}

		public static Color GetColor(int index)
		{
			return colors[index % colors.Length];
		}

		public static SolidColorBrush GetBrush(int index)
		{
			return new SolidColorBrush(GetColor(index));
		}
	}
}
