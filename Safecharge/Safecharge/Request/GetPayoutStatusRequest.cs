using Safecharge.Model.Common;
using Safecharge.Request.Common;
using Safecharge.Utils;
using Safecharge.Utils.Enum;

namespace Safecharge.Request
{
    // Extending Safecharge properties per https://docs.nuvei.com/api/main/indexMain_v1_0.html?json#getPayoutStatus
    /// <summary>
    /// Request to get payment's status.
    /// </summary>
    public class GetPayoutStatusRequest : SafechargeRequest
    {
        /// <summary>
        /// Empty constructor used for mapping from config file.
        /// </summary>
        public GetPayoutStatusRequest() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GetPayoutStatusRequest"/> with the required parameters.
        /// </summary>
        /// <param name="merchantInfo">Merchant's data (E.g. secret key, the merchant id, the merchant site id, etc.)</param>
        public GetPayoutStatusRequest(
            MerchantInfo merchantInfo)
            : base(merchantInfo, ChecksumOrderMapping.ApiBasicChecksumMapping)
        {
            this.RequestUri = this.CreateRequestUri(ApiConstants.GetPayoutStatusRequestUrl);
        }
    }
}
