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
			Test01a("https://www.google.com");
			Test01a("https://www.youtube.com");
			Test01a("https://www.amazon.co.jp");
		}

		private void Test01a(string url)
		{
			const string RES_FILE = @"C:\temp\Test01a.dat";

			HTTPClient hc = new HTTPClient(url, RES_FILE);
			hc.Get();
			string contentType = hc.ResHeaders["Content-Type"];
			string[] charsetParts = contentType == null ? null : SCommon.ParseIsland(contentType, "charset=", true);
			string charset = charsetParts == null ? "none" : charsetParts[2].Trim();
			Console.WriteLine(charset); // cout
			Encoding encoding;

			// charset -> encoding
			// 他の文字セットがあれば追加すること。
			if (SCommon.EqualsIgnoreCase(charset, "Shift_JIS"))
				encoding = SCommon.ENCODING_SJIS;
			else if (SCommon.EqualsIgnoreCase(charset, "ISO-8859-1"))
				encoding = Encoding.GetEncoding(28591);
			else
				encoding = Encoding.UTF8;

			Console.WriteLine(encoding); // cout
			string resBodyText = encoding.GetString(File.ReadAllBytes(RES_FILE));
			//Console.WriteLine(resBodyText); // cout
			File.WriteAllText(Common.NextOutputPath() + ".txt", resBodyText, Encoding.UTF8);

			SCommon.DeletePath(RES_FILE);
		}
	}
}
