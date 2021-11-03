using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Charlotte.Commons;

namespace Charlotte.WebServices
{
	public static class SockCommon
	{
		public enum ErrorLevel_e
		{
			INFO = 1,
			WARNING,
			FIRST_LINE_TIMEOUT,
			NETWORK,
			NETWORK_OR_SERVER_LOGIC,
			FATAL,
		}

		public static void WriteLog(ErrorLevel_e errorLevel, object message)
		{
			switch (errorLevel)
			{
				case ErrorLevel_e.INFO:
					ProcMain.WriteLog(message);
					break;

				case ErrorLevel_e.WARNING:
					ProcMain.WriteLog("[WARNING] " + message);
					break;

				case ErrorLevel_e.FIRST_LINE_TIMEOUT:
					ProcMain.WriteLog("[FIRST-LINE-TIMEOUT]");
					break;

				case ErrorLevel_e.NETWORK:
					ProcMain.WriteLog("[NETWORK] " + message);
					break;

				case ErrorLevel_e.NETWORK_OR_SERVER_LOGIC:
					ProcMain.WriteLog("[NETWORK-SERVER-LOGIC] " + message);
					break;

				case ErrorLevel_e.FATAL:
					ProcMain.WriteLog("[FATAL] " + message);
					break;

				default:
					throw null; // never
			}
		}

		public static T NB<T>(string title, Func<T> routine)
		{
#if !true
			return routine();
#else
			DateTime startedTime = DateTime.Now;
			try
			{
				return routine();
			}
			finally
			{
				double millis = (DateTime.Now - startedTime).TotalMilliseconds;

				const double MILLIS_LIMIT = 50.0;

				if (MILLIS_LIMIT < millis)
					SockCommon.WriteLog(SockCommon.ErrorLevel_e.WARNING, "NB-処理に掛かった時間 " + title + " " + Thread.CurrentThread.ManagedThreadId + " " + millis.ToString("F0"));
			}
#endif
		}

		public static UTF8Check P_UTF8Check = new UTF8Check();

		public class UTF8Check
		{
			private object[] Home = CreateByteCodeBranchTable();

			public UTF8Check()
			{
				for (byte chr = 0x20; chr <= 0x7e; chr++) // ASCII
					this.Add(new byte[] { chr });

				for (byte chr = 0xa1; chr <= 0xdf; chr++) // 半角カナ
					this.Add(Encoding.UTF8.GetBytes(SCommon.ENCODING_SJIS.GetString(new byte[] { chr })));

				foreach (char chr in SCommon.GetJChars()) // 2バイト文字
					this.Add(Encoding.UTF8.GetBytes(new string(new char[] { chr })));
			}

			private void Add(byte[] bytes)
			{
				if (bytes.Length < 1)
					throw null; // never

				object[] curr = this.Home;

				foreach (byte bChr in bytes.Take(bytes.Length - 1))
				{
					int index = (int)bChr;

					if (curr[index] == null)
						curr[index] = CreateByteCodeBranchTable();

					curr = (object[])curr[index];

					if (curr == this.Home)
						throw null; // never
				}

				{
					int index = (int)bytes[bytes.Length - 1];

					if (curr[index] != null)
						if (curr[index] != this.Home) // 同じ文字が追加されることがある。
							throw null; // never

					curr[index] = this.Home;
				}
			}

			private static object[] CreateByteCodeBranchTable()
			{
				return new object[256];
			}

			public bool Check(byte[] bytes)
			{
				object[] curr = this.Home;

				foreach (byte bChr in bytes)
				{
					curr = (object[])curr[(int)bChr];

					if (curr == null)
						return false;
				}
				return curr == this.Home;
			}
		}

		public class Critical : Semaphore
		{
			public Critical()
				: base(1)
			{ }
		}

		public class Semaphore
		{
			private int Permit;

			public Semaphore(int permit)
			{
				if (permit < 1 || SCommon.IMAX < permit)
					throw new ArgumentException();

				this.Permit = permit;
			}

			private object Enter_SYNCROOT = new object();
			private object SYNCROOT = new object();
			private int Entry = 0;

