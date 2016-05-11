using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace npcook.Terminal
{
	public class LinesMovedEventArgs : EventArgs
	{
		public int OldIndex
		{ get; }

		public int NewIndex
		{ get; }

		public int Count
		{ get; }

		public int RemovedLinesIndex
		{
			get
			{
				if (NewIndex > OldIndex)
					return OldIndex + Count;
				else
					return NewIndex;
			}
		}

		public int AddedLinesIndex
		{
			get
			{
				if (NewIndex > OldIndex)
					return OldIndex;
				else
					return NewIndex + Count;
			}
		}

		public LinesMovedEventArgs(int oldIndex, int newIndex, int count)
		{
			OldIndex = oldIndex;
			NewIndex = newIndex;
			Count = count;
		}
	}

	public class SizeChangedEventArgs : EventArgs
	{
		public Point OldCursorPos
		{ get; }

		public SizeChangedEventArgs(Point oldCursorPos)
		{
			OldCursorPos = oldCursorPos;
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

		public static bool operator ==(Point _1, Point _2)
		{
			return _1.Col == _2.Col && _1.Row == _2.Row;
		}

		public static bool operator !=(Point _1, Point _2)
		{
			return _1.Col != _2.Col || _1.Row != _2.Row;
		}

		public override bool Equals(object obj)
		{
			if (obj is Point)
				return Equals((Point) obj);
			return base.Equals(obj);
		}

		public bool Equals(Point other)
		{
			return this == other;
		}

		public override int GetHashCode()
		{
			return Col ^ Row;
		}
	}

	public class StreamExceptionEventArgs : EventArgs
	{
		public Exception Exception
		{ get; }

		public StreamExceptionEventArgs(Exception exception)
		{
			Exception = exception;
		}
	}

	public class TerminalBase : IDisposable
	{
		public event EventHandler<StreamExceptionEventArgs> StreamException;
		
		public Point CursorPos
		{
			get
			{
				lock (this)
					return new Point(currentBuffer.CursorCol, currentBuffer.CursorRow);
			}
			internal set
			{
				lock (this)
				{
					currentBuffer.CursorCol = value.Col;
					currentBuffer.CursorRow = value.Row;
				}

				notifyCursorPosChanged();
			}
		}

		private int cursorCol
		{
			get { return currentBuffer.CursorCol; }
			set { currentBuffer.CursorCol = value; }
		}

		private int cursorRow
		{
			get { return currentBuffer.CursorRow; }
			set { currentBuffer.CursorRow = value; }
		}

		Point size = new Point(0, 0);
		public Point Size
		{
			get
			{
				lock (this)
				{
					System.Diagnostics.Debug.Assert(currentBuffer.Size == altScreenBuffer.Size && currentBuffer.Size == size);
					return size;
				}
			}
			set
			{
				var oldCursorPos = CursorPos;
				lock (this)
				{
					screenBuffer.Resize(value.Col, value.Row);
					altScreenBuffer.Resize(value.Col, value.Row);
					size = value;
					CursorPos = CursorPos;
				}

				SizeChanged?.Invoke(this, new SizeChangedEventArgs(oldCursorPos));
			}
		}

		public Stream Stream
		{ get; }

		BinaryWriter writer;
		protected BinaryWriter Writer
		{ get { return writer; } }

		public TerminalFont CurrentFont
		{ get; set; }

		// DECAWM: When the cursor is at the end of a line, should it wrap to the beginning of the
		// next line or should it stay at the end and overwrite
		public bool AutoWrapMode
		{ get; set; }

		TerminalBuffer screenBuffer = new TerminalBuffer();
		public IReadOnlyList<TerminalLine> Screen
		{ get { return screenBuffer.Lines; } }

		TerminalBuffer altScreenBuffer = new TerminalBuffer();
		public IReadOnlyList<TerminalLine> AltScreen
		{ get { return altScreenBuffer.Lines; } }

		TerminalBuffer currentBuffer;
		protected TerminalLine[] lines
		{ get { return currentBuffer.Lines; } }

		public IReadOnlyList<TerminalLine> CurrentScreen
		{ get { return currentBuffer.Lines; } }

		public event EventHandler<EventArgs> CursorPosChanged;
		public event EventHandler<SizeChangedEventArgs> SizeChanged;
		public event EventHandler<LinesMovedEventArgs> LinesMoved;
		public event EventHandler<EventArgs> ScreenChanged;

		public TerminalBase(IStreamNotifier streamNotifier)
		{
			Stream = streamNotifier.Stream;
			writer = new BinaryWriter(streamNotifier.Stream, Encoding.UTF8, true);

			currentBuffer = screenBuffer;

			AutoWrapMode = true;
		}

		public void ChangeToScreen(bool alternate)
		{
			if (alternate)
			{
				currentBuffer = altScreenBuffer;
				foreach (var line in currentBuffer.Lines)
				{
					line.DeleteCharacters(0, line.Length);
				}
			}
			else
				currentBuffer = screenBuffer;

			ScreenChanged?.Invoke(this, EventArgs.Empty);
		}

		void advanceCursorRow()
		{
			cursorRow++;

			if (cursorRow == Size.Row)
			{
				var oldLine = lines[0];
				var newLine = new TerminalLine();
				cursorRow--;
				Array.Copy(lines, 1, lines, 0, lines.Length - 1);
				lines[lines.Length - 1] = newLine;

				LinesMoved?.Invoke(this, new LinesMovedEventArgs(1, 0, Size.Row - 1));
			}
		}

		public void MoveLines(int index, int newIndex, int count)
		{
			int addIndex;
			if (newIndex > index)
				addIndex = index;
			else
				addIndex = newIndex + count;

			int addedCount = Math.Abs(index - newIndex);
			Array.Copy(lines, index, lines, newIndex, count);
			for (int i = 0; i < addedCount; ++i)
				lines[addIndex + i] = new TerminalLine();

			LinesMoved?.Invoke(this, new LinesMovedEventArgs(index, newIndex, count));
		}

		public void InsertCharacters(string text, TerminalFont font)
		{
			lines[CursorPos.Row].InsertCharacters(CursorPos.Col, text, font);
		}

		public void EraseCharacters(int length, bool advanceCursor = true)
		{
			SetCharacters(new string(' ', length), new TerminalFont() { Hidden = true }, advanceCursor);
		}

		public void DeleteCharacters(int length)
		{
			lines[CursorPos.Row].DeleteCharacters(CursorPos.Col, length);
		}

		bool godDamnSpecialCaseWraparoundBullshit = false;
		public void SetCharacters(string text, TerminalFont font, bool advanceCursor = true, bool wrapAround = true)
		{
			int textIndex = 0;
			int col = cursorCol;
			while (textIndex < text.Length)
			{
				if (godDamnSpecialCaseWraparoundBullshit)
				{
					if (col == Size.Col - 1 && text[0] != '\r')
					{
						col = 0;
						advanceCursorRow();
					}
					godDamnSpecialCaseWraparoundBullshit = false;
				}

				int lineEnd = text.IndexOf('\r', textIndex, Math.Min(text.Length - textIndex, Size.Col - col + 1));
				bool carriageFound = false;
				if (lineEnd == -1)
					lineEnd = text.Length;
				else
					carriageFound = true;
				lineEnd = textIndex + Math.Min(lineEnd - textIndex, Size.Col - col);

				lines[CursorPos.Row].SetCharacters(col, text.Substring(textIndex, lineEnd - textIndex), font);
				if (advanceCursor && !font.Hidden)
					col += lineEnd - textIndex;
				textIndex = lineEnd;

				//bool allowScroll = wrapAround || cursorRow != Size.Row - 1;
				if (!wrapAround && col == Size.Col)
				{
					godDamnSpecialCaseWraparoundBullshit = true;
					col--;
				}
				if (col == Size.Col && (!AutoWrapMode || (false && !wrapAround && cursorRow == Size.Row - 1)))
					col--;

				bool endOfLine = (col == Size.Col);
				bool nextRow = endOfLine;
				if (carriageFound)
				{
					if (text[textIndex] != '\r')
						textIndex++;
					textIndex++;
					col = 0;
					nextRow = false;
				}

				if (nextRow && advanceCursor) {
					col = 0;
					advanceCursorRow();
				}
			}

			cursorCol = col;

			if (advanceCursor)
				notifyCursorPosChanged();
		}

		public bool SendByte(byte data)
		{
			try
			{
				writer.Write(data);
				writer.Flush();
				return true;
			}
			catch (Exception ex)
			{
				if (StreamException != null)
					StreamException(this, new StreamExceptionEventArgs(ex));
				else
					throw;
			}
			return false;
		}

		public bool SendBytes(byte[] data)
		{
			return SendBytes(data, 0, data.Length);
		}

		public bool SendBytes(byte[] data, int index, int count)
		{
			try
			{
				writer.Write(data, index, count);
				writer.Flush();
				return true;
			}
			catch (Exception ex)
			{
				if (StreamException != null)
					StreamException(this, new StreamExceptionEventArgs(ex));
				else
					throw;
			}
			return false;
		}

		public bool SendChar(char data)
		{
			try
			{
				writer.Write(data);
				writer.Flush();
				return true;
			}
			catch (Exception ex)
			{
				if (StreamException != null)
					StreamException(this, new StreamExceptionEventArgs(ex));
				else
					throw;
			}
			return false;
		}

		void notifyCursorPosChanged()
		{
			if (CursorPosChanged != null)
				CursorPosChanged(this, EventArgs.Empty);
		}

		#region IDisposable Support
		private bool disposedValue = false; // To detect redundant calls

		protected virtual void Dispose(bool disposing)
		{
			if (!disposedValue)
			{
				if (disposing)
				{
					writer.Dispose();
				}

				disposedValue = true;
			}
		}

		public void Dispose()
		{
			Dispose(true);

			GC.SuppressFinalize(this);
		}
		#endregion
	}
}
