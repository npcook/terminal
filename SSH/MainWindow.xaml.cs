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
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window
	{
		private HwndSource hwndSource;
		
		ShellStream stream;
		BinaryWriter writer;
		ITerminalHandler handler;
		public MainWindow()
		{
			InitializeComponent();

			IsVisibleChanged += (sender, e) =>
			{
				if (hwndSource == null)
				{
					hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
					hwndSource.AddHook(HwndHook);
				}
			};

			var terminal = new Terminal.TerminalBase();
			terminal.Size = new Terminal.Point(160, 40);
			handler = new XtermTerminalHandler(terminal);
			handler.DefaultFont = new TerminalFont()
			{
				Foreground = TerminalColors.GetBasicColor(7)
			};
			terminal.CurrentFont = handler.DefaultFont;

			terminalControl.Terminal = terminal;
			
			var dataThread = new Thread(() =>
			{
				var settings = File.ReadAllLines(@"X:\settings.txt");
				var connectionInfo = new PrivateKeyConnectionInfo(settings[0], settings[1], new PrivateKeyFile(settings[2], settings[3]));
				SshClient client = new SshClient(connectionInfo);
				client.Connect();
				
				stream = client.CreateShellStream("xterm", (uint) terminal.Size.Col, (uint) terminal.Size.Row, 0, 0, 1000);
				writer = new BinaryWriter(stream, Encoding.UTF8);
				var reader = new StreamReader(stream, Encoding.UTF8, false, 2048, true);

				stream.DataReceived += (sender, e) =>
				{
					try
					{
						terminalControl.BeginChange();
						handler.HandleInput(reader);
						terminalControl.EndChange();
					}
					catch (Exception)
					{
						System.Diagnostics.Debugger.Break();
					}
				};
			});
			dataThread.Name = "Data Thread";
			dataThread.IsBackground = true;
			dataThread.Start();

			(handler as XtermTerminalHandler).TitleChanged += (sender, e) =>
			{
				Dispatcher.Invoke(() => Title = e.Title);
			};

			terminalControl.KeyDown += (sender, e) =>
			{
				Dispatcher.Invoke(() =>
				{
					// Convert to char array because BinaryWriter sends strings prefixed with their
					// length
					if (e.Key == Key.Left)
						writer.Write("\x1b[D".ToCharArray());
					else if (e.Key == Key.Right)
						writer.Write("\x1b[C".ToCharArray());
					else if (e.Key == Key.Up)
						writer.Write("\x1b[A".ToCharArray());
					else if (e.Key == Key.Down)
						writer.Write("\x1b[B".ToCharArray());
					else if (e.Key == Key.Delete)
						writer.Write("\x1b[3~".ToCharArray());
					else if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
					{
						if (e.Key >= Key.A && e.Key <= Key.Z)
							writer.Write((byte) (e.Key - Key.A + 1));
						else if (e.Key == Key.OemOpenBrackets)
							writer.Write((byte) 27);
						else if (e.Key == Key.Oem5)
							writer.Write((byte) 28);
						else if (e.Key == Key.OemCloseBrackets)
							writer.Write((byte) 29);
						else if (e.Key == Key.D6 && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
							writer.Write((byte) 30);
						else if (e.Key == Key.OemMinus && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
							writer.Write((byte) 31);
					}
					writer.Flush();
				});
			};

			terminalControl.TextInput += (sender, e) =>
			{
				Dispatcher.Invoke(() =>
				{
					foreach (char c in e.Text)
					{
						if (c == 4)
							System.Diagnostics.Debugger.Break();
						if (!char.IsControl(c) || c == 8 || c == 13)
							writer.Write(c);
						else
							System.Diagnostics.Debugger.Break();
					}
					writer.Flush();
				});
			};
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
				var stops = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.Transform(new System.Windows.Point(terminalControl.CharWidth, terminalControl.CharHeight));
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
		}
	}
}
