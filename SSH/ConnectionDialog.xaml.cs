using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
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

namespace npcook.Ssh
{
	/// <summary>
	/// Interaction logic for ConnectionDialog.xaml
	/// </summary>
	public partial class ConnectionDialog : Window
	{
		public string ServerAddress
		{ get { return serverAddress.Text; } }

		public int ServerPort
		{ get { return int.Parse(serverPort.Text); } }

		public string Username
		{ get { return username.Text; } }

		public SecureString Password
		{ get { return password.SecurePassword; } }

		public string KeyFilePath
		{ get { return keyPath.Text; } }

		public SecureString KeyFilePassphrase
		{ get { return keyPassphrase.SecurePassword; } }

		public ConnectionDialog()
		{
			InitializeComponent();
		}

		private void keyPathBrowse_Click(object sender, RoutedEventArgs e)
		{
			var dialog = new Microsoft.Win32.OpenFileDialog();
			dialog.Filter = "All Files|*.*";
			dialog.Title = "Private Key File";

			bool result = dialog.ShowDialog(this).GetValueOrDefault(false);
			if (result)
			{
				keyPath.Text = dialog.FileName;
			}
		}

		private void close_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}

		private void connect_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}
	}
}
