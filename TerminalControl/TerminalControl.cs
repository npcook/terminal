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
			impl.SizeChanged += Impl_SizeChanged;

			var familyBinding = new Binding("FontFamily");
			familyBinding.Source = this;
			impl.SetBinding(TerminalControlCore.FontFamilyProperty, familyBinding);

			var sizeBinding = new Binding("FontSize");
			sizeBinding.Source = this;
			impl.SetBinding(TerminalControlCore.FontSizeProperty, sizeBinding);
		}

		private void Impl_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (scrollViewer != null)
			{
				bool atEnd = scrollViewer.VerticalOffset == scrollViewer.ScrollableHeight;
				if (atEnd)
					scrollViewer.ScrollToBottom();
            }
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			if (scrollViewer != null)
				scrollViewer.Content = null;

			if (Template == null)
				scrollViewer = null;
			else
			{
				scrollViewer = Template.FindName("PART_ScrollViewer", this) as ScrollViewer;
				if (scrollViewer != null)
					scrollViewer.Content = impl;
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
			bool handled = true;

			var encoding = Encoding.ASCII;
			byte[] bytesToWrite = null;

			// Convert to char array because BinaryWriter sends strings prefixed with their
			// length
			if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Control))
			{
				if (e.Key >= Key.A && e.Key <= Key.Z)
					bytesToWrite = new byte[] { (byte) (e.Key - Key.A + 1) };
				else if (e.Key == Key.OemOpenBrackets)
					bytesToWrite = new byte[] { 27 };
				else if (e.Key == Key.Oem5)
					bytesToWrite = new byte[] { 28 };
				else if (e.Key == Key.OemCloseBrackets)
					bytesToWrite = new byte[] { 29 };
				else if (e.Key == Key.D6 && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
					bytesToWrite = new byte[] { 30 };
				else if (e.Key == Key.OemMinus && e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
					bytesToWrite = new byte[] { 31 };
			}
			else if (!Terminal.AppCursorKeys)
			{
				if (e.Key == Key.Tab)
					bytesToWrite = encoding.GetBytes("\t");
				else if (e.Key == Key.Left)
					bytesToWrite = encoding.GetBytes("\x1b[D");
				else if (e.Key == Key.Right)
					bytesToWrite = encoding.GetBytes("\x1b[C");
				else if (e.Key == Key.Up)
					bytesToWrite = encoding.GetBytes("\x1b[A");
				else if (e.Key == Key.Down)
					bytesToWrite = encoding.GetBytes("\x1b[B");
				else if (e.Key == Key.Delete)
					bytesToWrite = encoding.GetBytes("\x1b[3~");
				else if (e.Key == Key.Home)
					bytesToWrite = encoding.GetBytes("\x1b[H");
				else if (e.Key == Key.End)
					bytesToWrite = encoding.GetBytes("\x1b[F");
				else
					handled = false;
			}
			else if (Terminal.AppCursorKeys)
			{
				if (e.Key == Key.Tab)
					bytesToWrite = encoding.GetBytes("\t");
				else if (e.Key == Key.Left)
					bytesToWrite = encoding.GetBytes("\x1bOD");
				else if (e.Key == Key.Right)
					bytesToWrite = encoding.GetBytes("\x1bOC");
				else if (e.Key == Key.Up)
					bytesToWrite = encoding.GetBytes("\x1bOA");
				else if (e.Key == Key.Down)
					bytesToWrite = encoding.GetBytes("\x1bOB");
				else if (e.Key == Key.Delete)
					bytesToWrite = encoding.GetBytes("\x1b[3~");
				else if (e.Key == Key.Home)
					bytesToWrite = encoding.GetBytes("\x1bOH");
				else if (e.Key == Key.End)
					bytesToWrite = encoding.GetBytes("\x1bOF");
				else
					handled = false;
			}
			else
				handled = false;

			if (bytesToWrite != null)
			{
				Terminal.SendBytes(bytesToWrite);
			}
			e.Handled = handled;
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

		public void AddMessage(string text, TerminalFont font)
		{
			impl.AddMessage(text, font);
		}
	}
}
