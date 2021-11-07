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
	public abstract class SockServer
	{
		/// <summary>
		/// ポート番号
		/// </summary>
		public int PortNo = 59999;

		/// <summary>
		/// 接続待ちキューの長さ
		/// </summary>
		public int Backlog = 300;

		/// <summary>
		/// 最大同時接続数
		/// </summary>
		public int ConnectMax = 100;

		/// <summary>
		/// 処理の合間に呼ばれる処理
		/// 戻り値：
		/// -- サーバーを継続するか
		/// </summary>
		public Func<bool> Interlude = () => !Console.KeyAvailable;

		// <---- prm

		/// <summary>
		/// サーバーロジック
		/// 通信量：
		/// -- 0 == 通信終了 -- SCommon.Supplier の最後の要素の次以降 0 (default(int)) になるため
		/// -- 0 未満 == 通信無し
		/// -- 1 以上 == 通信有り
		/// </summary>
		/// <param name="channel">接続チャネル</param>
		/// <returns>通信量</returns>
		protected abstract IEnumerable<int> E_Connected(SockChannel channel);

		private List<SockChannel> Channels = new List<SockChannel>();

		public void Perform()
		{
			SockCommon.WriteLog(SockCommon.ErrorLevel_e.INFO, "サーバーを開始しています...");

			try
			{
				using (Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
				{
					IPEndPoint endPoint = new IPEndPoint(0L, this.PortNo);

					try
					{
						listener.Bind(endPoint);
					}
					catch (Exception e)
					{
						throw new Exception("バインドに失敗しました。指定されたポート番号は使用中です。", e);
					}
					listener.Listen(this.Backlog);
					listener.Blocking = false;

					SockCommon.WriteLog(SockCommon.ErrorLevel_e.INFO, "サーバーを開始しました。");

					int waitMillis = 0;

					while (this.Interlude())
					{
						if (waitMillis < 100)
							waitMillis++;

						for (int c = 0; c < 30; c++) // HACK: 繰り返し回数_適当
						{
							Socket handler = this.Channels.Count < this.ConnectMax ? this.Connect(listener) : null;

							if (handler == null) // ? 接続無し || 最大同時接続数に達している。
								break;

							waitMillis = 0; // reset

							SockCommon.TimeWaitMonitor.Connected();

							{
								SockChannel channel = new SockChannel();

								channel.Handler = handler;
								handler = null; // もう使わない。
								channel.Handler.Blocking = false;
								channel.ID = SockCommon.IDIssuer.Issue();
								channel.Connected = SCommon.Supplier(this.E_Connected(channel));
								channel.BodyOutputStream = new HTTPBodyOutputStream();

								this.Channels.Add(channel);

								SockCommon.WriteLog(SockCommon.ErrorLevel_e.INFO, "通信開始 " + channel.ID);
							}
						}
						for (int index = this.Channels.Count - 1; 0 <= index; index--)
						{
							SockChannel channel = this.Channels[index];
							int size;

							try
							{
								size = channel.Connected();

								if (0 < size) // ? 通信有り
								{
									waitMillis = 0; // reset
								}
							}
							catch (Exception e)
							{
								if (channel.FirstLineRecving && e is SockChannel.RecvIdleTimeoutException)
									SockCommon.WriteLog(SockCommon.ErrorLevel_e.FIRST_LINE_TIMEOUT, null);
								else
									SockCommon.WriteLog(SockCommon.ErrorLevel_e.NETWORK_OR_SERVER_LOGIC, e);

								size = 0;
							}

							if (size == 0) // ? 切断
							{
								SockCommon.WriteLog(SockCommon.ErrorLevel_e.INFO, "通信終了 " + channel.ID);

								this.Disconnect(channel);
								SCommon.FastDesertElement(this.Channels, index);

								SockCommon.TimeWaitMonitor.Disconnect();
							}
						}

						//GC.Collect(); // GeoDemo の Server.sln が重くなるため、暫定削除 @ 2019.4.9

						if (0 < waitMillis)
							Thread.Sleep(waitMillis);
					}

					SockCommon.WriteLog(SockCommon.ErrorLevel_e.INFO, "サーバーを終了しています...");

					this.Stop();
				}
			}
			catch (Exception e)
			{
				SockCommon.WriteLog(SockCommon.ErrorLevel_e.FATAL, e);
			}

			SockCommon.WriteLog(SockCommon.ErrorLevel_e.INFO, "サーバーを終了しました。");
		}

		private Socket Connect(Socket listener) // ret: null == 接続タイムアウト
		{
			try
			{
				return SockCommon.NB("conn", () => listener.Accept());
			}
			catch (SocketException e)
			{
				if (e.ErrorCode != 10035)
				{
					throw new Exception("接続エラー", e);
				}
				return null;
			}
		}

		private void Stop()
		{
			foreach (SockChannel channel in this.Channels)
				this.Disconnect(channel);

			this.Channels.Clear();
		}

		private void Disconnect(SockChannel channel)
		{
			try
			{
				channel.Handler.Shutdown(SocketShutdown.Both);
			}
			catch (Exception e)
			{
				SockCommon.WriteLog(SockCommon.ErrorLevel_e.NETWORK, e);
			}

			try
			{
				channel.Handler.Close();
			}
			catch (Exception e)
			{
				SockCommon.WriteLog(SockCommon.ErrorLevel_e.NETWORK, e);
			}

			channel.BodyOutputStream.Dispose();

			SockCommon.IDIssuer.Discard(channel.ID);
		}
	}
}
