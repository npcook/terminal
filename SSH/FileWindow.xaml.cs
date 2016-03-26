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
using System.Runtime.InteropServices;

namespace npcook.Ssh
{
	class CommonFile
	{
		public string Name
		{ get; set; }

		public string Size
		{ get; set; }

		public bool IsDirectory
		{ get; set; }

		public ImageSource Icon
		{ get; set; }
	}

	class LocalFile : CommonFile
	{
		public string Path
		{ get; set; }
	}

	class RemoteFile : CommonFile
	{
		public SftpFile File
		{ get; set; }
	}

	/// <summary>
	/// Interaction logic for FileWindow.xaml
	/// </summary>
	public partial class FileWindow : Window
	{
		SftpClient client;
		string localDir;
		ObservableCollection<LocalFile> localFiles = new ObservableCollection<LocalFile>();
		ObservableCollection<RemoteFile> remoteFiles = new ObservableCollection<RemoteFile>();

		Dictionary<string, int> localFileMap = new Dictionary<string, int>();
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
			watcher.Renamed += Watcher_Renamed;
			watcher.Deleted += Watcher_Deleted;
		}

		private void Watcher_Created(object sender, FileSystemEventArgs e)
		{
			Dispatcher.BeginInvoke(new Action(() =>
			{
				var fileInfo = new FileInfo(e.FullPath);
				var localFile = makeLocalFile(fileInfo);
				localFiles.Add(localFile);
				localFileMap.Add(e.Name, localFiles.Count - 1);
			}));
		}

		private void Watcher_Renamed(object sender, RenamedEventArgs e)
		{
			Dispatcher.BeginInvoke(new Action(() =>
			{
				var fileInfo = new FileInfo(e.FullPath);
				int index = localFileMap[e.OldName];
				localFiles[index] = makeLocalFile(fileInfo);
				localFileMap.Remove(e.OldName);
				localFileMap.Add(e.Name, index);
			}));
		}

		private void Watcher_Deleted(object sender, FileSystemEventArgs e)
		{
			Dispatcher.BeginInvoke(new Action(() =>
			{
				int index = localFileMap[e.Name];
				localFiles.RemoveAt(index);
				localFileMap.Remove(e.Name);
			}));
		}

		LocalFile makeLocalFile(FileSystemInfo info)
		{
			var localFile = new LocalFile()
			{
				Name = info.Name,
				Path = info.FullName,
				IsDirectory = (info.Attributes & FileAttributes.Directory) != 0,
				Size = "",
			};
			if (!localFile.IsDirectory)
				localFile.Size = getSizeString((info as FileInfo).Length);

			localFile.Icon = getIconForFile(info.FullName);

			return localFile;
		}

		ImageSource getIconForFile(string filename)
		{
			return getIconInternal(filename, NativeMethods.SHGFI.ICON | NativeMethods.SHGFI.SMALLICON);
		}

		ImageSource getIconForExtension(string extension)
		{
			if (extension != "")
				return getIconInternal(extension, NativeMethods.SHGFI.ICON | NativeMethods.SHGFI.SMALLICON | NativeMethods.SHGFI.USEFILEATTRIBUTES);
			else
				return getStockIcon(NativeMethods.SIID.SIID_DOCNOASSOC, NativeMethods.SHGSI.ICON | NativeMethods.SHGSI.SMALLICON);
		}

		ImageSource getIconForDirectory()
		{
			return getStockIcon(NativeMethods.SIID.FOLDER, NativeMethods.SHGSI.ICON | NativeMethods.SHGSI.SMALLICON);
		}

