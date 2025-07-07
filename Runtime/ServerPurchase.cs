using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using com.ktgame.iap.core;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Purchasing;
using UnityEngine.Purchasing.MiniJSON;

namespace com.ktgame.iap.server
{
    public class ServerPurchase : PurchaseDecorator, IDisposable
    {
        private const string ApiToken = "YWRtaW46dW5pbW9iLmNvbS52bg==";
        private const string BaseUrl = "https://purchase-validator.ktgame.com.vn/api/v1.3";
        private const string PurchaseUrl = BaseUrl + "/purchase.php";
        private const string ConfirmPurchaseUrl = BaseUrl + "/confirm_purchase.php";
        private const string SavePurchaseRequestKey = "save_purchase_requests";
        private const string SavePurchaseInventoryKey = "save_purchase_inventories";
        private readonly IPurchase _purchaser;
        private readonly string _appId;
        private string _userId;
        private int _timeOut = 10;
        private readonly CancellationTokenSource _cancelToken;
        private readonly Dictionary<string, PurchaseRequest> _purchaseRequests = new Dictionary<string, PurchaseRequest>();
        private readonly Dictionary<string, PurchaseResponse> _purchaseInventories = new Dictionary<string, PurchaseResponse>();

        public int PurchaseRequestCount => _purchaseRequests.Count;

        public int PurchaseInventoryCount => _purchaseInventories.Count;

        public ServerPurchase(string appId, IPurchase purchaser) : base(purchaser)
        {
            _appId = appId;
            _purchaser = purchaser;
            _cancelToken = new CancellationTokenSource();
            _purchaser.PurchaseInitialized += OnPurchaseInitialized;
            _purchaseRequests.Clear();
            _purchaseInventories.Clear();
            LoadPurchaseRequest();
            LoadPurchaseInventory();
        }

        private void OnPurchaseInitialized(PurchaseInitialize obj)
        {
            if (_purchaser.ServerValidator != null)
            {
                var validator = (ServerPurchaseValidator)_purchaser.ServerValidator;
                validator.OnValidate += (productId, productType, receipt) => OnValidateServer(productId, productType, receipt).Forget();
                ProcessPendingPurchase();
            }
        }

        public void SetUserId(string userId)
        {
            _userId = userId;
        }

        public void SetRequestValidateTimeOut(int timeOut)
        {
            _timeOut = timeOut;
        }

        private async void ProcessPendingPurchase()
        {
            Debug.Log($"[{GetType().Name}] ProcessPendingPurchase PurchaseRequestCount. {PurchaseRequestCount}");
            if (PurchaseRequestCount <= 0) return;
            for (var i = _purchaseRequests.Count - 1; i >= 0; i--)
            {
                var request = _purchaseRequests.ElementAt(i);
                if (!request.Equals(default(Dictionary<string, PurchaseRequest>)))
                {
                    var rawValidation = await ValidationAsync(request.Value);
                    if (string.IsNullOrEmpty(rawValidation)) continue;

                    Debug.Log($"[{GetType().Name}] ProcessPendingPurchase RawValidation. {rawValidation}");

                    var validation = (Dictionary<string, object>)MiniJson.JsonDecode(rawValidation);
                    if (validation != null)
                    {
                        var orderId = validation.GetString("OrderId");
                        var purchaseState = validation.GetString("PurchaseState");
                        if (!string.IsNullOrEmpty(orderId) && !string.IsNullOrEmpty(purchaseState) && purchaseState.Equals("Purchased"))
                        {
                            if (!_purchaseInventories.ContainsKey(orderId))
                            {
                                RemovePurchaseRequest(orderId);
                                AddPurchaseInventory(JsonUtility.FromJson<PurchaseResponse>(rawValidation));
                                PurchaseValidInvoke(new PurchaseComplete(validation.GetString("ProductId"), rawValidation));
                            }
                            else
                            {
                                RemovePurchaseRequest(validation.GetString("OrderId"));
                            }
                        }
                    }

                    // var rawConfirm = await ConfirmAsync(validation.GetString("Id"), validation.GetString("Store"), validation.GetString("BundleId"),
                    //     validation.GetString("ProductId"), validation.GetString("OrderId"));
                    // if (string.IsNullOrEmpty(rawConfirm)) continue;
                    //
                    // Debug.Log($"[{GetType().Name}] ProcessPendingPurchase RawConfirm. {rawValidation}");
                    //
                    // RemovePurchaseRequest(validation.GetString("OrderId"));
                    // AddPurchaseInventory(JsonUtility.FromJson<PurchaseResponse>(rawConfirm));
                    // PurchaseValidInvoke(new PurchaseComplete(validation.GetString("ProductId"), rawConfirm));
                }
            }
        }

