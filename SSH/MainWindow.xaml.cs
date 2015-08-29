using System;
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
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Renci.SshNet;
using System.IO;
using System.Threading;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using npcook.Terminal;
using npcook.Terminal.Controls;

namespace npcook.Ssh
{
	public abstract class Authentication
	{
		internal Authentication()
		{ }
	}

	public sealed class PasswordAuthentication : Authentication
	{
		public string Password
		{ get; }

		public PasswordAuthentication(string password)
		{
			Password = password;
		}
	}

	public sealed class KeyAuthentication : Authentication
	{
		public Stream Key
		{ get; }

		public string Passphrase
		{ get; }

		public KeyAuthentication(Stream key, string passphrase)
		{
			Key = key;
			Passphrase = passphrase;
		}
	}

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

		private void Stream_DataReceived(object sender, Renci.SshNet.Common.ShellDataEventArgs e)
		{
			terminal.BeginChange();
			if (DataAvailable != null)
				DataAvailable(this, EventArgs.Empty);
			terminal.EndChange();
		}
	}

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		const int DefaultTerminalCols = 160;
		const int DefaultTerminalRows = 40;

		private HwndSource hwndSource;

#if USE_LIBSSHNET
		libsshnetStream stream;
#else
		SshClient client;
		ShellStream stream;
#endif
		ITerminalHandler handler;

		public void Connect(string serverAddress, int serverPort, string username, IEnumerable<Authentication> authentications)
		{
			var dataThread = new Thread(() =>
			{
				var settings = File.ReadAllLines(@"X:\settings.txt");
#if USE_LIBSSHNET
				var connection = new libsshnetConnection(settings[0], settings[1], settings[2]);
				stream = connection.GetStream();
#else
				ConnectionInfo connectionInfo = null;
				foreach (var auth in authentications)
				{
					if (auth is PasswordAuthentication)
						connectionInfo = new PasswordConnectionInfo(serverAddress, serverPort, username, (auth as PasswordAuthentication).Password);
					else if (auth is KeyAuthentication)
					{
						var privateKeyFile = new PrivateKeyFile((auth as KeyAuthentication).Key, (auth as KeyAuthentication).Passphrase);
						connectionInfo = new PrivateKeyConnectionInfo(serverAddress, serverPort, username, privateKeyFile);
					}
				}

				client = new SshClient(connectionInfo);
				client.Connect();

				client.KeepAliveInterval = TimeSpan.FromSeconds(20);

				stream = client.CreateShellStream("xterm-256color", (uint) DefaultTerminalCols, (uint) DefaultTerminalRows, 0, 0, 1000);

				Dispatcher.Invoke(() =>
				{
					terminalControl.Terminal = new XtermTerminal(new ShellStreamNotifier(terminalControl, stream))
					{
						Size = new Terminal.Point(DefaultTerminalCols, DefaultTerminalRows),
						DefaultFont = new TerminalFont()
						{
							Foreground = TerminalColors.GetBasicColor(7)
						}
					};
					handler = terminalControl.Terminal;
					terminalControl.Terminal.StreamException += Terminal_StreamException;
					terminalControl.Terminal.TitleChanged += (sender, e) =>
					{
						Dispatcher.Invoke(() => Title = e.Title);
					};

					Terminal_SizeChanged(this, EventArgs.Empty);
				});
#endif
			});
			dataThread.Name = "Data Thread";
			dataThread.IsBackground = true;
			dataThread.Start();
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
			terminalControl.AddMessage("", new TerminalFont());
			terminalControl.AddMessage(message, new TerminalFont() { Foreground = Terminal.Color.FromRgb(255, 0, 0), Background = Terminal.Color.FromRgb(0, 0, 0) } );
			terminalControl.AddMessage("", new TerminalFont());
			MessageBox.Show(this, message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		}

		public MainWindow()
		{
			InitializeComponent();

			IsVisibleChanged += this_IsVisibleChanged;
		}

		private void this_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
			hwndSource.AddHook(HwndHook);

			resizeTerminal(DefaultTerminalCols, DefaultTerminalRows);

			IsVisibleChanged -= this_IsVisibleChanged;
		}

		private void resizeTerminal(int cols, int rows)
		{
			terminalControl.Width = cols * terminalControl.CharWidth + SystemParameters.ScrollWidth;
			terminalControl.Height = rows * terminalControl.CharHeight;
		}

		private void Terminal_SizeChanged(object sender, EventArgs e)
		{
			resizeTerminal(terminalControl.Terminal.Size.Col, terminalControl.Terminal.Size.Row);
		}

		[StructLayout(LayoutKind.Sequential)]
		struct RECT
		{
			public int left;
			public int top;
			public int right;
			public int bottom;
		}

		private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
		{
			const int WM_SIZING = 0x0214;

			if (msg == WM_SIZING)
			{
				var transformer = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
                var stops = transformer.Transform(new System.Windows.Point(terminalControl.CharWidth, terminalControl.CharHeight));
				double horizontalStop = stops.X;
				double verticalStop = stops.Y;

				RECT rect = (RECT) Marshal.PtrToStructure(lParam, typeof(RECT));

				rect.right = rect.left + (int) (Math.Floor((rect.right - rect.left + 1) / horizontalStop) * horizontalStop);
				rect.bottom = rect.top + (int) (Math.Floor((rect.bottom - rect.top + 1) / verticalStop) * verticalStop);

				Marshal.StructureToPtr(rect, lParam, false);

				handled = true;

				return (IntPtr) 1;
			}

			return IntPtr.Zero;
		}

		void OnKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
		{
			terminalControl.Focus();
			Keyboard.Focus(terminalControl);
		}

		private void connect_click(object sender, RoutedEventArgs e)
		{
			var dialog = new ConnectionDialog();
			if (dialog.ShowDialog().GetValueOrDefault(false))
			{
				Connect(null, 0, null, null);
			}
		}
	}
}
