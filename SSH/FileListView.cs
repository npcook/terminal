using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace npcook.Ssh
{
	public class FileListView : ListView
	{
		protected override DependencyObject GetContainerForItemOverride()
		{
			return new FileListViewItem(this);
		}
	}

	public class FileListViewItem : ListViewItem
	{
		bool dragging = false;
		Point dragStart;
		LocalFile dragFile;
		FileListView listView;

		public FileListViewItem(FileListView listView)
		{
			this.listView = listView;
		}

		protected override void OnMouseDown(MouseButtonEventArgs e)
		{
			base.OnMouseDown(e);

			if (e.ChangedButton != MouseButton.Left)
				return;

			dragging = true;
			dragStart = e.GetPosition(this);
			dragFile = Content as LocalFile;
			Mouse.Capture(this);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);

			if (!dragging)
				return;
			var diff = dragStart - e.GetPosition(this);
			if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
				return;

			dragging = false;
			Mouse.Capture(null);

			var dropList = new System.Collections.Specialized.StringCollection();
			dropList.Add(dragFile.Path);

			var data = new DataObject();
			data.SetFileDropList(dropList);

			DragDrop.DoDragDrop(listView, data, DragDropEffects.Copy);
		}

		protected override void OnMouseUp(MouseButtonEventArgs e)
		{
			base.OnMouseUp(e);

			if (e.ChangedButton != MouseButton.Left)
				return;

			if (dragging)
			{
				dragging = false;
				Mouse.Capture(null);
			}
		}
	}
}