        private async UniTaskVoid OnValidateServer(string productId, string productType, string receipt)
        {
            Debug.Log($"[{GetType().Name}] OnValidateServer PurchaseRequestCount. {PurchaseRequestCount}");

            if (PurchaseRequestCount > 0) return;

            Debug.Log($"[{GetType().Name}] OnValidateServer ProductId. {productId}");

            Debug.Log($"[{GetType().Name}] OnValidateServer ProductType. {productType}");

            var rawValidation = await RequestAsync(productId, productType, receipt);
            if (string.IsNullOrEmpty(rawValidation)) return;

            Debug.Log($"[{nameof(ServerPurchase)}] OnValidateServer RawValidation. {rawValidation}");

            var validation = (Dictionary<string, object>)MiniJson.JsonDecode(rawValidation);
            if (validation != null)
            {
                var orderId = validation.GetString("OrderId");
                var purchaseState = validation.GetString("PurchaseState");
                if (!string.IsNullOrEmpty(orderId) && !string.IsNullOrEmpty(purchaseState) && purchaseState.Equals("Purchased"))
                {
                    if (!_purchaseInventories.ContainsKey(orderId))
                    {
                        RemovePurchaseRequest(orderId);
                        AddPurchaseInventory(JsonUtility.FromJson<PurchaseResponse>(rawValidation));
                        PurchaseValidInvoke(new PurchaseComplete(productId, rawValidation));
                    }
                    else
                    {
                        RemovePurchaseRequest(validation.GetString("OrderId"));
                    }
                }
            }

            // var rawConfirm = await ConfirmAsync(validation.GetString("Id"), validation.GetString("Store"), validation.GetString("BundleId"),
            //     validation.GetString("ProductId"), validation.GetString("OrderId"));
            // if (string.IsNullOrEmpty(rawConfirm)) return;
            // Debug.Log($"[{nameof(ServerPurchase)}] OnValidateServer RawConfirm. {rawValidation}");

            // RemovePurchaseRequest(validation.GetString("OrderId"));
            // AddPurchaseInventory(JsonUtility.FromJson<PurchaseResponse>(rawConfirm));
            // PurchaseValidInvoke(new PurchaseComplete(productId, rawConfirm));
        }

        private async UniTask<string> RequestAsync(string productId, string productType, string receipt)
        {
            var wrapper = (Dictionary<string, object>)MiniJson.JsonDecode(receipt);
            var store = wrapper.GetString("Store");
            Debug.Log($"[{nameof(ServerPurchase)}] Request Store. {store}");

            var payload = wrapper.GetString("Payload");
            Debug.Log($"[{nameof(ServerPurchase)}] Request Payload. {payload}");

            var transactionID = wrapper.GetString("TransactionID");
            Debug.Log($"[{nameof(ServerPurchase)}] Request TransactionID. {transactionID}");

            var purchaseRequest = new PurchaseRequest
            {
                App = _appId,
                User = _userId,
                Store = store,
                Bid = Application.identifier,
                Pid = productId,
                Type = productType,
                Order = transactionID,
                Receipt = payload
            };

            AddPurchaseRequest(purchaseRequest);
            return await ValidationAsync(purchaseRequest);
        }

