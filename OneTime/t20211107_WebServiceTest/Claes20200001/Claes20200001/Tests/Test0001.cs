using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Charlotte.Commons;
using Charlotte.WebServices;

namespace Charlotte.Tests
{
	public class Test0001
	{
		public void Test01()
		{
			// none
		}

		public void Test02()
		{
			new HTTPServer()
			{
				HTTPConnected = channel =>
				{
					//channel.ResStatus = 200;
					channel.ResHeaderPairs.Add(new string[] { "Content-Type", "text/plain; charset=US-ASCII" });
					//channel.ResHeaderPairs.Add(new string[] { "X-Key-01", "Value-01" });
					//channel.ResHeaderPairs.Add(new string[] { "X-Key-02", "Value-02" });
					//channel.ResHeaderPairs.Add(new string[] { "X-Key-03", "Value-03" });
					channel.ResBody = "Hello, Happy World!".ToCharArray().Select(chr => Encoding.ASCII.GetBytes("" + chr));
				},
				//PortNo = 80,
				//Backlog = 300,
				//ConnectMax = 100,
				//Interlude = () => !Console.KeyAvailable,
			}
			.Perform();
		}

		public void Test03()
		{
			new HTTPServer()
			{
				HTTPConnected = channel =>
				{
					channel.ResHeaderPairs.Add(new string[] { "Content-Type", "text/plain; charset=US-ASCII" });
					channel.ResHeaderPairs.Add(new string[] { "Content-Disposition", "attachment" }); // ダウンロードさせる。
					channel.ResBody = Test03_E_ResBody();
				},
			}
			.Perform();
		}

		/// <summary>
		/// カウンタを2MB毎に連結して(塊にして)列挙する。
		/// </summary>
		/// <returns>カウンタの塊の列挙</returns>
		private IEnumerable<byte[]> Test03_E_ResBody()
		{
			List<byte[]> parts = new List<byte[]>();
			int size = 0;

			foreach (byte[] part in Test03_E_Counter())
			{
				parts.Add(part);
				size += part.Length;

				if (2000000 < size)
				{
					yield return SCommon.Join(parts);
					parts.Clear();
					size = 0;
				}
			}
			yield return SCommon.Join(parts);
		}

		/// <summary>
		/// カウンタ
		/// 1～1億
		/// およそ1GB弱になる。
		/// </summary>
		/// <returns>カウンタ</returns>
		private IEnumerable<byte[]> Test03_E_Counter()
		{
			for (int count = 1; count <= 100000000; count++)
			{
				yield return Encoding.ASCII.GetBytes(count + "\r\n");
			}
		}

		public void Test04()
		{
			new HTTPServer()
			{
				HTTPConnected = channel =>
				{
					List<string> dest = new List<string>();

					dest.Add(channel.FirstLine);
					dest.Add(channel.Method);
					dest.Add(channel.PathQuery);
					dest.Add(channel.HTTPVersion);

					foreach (string[] pair in channel.HeaderPairs)
					{
						dest.Add(pair[0]);
						dest.Add(pair[1]);
					}
					dest.Add("END-HEADER / BODY-SIZE = " + channel.Body.Length.ToString());
					dest.Add(channel.Body == null ? "<none>" : SCommon.Hex.ToString(channel.Body));
					dest.Add(channel.ContentLength.ToString());
					dest.Add(channel.Chunked.ToString());
					dest.Add(channel.ContentType == null ? "<none>" : channel.ContentType);
					dest.Add(channel.Expect100Continue.ToString());
					dest.Add(channel.KeepAlive.ToString());

					channel.ResHeaderPairs.Add(new string[] { "Content-Type", "text/plain; charset=UTF-8" });
					channel.ResBody = new byte[][] { Encoding.UTF8.GetBytes(SCommon.LinesToText(dest)) };
				},
			}
			.Perform();
		}
	}
}
