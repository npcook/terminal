using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace npcook.Ssh
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		public const int DefaultTerminalCols = 160;
		public const int DefaultTerminalRows = 40;

		public static new App Current
		{ get { return Application.Current as App; } }

		public App()
		{
			Dispatcher.UnhandledException += Dispatcher_UnhandledException;
		}

		private void Dispatcher_UnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
		{
			MessageBox.Show(MainWindow, e.Exception.Message, "An unhandled exception has occurred", MessageBoxButton.OK, MessageBoxImage.Error);
			Shutdown(1);
		}

		private void this_Startup(object sender, StartupEventArgs e)
		{
			ShutdownMode = ShutdownMode.OnExplicitShutdown;
			var dialog = new ConnectionDialog();
			if (dialog.ShowDialog().GetValueOrDefault(false))
			{
				/*var fileWindow = new FileWindow();
				MainWindow = fileWindow;

				var client = new Renci.SshNet.SftpClient(dialog.Connection.Client.ConnectionInfo);
				client.Connect();
				fileWindow.Connect(client);
				ShutdownMode = ShutdownMode.OnLastWindowClose;
				fileWindow.Show();*/
				
				var mainWindow = new MainWindow();
				MainWindow = mainWindow;

				var authList = new List<Authentication>();
				if (dialog.Password != "")
					authList.Add(new PasswordAuthentication(dialog.Password));
				if (dialog.KeyFilePath != "")
					authList.Add(new KeyAuthentication(File.Open(dialog.KeyFilePath, FileMode.Open), dialog.KeyFilePassphrase.ToString()));

				mainWindow.Connect(dialog.Connection.Stream);
				mainWindow.Show();
				ShutdownMode = ShutdownMode.OnLastWindowClose;
			}
			else
				Shutdown();
		}
	}
}