        private async UniTask<string> ValidationAsync(PurchaseRequest purchaseRequest)
        {
            if (string.IsNullOrEmpty(purchaseRequest.App) || string.IsNullOrEmpty(purchaseRequest.Store) || string.IsNullOrEmpty(purchaseRequest.Bid) ||
                string.IsNullOrEmpty(purchaseRequest.Pid) || string.IsNullOrEmpty(purchaseRequest.Type) || string.IsNullOrEmpty(purchaseRequest.Order) ||
                string.IsNullOrEmpty(purchaseRequest.Receipt))
            {
                PurchaseInvalidInvoke(new PurchaseError(purchaseRequest.Pid, PurchaseStatus.InvalidRequest));
                return null;
            }

            try
            {
                var formData = new WWWForm();
                if (!string.IsNullOrEmpty(purchaseRequest.User))
                {
                    formData.AddField("user", purchaseRequest.User);
                }

                formData.AddField("app", purchaseRequest.App);
                formData.AddField("store", purchaseRequest.Store);
                formData.AddField("bid", purchaseRequest.Bid);
                formData.AddField("pid", purchaseRequest.Pid);
                formData.AddField("type", purchaseRequest.Type);
                formData.AddField("order", purchaseRequest.Order);
                formData.AddField("receipt", purchaseRequest.Receipt);
                var request = UnityWebRequest.Post(PurchaseUrl, formData);
                request.timeout = _timeOut;
                request.SetRequestHeader("Authorization", $"Bearer {ApiToken}");
                await request.SendWebRequest().WithCancellation(_cancelToken.Token);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[{nameof(ServerPurchase)}] Validation Failed. {request.error}");
                    PurchaseInvalidInvoke(new PurchaseError(purchaseRequest.Pid, PurchaseStatus.Unknown));
                }
                else
                {
                    Debug.Log($"[{nameof(ServerPurchase)}] Validation Response. {request.downloadHandler.text}");
                    var rawResponse = (Dictionary<string, object>)MiniJson.JsonDecode(request.downloadHandler.text);
                    var statusCode = rawResponse.GetLong("status");
                    switch (statusCode)
                    {
                        case 200:
                            var rawResponseData = rawResponse.GetHash("data");
                            return rawResponseData.toJson();
                        case 400:
                            PurchaseInvalidInvoke(new PurchaseError(purchaseRequest.Pid, PurchaseStatus.InvalidRequest));
                            break;
                        case 401:
                            PurchaseInvalidInvoke(new PurchaseError(purchaseRequest.Pid, PurchaseStatus.Unauthorized));
                            break;
                        case 105:
                            PurchaseInvalidInvoke(new PurchaseError(purchaseRequest.Pid, PurchaseStatus.InvalidAppId));
                            break;
                        case 106:
                            PurchaseInvalidInvoke(new PurchaseError(purchaseRequest.Pid, PurchaseStatus.ReceiptValidationFailed));
                            break;
                        case 107:
                            PurchaseInvalidInvoke(new PurchaseError(purchaseRequest.Pid, PurchaseStatus.TransactionNotMatch));
                            break;
                        case 108:
                            PurchaseInvalidInvoke(new PurchaseError(purchaseRequest.Pid, PurchaseStatus.UnsupportedStore));
                            break;
                        case 109:
                            PurchaseInvalidInvoke(new PurchaseError(purchaseRequest.Pid, PurchaseStatus.UnexpectedResponse));
                            break;
                        case 110:
                            var rawResponseDuplicateData = rawResponse.GetHash("data");
                            if (rawResponseDuplicateData != null)
                            {
                                return rawResponseDuplicateData.toJson();
                            }
                            else
                            {
                                PurchaseInvalidInvoke(new PurchaseError(purchaseRequest.Pid, PurchaseStatus.DuplicateTransaction));
                            }

                            break;
                        default:
                            PurchaseInvalidInvoke(new PurchaseError(purchaseRequest.Pid, PurchaseStatus.Unknown));
                            break;
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                if (ex.CancellationToken == _cancelToken.Token)
                {
                    Debug.Log($"[{nameof(ServerPurchase)}] Validation Time-Out. {ex.Message}");
                    PurchaseInvalidInvoke(new PurchaseError(purchaseRequest.Pid, PurchaseStatus.RequestTimeout));
                }
            }

            return null;
        }

        private async UniTask<string> ConfirmAsync(string id, string store, string bid, string pid, string order)
        {
            try
            {
                var formData = new WWWForm();
                formData.AddField("id", id);
                formData.AddField("store", store);
                formData.AddField("bid", bid);
                formData.AddField("pid", pid);
                formData.AddField("order", order);
                var request = UnityWebRequest.Post(ConfirmPurchaseUrl, formData);
                request.timeout = _timeOut;
                request.SetRequestHeader("Authorization", $"Bearer {ApiToken}");
                await request.SendWebRequest().WithCancellation(_cancelToken.Token);
                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.Log($"[{nameof(ServerPurchase)}] Confirm Failed. {request.error}");
                    PurchaseInvalidInvoke(new PurchaseError(pid, PurchaseStatus.Unknown));
                }
                else
                {
                    Debug.Log($"[{nameof(ServerPurchase)}] Confirm Response. {request.downloadHandler.text}");
                    var rawResponse = (Dictionary<string, object>)MiniJson.JsonDecode(request.downloadHandler.text);
                    var statusCode = rawResponse.GetLong("status");
                    switch (statusCode)
                    {
                        case 200:
                            var rawResponseData = rawResponse.GetHash("data");
                            return rawResponseData.toJson();
                        default:
                            PurchaseInvalidInvoke(new PurchaseError(pid, PurchaseStatus.InvalidRequest));
                            break;
                    }
                }
            }
            catch (OperationCanceledException ex)
            {
                if (ex.CancellationToken == _cancelToken.Token)
                {
                    Debug.Log($"[{nameof(ServerPurchase)}] Confirm Time-Out. {ex.Message}");
                    PurchaseInvalidInvoke(new PurchaseError(pid, PurchaseStatus.RequestTimeout));
                }
            }

            return null;
        }

