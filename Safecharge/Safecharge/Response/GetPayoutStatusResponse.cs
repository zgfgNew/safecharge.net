using Safecharge.Response.Common;

namespace Safecharge.Response
{
    // Extending Safecharge properties per https://docs.nuvei.com/api/main/indexMain_v1_0.html?json#getPayoutStatus
    /// <summary>
    /// Response received from the SafeCharge's servers to the <see cref="Request.GetPayoutStatusResponse"/>.
    /// </summary>
    public class GetPayoutStatusResponse : SafechargeResponse
    {
        public string TransactionStatus { get; set; }
        public string TransactionId { get; set; }

        public int GwExtendedErrorCode { get; set; }
        public int GwErrorCode { get; set; }
        public string GwErrorReason { get; set; }

        public int PaymentMethodErrorCode { get; set; }
        public string PaymentMethodErrorReason { get; set; }

        public string Amount { get; set; }
        public string Currency { get; set; }
    }
}
