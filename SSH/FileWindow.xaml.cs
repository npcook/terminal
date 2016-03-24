using System;
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
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.IO;
using System.Collections.ObjectModel;

namespace npcook.Ssh
{
	class LocalFile
	{
		public string Name
		{ get; set; }

		public string Path
		{ get; set; }
	}

	/// <summary>
	/// Interaction logic for FileWindow.xaml
	/// </summary>
	public partial class FileWindow : Window
	{
		SftpClient client;
		string localPath;
		ObservableCollection<LocalFile> localFiles = new ObservableCollection<LocalFile>();
		ObservableCollection<SftpFile> remoteFiles = new ObservableCollection<SftpFile>();
		FileSystemWatcher watcher;

		public FileWindow()
		{
			InitializeComponent();

			localFilesList.ItemsSource = localFiles;
			remoteFilesList.ItemsSource = remoteFiles;

			watcher = new FileSystemWatcher()
			{
				Filter = "",	// Watch all files, no matter the extension
				NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size,
				IncludeSubdirectories = false,
			};

			watcher.Created += Watcher_Created;
		}

		private void Watcher_Created(object sender, FileSystemEventArgs e)
		{
			throw new NotImplementedException();
		}

		class DirectoryComparer : IComparer<bool>
		{
			public int Compare(bool x, bool y)
			{
				if (x == y)
					return 0;
				else if (x && !y)
					return -1;
				else
					return 1;
			}
		}

		public void Connect(SftpClient client)
		{
			this.client = client;
			changeLocalDirectory(Directory.GetCurrentDirectory());
			changeRemoteDirectory(null);
        }

		private void changeLocalDirectory(string newPath)
		{
			localPath = newPath;
			foreach (var filename in Directory.GetFiles(localPath))
				localFiles.Add(new LocalFile { Name = System.IO.Path.GetFileName(filename), Path = filename });

			watcher.Path = localPath;
			watcher.EnableRaisingEvents = true;
		}

		private void changeRemoteDirectory(string newPath)
		{
			if (newPath != null)
				client.ChangeDirectory(newPath);
			remoteFiles.Clear();

			var directories =
				client
				.ListDirectory(client.WorkingDirectory)
				.Where(file => !file.Name.StartsWith(".") || file.Name == "..")
				.OrderBy(file => file.IsDirectory, new DirectoryComparer())
				.ThenBy(file => file.Name);

			foreach (var file in directories)
				remoteFiles.Add(file);
		}

		private void localFilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
		}

		private void remoteFilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var item = remoteFilesList.SelectedItem as SftpFile;
			if (item != null && item.IsDirectory)
			{
				changeRemoteDirectory(item.FullName);
			}
		}

		bool dragging = false;
		Point dragStart;
		LocalFile dragFile;
		private void localFilesList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			dragging = true;
			dragStart = e.GetPosition(localFilesList);
			var itemContainer = FindAncestor<ListViewItem>(VisualTreeHelper.HitTest(localFilesList, dragStart).VisualHit);
			dragFile = localFilesList.ItemContainerGenerator.ItemFromContainer(itemContainer) as LocalFile;
			Mouse.Capture(localFilesList);

			e.Handled = false;
		}

		static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
		{
			do
			{
				if (current is T)
					return (T) current;
				current = VisualTreeHelper.GetParent(current);
			} while (current != null);
			return null;
		}

		private void localFilesList_PreviewMouseMove(object sender, MouseEventArgs e)
		{
			if (!dragging)
				return;
			var diff = dragStart - e.GetPosition(localFilesList);
			if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance || Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
				return;

			var dropList = new System.Collections.Specialized.StringCollection();
			dropList.Add(dragFile.Path);

			var data = new DataObject();
			data.SetFileDropList(dropList);

			DragDrop.DoDragDrop(localFilesList, data, DragDropEffects.Copy);

			e.Handled = false;
		}

		private void localFilesList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
		{
			if (dragging)
			{
				dragging = false;
				Mouse.Capture(null);
			}
		}

		private void remoteFilesList_DragEnter(object sender, DragEventArgs e)
		{
			if (!e.Data.GetDataPresent(DataFormats.FileDrop))
				e.Effects = DragDropEffects.None;
		}

		private void remoteFilesList_Drop(object sender, DragEventArgs e)
		{
			if (!e.Data.GetDataPresent(DataFormats.FileDrop))
				return;

			var data = e.Data.GetData(DataFormats.FileDrop);
			var paths = data as IEnumerable<string>;
			foreach (string path in paths)
			{
				uploadFile(path);
			}
		}

		void uploadFile(string localPath)
		{
			string remotePath = System.IO.Path.GetFileName(localPath);
			using (var stream = File.OpenRead(localPath))
			{
				client.UploadFile(stream, remotePath);
			}
		}
	}
}
