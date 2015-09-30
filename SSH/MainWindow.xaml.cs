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

	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private HwndSource hwndSource;

#if USE_LIBSSHNET
		LibSshNetStream stream;
#else
		ShellStream stream;
#endif
		ITerminalHandler handler;

		public void Connect(ShellStream stream)
		{
			this.stream = stream;
			var notifier = new ShellStreamNotifier(terminalControl, stream);
            terminalControl.Terminal = new XtermTerminal(notifier)
			{
				Size = new Terminal.Point(App.DefaultTerminalCols, App.DefaultTerminalRows),
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

			notifier.Start();
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
			IsVisibleChanged -= this_IsVisibleChanged;

			hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
			hwndSource.AddHook(HwndHook);

			resizeTerminal(App.DefaultTerminalCols, App.DefaultTerminalRows);
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
			}
		}
	}
}
