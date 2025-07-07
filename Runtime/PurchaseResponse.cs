using System;

namespace com.ktgame.iap.server
{
	[Serializable]
	public class PurchaseResponse
	{
		public string Id;
		public string UserId;
		public string Store;
		public string OrderId;
		public string BundleId;
		public string ProductId;
		public string ProductType;
		public string PurchaseTime;
		public string PurchaseState;
		public bool Sandbox;
	}
}
