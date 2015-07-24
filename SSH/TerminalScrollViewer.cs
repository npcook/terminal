using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace npcook.Ssh
{
	class TerminalScrollViewer : ScrollViewer
	{
		protected override void OnKeyDown(KeyEventArgs e)
		{
		}

		protected override void OnChildDesiredSizeChanged(UIElement child)
		{
			bool atEnd = VerticalOffset == ScrollableHeight;
			base.OnChildDesiredSizeChanged(child);
			if (atEnd)
				ScrollToBottom();
		}
	}
}
