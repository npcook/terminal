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
		public static new App Current
		{ get { return Application.Current as App; } }

		private void this_Startup(object sender, StartupEventArgs e)
		{
			ShutdownMode = ShutdownMode.OnExplicitShutdown;
			var dialog = new ConnectionDialog();
			if (dialog.ShowDialog().GetValueOrDefault(false))
			{
				var mainWindow = new MainWindow();
				MainWindow = mainWindow;

				var authList = new List<Authentication>();
				if (dialog.Password != "")
					authList.Add(new PasswordAuthentication(dialog.Password));
				if (dialog.KeyFilePath != "")
					authList.Add(new KeyAuthentication(File.Open(dialog.KeyFilePath, FileMode.Open), dialog.KeyFilePassphrase.ToString()));

				mainWindow.Connect(dialog.ServerAddress, dialog.ServerPort, dialog.Username, authList);
				mainWindow.Show();
				ShutdownMode = ShutdownMode.OnLastWindowClose;
			}
			else
				Shutdown();
		}
	}
}
