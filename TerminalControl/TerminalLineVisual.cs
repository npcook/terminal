using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using npcook.Terminal;

namespace npcook.Terminal.Controls
{
	class TerminalLineVisual : DrawingVisual
	{
		// The <c>TerminalControl</c> this visual is a child of	
		public TerminalControlCore Terminal
		{ get; }

		// The line this visual represents
		TerminalLine line;
		public TerminalLine Line
		{
			get { return line; }
			set
			{
				Dispatcher.VerifyAccess();

				if (line == value)
					return;
				bool wasNull = line == null;
				if (line != null)
					line.RunsChanged -= Line_RunsChanged;
				line = value;
				if (line != null)
				{
					line.RunsChanged += Line_RunsChanged;

					Terminal.RemoveDeferChangesCallback(this);
					redraw();
				}
			}
		}

		public int SelectionStart
		{ get; private set; }

		public int SelectionEnd
		{ get; private set; }

		public void Select(int start, int end)
		{
			Dispatcher.VerifyAccess();

			if (SelectionStart == start && SelectionEnd == end)
				return;
			SelectionStart = start;
			SelectionEnd = end;

			scheduleRedraw();
		}

		public TerminalLineVisual(TerminalControlCore terminal, TerminalLine line)
		{
			this.Terminal = terminal;
			this.Line = line;
		}

		protected override void OnVisualParentChanged(DependencyObject oldParent)
		{
			base.OnVisualParentChanged(oldParent);

			if (Line != null)
			{
				if (oldParent != null)
					Line.RunsChanged -= Line_RunsChanged;
				if (VisualParent != null)
				{
					Line.RunsChanged += Line_RunsChanged;

					scheduleRedraw();
				}
			}
		}

		private void Line_RunsChanged(object sender, EventArgs e)
		{
			scheduleRedraw();
		}

		void scheduleRedraw()
		{
			Action action = () => Dispatcher.Invoke(redraw);
			Terminal.AddDeferChangesCallback(this, action);
		}

		private void redraw()
		{
			Dispatcher.VerifyAccess();

			var context = RenderOpen();

			var textDecorations = new TextDecorationCollection();

			var drawPoint = new System.Windows.Point(0, 0);
			int index = 0;
			foreach (var run in line.Runs)
			{
				if (run.Font.Hidden && Line.Runs[Line.Runs.Count - 1] == run)
					break;

				SolidColorBrush foreground;
				SolidColorBrush background;
				if (run.Font.Inverse)
				{
					foreground = Terminal.GetFontBackgroundBrush(run.Font);
					background = Terminal.GetFontForegroundBrush(run.Font);
				}
				else
				{
					foreground = Terminal.GetFontForegroundBrush(run.Font);
					background = Terminal.GetFontBackgroundBrush(run.Font);
				}

				var ft = new FormattedText(
					run.Text,
					System.Globalization.CultureInfo.CurrentUICulture,
					Terminal.FlowDirection,
					Terminal.GetFontTypeface(run.Font),
					Terminal.FontSize,
					foreground,
					new NumberSubstitution(),
					TextFormattingMode.Ideal
					);

				if (run.Font.Underline)
					textDecorations.Add(TextDecorations.Underline);
				if (run.Font.Strike)
					textDecorations.Add(TextDecorations.Strikethrough);

				if (textDecorations.Count > 0)
					ft.SetTextDecorations(textDecorations);

				Pen border = null;
				if (Terminal.DrawRunBoxes)
					border = new Pen(DebugColors.GetBrush(index), 1);

				var backgroundTopLeft = new System.Windows.Point(Math.Floor(drawPoint.X), Math.Floor(drawPoint.Y));
				var backgroundSize = new Vector(Math.Ceiling(ft.WidthIncludingTrailingWhitespace), Math.Ceiling(ft.Height));
				context.DrawRectangle(background, border, new Rect(backgroundTopLeft, backgroundSize));

				context.DrawText(ft, drawPoint);
				drawPoint.X += ft.WidthIncludingTrailingWhitespace;

				textDecorations.Clear();

				index++;
			}

			if (SelectionStart != SelectionEnd)
			{
				var selectRect = new Rect(
					new System.Windows.Point(Math.Floor(Math.Min(drawPoint.X, Terminal.CharWidth * SelectionStart)), 0.0),
					new System.Windows.Point(Math.Ceiling(Math.Min(drawPoint.X, Terminal.CharWidth * SelectionEnd)), Math.Ceiling(Terminal.CharHeight)));

				var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 90, 180, 230));
				context.DrawRectangle(brush, null, selectRect);
			}

			context.Close();

			Opacity = 1.0;
		}
	}
}