			// @ 2019.1.7
			// Monitor.Enter -> Monitor.Exit は同一スレッドでなければならないっぽい。
			// (Enter -> 別スレッドで Leave)出来るように ---> (Monitor.Wait -> Monitor.Pulse)にした。

			public void Enter()
			{
				lock (Enter_SYNCROOT)
				{
					lock (SYNCROOT)
					{
						Entry++;

						if (Entry == Permit + 1)
							Monitor.Wait(SYNCROOT);
					}
				}
			}

			public void Leave()
			{
				lock (SYNCROOT)
				{
					if (Entry == 0)
						throw null; // never

					Entry--;

					if (Entry == Permit)
						Monitor.Pulse(SYNCROOT);
				}
			}

			public void Section(Action routine)
			{
				this.Enter();
				try
				{
					routine();
				}
				finally
				{
					this.Leave();
				}
			}

			public void Unsection(Action routine)
			{
				this.Leave();
				try
				{
					routine();
				}
				finally
				{
					this.Enter();
				}
			}

			public void ContextSwitching()
			{
				this.Leave();
				this.Enter();
			}
		}

		public static class TimeWaitMonitor
		{
			// 参考値：
			// 動的ポートの数 16384 (49152 ～ 65535), TIME_WAIT-タイムアウト 4 min (240 sec) の場合 (Windowsの既定値)
			// CTR_ROT_SEC = 60
			// COUNTER_NUM = 5       -- 直近 4 ～ 5 分間の切断回数を保持
			// COUNT_LIMIT = 10000   -- 50 ミリ秒間隔で接続＆切断し続けた場合 4 分間に 4800 回 --> TIME_WAIT 数 14800 (COUNT_LIMIT + 4800) を超えない。
			// - - -
			// 動的ポートの数 64511 (1025 ～ 65535), TIME_WAIT-タイムアウト 1 min (60 sec) の場合 (動的ポート最大)
			// CTR_ROT_SEC = 30
			// COUNTER_NUM = 3       -- 直近 1 ～ 1.5 分間の切断回数を保持
			// COUNT_LIMIT = 60000   -- 50 ミリ秒間隔で接続＆切断し続けた場合 1 分間に 1200 回 --> TIME_WAIT 数 61200 (COUNT_LIMIT + 1200) を超えない。

			private const int CTR_ROT_SEC = 60;
			private const int COUNTER_NUM = 5;
			private const int COUNT_LIMIT = 10000;

			private static int ConnectedCount = 0;
			private static int[] TWCounters = new int[COUNTER_NUM]; // 直近数分間に発生した切断(TIME_WAIT)の回数
			private static int TWCounterIndex = 0;
			private static DateTime NextRotateTime = GetNextRotateTime();

			public static void Connected()
			{
				KickCounter(1, 0);

				if (COUNT_LIMIT < ConnectedCount + TWCounters.Sum()) // ? TIME_WAIT 多すぎ -> 時間当たりの接続数を制限する。-- TIME_WAIT を減らす。
				{
					SockCommon.WriteLog(SockCommon.ErrorLevel_e.WARNING, "PORT-EXHAUSTION");

					SockChannel.Critical.Unsection(() => Thread.Sleep(50));
				}
			}

			public static void Disconnect()
			{
				KickCounter(-1, 1);
			}

			private static void KickCounter(int connAdd, int twAdd)
			{
				ConnectedCount += connAdd;

				if (NextRotateTime < DateTime.Now)
				{
					TWCounterIndex++;
					TWCounterIndex %= TWCounters.Length;
					TWCounters[TWCounterIndex] = twAdd;
					NextRotateTime = GetNextRotateTime();
				}
				else
					TWCounters[TWCounterIndex] += twAdd;

				SockCommon.WriteLog(SockCommon.ErrorLevel_e.INFO, "TIME-WAIT-MONITOR: " + twAdd + ", " + ConnectedCount + " + " + TWCounters.Sum() + " = " + (ConnectedCount + TWCounters.Sum()));
			}

			private static DateTime GetNextRotateTime()
			{
				return DateTime.Now + TimeSpan.FromSeconds((double)CTR_ROT_SEC);
			}
		}
	}
}
