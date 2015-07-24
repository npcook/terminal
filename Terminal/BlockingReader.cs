using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace npcook.Terminal
{
	class BlockingReader : IDisposable
	{
		AutoResetEvent dataReceived = new AutoResetEvent(false);
		StreamReader reader;
		Queue<char> buffer;

		public BlockingReader(int initialCapacity = 500)
		{
			buffer = new Queue<char>(initialCapacity);
		}

		public void Write(StreamReader reader)
		{
			while (!reader.EndOfStream)
				buffer.Enqueue((char) reader.Read());

			dataReceived.Set();
		}

		public bool DataAvailable
		{ get { return buffer.Count > 0; } }

		public char ReadOne()
		{
			if (buffer.Count > 0)
				return buffer.Dequeue();
			dataReceived.WaitOne();
			return buffer.Dequeue();
		}

		public string ReadUntil(Predicate<char> untilPredicate)
		{
			char c;
			var builder = new StringBuilder();
			do
			{
				c = ReadOne();
				builder.Append(c);
			} while (!untilPredicate(c));

			return builder.ToString();
		}

		public string ReadWhile(Predicate<char> untilPredicate)
		{
			char c;
			var builder = new StringBuilder();
			do
			{
				c = ReadOne();
				builder.Append(c);
			} while (untilPredicate(c));

			return builder.ToString();
		}

		public void Wait(TimeSpan timeout)
		{
			dataReceived.WaitOne(timeout);
		}

		#region IDisposable Support
		private bool disposed = false;

		protected virtual void Dispose(bool disposing)
		{
			if (!disposed)
			{
				if (disposing)
				{
					reader.Dispose();
					dataReceived.Dispose();
				}

				reader = null;
				dataReceived = null;

				disposed = true;
			}
		}
		
		public void Dispose()
		{
			Dispose(true);
		}
		#endregion
	}
}
