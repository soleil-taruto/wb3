using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net.Sockets;
using System.Windows.Forms;
using Charlotte.Commons;
using Charlotte.Tests;
using Charlotte.WebServices;
using System.Threading;

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
				Main4();
			}
			Common.OpenOutputDirIfCreated();
		}

		private void Main3()
		{
			Main4();
			Common.Pause();
		}

		private void Main4()
		{
			// -- choose one --

			Test01();
			//new Test0001().Test01();
			//new Test0002().Test01();
			//new Test0003().Test01();

			// --
		}

		private void Test01()
		{
			HTTPTest(@"
GET / HTTP/1.1
Host: {Host}:{PortNo}
");
			HTTPTest(@"
GET /Dummy HTTP/1.1
Host: {Host}:{PortNo}
");
			HTTPTest(@"
GET /dir01 HTTP/1.1
Host: {Host}:{PortNo}
");
			HTTPTest(@"
HEAD / HTTP/1.1
Host: {Host}:{PortNo}
");
			HTTPTest(@"
HEAD /Dummy HTTP/1.1
Host: {Host}:{PortNo}
");
			HTTPTest(@"
HEAD /dir01 HTTP/1.1
Host: {Host}:{PortNo}
");
		}

		private string Host = "localhost";
		private int PortNo = 80;

		private void HTTPTest(string request)
		{
			using (SockClient client = new SockClient())
			{
				request = request
					.Trim()
					.Replace("{Host}", Host)
					.Replace("{PortNo}", "" + PortNo);

				Console.WriteLine("---- Request ----");
				Console.WriteLine(request);
				Console.WriteLine("----");
				Console.WriteLine("");

				client.Connect(Host, PortNo);
				client.Send(Encoding.ASCII.GetBytes(request + "\r\n\r\n"));
				string response = string.Join("\r\n", RecvHeader(client));

				Console.WriteLine("---- Response ----");
				Console.WriteLine(response);
				Console.WriteLine("----");
				Console.WriteLine("");

				long size = RecvWhile(client);

				Console.WriteLine("size: " + size);
				Console.WriteLine("");
			}
		}

		private string[] RecvHeader(SockClient client)
		{
			List<string> lines = new List<string>();

			for (; ; )
			{
				string line = RecvLine(client);

				if (line == "")
					break;

				lines.Add(line);
			}
			return lines.ToArray();
		}

		private string RecvLine(SockClient client)
		{
			List<byte> buff = new List<byte>();

			for (; ; )
			{
				byte chr = client.Recv(1)[0];

				if (chr == 0x0d) // ? CR
					continue;

				if (chr == 0x0a) // ? LF
					break;

				buff.Add(chr);
			}
			return Encoding.ASCII.GetString(buff.ToArray());
		}

		private long RecvWhile(SockClient client)
		{
			long count = 0L;

			for (; ; )
			{
				int recvSize = Recv_1B(client);

				if (recvSize <= 0)
				{
					Thread.Sleep(300);
					recvSize = Recv_1B(client);

					if (recvSize <= 0)
						break;
				}
				count++;
			}
			return count;
		}

		public int Recv_1B(SockClient client)
		{
			try
			{
				return client.Handler.Receive(new byte[1], 0, 1, SocketFlags.None);
			}
			catch
			{
				return 0;
			}
		}
	}
}
