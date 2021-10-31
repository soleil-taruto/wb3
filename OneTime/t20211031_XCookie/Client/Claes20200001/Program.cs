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

			Main4(new ArgsReader(new string[] { "http://localhost/", @"C:\temp\1.txt" }));
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
		}

		private string Url;

		private void Main5(ArgsReader ar)
		{
			byte[] buff = new byte[65000];

			Url = ar.NextArg();
			string file = ar.NextArg();

			Send(Encoding.ASCII.GetBytes("Dummy"), 1);

			using (FileStream reader = new FileStream(file, FileMode.Open, FileAccess.Read))
			{
				for (; ; )
				{
					int readSize = reader.Read(buff, 0, buff.Length);

					if (readSize <= 0)
						break;

					ProcMain.WriteLog("Position: " + reader.Position);

					byte[] data = SCommon.GetSubBytes(buff, 0, readSize);
					Common.MaskGZDataEng.Transpose(data);
					Send(data, 2);
				}
			}

			ProcMain.WriteLog("OK!");
		}

		private void Send(byte[] data, int command)
		{
			for (int tryCount = 1; ; tryCount++)
			{
				ProcMain.WriteLog("TRY-COUNT = " + tryCount);

				try
				{
					TrySend(data, command);
					return;
				}
				catch (Exception e)
				{
					ProcMain.WriteLog(e);
				}

				Thread.Sleep(2000); // 失敗したので待ち
			}
		}

		private void TrySend(byte[] data, int command)
		{
			using (WorkingDir wd = new WorkingDir())
			{
				string resFile = wd.MakePath();

				HTTPClient hc = new HTTPClient(Url, resFile);

				hc.AddHeader("X-Cookie", SCommon.Hex.ToString(data));
				hc.AddHeader("X-Tea", command.ToString());

				hc.Get();

				if (!File.Exists(resFile))
					throw new Exception("resFile does not exist");

				if (new FileInfo(resFile).Length == 0)
					throw new Exception("resFile is empty");
			}
		}
	}
}
