using System;
using com.ktgame.iap.core;
using UnityEngine.Purchasing;

namespace com.ktgame.iap.server
{
	public class ServerPurchaseValidator : IPurchaseValidator
	{
		public event Action<string, string, string> OnValidate;

		public PurchaseState Validate(string productId, string productType, string receipt)
		{
			if (string.IsNullOrEmpty(receipt))
			{
				return PurchaseState.Canceled;
			}

			if (IsServerValidationSupported())
			{
				OnValidate?.Invoke(productId, productType, receipt);
				return PurchaseState.Pending;
			}

			return PurchaseState.Purchased;
		}

		private bool IsServerValidationSupported()
		{
			var currentAppStore = StandardPurchasingModule.Instance().appStore;
			if (currentAppStore == AppStore.GooglePlay || currentAppStore == AppStore.AppleAppStore || currentAppStore == AppStore.MacAppStore)
			{
				return true;
			}

			return false;
		}
	}
}
