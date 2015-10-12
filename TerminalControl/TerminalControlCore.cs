using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace npcook.Terminal.Controls
{
	class TerminalControlCore : FrameworkElement, IScrollInfo
	{
		public static readonly DependencyProperty FontFamilyProperty = DependencyProperty.Register("FontFamily", typeof(FontFamily), typeof(TerminalControlCore), new PropertyMetadata(new FontFamily("Consolas,Courier New"), FontFamilyChanged));
		public static readonly DependencyProperty FontSizeProperty = DependencyProperty.Register("FontSize", typeof(double), typeof(TerminalControlCore), new PropertyMetadata(12.0, FontSizeChanged));
		
		static TerminalControlCore()
		{
		}

		private static void FontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var self = d as TerminalControlCore;
			if (self != null)
				self.updateCharDimensions();
		}

		private static void FontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var self = d as TerminalControlCore;
			if (self != null)
				self.updateCharDimensions();
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

		public double CharWidth
		{ get; private set; }

		public double CharHeight
		{ get; private set; }

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

		const int historySize = 2000;
		
		// Backing for the scroll-back history
		readonly Deque<TerminalLine> history = new Deque<TerminalLine>(historySize);
		// Visuals for visible lines.  Always of size terminal.Size.Row
		TerminalLineVisual[] visuals;
		// The terminal backing this visual representation
		XtermTerminal terminal = null;
		// Visual for the caret
		readonly DrawingVisual caret;
		// Timer for blinking the caret
		DispatcherTimer caretTimer;

		internal bool DrawRunBoxes
		{ get; set; }

		public XtermTerminal Terminal
		{
			get { return terminal; }
			set
			{
				if (terminal != null)
				{
					// Unregister events from the old terminal
					terminal.CursorPosChanged -= Terminal_CursorPosChanged;
					terminal.SizeChanged -= Terminal_SizeChanged;
					terminal.LinesMoved -= Terminal_LinesMoved;
					terminal.ScreenChanged -= Terminal_ScreenChanged;
					terminal.PrivateModeChanged -= Terminal_PrivateModeChanged;
				}
				terminal = value;

				if (visuals != null)
				{
					foreach (var visual in visuals)
						RemoveVisualChild(visual);
				}
				history.Clear();
				
				visuals = new TerminalLineVisual[terminal.Size.Row];
				// Create new visuals for each line in the new terminal screen
				foreach (var item in terminal.CurrentScreen.Select((line, i) => new { line, i }))
				{
					history.Add(item.line);
					var visual = new TerminalLineVisual(this, item.line);
					visual.Offset = new Vector(0.0, item.i * CharHeight);
					visuals[item.i] = visual;
					AddVisualChild(visual);
				}

				terminal.CursorPosChanged += Terminal_CursorPosChanged;
				terminal.SizeChanged += Terminal_SizeChanged;
				terminal.LinesMoved += Terminal_LinesMoved;
				terminal.ScreenChanged += Terminal_ScreenChanged;
				terminal.PrivateModeChanged += Terminal_PrivateModeChanged;
			}
		}

		private void updateCursorState()
		{
			if (ShowCursor)
			{
				if (BlinkCursorCore)
				{
					caret.Opacity = 1.0;
					caretTimer.Start();
				}
				else
				{
					caretTimer.Stop();
					caret.Opacity = 1.0;
				}
			}
			else
			{
				caretTimer.Stop();
				caret.Opacity = 0.0;
			}
		}

		private void Terminal_PrivateModeChanged(object sender, PrivateModeChangedEventArgs e)
		{
			if (e.Mode == XtermDecMode.BlinkCursor)
			{
				Dispatcher.Invoke(updateCursorState);
			}
			else if (e.Mode == XtermDecMode.ShowCursor)
			{
				Dispatcher.Invoke(updateCursorState);
			}
		}

		bool blinkCursor = true;
		public bool BlinkCursor
		{
			get { return blinkCursor; }
			set
			{
				blinkCursor = value;

				Dispatcher.Invoke(updateCursorState);
			}
		}

		protected bool BlinkCursorCore
		{
			get { return BlinkCursor ^ terminal.BlinkCursor; }
		}
		
		public bool ShowCursor
		{
			get { return terminal.ShowCursor; }
		}

		bool enableCaret;
		public bool EnableCaret
		{
			get { return enableCaret; }
			set
			{
				if (enableCaret == value || terminal == null)
					return;

				if (value)
				{
					updateCursorState();
				}
				else if (ShowCursor)
				{
					caretTimer.Stop();
					caret.Opacity = 0.5;
				}
				enableCaret = value;
			}
		}

		protected bool historyEnabled
		{ get { return terminal.CurrentScreen == terminal.Screen; } }

		protected int screenIndex
		{ get { return history.Count - terminal.Size.Row; } }

		void initContextMenu()
		{
			ContextMenu = new ContextMenu();
			ContextMenu.BeginInit();

			ContextMenu.ItemsSource = new MenuItem[]
			{
				new MenuItem() { Header = "Copy", Command = new RelayCommand(this_Copy, this_CanCopy) },
				new MenuItem() { Header = "Paste", Command = new RelayCommand(this_Paste, this_CanPaste) },
			};

			ContextMenu.EndInit();
		}

		string getSelectedText()
		{
			var builder = new StringBuilder();
			bool first = true;
			foreach (var line in selectedLines)
			{
				if (first)
					first = false;
				else
					builder.Append(Environment.NewLine);
				builder.Append(line.Line.GetCharacters(line.SelectionStart, Math.Min(line.SelectionEnd - line.SelectionStart, line.Line.Length)));
			}
			return builder.ToString();
		}

		bool this_CanCopy(object _)
		{
			return selectedLines.Count > 0;
		}

		void this_Copy(object _)
		{
			if (selectedLines.Count > 0)
				Clipboard.SetText(getSelectedText());
		}

		bool this_CanPaste(object _)
		{
			return Clipboard.ContainsText();
		}

		void this_Paste(object _)
		{
			if (Clipboard.ContainsText())
			{
				string text = Clipboard.GetText();
				if (!string.IsNullOrEmpty(text))
				{
					Terminal.SendBytes(
						Encoding.UTF8.GetBytes(
							text.Where(
								c => !char.IsControl(c) || c == 27 || c == 8 || c == 13
								).ToArray()));
				}
			}
		}

		public TerminalControlCore()
		{
			Focusable = false;
			CanVerticallyScroll = true;

			Cursor = Cursors.IBeam;

			caret = new DrawingVisual();
			AddVisualChild(caret);

			uint caretBlinkTime = NativeMethods.GetCaretBlinkTime();
			caretTimer = new DispatcherTimer(
				TimeSpan.FromMilliseconds(caretBlinkTime), 
				DispatcherPriority.Normal, 
				(sender, e) => caret.Opacity = (caret.Opacity > 0.5 ? 0.0 : 1.0), 
                Dispatcher);
			caretTimer.Start();

			initContextMenu();
		}

		protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
		{
			double Y = hitTestParameters.HitPoint.Y;
			int lineIndex = (int) (Y / CharHeight);
			if (lineIndex < 0 || lineIndex >= visuals.Length)
				return null;
			return new PointHitTestResult(visuals[lineIndex], hitTestParameters.HitPoint);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			return base.ArrangeOverride(finalSize);
		}

		bool selecting = false;
		Point selectionStart;
		List<TerminalLineVisual> selectedLines = new List<TerminalLineVisual>();
		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonDown(e);

			var position = e.GetPosition(this);
			selectionStart = new Point((int) (position.X / CharWidth + 0.5), (int) (position.Y / CharHeight));
			selecting = true;

			foreach (var line in selectedLines)
				line.Select(0, 0);
			selectedLines.Clear();

			CaptureMouse();
		}

