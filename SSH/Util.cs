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
		public static IEnumerable<Tuple<int, T>> Enumerate<T>(this IEnumerable<T> source)
		{
			int index = 0;
			foreach (T item in source)
			{
				yield return Tuple.Create(index, item);
				index++;
			}
		}

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
