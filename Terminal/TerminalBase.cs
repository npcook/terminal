using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace npcook.Terminal
{
	public class LineShiftedUpEventArgs : EventArgs
	{
		public TerminalLine OldLine
		{ get; }

		public TerminalLine NewLine
		{ get; }

		public LineShiftedUpEventArgs(TerminalLine oldLine, TerminalLine newLine)
		{
			OldLine = oldLine;
			NewLine = newLine;
		}
	}

	public struct Point
	{
		public int Col;
		public int Row;

		public Point(int col, int row)
		{
			Col = col;
			Row = row;
		}

		public override string ToString()
		{
			return string.Format("Point({0}, {1})", Col, Row);
		}
	}

	public class TerminalBase
	{
		int cursorCol;
		int cursorRow;
		public Point CursorPos
		{
			get { return new Point(cursorCol, cursorRow); }
			internal set
			{
//				if (value.Col < 0 || value.Row < 0 || value.Col >= Size.Col || value.Row >= Size.Row)
//					throw new ArgumentOutOfRangeException("value", value, "CursorPos set outside of the terminal size");
				cursorCol = Math.Min(Math.Max(value.Col, 0), Size.Col - 1);
				cursorRow = Math.Min(Math.Max(value.Row, 0), Size.Row - 1);

//				System.Diagnostics.Debug.WriteLine(string.Format("Moving cursor to {0}", value));

				if (CursorPosChanged != null)
					CursorPosChanged(this, EventArgs.Empty);
			}
		}

		Point size = new Point(0, 0);
		public Point Size
		{
			get { return size; }
			set
			{
				screenBuffer.RowCount = value.Row;
				altScreenBuffer.RowCount = value.Row;
				size = value;

				if (SizeChanged != null)
					SizeChanged(this, EventArgs.Empty);
			}
		}

		public TerminalFont CurrentFont
		{ get; set; }

		// DECAWM: When the cursor is at the end of a line, should it wrap to the beginning of the
		// next line or should it stay at the end and overwrite
		public bool AutoWrapMode
		{ get; set; }

		TerminalBuffer screenBuffer = new TerminalBuffer();
		public IReadOnlyCollection<TerminalLine> Screen
		{ get { return screenBuffer.Lines; } }

		TerminalBuffer altScreenBuffer = new TerminalBuffer();
		public IReadOnlyCollection<TerminalLine> AltScreen
		{ get { return altScreenBuffer.Lines; } }

		TerminalBuffer currentBuffer;
		TerminalLine[] lines
		{ get { return currentBuffer.Lines; } }

		public IReadOnlyCollection<TerminalLine> CurrentScreen
		{ get { return currentBuffer.Lines; } }

		public event EventHandler<EventArgs> CursorPosChanged;
		public event EventHandler<EventArgs> SizeChanged;
		public event EventHandler<LineShiftedUpEventArgs> LineShiftedUp;
		public event EventHandler<EventArgs> ScreenChanged;

		public TerminalBase()
		{
			currentBuffer = screenBuffer;

			AutoWrapMode = true;
		}

		public void ChangeToScreen(bool alternate)
		{
			if (alternate)
				currentBuffer = altScreenBuffer;
			else
				currentBuffer = screenBuffer;

			if (ScreenChanged != null)
				ScreenChanged(this, EventArgs.Empty);
		}

		void advanceCursorRow()
		{
			cursorRow++;
			if (cursorCol == Size.Col)
				cursorCol = 0;

			if (cursorRow == Size.Row)
			{
				var oldLine = lines[0];
				var newLine = new TerminalLine();
				cursorRow--;
				Array.Copy(lines, 1, lines, 0, lines.Length - 1);
				lines[lines.Length - 1] = newLine;

				if (LineShiftedUp != null)
					LineShiftedUp(this, new LineShiftedUpEventArgs(oldLine, newLine));
			}
		}

		public void EraseCharacters(int length, bool advanceCursor = true)
		{
			SetCharacters(new string(' ', length), new TerminalFont() { Hidden = true }, advanceCursor);
		}

		public void DeleteCharacters(int length)
		{
			lines[CursorPos.Row].DeleteCharacters(CursorPos.Col, length);
		}

		public void SetCharacters(string text, TerminalFont font, bool advanceCursor = true)
		{
			bool ignoreNextLine = false;
			int textIndex = 0;
			while (textIndex < text.Length)
			{
				int lineEnd = text.IndexOfAny(new[] { '\r', '\n' }, textIndex, Math.Min(text.Length - textIndex, Size.Col - CursorPos.Col + 1));
				bool controlFound = false;
				if (lineEnd == -1)
					lineEnd = text.Length;
				else
					controlFound = true;
				lineEnd = textIndex + Math.Min(lineEnd - textIndex, Size.Col - CursorPos.Col);

				lines[CursorPos.Row].SetCharacters(CursorPos.Col, text.Substring(textIndex, lineEnd - textIndex), font);
				if (advanceCursor && !font.Hidden)
					cursorCol += lineEnd - textIndex;
				textIndex = lineEnd;

				if (cursorCol == Size.Col && !AutoWrapMode)
					cursorCol--;

				bool endOfLine = (cursorCol == Size.Col);
				bool nextRow = endOfLine;
				if (controlFound)
				{
					if (text[textIndex] != '\r' && text[textIndex] != '\n')
						textIndex++;
					char c = text[textIndex];
					if (c == '\r')
					{
						textIndex++;
						cursorCol = 0;
						nextRow = false;
					}
					else if (c == '\n')
					{
						textIndex++;
						if (!ignoreNextLine)
							nextRow = true;
						ignoreNextLine = false;
					}
				}
				else
					ignoreNextLine = false;

				if (endOfLine)
					ignoreNextLine = true;

				if (nextRow && advanceCursor)
					advanceCursorRow();
			}

			if (CursorPosChanged != null && advanceCursor)
				CursorPosChanged(this, EventArgs.Empty);
		}
	}
}
