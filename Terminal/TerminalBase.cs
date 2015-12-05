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
		public event EventHandler<EventArgs> SizeChanged;
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

				if (LinesMoved != null)
					LinesMoved(this, new LinesMovedEventArgs(1, 0, Size.Row - 1));
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

			if (LinesMoved != null)
				LinesMoved(this, new LinesMovedEventArgs(index, newIndex, count));
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

		public void SetCharacters(string text, TerminalFont font, bool advanceCursor = true, bool wrapAround = true)
		{
			int textIndex = 0;
			while (textIndex < text.Length)
			{
				int lineEnd = text.IndexOf('\r', textIndex, Math.Min(text.Length - textIndex, Size.Col - CursorPos.Col + 1));
				bool carriageFound = false;
				if (lineEnd == -1)
					lineEnd = text.Length;
				else
					carriageFound = true;
				lineEnd = textIndex + Math.Min(lineEnd - textIndex, Size.Col - CursorPos.Col);

				lines[CursorPos.Row].SetCharacters(CursorPos.Col, text.Substring(textIndex, lineEnd - textIndex), font);
				if (advanceCursor && !font.Hidden)
					cursorCol += lineEnd - textIndex;
				textIndex = lineEnd;

				//bool allowScroll = wrapAround || cursorRow != Size.Row - 1;
				if (cursorCol == Size.Col && (!AutoWrapMode || (false && !wrapAround && cursorRow == Size.Row - 1)))
					cursorCol--;

				bool endOfLine = (cursorCol == Size.Col);
				bool nextRow = endOfLine;
				if (carriageFound)
				{
					if (text[textIndex] != '\r')
						textIndex++;
					textIndex++;
					cursorCol = 0;
					nextRow = false;
				}

				if (nextRow && advanceCursor)
					advanceCursorRow();
			}

			if (CursorPosChanged != null && advanceCursor)
				CursorPosChanged(this, EventArgs.Empty);
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
