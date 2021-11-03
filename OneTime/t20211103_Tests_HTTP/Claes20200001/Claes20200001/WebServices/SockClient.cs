using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using Charlotte.Commons;

namespace Charlotte.WebServices
{
	public class SockClient : SockChannel, IDisposable
	{
		public SockClient()
		{
			SockChannel.Critical.Enter();
		}

		public void Connect(string domain, int portNo)
		{
			IPHostEntry hostEntry = Dns.GetHostEntry(domain);
			IPAddress address = GetFairAddress(hostEntry.AddressList);
			IPEndPoint endPoint = new IPEndPoint(address, portNo);

			this.Handler = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			this.Handler.Connect(endPoint);
			this.Handler.Blocking = false;
		}

		private static IPAddress GetFairAddress(IPAddress[] addresses)
		{
			foreach (IPAddress address in addresses)
			{
				if (address.AddressFamily == AddressFamily.InterNetwork) // ? IPv4
				{
					return address;
				}
			}
			return addresses[0];
		}

		/// <summary>
		/// 例外を投げないこと。
		/// </summary>
		public void Dispose()
		{
			if (this.Handler != null)
			{
				try
				{
					this.Handler.Disconnect(false);
				}
				catch (Exception e)
				{
					SockCommon.WriteLog(SockCommon.ErrorLevel_e.NETWORK, e);
				}

				try
				{
					this.Handler.Dispose();
				}
				catch (Exception e)
				{
					SockCommon.WriteLog(SockCommon.ErrorLevel_e.NETWORK, e);
				}

				this.Handler = null;

				try
				{
					SockChannel.Critical.Leave();
				}
				catch (Exception e)
				{
					SockCommon.WriteLog(SockCommon.ErrorLevel_e.FATAL, e);
				}
			}
		}
	}
}
