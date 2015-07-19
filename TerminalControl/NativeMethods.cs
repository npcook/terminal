using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace npcook.Terminal.Controls
{
	static class NativeMethods
	{
		[DllImport("user32.dll")]
		public static extern uint GetCaretBlinkTime();
	}
}
