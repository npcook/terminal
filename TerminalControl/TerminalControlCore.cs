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

		const int historySize = 2000;

		enum SelectionState
		{
			None,			// Nothing is selected
			Selecting,		// Selection is currently being made
			Selected		// Selection has already been made
		}

		// Backing for the scroll-back history
		readonly Deque<TerminalLine> history = new Deque<TerminalLine>(historySize);
		// Visuals for visible lines.  Always of size terminal.Size.Row
		Deque<TerminalLineVisual> visuals;
		// Extra visuals.
		Deque<TerminalLineVisual> extraVisuals;
		// The terminal backing this visual representation
		XtermTerminal terminal = null;
		// Timer for blinking the caret
		DispatcherTimer caretTimer;
		// Backing for the BlinkCursor property
		bool blinkCursor = true;
		// Backing for the EnableCaret property
		bool enableCaret;
		// Determines whether user is selecting or has selected text
		SelectionState selectionState;
		// Point where user started making a selection, relative to history
		// This point doesn't need to be higher or left-er than selectionEnd
		Point selectionStart;
		// Point where user stopped making a selection, relative to history
		// This point doesn't need to be lower or right-er than selectionStart
		Point selectionEnd;
		// Stores brushes that correspond to terminal colors so we don't have to create a new one 
		// each time
		Dictionary<Color, SolidColorBrush> brushCache = new Dictionary<Color, SolidColorBrush>();
		// The ScrollViewer that owns our IScrollInfo implementation
		ScrollViewer scrollOwner;
		// Backing for the VerticalOffset property
		double verticalOffset;
		// Backing for the HorizontalOffset property
		double horizontalOffset;
		// Object to protect deferChangesCallbacks
		object deferChangesLock = new object();
		// Holds callbacks that will be called when changes stop being deferred
		Dictionary<object, Action> deferChangesCallbacks = new Dictionary<object, Action>();
		// Visuals for each caret type
		readonly DrawingVisual verticalLineCaret = new DrawingVisual();
		readonly DrawingVisual blockCaret = new DrawingVisual();
		readonly DrawingVisual underlineCaret = new DrawingVisual();
		// Currently displayed caret visual.  This will only refer to one of the above 3 visuals
		DrawingVisual caret;

		public TerminalControlCore()
		{
			Focusable = false;
			CanVerticallyScroll = true;

			Cursor = Cursors.IBeam;

			initCaret();
			initContextMenu();
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

		public double CharWidth
		{ get; private set; }

		public double CharHeight
		{ get; private set; }

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

				visuals = new Deque<TerminalLineVisual>(terminal.Size.Row);
				extraVisuals = new Deque<TerminalLineVisual>(terminal.Size.Row);
				// Create new visuals for each line in the new terminal screen
				foreach (var item in terminal.CurrentScreen.Select((line, i) => new { line, i }))
				{
					history.Add(item.line);
					var visual = new TerminalLineVisual(this, item.line);
					visual.Offset = new Vector(0.0, item.i * CharHeight);
					visuals.PushBack(visual);
					AddVisualChild(visual);

					visual = new TerminalLineVisual(this, null);
					extraVisuals.PushBack(visual);
				}

				terminal.CursorPosChanged += Terminal_CursorPosChanged;
				terminal.SizeChanged += Terminal_SizeChanged;
				terminal.LinesMoved += Terminal_LinesMoved;
				terminal.ScreenChanged += Terminal_ScreenChanged;
				terminal.PrivateModeChanged += Terminal_PrivateModeChanged;
			}
		}

		public bool BlinkCursor
		{
			get { return blinkCursor; }
			set
			{
				blinkCursor = value;

				Dispatcher.Invoke(updateCursorState);
			}
		}

		public bool ShowCursor
		{
			get { return terminal.ShowCursor; }
		}

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

		// Begin a group of changes
		public void BeginChange()
		{
			lock (deferChangesLock)
			{
				DeferChanges = true;
			}
		}

		// End a group of changes and update all visuals in one swoop
		public void EndChange()
		{
			IEnumerable<Action> callbacks = Enumerable.Empty<Action>();
			lock (deferChangesLock)
			{
				if (DeferChanges)
				{
					DeferChanges = false;
					callbacks = deferChangesCallbacks.Values.ToArray();
					deferChangesCallbacks.Clear();
				}
			}
			foreach (var callback in callbacks)
				callback();
		}

		// Register a delegate to be called when changes should be applied.  If the same callee
		// is passed multiple times, the last callback overwrites the previous callbacks
		public void AddDeferChangesCallback(object callee, Action callback)
		{
			bool callNow = false;
			lock (deferChangesLock)
			{
				if (DeferChanges)
					deferChangesCallbacks[callee] = callback;
				else
					callNow = true;
			}
			if (callNow)
				callback();
		}

		public void RemoveDeferChangesCallback(object callee)
		{
			lock (deferChangesLock)
			{
				if (DeferChanges)
					deferChangesCallbacks.Remove(callee);
			}
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

		private static void FontFamilyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			// Update character dimensions whenever a font property changes
			var self = d as TerminalControlCore;
			if (self != null)
				self.updateCharDimensions();
		}

		private static void FontSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			// Update character dimensions whenever a font property changes
			var self = d as TerminalControlCore;
			if (self != null)
				self.updateCharDimensions();
		}

		void initCaret()
		{
			caret = verticalLineCaret;
			AddVisualChild(caret);

			// Show and hide the caret with the interval given by the system blink time
			uint caretBlinkTime = NativeMethods.GetCaretBlinkTime();
			caretTimer = new DispatcherTimer(
				TimeSpan.FromMilliseconds(caretBlinkTime),
				DispatcherPriority.Normal,
				(sender, e) => caret.Opacity = (caret.Opacity > 0.5 ? 0.0 : 1.0),
				Dispatcher);
			caretTimer.Start();
		}

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

		void updateCharDimensions()
		{
			// Use the dimensions of the 'y' character as our width and height
			var ft = new FormattedText("y", System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, GetFontTypeface(null), FontSize, Brushes.Transparent);
			CharWidth = ft.Width;
			CharHeight = ft.Height;

			System.Diagnostics.Debug.Assert(CharWidth > 0 && CharHeight > 0);

			// Redraw caret visuals to reflect the new character dimensions
			{
				var context = verticalLineCaret.RenderOpen();

				var caretPen = new Pen(Brushes.White, SystemParameters.CaretWidth);
				caretPen.Freeze();
				context.DrawLine(
					caretPen,
					new System.Windows.Point(0.0, 0.0),
					new System.Windows.Point(0.0, CharHeight));

				context.Close();
			}
			{
				var context = blockCaret.RenderOpen();

				context.DrawRectangle(
					Brushes.White, null, new Rect(0.0, 0.0, CharWidth, CharHeight));

				context.Close();
			}
			{
				var context = underlineCaret.RenderOpen();

				context.DrawRectangle(
					Brushes.White, null,
					new Rect(
						0.0, CharHeight - SystemParameters.CaretWidth,
						CharWidth, SystemParameters.CaretWidth));

				context.Close();
			}
		}

		enum CaretType
		{
			VerticalLine,
			Block,
			Underline,
		}

		void setCaretType(CaretType type)
		{
			if (caret != null)
				RemoveVisualChild(caret);

			switch (type)
			{
				case CaretType.VerticalLine:
					caret = verticalLineCaret;
					break;

				case CaretType.Block:
					caret = blockCaret;
					break;

				case CaretType.Underline:
					caret = underlineCaret;
					break;
			}

			AddVisualChild(caret);

			updateCaret();
		}

		internal bool DrawRunBoxes
		{ get; set; }

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

		protected bool BlinkCursorCore
		{ get { return BlinkCursor ^ terminal.BlinkCursor; } }

		protected bool historyEnabled
		{ get { return terminal.CurrentScreen == terminal.Screen; } }

		protected int screenIndex
		{ get { return history.Count - terminal.Size.Row; } }

		string getSelectedText()
		{
			if (selectionState != SelectionState.Selected)
				return "";

			var builder = new StringBuilder();

			if (selectionTop.Row == selectionBottom.Row)
				builder.Append(history[selectionTop.Row].GetCharacters(selectionTop.Col, selectionBottom.Col - selectionBottom.Col));
			else
			{
				for (int i = selectionTop.Row; i <= selectionBottom.Row; ++i)
				{
					var line = history[i];
					bool first = i == selectionTop.Row;
					bool last = i == selectionBottom.Row;
					if (first)
						builder.Append(line.GetCharacters(selectionTop.Col, line.Length - selectionTop.Col));
					else if (last)
						builder.Append(line.GetCharacters(0, selectionBottom.Col));
					else
						builder.Append(line.GetCharacters(0, line.Length));

					if (!last)
						builder.Append(Environment.NewLine);
				}
			}
			return builder.ToString();
		}

		bool this_CanCopy(object _)
		{
			return selectionState == SelectionState.Selected;
		}

		void this_Copy(object _)
		{
			if (selectionState == SelectionState.Selected)
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

		protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
		{
			double Y = hitTestParameters.HitPoint.Y;
			int lineIndex = (int) (Y / CharHeight);
			if (lineIndex < 0 || lineIndex >= visuals.Count)
				return null;
			return new PointHitTestResult(visuals[lineIndex], hitTestParameters.HitPoint);
		}

		protected override Size ArrangeOverride(Size finalSize)
		{
			return base.ArrangeOverride(finalSize);
		}

		Point selectionTop
		{
			get
			{
				if (selectionStart.Row < selectionEnd.Row)
					return selectionStart;
				else if (selectionStart.Row > selectionEnd.Row)
					return selectionEnd;
				else if (selectionStart.Col < selectionEnd.Col)
					return selectionStart;
				else
					return selectionEnd;
			}
		}

		Point selectionBottom
		{
			get
			{
				if (selectionStart.Row < selectionEnd.Row)
					return selectionEnd;
				else if (selectionStart.Row > selectionEnd.Row)
					return selectionStart;
				else if (selectionStart.Col < selectionEnd.Col)
					return selectionEnd;
				else
					return selectionStart;
			}
		}

		protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonDown(e);

			var position = e.GetPosition(this);
			selectionStart = new Point((int) (position.X / CharWidth + 0.5), (int) (position.Y / CharHeight + VerticalOffset));
			selectionEnd = selectionStart;
			selectionState = SelectionState.Selecting;

			foreach (var visual in visuals)
				visual.Select(0, 0);

			CaptureMouse();
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			if (selectionState == SelectionState.Selecting)
			{
				var position = e.GetPosition(this);
				int visualIndex = (int) (position.Y / CharHeight);
				selectionEnd = new Point((int) (position.X / CharWidth + 0.5), (int) (visualIndex + VerticalOffset));

				updateSelectedVisuals();
			}
		}

		void updateSelectedVisuals()
		{
			if (selectionState == SelectionState.None)
				return;
			foreach (var item in visuals.Select((visual, i) => new { visual, i }))
			{
				int historyIndex = (int) (item.i + VerticalOffset);
				if (historyIndex >= selectionTop.Row && historyIndex <= selectionBottom.Row)
				{
					bool first = historyIndex == selectionTop.Row;
					bool last = historyIndex == selectionBottom.Row;
					if (first && last)
						item.visual.Select(selectionTop.Col, selectionBottom.Col);
					else if (first)
						item.visual.Select(selectionTop.Col, item.visual.Line.Length);
					else if (last)
						item.visual.Select(0, selectionBottom.Col);
					else
						item.visual.Select(0, item.visual.Line.Length);
				}
				else
					item.visual.Select(0, 0);
			}
		}

		protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
		{
			base.OnMouseLeftButtonUp(e);

			if (selectionStart.Col != selectionEnd.Col || selectionStart.Row != selectionEnd.Row)
				selectionState = SelectionState.Selected;
			else
				selectionState = SelectionState.None;
			ReleaseMouseCapture();
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

		private void prepareHistory(int newLines)
		{
			while (history.Count + newLines > historySize)
			{
				history.PopFront();
			}
		}

		private TerminalLineVisual recycleVisual(TerminalLineVisual visual)
		{
			RemoveVisualChild(visual);
			visual.Line = null;
			visual.Select(0, 0);
			visual.Offset = new Vector(-1000, -1000);

			var newVisual = extraVisuals.PopFront();
			extraVisuals.PushBack(visual);
			AddVisualChild(newVisual);
			return newVisual;
		}

		private void Terminal_LinesMoved(object sender, LinesMovedEventArgs e)
		{
			Dispatcher.Invoke(() =>
			{
				try
				{
					bool atEnd = VerticalOffset + ViewportHeight == ExtentHeight;
					bool historyShifted = history.Count == historySize;
					bool addToHistory = historyEnabled && e.OldIndex == 1 && e.NewIndex == 0;
					int insertBase = screenIndex + e.AddedLinesIndex;

					if (addToHistory)
					{
						prepareHistory(1);
					}
					else
					{
						int removeBase = screenIndex + e.RemovedLinesIndex;
						for (int i = Math.Abs(e.NewIndex - e.OldIndex) - 1; i >= 0; --i)
							history.RemoveAt(removeBase + i);
					}

					for (int i = 0; i < Math.Abs(e.NewIndex - e.OldIndex); ++i)
					{
						var line = terminal.CurrentScreen[e.AddedLinesIndex + i];
						if (addToHistory)
							history.PushBack(line);
						else
							history.Insert(insertBase + i, line);
					}

					if (addToHistory)
					{
						if (historyShifted)
						{
							if (!atEnd)
								verticalOffset -= 1;
							else
							{
								shiftVisuals(1, 0, terminal.Size.Row - 1);
								updateVisuals();
							}
						}
						else if (atEnd)
							scrollOwner.ScrollToBottom();
					}
					else
					{
						int removeBase = e.RemovedLinesIndex;
						insertBase = e.AddedLinesIndex;
						for (int i = Math.Abs(e.NewIndex - e.OldIndex) - 1; i >= 0; --i)
						{
							var visual = visuals[removeBase + i];
							if (insertBase > removeBase)
								insertBase--;
							visuals.RemoveAt(removeBase + i);
							visuals.Insert(insertBase + i, recycleVisual(visual));
							if (removeBase > insertBase)
								removeBase++;
						}

						updateVisuals();
					}
				}
				catch (Exception ex)
				{
					System.Diagnostics.Debug.WriteLine(ex);
				}
			});
		}

		void shiftVisuals(int index, int newIndex, int count)
		{
			if (newIndex > index)
			{
				for (int i = 0; i < newIndex - index; ++i)
				{
					var visual = visuals[newIndex + count - 1];
					visual.Select(0, 0);
					visuals.RemoveAt(newIndex + count - 1);
					visuals.Insert(index, recycleVisual(visual));
				}
			}
			else
			{
				for (int i = 0; i < index - newIndex; ++i)
				{
					var visual = visuals[newIndex];
					visual.Select(0, 0);
					visuals.RemoveAt(newIndex);
					visuals.Insert(index + count - 1, recycleVisual(visual));
				}
			}
		}

		void updateVisuals()
		{
			for (int i = 0; i < (int) (ViewportHeight + 0.01); ++i)
			{
				var line = history[(int) VerticalOffset + i];
				visuals[i].Line = line;
			}

			foreach (var item in visuals.Select((visual, i) => new { visual, i }))
				item.visual.Offset = new Vector(0.0, item.i * CharHeight);

			updateSelectedVisuals();

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
					SetVerticalOffset(VerticalOffset);
				}
				else
				{
					bool atEnd = false;
					if (VerticalOffset + ViewportHeight == ExtentHeight)
						atEnd = true;
					prepareHistory(terminal.Size.Row);
					foreach (var line in terminal.CurrentScreen)
						history.PushBack(line);
					if (atEnd)
					{
						verticalOffset--;
						scrollOwner.ScrollToBottom();
					}
				}
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
			get { return visuals.Count + 1; }
		}

		protected override Visual GetVisualChild(int index)
		{
			if (index < 0 || index >= VisualChildrenCount)
				throw new ArgumentOutOfRangeException("index", index, "");

			// Make sure the caret comes last so it gets drawn over the lines
			if (index < visuals.Count)
				return visuals[index];
			else
				return caret;
		}

		void updateCaret()
		{
			int adjustedRowPos = terminal.CursorPos.Row + (int) (ExtentHeight - ViewportHeight - VerticalOffset);
			if (terminal != null)
			{
				// Add 0.5 to each dimension so the caret is aligned to pixels
				// It's possible the caret is offscreen after this if adjustedRowPos > ViewportHeight
				caret.Offset = new Vector(Math.Floor(CharWidth * terminal.CursorPos.Col) + 0.5, Math.Floor(CharHeight * adjustedRowPos) + 0.5);
			}

			if (ShowCursor && BlinkCursorCore)
			{
				caretTimer.Stop();
				caret.Opacity = 1.0;
				caretTimer.Start();
			}
		}

		internal SolidColorBrush GetBrush(Color color)
		{
			const int MaxCacheSize = 512;

			SolidColorBrush brush;
			if (brushCache.TryGetValue(color, out brush))
				return brush;

			if (brushCache.Count == MaxCacheSize)
				brushCache.Clear();
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
		private bool DeferChanges
		{ get; set; }

		// IScrollInfo implementation
		
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

		public double HorizontalOffset
		{
			get { return horizontalOffset; }
			private set
			{
				horizontalOffset = (int) value;
				updateVisuals();
			}
		}

		public double VerticalOffset
		{
			get { return verticalOffset; }
			private set
			{
				if ((int) (value + 0.5) == (int) (verticalOffset + 0.5))
					return;
				int diff = (int) (value - verticalOffset);
				verticalOffset = value;
				if (diff > 0)
					shiftVisuals(diff, 0, visuals.Count - diff);
				else if (diff < 0)
					shiftVisuals(0, -diff, visuals.Count + diff);
				updateVisuals();
			}
		}

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
