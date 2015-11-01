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
	struct LocalFile
	{
		public string Name
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
				localFiles.Add(new LocalFile { Name = System.IO.Path.GetFileName(filename) });
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
	}
}
