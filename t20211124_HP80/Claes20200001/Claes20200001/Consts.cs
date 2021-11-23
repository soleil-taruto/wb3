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

		public static string[] HTT_HostNames = new string[]
		{
			"ornithopter.ccsp.mydns.jp",
			"stackprobe.ccsp.mydns.jp",
			"cerulean.ccsp.mydns.jp",
		};

		public const int GeTunnelPortNo = 8080;
		public const int HTT_PortNo = 58946;
	}
}
