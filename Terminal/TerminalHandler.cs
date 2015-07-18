using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Terminal
{
	public class TitleChangeEventArgs : EventArgs
	{
		public string Title
		{ get; }

		public TitleChangeEventArgs(string title)
		{
			Title = title;
		}
	}

	public interface ITerminalHandler
	{
		Terminal Terminal
		{ get; }

		TerminalFont DefaultFont
		{ get; set; }

		void HandleInput(StreamReader reader);
	}

	public class XtermTerminalHandler : ITerminalHandler
	{
		Terminal terminal;
		public Terminal Terminal
		{ get { return Terminal; } }

		Point savedCursorPos;
		StringBuilder runBuilder = new StringBuilder();
		TerminalFont font;

		Thread processingThread;
		Queue<char> buffer = new Queue<char>();

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

		AutoResetEvent dataReceived = new AutoResetEvent(false);
		AutoResetEvent dataProcessed = new AutoResetEvent(false);

		public event EventHandler<TitleChangeEventArgs> TitleChanged;

		public XtermTerminalHandler(Terminal terminal)
		{
			this.terminal = terminal;

			processingThread = new Thread(handleInputCore);
			processingThread.IsBackground = true;
			processingThread.Name = "XtermTerminalHandler::handleInputCore";
			processingThread.Start();
		}

		void endRun()
		{
			if (runBuilder.Length > 0)
			{
				System.Diagnostics.Debug.WriteLine("Read run: " + runBuilder.ToString());
				string text = runBuilder.ToString();
				terminal.SetCharacters(text, font);
				runBuilder.Clear();
			}
		}

		static int getAtOrDefault(int?[] arr, int index, int defaultValue)
		{
			if (index >= arr.Length)
				return defaultValue;
			return arr[index].GetValueOrDefault(defaultValue);
		}

		char readOne()
		{
			if (buffer.Count > 0)
				return buffer.Dequeue();
			dataReceived.WaitOne();
			return buffer.Dequeue();
		}

		string readUntil(Predicate<char> untilPredicate)
		{
			char c;
			var builder = new StringBuilder();
			do
			{
				c = readOne();
				builder.Append(c);
			} while (!untilPredicate(c));

			return builder.ToString();
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
			else
				return false;
			return true;
		}

		bool handleCsi()
		{
			bool handled = true;

			endRun();

			string sequence = readUntil(ch => ch >= 64 && ch <= 126);
			bool extendedKind = sequence[0] == '?';
			char kind = sequence.Last();
			int?[] codes = null;
			try
			{
				string realSequence;
				if (extendedKind)
					realSequence = sequence.Substring(1, sequence.Length - 2);
				else
					realSequence = sequence.Substring(0, sequence.Length - 1);
				codes = (from str in realSequence.Split(';') select str.Length > 0 ? (int?) int.Parse(str) : null).ToArray();
			}
			catch (FormatException ex)
			{
				var a = ex.StackTrace;
			}

			if (extendedKind)
			{
			}
			else
			{
				switch (kind)
				{
					case 'm':
						handled = handleCsiSgr(getAtOrDefault(codes, 0, 0), codes);
						break;

					case 'A':
						terminal.CursorPos = new Point(terminal.CursorPos.Col, terminal.CursorPos.Row - getAtOrDefault(codes, 0, 1));
						break;

					case 'B':
						terminal.CursorPos = new Point(terminal.CursorPos.Col, terminal.CursorPos.Row + getAtOrDefault(codes, 0, 1));
						break;

					case 'C':
						terminal.CursorPos = new Point(terminal.CursorPos.Col + getAtOrDefault(codes, 0, 1), terminal.CursorPos.Row);
						break;

					case 'D':
						terminal.CursorPos = new Point(terminal.CursorPos.Col - getAtOrDefault(codes, 0, 1), terminal.CursorPos.Row);
						break;

					case 'E':
						terminal.CursorPos = new Point(0, terminal.CursorPos.Row + 1);
						break;

					case 'F':
						terminal.CursorPos = new Point(0, terminal.CursorPos.Row - 1);
						break;

					case 'G':
						terminal.CursorPos = new Point(getAtOrDefault(codes, 0, 1) - 1, terminal.CursorPos.Row);
						break;

					case 'H':
					case 'f':
						int row = getAtOrDefault(codes, 0, 1);
						int col = getAtOrDefault(codes, 1, 1);
						terminal.CursorPos = new Point(row - 1, col - 1);
						break;

					case 'K':
						Point oldCursorPos = terminal.CursorPos;
						switch (getAtOrDefault(codes, 0, 0))
						{
							case 0:
								terminal.SetCharacters(new string(' ', terminal.Size.Col - terminal.CursorPos.Col), new TerminalFont() { Hidden = true }, false);
								break;

							case 1:
								terminal.CursorPos = new Point(0, terminal.CursorPos.Row);
								terminal.SetCharacters(new string(' ', oldCursorPos.Col + 1), new TerminalFont() { Hidden = true }, false);
								break;

							case 2:
								terminal.CursorPos = new Point(0, terminal.CursorPos.Row);
								terminal.SetCharacters(new string(' ', terminal.Size.Col), new TerminalFont { Hidden = true }, false);
								break;
						}
						terminal.CursorPos = oldCursorPos;
						break;

					case 's':
						savedCursorPos.Col = terminal.CursorPos.Col;
						savedCursorPos.Row = terminal.CursorPos.Row;
						break;

					case 'u':
						terminal.CursorPos = new Point(savedCursorPos.Col, savedCursorPos.Row);
						break;

					default:
						handled = false;
						break;
				}
			}

			return handled;
		}

		bool handleOsc()
		{
			string sequence = readUntil(ch => ch == 7);
			sequence = sequence.Substring(0, sequence.Length - 1);
			int kind = int.Parse(sequence.Substring(0, sequence.IndexOf(';')));
			switch (kind)
			{
				case 0:
					if (TitleChanged != null)
						TitleChanged(this, new TitleChangeEventArgs(sequence.Substring(sequence.IndexOf(';') + 1)));
					break;

				default:
					return false;
			}
			return true;
		}

		void handleInputCore()
		{
			while (true)
			{
				if (buffer.Count == 0)
				{
					endRun();
					dataProcessed.Set();
					dataReceived.WaitOne(TimeSpan.FromMilliseconds(500));
				}

				char c = readOne();
				if (!char.IsControl(c) || c == '\r' || c == '\n')
					runBuilder.Append(c);
				else if (c != '\x1b')
				{
					if (c == '\b')
					{
						endRun();

						if (terminal.CursorPos.Col == 0)
							terminal.CursorPos = new Point(terminal.Size.Col - 1, terminal.CursorPos.Row - 1);
						else
							terminal.CursorPos = new Point(terminal.CursorPos.Col - 1, terminal.CursorPos.Row);
					}
				}
				else
				{
					bool handled = true;

					char escapeKind = readOne();
					string sequence = "";
					if (escapeKind == '[')
						handled = handleCsi();
					else if (escapeKind == ']')
						handled = handleOsc();
					else if (escapeKind == '(')
					{
						sequence = new string((char) readOne(), 1);
						handled = false;
					}
					else
					{
						handled = false;
					}

					System.Diagnostics.Debug.WriteLine(string.Format("Escape sequence: ^[{0}{1}{2}", escapeKind, sequence, !handled ? " (unhandled)" : ""));
				}
			}
		}

		bool busy = false;
		public void HandleInput(StreamReader reader)
		{
			if (busy)
				System.Diagnostics.Debugger.Break();
			busy = true;

			while (!reader.EndOfStream)
				buffer.Enqueue((char) reader.Read());

			dataProcessed.Reset();
			dataReceived.Set();
			dataProcessed.WaitOne(TimeSpan.FromMilliseconds(500));

			busy = false;
		}
	}
}
