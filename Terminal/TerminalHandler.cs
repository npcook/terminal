using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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

		public TerminalFont DefaultFont
		{ get; set; }

		public event EventHandler<TitleChangeEventArgs> TitleChanged;

		public XtermTerminalHandler(Terminal terminal)
		{
			this.terminal = terminal;
		}

		bool busy = false;
		public void HandleInput(StreamReader reader)
		{
			if (busy)
				System.Diagnostics.Debugger.Break();
			busy = true;
			var font = terminal.CurrentFont;
			var runBuilder = new StringBuilder();
			Action endRun = () =>
			{
				if (runBuilder.Length > 0)
				{
					System.Diagnostics.Debug.WriteLine("Read run: " + runBuilder.ToString());
					string text = runBuilder.ToString();
					terminal.SetCharacters(text, font);
					runBuilder.Clear();
				}
			};

			while (!reader.EndOfStream)
			{
				char c = (char) reader.Read();
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

					char escapeKind = (char) reader.Read();
					string sequence = "";
					if (escapeKind == '[')
					{
						endRun();

						var sequenceBuilder = new StringBuilder();
						do
						{
							c = (char) reader.Read();
							sequenceBuilder.Append(c);
						} while (c < 64 || c > 126);
						sequence = sequenceBuilder.ToString();
						bool extendedKind = sequence[0] == '?';
						char kind = c;
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
									int sgr = codes[0].GetValueOrDefault(0);
									if (sgr == 0)
										font = DefaultFont;
									else if (sgr == 1)
										font.Bold = true;
									else if (sgr == 4)
										font.Underline = true;
									else if (sgr == 22)
										font.Bold = false;
									else if (sgr == 24)
										font.Underline = false;
									else if (sgr >= 30 && sgr <= 37)
										font.Foreground = TerminalColors.GetBasicColor(sgr - 30);
									else if (sgr == 38 && codes[1] == 5)
										font.Foreground = TerminalColors.GetXtermColor(codes[2].Value);
									else if (sgr == 39)
										font.Foreground = DefaultFont.Foreground;
									else if (sgr >= 40 && sgr <= 47)
										font.Background = TerminalColors.GetBasicColor(sgr - 40);
									else if (sgr == 48 && codes[1] == 5)
										font.Background = TerminalColors.GetXtermColor(codes[2].Value);
									else if (sgr == 49)
										font.Background = DefaultFont.Background;
									else
										handled = false;
									break;

								case 'A':
									terminal.CursorPos = new Point(terminal.CursorPos.Col, terminal.CursorPos.Row - codes[0].GetValueOrDefault(1));
									break;

								case 'B':
									terminal.CursorPos = new Point(terminal.CursorPos.Col, terminal.CursorPos.Row + codes[0].GetValueOrDefault(1));
									break;

								case 'C':
									terminal.CursorPos = new Point(terminal.CursorPos.Col + codes[0].GetValueOrDefault(1), terminal.CursorPos.Row);
									break;

								case 'D':
									terminal.CursorPos = new Point(terminal.CursorPos.Col - codes[0].GetValueOrDefault(1), terminal.CursorPos.Row);
									break;

								case 'E':
									terminal.CursorPos = new Point(0, terminal.CursorPos.Row + 1);
									break;

								case 'F':
									terminal.CursorPos = new Point(0, terminal.CursorPos.Row - 1);
									break;

								case 'G':
									terminal.CursorPos = new Point(codes[0].GetValueOrDefault(1) - 1, terminal.CursorPos.Row);
									break;

								case 'H':
								case 'f':
									int row = codes[0].GetValueOrDefault(1);
									int col = codes[1].GetValueOrDefault(1);
									terminal.CursorPos = new Point(row - 1, col - 1);
									break;

								case 'K':
									Point oldCursorPos = terminal.CursorPos;
									switch (codes[0].GetValueOrDefault(0))
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
					}
					else if (escapeKind == ']')
					{
						var sequenceBuilder = new StringBuilder();
						do
						{
							c = (char) reader.Read();
							sequenceBuilder.Append(c);
						} while (c != 0x7);
						sequenceBuilder.Remove(sequenceBuilder.Length - 1, 1);
						sequence = sequenceBuilder.ToString();
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
					}
					else if (escapeKind == '(')
					{
						sequence = new string((char) reader.Read(), 1);
						handled = false;
					}
					else
					{
						sequence = new string((char) reader.Peek(), 1);
						handled = false;
					}

					System.Diagnostics.Debug.WriteLine(string.Format("Escape sequence: ^[{0}{1}{2}", escapeKind, sequence, !handled ? " (unhandled)" : ""));
				}
			}

			endRun();

			busy = false;
		}
	}
}
