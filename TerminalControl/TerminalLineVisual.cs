using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

using Terminal;

namespace TerminalControls
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
				var ft = new FormattedText(
					run.Text,
					System.Globalization.CultureInfo.CurrentUICulture,
					Terminal.FlowDirection,
					Terminal.GetFontTypeface(run.Font),
					Terminal.FontSize,
					Terminal.GetFontForegoundBrush(run.Font),
					new NumberSubstitution(),
					TextFormattingMode.Ideal
					);

				if (run.Font.Underline)
					textDecorations.Add(TextDecorations.Underline);
				if (run.Font.Strike)
					textDecorations.Add(TextDecorations.Strikethrough);

				if (textDecorations.Count > 0)
					ft.SetTextDecorations(textDecorations);

				context.DrawText(ft, drawPoint);

				if (DrawRunBoxes)
				{
					SolidColorBrush backgroundBrush = DebugColors.GetBrush(index);
					context.DrawRoundedRectangle(null, new Pen(backgroundBrush, 1), new Rect(drawPoint, new Vector(ft.Width - 1, ft.Height)), 1, 1);
				}
				drawPoint.X += ft.WidthIncludingTrailingWhitespace;

				textDecorations.Clear();

				index++;
			}

			context.Close();
		}
	}
}
