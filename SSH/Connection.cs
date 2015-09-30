using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace npcook.Ssh
{
	public enum ConnectionError
	{
		PassphraseIncorrect,
		NetError,
		AuthenticationError,
	}

	public class ConnectionFailedEventArgs : EventArgs
	{
		public ConnectionError Error
		{ get; }

		public string Message
		{ get; }

		public ConnectionFailedEventArgs(ConnectionError error, string message)
		{
			Error = error;
			Message = message;
		}
	}

	public class Connection
	{
		Thread dataThread = null;

		public SshClient Client
		{ get; private set; }

		public ShellStream Stream
		{ get; private set; }

		public event EventHandler Connected;
		public event EventHandler<ConnectionFailedEventArgs> Failed;

		public Connection()
		{ }

		public void Connect(string serverAddress, int serverPort, string username, IEnumerable<Authentication> authentications, int terminalCols, int terminalRows)
		{
			if (dataThread != null)
				throw new InvalidOperationException("Already connecting to a server.");
			dataThread = new Thread(() =>
			{
#if USE_LIBSSHNET
				var connection = new LibSshNetConnection(serverAddress, username, (authentications.First() as PasswordAuthentication).Password);
				stream = connection.GetStream();
#else
				try
				{
					ConnectionInfo connectionInfo = new ConnectionInfo(serverAddress, serverPort, username, authentications.Select(auth =>
					{
						if (auth is PasswordAuthentication)
							return new PasswordAuthenticationMethod(username, (auth as PasswordAuthentication).Password) as AuthenticationMethod;
						else if (auth is KeyAuthentication)
						{
							var privateKeyFile = new PrivateKeyFile((auth as KeyAuthentication).Key, (auth as KeyAuthentication).Passphrase);
							return new PrivateKeyAuthenticationMethod(username, privateKeyFile) as AuthenticationMethod;
						}
						else
							throw new NotImplementedException("Unknown type of authentication given to Connect");
					}).ToArray());

					Client = new SshClient(connectionInfo);
					Client.Connect();

					Client.KeepAliveInterval = TimeSpan.FromSeconds(20);

					Stream = Client.CreateShellStream("xterm-256color", (uint) terminalCols, (uint) terminalRows, 0, 0, 1000);

					if (Connected != null)
						Connected(this, EventArgs.Empty);
				}
				catch (Exception ex)
				{
					ConnectionFailedEventArgs args = null;
					if (ex is Renci.SshNet.Common.SshPassPhraseNullOrEmptyException ||
						ex is InvalidOperationException)
						args = new ConnectionFailedEventArgs(ConnectionError.PassphraseIncorrect, ex.Message);
					else if (ex is SocketException)
						args = new ConnectionFailedEventArgs(ConnectionError.NetError, ex.Message);
					else if (ex is Renci.SshNet.Common.SshAuthenticationException)
						args = new ConnectionFailedEventArgs(ConnectionError.AuthenticationError, ex.Message);
					else
						throw;

					if (Failed != null)
						Failed(this, args);
				}
#endif
			});
			dataThread.Name = "Data Thread";
			dataThread.IsBackground = true;
			dataThread.Start();
		}
	}
}
