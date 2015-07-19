using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace npcook.Terminal
{
	public class TerminalRun
	{
		public string Text
		{ get; set; }

		public TerminalFont Font
		{ get; set; }

		public TerminalRun()
		{ }

		public TerminalRun(string text, TerminalFont font)
		{
			Text = text;
			Font = font;
		}
	}
}
