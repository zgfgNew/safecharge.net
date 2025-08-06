using Safecharge.Model.Common;
using Safecharge.Request.Common;
using Safecharge.Utils;
using Safecharge.Utils.Enum;

namespace Safecharge.Request
{
    // Extending Safecharge properties per https://docs.nuvei.com/api/advanced/indexAdvanced.html?json#deleteUPO
    /// <summary>
    ///  Request to delete user payment option id (UPO) for a specific user.
    /// </summary>
    /// <remarks>
    ///  Allows to delete the UPO to force user to re-enter the card details on the next deposit.
    ///  Use in case of Expired card error for the saved UPO.
    /// </remarks>
    public class DeleteUPORequest : SafechargeRequest
    {
        private string userTokenId;
        private string userPaymentOptionId;

        /// <summary>
        /// Empty constructor used for mapping from config file.
        /// </summary>
        public DeleteUPORequest() : base()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DeleteUPORequest."/> with the required parameters.
        /// </summary>
        /// <param name="merchantInfo">Merchant's data (E.g. secret key, the merchant id, the merchant site id, etc.)</param>
        /// <param name="clientRequestId">Use this advanced field to prevent idempotency. Use it to uniquely identify the request you are submitting. If our system receives two calls with the same clientRequestId, it refuses the second call as it will assume idempotency.</param>
        /// <param name="userTokenId">Unique ID of the user in merchant system.</param>
        /// <param name="userPaymentOptionId">Unique user payment option ID as assigned by Nuvei.</param>
        public DeleteUPORequest(
            MerchantInfo merchantInfo,
            string clientRequestId,
            string userTokenId,
            string userPaymentOptionId
            )
            : base(merchantInfo, ChecksumOrderMapping.ApiBasicChecksumMapping)
        {
            this.ClientRequestId = clientRequestId;
            this.userTokenId = userTokenId;
            this.userPaymentOptionId = userPaymentOptionId;
            this.RequestUri = this.CreateRequestUri(ApiConstants.DeleteUPOUrl);
        }

        /// <summary>
        /// Unique ID of the user in merchant system.
        /// </summary>
        public string UserTokenId
        {
            get { return this.userTokenId; }
            set
            {
                Guard.RequiresLengthBetween(value?.Length, 5, 30, nameof(this.UserTokenId));
                this.userTokenId = value;
            }
        }
        /// <summary>
        /// Unique user payment option ID as assigned by Nuvei.
        /// </summary>
        public string UserPaymentOptionId
        {
            get { return this.userPaymentOptionId; }
            set
            {
                Guard.RequiresNotNull(value, nameof(UserPaymentOptionId));
                Guard.RequiresMaxLength(value?.Length, Constants.MaxLengthStringId, nameof(this.UserPaymentOptionId));
                this.userPaymentOptionId = value;
            }
        }
    }
}
