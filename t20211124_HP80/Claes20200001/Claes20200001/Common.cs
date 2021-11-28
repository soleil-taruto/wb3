using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;
using Charlotte.Commons;

namespace Charlotte
{
	public static class Common
	{
		public static void Pause()
		{
			Console.WriteLine("Press ENTER key.");
			Console.ReadLine();
		}

		#region GetOutputDir

		private static string GOD_Dir;

		public static string GetOutputDir()
		{
			if (GOD_Dir == null)
				GOD_Dir = GetOutputDir_Main();

			return GOD_Dir;
		}

		private static string GetOutputDir_Main()
		{
			for (int c = 1; c <= 999; c++)
			{
				string dir = "C:\\" + c;

				if (
					!Directory.Exists(dir) &&
					!File.Exists(dir)
					)
				{
					SCommon.CreateDir(dir);
					//SCommon.Batch(new string[] { "START " + dir });
					return dir;
				}
			}
			throw new Exception("C:\\1 ～ 999 は使用できません。");
		}

		public static void OpenOutputDir()
		{
			SCommon.Batch(new string[] { "START " + GetOutputDir() });
		}

		public static void OpenOutputDirIfCreated()
		{
			if (GOD_Dir != null)
			{
				OpenOutputDir();
			}
		}

		private static int NOP_Count = 0;

		public static string NextOutputPath()
		{
			return Path.Combine(GetOutputDir(), (++NOP_Count).ToString("D4"));
		}

		#endregion

		#region ZEnc

		public static string ZEnc(string str)
		{
			str = ZEncP(str, 1, 2);
			str = ZEncP(str, 2, 3);
			str = ZEncP(str, 3, 4);

			return str;
		}

		public static string ZDec(string str)
		{
			str = ZEncP(str, 3, 4);
			str = ZEncP(str, 2, 3);
			str = ZEncP(str, 1, 2);

			return str;
		}

		private static string ZEncP(string str, int ss, int es)
		{
			char[] cs = str.ToCharArray();

			int s = 0;
			int e = cs.Length - 1;

			while (s < e)
			{
				SCommon.Swap(ref cs[s], ref cs[e]);

				s += ss;
				e -= es;
			}
			return new string(cs);
		}

		#endregion

		public static double GetDistance(D2Point pt)
		{
			return Math.Sqrt(pt.X * pt.X + pt.Y * pt.Y);
		}
	}
}
