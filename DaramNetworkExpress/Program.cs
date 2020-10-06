using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Forms;
using DaramNetworkExpress.Properties;
using Microsoft.Win32;

namespace DaramNetworkExpress
{
	static class Program
	{
		private static readonly RegistryKey Msmq =
			Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\MSMQ\\Parameters", true)
			?? Microsoft.Win32.Registry.LocalMachine.CreateSubKey("SOFTWARE\\Microsoft\\MSMQ\\Parameters");

		private static readonly RegistryKey Ipv4Interfaces =
			Microsoft.Win32.Registry.LocalMachine.OpenSubKey (
				"SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces", true);

		private static readonly RegistryKey Ipv6Interfaces =
			Microsoft.Win32.Registry.LocalMachine.OpenSubKey (
				"SYSTEM\\CurrentControlSet\\Services\\Tcpip6\\Parameters\\Interfaces", true);

		static IEnumerable<Tuple<NetworkInterface, bool>> GetNetworkAdapters()
		{
			var interfaces = NetworkInterface.GetAllNetworkInterfaces();
			foreach (var i in interfaces)
			{
				var interfaceKey4 = Ipv4Interfaces?.OpenSubKey(i.Id);
				var interfaceKey6 = Ipv6Interfaces?.OpenSubKey(i.Id);

				var taf4 = (int) (interfaceKey4?.GetValue("TcpAckFrequency", 2) ?? 2);
				var taf6 = (int) (interfaceKey6?.GetValue("TcpAckFrequency", 2) ?? 2);

				var tdat4 = (int) (interfaceKey4?.GetValue ("TcpDelAckTicks", 2) ?? 2);
				var tdat6 = (int) (interfaceKey6?.GetValue ("TcpDelAckTicks", 2) ?? 2);

				yield return new Tuple<NetworkInterface, bool>(i,
					(taf4 != 2 && tdat4 != 2) || (taf6 != 2 && tdat6 != 2));
			}
		}

		static void EnableAdapter (NetworkInterface i)
		{
			var psi = new ProcessStartInfo ("netsh", "interface set interface \"" + i.Name + "\" enable")
			{
				CreateNoWindow = true
			};
			Process.Start (psi)?.WaitForExit ();
		}

		static void DisableAdapter (NetworkInterface i)
		{
			var psi = new ProcessStartInfo ("netsh", "interface set interface \"" + i.Name + "\" disable")
			{
				CreateNoWindow = true
			};
			Process.Start(psi)?.WaitForExit();
		}

		[STAThread]
		static int Main ()
		{
			var isAlive = true;

			Application.SetHighDpiMode (HighDpiMode.SystemAware);
			Application.EnableVisualStyles ();
			Application.SetCompatibleTextRenderingDefault (false);
			 
			var msmqMenuItem = new ToolStripMenuItem("&MSMQ")
			{
				Checked = (int) Msmq.GetValue("TCPNoDelay", 0) != 0
			};
			msmqMenuItem.Click += (sender, e) =>
			{
				var item = sender as ToolStripMenuItem;
				Msmq.SetValue("TCPNoDelay", item.Checked ? 0 : 1);
				item.Checked = !item.Checked;
			};

			var adapterMenuItem = new ToolStripMenuItem("Network &Adapters");
			foreach (var i in GetNetworkAdapters())
			{
				var item = new ToolStripMenuItem(i.Item1.Name)
				{
					Checked = i.Item2,
					Tag = i.Item1,
				};
				item.Click += (sender, e) =>
				{
					var item = sender as ToolStripMenuItem;

					var networkInteface = item.Tag as NetworkInterface;
					var interfaceKey4 = Ipv4Interfaces?.OpenSubKey (networkInteface.Id, true);
					if (interfaceKey4 != null)
					{
						interfaceKey4.SetValue("TcpAckFrequency", item.Checked ? 2 : 1);
						interfaceKey4.SetValue("TcpDelAckTicks", item.Checked ? 2 : 0);
					}

					var interfaceKey6 = Ipv6Interfaces?.OpenSubKey (networkInteface.Id, true);
					if (interfaceKey6 != null)
					{
						interfaceKey6.SetValue ("TcpAckFrequency", item.Checked ? 2 : 1);
						interfaceKey6.SetValue ("TcpDelAckTicks", item.Checked ? 2 : 0);
					}

					item.Checked = !item.Checked;

					DisableAdapter(networkInteface);
					EnableAdapter(networkInteface);
				};
				adapterMenuItem.DropDownItems.Add(item);
			}

			var resetNetworkAdapters = new ToolStripMenuItem("&Reset all Network Adapters");
			resetNetworkAdapters.Click += (sender, e) =>
			{
			};

			var exitMenuItem = new ToolStripMenuItem("E&xit");
			exitMenuItem.Click += (sender, e) =>
			{
				isAlive = false;
			};

			var notifyIcon = new NotifyIcon
			{
				Icon = Resources.MainIcon,
				Text = "DaramNetworkExpress",
				Visible = true,
				ContextMenuStrip = new ContextMenuStrip()
				{
					Items =
					{
						msmqMenuItem,
						adapterMenuItem,
						new ToolStripSeparator (),
						resetNetworkAdapters,
						new ToolStripSeparator (),
						exitMenuItem,
					}
				}
			};

			while (isAlive)
			{
				Application.DoEvents();
			}

			return 0;
		}
	}
}
