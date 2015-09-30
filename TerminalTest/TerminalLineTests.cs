using Microsoft.VisualStudio.TestTools.UnitTesting;
using npcook.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace npcook.Terminal.Tests
{
	[TestClass()]
	public class TerminalLineTests
	{
		void AssertAreEqual(TerminalRun run1, TerminalRun run2)
		{
			Assert.AreEqual(run1.Text, run2.Text);
			Assert.AreEqual(run1.Font, run2.Font);
		}

		[TestMethod()]
		public void DeleteCharactersTest()
		{
			TerminalLine line = new TerminalLine();
			line.SetCharacters(0, new string('0', 4), new TerminalFont() { Foreground = Color.FromRgb(0, 0, 0) });
			line.SetCharacters(4, new string('1', 4), new TerminalFont() { Foreground = Color.FromRgb(1, 1, 1) });
			line.SetCharacters(8, new string('2', 4), new TerminalFont() { Foreground = Color.FromRgb(2, 2, 2) });
			line.SetCharacters(12, new string('3', 4), new TerminalFont() { Foreground = Color.FromRgb(3, 3, 3) });
			line.DeleteCharacters(7, 6);

			TerminalRun[] expectedRuns = new[]
			{
				new TerminalRun(new string('0', 4), new TerminalFont() { Foreground = Color.FromRgb(0, 0, 0) }),
				new TerminalRun(new string('1', 3), new TerminalFont() { Foreground = Color.FromRgb(1, 1, 1) }),
				new TerminalRun(new string('3', 3), new TerminalFont() { Foreground = Color.FromRgb(3, 3, 3) }),
			};

			AssertAreEqual(line.Runs[0], expectedRuns[0]);
			AssertAreEqual(line.Runs[1], expectedRuns[1]);
			AssertAreEqual(line.Runs[2], expectedRuns[2]);
		}
	}
}