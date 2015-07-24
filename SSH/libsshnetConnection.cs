using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using npcook.libsshnet;
using System.IO;
using System.Threading;

namespace npcook.Ssh
{
	class libsshnetStream : Stream
	{
		libsshnetConnection connection;
		Queue<byte> buffer = new Queue<byte>(2048);

		internal libsshnetStream(libsshnetConnection connection)
		{
			this.connection = connection;
		}

		public override bool CanRead
		{ get { return true; } }

		public override bool CanSeek
		{ get { return false; } }

		public override bool CanWrite
		{ get { return true; } }

		public override long Length
		{ get { throw new NotImplementedException(); } }

		public override long Position
		{
			get { return 0; }
			set { throw new NotImplementedException(); }
		}

		public override void Flush()
		{
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			return connection.Pty.Read(buffer, offset, count, TimeSpan.Zero);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			int totalBytesWritten = 0;
			while (totalBytesWritten < count)
			{
				int bytesWritten = connection.Pty.Write(buffer, offset, count, false);
				if (bytesWritten == 0)
					throw new EndOfStreamException();
				totalBytesWritten += bytesWritten;
			}
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotImplementedException();
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		internal void StartReading()
		{
			var thread = new Thread(ReadThread);
			thread.IsBackground = true;
			thread.Start();
		}

		void ReadThread()
		{
			byte[] readBuffer = new byte[256];
			while (true)
			{
				int bytesRead = connection.Pty.Read(readBuffer, TimeSpan.FromMilliseconds(100));
				if (bytesRead > 0)
				{
					for (int i = 0; i < bytesRead; ++i)
						buffer.Enqueue(readBuffer[i]);

					if (DataReceived != null)
						DataReceived(this, EventArgs.Empty);
				}
				else
					break;
			}
		}

		public event EventHandler<EventArgs> DataReceived;
	}

	class libsshnetConnection
	{
		internal Session Session
		{ get; private set; }

		internal Channel Pty
		{ get; private set; }

		libsshnetStream stream;

		public libsshnetConnection(string host, string username, string password)
		{
			Session = new Session();
			Session.SetOption(SshOption.Host, host);
			Session.SetOption(SshOption.Port, 22);
			Session.SetOption(SshOption.User, username);
			Session.SetOption(SshOption.CiphersCS, "aes256-cbc");
            Session.Connect();

			Session.PasswordAuth(password);

			Pty = new Channel(Session);
			Pty.RequestPty("xterm", 160, 40);
			Pty.RequestShell();

			stream = new libsshnetStream(this);
		}

		public libsshnetStream GetStream()
		{
			return stream;
		}
	}
}