        private void PurchaseInvalidInvoke(PurchaseError purchaseError)
        {
            // ServerPurchaseInvalid?.Invoke(purchaseError);
        }

        private void PurchaseValidInvoke(PurchaseComplete purchaseComplete)
        {
            Debug.Log($"[{nameof(ServerPurchase)}] PurchaseValidInvoke.\n{JsonUtility.ToJson(purchaseComplete)}");
            ServerPurchaseValidHandler(purchaseComplete);
        }

        private void LoadPurchaseRequest()
        {
            if (PlayerPrefs.HasKey(SavePurchaseRequestKey))
            {
                var jsonPurchaseRequest = PlayerPrefs.GetString(SavePurchaseRequestKey);
                // Debug.Log($"[{nameof(ServerPurchase)}] LoadPurchaseRequest RawData.\n{jsonPurchaseRequest}");
                if (!string.IsNullOrEmpty(jsonPurchaseRequest) && jsonPurchaseRequest.Length > 2)
                {
                    var serializedDictionary = SerializableDictionary.FromJson<string, PurchaseRequest>(jsonPurchaseRequest);
                    foreach (var kvp in serializedDictionary)
                    {
                        Debug.Log(
                            $"[{nameof(ServerPurchase)}] LoadPurchaseRequest.\n----------------\n{kvp.Key}\n----------------\n{JsonUtility.ToJson(kvp.Value)}\n----------------\n");
                        _purchaseRequests.Add(kvp.Key, kvp.Value);
                    }
                }
            }
        }

