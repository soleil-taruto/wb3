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

		public static S_MaskGZDataEng MaskGZDataEng = new S_MaskGZDataEng();

		/// <summary>
		/// サーバー・クライアントで同期する必要アリ
		/// </summary>
		public class S_MaskGZDataEng
		{
			private int GetSize(int size)
			{
				return Math.Min(300, size / 2);
			}

			private uint X;

			private void AvoidXIsZero()
			{
				this.X = this.X % 0xffffffffu + 1u;
			}

			private uint Rand()
			{
				// Xorshift-32

				this.X ^= this.X << 13;
				this.X ^= this.X >> 17;
				this.X ^= this.X << 5;

				return this.X;
			}

			private void Shuffle(int[] values)
			{
				for (int index = values.Length; 2 <= index; index--)
				{
					int a = index - 1;
					int b = (int)(this.Rand() % (uint)index);

					int tmp = values[a];
					values[a] = values[b];
					values[b] = tmp;
				}
			}

			private void Mask(byte[] data)
			{
				int size = this.GetSize(data.Length);

				for (int index = 0; index < size; index += 13)
				{
					data[index] ^= (byte)0xf5;
				}
			}

			private void Swap(byte[] data, int[] swapIdxLst)
			{
				for (int index = 0; index < swapIdxLst.Length; index++)
				{
					int a = index;
					int b = data.Length - swapIdxLst[index];

					byte tmp = data[a];
					data[a] = data[b];
					data[b] = tmp;
				}
			}

			private void Transpose(byte[] data, string seed)
			{
				int[] swapIdxLst = Enumerable.Range(1, this.GetSize(data.Length)).ToArray();

				this.X = (uint)data.Length;
				this.Rand();
				this.X ^= uint.Parse(seed);
				this.AvoidXIsZero();
				this.Shuffle(swapIdxLst);

				this.Mask(data);
				this.Swap(data, swapIdxLst);
				this.Mask(data);
			}

			public void Transpose(byte[] data)
			{
				this.Transpose(data, "2021103101");
			}
		}
	}
}
