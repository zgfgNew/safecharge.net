using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Safecharge.Request;
using Safecharge.Request.Common;
using Safecharge.Response;
using Safecharge.Response.Payment;
using Safecharge.Response.Transaction;
using Safecharge.Utils.Exceptions;
using Safecharge.Utils.Serialization;

namespace Safecharge
{
    /// <summary>
    /// This class provides functionality to execute SafechargeRequests directly to the SafeCharge's REST API
    /// </summary>
    /// <inheritdoc/>
    public class SafechargeRequestExecutor : ISafechargeRequestExecutor
    {
        // toDo: TxId 7854237322927583, Nuvei TransactionID 2130000005051346182, PayInReq Exception: The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.
        private static readonly TimeSpan DefaultTimeoutTimeSpan = new TimeSpan(0, 0, 30);
        private static JsonSerializerSettings SerializerSettings =>
            new JsonSerializerSettings { 
                DateFormatHandling = DateFormatHandling.MicrosoftDateFormat, 
                ContractResolver = new CamelCasePropertyNamesContractResolver()
                {
                    NamingStrategy = new CustomNamingStrategy()
                },
            };

        protected static HttpClient HttpClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="SafechargeRequestExecutor"/> with a default Safecharge's HttpClient and server information.
        /// </summary>
        public SafechargeRequestExecutor()
        {
            HttpClient = new HttpClient { Timeout = DefaultTimeoutTimeSpan };
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SafechargeRequestExecutor"/> with a configured HttpClient and server information.
        /// </summary>
        /// <param name="httpClient">httpClient to get the client's properties from</param>
        public SafechargeRequestExecutor(HttpClient httpClient)
        {
            HttpClient = httpClient;
        }

        public async Task<GetSessionTokenResponse> GetSessionToken(GetSessionTokenRequest getSessionTokenRequest)
        {
            return await this.Post<GetSessionTokenResponse, GetSessionTokenRequest>(getSessionTokenRequest);
        }

        public async Task<PaymentResponse> Payment(PaymentRequest paymentRequest)
        {
            return await this.Post<PaymentResponse, PaymentRequest>(paymentRequest);
        }

        public async Task<SettleTransactionResponse> SettleTransaction(SettleTransactionRequest settleTransactionRequest)
        {
            return await this.Post<SettleTransactionResponse, SettleTransactionRequest>(settleTransactionRequest);
        }

        public async Task<VoidTransactionResponse> VoidTransaction(VoidTransactionRequest voidTransactionRequest)
        {
            return await this.Post<VoidTransactionResponse, VoidTransactionRequest>(voidTransactionRequest);
        }

        public async Task<RefundTransactionResponse> RefundTransaction(RefundTransactionRequest refundTransactionRequest)
        {
            return await this.Post<RefundTransactionResponse, RefundTransactionRequest>(refundTransactionRequest);
        }

        public async Task<GetPaymentStatusResponse> GetPaymentStatus(GetPaymentStatusRequest getPaymentStatusRequest)
        {
            return await this.Post<GetPaymentStatusResponse, GetPaymentStatusRequest>(getPaymentStatusRequest);
        }

        public async Task<OpenOrderResponse> OpenOrder(OpenOrderRequest openOrderRequest)
        {
            return await this.Post<OpenOrderResponse, OpenOrderRequest>(openOrderRequest);
        }

        public async Task<InitPaymentResponse> InitPayment(InitPaymentRequest initPaymentRequest)
        {
            return await this.Post<InitPaymentResponse, InitPaymentRequest>(initPaymentRequest);
        }

        public async Task<Authorize3dResponse> Authorize3d(Authorize3dRequest authorize3dRequest)
        {
            return await this.Post<Authorize3dResponse, Authorize3dRequest>(authorize3dRequest);
        }

        public async Task<Verify3dResponse> Verify3d(Verify3dRequest verify3dRequest)
        {
            return await this.Post<Verify3dResponse, Verify3dRequest>(verify3dRequest);
        }

        public async Task<PayoutResponse> Payout(PayoutRequest payoutRequest)
        {
            return await this.Post<PayoutResponse, PayoutRequest>(payoutRequest);
        }

        public async Task<GetPayoutStatusResponse> PayoutStatus(GetPayoutStatusRequest payoutStatusRequest)
        {
            return await this.Post<GetPayoutStatusResponse, GetPayoutStatusRequest>(payoutStatusRequest);
        }

        public async Task<GetCardDetailsResponse> GetCardDetails(GetCardDetailsRequest request)
        {
            return await this.Post<GetCardDetailsResponse, GetCardDetailsRequest>(request);
        }

        public async Task<GetDCCResponse> GetDCCDetails(GetDCCRequest request)
        {
            return await this.Post<GetDCCResponse, GetDCCRequest>(request);
        }

        public async Task<GetMerchantPaymentMethodsResponse> GetMerchantPaymentMethods(GetMerchantPaymentMethodsRequest request)
        {
            return await this.Post<GetMerchantPaymentMethodsResponse, GetMerchantPaymentMethodsRequest>(request);
        }

        public async Task<T1> Post<T1, T2>(SafechargeBaseRequest request)
        {
            // Async Post
            if (true)
            {
                var response = await HttpClient.PostAsync(request.RequestUri.ToString(), CreateHttpContent(request));
                return await GetResponseData<T1>(response);
            }
            //else
            // Sync Post
            //{
            //    var URL = request.RequestUri.ToString();
            //    var httpReq = new HttpRequestMessage(HttpMethod.Post, URL);

            //    var content = CreateHttpContent(request);
            //    httpReq.Content = content;

            //    var httpResp = HttpClient.Send(httpReq);
            //    return await GetResponseData<T1>(httpResp);
            //}
        }

        private static HttpContent CreateHttpContent<T>(T content)
        {
            var json = JsonConvert.SerializeObject(content, SerializerSettings);
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private static async Task<T> GetResponseData<T>(HttpResponseMessage response)
        {
            try
            {
                response.EnsureSuccessStatusCode();
                var data = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(data);
            }
            catch (HttpRequestException ex)
            {
                throw new SafechargeException(ex.Message);
            }
        }
    }
}
