using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Charlotte.Commons;

namespace Charlotte
{
	public static class Consts
	{
		public const string SERVER_STOP_EVENT_NAME = ProcMain.APP_IDENT + "_SERVER_STOP_EVENT";

		public static readonly string[] HP80_HostNames = new string[]
		{
			"127.0.0.1", // テスト用
			"localhost", // テスト用
			"ccsp.mydns.jp",
		};

		public const int GeTunnelPortNo = 8080;
		public const int HTT_PortNo = 58946;
	}
}
