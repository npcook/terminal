using npcook.Terminal;
using npcook.Terminal.Controls;
using Renci.SshNet;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
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

	interface IWindowStreamNotifier : IStreamNotifier
	{
		void Start();
		void Stop();
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
	class ShellStreamNotifier : IWindowStreamNotifier
	{
		readonly TerminalControl terminal;
		readonly ShellStream stream;
		readonly SemaphoreSlim received = new SemaphoreSlim(0);
		bool stopped;

		public Stream Stream
		{ get { return stream; } }

		public event EventHandler<DataAvailableEventArgs> DataAvailable;

		public ShellStreamNotifier(TerminalControl terminal, ShellStream stream)
		{
			this.terminal = terminal;
			this.stream = stream;
		}

		public void Start()
		{
			stopped = false;
			stream.DataReceived += Stream_DataReceived;
			Task.Run(() =>
			{
				while (true)
				{
					received.Wait();
					if (stopped)
						break;

					while (stream.DataAvailable)
					{
						terminal.BeginChange();
						if (DataAvailable != null)
						{
							bool done = false;
							Task.Run(() =>
							{
								while (!done)
								{
									Thread.Sleep(50);
									if (done)
										break;
									terminal.CycleChange();
								}
							});
							DataAvailable(this, new DataAvailableEventArgs(0));
							done = true;
						}
						else
							throw new InvalidOperationException("Data was received but no one was listening.");
						terminal.EndChange();
					}
				}
			});
			received.Release();
		}

		public void Stop()
		{
			stopped = true;
			stream.Close();
			received.Release();
		}

		private void Stream_DataReceived(object sender, Renci.SshNet.Common.ShellDataEventArgs e)
		{
			received.Release();
		}
	}

	class SavingShellStreamNotifier : IWindowStreamNotifier
	{
		readonly TerminalControl terminal;
		readonly ShellStream input;
		readonly Stream output;
		readonly ProxyStream middle;
		readonly ConcurrentQueue<byte[]> outputQueue = new ConcurrentQueue<byte[]>();
		readonly EventWaitHandle outputWait = new EventWaitHandle(false, EventResetMode.AutoReset);
		readonly Task outputTask;

		public Stream Stream
		{ get { return middle; } }

		public event EventHandler<DataAvailableEventArgs> DataAvailable;

		public SavingShellStreamNotifier(TerminalControl terminal, ShellStream stream, Stream output)
		{
			this.terminal = terminal;
			input = stream;
			this.output = output;
			middle = new ProxyStream(input);
			stream.DataReceived += Stream_DataReceived;
			outputTask = Task.Run(() =>
			{
				do
				{
					outputWait.WaitOne();
					if (outputQueue.IsEmpty)
						break;

					byte[] result;
					while (!outputQueue.TryDequeue(out result)) ;

					output.Write(result, 0, result.Length);
				} while (true);
			});
		}

		public void Start()
		{
			if (input.DataAvailable && DataAvailable != null)
				Stream_DataReceived(this, null);
		}

		public void Stop()
		{
			output.Close();
		}

		private void Stream_DataReceived(object sender, Renci.SshNet.Common.ShellDataEventArgs e)
		{
			terminal.BeginChange();
			if (DataAvailable != null)
			{
				DataAvailable(this, new DataAvailableEventArgs(e.Data.Length));
				outputQueue.Enqueue(middle.GetCopy());
				outputWait.Set();
			}
			else
				throw new InvalidOperationException("Data was received but no one was listening.");
			terminal.EndChange();
		}
	}

	class LoadingShellStreamNotifier : IWindowStreamNotifier
	{
		TerminalControl terminal;
		
		public Stream Stream
		{ get; }

		public event EventHandler<DataAvailableEventArgs> DataAvailable;

		public LoadingShellStreamNotifier(TerminalControl terminal, Stream input)
		{
			this.terminal = terminal;
			Stream = new ProxyStream(input, false, true);
		}

		public void Start()
		{
			if (DataAvailable != null)
			{
				Stream_DataReceived(this, null);
			}
		}

		public void Stop()
		{
			Stream.Close();
		}

		private void Stream_DataReceived(object sender, Renci.SshNet.Common.ShellDataEventArgs e)
		{
			terminal.BeginChange();
			if (DataAvailable != null)
				DataAvailable(this, new DataAvailableEventArgs(e.Data.Length));
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
				PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(memberName));
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
				Error?.Invoke(this, new ErrorEventArgs(ex.Message));
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

		public void Connect(IStreamNotifier notifier, ShellStream stream, ConnectionSettings settings)
		{
			this.settings = settings;
			this.stream = stream;
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

			Error?.Invoke(this, new ErrorEventArgs(message));
		}

		public void ChangeSize(int cols, int rows)
		{
			if (cols != terminal.Size.Col || rows != terminal.Size.Row)
			{
				terminal.Size = new Terminal.Point(cols, rows);
				//stream.Channel.SendWindowChangeRequest((uint) cols, (uint) rows, 0, 0);
			}
		}
	}
}
