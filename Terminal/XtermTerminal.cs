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
	public enum XtermDecMode
	{
		None = 0, 
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

	public class PrivateModeChangedEventArgs : EventArgs
	{
		public XtermDecMode Mode
		{ get; }

		public bool Value
		{ get; }

		public PrivateModeChangedEventArgs(XtermDecMode mode, bool value)
		{
			Mode = mode;
			Value = value;
		}
	}

	enum SequenceType
	{
		Text,
		Csi,
		Osc,
		SingleEscape,
		ControlCode,
	}

	class SequenceReceivedEventArgs : EventArgs
	{
		public SequenceType Type
		{ get; }

		public string Sequence
		{ get; }

		public SequenceReceivedEventArgs(SequenceType type, string sequence)
		{
			Type = type;
			Sequence = sequence;
		}
	}

	class XtermStreamParser : IDisposable
	{
		enum State
		{
			Text,
			Escape,
			Csi,
			Osc,
			Escape2,
		}
		
		StreamReader reader;
		StringBuilder partial = new StringBuilder();
		State state = State.Text;

		public event EventHandler<SequenceReceivedEventArgs> SequenceReceived;

		public XtermStreamParser(IStreamNotifier notifier)
		{
			reader = new StreamReader(notifier.Stream, Encoding.UTF8, false, 2048, true);
			notifier.DataAvailable += Notifier_DataAvailable;
		}

		private void Notifier_DataAvailable(object sender, EventArgs e)
		{
			try
			{
				while (!reader.EndOfStream)
					readChar();
				if (state == State.Text)
					endSequence(SequenceType.Text);
			}
			catch (Exception ex)
			{
				if (System.Diagnostics.Debugger.IsAttached) {
					System.Diagnostics.Debugger.Break();
				}
				System.Diagnostics.Debug.WriteLine(ex);
			}
        }

		void endSequence(SequenceType type)
		{
			if (partial.Length > 0)
			{
				if (SequenceReceived != null)
					SequenceReceived(this, new SequenceReceivedEventArgs(type, partial.ToString()));
				partial.Clear();
			}
		}

		void discardSequence()
		{
			System.Diagnostics.Debug.WriteLine("Discarding sequence: " + partial.ToString());
			partial.Clear();
		}

		void readChar()
		{
			char c = (char) reader.Read();
			switch (state)
			{
				case State.Text:
					if (!char.IsControl(c) || c == '\r')
						partial.Append(c);
					else if (c == '\x1b')
					{
						endSequence(SequenceType.Text);
						state = State.Escape;
					}
					else
					{
						endSequence(SequenceType.Text);
						partial.Append(c);
						endSequence(SequenceType.ControlCode);
					}
					break;

				case State.Escape:
					if (c == '[')
						state = State.Csi;
					else if (c == ']')
						state = State.Osc;
					else if (c >= '0')
					{
						partial.Append(c);
						endSequence(SequenceType.SingleEscape);
						state = State.Text;
					}
					else
					{
						partial.Append(c);
						state = State.Escape2;
					}
					break;

				case State.Escape2:
					discardSequence();
					state = State.Text;
					break;

				case State.Csi:
					partial.Append(c);
					if (c >= 64 && c <= 126)
					{
						endSequence(SequenceType.Csi);
						state = State.Text;
					}
                    break;

				case State.Osc:
					if (c == 7)
					{
						endSequence(SequenceType.Osc);
						state = State.Text;
					}
					else
						partial.Append(c);
					break;
			}
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					reader.Dispose();
				}

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

	public class XtermTerminal : TerminalBase, ITerminalHandler
	{
		public TerminalBase Terminal
		{ get { return this; } }

		Dictionary<XtermDecMode, bool> privateModes = new Dictionary<XtermDecMode, bool>();

		Point savedCursorPos;
		Point mainScreenSavedCursorPos;
		TerminalFont font;
		XtermStreamParser parser;

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

		void setPrivateMode(XtermDecMode mode, bool value)
		{
			if (Enum.IsDefined(typeof(XtermDecMode), mode))
			{
				privateModes[mode] = value;

				if (PrivateModeChanged != null)
					PrivateModeChanged(this, new PrivateModeChangedEventArgs(mode, value));
			}
		}

		public event EventHandler<TitleChangeEventArgs> TitleChanged;
		public event EventHandler<PrivateModeChangedEventArgs> PrivateModeChanged;

		public XtermTerminal(IStreamNotifier streamNotifier)
			: base(streamNotifier)
		{
			foreach (var key in Enum.GetValues(typeof(XtermDecMode)).Cast<XtermDecMode>())
				privateModes.Add(key, false);
			privateModes[XtermDecMode.ShowCursor] = true;
			privateModes[XtermDecMode.UseNormalScreen] = true;
			privateModes[XtermDecMode.Wraparound] = true;

			parser = new XtermStreamParser(streamNotifier);
			parser.SequenceReceived += Parser_SequenceReceived;
		}

		private void Parser_SequenceReceived(object sender, SequenceReceivedEventArgs e)
		{
			bool handled = true;
			switch (e.Type)
			{
				case SequenceType.Text:
					Terminal.SetCharacters(e.Sequence, font, true, CurrentScreen == Screen);
					System.Diagnostics.Debug.WriteLine("got: " + e.Sequence);
					break;

				case SequenceType.Csi:
					handled = handleCsi(e.Sequence);
					break;

				case SequenceType.Osc:
					handled = handleOsc(e.Sequence);
					break;

				case SequenceType.SingleEscape:
					handled = handleSingleEscape(e.Sequence[0]);
					break;

				case SequenceType.ControlCode:
					if (e.Sequence[0] == '\n')
					{
						lineFeed(false);
					}
					else if (e.Sequence[0] == '\b')
					{
						if (CursorPos.Col == 0)
							CursorPos = new Point(Size.Col - 1, CursorPos.Row - 1);
						else
							CursorPos = new Point(CursorPos.Col - 1, CursorPos.Row);
					}
					else if (e.Sequence[0] == '\t')
					{
						tab(false);
					}
					else if (e.Sequence[0] == '\a')
					{
						// Disabled because annoyance
						//System.Media.SystemSounds.Beep.Play();
					}
					else
						handled = false;
					break;
			}

/*#if !DEBUG
			if (!handled)
				System.Diagnostics.Debugger.Break();
#else
			if (!handled)
				System.Diagnostics.Debugger.Break();
#endif*/
		}

		static int getAtOrDefault(int?[] arr, int index, int defaultValue)
		{
			if (index >= arr.Length)
				return defaultValue;
			return arr[index].GetValueOrDefault(defaultValue);
		}

		bool handleCsiSgr(int?[] codes)
		{
			if (codes.Length == 0)
				codes = new int?[] { 0 };
			else
			{
				// I don't know how these would be combined with other codes, so do them here
				if (codes[0] == 38 && getAtOrDefault(codes, 1, 0) == 5)
				{
					font.Foreground = TerminalColors.GetXtermColor(getAtOrDefault(codes, 2, 0));
					return true;
				}
				else if (codes[0] == 48 && getAtOrDefault(codes, 1, 0) == 5)
				{
					font.Background = TerminalColors.GetXtermColor(getAtOrDefault(codes, 2, 0));
					return true;
				}
			}

			foreach (int? code in codes)
			{
				int sgr = code.GetValueOrDefault(0);

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
				else if (sgr == 39)
					font.Foreground = DefaultFont.Foreground;
				else if (sgr >= 40 && sgr <= 47)
					font.Background = TerminalColors.GetBasicColor(sgr - 40);
				else if (sgr == 49)
					font.Background = DefaultFont.Background;
				else if (sgr >= 90 && sgr <= 97)
					font.Foreground = TerminalColors.GetBasicColor(sgr - 90);
				else if (sgr >= 100 && sgr <= 107)
					font.Background = TerminalColors.GetBasicColor(sgr - 100);
				else
					return false;
			}
			return true;
		}

		public bool BlinkCursor
		{ get { return privateModes[XtermDecMode.BlinkCursor]; } }

		public bool ShowCursor
		{ get { return privateModes[XtermDecMode.ShowCursor]; } }

		public bool AppCursorKeys
		{ get { return privateModes[XtermDecMode.AppCursorKeys]; } }

		public bool UnderlineCursor
		{ get; private set; }

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
					mainScreenSavedCursorPos = CursorPos;
					CursorPos = new Point(0, 0);
					ChangeToScreen(true);
					break;

				case XtermDecMode.AppCursorKeys:
				case XtermDecMode.BlinkCursor:
				case XtermDecMode.ShowCursor:
					break;

				default:
					handled = false;
					break;
			}

			setPrivateMode((XtermDecMode) kind, true);
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
					ChangeToScreen(false);
					CursorPos = mainScreenSavedCursorPos;
					break;

				case XtermDecMode.AppCursorKeys:
				case XtermDecMode.BlinkCursor:
				case XtermDecMode.ShowCursor:
					break;

				default:
					handled = false;
					break;
			}

			setPrivateMode((XtermDecMode) kind, false);

			return handled;
		}

		int scrollRegionTop = 0;
		int scrollRegionBottom = int.MaxValue;

		bool handleCsi(string sequence)
		{
			bool handled = true;

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
						foreach (int code in codes)
							handled &= handleDecSet(code);
						break;

					case 'l':
						foreach (int code in codes)
							handled &= handleDecReset(code);
						break;

					case 'r':
						if (sequence[sequence.Length - 2] == ' ')
						{
							int cursorType = getAtOrDefault(codes, 0, 1);
							if (cursorType == 3 || cursorType == 4)
								UnderlineCursor = true;
							else
								UnderlineCursor = false;

							if (cursorType == 0 || cursorType == 1 || cursorType == 3 || cursorType == 5)
								privateModes[XtermDecMode.BlinkCursor] = true;
							else
								privateModes[XtermDecMode.BlinkCursor] = false;
						}
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
					case '@':
						// ICH: Insert x = 1 blank characters at cursor, moving the cursor accordingly.
						InsertCharacters(new string(' ', getAtOrDefault(codes, 0, 1)), CurrentFont);
						break;

					case 'A':
						// CUU: Move cursor up x = 1 rows, clamped to screen size.
						CursorPos = new Point(CursorPos.Col, CursorPos.Row - getAtOrDefault(codes, 0, 1));
						break;

					case 'B':
						// CUD: Move cursor down x = 1 rows, clamped to screen size.
						CursorPos = new Point(CursorPos.Col, CursorPos.Row + getAtOrDefault(codes, 0, 1));
						break;

					case 'C':
					case 'a':
						// CUF: Move cursor right x = 1 columns, clamped to screen size.
						CursorPos = new Point(CursorPos.Col + getAtOrDefault(codes, 0, 1), CursorPos.Row);
						break;

					case 'D':
						// CUB: Move cursor left x = 1 columns, clamped to screen size.
						CursorPos = new Point(CursorPos.Col - getAtOrDefault(codes, 0, 1), CursorPos.Row);
						break;

					case 'E':
						// CNL: Move cursor to the first column of the xth = 1 row below the cursor.
						CursorPos = new Point(0, CursorPos.Row + getAtOrDefault(codes, 0, 1));
						break;

					case 'F':
						// CPL: Move cursor to the first column of the xth = 1 row above the cursor.
						CursorPos = new Point(0, CursorPos.Row - getAtOrDefault(codes, 0, 1));
						break;

					case 'G':
					case '`':
						// CHA: Move cursor to the xth = 1 column of the current row.
						CursorPos = new Point(getAtOrDefault(codes, 0, 1) - 1, CursorPos.Row);
						break;

					case 'H':
					case 'f':
						// CUP: Move cursor to the xth = 1 row and yth = 1 column of the screen.
						{
							int row = getAtOrDefault(codes, 0, 1);
							int col = getAtOrDefault(codes, 1, 1);
							CursorPos = new Point(col - 1, row - 1);
						}
						break;

					case 'I':
						// CHT: Move the cursor forward x = 1 tabstops.  Tabstops seem to be every 8 characters.
						int times = getAtOrDefault(codes, 0, 1);
						for (int i = 0; i < times; ++i)
							tab(false);
						break;

					case 'J':
						// ED: Erase certain lines depending on x = 0 WITHOUT changing the cursor 
						//     position:
						//     x = 0: Erase everything under and to the right of the cursor on the 
						//            current line, then everything on the lines underneath.
						//     x = 1: Erase everything to the left (not under?) of the cursor on 
						//            the current line, then everything on the lines above.
						//     x = 2: Erase everything.
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

					// UNIMPLEMENTED: 
					// DECSED: Do the same as ED, except respect the character
					//         protection attribute set with DECSCA?

					case 'K':
						// EL: Erase a portion of the line the cursor is on depending on x = 0
						//     WITHOUT changing the cursor position:
						//     x = 0: Erase everything under and to the right of the cursor
						//     x = 1: Erase everything under and to the left of the cursor
						//     x = 2: Erase the entire line
						switch (getAtOrDefault(codes, 0, 0))
						{
							case 0:
								EraseCharacters(Size.Col - CursorPos.Col, false);
								break;

							case 1:
								CursorPos = new Point(0, CursorPos.Row);
								EraseCharacters(oldCursorPos.Col + 1, false);
								break;

							case 2:
								CursorPos = new Point(0, CursorPos.Row);
								EraseCharacters(Size.Col + 1, false);
								break;
						}
						CursorPos = oldCursorPos;
						break;

					// UNIMPLEMENTED: 
					// DECSEL: Do the same as EL, except respect the character
					//         protection attribute set with DECSCA?

					case 'L':
						// IL: Insert x = 1 lines at the cursor, scrolling using the scroll region
						//     if necessary WITHOUT moving the cursor.  The insertion happens just
						//     before the current line (so the current line is pushed down).
						{
							int rows = getAtOrDefault(codes, 0, 1);
							// Copy rows to their new location.  Start at the bottom of the 
							// scrollable region so we don't delete rows before we copy them.
							for (int i = scrollRegionBottom - 1; i >= CursorPos.Row; --i)
							{
								lines[i].DeleteCharacters(0, lines[i].Length);
								// Only copy rows if the "original" row is being affected by the
								// insertion operation.
								if (i - rows >= Math.Max(CursorPos.Row, scrollRegionTop))
								{
									foreach (var run in lines[i - rows].Runs)
									{
										lines[i].SetCharacters(lines[i].Length, run.Text, run.Font);
									}
								}
							}
						}
						break;

					case 'M':
						// DL: Delete x = 1 lines at the cursor, scrolling lines up from the bottom
						//     of the scroll region WITHOUT moving the cursor.  The current line is
						//     included in the delete.
						{
							int rows = getAtOrDefault(codes, 0, 1);
							for (int i = CursorPos.Row; i < scrollRegionBottom; ++i)
							{
								lines[i].DeleteCharacters(0, lines[i].Length);
								if (i + rows < scrollRegionBottom)
								{
									foreach (var run in lines[i + rows].Runs)
									{
										lines[i].SetCharacters(lines[i].Length, run.Text, run.Font);
									}
								}
							}
						}
						break;

					case 'P':
						// DCH: Delete x = 1 characters starting at the cursor.
						DeleteCharacters(getAtOrDefault(codes, 0, 1));
						break;

					case 'S':
						// SU: Scroll up x = 1 lines, respecting the scroll regions.  All lines
						//     that are more than x lines from the top are moved up x lines.  Rows
						//     near the bottom are replaced with empty lines.
						scroll(false, getAtOrDefault(codes, 0, 1));
						break;

					case 'T':
						// SD: Scroll down x = 1 lines.  Similar to SU, except in the opposite 
						//     direction.  Rows near the top are replaced with empty lines.
						scroll(true, getAtOrDefault(codes, 0, 1));
						break;

					case 'X':
						// ECH: Erase x = 1 characters, starting at and including the cursor.  Not
						//      sure if this should wrap lines.  Cursor is moved accordingly.
						EraseCharacters(getAtOrDefault(codes, 0, 1));
						break;

					case 'Z':
						// CBT: Move the cursor back x = 1 tabstops.  Same as CHT, except reversed.
						tab(true);
						break;
						
					// UNIMPLEMENTED:
					// REP: Repeat the character in the cell to the left of the cursor x = 1 
					//      times to the right, moving the cursor accordingly.  Not sure if 
					//      these are inserts or overwrites.

					case 'd':
						// VPA: Move cursor to the xth = 1 row without changing the column.
						{
							int row = getAtOrDefault(codes, 0, 1);
							CursorPos = new Point(CursorPos.Col, row - 1);
						}
						break;

					case 'e':
						// VPR: Move the cursor down x = 1 rows without changing the column.
						{
							int rows = getAtOrDefault(codes, 0, 1);
							CursorPos = new Point(CursorPos.Col, CursorPos.Row + rows);
						}
						break;

					case 'm':
						// SGR: Set attribute of next characters to be written.
						handled = handleCsiSgr(codes);
						break;

					case 'r':
						// DECSTBM: Set scroll region to start at row x = 1 and end at row
						//          y = height.  Top is inclusive, bottom is exclusive.
						//          Also, set cursor to the top-left corner?
						scrollRegionTop = getAtOrDefault(codes, 0, 1) - 1;
						scrollRegionBottom = getAtOrDefault(codes, 1, Size.Row);
						CursorPos = new Point(0, 0);
						break;

					case 's':
						// Save cursor position
						savedCursorPos.Col = CursorPos.Col;
						savedCursorPos.Row = CursorPos.Row;
						break;

					case 'u':
						// Restore cursor position
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

		bool handleOsc(string sequence)
		{
			bool handled = true;
			int separatorIndex = sequence.IndexOf(';');
			if (separatorIndex == -1)
				separatorIndex = sequence.Length;
			int kind = int.Parse(sequence.Substring(0, separatorIndex));
			switch (kind)
			{
				case 0:
					if (TitleChanged != null && separatorIndex != sequence.Length)
						TitleChanged(this, new TitleChangeEventArgs(sequence.Substring(separatorIndex + 1)));
					break;

				default:
					handled = false;
					break;
			}
			System.Diagnostics.Debug.WriteLine(string.Format("{0} ^[ ] {1} [ST]", handled ? "X" : " ", sequence));
			return handled;
		}

		//bool applicationKeypad = false;
		bool handleSingleEscape(char kind)
		{
			bool handled = true;
			string sequence = new string(kind, 1);
			//if (kind == '=')
			//	applicationKeypad = true;
			//else if (kind == '>')
			//	applicationKeypad = false;
			if (kind == '7')
				savedCursorPos = CursorPos;
			else if (kind == '8')
				CursorPos = savedCursorPos;
			else if (kind == 'M')
				lineFeed(true);
			else
				handled = false;
			System.Diagnostics.Debug.WriteLine(string.Format("{0} ^[ {1}", handled ? "X" : " ", sequence));

			return handled;
		}

		void scroll(bool up, int count)
		{
			int actualTop = Math.Max(scrollRegionTop, 0);
			int actualBottom = Math.Min(scrollRegionBottom, Terminal.Size.Row);
			if (up)
			{
				MoveLines(actualTop, actualTop + count, actualBottom - actualTop - count);
			}
			else
			{
				MoveLines(actualTop + count, actualTop, actualBottom - actualTop - count);
			}
		}

		void tab(bool reverse)
		{
			const int TabStopInterval = 8;
			if (reverse)
				CursorPos = new Point(Math.Max(((CursorPos.Col - TabStopInterval) / TabStopInterval + 1) * TabStopInterval, 0), CursorPos.Row);
			else
				CursorPos = new Point(Math.Min(((CursorPos.Col + TabStopInterval) / TabStopInterval) * TabStopInterval, Size.Col - 1), CursorPos.Row);
		}

		void lineFeed(bool reverse)
		{
			int actualTop = Math.Max(scrollRegionTop, 0);
			int actualBottom = Math.Min(scrollRegionBottom, Terminal.Size.Row);
			if (reverse)
			{
				int newRow = CursorPos.Row - 1;
				if (newRow < actualTop)
				{
					MoveLines(actualTop, actualTop + 1, actualBottom - actualTop - 1);
				}
				else
					CursorPos = new Point(CursorPos.Col, newRow);
			}
			else
			{
				int newRow = CursorPos.Row + 1;
				if (newRow >= actualBottom)
				{
					MoveLines(actualTop + 1, actualTop, actualBottom - actualTop - 1);
				}
				else
					CursorPos = new Point(CursorPos.Col, newRow);
			}
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected new virtual void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (!disposedValue)
			{
				if (disposing)
				{
					parser.Dispose();
				}

				parser = null;

				disposedValue = true;
			}
		}
		#endregion
	}
}
