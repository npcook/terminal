using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Timers;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using npcook.Terminal;
using Color = npcook.Terminal.Color;

namespace npcook.Terminal.Controls
{
	/// <summary>
	/// Provides a visual representation of a <c>Terminal.Terminal</c>
	/// </summary>
	[TemplatePart(Name = "PART_ScrollViewer", Type = typeof(ScrollViewer))]
	public class TerminalControl : Control
	{
		ScrollViewer scrollViewer = null;
		readonly TerminalControlCore impl = new TerminalControlCore();

		public TerminalControl()
		{
			var familyBinding = new Binding("FontFamily");
			familyBinding.Source = this;
			impl.SetBinding(TerminalControlCore.FontFamilyProperty, familyBinding);

			var sizeBinding = new Binding("FontSize");
			sizeBinding.Source = this;
			impl.SetBinding(TerminalControlCore.FontSizeProperty, sizeBinding);
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			if (scrollViewer != null)
			{
				scrollViewer.Content = null;
			}

			if (Template == null)
				scrollViewer = null;
			else
			{
				scrollViewer = Template.FindName("PART_ScrollViewer", this) as ScrollViewer;
				if (scrollViewer != null)
				{
					scrollViewer.Content = impl;
					scrollViewer.CanContentScroll = true;
				}
				else
					throw new InvalidCastException("PART_ScrollViewer must be a ScrollViewer");
			}
		}

		static TerminalControl()
		{
			DefaultStyleKeyProperty.OverrideMetadata(typeof(TerminalControl), new FrameworkPropertyMetadata(typeof(TerminalControl)));
		}

		public XtermTerminal Terminal
		{
			get { return impl.Terminal; }
			set { impl.Terminal = value; }
		}

		public bool BlinkCursor
		{
			get { return impl.BlinkCursor; }
			set { impl.BlinkCursor = value; }
		}

		public double CharWidth
		{ get { return impl.CharWidth; } }

		public double CharHeight
		{ get { return impl.CharHeight; } }

		public Size TerminalSize
		{ get; private set; }

		bool sendKey(Key key, ModifierKeys modifiers)
		{
			var encoding = Encoding.ASCII;
			byte[] bytesToWrite = null;

			// Convert to char array because BinaryWriter sends strings prefixed with their
			// length
			if (modifiers.HasFlag(ModifierKeys.Control))
			{
				if (key >= Key.A && key <= Key.Z)
					bytesToWrite = new byte[] { (byte) (key - Key.A + 1) };
				else
				{
					switch (key)
					{
						case Key.OemOpenBrackets:
							bytesToWrite = new byte[] { 27 }; break;
						case Key.Oem5:
							bytesToWrite = new byte[] { 28 }; break;
						case Key.OemCloseBrackets:
							bytesToWrite = new byte[] { 29 }; break;
					}

					if (modifiers.HasFlag(ModifierKeys.Shift))
					{
						switch (key)
						{
							case Key.D6:
								bytesToWrite = new byte[] { 30 }; break;
							case Key.OemMinus:
								bytesToWrite = new byte[] { 31 }; break;
						}
					}
				}
			}
			else
			{
				string output = null;
				switch (key)
				{
					case Key.Tab:
						output = "\t"; break;
					case Key.Home:
						output = "\x1b[1~"; break;
					case Key.Insert:
						output = "\x1b[2~"; break;
					case Key.Delete:
						output = "\x1b[3~"; break;
					case Key.End:
						output = "\x1b[4~"; break;
					case Key.PageUp:
						output = "\x1b[5~"; break;
					case Key.PageDown:
						output = "\x1b[6~"; break;
					case Key.F1:
						output = "\x1bOP"; break;
					case Key.F2:
						output = "\x1bOQ"; break;
					case Key.F3:
						output = "\x1bOR"; break;
					case Key.F4:
						output = "\x1bOS"; break;
					case Key.F5:
						output = "\x1b[15~"; break;
					case Key.F6:
						output = "\x1b[17~"; break;
					case Key.F7:
						output = "\x1b[18~"; break;
					case Key.F8:
						output = "\x1b[19~"; break;
					case Key.F9:
						output = "\x1b[20~"; break;
					case Key.F10:
						output = "\x1b[21~"; break;
					case Key.F11:
						output = "\x1b[23~"; break;
					case Key.F12:
						output = "\x1b[24~"; break;
				}

				if (output != null)
					bytesToWrite = encoding.GetBytes(output);
			}

			if (bytesToWrite == null)
			{
				string output = null;
				switch (key)
				{
					case Key.Left:
						output = "\x1b[D"; break;
					case Key.Right:
						output = "\x1b[C"; break;
					case Key.Up:
						output = "\x1b[A"; break;
					case Key.Down:
						output = "\x1b[B"; break;
				}

				if (output != null)
				{
					bytesToWrite = encoding.GetBytes(output);
					if (Terminal.AppCursorKeys)
						bytesToWrite[1] = encoding.GetBytes("O")[0];
				}
			}

			if (bytesToWrite != null)
			{
				Terminal.SendBytes(bytesToWrite);
				return true;
			}
			return false;
		}

		protected override void OnPreviewMouseWheel(MouseWheelEventArgs e)
		{
			base.OnPreviewMouseWheel(e);

			if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt) && Terminal.CurrentScreen == Terminal.AltScreen)
			{
				int lines = e.Delta / Mouse.MouseWheelDeltaForOneLine;
				for (int i = 0; i < lines; ++i)
					sendKey(Key.Up, ModifierKeys.None);

				for (int i = 0; i > lines; --i)
					sendKey(Key.Down, ModifierKeys.None);

				e.Handled = true;
			}
		}

		protected override void OnGotKeyboardFocus(KeyboardFocusChangedEventArgs e)
		{
			base.OnGotKeyboardFocus(e);
			
			impl.EnableCaret = true;
			e.Handled = true;
		}

		protected override void OnLostKeyboardFocus(KeyboardFocusChangedEventArgs e)
		{
			base.OnLostKeyboardFocus(e);
			
			impl.EnableCaret = false;
			e.Handled = true;
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.F12 && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
				impl.DrawRunBoxes = !impl.DrawRunBoxes;

			e.Handled = sendKey(e.Key, e.KeyboardDevice.Modifiers);

			scrollViewer.ScrollToBottom();
		}

		protected override void OnPreviewTextInput(TextCompositionEventArgs e)
		{
			foreach (char c in e.Text)
			{
				if (c == 4)
					System.Diagnostics.Debugger.Break();
				if (!char.IsControl(c) || c == 27 || c == 8 || c == 13)
				{
					Terminal.SendChar(c);
				}
				else
					System.Diagnostics.Debugger.Break();
			}

			e.Handled = true;
		}

		// Begin a group of changes
		public void BeginChange()
		{
			impl.BeginChange();
		}

		// End a group of changes and update all visuals in one swoop
		public void EndChange()
		{
			impl.EndChange();
		}

		public void CycleChange()
		{
			impl.CycleChange();
		}

		public void AddMessage(string text, TerminalFont font)
		{
			impl.AddMessage(text, font);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			scrollViewer.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			TerminalSize = scrollViewer.DesiredSize;
			return scrollViewer.DesiredSize;
		}
	}
}
