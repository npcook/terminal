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
	public class TerminalControl : FrameworkElement
	{
		public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register("FontFamily", typeof(FontFamily), typeof(TerminalControl), new PropertyMetadata(new FontFamily("Consolas,Courier New"), FontFamilyChanged));
		public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register("FontSize", typeof(Double), typeof(TerminalControl), new PropertyMetadata(12.0, FontSizeChanged));
		public static readonly DependencyProperty BackgroundProperty = DependencyProperty.Register("Background", typeof(Brush), typeof(TerminalControl), new PropertyMetadata(Brushes.Black));

		private static void FontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as TerminalControl).updateCharDimensions();
		}

		private static void FontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			(d as TerminalControl).updateCharDimensions();
		}

		public FontFamily FontFamily
		{
			get { return (FontFamily) GetValue(FontFamilyProperty); }
			set { SetValue(FontFamilyProperty, value); }
		}

		public double FontSize
		{
			get { return (double) GetValue(FontSizeProperty); }
			set { SetValue(FontFamilyProperty, value); }
		}

		public Brush Background
		{
			get { return (Brush) GetValue(BackgroundProperty); }
			set { SetValue(FontFamilyProperty, value); }
		}
		
		// Visuals for the terminal screen (same size as number of rows)
		readonly List<TerminalLineVisual> screen = new List<TerminalLineVisual>();
		// The terminal backing this visual representation
		TerminalBase terminal = null;
		// Visual for the caret
		readonly DrawingVisual caret;
		// Timer for blinking the caret
		Timer caretTimer;
		
		public TerminalBase Terminal
		{
			get { return terminal; }
			set
			{
				if (terminal != null)
				{
					// Unregister events from the old terminal
					terminal.CursorPosChanged -= Terminal_CursorPosChanged;
					terminal.SizeChanged -= Terminal_SizeChanged;
					terminal.LineShiftedUp -= Terminal_LineShiftedUp;
					terminal.ScreenChanged -= Terminal_ScreenChanged;
				}
				terminal = value;

				// Remove all lines from the visual tree and the screen
				foreach (var line in screen)
					RemoveVisualChild(line);
				screen.Clear();

				// Create new visuals for each line in the new terminal screen
				foreach (var line in terminal.CurrentScreen)
				{
					var visual = new TerminalLineVisual(this, line);
					visual.Offset = new Vector(0.0, screen.Count * CharHeight);
					screen.Add(visual);
					AddVisualChild(visual);
				}

				terminal.CursorPosChanged += Terminal_CursorPosChanged;
				terminal.SizeChanged += Terminal_SizeChanged;
				terminal.LineShiftedUp += Terminal_LineShiftedUp;
				terminal.ScreenChanged += Terminal_ScreenChanged;
			}
		}

		public double CharWidth
		{ get; private set; }

		public double CharHeight
		{ get; private set; }

		public TerminalControl()
		{
			caret = new DrawingVisual();
			AddVisualChild(caret);

			uint caretBlinkTime = NativeMethods.GetCaretBlinkTime();
			caretTimer = new Timer(caretBlinkTime);
			caretTimer.Elapsed += (sender, e) => Dispatcher.Invoke(() => caret.Opacity = (caret.Opacity > 0.5 ? 0.0 : 1.0));
			caretTimer.AutoReset = true;
			caretTimer.Start();
		}

		public Typeface GetFontTypeface(TerminalFont? font)
		{
			if (font.HasValue)
			{
				return new Typeface(
					FontFamily,
					font.Value.Italic ? FontStyles.Italic : FontStyles.Normal,
					font.Value.Bold ? FontWeights.Bold : FontWeights.Light,
					FontStretches.Normal);
			}
			else
				return new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
		}

		private void Terminal_LineShiftedUp(object sender, LineShiftedUpEventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				int oldIndex = -1;
				for (int i = 0; i < screen.Count; ++i)
				{
					var visual = screen[i];
					if (visual.Line == e.OldLine)
						oldIndex = i;
					else
						visual.Offset = new Vector(0.0, (i - 1) * CharHeight);
				}
				RemoveVisualChild(screen[oldIndex]);
				screen.RemoveAt(oldIndex);

				var newVisual = new TerminalLineVisual(this, e.NewLine);
				newVisual.Offset = new Vector(0.0, screen.Count * CharHeight);
				screen.Add(newVisual);
				AddVisualChild(newVisual);
			});
		}

		private void Terminal_CursorPosChanged(object sender, EventArgs e)
		{
			// Update the caret position
			Action action = () => { Dispatcher.Invoke(updateCaret); };
			if (DeferChanges)
				AddDeferChangesCallback(this, action);
			else
				action();
		}

		private void Terminal_SizeChanged(object sender, EventArgs e)
		{ }

		private void Terminal_ScreenChanged(object sender, EventArgs e)
		{
			Dispatcher.Invoke(() => Terminal = Terminal);
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			if (terminal != null)
			{
				// The size is based on the number of rows and columns in the terminal
				return new Size(
					Math.Min(availableSize.Width, CharWidth * terminal.Size.Col),
					Math.Min(availableSize.Height, CharHeight * terminal.Size.Row));
			}
			else
				return Size.Empty;
		}
		
		void updateCharDimensions()
		{
			var ft = new FormattedText("y", System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, GetFontTypeface(null), FontSize, Brushes.Transparent);
			CharWidth = ft.Width;
			CharHeight = ft.Height;

			System.Diagnostics.Debug.Assert(CharWidth > 0 && CharHeight > 0);

			var context = caret.RenderOpen();
			
			var caretPen = new Pen(Brushes.White, SystemParameters.CaretWidth);
			caretPen.Freeze();
			context.DrawLine(caretPen, new System.Windows.Point(0.0, 0.0), new System.Windows.Point(0.0, CharHeight));

			context.Close();
		}
		
		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			drawingContext.DrawRectangle(Background, null, new Rect(0.0, 0.0, ActualWidth, ActualHeight));
		}

		protected override int VisualChildrenCount
		{
			// Each line is a child, plus 1 for the caret
			get { return screen.Count + 1; }
		}

		protected override Visual GetVisualChild(int index)
		{
			if (index < 0 || index >= VisualChildrenCount)
				throw new ArgumentOutOfRangeException("index", index, "");

			// Make sure the caret comes last so it gets drawn over the lines
			if (index < screen.Count)
				return screen[index];
			else
				return caret;
		}

		void updateCaret()
		{
			// Add 0.5 to each dimension so the caret is aligned to pixels
			if (terminal != null)
				caret.Offset = new Vector(Math.Floor(CharWidth * terminal.CursorPos.Col) + 0.5, Math.Floor(CharHeight * terminal.CursorPos.Row) + 0.5);

			caretTimer.Stop();
			caret.Opacity = 1.0;
			caretTimer.Start();
		}

		// Bulk changes can be deferred to avoid redundantly updating visuals
		internal bool DeferChanges
		{ get; private set; }

		Dictionary<object, Action> deferChangesCallbacks = new Dictionary<object, Action>();

		// Begin a group of changes
		public void BeginChange()
		{
			DeferChanges = true;
		}

		// End a group of changes and update all visuals in one swoop
		public void EndChange()
		{
			if (!DeferChanges)
				throw new InvalidOperationException("Must be deferring changes before deferring changes can end");

			DeferChanges = false;
			foreach (var kvp in deferChangesCallbacks)
				kvp.Value();
			deferChangesCallbacks.Clear();
		}

		// Register a delegate to be called when changes should be applied.  If the same callee
		// is passed multiple times, the last callback overwrites the previous callbacks
		public void AddDeferChangesCallback(object callee, Action callback)
		{
			if (!DeferChanges)
				throw new InvalidOperationException("Must be deferring changes before a callback can be added");

			deferChangesCallbacks[callee] = callback;
		}

		Dictionary<Color, SolidColorBrush> brushCache = new Dictionary<Color, SolidColorBrush>();
		internal SolidColorBrush GetBrush(Color color)
		{
			SolidColorBrush brush;
			if (brushCache.TryGetValue(color, out brush))
				return brush;

			brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(color.R, color.G, color.B));
			brush.Freeze();
			brushCache.Add(color, brush);
			return brush;
		}

		internal SolidColorBrush GetFontForegroundBrush(TerminalFont font)
		{
			var color = font.Foreground;
			if (font.Bold)
				return GetBrush(TerminalColors.MakeBold(color));
			else
				return GetBrush(color);
		}

		internal SolidColorBrush GetFontBackgroundBrush(TerminalFont font)
		{
			var color = font.Background;
			if (font.Bold)
				return GetBrush(TerminalColors.MakeBold(color));
			else
				return GetBrush(color);
		}
	}
}
