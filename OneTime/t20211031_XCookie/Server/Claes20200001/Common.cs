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

		public static double GetDistance(D2Point pt)
		{
			return Math.Sqrt(pt.X * pt.X + pt.Y * pt.Y);
		}

		public static class WonderHex
		{
			private const string WONDER_DIGITS = "abcdef9876543210";

			public static string ToString(byte[] src)
			{
				StringBuilder buff = new StringBuilder(src.Length * 2);

				foreach (byte chr in src)
				{
					buff.Append(WONDER_DIGITS[chr >> 4]);
					buff.Append(WONDER_DIGITS[chr & 0x0f]);
				}
				return buff.ToString();
			}

			public static byte[] ToBytes(string src)
			{
				if (src.Length % 2 != 0)
					throw new ArgumentException("入力文字列の長さに問題があります。");

				byte[] dest = new byte[src.Length / 2];

				for (int index = 0; index < dest.Length; index++)
				{
					int hi = To4Bit(src[index * 2 + 0]);
					int lw = To4Bit(src[index * 2 + 1]);

					dest[index] = (byte)((hi << 4) | lw);
				}
				return dest;
			}

			private static int To4Bit(char chr)
			{
				int ret = WONDER_DIGITS.IndexOf(char.ToLower(chr));

				if (ret == -1)
					throw new ArgumentException("入力文字列に含まれる文字に問題があります。");

				return ret;
			}
		}
	}
}
