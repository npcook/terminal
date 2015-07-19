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
					Array.Resize(ref lines, value);
				}
			}
		}

		TerminalLine[] lines = new TerminalLine[0];
		public TerminalLine[] Lines
		{ get { return lines; } }
	}
}