		ImageSource getIconInternal(string filename, NativeMethods.SHGFI flags, bool directory = false)
		{
			var shellInfo = new NativeMethods.SHFILEINFO();
			uint size = (uint) Marshal.SizeOf(shellInfo);
			uint attributes = directory ? NativeMethods.FILE_ATTRIBUTE_DIRECTORY : NativeMethods.FILE_ATTRIBUTE_NORMAL;
			if (NativeMethods.SHGetFileInfo(filename, attributes, ref shellInfo, size, flags) != IntPtr.Zero)
			{
				try
				{
					return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(shellInfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
				}
				finally
				{
					NativeMethods.DestroyIcon(shellInfo.hIcon);
				}
			}
			return null;
		}

		ImageSource getStockIcon(NativeMethods.SIID stockId, NativeMethods.SHGSI flags)
		{
			var shellInfo = new NativeMethods.SHSTOCKICONINFO();
			shellInfo.cbSize = (uint) Marshal.SizeOf(shellInfo);
			if (NativeMethods.SHGetStockIconInfo(stockId, flags, ref shellInfo) == 0)
			{
				try
				{
					return System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(shellInfo.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
				}
				finally
				{
					NativeMethods.DestroyIcon(shellInfo.hIcon);
				}
			}
			return null;
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
			localFiles.Clear();
			localFileMap.Clear();

			localDir = newPath;
			var files =
				Directory.EnumerateFileSystemEntries(localDir)
				.Select(path =>
				{
					FileSystemInfo info = new FileInfo(path);
					return info;
				})
				.Where(info => (info.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0)
				.Select(info => makeLocalFile(info))
				.OrderBy(file => file.IsDirectory, new DirectoryComparer())
				.ThenBy(file => file.Name);
			
			foreach (var tuple in files.Enumerate())
			{
				int index = tuple.Item1;
				var file = tuple.Item2;
				localFiles.Add(file);
				localFileMap.Add(file.Name, index);
			}

			watcher.Path = localDir;
			watcher.EnableRaisingEvents = true;

			localPathText.Text = localDir;
		}

		private void changeRemoteDirectory(string newPath)
		{
			if (newPath != null)
				client.ChangeDirectory(newPath);
			remoteFiles.Clear();

			var files =
				client
				.ListDirectory(client.WorkingDirectory)
				.Where(file => !file.Name.StartsWith("."))
				.Select(file =>
				{
					var remoteFile = new RemoteFile()
					{
						File = file,
						Name = file.Name,
						IsDirectory = file.IsDirectory,
						Size = "",
					};
					if (!remoteFile.IsDirectory)
					{
						remoteFile.Size = getSizeString(file.Attributes.Size);
						remoteFile.Icon = getIconForExtension(System.IO.Path.GetExtension(file.Name));
					}
					else
						remoteFile.Icon = getIconForDirectory();

					return remoteFile;
				})
				.OrderBy(file => file.IsDirectory, new DirectoryComparer())
				.ThenBy(file => file.Name);

			foreach (var file in files)
				remoteFiles.Add(file);

			remotePathText.Text = client.WorkingDirectory;
		}

		string getSizeString(long size)
		{
			string[] units = new[] { "B", "KB", "MB", "GB", "TB", "PB", "EB", "ZB" };
			int index = 0;
			while (size > 1024 && index < units.Length - 1)
			{
				size /= 1024;
				index++;
			}
			return $"{size} {units[index]}";
		}

		private void localFilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var item = localFiles[localFilesList.SelectedIndex];
			if (item == null)
				return;
			if (item.IsDirectory)
				changeLocalDirectory(item.Path);
			else
				uploadFile(item.Path);
		}

		private void remoteFilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			var item = remoteFiles[remoteFilesList.SelectedIndex];
			if (item == null)
				return;
			if (item.IsDirectory)
				changeRemoteDirectory(item.File.FullName);
			else
				downloadFile(item.File.FullName);
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
				changeRemoteDirectory(client.WorkingDirectory);
			}
		}

		void downloadFile(string remotePath)
		{
			string filenameBase = System.IO.Path.GetFileNameWithoutExtension(remotePath);
			string extension = System.IO.Path.GetExtension(remotePath);
			string localPath = System.IO.Path.Combine(localDir, filenameBase + extension);
			int uniqueIndex = 1;
			while (File.Exists(localPath))
			{
				localPath = System.IO.Path.Combine(localDir, $"{filenameBase} ({uniqueIndex}){extension}");
				uniqueIndex++;
			}

			watcher.EnableRaisingEvents = false;
			using (var stream = File.Open(localPath, FileMode.CreateNew))
			{
				client.DownloadFile(remotePath, stream);
				changeLocalDirectory(localDir);
			}
		}

		private void LocalUp_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				var currentInfo = new DirectoryInfo(localDir);
				changeLocalDirectory(currentInfo.Parent.FullName);
			}
			catch (IOException)
			{
				throw;
			}
		}

		private void RemoteUp_Click(object sender, RoutedEventArgs e)
		{
			try
			{
				changeRemoteDirectory($"{client.WorkingDirectory}/..");
            }
			catch (IOException)
			{
				throw;
			}
		}
	}
}
