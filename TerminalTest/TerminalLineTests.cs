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
		[TestMethod()]
		public void DeleteCharactersTest()
		{
			TerminalLine line = new TerminalLine();
			line.SetCharacters(0, new string('a', 160), new TerminalFont() { Bold = true });
			line.DeleteCharacters(70, 20);

			TerminalRun[] expectedRuns = new[]
			{
				new TerminalRun(new string('a', 70), new TerminalFont() {Bold = true }),
				new TerminalRun(new string('a', 70), new TerminalFont() {Bold = true }),
			};

			Assert.Fail();
		}
	}
}