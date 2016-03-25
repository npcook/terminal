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
	[Serializable]
	public class ConnectException : Exception
	{
		public ConnectException() { }
		public ConnectException(string message) : base(message) { }
		public ConnectException(string message, Exception inner) : base(message, inner) { }
		protected ConnectException(
		  System.Runtime.Serialization.SerializationInfo info,
		  System.Runtime.Serialization.StreamingContext context) : base(info, context)
		{ }
	}

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
			string message = $"{e.Exception.Message}\n\n{e.Exception.StackTrace}";
			MessageBox.Show(MainWindow, message, "An unhandled exception has occurred", MessageBoxButton.OK, MessageBoxImage.Error);
			Shutdown(1);
		}

		private async void this_Startup(object sender, StartupEventArgs e)
		{
			ShutdownMode = ShutdownMode.OnExplicitShutdown;
			var connection = await AskForConnectionAsync();
			if (connection != null)
			{
				MainWindow = MakeWindowForConnection(connection);
				ShutdownMode = ShutdownMode.OnLastWindowClose;
			}
			else
				Shutdown();
		}

		internal async Task<Connection> AskForConnectionAsync()
		{
			using (var closedEvent = new System.Threading.ManualResetEvent(false))
			{
				var dialog = new ConnectionDialog();
				dialog.Closed += (sender, e) =>
				{
					closedEvent.Set();
				};
				dialog.Show();

				await Task.Run(new Action(() => closedEvent.WaitOne()));

				if (dialog.Ok ?? false)
					return dialog.Connection;
				else
					return null;
			}
		}

		internal Window MakeWindowForConnection(Connection connection)
		{
			var window = new MainWindow();
			window.Connect(connection.Stream, connection.Settings);
			window.Show();
			return window;
		}

		internal async Task<Connection> MakeConnectionAsync(ConnectionSettings settings, int terminalCols, int terminalRows)
		{
			using (var doneEvent = new System.Threading.ManualResetEvent(false))
			{
				var connection = new Connection();
				string error = null;
				connection.Connected += (_sender, _e) =>
				{
					Dispatcher.Invoke(() =>
					{
						doneEvent.Set();
					});
				};
				connection.Failed += (_sender, _e) =>
				{
					Dispatcher.Invoke(() =>
					{
						error = _e.Message;
						doneEvent.Set();
					});
				};

				connection.Connect(settings, App.DefaultTerminalCols, App.DefaultTerminalRows);

				await Task.Run(new Action(() => doneEvent.WaitOne()));
				if (error != null)
					throw new ConnectException(error);
				return connection;
			}
		}
	}
}
