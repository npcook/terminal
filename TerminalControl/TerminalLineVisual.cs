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
		public TerminalControl Terminal
		{ get; }

		// The line this visual represents
		public TerminalLine Line
		{ get; }

		// Should boxes be drawn around every run?
		internal bool DrawRunBoxes
		{ get; set; }

		public TerminalLineVisual(TerminalControl terminal, TerminalLine line)
		{
			this.Terminal = terminal;
			this.Line = line;

			DrawRunBoxes = false;

			line.RunsChanged += Line_RunsChanged;

			redraw();
		}

		private void Line_RunsChanged(object sender, EventArgs e)
		{
			// Redraw all this line's runs
			Action action = () => Dispatcher.Invoke(redraw);
			if (Terminal.DeferChanges)
				Terminal.AddDeferChangesCallback(this, action);
			else
				action();
		}

		private void redraw()
		{
			var context = RenderOpen();

			var textDecorations = new TextDecorationCollection();

			int index = 0;
			var drawPoint = new System.Windows.Point(0, 0);
			foreach (var run in Line.Runs)
			{
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
				if (DrawRunBoxes)
					border = new Pen(DebugColors.GetBrush(index), 1);
				context.DrawRoundedRectangle(background, border, new Rect(drawPoint, new Vector(ft.WidthIncludingTrailingWhitespace - 1, ft.Height)), 1, 1);

				context.DrawText(ft, drawPoint);
				drawPoint.X += ft.WidthIncludingTrailingWhitespace;

				textDecorations.Clear();

				index++;
			}

			context.Close();
		}
	}
}
