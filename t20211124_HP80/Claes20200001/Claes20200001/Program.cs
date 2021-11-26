using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;
using Charlotte.Commons;
using Charlotte.Tests;
using Charlotte.WebServices;

namespace Charlotte
{
	class Program
	{
		static void Main(string[] args)
		{
			ProcMain.CUIMain(new Program().Main2);
		}

		private void Main2(ArgsReader ar)
		{
			if (ProcMain.DEBUG)
			{
				Main3();
			}
			else
			{
				Main4(ar);
			}
			Common.OpenOutputDirIfCreated();
		}

		private void Main3()
		{
			// -- choose one --

			Main4(new ArgsReader(new string[] { "80", @"..\..\..\..\dat\favicon.ico" }));
			//new Test0001().Test01();
			//new Test0002().Test01();
			//new Test0003().Test01();

			// --

			//Common.Pause();
		}

		private void Main4(ArgsReader ar)
		{
			try
			{
				Main5(ar);
			}
			catch (Exception e)
			{
				ProcMain.WriteLog(e);
			}

			// 終了時のコンソール出力が見えるように、少し待つ。
			Thread.Sleep(500);
		}

		private void Main5(ArgsReader ar)
		{
			using (EventWaitHandle evStop = new EventWaitHandle(false, EventResetMode.AutoReset, Consts.SERVER_STOP_EVENT_NAME))
			{
				HTTPServer hs = new HTTPServer()
				{
					//PortNo = 80,
					//Backlog = 300,
					//ConnectMax = 100,
					Interlude = () => !evStop.WaitOne(0) && !Console.KeyAvailable, // 停止イベント / キー押下
					HTTPConnected = P_Connected,
				};

				//SockChannel.ThreadTimeoutMillis = 100;

				//HTTPServer.KeepAliveTimeoutMillis = 5000;

				HTTPServerChannel.RequestTimeoutMillis = 10000; // 10 sec
				//HTTPServerChannel.ResponseTimeoutMillis = -1;
				//HTTPServerChannel.FirstLineTimeoutMillis = 2000;
				HTTPServerChannel.IdleTimeoutMillis = 600000; // 10 min
				HTTPServerChannel.BodySizeMax = 0;

				// サーバーの設定ここまで

				ProcMain.WriteLog("HP80-ST");

				if (ar.ArgIs("/S"))
				{
					evStop.Set();
				}
				else
				{
					hs.PortNo = int.Parse(ar.NextArg());
					this.FaviconFile = ar.NextArg();

					ProcMain.WriteLog("PortNo: " + hs.PortNo);
					ProcMain.WriteLog("FaviconFile: " + this.FaviconFile);

					if (hs.PortNo < 1 || 65535 < hs.PortNo)
						throw new Exception("Bad PortNo");

					if (string.IsNullOrEmpty(this.FaviconFile))
						throw new Exception("Bad FaviconFile");

					if (!File.Exists(this.FaviconFile))
						throw new Exception("no FaviconFile");

					this.FaviconData = File.ReadAllBytes(this.FaviconFile);

					ProcMain.WriteLog("FaviconData-Size: " + this.FaviconData.Length);

					hs.Perform();
				}

				ProcMain.WriteLog("HP80-ED");
			}
		}

		private string FaviconFile;
		private byte[] FaviconData;

		private void P_Connected(HTTPServerChannel channel)
		{
			ProcMain.WriteLog("Client: " + channel.Channel.Handler.RemoteEndPoint);

			if (10 < channel.Method.Length) // rough limit
				throw new Exception("Bad method (too long)");

			ProcMain.WriteLog("Method: " + channel.Method);

			switch (channel.Method)
			{
				case "GET":
					this.HTTP_Get(channel);
					break;

				default:
					throw new Exception("Bad method");
			}
		}

		private void HTTP_Get(HTTPServerChannel channel)
		{
			string urlPath = channel.PathQuery;

			// クエリ除去
			{
				int ques = urlPath.IndexOf('?');

				if (ques != -1)
					urlPath = urlPath.Substring(0, ques);
			}

			if (1000 < urlPath.Length) // rough limit
				throw new Exception("Received path is too long");

			ProcMain.WriteLog("URL-Path：" + urlPath);

			string host = GetHeaderValue(channel, "Host");

			if (host == null)
				throw new Exception("No HOST header value");

			if (300 < host.Length) // rough limit
				throw new Exception("HOST header is too long");

			ProcMain.WriteLog("Host：" + host);

			string hostName = host;

			{
				int colon = hostName.IndexOf(':');

				if (colon != -1)
					hostName = host.Substring(0, colon);
			}

			if (urlPath == "/favicon.ico")
			{
				channel.ResStatus = 200;
				channel.ResHeaderPairs.Add(new string[] { "Content-Type", "image/x-icon" });
				channel.ResBody = new byte[][] { this.FaviconData };
			}
			else if (Consts.HP80_HostNames.Contains(hostName))
			{
				if (urlPath == "/")
					channel.ResStatus = 200;
				else
					channel.ResStatus = 404;

				channel.ResHeaderPairs.Add(new string[] { "Content-Type", "text/html" });
				channel.ResBody = new byte[][] { Encoding.ASCII.GetBytes(this.GetHP80Page(channel)) };
			}
			else if (urlPath == "/")
			{
				channel.ResStatus = 301;
				channel.ResHeaderPairs.Add(new string[] { "Location", "http://" + hostName + ":" + Consts.GeTunnelPortNo + "/" });
			}
			else
			{
				channel.ResStatus = 301;
				channel.ResHeaderPairs.Add(new string[] { "Location", "http://" + hostName + ":" + Consts.HTT_PortNo + urlPath });
			}

			channel.ResHeaderPairs.Add(new string[] { "Server", "HP80" });

			// ----

			SockCommon.WriteLog(SockCommon.ErrorLevel_e.INFO, "RES-STATUS " + channel.ResStatus);

			foreach (string[] pair in channel.ResHeaderPairs)
				SockCommon.WriteLog(SockCommon.ErrorLevel_e.INFO, "RES-HEADER " + pair[0] + " = " + pair[1]);

			SockCommon.WriteLog(SockCommon.ErrorLevel_e.INFO, "RES-BODY " + (channel.ResBody != null));
		}

		private static string GetHeaderValue(HTTPServerChannel channel, string name)
		{
			foreach (string[] pair in channel.HeaderPairs)
				if (SCommon.EqualsIgnoreCase(pair[0], name))
					return pair[1];

			return null;
		}

		private string GetHP80Page(HTTPServerChannel channel)
		{
			return string.Join("", new string[]
			{
				channel.ResStatus.ToString(),
				DateTime.Now.ToString(),
				channel.Channel.Handler.RemoteEndPoint.ToString(),
				channel.FirstLine,
			}
			.Concat(channel.HeaderPairs.Select(v => v[0] + " = " + v[1]))
			.Select(v => "<div>" + v + "</div>"));
		}
	}
}
