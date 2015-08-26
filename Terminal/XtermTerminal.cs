using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
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
			while (!reader.EndOfStream)
				readChar();
			if (state == State.Text)
				endSequence(SequenceType.Text);
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
			privateModes[mode] = value;

			if (PrivateModeChanged != null)
				PrivateModeChanged(this, new PrivateModeChangedEventArgs(mode, value));
		}

		public event EventHandler<TitleChangeEventArgs> TitleChanged;
		public event EventHandler<PrivateModeChangedEventArgs> PrivateModeChanged;

		public XtermTerminal(IStreamNotifier streamNotifier)
			: base(streamNotifier)
		{
			Contract.Requires(streamNotifier != null);

			foreach (var key in Enum.GetValues(typeof(XtermDecMode)).Cast<XtermDecMode>())
				privateModes.Add(key, false);
			privateModes[XtermDecMode.BlinkCursor] = true;
			privateModes[XtermDecMode.ShowCursor] = true;
			privateModes[XtermDecMode.UseNormalScreen] = true;
			privateModes[XtermDecMode.Wraparound] = true;

			parser = new XtermStreamParser(streamNotifier);
			parser.SequenceReceived += Parser_SequenceReceived;
		}

		private void Parser_SequenceReceived(object sender, SequenceReceivedEventArgs e)
		{
			switch (e.Type)
			{
				case SequenceType.Text:
					Terminal.SetCharacters(e.Sequence, font);
					break;

				case SequenceType.Csi:
					handleCsi(e.Sequence);
					break;

				case SequenceType.Osc:
					handleOsc(e.Sequence);
					break;

				case SequenceType.SingleEscape:
					handleSingleEscape(e.Sequence[0]);
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
					break;
			}
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
					savedCursorPos = CursorPos;
					ChangeToScreen(true);
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
					CursorPos = savedCursorPos;
					ChangeToScreen(false);
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
					case 'm':
						handled = handleCsiSgr(codes);
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

					case 'M':
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

					case 'S':
						foreach (int i in Enumerable.Range(0, getAtOrDefault(codes, 0, 1)))
							scroll(false);
						break;

					case 'T':
						foreach (int i in Enumerable.Range(0, getAtOrDefault(codes, 0, 1)))
							scroll(true);
						break;

					case 'P':
						DeleteCharacters(getAtOrDefault(codes, 0, 1));
						break;

					case 'r':
						scrollRegionTop = getAtOrDefault(codes, 0, 1) - 1;
						scrollRegionBottom = getAtOrDefault(codes, 1, Size.Row);
						CursorPos = new Point(0, 0);
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

		bool handleOsc(string sequence)
		{
			bool handled = true;
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
				applicationKeypad = true;
			else if (kind == '>')
				applicationKeypad = false;
			else if (kind == '7')
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

		void scroll(bool up)
		{
			int actualTop = Math.Max(scrollRegionTop, 0);
			int actualBottom = Math.Min(scrollRegionBottom, Terminal.Size.Row);
			if (up)
			{
				MoveLines(actualTop, actualTop + 1, actualBottom - actualTop - 1);
			}
			else
			{
				MoveLines(actualTop + 1, actualTop, actualBottom - actualTop - 1);
			}
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
