using npcook.Terminal;
using npcook.Terminal.Controls;
using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace npcook.Ssh
{
	public class ErrorEventArgs : EventArgs
	{
		public string Message
		{ get; }

		public ErrorEventArgs(string message)
		{
			Message = message;
		}
	}

#if USE_LIBSSHNET
	class LibSshNetStreamNotifier : IStreamNotifier
	{
		TerminalControl terminal;

		public Stream Stream
		{ get; }

		public event EventHandler DataAvailable;

		public LibSshNetStreamNotifier(TerminalControl terminal, LibSshNetStream stream)
		{
			this.terminal = terminal;
			Stream = stream;
			stream.DataReceived += Stream_DataReceived;
		}

		private void Stream_DataReceived(object sender, EventArgs e)
		{
			terminal.BeginChange();
			if (DataAvailable != null)
				DataAvailable(this, EventArgs.Empty);
			terminal.EndChange();
		}
	}
#else
	class ShellStreamNotifier : IStreamNotifier
	{
		TerminalControl terminal;

		public Stream Stream
		{ get; }

		public event EventHandler DataAvailable;

		public ShellStreamNotifier(TerminalControl terminal, ShellStream stream)
		{
			this.terminal = terminal;
			Stream = stream;
			stream.DataReceived += Stream_DataReceived;
		}

		public void Start()
		{
			if ((Stream as ShellStream).DataAvailable && DataAvailable != null)
				Stream_DataReceived(this, null);
		}

		private void Stream_DataReceived(object sender, Renci.SshNet.Common.ShellDataEventArgs e)
		{
			terminal.BeginChange();
			if (DataAvailable != null)
				DataAvailable(this, EventArgs.Empty);
			else
				throw new InvalidOperationException("Data was received but no one was listening.");
			terminal.EndChange();
		}
	}
#endif

	class MainWindowViewModel : INotifyPropertyChanged
	{
		ConnectionSettings settings;
#if USE_LIBSSHNET
		LibSshNetStream stream;
#else
		ShellStream stream;
#endif
		public event PropertyChangedEventHandler PropertyChanged;
		public event EventHandler<ErrorEventArgs> Error;

		protected void notifyPropertyChanged([CallerMemberName] string memberName = null)
		{
			App.Current.Dispatcher.BeginInvoke(new Action(() =>
			{
				var handler = PropertyChanged;
				if (handler != null)
					handler(this, new PropertyChangedEventArgs(memberName));
			}));
		}

		string title;
		public string Title
		{
			get { return title + titlePostfix; }
			private set
			{
				title = value;
				notifyPropertyChanged();
			}
		}

		string titlePostfix = " - NiSSH";
		public string TitlePostfix
		{
			get { return titlePostfix; }
			private set
			{
				titlePostfix = value;
				notifyPropertyChanged();
				notifyPropertyChanged(nameof(Title));
			}
		}

		XtermTerminal terminal;
		public XtermTerminal Terminal
		{
			get { return terminal; }
			private set
			{
				terminal = value;
				notifyPropertyChanged();
			}
		}

		public ICommand NewSessionCommand
		{ get; }

		public ICommand ReopenSessionCommand
		{ get; }

		public ICommand TransferFilesCommand
		{ get; }

		public ICommand ExitCommand
		{ get; }

		public ICommand OptionsCommand
		{ get; }

		public MainWindowViewModel()
		{
			NewSessionCommand = new RelayCommand(onNewSession);
			ReopenSessionCommand = new RelayCommand(onReopenSession);
			TransferFilesCommand = new RelayCommand(onTransferFiles);
			ExitCommand = new RelayCommand(onExitCommand);
			OptionsCommand = new RelayCommand(onOptions);
		}

		async void onNewSession(object _)
		{
			var connection = await App.Current.AskForConnectionAsync();
			if (connection != null)
				App.Current.MakeWindowForConnection(connection);
		}

		async void onReopenSession(object _)
		{
			try
			{
				var connection = await App.Current.MakeConnectionAsync(settings, App.DefaultTerminalCols, App.DefaultTerminalRows);
				App.Current.MakeWindowForConnection(connection);
			}
			catch (ConnectException ex)
			{
				var handler = Error;
				if (handler != null)
					handler(this, new ErrorEventArgs(ex.Message));
			}
		}

		void onTransferFiles(object _)
		{

		}

		void onExitCommand(object _)
		{
			App.Current.Shutdown();
		}

		void onOptions(object _)
		{

		}

		public void Connect(IStreamNotifier notifier, ConnectionSettings settings)
		{
			this.settings = settings;
			stream = notifier.Stream as ShellStream;
			var terminal = new XtermTerminal(notifier)
			{
				Size = new Terminal.Point(App.DefaultTerminalCols, App.DefaultTerminalRows),
				DefaultFont = new TerminalFont()
				{
					Foreground = TerminalColors.GetBasicColor(7)
				}
			};
			terminal.StreamException += Terminal_StreamException;
			terminal.TitleChanged += (sender, e) =>
			{
				Title = e.Title;
			};

			Terminal = terminal;
		}

		private void Terminal_StreamException(object sender, StreamExceptionEventArgs e)
		{
			string message;
			var ex1 = (e.Exception as Renci.SshNet.Common.SshConnectionException);
			var ex2 = (e.Exception as IOException);
			var ex3 = (e.Exception as System.Net.Sockets.SocketException);
			if (ex1 != null || ex3 != null)
				message = string.Format("Connection to the server has been lost: {0}", e.Exception.Message);
			else if (ex2 != null)
				message = string.Format("An error occurred reading from the server: {0}", e.Exception.Message);
			else
				throw new Exception("An unidentified error occurred.", e.Exception);

			var handler = Error;
			if (handler != null)
				handler(this, new ErrorEventArgs(message));
		}

		public void ChangeSize(int cols, int rows)
		{
			if (cols != terminal.Size.Col || rows != terminal.Size.Row)
			{
				terminal.Size = new Terminal.Point(cols, rows);
				stream.Channel.SendWindowChangeRequest((uint) cols, (uint) rows, 0, 0);
			}
		}
	}
}
