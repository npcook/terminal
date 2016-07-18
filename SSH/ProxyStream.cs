using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace npcook.Ssh
{
	class ProxyStream : Stream
	{
		private readonly Stream s;
		private readonly MemoryStream copyStream;
		private readonly bool nullWrite;

		public ProxyStream(Stream parent, bool copyRead = true, bool nullWrite = false)
		{
			s = parent;
			if (copyRead)
				copyStream = new MemoryStream();
			this.nullWrite = nullWrite;
		}

		public byte[] GetCopy()
		{
			Contract.Requires(copyStream != null);

			byte[] copy = new byte[copyStream.Length];
			copyStream.Seek(0, SeekOrigin.Begin);
			copyStream.Read(copy, 0, copy.Length);
			copyStream.SetLength(0);
			return copy;
		}

		public override bool CanRead => s.CanRead;
		public override bool CanSeek => s.CanSeek;
		public override bool CanWrite => nullWrite ? true : s.CanWrite;
		public override long Length => s.Length;

		public override long Position
		{
			get { return s.Position; }
			set { s.Position = value; }
		}

		public override void Flush()
		{
			s.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int readCount = s.Read(buffer, offset, count);
			copyStream?.Write(buffer, offset, readCount);
			return readCount;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return s.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			s.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (nullWrite)
				return;
			s.Write(buffer, offset, count);
		}

		bool disposed = false;
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (disposed)
				return;
			disposed = true;

			if (disposing)
			{
				copyStream?.Dispose();
				s?.Dispose();
			}
		}
	}
}
