using System;
using System.Collections.Generic;
using System.IO;
using System.IO.IsolatedStorage;
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
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace npcook.Ssh
{
	/// <summary>
	/// Interaction logic for ConnectionDialog.xaml
	/// </summary>
	public partial class ConnectionDialog : Window
	{
		const string configPath = "connect.cfg";

		public string ServerAddress
		{ get { return serverAddress.Text; } }

		public int ServerPort
		{
			get
			{
				int port;
				if (int.TryParse(serverPort.Text, out port))
					return port;
				return 0;
			}
		}

		public string Username
		{ get { return username.Text; } }

		public string Password
		{ get { return password.Password; } }

		public string KeyFilePath
		{ get { return keyPath.Text; } }

		public string KeyFilePassphrase
		{ get { return keyPassphrase.Password; } }

		public ConnectionDialog()
		{
			InitializeComponent();
			
			var store = IsolatedStorageFile.GetUserStoreForAssembly();
			if (store.FileExists(configPath))
			{
				try
				{
					using (var reader = new StreamReader(store.OpenFile(configPath, FileMode.Open)))
					{
						var document = XDocument.Load(reader);

						string rawServerAddress = document.XPathSelectElement("/Settings/ServerAddress")?.Value;
						if (rawServerAddress != null)
							serverAddress.Text = rawServerAddress;

						string rawServerPort = document.XPathSelectElement("/Settings/ServerPort")?.Value;
						if (rawServerPort != null)
							serverPort.Text = rawServerPort;

						string rawUsername = document.XPathSelectElement("/Settings/Username")?.Value;
						if (rawUsername != null)
							username.Text = rawUsername;

						string rawKeyFilePath = document.XPathSelectElement("/Settings/KeyFilePath")?.Value;
						if (rawKeyFilePath != null)
							keyPath.Text = rawKeyFilePath;
					}
				}
				catch (Exception ex) when (ex is IOException || ex is XmlException)
				{
					MessageBox.Show(this, "Could not access your saved settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
            }
		}

		protected override void OnClosed(EventArgs e)
		{
			base.OnClosed(e);

			if (DialogResult.GetValueOrDefault(false))
			{
				var store = IsolatedStorageFile.GetUserStoreForAssembly();
				try
				{
					var document = new XDocument();
					var root = new XElement(XName.Get("Settings"));
					document.Add(root);

					root.Add(new XElement(XName.Get("ServerAddress"), ServerAddress));
					root.Add(new XElement(XName.Get("ServerPort"), ServerPort));
					root.Add(new XElement(XName.Get("Username"), Username));
					root.Add(new XElement(XName.Get("KeyFilePath"), KeyFilePath));

					using (var writer = XmlWriter.Create(store.OpenFile(configPath, FileMode.Create)))
					{
						document.WriteTo(writer);
					}
				}
				catch (IOException)
				{
					MessageBox.Show(this, "Could not access your saved settings.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				}
			}
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
