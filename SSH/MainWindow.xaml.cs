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
		MainWindowViewModel viewModel;
		IWindowStreamNotifier notifier;

		bool initialSized = false;
		public MainWindow()
		{
			InitializeComponent();

			viewModel = new MainWindowViewModel();
			DataContext = viewModel;

			viewModel.Error += ViewModel_Error;

			IsVisibleChanged += this_IsVisibleChanged;
			SizeChanged += this_SizeChanged;

			Loaded += this_Loaded;
		}

		private void ViewModel_Error(object sender, ErrorEventArgs e)
		{
			MessageBox.Show(this, e.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
		}

		public void Connect(ShellStream stream, ConnectionSettings settings)
		{
			notifier?.Stop();
			notifier = new ShellStreamNotifier(terminalControl, stream);
			//notifier = new LoadingShellStreamNotifier(terminalControl, File.OpenRead("input.log"));
			//notifier = new SavingShellStreamNotifier(terminalControl, stream, File.OpenWrite("input.log"));
			viewModel.Connect(notifier, stream, settings);
			notifier.Start();

			terminalControl.Terminal = viewModel.Terminal;
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);

			notifier?.Stop();
		}

		private void this_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
		{
			IsVisibleChanged -= this_IsVisibleChanged;

			resizeTerminal(App.DefaultTerminalCols, App.DefaultTerminalRows);
		}

		private void this_SizeChanged(object sender, System.Windows.SizeChangedEventArgs e)
		{
			if (!initialSized)
			{
				initialSized = true;
				return;
			}

			terminalControl.Width = double.NaN;
			terminalControl.Height = double.NaN;

			IntPtr hwnd = new WindowInteropHelper(this).Handle;

			var transformer = PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice;
			var stops = transformer.Transform(new System.Windows.Point(terminalControl.CharWidth, terminalControl.CharHeight));
			double horizontalStop = stops.X;
			double verticalStop = stops.Y;

			var terminalOffset = terminalControl.TransformToAncestor(root).Transform(new System.Windows.Point(0, 0));

			NativeMethods.RECT windowRect;
			NativeMethods.GetWindowRect(hwnd, out windowRect);

			NativeMethods.RECT clientRect;
			NativeMethods.GetClientRect(hwnd, out clientRect);

			int newCols = (int) ((clientRect.right - terminalOffset.X - SystemParameters.ScrollWidth + 1f) / horizontalStop);
			int newRows = (int) ((clientRect.bottom - terminalOffset.Y + 1f) / verticalStop);

			viewModel.ChangeSize(newCols, newRows);
		}

		private void this_Loaded(object sender, RoutedEventArgs e)
		{
			var content = root;
			
			InvalidateMeasure();
			var contentDesired = terminalControl.TerminalSize;

			Width = contentDesired.Width + ActualWidth - content.ActualWidth;
			Height = contentDesired.Height + ActualHeight - content.ActualHeight;
		}

		private void resizeTerminal(int cols, int rows)
		{
			terminalControl.Width = cols * terminalControl.CharWidth + SystemParameters.ScrollWidth;
			terminalControl.Height = rows * terminalControl.CharHeight;
		}

		void OnKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
		{
			if (e.NewFocus == this)
			{
				terminalControl.Focus();
				Keyboard.Focus(terminalControl);
			}
		}
	}
}
