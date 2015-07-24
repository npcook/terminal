using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace npcook.Terminal
{
	// Thanks to http://invisible-island.net/xterm/ctlseqs/ctlseqs.html for much of this information

	// Contains xterm DEC private mode set values.  Values that end in an underscore are not implemented.
	enum XtermDecMode
	{
		AppCursorKeys = 1,		// Application cursor keys
		USASCII_VT100_ = 2,		// USASCII for character sets G0-G3 & VT100 mode
		Columns132_ = 3,		// 132 column mode
		SmoothScroll_ = 4,		// Smooth/slow scroll mode
		ReverseVideo_ = 5,		// Reverse video
		Origin_ = 6,			// Origin mode
		Wraparound = 7,			// Wraparound mode: should cursor wrap around to the next line when at the margin or overwrite the margin character
		AutoRepeatKeys_ = 8,	// Auto-repeat keys
		SendMouseXYOnPress_ = 9,		// Send mouse XY on button press
		ShowToolbar_ = 10,		// Show toolbar
		BlinkCursor = 12,		// Blink the cursor
		PrintFormFeed_ = 18,
		FullScreenPrint_ = 19,
		ShowCursor = 25,
		ShowScrollbar_ = 30,
		EnableFontShifting_ = 35,
		Tektronix_ = 38,
		Allow80To132_ = 40,
		moreFix_ = 41,
		NationalReplacement_ = 42,
		MarginBell_ = 44,
		ReverseWraparound_ = 45,
		Logging_ = 46,
		UseNormalScreen = 47,
		NumericKeypad_ = 66,
		BackarrowDelete_ = 67,
		LeftRightMargin_ = 69,
		NoScreenClearOnColumns132_ = 95,
		SendMouseXYOnPressRelease_ = 1000,
		HiliteMouseTracking_ = 1001,
		CellMotionMouseTracking_ = 1002,
		AllMotionMouseTracking_ = 1003,
		SendFocusEvents_ = 1004,
		EnableUTF8Mouse_ = 1005,
		EnableSGRMouse_ = 1006,
		EnableAltScroll_ = 1007,
		ScrollOnTTYOutput_ = 1010,
		ScrollOnKeyPress_ = 1011,
		UseAltScreen = 1047,
		SaveCursor = 1048,
		UseAltScreenAndSaveCursor = 1049,	// Switches to alt screen, saves the cursor position, and clears the screen
	}

	public class XtermTerminal : TerminalBase, ITerminalHandler, IDisposable
	{
		public TerminalBase Terminal
		{ get { return this; } }

		Dictionary<XtermDecMode, bool> privateModes = new Dictionary<XtermDecMode, bool>();

		Point savedCursorPos;
		StringBuilder runBuilder = new StringBuilder();
		TerminalFont font;

		Thread processingThread;
		BlockingReader reader = new BlockingReader();

		TerminalFont defaultFont;
		public TerminalFont DefaultFont
		{
			get { return defaultFont; }
			set
			{
				defaultFont = value;
				font = value;
			}
		}

		AutoResetEvent dataProcessed = new AutoResetEvent(false);

		public event EventHandler<TitleChangeEventArgs> TitleChanged;

		public XtermTerminal()
		{
			foreach (var key in Enum.GetValues(typeof(XtermDecMode)).Cast<XtermDecMode>())
				privateModes.Add(key, false);
			privateModes[XtermDecMode.BlinkCursor] = true;
			privateModes[XtermDecMode.ShowCursor] = true;
			privateModes[XtermDecMode.UseNormalScreen] = true;
			privateModes[XtermDecMode.Wraparound] = true;

			processingThread = new Thread(handleInputCore);
			processingThread.IsBackground = true;
			processingThread.Name = "XtermTerminal::handleInputCore";
			processingThread.Start();
		}

		void endRun()
		{
			if (runBuilder.Length > 0)
			{
				//				System.Diagnostics.Debug.WriteLine("Read run: " + runBuilder.ToString());
				string text = runBuilder.ToString();
				SetCharacters(text, font);
				runBuilder.Clear();
			}
		}

		static int getAtOrDefault(int?[] arr, int index, int defaultValue)
		{
			if (index >= arr.Length)
				return defaultValue;
			return arr[index].GetValueOrDefault(defaultValue);
		}

		bool handleCsiSgr(int sgr, int?[] codes)
		{
			if (sgr == 0)
				font = DefaultFont;
			else if (sgr == 1)
				font.Bold = true;
			else if (sgr == 2)
				font.Faint = true;
			else if (sgr == 3)
				font.Italic = true;
			else if (sgr == 4)
				font.Underline = true;
			else if (sgr == 7)
				font.Inverse = true;
			else if (sgr == 8)
				font.Hidden = true;
			else if (sgr == 9)
				font.Strike = true;
			else if (sgr == 22)
				font.Bold = false;
			else if (sgr == 23)
				font.Italic = false;
			else if (sgr == 24)
				font.Underline = false;
			else if (sgr == 27)
				font.Inverse = false;
			else if (sgr == 28)
				font.Hidden = false;
			else if (sgr == 29)
				font.Strike = false;
			else if (sgr >= 30 && sgr <= 37)
				font.Foreground = TerminalColors.GetBasicColor(sgr - 30);
			else if (sgr == 38 && codes[1] == 5)
				font.Foreground = TerminalColors.GetXtermColor(getAtOrDefault(codes, 2, 0));
			else if (sgr == 39)
				font.Foreground = DefaultFont.Foreground;
			else if (sgr >= 40 && sgr <= 47)
				font.Background = TerminalColors.GetBasicColor(sgr - 40);
			else if (sgr == 48 && codes[1] == 5)
				font.Background = TerminalColors.GetXtermColor(getAtOrDefault(codes, 2, 0));
			else if (sgr == 49)
				font.Background = DefaultFont.Background;
			else if (sgr >= 90 && sgr <= 97)
				font.Foreground = TerminalColors.GetBasicColor(sgr - 90);
			else if (sgr >= 100 && sgr <= 107)
				font.Background = TerminalColors.GetBasicColor(sgr - 100);
			else
				return false;
			return true;
		}

		bool handleDecSet(int kind)
		{
			bool handled = true;
			switch ((XtermDecMode) kind)
			{
				case XtermDecMode.Wraparound:
					AutoWrapMode = true;
					break;

				case XtermDecMode.UseAltScreen:
					ChangeToScreen(true);
					break;

				case XtermDecMode.SaveCursor:
					savedCursorPos = CursorPos;
					break;

				case XtermDecMode.UseAltScreenAndSaveCursor:
					savedCursorPos = CursorPos;
					ChangeToScreen(true);
					break;

				default:
					handled = false;
					break;
			}
			return handled;
		}

		bool handleDecReset(int kind)
		{
			bool handled = true;
			switch ((XtermDecMode) kind)
			{
				case XtermDecMode.Wraparound:
					AutoWrapMode = false;
					break;

				case XtermDecMode.UseAltScreen:
					ChangeToScreen(false);
					break;

				case XtermDecMode.SaveCursor:
					CursorPos = savedCursorPos;
					break;

				case XtermDecMode.UseAltScreenAndSaveCursor:
					CursorPos = savedCursorPos;
					ChangeToScreen(false);
					break;

				default:
					handled = false;
					break;
			}
			return handled;
		}

		bool handleCsi()
		{
			bool handled = true;

			endRun();

			string sequence = reader.ReadUntil(ch => ch >= 64 && ch <= 126);
			bool isPrivate = sequence[0] == '?';
			char kind = sequence.Last();
			int?[] codes = null;
			try
			{
				string realSequence;
				if (isPrivate)
					realSequence = sequence.Substring(1, sequence.Length - 2);
				else
					realSequence = sequence.Substring(0, sequence.Length - 1);
				codes = (from str in realSequence.Split(';') select str.Length > 0 ? (int?) int.Parse(str) : null).ToArray();
			}
			catch (FormatException ex)
			{
				var a = ex.StackTrace;
			}

			Point oldCursorPos = CursorPos;
			if (isPrivate)
			{
				switch (kind)
				{
					case 'h':
						handled = handleDecSet(getAtOrDefault(codes, 0, 0));
						break;

					case 'l':
						handled = handleDecReset(getAtOrDefault(codes, 0, 0));
						break;

					default:
						handled = false;
						break;
				}
			}
			else
			{
				switch (kind)
				{
					case 'm':
						handled = handleCsiSgr(getAtOrDefault(codes, 0, 0), codes);
						break;

					case 'A':
						CursorPos = new Point(CursorPos.Col, CursorPos.Row - getAtOrDefault(codes, 0, 1));
						break;

					case 'B':
						CursorPos = new Point(CursorPos.Col, CursorPos.Row + getAtOrDefault(codes, 0, 1));
						break;

					case 'C':
						CursorPos = new Point(CursorPos.Col + getAtOrDefault(codes, 0, 1), CursorPos.Row);
						break;

					case 'D':
						CursorPos = new Point(CursorPos.Col - getAtOrDefault(codes, 0, 1), CursorPos.Row);
						break;

					case 'E':
						CursorPos = new Point(0, CursorPos.Row + 1);
						break;

					case 'F':
						CursorPos = new Point(0, CursorPos.Row - 1);
						break;

					case 'G':
						CursorPos = new Point(getAtOrDefault(codes, 0, 1) - 1, CursorPos.Row);
						break;

					case 'H':
					case 'f':
						{
							int row = getAtOrDefault(codes, 0, 1);
							int col = getAtOrDefault(codes, 1, 1);
							CursorPos = new Point(col - 1, row - 1);
						}
						break;

					case 'd':
						{
							int row = getAtOrDefault(codes, 0, 1);
							CursorPos = new Point(CursorPos.Col, row - 1);
						}
						break;

					case 'e':
						{
							int rows = getAtOrDefault(codes, 0, 1);
							CursorPos = new Point(CursorPos.Col, CursorPos.Row + rows);
						}
						break;

					case 'J':
						switch (getAtOrDefault(codes, 0, 0))
						{
							case 0:
								EraseCharacters(Size.Col - oldCursorPos.Col, false);
								for (int i = oldCursorPos.Row + 1; i < Size.Row; ++i)
								{
									CursorPos = new Point(0, i);
									EraseCharacters(Size.Col, false);
								}
								break;

							case 1:
								for (int i = 0; i < oldCursorPos.Row; ++i)
								{
									CursorPos = new Point(0, i);
									EraseCharacters(Size.Col, false);
								}
								EraseCharacters(oldCursorPos.Col, false);
								break;

							case 2:
								for (int i = 0; i < Size.Row; ++i)
								{
									CursorPos = new Point(0, i);
									EraseCharacters(Size.Col, false);
								}
								break;
						}
						CursorPos = oldCursorPos;
						break;

					case 'K':
						switch (getAtOrDefault(codes, 0, 0))
						{
							case 0:
								SetCharacters(new string(' ', Size.Col - CursorPos.Col), new TerminalFont() { Hidden = true }, false);
								break;

							case 1:
								CursorPos = new Point(0, CursorPos.Row);
								SetCharacters(new string(' ', oldCursorPos.Col + 1), new TerminalFont() { Hidden = true }, false);
								break;

							case 2:
								CursorPos = new Point(0, CursorPos.Row);
								SetCharacters(new string(' ', Size.Col), new TerminalFont { Hidden = true }, false);
								break;
						}
						CursorPos = oldCursorPos;
						break;

					case 'P':
						DeleteCharacters(getAtOrDefault(codes, 0, 1));
						break;

					case 'r':
						break;

					case 's':
						savedCursorPos.Col = CursorPos.Col;
						savedCursorPos.Row = CursorPos.Row;
						break;

					case 'u':
						CursorPos = new Point(savedCursorPos.Col, savedCursorPos.Row);
						break;

					default:
						handled = false;
						break;
				}
			}

			System.Diagnostics.Debug.WriteLine(string.Format("{0} ^[ [ {1}", handled ? "X" : " ", sequence));
			return handled;
		}

		bool handleOsc()
		{
			bool handled = true;
			string sequence = reader.ReadUntil(ch => ch == 7);
			sequence = sequence.Substring(0, sequence.Length - 1);
			int kind = int.Parse(sequence.Substring(0, sequence.IndexOf(';')));
			switch (kind)
			{
				case 0:
					if (TitleChanged != null)
						TitleChanged(this, new TitleChangeEventArgs(sequence.Substring(sequence.IndexOf(';') + 1)));
					break;

				default:
					handled = false;
					break;
			}
			System.Diagnostics.Debug.WriteLine(string.Format("{0} ^[ ] {1} [ST]", handled ? "X" : " ", sequence));
			return handled;
		}

		bool applicationKeypad = false;
		bool handleSingleEscape(char kind)
		{
			bool handled = true;
			string sequence = new string(kind, 1);
			if (kind == '=')
			{
				applicationKeypad = true;
			}
			else if (kind == '>')
			{
				applicationKeypad = false;
			}
			else
				handled = false;
			System.Diagnostics.Debug.WriteLine(string.Format("{0} ^[ {1}", handled ? "X" : " ", sequence));

			return handled;
		}

		void handleInputCore()
		{
			while (true)
			{
				if (!reader.DataAvailable)
				{
					endRun();
					dataProcessed.Set();
					reader.Wait(TimeSpan.FromMilliseconds(500));
				}

				char c = reader.ReadOne();
				if (!char.IsControl(c) || c == '\r' || c == '\n')
					runBuilder.Append(c);
				else if (c != '\x1b')
				{
					if (c == '\b')
					{
						endRun();

						if (CursorPos.Col == 0)
							CursorPos = new Point(Size.Col - 1, CursorPos.Row - 1);
						else
							CursorPos = new Point(CursorPos.Col - 1, CursorPos.Row);
					}
				}
				else
				{
					bool handled = true;

					char escapeKind = reader.ReadOne();
					string sequence = "";
					if (escapeKind == '[')
						handled = handleCsi();
					else if (escapeKind == ']')
						handled = handleOsc();
					else if (escapeKind == '(')
					{
						sequence = new string(reader.ReadOne(), 1);

						if (sequence != "B")
							handled = false;
						System.Diagnostics.Debug.WriteLine(string.Format("{0} ^[ ( {1}", handled ? "X" : " ", sequence));
					}
					else
						handled = handleSingleEscape(escapeKind);
				}
			}
		}

		bool busy = false;
		public void HandleInput(StreamReader reader)
		{
			if (busy)
				System.Diagnostics.Debugger.Break();
			busy = true;

			dataProcessed.Reset();
			this.reader.Write(reader);
			dataProcessed.WaitOne(TimeSpan.FromMilliseconds(500));

			busy = false;
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					dataProcessed.Dispose();
					reader.Dispose();
				}

				dataProcessed = null;
				reader = null;

				disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);
		}
		#endregion
	}
}
