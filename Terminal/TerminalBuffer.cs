using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace npcook.Terminal
{
	class TerminalBuffer
	{
		public int RowCount
		{
			get { return lines.Length; }
			set
			{
				int oldRowCount = RowCount;
				if (value > oldRowCount)
				{
					Array.Resize(ref lines, value);
					for (int i = oldRowCount; i < value; ++i)
						lines[i] = new TerminalLine();
				}
				else if (value < oldRowCount)
				{
					var newLines = new TerminalLine[value];
					Array.Copy(lines, oldRowCount - value, newLines, 0, value);
					lines = newLines;
				}
			}
		}

		TerminalLine[] lines = new TerminalLine[0];
		public TerminalLine[] Lines
		{ get { return lines; } }
	}
}
