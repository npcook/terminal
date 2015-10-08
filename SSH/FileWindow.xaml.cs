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

		public void Connect(SftpClient client)
		{
			this.client = client;
			localPath = Directory.GetCurrentDirectory();
			foreach (var filename in Directory.GetFiles(localPath))
				localFiles.Add(new LocalFile { Name = System.IO.Path.GetFileName(filename) });

			foreach (var file in client.ListDirectory(client.WorkingDirectory))
				remoteFiles.Add(file);
        }
	}
}
