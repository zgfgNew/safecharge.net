using Safecharge.Model.PaymentOptionModels;
using Safecharge.Request;
using Safecharge.Response;


// Task Asana "New PSP: Nuvei": SafeCharge API Wrapper
// See https://docs.nuvei.com/api/main/indexMain_v1_0.html?json#PaymentAPIOverview
namespace EG.SoarPay.PSP.Nuvei.SafeCharge_Wrapper
{
    /*****************************************************************************************************************************/

    public class NuveiWithdrawal : NuveiSession  // Nuvei session (as they name) for particular user and his particular payout/withdrawal, state machine keeping all the needed session data
    {
        public NuveiWithdrawal(NuveiUser user, string currency, decimal ammount, string? sessionID = null) : base(user, currency, ammount, sessionID)
        {
            PayType = PayType.Withdrawal;
            var ret = IsInit();
        }

        protected bool IsReadyForPayOut()
        {
            var ret = IsReadyForPayByOptionId();
            return ret;
        }

        public override bool IsReadyForPay()
        {
            var ret = IsReadyForPayOut();
            return ret;
        }

        // Logging

        public PayoutRequest? _PayoutRequest { get; protected set; }
        public PayoutResponse? _PayoutResponse{ get; protected set; }

        public GetPayoutStatusRequest? _GetPayoutStatusRequest { get; protected set; }
        public GetPayoutStatusResponse? _GetPayoutStatusResponse { get; protected set; }

        // SafeCharge payouts/withdrawals

