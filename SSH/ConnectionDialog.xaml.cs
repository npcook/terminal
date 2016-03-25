using Renci.SshNet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
	public class ConnectionSettings
	{
		public string ServerAddress
		{ get; set; }

		public int ServerPort
		{ get; set; }

		public string Username
		{ get; set; }

		public string Password
		{ get; set; }

		public string KeyFilePath
		{ get; set; }

		public string KeyFilePassphrase
		{ get; set; }
	}

	/// <summary>
	/// Interaction logic for ConnectionDialog.xaml
	/// </summary>
	public partial class ConnectionDialog : Window
	{
		const string configPath = "connect.cfg";

		public ConnectionSettings SelectedSettings
		{
			get
			{
				return new ConnectionSettings()
				{
					ServerAddress = ServerAddress,
					ServerPort = ServerPort,
					Username = Username,
					Password = Password,
					KeyFilePath = KeyFilePath,
					KeyFilePassphrase = KeyFilePassphrase,
				};
			}
		}

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

		public ObservableCollection<ConnectionSettings> SavedSettings = new ObservableCollection<ConnectionSettings>();

		public Connection Connection
		{ get; private set; }

		public bool? Ok
		{ get; private set; }

		public ConnectionDialog()
		{
			DataContext = this;

			InitializeComponent();

			Loaded += (sender, e) => { MinHeight = ActualHeight; MaxHeight = ActualHeight; };

			settingsList.ItemsSource = SavedSettings;

			var store = IsolatedStorageFile.GetUserStoreForAssembly();
			if (store.FileExists(configPath))
			{
				try
				{
					using (var reader = new StreamReader(store.OpenFile(configPath, FileMode.Open)))
					{
						var document = XDocument.Load(reader);

						foreach (var settingsElement in document.XPathSelectElements("/Settings/Connection"))
						{
							var settings = new ConnectionSettings();
							string rawServerAddress = settingsElement.XPathSelectElement("ServerAddress")?.Value;
							if (rawServerAddress != null)
								settings.ServerAddress = rawServerAddress;

							string rawServerPort = settingsElement.XPathSelectElement("ServerPort")?.Value;
							if (rawServerPort != null)
							{
								int serverPort = 0;
								int.TryParse(rawServerPort, out serverPort);
								settings.ServerPort = serverPort;
							}

							string rawUsername = settingsElement.XPathSelectElement("Username")?.Value;
							if (rawUsername != null)
								settings.Username = rawUsername;

							string rawKeyFilePath = settingsElement.XPathSelectElement("KeyFilePath")?.Value;
							if (rawKeyFilePath != null)
								settings.KeyFilePath = rawKeyFilePath;

							SavedSettings.Add(settings);
						}
					}

					if (SavedSettings.Count > 0)
						settingsList.SelectedIndex = 0;
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

			if (Ok ?? false)
			{
				var store = IsolatedStorageFile.GetUserStoreForAssembly();
				try
				{
					var document = new XDocument();
					var root = new XElement(XName.Get("Settings"));
					document.Add(root);
					foreach (var settings in SavedSettings)
					{
						var connection = new XElement(XName.Get("Connection"));
						root.Add(connection);

						connection.Add(new XElement(XName.Get("ServerAddress"), settings.ServerAddress));
						connection.Add(new XElement(XName.Get("ServerPort"), settings.ServerPort));
						connection.Add(new XElement(XName.Get("Username"), settings.Username));
						connection.Add(new XElement(XName.Get("KeyFilePath"), settings.KeyFilePath));
					}

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

		private void save_Click(object sender, RoutedEventArgs e)
		{
			SavedSettings.Add(SelectedSettings);
			settingsList.SelectedIndex = settingsList.Items.Count - 1;
		}

		private void close_Click(object sender, RoutedEventArgs e)
		{
			Ok = false;
			Close();
		}

		void displayError(string message, string title)
		{
			MessageBox.Show(this, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
		}

		async void connect_Click(object sender, RoutedEventArgs e)
		{
			IsEnabled = false;

			try
			{
				Connection = await App.Current.MakeConnectionAsync(SelectedSettings, App.DefaultTerminalCols, App.DefaultTerminalRows);
				Ok = true;
				Close();
			}
			catch (ConnectException ex)
			{
				IsEnabled = true;
				displayError(ex.Message, "Could not connect");
			}
		}

		private void settingsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.AddedItems.Count > 0)
			{
				var settings = e.AddedItems[0] as ConnectionSettings;	// Let an exception happen if the items are not ConnectionSettings
				serverAddress.Text = settings.ServerAddress;
				serverPort.Text = settings.ServerPort.ToString();
				username.Text = settings.Username;
				password.Clear();
				keyPath.Text = settings.KeyFilePath;
				keyPassphrase.Clear();

				if (serverAddress.Text == "")
					serverAddress.Focus();
				else if (serverPort.Text == "")
					serverPort.Focus();
				else if (username.Text == "")
					username.Focus();
				else if (keyPath.Text == "")
					password.Focus();
				else
					keyPassphrase.Focus();
			}
		}

		private void settingsListItem_Delete(object sender, RoutedEventArgs e)
		{
			SavedSettings.Remove((sender as MenuItem).Tag as ConnectionSettings);
		}
	}
}
