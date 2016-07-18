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
		struct VisualRun
		{
			public TerminalRun Run;
			public FormattedText Text;
		}

		// The <c>TerminalControl</c> this visual is a child of	
		public TerminalControlCore Terminal
		{ get; }

		List<VisualRun> drawnRuns = new List<VisualRun>();
		bool selectionChanged = false;

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

					clearDraw();
					scheduleRedraw();
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

			selectionChanged = true;
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

		TerminalRun[] CopyLine(TerminalLine line)
		{
			var runs = line.Runs;
			var result = new TerminalRun[runs.Count];
			for (int i = 0; i < runs.Count; ++i)
			{
				var run = runs[i];
				result[i] = new TerminalRun(run.Text, run.Font);
			}
			return result;
		}

		void scheduleRedraw()
		{
			var copiedLine = CopyLine(Line);
			Action action = () => Dispatcher.InvokeAsync(() => redraw(copiedLine));
			Terminal.AddDeferChangesCallback(this, action);
		}

		private void clearDraw()
		{
			var context = RenderOpen();
			context.Close();
		}

		private void redraw(TerminalRun[] runs)
		{
			Dispatcher.VerifyAccess();

			//drawnRuns = new List<VisualRun>(runs.Length);
			//for (int i = 0; i < runs.Length; ++i)
			//{
			//	drawnRuns.Add(new VisualRun() { Run = runs[i], Text = null });
			//}
			bool changed = false;
			for (int i = 0; i < Math.Min(drawnRuns.Count, runs.Length); ++i)
			{
				if (drawnRuns[i].Run.Text != runs[i].Text || drawnRuns[i].Run.Font != runs[i].Font)
				{
					drawnRuns[i] = new VisualRun() { Run = runs[i], Text = null };
					changed = true;
				}
			}

			if (drawnRuns.Count > runs.Length)
				drawnRuns.RemoveRange(runs.Length, drawnRuns.Count - runs.Length);
			else if (drawnRuns.Count < runs.Length)
				drawnRuns.AddRange(runs.Skip(drawnRuns.Count).Select(x => new VisualRun() { Run = x }));
			else if (!changed && !selectionChanged)
				return;
			selectionChanged = false;

			for (int i = 0; i < drawnRuns.Count; ++i)
			{
				var run = drawnRuns[i].Run;
				if (drawnRuns[i].Text != null)
					continue;

				SolidColorBrush foreground = Terminal.GetFontForegroundBrush(run.Font);

				// Format the text for this run.  However, this is drawn NEXT run.  The
				// background is drawn one run ahead of the text so the text doesn't get
				// clipped.
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

				if (run.Font.Underline || run.Font.Strike)
				{
					var textDecorations = new TextDecorationCollection(2);

					if (run.Font.Underline)
						textDecorations.Add(TextDecorations.Underline);
					if (run.Font.Strike)
						textDecorations.Add(TextDecorations.Strikethrough);

					ft.SetTextDecorations(textDecorations);
				}

				drawnRuns[i] = new VisualRun() { Run = drawnRuns[i].Run, Text = ft };
			}

			var context = RenderOpen();
			try
			{
				var drawPoint = new System.Windows.Point(0, 0);
				int index = 0;
				foreach (var run in drawnRuns)
				{
					if (run.Run.Font.Hidden && index == drawnRuns.Count - 1)
						break;

					SolidColorBrush background = Terminal.GetFontBackgroundBrush(run.Run.Font);

					// Draw the background and border for the current run
					Pen border = null;
					if (Terminal.DrawRunBoxes)
						border = new Pen(DebugColors.GetBrush(index), 1);

					var backgroundTopLeft = new System.Windows.Point(Math.Floor(drawPoint.X), Math.Floor(drawPoint.Y));
					var backgroundSize = new Vector(Math.Floor(run.Text.WidthIncludingTrailingWhitespace), Math.Ceiling(run.Text.Height));
					context.DrawRectangle(background, border, new Rect(backgroundTopLeft, backgroundSize));

					drawPoint.X += run.Text.WidthIncludingTrailingWhitespace;

					index++;
				}

				drawPoint = new System.Windows.Point(0, 0);
				index = 0;
				foreach (var run in drawnRuns)
				{
					if (run.Run.Font.Hidden && index == drawnRuns.Count - 1)
						break;
					
					context.DrawText(run.Text, drawPoint);
					drawPoint.X += run.Text.WidthIncludingTrailingWhitespace;

					index++;
				}

				// TODO: This selection drawing logic doesn't account for multi-width characters.
				if (SelectionStart != SelectionEnd)
				{
					var selectRect = new Rect(
						new System.Windows.Point(Math.Floor(Math.Min(drawPoint.X, Terminal.CharWidth * SelectionStart)), 0.0),
						new System.Windows.Point(Math.Ceiling(Math.Min(drawPoint.X, Terminal.CharWidth * SelectionEnd)), Math.Ceiling(Terminal.CharHeight)));

					var brush = new SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 90, 180, 230));
					context.DrawRectangle(brush, null, selectRect);
				}
			}
			finally
			{
				context.Close();
			}
		}
	}
}