        private void LoadPurchaseInventory()
        {
            if (PlayerPrefs.HasKey(SavePurchaseInventoryKey))
            {
                var jsonPurchaseInventory = PlayerPrefs.GetString(SavePurchaseInventoryKey);
                // Debug.Log($"[{GetType().Name}] LoadPurchaseInventory RawData.\n{jsonPurchaseInventory}");
                if (!string.IsNullOrEmpty(jsonPurchaseInventory) && jsonPurchaseInventory.Length > 2)
                {
                    var serializedDictionary = SerializableDictionary.FromJson<string, PurchaseResponse>(jsonPurchaseInventory);
                    foreach (var kvp in serializedDictionary)
                    {
                        Debug.Log(
                            $"[{nameof(ServerPurchase)}] LoadPurchaseInventory.\n----------------\n{kvp.Key}\n----------------\n{JsonUtility.ToJson(kvp.Value)}\n----------------\n");
                        _purchaseInventories.Add(kvp.Key, kvp.Value);
                    }
                }
            }
        }

        private void SavePurchaseRequest()
        {
            Debug.Log($"[{GetType().Name}] SavePurchaseRequest RequestData.\n{PurchaseRequestCount}");
            var jsonPurchaseRequest = SerializableDictionary.ToJson(_purchaseRequests);
            Debug.Log($"[{GetType().Name}] SavePurchaseRequest JsonData.\n{jsonPurchaseRequest}");
            PlayerPrefs.SetString(SavePurchaseRequestKey, jsonPurchaseRequest);
            PlayerPrefs.Save();
        }

        private void AddPurchaseRequest(PurchaseRequest purchaseRequest)
        {
            if (purchaseRequest == null) return;
            Debug.Log($"[{GetType().Name}] AddPurchaseRequest RequestData.\n{JsonUtility.ToJson(purchaseRequest)}");
            var orderId = purchaseRequest.Order;
            if (purchaseRequest.Store == "GooglePlay")
            {
                // Debug.Log($"[{GetType().Name}] AddPurchaseRequest ReceiptData.\n{purchaseRequest.Receipt}");
                var request = (Dictionary<string, object>)MiniJson.JsonDecode(purchaseRequest.Receipt);
                if (request != null)
                {
                    var json = (Dictionary<string, object>)MiniJson.JsonDecode(request.GetString("json"));
                    if (json != null)
                    {
                        orderId = json.GetString("orderId");
                    }
                }
            }

            if (!_purchaseRequests.ContainsKey(orderId))
            {
                _purchaseRequests.Add(orderId, purchaseRequest);
                SavePurchaseRequest();
            }
        }

        private void RemovePurchaseRequest(string orderId)
        {
            Debug.Log($"[{GetType().Name}] RemovePurchaseRequest RequestCount. {orderId}");

            if (_purchaseRequests.ContainsKey(orderId))
            {
                _purchaseRequests.Remove(orderId);
            }

            Debug.Log($"[{GetType().Name}] RemovePurchaseRequest RequestCount. {PurchaseRequestCount}");

            if (PlayerPrefs.HasKey(SavePurchaseRequestKey) && PurchaseRequestCount <= 0)
            {
                PlayerPrefs.DeleteKey(SavePurchaseRequestKey);
                PlayerPrefs.Save();
                return;
            }

            SavePurchaseRequest();
        }

        private void SavePurchaseInventory()
        {
            Debug.Log($"[{GetType().Name}] SavePurchaseInventory ResponseData.\n{PurchaseInventoryCount}");
            var jsonPurchaseResponse = SerializableDictionary.ToJson(_purchaseInventories);
            Debug.Log($"[{GetType().Name}] SavePurchaseInventory JsonData.\n{jsonPurchaseResponse}");
            PlayerPrefs.SetString(SavePurchaseInventoryKey, jsonPurchaseResponse);
            PlayerPrefs.Save();
        }

        private void AddPurchaseInventory(PurchaseResponse purchaseResponse)
        {
            if (purchaseResponse == null) return;
            Debug.Log($"[{GetType().Name}] AddPurchaseInventory ResponseData.\n{JsonUtility.ToJson(purchaseResponse)}");
            if (!_purchaseInventories.ContainsKey(purchaseResponse.OrderId))
            {
                _purchaseInventories.Add(purchaseResponse.OrderId, purchaseResponse);
                SavePurchaseInventory();
            }
        }

        public void Dispose()
        {
            _cancelToken?.Dispose();
        }
    }
}