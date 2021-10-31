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

			Main4(new ArgsReader(new string[] { "80" }));
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

		private int PortNo;

		private void Main5(ArgsReader ar)
		{
			PortNo = int.Parse(ar.NextArg());

			if (PortNo < 1 || 65535 < PortNo)
				throw new Exception("Bad PortNo: " + PortNo);

			new HTTPServer()
			{
				HTTPConnected = Connected,
				PortNo = PortNo,
			}
			.Perform();
		}

		private string CurrOutputFile = null;

		private void Connected(HTTPServerChannel channel)
		{
			string sData = null;
			string sCommand = null;

			foreach (string[] pair in channel.HeaderPairs)
			{
				if (SCommon.EqualsIgnoreCase(pair[0], "X-Cookie"))
				{
					sData = pair[1];
				}
				else if (SCommon.EqualsIgnoreCase(pair[0], "X-Tea"))
				{
					sCommand = pair[1];
				}
			}

			if (sData != null && sCommand != null)
			{
				byte[] data = SCommon.Hex.ToBytes(sData);
				int command = int.Parse(sCommand);

				Common.MaskGZDataEng.Transpose(data);

				if (command == 1)
				{
					this.CurrOutputFile = Common.NextOutputPath() + "_" + SCommon.SimpleDateTime.Now().ToTimeStamp() + ".dat";
				}
				else if (command == 2)
				{
					using (FileStream writer = new FileStream(this.CurrOutputFile, FileMode.Append, FileAccess.Write))
					{
						writer.Write(data, 0, data.Length);
					}
				}
			}

			channel.ResBody = new byte[][] { Encoding.ASCII.GetBytes("<h1>HELLO HAPPY WORLD</h1>") };
		}
	}
}
