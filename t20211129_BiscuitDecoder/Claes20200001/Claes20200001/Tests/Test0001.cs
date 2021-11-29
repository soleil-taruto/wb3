using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using Charlotte.Commons;

namespace Charlotte.Tests
{
	public class Test0001
	{
		public void Test01()
		{
			Test01_a("");
			Test01_a("1");
			Test01_a("12");
			Test01_a("123");
			Test01_a("1234");
			Test01_a("12345");
			Test01_a(SCommon.DECIMAL);
			Test01_a(SCommon.ALPHA);
			Test01_a(SCommon.MBC_HIRA);

			for (int testcnt = 0; testcnt < 1000; testcnt++)
			{
				string str = new string(Enumerable.Range(1, SCommon.CRandom.GetRange(1, 1000))
					.Select(dummy => SCommon.CRandom.ChooseOne(SCommon.HALF.ToCharArray()))
					.ToArray());

				Test01_a(str);
			}

			Common.Pause();
		}

		private void Test01_a(string str)
		{
			string enc = Common.ZEnc(str); // エンコード
			string dec = Common.ZEnc(enc); // 復元

			Console.WriteLine("< " + str);
			Console.WriteLine("* " + enc);
			Console.WriteLine("> " + dec);

			if (str != dec)
				throw null; // bug
		}
	}
}
