using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace npcook.Terminal
{
	enum BufferResizeKind
	{
		AddRemoveTop,
		AddRemoveBottom,
	}

	class TerminalBuffer
	{
		public Point Size
		{ get; private set; }

		int cursorCol;
		public int CursorCol
		{
			get { return cursorCol; }
			set { cursorCol = Math.Min(Math.Max(value, 0), Size.Col - 1); }
		}

		int cursorRow;
		public int CursorRow
		{
			get { return cursorRow; }
			set { cursorRow = Math.Min(Math.Max(value, 0), Size.Row - 1); }
		}

		TerminalLine[] lines = new TerminalLine[0];
		public TerminalLine[] Lines
		{ get { return lines; } }

		public void Resize(int cols, int rows)
		{
			Size = new Point(cols, rows);

			int oldRowCount = lines.Length;
			if (rows != oldRowCount)
			{
				var newLines = new TerminalLine[rows];
				if (rows > oldRowCount)
				{
					Array.Copy(lines, 0, newLines, 0, oldRowCount);
					for (int i = oldRowCount; i < rows; ++i)
						newLines[i] = new TerminalLine();
				}
				else if (rows < oldRowCount)
				{
					int startIndex = 0;
					int remainingRows = cursorRow - rows + 1;
					if (remainingRows > 0)
						startIndex = remainingRows;
					Array.Copy(lines, startIndex, newLines, 0, rows);
				}

				lines = newLines;
			}

			foreach (var line in lines)
			{
				line.ColCount = cols;
			}

			CursorRow = CursorRow;
		}
	}
}
