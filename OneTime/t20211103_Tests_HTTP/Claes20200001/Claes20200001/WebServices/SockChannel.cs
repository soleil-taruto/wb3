using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net.Sockets;
using Charlotte.Commons;

namespace Charlotte.WebServices
{
	public class SockChannel
	{
		public Socket Handler;

		// <---- prm

		public static SockCommon.Critical Critical = new SockCommon.Critical();

		/// <summary>
		/// 停止リクエスト
		/// プロセス終了時の利用を想定
		/// プロセス内の全てのチャネルが停止することに注意
		/// </summary>
		public static bool StopFlag = false;

		/// <summary>
		/// セッションタイムアウト日時
		/// null == INFINITE
		/// </summary>
		public DateTime? SessionTimeoutTime = null;

		/// <summary>
		/// スレッド占用タイムアウト日時
		/// null == リセット状態
		/// </summary>
		public DateTime? ThreadTimeoutTime = null;

		/// <summary>
		/// スレッド占用タイムアウト_ミリ秒
		/// -1 == INFINITE
		/// </summary>
		public static int ThreadTimeoutMillis = 100;

		/// <summary>
		/// 無通信タイムアウト_ミリ秒
		/// -1 == INFINITE
		/// </summary>
		public int P_IdleTimeoutMillis = -1;

		private void PreRecvSend()
		{
			if (StopFlag)
			{
				throw new Exception("停止リクエスト");
			}
			if (this.SessionTimeoutTime != null && this.SessionTimeoutTime.Value < DateTime.Now)
			{
				throw new Exception("セッション時間切れ");
			}
			if (this.ThreadTimeoutTime == null)
			{
				if (ThreadTimeoutMillis != -1)
					this.ThreadTimeoutTime = DateTime.Now + TimeSpan.FromMilliseconds((double)ThreadTimeoutMillis);
			}
			else if (this.ThreadTimeoutTime.Value < DateTime.Now)
			{
				SockCommon.WriteLog(SockCommon.ErrorLevel_e.INFO, "スレッド占用タイムアウト");

				this.ThreadTimeoutTime = null;
				Critical.ContextSwitching();
			}
		}

		public byte[] Recv(int size)
		{
			byte[] data = new byte[size];

			this.Recv(data);

			return data;
		}

		public void Recv(byte[] data, int offset = 0)
		{
			this.Recv(data, offset, data.Length - offset);
		}

		public void Recv(byte[] data, int offset, int size)
		{
			while (1 <= size)
			{
				int recvSize = this.TryRecv(data, offset, size);

				size -= recvSize;
				offset += recvSize;
			}
		}

		public void Recv(byte[] buff, SCommon.Write_d writer)
		{
			writer(buff, 0, TryRecv(buff, 0, buff.Length));
		}

		public int TryRecv(byte[] data, int offset, int size)
		{
			DateTime startedTime = DateTime.Now;
			int waitMillis = 0;

			for (; ; )
			{
				this.PreRecvSend();

				try
				{
					int recvSize = SockCommon.NB("recv", () => this.Handler.Receive(data, offset, size, SocketFlags.None));

					if (recvSize <= 0)
					{
						throw new Exception("受信エラー(切断)");
					}
					if (10.0 <= (DateTime.Now - startedTime).TotalSeconds) // 長い無通信時間をモニタする。
					{
						SockCommon.WriteLog(SockCommon.ErrorLevel_e.WARNING, "IDLE-RECV " + (DateTime.Now - startedTime).TotalSeconds.ToString("F3"));
					}
					return recvSize;
				}
				catch (SocketException e)
				{
					if (e.ErrorCode != 10035)
					{
						throw new Exception("受信エラー", e);
					}
				}
				if (this.P_IdleTimeoutMillis != -1 && this.P_IdleTimeoutMillis < (DateTime.Now - startedTime).TotalMilliseconds)
				{
					throw new RecvIdleTimeoutException();
				}

				this.ThreadTimeoutTime = null;

				if (waitMillis < 100)
					waitMillis++;

				Critical.Unsection(() => Thread.Sleep(waitMillis));
			}
		}

		/// <summary>
		/// 受信の無通信タイムアウト
		/// </summary>
		public class RecvIdleTimeoutException : Exception
		{ }

		public void Send(byte[] data, int offset = 0)
		{
			this.Send(data, offset, data.Length - offset);
		}

		public void Send(byte[] data, int offset, int size)
		{
			while (1 <= size)
			{
				int sentSize = this.TrySend(data, offset, Math.Min(4 * 1024 * 1024, size));

				size -= sentSize;
				offset += sentSize;
			}
		}

		private int TrySend(byte[] data, int offset, int size)
		{
			DateTime startedTime = DateTime.Now;
			int waitMillis = 0;

			for (; ; )
			{
				this.PreRecvSend();

				try
				{
					int sentSize = SockCommon.NB("send", () => this.Handler.Send(data, offset, size, SocketFlags.None));

					if (sentSize <= 0)
					{
						throw new Exception("送信エラー(切断)");
					}
					if (10.0 <= (DateTime.Now - startedTime).TotalSeconds) // 長い無通信時間をモニタする。
					{
						SockCommon.WriteLog(SockCommon.ErrorLevel_e.WARNING, "IDLE-SEND " + (DateTime.Now - startedTime).TotalSeconds.ToString("F3"));
					}
					return sentSize;
				}
				catch (SocketException e)
				{
					if (e.ErrorCode != 10035)
					{
						throw new Exception("送信エラー", e);
					}
				}
				if (this.P_IdleTimeoutMillis != -1 && this.P_IdleTimeoutMillis < (DateTime.Now - startedTime).TotalMilliseconds)
				{
					throw new Exception("送信の無通信タイムアウト");
				}

				this.ThreadTimeoutTime = null;

				if (waitMillis < 100)
					waitMillis++;

				Critical.Unsection(() => Thread.Sleep(waitMillis));
			}
		}
	}
}
