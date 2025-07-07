using System;

namespace com.ktgame.iap.server
{
	[Serializable]
	public class PurchaseRequest
	{
		public string App;
		public string Store;
		public string Bid;
		public string Pid;
		public string Type;
		public string User;
		public string Order;
		public string Receipt;
	}
}
