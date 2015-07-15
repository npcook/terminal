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

namespace Terminal
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

	public class Terminal
	{
		int cursorCol;
		int cursorRow;
		public Point CursorPos
		{
			get { return new Point(cursorCol, cursorRow); }
			internal set
			{
				if (value.Col < 0 || value.Row < 0 || value.Col >= Size.Col || value.Row >= Size.Row)
					throw new ArgumentOutOfRangeException("value", value, "CursorPos set outside of the terminal size");
				cursorCol = value.Col;
				cursorRow = value.Row;

				System.Diagnostics.Debug.WriteLine(string.Format("Moving cursor to {0}", value));

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
				if (value.Row < size.Row)
				{
					var newScreen = new TerminalLine[value.Row];
					Array.Copy(screen, size.Row - value.Row, newScreen, 0, value.Row);
					screen = newScreen;
				}
				else if (value.Row > size.Row)
				{
					var newScreen = new TerminalLine[value.Row];
					Array.Copy(screen, newScreen, size.Row);
					for (int i = size.Row; i < value.Row; ++i)
						newScreen[i] = new TerminalLine();
					screen = newScreen;
				}
				foreach (var line in screen)
					line.ColCount = value.Col;
				size = value;

				if (SizeChanged != null)
					SizeChanged(this, EventArgs.Empty);
			}
		}

		public TerminalFont CurrentFont
		{ get; set; }

		TerminalLine[] screen = new TerminalLine[0];
		public IReadOnlyCollection<TerminalLine> Screen
		{ get { return screen; } }

		public event EventHandler<EventArgs> CursorPosChanged;
		public event EventHandler<EventArgs> SizeChanged;
		public event EventHandler<LineShiftedUpEventArgs> LineShiftedUp;

		public Terminal()
		{ }

		void advanceCursorRow()
		{
			cursorRow++;
			if (cursorCol == Size.Col)
				cursorCol = 0;

			if (cursorRow == Size.Row)
			{
				var oldLine = screen[0];
				var newLine = new TerminalLine();
				cursorRow--;
				Array.Copy(screen, 1, screen, 0, screen.Length - 1);
				screen[screen.Length - 1] = newLine;

				if (LineShiftedUp != null)
					LineShiftedUp(this, new LineShiftedUpEventArgs(oldLine, newLine));
			}
		}

		public void SetCharacters(string text, TerminalFont font, bool advanceCursor = true)
		{
			int textIndex = 0;
			while (textIndex < text.Length)
			{
				int lineEnd = text.IndexOfAny(new[] { '\r', '\n' }, textIndex);
				bool controlFound = false;
				if (lineEnd == -1)
					lineEnd = text.Length;
				else
					controlFound = true;
				lineEnd = Math.Min(lineEnd, Size.Col - CursorPos.Col);

				screen[CursorPos.Row].SetCharacters(CursorPos.Col, text.Substring(textIndex, lineEnd - textIndex), font);
				if (advanceCursor)
					cursorCol += lineEnd - textIndex;
				textIndex = lineEnd;

				bool nextRow = (cursorCol == Size.Col);
				if (controlFound)
				{
					if (text[textIndex] != '\r' && text[textIndex] != '\n')
						textIndex++;
					char c = text[textIndex];
					if (c == '\r')
					{
						textIndex++;
						cursorCol = 0;
					}
					else if (c == '\n')
					{
						textIndex++;
						nextRow = true;
					}
				}

				if (nextRow && advanceCursor)
					advanceCursorRow();
			}

			if (CursorPosChanged != null && advanceCursor)
				CursorPosChanged(this, EventArgs.Empty);
		}
	}
}
