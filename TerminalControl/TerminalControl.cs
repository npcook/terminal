using System;
using System.Collections;
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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using Terminal;
using Color = Terminal.Color;

namespace TerminalControls
{
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

		List<TerminalLineVisual> lines = new List<TerminalLineVisual>();
		DrawingVisual caret = null;
		Terminal.Terminal terminal;
		public Terminal.Terminal Terminal
		{
			get { return terminal; }
			set
			{
				terminal = value;

				ColCount = terminal.Size.Col;
				RowCount = terminal.Size.Row;
				CursorCol = terminal.CursorPos.Col;
				CursorRow = terminal.CursorPos.Row;

				foreach (var line in lines)
					RemoveVisualChild(line);

				lines.Clear();
				foreach (var line in terminal.Screen)
				{
					var visual = new TerminalLineVisual(this, line);
					visual.Offset = new Vector(0.0, lines.Count * CharHeight);
					lines.Add(visual);
					AddVisualChild(visual);
				}

				terminal.CursorPosChanged += Terminal_CursorPosChanged;
				terminal.SizeChanged += Terminal_SizeChanged;
				terminal.LineShiftedUp += Terminal_LineShiftedUp;
			}
		}

		private void Terminal_LineShiftedUp(object sender, LineShiftedUpEventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				int oldIndex = -1;
				for (int i = 0; i < lines.Count; ++i)
				{
					var visual = lines[i];
					if (visual.Line == e.OldLine)
						oldIndex = i;
					else
						visual.Offset = new Vector(0.0, (i - 1) * CharHeight);
				}
				RemoveVisualChild(lines[oldIndex]);
				lines.RemoveAt(oldIndex);

				var newVisual = new TerminalLineVisual(this, e.NewLine);
				newVisual.Offset = new Vector(0.0, lines.Count * CharHeight);
				lines.Add(newVisual);
				AddVisualChild(newVisual);
			});
		}

		private void Terminal_CursorPosChanged(object sender, EventArgs e)
		{
			Action action = () => { Dispatcher.Invoke(updateCaret); };
			if (DeferChanges)
				AddDeferChangesCallback(this, action);
			else
				action();
		}

		private void Terminal_SizeChanged(object sender, EventArgs e)
		{ }

		protected override Size MeasureOverride(Size availableSize)
		{
			return new Size(
				Math.Min(availableSize.Width, CharWidth * terminal.Size.Col),
				Math.Min(availableSize.Height, CharHeight * terminal.Size.Row));
		}

		double? cachedCharWidth = null;
		double? cachedCharHeight = null;
		void updateCharDimensions()
		{
			var ft = new FormattedText("X", System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface, FontSize, Brushes.Transparent);
			cachedCharWidth = ft.Width;
			cachedCharHeight = FontSize;

			if (caret == null)
			{
				caret = new DrawingVisual();
				AddVisualChild(caret);
			}
			var context = caret.RenderOpen();

			var caretPen = new Pen(Brushes.White, SystemParameters.CaretWidth);
			caretPen.Freeze();
			context.DrawLine(caretPen, new System.Windows.Point(0.0, 0.0), new System.Windows.Point(0.0, CharHeight + 2));

			context.Close();
		}

		void clearCharDimensionsCache()
		{
			cachedCharWidth = null;
			cachedCharHeight = null;
		}

		public double CharWidth
		{
			get
			{
				if (!cachedCharWidth.HasValue)
					updateCharDimensions();
				return cachedCharWidth.Value;
			}
		}

		public double CharHeight
		{
			get
			{
				if (!cachedCharHeight.HasValue)
					updateCharDimensions();
				return cachedCharHeight.Value;
			}
		}

		public Typeface Typeface
		{
			get
			{
				return new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
			}
		}

		public int ColCount
		{
			get; set;
		}

		public int RowCount
		{
			get; set;
		}

		int cursorCol;
		public int CursorCol
		{
			get { return cursorCol; }
			internal set { cursorCol = value; }
		}

		int cursorRow;
		public int CursorRow
		{
			get { return cursorRow; }
			internal set { cursorRow = value; }
		}
		
		public TerminalControl()
		{ }

		protected override void OnRender(DrawingContext drawingContext)
		{
			base.OnRender(drawingContext);

			drawingContext.DrawRectangle(Background, null, new Rect(0.0, 0.0, ActualWidth, ActualHeight));
		}

		protected override int VisualChildrenCount
		{
			// Each line is a child, plus 1 for the caret
			get { return lines.Count + 1; }
		}

		protected override Visual GetVisualChild(int index)
		{
			if (index < 0 || index >= VisualChildrenCount)
				throw new ArgumentOutOfRangeException("index", index, "");

			if (index < lines.Count)
				return lines[index];
			else
				return caret;
		}

		public void updateCaret()
		{
			if (caret != null)
				caret.Offset = new Vector(Math.Floor(CharWidth * terminal.CursorPos.Col) + 0.5, Math.Floor(CharHeight * terminal.CursorPos.Row) + 0.5);
		}

		internal bool DeferChanges
		{ get; private set; }

		Dictionary<object, Action> deferChangesCallbacks = new Dictionary<object, Action>();
		public void AddDeferChangesCallback(object callee, Action callback)
		{
			if (!DeferChanges)
				throw new InvalidOperationException("Must be deferring changes before a callback can be added");

			deferChangesCallbacks[callee] = callback;
		}

		public void BeginChange()
		{
			DeferChanges = true;
		}

		public void EndChange()
		{
			if (!DeferChanges)
				throw new InvalidOperationException("Must be deferring changes before deferring changes can end");

			DeferChanges = false;
			foreach (var kvp in deferChangesCallbacks)
				kvp.Value();
			deferChangesCallbacks.Clear();
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

		internal SolidColorBrush GetFontForegoundBrush(TerminalFont font)
		{
			var color = font.Foreground;
			if (font.Bold)
				return GetBrush(TerminalColors.MakeBold(font.Foreground));
			else
				return GetBrush(font.Foreground);
		}
	}
}
