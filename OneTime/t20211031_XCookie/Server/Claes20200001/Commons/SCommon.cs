using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Charlotte.Commons
{
	/// <summary>
	/// 共通機能・便利機能はできるだけこのクラスに集約する。
	/// </summary>
	public static class SCommon
	{
		private class S_AnonyDisposable : IDisposable
		{
			private Action Routine;

			public S_AnonyDisposable(Action routine)
			{
				this.Routine = routine;
			}

			public void Dispose()
			{
				if (this.Routine != null)
				{
					this.Routine();
					this.Routine = null;
				}
			}
		}

		public static IDisposable GetAnonyDisposable(Action routine)
		{
			return new S_AnonyDisposable(routine);
		}

		public static int Comp<T>(IList<T> a, IList<T> b, Comparison<T> comp)
		{
			int minlen = Math.Min(a.Count, b.Count);

			for (int index = 0; index < minlen; index++)
			{
				int ret = comp(a[index], b[index]);

				if (ret != 0)
					return ret;
			}
			return Comp(a.Count, b.Count);
		}

		public static int IndexOf<T>(IList<T> list, Predicate<T> match)
		{
			for (int index = 0; index < list.Count; index++)
				if (match(list[index]))
					return index;

			return -1; // not found
		}

		public static void Swap<T>(IList<T> list, int a, int b)
		{
			T tmp = list[a];
			list[a] = list[b];
			list[b] = tmp;
		}

		public static void Swap<T>(ref T a, ref T b)
		{
			T tmp = a;
			a = b;
			b = tmp;
		}

		public static byte[] EMPTY_BYTES = new byte[0];

		public static int Comp(byte a, byte b)
		{
			return (int)a - (int)b;
		}

		public static int Comp(byte[] a, byte[] b)
		{
			return Comp(a, b, Comp);
		}

		public static byte[] GetSubBytes(byte[] src, int offset, int size)
		{
			byte[] dest = new byte[size];
			Array.Copy(src, offset, dest, 0, size);
			return dest;
		}

		public static byte[] ToBytes(int value)
		{
			return ToBytes((uint)value);
		}

		public static int ToInt(byte[] src, int index = 0)
		{
			return (int)ToUInt(src, index);
		}

		public static byte[] ToBytes(uint value)
		{
			byte[] dest = new byte[4];
			ToBytes(value, dest);
			return dest;
		}

		public static void ToBytes(uint value, byte[] dest, int index = 0)
		{
			dest[index + 0] = (byte)((value >> 0) & 0xff);
			dest[index + 1] = (byte)((value >> 8) & 0xff);
			dest[index + 2] = (byte)((value >> 16) & 0xff);
			dest[index + 3] = (byte)((value >> 24) & 0xff);
		}

		public static uint ToUInt(byte[] src, int index = 0)
		{
			return
				((uint)src[index + 0] << 0) |
				((uint)src[index + 1] << 8) |
				((uint)src[index + 2] << 16) |
				((uint)src[index + 3] << 24);
		}

		/// <summary>
		/// バイト列を連結する。
		/// 例：{ BYTE_ARR_1, BYTE_ARR_2, BYTE_ARR_3 } -> BYTE_ARR_1 + BYTE_ARR_2 + BYTE_ARR_3
		/// </summary>
		/// <param name="src">バイト列の引数配列</param>
		/// <returns>連結したバイト列</returns>
		public static byte[] Join(IList<byte[]> src)
		{
			int offset = 0;

			foreach (byte[] block in src)
				offset += block.Length;

			byte[] dest = new byte[offset];
			offset = 0;

			foreach (byte[] block in src)
			{
				Array.Copy(block, 0, dest, offset, block.Length);
				offset += block.Length;
			}
			return dest;
		}

		/// <summary>
		/// バイト列を再分割可能なように連結する。
		/// 再分割するには BinTools.Split を使用すること。
		/// 例：{ BYTE_ARR_1, BYTE_ARR_2, BYTE_ARR_3 } -> SIZE(BYTE_ARR_1) + BYTE_ARR_1 + SIZE(BYTE_ARR_2) + BYTE_ARR_2 + SIZE(BYTE_ARR_3) + BYTE_ARR_3
		/// SIZE(b) は BinTools.ToBytes(b.Length) である。
		/// </summary>
		/// <param name="src">バイト列の引数配列</param>
		/// <returns>連結したバイト列</returns>
		public static byte[] SplittableJoin(IList<byte[]> src)
		{
			int offset = 0;

			foreach (byte[] block in src)
				offset += 4 + block.Length;

			byte[] dest = new byte[offset];
			offset = 0;

			foreach (byte[] block in src)
			{
				Array.Copy(ToBytes(block.Length), 0, dest, offset, 4);
				offset += 4;
				Array.Copy(block, 0, dest, offset, block.Length);
				offset += block.Length;
			}
			return dest;
		}

		/// <summary>
		/// バイト列を再分割する。
		/// </summary>
		/// <param name="src">連結したバイト列</param>
		/// <returns>再分割したバイト列の配列</returns>
		public static byte[][] Split(byte[] src)
		{
			List<byte[]> dest = new List<byte[]>();

			for (int offset = 0; offset < src.Length; )
			{
				int size = ToInt(src, offset);
				offset += 4;
				dest.Add(GetSubBytes(src, offset, size));
				offset += size;
			}
			return dest.ToArray();
		}

		public class Serializer
		{
			public static Serializer I = new Serializer();

			private Serializer()
			{ }

			/// <summary>
			/// 文字列のリストを連結してシリアライズします。
			/// シリアライズされた文字列：
			/// -- 空文字列ではない。
			/// -- 書式 == ^[0-9][-_A-Za-z0-9]*[0-9]$
			/// </summary>
			/// <param name="plainStrings">任意の文字列のリスト</param>
			/// <returns>シリアライズされた文字列</returns>
			public string Join(IList<string> plainStrings)
			{
				if (
					plainStrings == null ||
					plainStrings.Any(plainString => plainString == null)
					)
					throw new ArgumentException();

				return EncodeGzB64(SCommon.Base64.I.EncodeURL(SCommon.Compress(
					SCommon.SplittableJoin(plainStrings.Select(plainString => Encoding.UTF8.GetBytes(plainString)).ToArray())
					)));
			}

			/// <summary>
			/// シリアライズされた文字列から文字列のリストを復元します。
			/// </summary>
			/// <param name="serializedString">シリアライズされた文字列</param>
			/// <returns>元の文字列リスト</returns>
			public string[] Split(string serializedString)
			{
				if (
					serializedString == null ||
					!RegexSerializedString.IsMatch(serializedString)
					)
					throw new ArgumentException();

				return SCommon.Split(SCommon.Decompress(SCommon.Base64.I.Decode(DecodeGzB64(serializedString))))
					.Select(decodedBlock => Encoding.UTF8.GetString(decodedBlock))
					.ToArray();
			}

			private Regex RegexSerializedString = new Regex("^[0-9][-_A-Za-z0-9]*[0-9]$");

			private string EncodeGzB64(string str)
			{
				int stAn = 0;
				int edAn = 0;

				if (str.StartsWith("H4sIA")) // 先頭を圧縮
				{
					for (stAn = 1; stAn < 9; stAn++)
					{
						int i = 4 + stAn;

						if (str.Length <= i || str[i] != 'A')
							break;
					}
					str = str.Substring(4 + stAn);
				}

				// 終端を圧縮
				{
					for (edAn = 0; edAn < 9; edAn++)
					{
						int i = str.Length - 1 - edAn;

						if (i < 0 || str[i] != 'A')
							break;
					}
					str = str.Substring(0, str.Length - edAn);
				}

				return stAn + str + edAn;
			}

			private string DecodeGzB64(string str)
			{
				return
					(str[0] == '0' ? "" : "H4sI") +
					new string('A', str[0] - '0') +
					str.Substring(1, str.Length - 2) +
					new string('A', str[str.Length - 1] - '0');
			}
		}

		public static Dictionary<string, V> CreateDictionary<V>()
		{
			return new Dictionary<string, V>(new IECompString());
		}

		public static Dictionary<string, V> CreateDictionaryIgnoreCase<V>()
		{
			return new Dictionary<string, V>(new IECompStringIgnoreCase());
		}

		public const double MICRO = 1.0 / IMAX;

		private static void CheckNaN(double value)
		{
			if (double.IsNaN(value))
				throw new Exception("NaN");
		}

		public static double ToRange(double value, double minval, double maxval)
		{
			CheckNaN(value);

			return Math.Max(minval, Math.Min(maxval, value));
		}

		public static int ToInt(double value)
		{
			CheckNaN(value);

			if (value < 0.0)
				return (int)(value - 0.5);
			else
				return (int)(value + 0.5);
		}

		public static long ToLong(double value)
		{
			CheckNaN(value);

			if (value < 0.0)
				return (long)(value - 0.5);
			else
				return (long)(value + 0.5);
		}

		/// <summary>
		/// 列挙の列挙(2次元列挙)を列挙(1次元列挙)に変換する。
		/// 例：{{ A, B, C }, { D, E, F }, { G, H, I }} -> { A, B, C, D, E, F, G, H, I }
		/// 但し Concat(new X[] { AAA, BBB, CCC }) は AAA.Concat(BBB).Concat(CCC) と同じ。
		/// </summary>
		/// <typeparam name="T">要素の型</typeparam>
		/// <param name="src">列挙の列挙(2次元列挙)</param>
		/// <returns>列挙(1次元列挙)</returns>
		public static IEnumerable<T> Concat<T>(IEnumerable<IEnumerable<T>> src)
		{
			foreach (IEnumerable<T> part in src)
				foreach (T element in part)
					yield return element;
		}

		/// <summary>
		/// 列挙をゲッターメソッドでラップします。
		/// 例：{ A, B, C } -> 呼び出し毎に右の順で戻り値を返す { A, B, C, default(T), default(T), default(T), ... }
		/// </summary>
		/// <typeparam name="T">要素の型</typeparam>
		/// <param name="src">列挙</param>
		/// <returns>ゲッターメソッド</returns>
		public static Func<T> Supplier<T>(IEnumerable<T> src)
		{
			IEnumerator<T> reader = src.GetEnumerator();

			return () =>
			{
				if (reader != null)
				{
					if (reader.MoveNext())
						return reader.Current;

					reader.Dispose();
					reader = null;
				}
				return default(T);
			};
		}

		public static T DesertElement<T>(List<T> list, int index)
		{
			T ret = list[index];
			list.RemoveAt(index);
			return ret;
		}

		public static T UnaddElement<T>(List<T> list)
		{
			return DesertElement(list, list.Count - 1);
		}

		public static T FastDesertElement<T>(List<T> list, int index)
		{
			T ret = UnaddElement(list);

			if (index < list.Count)
			{
				T ret2 = list[index];
				list[index] = ret;
				ret = ret2;
			}
			return ret;
		}

		private const int IO_TRY_MAX = 10;

		public static void DeletePath(string path)
		{
			if (string.IsNullOrEmpty(path))
				throw new Exception("削除しようとしたパスは null 又は空文字列です。");

			if (File.Exists(path))
			{
				for (int trycnt = 1; ; trycnt++)
				{
					try
					{
						File.Delete(path);
					}
					catch (Exception ex)
					{
						if (IO_TRY_MAX <= trycnt)
							throw new Exception("ファイルの削除に失敗しました。" + path + "\r\n" + ex);
					}
					if (!File.Exists(path))
						break;

					if (IO_TRY_MAX <= trycnt)
						throw new Exception("ファイルの削除に失敗しました。" + path);

					ProcMain.WriteLog("ファイルの削除をリトライします。" + path);
					Thread.Sleep(trycnt * 100);
				}
			}
			else if (Directory.Exists(path))
			{
				for (int trycnt = 1; ; trycnt++)
				{
					try
					{
						Directory.Delete(path, true);
					}
					catch (Exception ex)
					{
						if (IO_TRY_MAX <= trycnt)
							throw new Exception("ディレクトリの削除に失敗しました。" + path + "\r\n" + ex);
					}
					if (!Directory.Exists(path))
						break;

					if (IO_TRY_MAX <= trycnt)
						throw new Exception("ディレクトリの削除に失敗しました。" + path);

					ProcMain.WriteLog("ディレクトリの削除をリトライします。" + path);
					Thread.Sleep(trycnt * 100);
				}
			}
		}

		public static void CreateDir(string dir)
		{
			if (string.IsNullOrEmpty(dir))
				throw new Exception("作成しようとしたディレクトリは null 又は空文字列です。");

			for (int trycnt = 1; ; trycnt++)
			{
				try
				{
					Directory.CreateDirectory(dir); // ディレクトリが存在するときは何もしない。
				}
				catch (Exception ex)
				{
					if (IO_TRY_MAX <= trycnt)
						throw new Exception("ディレクトリを作成出来ません。" + dir + "\r\n" + ex);
				}
				if (Directory.Exists(dir))
					break;

				if (IO_TRY_MAX <= trycnt)
					throw new Exception("ディレクトリを作成出来ません。" + dir);

				ProcMain.WriteLog("ディレクトリの作成をリトライします。" + dir);
				Thread.Sleep(trycnt * 100);
			}
		}

		public static string EraseExt(string path)
		{
			return Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path));
		}

		public static string ChangeRoot(string path, string oldRoot, string rootNew)
		{
			return PutYen(rootNew) + ChangeRoot(path, oldRoot);
		}

		public static string ChangeRoot(string path, string oldRoot)
		{
			oldRoot = PutYen(oldRoot);

			if (!StartsWithIgnoreCase(path, oldRoot))
				throw new Exception("パスの配下ではありません。" + oldRoot + " -> " + path);

			return path.Substring(oldRoot.Length);
		}

		public static string PutYen(string path)
		{
			return Put_INE(path, "\\");
		}

		private static string Put_INE(string str, string endPtn)
		{
			if (!str.EndsWith(endPtn))
				str += endPtn;

			return str;
		}

		/// <summary>
		/// 厳しいフルパス化
		/// </summary>
		/// <param name="path">パス</param>
		/// <returns>フルパス</returns>
		public static string MakeFullPath(string path)
		{
			if (path == null)
				throw new Exception("パスが定義されていません。(null)");

			if (path == "")
				throw new Exception("パスが定義されていません。(空文字列)");

			if (path.Replace("\u0020", "") == "")
				throw new Exception("パスが定義されていません。(空白のみ)");

			if (path.Any(chr => chr < '\u0020'))
				throw new Exception("パスに制御コードが含まれています。");

			path = Path.GetFullPath(path);

			if (path.Contains('/')) // Path.GetFullPath が '/' を '\\' に置換するはず。
				throw null;

			if (path.StartsWith("\\\\"))
				throw new Exception("ネットワークパスまたはデバイス名は使用出来ません。");

			if (path.Substring(1, 2) != ":\\") // ネットワークパスでないならローカルパスのはず。
				throw null;

			path = PutYen(path) + ".";
			path = Path.GetFullPath(path);

			return path;
		}

		/// <summary>
		/// ゆるいフルパス化
		/// </summary>
		/// <param name="path">パス</param>
		/// <returns>フルパス</returns>
		public static string ToFullPath(string path)
		{
			path = Path.GetFullPath(path);
			path = PutYen(path) + ".";
			path = Path.GetFullPath(path);

			return path;
		}

		public static byte[] Read(FileStream reader, int size)
		{
			byte[] buff = new byte[size];
			Read(reader, buff);
			return buff;
		}

		public static void Read(FileStream reader, byte[] buff, int offset = 0)
		{
			Read(reader, buff, offset, buff.Length - offset);
		}

		public static void Read(FileStream reader, byte[] buff, int offset, int count)
		{
			if (reader.Read(buff, offset, count) != count)
			{
				throw new Exception("データの途中でファイルの終端に到達しました。");
			}
		}

		public static void Write(FileStream writer, byte[] buff, int offset = 0)
		{
			writer.Write(buff, offset, buff.Length - offset);
		}

		/// <summary>
		/// 行リストをテキストに変換します。
		/// </summary>
		/// <param name="lines">行リスト</param>
		/// <returns>テキスト</returns>
		public static string LinesToText(IList<string> lines)
		{
			return lines.Count == 0 ? "" : string.Join("\r\n", lines) + "\r\n";
		}

		/// <summary>
		/// テキストを行リストに変換します。
		/// </summary>
		/// <param name="text">テキスト</param>
		/// <returns>行リスト</returns>
		public static string[] TextToLines(string text)
		{
			text = text.Replace("\r", "");

			string[] lines = text.Split('\n');

			if (1 <= lines.Length && lines[lines.Length - 1] == "")
			{
				lines = lines.Take(lines.Length - 1).ToArray();
			}
			return lines;
		}

		/// <summary>
		/// ファイル読み込みハンドルっぽいコールバック
		/// </summary>
		/// <param name="buff">読み込んだデータの書き込み先</param>
		/// <param name="offset">書き込み開始位置</param>
		/// <param name="count">書き込みサイズ</param>
		/// <returns>実際に読み込んだサイズ(1～), ～0 == これ以上読み込めない</returns>
		public delegate int Read_d(byte[] buff, int offset, int count);

		/// <summary>
		/// ファイル書き込みハンドルっぽいコールバック
		/// </summary>
		/// <param name="buff">書き込むデータの読み込み先</param>
		/// <param name="offset">読み込み開始位置</param>
		/// <param name="count">読み込みサイズ</param>
		public delegate void Write_d(byte[] buff, int offset, int count);

		public static void ReadToEnd(Read_d reader, Write_d writer)
		{
			byte[] buff = new byte[2 * 1024 * 1024];

			for (; ; )
			{
				int readSize = reader(buff, 0, buff.Length);

				if (readSize <= 0)
					break;

				writer(buff, 0, readSize);
			}
		}

		public const int IMAX = 1000000000; // 10^9
		public const long IMAX_64 = 1000000000000000000L; // 10^18

		public static int Comp(int a, int b)
		{
			if (a < b)
				return -1;

			if (a > b)
				return 1;

			return 0;
		}

		public static int Comp(long a, long b)
		{
			if (a < b)
				return -1;

			if (a > b)
				return 1;

			return 0;
		}

		public static int Comp(double a, double b)
		{
			if (a < b)
				return -1;

			if (a > b)
				return 1;

			return 0;
		}

		public static int ToRange(int value, int minval, int maxval)
		{
			return Math.Max(minval, Math.Min(maxval, value));
		}

		public static long ToRange(long value, long minval, long maxval)
		{
			return Math.Max(minval, Math.Min(maxval, value));
		}

		public static bool IsRange(int value, int minval, int maxval)
		{
			return minval <= value && value <= maxval;
		}

		public static bool IsRange(long value, long minval, long maxval)
		{
			return minval <= value && value <= maxval;
		}

		public static bool IsRange(double value, double minval, double maxval)
		{
			return minval <= value && value <= maxval;
		}

		public static int ToInt(string str, int minval, int maxval, int defval)
		{
			try
			{
				int value = int.Parse(str);

				if (value < minval || maxval < value)
					throw null;

				return value;
			}
			catch
			{
				return defval;
			}
		}

		public static long ToLong(string str, long minval, long maxval, long defval)
		{
			try
			{
				long value = long.Parse(str);

				if (value < minval || maxval < value)
					throw null;

				return value;
			}
			catch
			{
				return defval;
			}
		}

		public static string ToJString(byte[] src, bool okJpn, bool okRet, bool okTab, bool okSpc)
		{
			if (src == null)
				src = new byte[0];

			using (MemoryStream dest = new MemoryStream())
			{
				for (int index = 0; index < src.Length; index++)
				{
					byte chr = src[index];

					if (chr == 0x09) // ? '\t'
					{
						if (!okTab)
							continue;
					}
					else if (chr == 0x0a) // ? '\n'
					{
						if (!okRet)
							continue;
					}
					else if (chr < 0x20) // ? other control code
					{
						continue;
					}
					else if (chr == 0x20) // ? ' '
					{
						if (!okSpc)
							continue;
					}
					else if (chr <= 0x7e) // ? ascii
					{
						// noop
					}
					else if (0xa1 <= chr && chr <= 0xdf) // ? kana
					{
						if (!okJpn)
							continue;
					}
					else // ? 全角文字の前半 || 破損
					{
						if (!okJpn)
							continue;

						index++;

						if (src.Length <= index) // ? 後半欠損
							break;

						if (!S_JChar.I.Contains(chr, src[index])) // ? 破損
							continue;

						dest.WriteByte(chr);
						chr = src[index];
					}
					dest.WriteByte(chr);
				}
				return ENCODING_SJIS.GetString(dest.ToArray());
			}
		}

		/// <summary>
		/// SJIS(CP-932)の2バイト文字を全て返す。
		/// </summary>
		/// <returns>SJIS(CP-932)の2バイト文字の文字列</returns>
		public static string GetJChars()
		{
			return ENCODING_SJIS.GetString(GetJCharBytes().ToArray());
		}

		/// <summary>
		/// SJIS(CP-932)の2バイト文字を全て返す。
		/// </summary>
		/// <returns>SJIS(CP-932)の2バイト文字のバイト列</returns>
		public static IEnumerable<byte> GetJCharBytes()
		{
			foreach (UInt16 chr in GetJCharCodes())
			{
				yield return (byte)(chr >> 8);
				yield return (byte)(chr & 0xff);
			}
		}

		/// <summary>
		/// SJIS(CP-932)の2バイト文字を全て返す。
		/// </summary>
		/// <returns>SJIS(CP-932)の2バイト文字の列挙</returns>
		public static IEnumerable<UInt16> GetJCharCodes()
		{
			for (UInt16 chr = S_JChar.CHR_MIN; chr <= S_JChar.CHR_MAX; chr++)
				if (S_JChar.I.Contains(chr))
					yield return chr;
		}

		private class S_JChar
		{
			private static S_JChar _i = null;

			public static S_JChar I
			{
				get
				{
					if (_i == null)
						_i = new S_JChar();

					return _i;
				}
			}

			private UInt64[] ChrMap = new UInt64[0x10000 / 64];

			private S_JChar()
			{
				this.Add();
			}

			public const UInt16 CHR_MIN = 0x8140;
			public const UInt16 CHR_MAX = 0xfc4b;

			#region Add Method

			/// <summary>
			/// generated by https://github.com/stackprobe/Factory/blob/master/Labo/GenData/IsJChar.c
			/// </summary>
			private void Add()
			{
				this.Add(0x8140, 0x817e);
				this.Add(0x8180, 0x81ac);
				this.Add(0x81b8, 0x81bf);
				this.Add(0x81c8, 0x81ce);
				this.Add(0x81da, 0x81e8);
				this.Add(0x81f0, 0x81f7);
				this.Add(0x81fc, 0x81fc);
				this.Add(0x824f, 0x8258);
				this.Add(0x8260, 0x8279);
				this.Add(0x8281, 0x829a);
				this.Add(0x829f, 0x82f1);
				this.Add(0x8340, 0x837e);
				this.Add(0x8380, 0x8396);
				this.Add(0x839f, 0x83b6);
				this.Add(0x83bf, 0x83d6);
				this.Add(0x8440, 0x8460);
				this.Add(0x8470, 0x847e);
				this.Add(0x8480, 0x8491);
				this.Add(0x849f, 0x84be);
				this.Add(0x8740, 0x875d);
				this.Add(0x875f, 0x8775);
				this.Add(0x877e, 0x877e);
				this.Add(0x8780, 0x879c);
				this.Add(0x889f, 0x88fc);
				this.Add(0x8940, 0x897e);
				this.Add(0x8980, 0x89fc);
				this.Add(0x8a40, 0x8a7e);
				this.Add(0x8a80, 0x8afc);
				this.Add(0x8b40, 0x8b7e);
				this.Add(0x8b80, 0x8bfc);
				this.Add(0x8c40, 0x8c7e);
				this.Add(0x8c80, 0x8cfc);
				this.Add(0x8d40, 0x8d7e);
				this.Add(0x8d80, 0x8dfc);
				this.Add(0x8e40, 0x8e7e);
				this.Add(0x8e80, 0x8efc);
				this.Add(0x8f40, 0x8f7e);
				this.Add(0x8f80, 0x8ffc);
				this.Add(0x9040, 0x907e);
				this.Add(0x9080, 0x90fc);
				this.Add(0x9140, 0x917e);
				this.Add(0x9180, 0x91fc);
				this.Add(0x9240, 0x927e);
				this.Add(0x9280, 0x92fc);
				this.Add(0x9340, 0x937e);
				this.Add(0x9380, 0x93fc);
				this.Add(0x9440, 0x947e);
				this.Add(0x9480, 0x94fc);
				this.Add(0x9540, 0x957e);
				this.Add(0x9580, 0x95fc);
				this.Add(0x9640, 0x967e);
				this.Add(0x9680, 0x96fc);
				this.Add(0x9740, 0x977e);
				this.Add(0x9780, 0x97fc);
				this.Add(0x9840, 0x9872);
				this.Add(0x989f, 0x98fc);
				this.Add(0x9940, 0x997e);
				this.Add(0x9980, 0x99fc);
				this.Add(0x9a40, 0x9a7e);
				this.Add(0x9a80, 0x9afc);
				this.Add(0x9b40, 0x9b7e);
				this.Add(0x9b80, 0x9bfc);
				this.Add(0x9c40, 0x9c7e);
				this.Add(0x9c80, 0x9cfc);
				this.Add(0x9d40, 0x9d7e);
				this.Add(0x9d80, 0x9dfc);
				this.Add(0x9e40, 0x9e7e);
				this.Add(0x9e80, 0x9efc);
				this.Add(0x9f40, 0x9f7e);
				this.Add(0x9f80, 0x9ffc);
				this.Add(0xe040, 0xe07e);
				this.Add(0xe080, 0xe0fc);
				this.Add(0xe140, 0xe17e);
				this.Add(0xe180, 0xe1fc);
				this.Add(0xe240, 0xe27e);
				this.Add(0xe280, 0xe2fc);
				this.Add(0xe340, 0xe37e);
				this.Add(0xe380, 0xe3fc);
				this.Add(0xe440, 0xe47e);
				this.Add(0xe480, 0xe4fc);
				this.Add(0xe540, 0xe57e);
				this.Add(0xe580, 0xe5fc);
				this.Add(0xe640, 0xe67e);
				this.Add(0xe680, 0xe6fc);
				this.Add(0xe740, 0xe77e);
				this.Add(0xe780, 0xe7fc);
				this.Add(0xe840, 0xe87e);
				this.Add(0xe880, 0xe8fc);
				this.Add(0xe940, 0xe97e);
				this.Add(0xe980, 0xe9fc);
				this.Add(0xea40, 0xea7e);
				this.Add(0xea80, 0xeaa4);
				this.Add(0xed40, 0xed7e);
				this.Add(0xed80, 0xedfc);
				this.Add(0xee40, 0xee7e);
				this.Add(0xee80, 0xeeec);
				this.Add(0xeeef, 0xeefc);
				this.Add(0xfa40, 0xfa7e);
				this.Add(0xfa80, 0xfafc);
				this.Add(0xfb40, 0xfb7e);
				this.Add(0xfb80, 0xfbfc);
				this.Add(0xfc40, 0xfc4b);
			}

			#endregion

			private void Add(UInt16 bgn, UInt16 end)
			{
				for (UInt16 chr = bgn; chr <= end; chr++)
				{
					this.Add(chr);
				}
			}

			private void Add(UInt16 chr)
			{
				this.ChrMap[chr / 64] |= (UInt64)1 << (chr % 64);
			}

			public bool Contains(byte lead, byte trail)
			{
				UInt16 chr = lead;

				chr <<= 8;
				chr |= trail;

				return Contains(chr);
			}

			public bool Contains(UInt16 chr)
			{
				return (this.ChrMap[chr / 64] & (UInt64)1 << (chr % 64)) != (UInt64)0;
			}
		}

		public static RandomUnit CRandom = new RandomUnit(new S_CSPRandomNumberGenerator());

		private class S_CSPRandomNumberGenerator : RandomUnit.IRandomNumberGenerator
		{
			private RandomNumberGenerator Rng = new RNGCryptoServiceProvider();
			private byte[] Cache = new byte[4096];

			public byte[] GetBlock()
			{
				this.Rng.GetBytes(this.Cache);
				return this.Cache;
			}

			public void Dispose()
			{
				if (this.Rng != null)
				{
					this.Rng.Dispose();
					this.Rng = null;
				}
			}
		}

		public static byte[] GetSHA512(byte[] src)
		{
			using (SHA512 sha512 = SHA512.Create())
			{
				return sha512.ComputeHash(src);
			}
		}

		public static class Hex
		{
			public static string ToString(byte[] src)
			{
				StringBuilder buff = new StringBuilder(src.Length * 2);

				foreach (byte chr in src)
				{
					buff.Append(hexadecimal[chr >> 4]);
					buff.Append(hexadecimal[chr & 0x0f]);
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
				int ret = hexadecimal.IndexOf(char.ToLower(chr));

				if (ret == -1)
					throw new ArgumentException("入力文字列に含まれる文字に問題があります。");

				return ret;
			}
		}

		public static string[] EMPTY_STRINGS = new string[0];

		public static Encoding ENCODING_SJIS = Encoding.GetEncoding(932);

		public static string BINADECIMAL = "01";
		public static string OCTODECIMAL = "012234567";
		public static string DECIMAL = "0123456789";
		public static string HEXADECIMAL = "0123456789ABCDEF";
		public static string hexadecimal = "0123456789abcdef";

		public static string ALPHA = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
		public static string alpha = "abcdefghijklmnopqrstuvwxyz";
		public static string PUNCT =
			S_GetString_SJISHalfCodeRange(0x21, 0x2f) +
			S_GetString_SJISHalfCodeRange(0x3a, 0x40) +
			S_GetString_SJISHalfCodeRange(0x5b, 0x60) +
			S_GetString_SJISHalfCodeRange(0x7b, 0x7e);

		public static string ASCII = DECIMAL + ALPHA + alpha + PUNCT; // == GetString_SJISHalfCodeRange(0x21, 0x7e)
		public static string KANA = S_GetString_SJISHalfCodeRange(0xa1, 0xdf);

		public static string HALF = ASCII + KANA;

		private static string S_GetString_SJISHalfCodeRange(int codeMin, int codeMax)
		{
			byte[] buff = new byte[codeMax - codeMin + 1];

			for (int code = codeMin; code <= codeMax; code++)
			{
				buff[code - codeMin] = (byte)code;
			}
			return ENCODING_SJIS.GetString(buff);
		}

		public static string MBC_DECIMAL = S_GetString_SJISCodeRange(0x82, 0x4f, 0x58);
		public static string MBC_ALPHA = S_GetString_SJISCodeRange(0x82, 0x60, 0x79);
		public static string mbc_alpha = S_GetString_SJISCodeRange(0x82, 0x81, 0x9a);
		public static string MBC_SPACE = S_GetString_SJISCodeRange(0x81, 0x40, 0x40);
		public static string MBC_PUNCT =
			S_GetString_SJISCodeRange(0x81, 0x41, 0x7e) +
			S_GetString_SJISCodeRange(0x81, 0x80, 0xac) +
			S_GetString_SJISCodeRange(0x81, 0xb8, 0xbf) + // 集合
			S_GetString_SJISCodeRange(0x81, 0xc8, 0xce) + // 論理
			S_GetString_SJISCodeRange(0x81, 0xda, 0xe8) + // 数学
			S_GetString_SJISCodeRange(0x81, 0xf0, 0xf7) +
			S_GetString_SJISCodeRange(0x81, 0xfc, 0xfc) +
			S_GetString_SJISCodeRange(0x83, 0x9f, 0xb6) + // ギリシャ語大文字
			S_GetString_SJISCodeRange(0x83, 0xbf, 0xd6) + // ギリシャ語小文字
			S_GetString_SJISCodeRange(0x84, 0x40, 0x60) + // キリル文字大文字
			S_GetString_SJISCodeRange(0x84, 0x70, 0x7e) + // キリル文字小文字(1)
			S_GetString_SJISCodeRange(0x84, 0x80, 0x91) + // キリル文字小文字(2)
			S_GetString_SJISCodeRange(0x84, 0x9f, 0xbe) + // 枠線
			S_GetString_SJISCodeRange(0x87, 0x40, 0x5d) + // 機種依存文字(1)
			S_GetString_SJISCodeRange(0x87, 0x5f, 0x75) + // 機種依存文字(2)
			S_GetString_SJISCodeRange(0x87, 0x7e, 0x7e) + // 機種依存文字(3)
			S_GetString_SJISCodeRange(0x87, 0x80, 0x9c) + // 機種依存文字(4)
			S_GetString_SJISCodeRange(0xee, 0xef, 0xfc); // 機種依存文字(5)

		public static string MBC_CHOUONPU = S_GetString_SJISCodeRange(0x81, 0x5b, 0x5b); // 815b == 長音符 -- ひらがなとカタカナの長音符は同じ文字

		public static string MBC_HIRA = S_GetString_SJISCodeRange(0x82, 0x9f, 0xf1);
		public static string MBC_KANA =
			S_GetString_SJISCodeRange(0x83, 0x40, 0x7e) +
			S_GetString_SJISCodeRange(0x83, 0x80, 0x96) + MBC_CHOUONPU;

		private static string S_GetString_SJISCodeRange(int lead, int trailMin, int trailMax)
		{
			byte[] buff = new byte[(trailMax - trailMin + 1) * 2];

			for (int trail = trailMin; trail <= trailMax; trail++)
			{
				buff[(trail - trailMin) * 2 + 0] = (byte)lead;
				buff[(trail - trailMin) * 2 + 1] = (byte)trail;
			}
			return ENCODING_SJIS.GetString(buff);
		}

		public static int Comp(string a, string b)
		{
			// MEMO: a.CompareTo(b) -- 三すくみの一件以来今でも信用出来ないので使わない。

			return Comp(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));
		}

		public static int CompIgnoreCase(string a, string b)
		{
			return Comp(a.ToLower(), b.ToLower());
		}

		public class IECompString : IEqualityComparer<string>
		{
			public bool Equals(string a, string b)
			{
				return a == b;
			}

			public int GetHashCode(string a)
			{
				return a.GetHashCode();
			}
		}

		public class IECompStringIgnoreCase : IEqualityComparer<string>
		{
			public bool Equals(string a, string b)
			{
				return a.ToLower() == b.ToLower();
			}

			public int GetHashCode(string a)
			{
				return a.ToLower().GetHashCode();
			}
		}

		public static bool EqualsIgnoreCase(string a, string b)
		{
			return a.ToLower() == b.ToLower();
		}

		public static bool StartsWithIgnoreCase(string str, string ptn)
		{
			return str.ToLower().StartsWith(ptn.ToLower());
		}

		public static bool EndsWithIgnoreCase(string str, string ptn)
		{
			return str.ToLower().EndsWith(ptn.ToLower());
		}

		public static bool ContainsIgnoreCase(string str, string ptn)
		{
			return str.ToLower().Contains(ptn.ToLower());
		}

		public static int IndexOfIgnoreCase(string str, string ptn)
		{
			return str.ToLower().IndexOf(ptn.ToLower());
		}

		public static int IndexOfIgnoreCase(string str, char chr)
		{
			return str.ToLower().IndexOf(char.ToLower(chr));
		}

		public static int IndexOf(IList<string> strs, string str)
		{
			for (int index = 0; index < strs.Count; index++)
				if (strs[index] == str)
					return index;

			return -1; // not found
		}

		public static int IndexOfIgnoreCase(IList<string> strs, string str)
		{
			string lStr = str.ToLower();

			for (int index = 0; index < strs.Count; index++)
				if (strs[index].ToLower() == lStr)
					return index;

			return -1; // not found
		}

		/// <summary>
		/// 文字列を区切り文字で分割する。
		/// </summary>
		/// <param name="str">文字列</param>
		/// <param name="delimiters">区切り文字の集合</param>
		/// <param name="meaningFlag">区切り文字(delimiters)以外を区切り文字とするか</param>
		/// <param name="ignoreEmpty">空文字列のトークンを除去するか</param>
		/// <param name="limit">最大トークン数(1～), 0 == 無制限</param>
		/// <returns>トークン配列</returns>
		public static string[] Tokenize(string str, string delimiters, bool meaningFlag = false, bool ignoreEmpty = false, int limit = 0)
		{
			StringBuilder buff = new StringBuilder();
			List<string> tokens = new List<string>();

			foreach (char chr in str)
			{
				if (tokens.Count + 1 == limit || delimiters.Contains(chr) == meaningFlag)
				{
					buff.Append(chr);
				}
				else
				{
					if (!ignoreEmpty || buff.Length != 0)
						tokens.Add(buff.ToString());

					buff = new StringBuilder();
				}
			}
			if (!ignoreEmpty || buff.Length != 0)
				tokens.Add(buff.ToString());

			return tokens.ToArray();
		}

		public static bool HasSameChar(string str)
		{
			for (int r = 1; r < str.Length; r++)
				for (int l = 0; l < r; l++)
					if (str[l] == str[r])
						return true;

			return false;
		}

		public static string[] ParseIsland(string text, string singleTag, bool ignoreCase = false)
		{
			int start;

			if (ignoreCase)
				start = text.ToLower().IndexOf(singleTag.ToLower());
			else
				start = text.IndexOf(singleTag);

			if (start == -1)
				return null;

			int end = start + singleTag.Length;

			return new string[]
			{
				text.Substring(0, start),
				text.Substring(start, end - start),
				text.Substring(end),
			};
		}

		public static string[] ParseEnclosed(string text, string openTag, string closeTag, bool ignoreCase = false)
		{
			string[] starts = ParseIsland(text, openTag, ignoreCase);

			if (starts == null)
				return null;

			string[] ends = ParseIsland(starts[2], closeTag, ignoreCase);

			if (ends == null)
				return null;

			return new string[]
			{
				starts[0],
				starts[1],
				ends[0],
				ends[1],
				ends[2],
			};
		}

		public static byte[] Compress(byte[] src)
		{
			using (MemoryStream reader = new MemoryStream(src))
			using (MemoryStream writer = new MemoryStream())
			{
				Compress(reader, writer);
				return writer.ToArray();
			}
		}

		public static byte[] Decompress(byte[] src, int limit = -1)
		{
			using (MemoryStream reader = new MemoryStream(src))
			using (MemoryStream writer = new MemoryStream())
			{
				Decompress(reader, writer, (long)limit);
				return writer.ToArray();
			}
		}

		public static void Compress(string rFile, string wFile)
		{
			using (FileStream reader = new FileStream(rFile, FileMode.Open, FileAccess.Read))
			using (FileStream writer = new FileStream(wFile, FileMode.Create, FileAccess.Write))
			{
				Compress(reader, writer);
			}
		}

		public static void Decompress(string rFile, string wFile, long limit = -1L)
		{
			using (FileStream reader = new FileStream(rFile, FileMode.Open, FileAccess.Read))
			using (FileStream writer = new FileStream(wFile, FileMode.Create, FileAccess.Write))
			{
				Decompress(reader, writer, limit);
			}
		}

		public static void Compress(Stream reader, Stream writer)
		{
			using (GZipStream gz = new GZipStream(writer, CompressionMode.Compress))
			{
				reader.CopyTo(gz);
			}
		}

		public static void Decompress(Stream reader, Stream writer, long limit = -1L)
		{
			using (GZipStream gz = new GZipStream(reader, CompressionMode.Decompress))
			{
				if (limit == -1L)
				{
					gz.CopyTo(writer);
				}
				else
				{
					ReadToEnd(gz.Read, GetLimitedWriter(writer.Write, limit));
				}
			}
		}

		public static Write_d GetLimitedWriter(Write_d writer, long remaining)
		{
			return (buff, offset, count) =>
			{
				if (remaining < (long)count)
					throw new Exception("ストリームに書き込めるバイト数の上限を超えようとしました。");

				remaining -= (long)count;
				writer(buff, offset, count);
			};
		}

		public static string[] Batch(IList<string> commands, string workingDir = "", StartProcessWindowStyle_e winStyle = StartProcessWindowStyle_e.INVISIBLE)
		{
			using (WorkingDir wd = new WorkingDir())
			{
				string batFile = wd.GetPath("Run.bat");
				string outFile = wd.GetPath("Run.out");
				string callBatFile = wd.GetPath("Call.bat");

				File.WriteAllLines(batFile, commands, ENCODING_SJIS);
				File.WriteAllText(callBatFile, "> " + outFile + " CALL " + batFile, ENCODING_SJIS);

				StartProcess("cmd", "/c " + callBatFile, workingDir, winStyle).WaitForExit();

				// batFile 終了待ち
				// -- どうやら MSBuild が終わる前に WaitForExit() が制御を返しているっぽい。@ 2020.11.10 -- t20201110_ConfuserElsa
				{
					//int millis = 0;
					int millis = 100;

					for (; ; )
					{
						try
						{
							File.ReadAllBytes(outFile); // 読み込みテスト
							break;
						}
						catch
						{ }

						Console.WriteLine("プロセス終了待ち");

						if (millis < 2000)
							//millis++;
							millis += 100;

						Thread.Sleep(millis);
					}
				}

				return File.ReadAllLines(outFile, ENCODING_SJIS);
			}
		}

		public enum StartProcessWindowStyle_e
		{
			INVISIBLE = 1,
			MINIMIZED,
			NORMAL,
		};

		public static Process StartProcess(string file, string args, string workingDir = "", StartProcessWindowStyle_e winStyle = StartProcessWindowStyle_e.INVISIBLE)
		{
			ProcessStartInfo psi = new ProcessStartInfo();

			psi.FileName = file;
			psi.Arguments = args;
			psi.WorkingDirectory = workingDir; // 既定値 == ""

			switch (winStyle)
			{
				case StartProcessWindowStyle_e.INVISIBLE:
					psi.CreateNoWindow = true;
					psi.UseShellExecute = false;
					break;

				case StartProcessWindowStyle_e.MINIMIZED:
					psi.CreateNoWindow = false;
					psi.UseShellExecute = true;
					psi.WindowStyle = ProcessWindowStyle.Minimized;
					break;

				case StartProcessWindowStyle_e.NORMAL:
					break;

				default:
					throw null;
			}
			return Process.Start(psi);
		}

		#region Base64

		public class Base64
		{
			private static Base64 _i = null;

			public static Base64 I
			{
				get
				{
					if (_i == null)
						_i = new Base64();

					return _i;
				}
			}

			private char[] Chars;
			private byte[] CharMap;

			private const char CHAR_PADDING = '=';

			private Base64()
			{
				this.Chars = (SCommon.ALPHA + SCommon.alpha + SCommon.DECIMAL + "+/").ToArray();
				this.CharMap = new byte[(int)char.MaxValue + 1];

				for (int index = 0; index < 64; index++)
					this.CharMap[this.Chars[index]] = (byte)index;

				this.CharMap['-'] = 62; // Base64 URL Encode の 63 番目の文字
				this.CharMap['_'] = 63; // Base64 URL Encode の 64 番目の文字
			}

			/// <summary>
			/// バイト列をBase64に変換します。
			/// 出力フォーマット：
			/// -- Base64 Encode -- 但し改行無し
			/// </summary>
			/// <param name="src">バイト列</param>
			/// <returns>Base64</returns>
			public string Encode(byte[] src)
			{
				char[] dest = new char[((src.Length + 2) / 3) * 4];
				int writer = 0;
				int index = 0;
				int chr;

				while (index + 3 <= src.Length)
				{
					chr = (src[index++] & 0xff) << 16;
					chr |= (src[index++] & 0xff) << 8;
					chr |= src[index++] & 0xff;
					dest[writer++] = this.Chars[chr >> 18];
					dest[writer++] = this.Chars[(chr >> 12) & 0x3f];
					dest[writer++] = this.Chars[(chr >> 6) & 0x3f];
					dest[writer++] = this.Chars[chr & 0x3f];
				}
				if (index + 2 == src.Length)
				{
					chr = (src[index++] & 0xff) << 8;
					chr |= src[index++] & 0xff;
					dest[writer++] = this.Chars[chr >> 10];
					dest[writer++] = this.Chars[(chr >> 4) & 0x3f];
					dest[writer++] = this.Chars[(chr << 2) & 0x3c];
					dest[writer++] = CHAR_PADDING;
				}
				else if (index + 1 == src.Length)
				{
					chr = src[index++] & 0xff;
					dest[writer++] = this.Chars[chr >> 2];
					dest[writer++] = this.Chars[(chr << 4) & 0x30];
					dest[writer++] = CHAR_PADDING;
					dest[writer++] = CHAR_PADDING;
				}
				return new string(dest);
			}

			/// <summary>
			/// バイト列をBase64に変換します。
			/// 出力フォーマット：
			/// -- Base64 URL Encode
			/// </summary>
			/// <param name="src">バイト列</param>
			/// <returns>Base64</returns>
			public string EncodeURL(byte[] src)
			{
				return Encode(src)
					.Replace("=", "")
					.Replace('+', '-')
					.Replace('/', '_');
			}

			/// <summary>
			/// Base64を元のバイト列に変換します。
			/// 対応フォーマット：
			/// -- Base64 Encode -- 但し改行を含まないこと。パディング無しでも良い。
			/// -- Base64 URL Encode
			/// </summary>
			/// <param name="src">Base64</param>
			/// <returns>元のバイト列</returns>
			public byte[] Decode(string src)
			{
				while (src.Length % 4 != 0)
					src += CHAR_PADDING;

				int destSize = (src.Length / 4) * 3;

				if (destSize != 0)
				{
					if (src[src.Length - 2] == CHAR_PADDING)
					{
						destSize -= 2;
					}
					else if (src[src.Length - 1] == CHAR_PADDING)
					{
						destSize--;
					}
				}
				byte[] dest = new byte[destSize];
				int writer = 0;
				int index = 0;
				int chr;

				while (writer + 3 <= destSize)
				{
					chr = (this.CharMap[src[index++]] & 0x3f) << 18;
					chr |= (this.CharMap[src[index++]] & 0x3f) << 12;
					chr |= (this.CharMap[src[index++]] & 0x3f) << 6;
					chr |= this.CharMap[src[index++]] & 0x3f;
					dest[writer++] = (byte)(chr >> 16);
					dest[writer++] = (byte)((chr >> 8) & 0xff);
					dest[writer++] = (byte)(chr & 0xff);
				}
				if (writer + 2 == destSize)
				{
					chr = (this.CharMap[src[index++]] & 0x3f) << 10;
					chr |= (this.CharMap[src[index++]] & 0x3f) << 4;
					chr |= (this.CharMap[src[index++]] & 0x3c) >> 2;
					dest[writer++] = (byte)(chr >> 8);
					dest[writer++] = (byte)(chr & 0xff);
				}
				else if (writer + 1 == destSize)
				{
					chr = (this.CharMap[src[index++]] & 0x3f) << 2;
					chr |= (this.CharMap[src[index++]] & 0x30) >> 4;
					dest[writer++] = (byte)chr;
				}
				return dest;
			}
		}

		#endregion

		#region TimeStampToSec

		/// <summary>
		/// 日時を 1/1/1 00:00:00 からの経過秒数に変換およびその逆を行います。
		/// 日時のフォーマット
		/// -- YMMDDhhmmss
		/// -- YYMMDDhhmmss
		/// -- YYYMMDDhhmmss
		/// -- YYYYMMDDhhmmss
		/// -- YYYYYMMDDhhmmss
		/// -- YYYYYYMMDDhhmmss
		/// -- YYYYYYYMMDDhhmmss
		/// -- YYYYYYYYMMDDhhmmss
		/// -- YYYYYYYYYMMDDhhmmss -- 但し YYYYYYYYY == 100000000 ～ 922337203
		/// ---- 年の桁数は 1 ～ 9 桁
		/// 日時の範囲
		/// -- 最小 1/1/1 00:00:00
		/// -- 最大 922337203/12/31 23:59:59
		/// </summary>
		public static class TimeStampToSec
		{
			private const int YEAR_MIN = 1;
			private const int YEAR_MAX = 922337203;

			private const long TIME_STAMP_MIN = 10101000000L;
			private const long TIME_STAMP_MAX = 9223372031231235959L;

			public static long ToSec(long timeStamp)
			{
				if (timeStamp < TIME_STAMP_MIN || TIME_STAMP_MAX < timeStamp)
					return 0L;

				int s = (int)(timeStamp % 100L);
				timeStamp /= 100L;
				int i = (int)(timeStamp % 100L);
				timeStamp /= 100L;
				int h = (int)(timeStamp % 100L);
				timeStamp /= 100L;
				int d = (int)(timeStamp % 100L);
				timeStamp /= 100L;
				int m = (int)(timeStamp % 100L);
				int y = (int)(timeStamp / 100L);

				if (
					//y < YEAR_MIN || YEAR_MAX < y ||
					m < 1 || 12 < m ||
					d < 1 || 31 < d ||
					h < 0 || 23 < h ||
					i < 0 || 59 < i ||
					s < 0 || 59 < s
					)
					return 0L;

				if (m <= 2)
					y--;

				long ret = y / 400;
				ret *= 365 * 400 + 97;

				y %= 400;

				ret += y * 365;
				ret += y / 4;
				ret -= y / 100;

				if (2 < m)
				{
					ret -= 31 * 10 - 4;
					m -= 3;
					ret += (m / 5) * (31 * 5 - 2);
					m %= 5;
					ret += (m / 2) * (31 * 2 - 1);
					m %= 2;
					ret += m * 31;
				}
				else
					ret += (m - 1) * 31;

				ret += d - 1;
				ret *= 24;
				ret += h;
				ret *= 60;
				ret += i;
				ret *= 60;
				ret += s;

				return ret;
			}

			public static long ToTimeStamp(long sec)
			{
				if (sec < 0L)
					return TIME_STAMP_MIN;

				int s = (int)(sec % 60L);
				sec /= 60L;
				int i = (int)(sec % 60L);
				sec /= 60L;
				int h = (int)(sec % 24L);
				sec /= 24L;

				int day = (int)(sec % 146097);
				sec /= 146097;
				sec *= 400;
				sec++;

				if (YEAR_MAX < sec)
					return TIME_STAMP_MAX;

				int y = (int)sec;
				int m = 1;
				int d;

				day += Math.Min((day + 306) / 36524, 3);
				y += (day / 1461) * 4;
				day %= 1461;

				day += Math.Min((day + 306) / 365, 3);
				y += day / 366;
				day %= 366;

				if (60 <= day)
				{
					m += 2;
					day -= 60;
					m += (day / 153) * 5;
					day %= 153;
					m += (day / 61) * 2;
					day %= 61;
				}
				m += day / 31;
				day %= 31;
				d = day + 1;

				if (y < YEAR_MIN)
					return TIME_STAMP_MIN;

				if (YEAR_MAX < y)
					return TIME_STAMP_MAX;

				if (
					//y < YEAR_MIN || YEAR_MAX < y ||
					m < 1 || 12 < m ||
					d < 1 || 31 < d ||
					h < 0 || 23 < h ||
					m < 0 || 59 < m ||
					s < 0 || 59 < s
					)
					throw null; // never

				return
					y * 10000000000L +
					m * 100000000L +
					d * 1000000L +
					h * 10000L +
					i * 100L +
					s;
			}

			public static long ToSec(DateTime dateTime)
			{
				return ToSec(ToTimeStamp(dateTime));
			}

			public static long ToTimeStamp(DateTime dateTime)
			{
				return
					10000000000L * dateTime.Year +
					100000000L * dateTime.Month +
					1000000L * dateTime.Day +
					10000L * dateTime.Hour +
					100L * dateTime.Minute +
					dateTime.Second;
			}
		}

		#endregion

		#region SimpleDateTime

		/// <summary>
		/// 日時の範囲：1/1/1 00:00:00 ～ 922337203/12/31 23:59:59
		/// </summary>
		public struct SimpleDateTime
		{
			public readonly int Year;
			public readonly int Month;
			public readonly int Day;
			public readonly int Hour;
			public readonly int Minute;
			public readonly int Second;
			public readonly string Weekday;

			public static SimpleDateTime Now()
			{
				return new SimpleDateTime(DateTime.Now);
			}

			public static SimpleDateTime FromTimeStamp(long timeStamp)
			{
				return new SimpleDateTime(TimeStampToSec.ToSec(timeStamp));
			}

			public SimpleDateTime(DateTime dateTime)
				: this(TimeStampToSec.ToSec(dateTime))
			{ }

			public SimpleDateTime(long sec)
			{
				long timeStamp = TimeStampToSec.ToTimeStamp(sec);
				long t = timeStamp;

				this.Second = (int)(t % 100L);
				t /= 100L;
				this.Minute = (int)(t % 100L);
				t /= 100L;
				this.Hour = (int)(t % 100L);
				t /= 100L;
				this.Day = (int)(t % 100L);
				t /= 100L;
				this.Month = (int)(t % 100L);
				this.Year = (int)(t / 100L);

				this.Weekday = new string(new char[] { "月火水木金土日"[(int)(TimeStampToSec.ToSec(timeStamp) / 86400 % 7)] });
			}

			public override string ToString()
			{
				return this.ToString("{0}/{1:D2}/{2:D2} ({3}) {4:D2}:{5:D2}:{6:D2}");
			}

			public string ToString(string format)
			{
				return string.Format(format, this.Year, this.Month, this.Day, this.Weekday, this.Hour, this.Minute, this.Second);
			}

			public DateTime ToDateTime()
			{
				return new DateTime(this.Year, this.Month, this.Day, this.Hour, this.Minute, this.Second);
			}

			public long ToTimeStamp()
			{
				return
					10000000000L * this.Year +
					100000000L * this.Month +
					1000000L * this.Day +
					10000L * this.Hour +
					100L * this.Minute +
					this.Second;
			}

			public long ToSec()
			{
				return TimeStampToSec.ToSec(this.ToTimeStamp());
			}

			public static SimpleDateTime operator +(SimpleDateTime instance, long sec)
			{
				return new SimpleDateTime(instance.ToSec() + sec);
			}

			public static SimpleDateTime operator +(long sec, SimpleDateTime instance)
			{
				return new SimpleDateTime(instance.ToSec() + sec);
			}

			public static SimpleDateTime operator -(SimpleDateTime instance, long sec)
			{
				return new SimpleDateTime(instance.ToSec() - sec);
			}

			public static long operator -(SimpleDateTime a, SimpleDateTime b)
			{
				return a.ToSec() - b.ToSec();
			}

			private long GetValueForCompare()
			{
				return this.ToTimeStamp();
			}

			public static bool operator ==(SimpleDateTime a, SimpleDateTime b)
			{
				return a.GetValueForCompare() == b.GetValueForCompare();
			}

			public static bool operator !=(SimpleDateTime a, SimpleDateTime b)
			{
				return a.GetValueForCompare() != b.GetValueForCompare();
			}

			public override bool Equals(object other)
			{
				return other is SimpleDateTime && this == (SimpleDateTime)other;
			}

			public override int GetHashCode()
			{
				return this.GetValueForCompare().GetHashCode();
			}

			public static bool operator <(SimpleDateTime a, SimpleDateTime b)
			{
				return a.GetValueForCompare() < b.GetValueForCompare();
			}

			public static bool operator >(SimpleDateTime a, SimpleDateTime b)
			{
				return a.GetValueForCompare() > b.GetValueForCompare();
			}

			public static bool operator <=(SimpleDateTime a, SimpleDateTime b)
			{
				return a.GetValueForCompare() <= b.GetValueForCompare();
			}

			public static bool operator >=(SimpleDateTime a, SimpleDateTime b)
			{
				return a.GetValueForCompare() >= b.GetValueForCompare();
			}
		}

		#endregion
	}
}