/*		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			if (selecting)
			{
				var newSelectedLines = new List<TerminalLineVisual>();

				var position = e.GetPosition(this);
				Point selectionEnd = new Point((int) (position.X / CharWidth + 0.5), (int) (position.Y / CharHeight));

				bool swapPoints = selectionStart.Row == selectionEnd.Row ? selectionStart.Col > selectionEnd.Col : selectionStart.Row > selectionEnd.Row;

				Point firstPoint = swapPoints ? selectionEnd : selectionStart;
				Point secondPoint = swapPoints ? selectionStart : selectionEnd;

				if (firstPoint.Row == secondPoint.Row)
				{
					history[selectionStart.Row].Select(firstPoint.Col, secondPoint.Col);
					newSelectedLines.Add(history[selectionStart.Row]);
				}
				else
				{
					for (int i = firstPoint.Row; i <= secondPoint.Row; ++i)
					{
						if (i == firstPoint.Row)
							history[i].Select(firstPoint.Col, terminal.Size.Col);
						else if (i == secondPoint.Row)
							history[i].Select(0, secondPoint.Col);
						else
							history[i].Select(0, terminal.Size.Col);
						newSelectedLines.Add(history[i]);
					}
				}

				foreach (var line in selectedLines.Where(visual => !newSelectedLines.Contains(visual)))
					line.Select(0, 0);
				selectedLines = newSelectedLines;
			}
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);

			selecting = false;
			ReleaseMouseCapture();
		}*/

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

		private void prepareHistory(int newLines)
		{
			bool needsMeasure = history.Count < historySize;

			if (history.Count + newLines == historySize + 1)
			{
				for (int i = 0; i < newLines; ++i)
					history.PopFront();

				verticalOffset -= newLines;
			}
		}

		private void Terminal_LinesMoved(object sender, LinesMovedEventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				bool addToHistory = historyEnabled && e.OldIndex == 1 && e.NewIndex == 0;
				int insertBase = e.AddedLinesIndex;

				if (addToHistory)
				{
					//if (history.Count == historySize)
					//	insertBase--;
					prepareHistory(1);
				}
				else
				{
					/*for (int i = 0; i < Math.Abs(e.NewIndex - e.OldIndex); ++i)
					{
						var visual = visuals[e.RemovedLinesIndex + i];
						RemoveVisualChild(visual);
					}*/

					int removeBase = screenIndex + e.RemovedLinesIndex;
					// Reversed loop because removal requires all succeeding lines to shift up.
					// Removing the final lines first means less or no shifting has to happen.
					for (int i = Math.Abs(e.NewIndex - e.OldIndex) - 1; i >= 0; --i)
						history.RemoveAt(removeBase + i);
					//Array.Copy(visuals, removeBase + Math.Abs(e.NewIndex - e.OldIndex), visuals, removeBase, visuals.Length - Math.Abs(e.NewIndex - e.OldIndex));
                }

				for (int i = 0; i < Math.Abs(e.NewIndex - e.OldIndex); ++i)
				{
					var line = terminal.CurrentScreen[e.AddedLinesIndex + i];
					//var newVisual = new TerminalLineVisual(this, line);
					if (addToHistory)
					{
						history.PushBack(line);
						insertBase++;
					}
					else
						history.Insert(insertBase + i, line);
					//newVisual.Offset = new Vector(0.0, (insertBase + i) * CharHeight);
					//AddVisualChild(newVisual);
				}

				updateVisuals();
				ScrollOwner.InvalidateScrollInfo();
			});
		}

		void updateVisuals()
		{
			for (int i = 0; i < (int) (ViewportHeight + 0.01); ++i)
			{
				var line = history[(int) VerticalOffset + i];
				visuals[i].Line = line;
			}

			updateCaret();
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
			Dispatcher.Invoke(() =>
			{
				bool mainScreen = terminal.CurrentScreen == terminal.Screen;
				if (mainScreen)
				{
					foreach (var line in terminal.CurrentScreen)
						history.PopBack();
					updateVisuals();
				}
				else
				{
					foreach (var line in terminal.CurrentScreen)
						history.PushBack(line);
					updateVisuals();
				}

				InvalidateMeasure();
			});
		}

		protected override Size MeasureOverride(Size availableSize)
		{
			if (terminal != null)
			{
				// The size is based on the number of rows and columns in the terminal
				return new Size(
					Math.Min(availableSize.Width, CharWidth * terminal.Size.Col),
					Math.Max(0, Math.Min(availableSize.Height, CharHeight * terminal.Size.Row)));
			}
			else
				return Size.Empty;
		}

		protected override int VisualChildrenCount
		{
			// Each line is a child, plus 1 for the caret
			get { return visuals.Length + 1; }
		}

		protected override Visual GetVisualChild(int index)
		{
			if (index < 0 || index >= VisualChildrenCount)
				throw new ArgumentOutOfRangeException("index", index, "");

			// Make sure the caret comes last so it gets drawn over the lines
			if (index < visuals.Length)
				return visuals[index];
			else
				return caret;
		}

		void updateCaret()
		{
			int adjustedRowPos = terminal.CursorPos.Row + (int) (ExtentHeight - ViewportHeight - VerticalOffset);
			if (terminal != null)
			{
				if (adjustedRowPos >= Terminal.Size.Row)
				{
					// Move the cursor outside of the viewport so it isn't visible
					caret.Offset = new Vector(-1, -1);
				}
				else
				{
					// Add 0.5 to each dimension so the caret is aligned to pixels
					caret.Offset = new Vector(Math.Floor(CharWidth * terminal.CursorPos.Col) + 0.5, Math.Floor(CharHeight * adjustedRowPos) + 0.5);
				}
			}

			if (ShowCursor && BlinkCursorCore)
			{
				caretTimer.Stop();
				caret.Opacity = 1.0;
				caretTimer.Start();
			}
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
			return GetBrush(color);
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

		public void AddMessage(string text, TerminalFont font)
		{
			prepareHistory(1);
			
			var line = new TerminalLine();
			int leftPadding = (Terminal.Size.Col - text.Length) / 2;
			int rightPadding = (Terminal.Size.Col - text.Length + 1) / 2;
            line.SetCharacters(0, new string(' ', leftPadding), font);
			line.SetCharacters(leftPadding, text, font);
			line.SetCharacters(leftPadding + text.Length, new string(' ', rightPadding), font);

			var visual = new TerminalLineVisual(this, line);
			visual.Offset = new Vector(0.0, history.Count * CharHeight);
			AddVisualChild(visual);
			history.PushBack(line);
		}
		
		public bool CanVerticallyScroll
		{ get; set; }

		public bool CanHorizontallyScroll
		{ get; set; }

		public double ExtentWidth
		{
			get
			{
				return terminal.Size.Col;
			}
		}

		public double ExtentHeight
		{
			get
			{
				return history.Count;
			}
		}

		public double ViewportWidth
		{
			get
			{
				return terminal.Size.Col;
			}
		}

		public double ViewportHeight
		{
			get
			{
				return terminal.Size.Row;
			}
		}

		double horizontalOffset;
		public double HorizontalOffset
		{
			get { return horizontalOffset; }
			private set
			{
				horizontalOffset = value;
				updateVisuals();
			}
		}

		double verticalOffset;
		public double VerticalOffset
		{
			get { return verticalOffset; }
			private set
			{
				verticalOffset = value;
				updateVisuals();
			}
		}

		ScrollViewer scrollOwner;
		public ScrollViewer ScrollOwner
		{
			get { return scrollOwner; }
			set
			{
				scrollOwner = value;
				HorizontalOffset = 0;
				VerticalOffset = 0;
			}
		}

		public void LineUp()
		{
			VerticalOffset = Math.Max(0, VerticalOffset - 1);
			ScrollOwner.InvalidateScrollInfo();
		}

		public void LineDown()
		{
			VerticalOffset = Math.Max(0, Math.Min(ExtentHeight - ViewportHeight, VerticalOffset + 1));
			ScrollOwner.InvalidateScrollInfo();
		}

		public void LineLeft()
		{
			throw new NotImplementedException();
		}

		public void LineRight()
		{
			throw new NotImplementedException();
		}

		public void PageUp()
		{
			VerticalOffset = Math.Max(0, VerticalOffset - ViewportHeight);
			ScrollOwner.InvalidateScrollInfo();
		}

		public void PageDown()
		{
			VerticalOffset = Math.Max(0, Math.Min(ExtentHeight - ViewportHeight, VerticalOffset + ViewportHeight));
			ScrollOwner.InvalidateScrollInfo();
		}

		public void PageLeft()
		{
			throw new NotImplementedException();
		}

		public void PageRight()
		{
			throw new NotImplementedException();
		}

		public void MouseWheelUp()
		{
			foreach (int i in Enumerable.Range(0, 3))
				LineUp();
		}

		public void MouseWheelDown()
		{
			foreach (int i in Enumerable.Range(0, 3))
				LineDown();
		}

		public void MouseWheelLeft()
		{
			throw new NotImplementedException();
		}

		public void MouseWheelRight()
		{
			throw new NotImplementedException();
		}

		public void SetHorizontalOffset(double offset)
		{
			HorizontalOffset = offset;
			ScrollOwner.InvalidateScrollInfo();
		}

		public void SetVerticalOffset(double offset)
		{
			VerticalOffset = Math.Max(0, Math.Min(ExtentHeight - ViewportHeight, offset));
			ScrollOwner.InvalidateScrollInfo();
		}

		public Rect MakeVisible(Visual visual, Rect rectangle)
		{
			throw new NotImplementedException();
		}
	}
}