        protected ResponseStatus_Ext CheckCftError()
        {
            const string errCFT = "Country does not support the CFT program";
            if ((GwErrCode == -1100 && !string.IsNullOrWhiteSpace(GwErrReason) && GwErrReason.Contains(errCFT, StringComparison.InvariantCultureIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(PayMethodErrReason) && PayMethodErrReason.Contains(errCFT, StringComparison.InvariantCultureIgnoreCase)))
                SetError("PayOutReq failed", ResponseStatus_Ext.Declined);

            return Status;
        }

        protected static async Task<ResponseStatus_Ext> PayOutReq(NuveiWithdrawal withdrawal)
        {
            if (!withdrawal.IsInit())
                return withdrawal.SetError("Withdrawal not initialized");

            if (!withdrawal.IsReadyForPayOut())
                return withdrawal.SetError("Withdrawal not ready for PayOut");

            if (string.IsNullOrWhiteSpace(withdrawal.SessionToken))
                return withdrawal.SetError("Missing SessionToken");

            var payByOptionId = new UserPaymentOption
            {
                UserPaymentOptionId = withdrawal.User.PayByOptionId.UserPaymentOptionId
            };
            var req = new PayoutRequest(withdrawal.Merchant, withdrawal.SessionToken, withdrawal.User.TokenID, withdrawal.SessionID, NuveiCommon.DecimalToString(withdrawal.ReqPay.GetAmount()), withdrawal.ReqPay.Currency, payByOptionId);
            req.DeviceDetails = withdrawal.Device;
            req.ClientRequestId = withdrawal.GetNewClientRequestID();

            //withdrawal.PrintToJsonFile(req, "PayoutRequest");  // toDo: Remove, debugging
            withdrawal._PayoutRequest = req;  // Logging

            var resp = await NuveiCommon.ReqExecutor().Payout(req);
            //withdrawal.PrintToJsonFile(resp, "PayoutResponse");  // toDo: Remove, debugging
            withdrawal._PayoutResponse = resp;  // Logging

            withdrawal.ReqID = resp.InternalRequestId;
            withdrawal.ClientReqID = resp.ClientRequestId;
            withdrawal.PayTxID = withdrawal.LastTxID = resp.TransactionId;

            withdrawal.ParseRespError(resp);
            withdrawal.ParsePayMethodError(resp.PaymentMethodErrorCode, resp.PaymentMethodErrorReason);
            withdrawal.ParseTxStatusError(resp.TransactionStatus, resp.GwErrorCode, resp.GwErrorReason, resp.GwExtendedErrorCode);
            if (withdrawal.IsSuccessful())
            {
                withdrawal.RespPay = withdrawal.ReqPay;
                withdrawal.SetApproved();
            }
            else if (withdrawal.IsError())
                withdrawal.CheckCftError();

            return withdrawal.Status;
        }

        protected static async Task<ResponseStatus_Ext> WrapPayOutReq(NuveiWithdrawal withdrawal)
        {
            var status = ResponseStatus_Ext.Error;
            try
            {
                status = await NuveiWithdrawal.PayOutReq(withdrawal);
                return status;
            }
            catch (Exception ex)
            {
                status = withdrawal.SetError("PayOutReq Exception: " + ex.Message, ResponseStatus_Ext.Exception);
                return status;
            }
        }

        // Extending Safecharge properties per https://docs.nuvei.com/api/main/indexMain_v1_0.html?json#getPayoutStatus
        protected static async Task<ResponseStatus_Ext> PayOutStatusReq(NuveiWithdrawal withdrawal)
        {
            if (!withdrawal.IsInit())
                return withdrawal.SetError("Withdrawal not initialized");

            if (!withdrawal.IsReadyForPayOut())
                return withdrawal.SetError("Withdrawal not ready for PayOut");

            if (string.IsNullOrWhiteSpace(withdrawal.ClientReqID))
                return withdrawal.SetError("Missing ClientReqID");

            var req = new GetPayoutStatusRequest(withdrawal.Merchant);
            req.ClientRequestId = withdrawal.ClientReqID;

            //withdrawal.PrintToJsonFile(req, "PayoutStatusRequest");  // toDo: Remove, debugging
            withdrawal._GetPayoutStatusRequest = req;  // Logging

            var resp = await NuveiCommon.ReqExecutor().PayoutStatus(req);
            //withdrawal.PrintToJsonFile(resp, "PayoutStatusResponse");  // toDo: Remove, debugging
            withdrawal._GetPayoutStatusResponse = resp;  // Logging

            withdrawal.ReqID = resp.InternalRequestId;
            withdrawal.LastTxID = NuveiCommon.CopyProperty(resp.TransactionId, withdrawal.LastTxID);
            withdrawal.PayTxID = NuveiCommon.CopyProperty(resp.TransactionId, withdrawal.PayTxID, false);

            withdrawal.ParseRespError(resp);
            withdrawal.ParsePayMethodError(resp.PaymentMethodErrorCode.ToString(), resp.PaymentMethodErrorReason);
            withdrawal.ParseTxStatusError(resp.TransactionStatus, resp.GwErrorCode, resp.GwErrorReason, resp.GwExtendedErrorCode);

            if (!string.IsNullOrWhiteSpace(withdrawal.TxStatus) && withdrawal.TxStatus.Equals("APPROVED", StringComparison.InvariantCultureIgnoreCase))
            {
                withdrawal.SetApproved();
                withdrawal.RespPay = new NuveiPay(resp.Currency, resp.Amount);
            }

            return withdrawal.Status;
        }

        protected static async Task<ResponseStatus_Ext> WrapPayOutStatusReq(NuveiWithdrawal withdrawal)
        {
            var status = withdrawal.Status;
            try
            {
                status = await NuveiWithdrawal.PayOutStatusReq(withdrawal);
                return status;
            }
            catch (Exception ex)
            {
                if (!withdrawal.IsApproved())
                    status = withdrawal.SetError("PayOutStatusReq Exception: " + ex.Message, ResponseStatus_Ext.Exception);

                return status;
            }
        }

        // PayOut/Withdrawal method

        public static async Task<ResponseStatus_Ext> StartWithdrawal(NuveiWithdrawal withdrawal)  // PayOut = Withdrawal
        {
            //withdrawal.PrintToJsonFile("StartWithdrawal");  // toDo: Remove, debugging
            ResponseStatus_Ext status;
            do
            {
                if (!withdrawal.IsReadyForPayOut())
                {
                    withdrawal.SetError("Withdrawal not ready for PayOut");
                    break;
                }

                // SessionTokenReq
                status = await NuveiWithdrawal.WrapSessionTokenReq(withdrawal);
                if (!withdrawal.IsSuccessful())
                {
                    withdrawal.SetError("SessionTokenReq failed");
                    break;
                }

                // PayOutReq
                status = await NuveiWithdrawal.WrapPayOutReq(withdrawal);
                if (!withdrawal.IsApproved())
                    withdrawal.SetError("PayOutReq failed");

                if (!withdrawal.IsFailed())
                    status = await CheckWithdrawal(withdrawal);
            } while (false);

            //withdrawal.PrintToJsonFile("StartWithdrawal-Result");  // toDo: Remove, debugging
            var ret = withdrawal.IsApproved();
            return withdrawal.Status;
        }

        public static async Task<ResponseStatus_Ext> CheckWithdrawal(NuveiWithdrawal withdrawal, string? clientReqID = null)
        {
            //deposit.PrintToJsonFile("CheckWithdrawal");  // toDo: Remove, debugging
            ResponseStatus_Ext status;
            do
            {
                if (!withdrawal.IsReadyForPayOut())
                {
                    withdrawal.SetError("Withdrawal not ready for PayOut");
                    break;
                }

                withdrawal.ClientReqID = NuveiCommon.CopyProperty(clientReqID, withdrawal.ClientReqID, false);
                if (string.IsNullOrWhiteSpace(withdrawal.ClientReqID))
                    return withdrawal.SetError("Missing ClientReqID");

                if (!withdrawal.IsFailed())
                {
                    // PayOutStatusReq
                    status = await NuveiWithdrawal.WrapPayOutStatusReq(withdrawal);
                    if (!withdrawal.IsApproved())
                    {
                        withdrawal.SetError("PayOutStatusReq failed");
                        break;
                    }
                }

            } while (false);

            //deposit.PrintToJsonFile("CheckWithdrawal-Result");  // toDo: Remove, debugging
            var ret = withdrawal.IsApproved();
            return withdrawal.Status;
        }

    }
}