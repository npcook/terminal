using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace npcook.Ssh
{
	static class Util
	{
		public static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
		{
			do
			{
				if (current is T)
					return (T) current;
				current = VisualTreeHelper.GetParent(current);
			} while (current != null);
			return null;
		}
	}
}
