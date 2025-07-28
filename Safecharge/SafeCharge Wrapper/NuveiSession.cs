using Safecharge.Model.Common;
using Safecharge.Request;
using Safecharge.Response.Transaction;
using Safecharge.Response.Common;
using Safecharge.Utils;
using Safecharge.Utils.Enum;


// Task Asana "New PSP: Nuvei": SafeCharge API Wrapper
// See https://docs.nuvei.com/api/main/indexMain_v1_0.html?json#PaymentAPIOverview
namespace EG.SoarPay.PSP.Nuvei.SafeCharge_Wrapper
{
    /*****************************************************************************************************************************/

    public class NuveiSession // Nuvei session (as they name) for particular user and his particular payin/deposit/payment or payout/withdrawal, state machine keeping all the needed session data
    {
        public DeviceDetails? Device { get; protected set; }

        public MerchantInfo? Merchant { get; private set; }
        public virtual void SetMerchant(string? merchantKey, string? merchantId, string? merchantSiteId, string? serverHostURL = null, string? merchantURL = null, string? acsURL = null)
        {
            if (Merchant is null)
                Merchant = new MerchantInfo()
                {
                    MerchantKey = NuveiCommon.Merchant.MerchantKey,
                    MerchantId = NuveiCommon.Merchant.MerchantId,
                    MerchantSiteId = NuveiCommon.Merchant.MerchantSiteId,
                    ServerHost = NuveiCommon.Merchant.ServerHost,

                    HashAlgorithm = NuveiCommon.Merchant.HashAlgorithm
                };

            Merchant.MerchantKey = NuveiCommon.CopyProperty(merchantKey, Merchant.MerchantKey);
            Merchant.MerchantId = NuveiCommon.CopyProperty(merchantId, Merchant.MerchantId);
            Merchant.MerchantSiteId = NuveiCommon.CopyProperty(merchantSiteId, Merchant.MerchantSiteId);
            Merchant.ServerHost = NuveiCommon.CopyProperty(serverHostURL, Merchant.ServerHost);
        }
        public bool IsStaging() { return Merchant is not null && !string.IsNullOrWhiteSpace(Merchant.ServerHost) && Merchant.ServerHost.Contains("test", StringComparison.InvariantCultureIgnoreCase); }

        public UrlDetails? URLDetails { get; private set; }

        public void SetNotificationURLs(string? callbackURL, string? successURL = null, string? failureURL = null, string? pendingURL = null)
        {
            URLDetails.SuccessUrl = NuveiCommon.CopyProperty(successURL, URLDetails.SuccessUrl);
            URLDetails.FailureUrl = NuveiCommon.CopyProperty(failureURL, URLDetails.FailureUrl);
            URLDetails.PendingUrl = NuveiCommon.CopyProperty(pendingURL, URLDetails.PendingUrl);

            if (string.IsNullOrWhiteSpace(callbackURL))
                return;

            callbackURL.Trim();
            var cResRL = callbackURL + "CallbackDepositCRes/" + SessionID;
            URLDetails.BackUrl = NuveiCommon.CopyProperty(cResRL, URLDetails.BackUrl);

            var callbackDepositDMN = callbackURL + "CallbackDepositDMN/" + SessionID;
            URLDetails.NotificationUrl = NuveiCommon.CopyProperty(callbackDepositDMN, URLDetails.NotificationUrl);
        }

        public ByType? byType { get; private set; }
        public ByType GetPayByType()
        {
            var byType = ByType.NotDefined;
            if (User.IsReadyForPayByApm())
                byType = ByType.ByApm;
            if (User.IsReadyForPayByOptionId(IsWithdrawal()))
                byType = ByType.ByOptionId;
            else if (User.IsReadyForPayInByCard())
                byType = ByType.ByCard;
            else if (User.IsReadyForPayInByTempToken())
                byType = ByType.ByTempToken;

            return byType;
        }

        public PayType PayType { get; protected set; }

        public bool IsDeposit() { return PayType == PayType.Deposit; }
        public bool IsWithdrawal() { return PayType == PayType.Withdrawal; }

        public string? SessionToken { get; protected set; }

        public NuveiUser? User { get; protected set; }

        public string? SessionID { get; private set; }  // unique id, constant for the whole payin/payout session (ClientUniqueId in Nuvei terminology)

        protected string SetSessionID(string? sessionID = null)
        {
            if (string.IsNullOrWhiteSpace(sessionID))
                sessionID = NuveiCommon.CreateSessionID();

            Guard.RequiresNotNull(sessionID, "SessionID");
            Guard.RequiresLengthBetween(sessionID?.Length, 15, 40, "SessionID");
            SessionID = sessionID;

            URLDetails = new UrlDetails()
            {
                //BackUrl = NuveiCommon.URLDetails.BackUrl + sessionID,
                //SuccessUrl = NuveiCommon.URLDetails.SuccessUrl,
                //FailureUrl = NuveiCommon.URLDetails.FailureUrl,
                //PendingUrl = NuveiCommon.URLDetails.PendingUrl,
                //NotificationUrl = NuveiCommon.URLDetails.NotificationUrl + sessionID
            };

            var callbackURL = NuveiCommon.URLDetails.BackUrl;
            if (!string.IsNullOrWhiteSpace(callbackURL))
            {
                const string cRes = "ChallengeResult/";
                callbackURL = callbackURL.Trim();
                if (callbackURL.EndsWith(cRes))
                    callbackURL = callbackURL.Substring(0, callbackURL.Length - cRes.Length);
            }
            SetNotificationURLs(callbackURL, NuveiCommon.URLDetails.SuccessUrl, NuveiCommon.URLDetails.FailureUrl, NuveiCommon.URLDetails.PendingUrl);

            return SessionID;
        }

        public ResponseStatus_Ext Status { get; protected set; }

        public virtual bool IsApproved(bool bSale = false) { return Status == ResponseStatus_Ext.Approved; }
        public bool IsFullyApproved() { return IsApproved(true) && RespPay is not null && RespPay.IsInit() && ReqPay.Equals(RespPay); }
        public virtual bool IsRedirected() { return Status == ResponseStatus_Ext.Redirect; }
        public bool IsVoided() { return Status == ResponseStatus_Ext.Voided; }
        public virtual bool IsDeclined(bool bIncludeThreeDError = false) { return Status == ResponseStatus_Ext.Declined; }
        public bool IsSuccessful() { return Status == ResponseStatus_Ext.Success || IsApproved() || IsRedirected(); }
        public bool IsError(bool bError = false) { return Status == ResponseStatus_Ext.Error || (!bError && Status == ResponseStatus_Ext.Exception); }
        public bool IsFailed() { return IsError() || IsDeclined() || IsVoided(); }
        public bool IsSessionExpired() { return IsError(true) && !string.IsNullOrWhiteSpace(ErrReason) && ErrReason.Contains("Session expired", StringComparison.InvariantCultureIgnoreCase); }
        public bool IsGwDeclined() { return IsDeclined() && GwErrCode is not null && (GwErrCode == -1 || GwErrCode > 0) && (GwExtErrCode is null || GwExtErrCode == 0); }
        public bool IsFilterError() { return IsError(true) && GwErrCode is not null && GwErrCode == -1100 && GwExtErrCode is not null && GwExtErrCode > 0; }
        public bool IsSystemError() { return IsError(true) &&
                                             ((!string.IsNullOrWhiteSpace(ErrReason) && ErrReason.Contains("System Error", StringComparison.InvariantCultureIgnoreCase)) ||
                                              (ErrCode > 0 && ErrCode != 1069 && ErrCode != 1140 && ErrCode != 9100 && ErrCode != 9155) || (ErrType is not null && !ErrType.Equals(ErrorType.NoError) && !ErrType.Equals(ErrorType.SessionExpired)) ||
                                              (GwErrCode is not null && ((GwErrCode == -1 && GwExtErrCode is not null && GwExtErrCode == -1) || (GwErrCode < -1 && (GwExtErrCode is null || GwExtErrCode <= 0)))));} 
        public bool IsPayMethodFailed() { return IsFailed() &&
                                                (!string.IsNullOrWhiteSpace(PayMethodErrReason) || (!string.IsNullOrWhiteSpace(PayMethodErrCode) && Convert.ToInt32(PayMethodErrCode) != 0)); }

        protected ResponseStatus_Ext SetApproved()
        {
            Status = ResponseStatus_Ext.Approved;

            var ret = IsApproved();
            return Status;
        }

        public ResponseStatus_Ext SetError(string reason = "Unspecified Reason", ResponseStatus_Ext status = ResponseStatus_Ext.Error)
        {
            if (!IsFailed() || status != ResponseStatus_Ext.Error)
                Status = status;

            ErrReason = NuveiCommon.CopyProperty(reason, ErrReason, false);
            var ret = IsFailed();
            return Status;
        }

        public long? ReqID { get; protected set; }

        public string? LastTxID { get; protected set; }

        public string? RelTxID { get; protected set; }

        public string? PayTxID { get; protected set; }

        public string? VoidTxID { get; protected set; }

        public string? ClientReqID { get; protected set; }
        protected string? GetNewClientRequestID() { return SessionID + (string.IsNullOrWhiteSpace(RelTxID) ? "" : "_" + RelTxID); }

        public string? TxStatus { get; protected set; }

        public string? ErrReason { get; protected set; }

        public string? GwErrReason { get; protected set; }

        public int ErrCode { get; protected set; }

        public int? GwErrCode { get; protected set; }

        public int? GwExtErrCode { get; protected set; }

        public ErrorType? ErrType { get; protected set; }

        public string? Hint { get; protected set; }

        public string? PayMethodErrCode { get; protected set; }

        public string? PayMethodErrReason { get; protected set; }

        public NuveiPay? ReqPay { get; protected set; }

        // For WithDrawal, Nuvei provides no info for succesful PayoutResponse, currency and amount must be as requeested
        // For Deposit, amount provided by Nuvei GetPaymentStatusResponse may be smaller than requested amount
        public NuveiPay? RespPay { get; protected set; }
        public NuveiPay? GetRespPay() { return RespPay is null || !RespPay.IsInit() ? ReqPay : RespPay; }

        protected NuveiSession(NuveiUser user, string currency, decimal amount, string? sessionID = null)
        {
            NuveiCommon.ReqExecutor();

            Device = new DeviceDetails { IpAddress = "192.168.0.1" };  // Nuvei requires user's IP address but it works with fake/internal one
            Status = ResponseStatus_Ext.NotInit;
            ErrType = ErrorType.NoError;
            PayType = PayType.NotDefined;
            SetMerchant(NuveiCommon.Merchant.MerchantKey, NuveiCommon.Merchant.MerchantId, NuveiCommon.Merchant.MerchantSiteId, NuveiCommon.Merchant.ServerHost);

            var rounded = NuveiCommon.RoundDecimal(amount);
            ReqPay = new NuveiPay(currency, rounded);
            RespPay = new NuveiPay("", 0);

            User = user;
            var byType = GetPayByType();

            SetSessionID(sessionID);
            var ret = IsInit();
        }

        public bool IsInit()
        {
            var ret = NuveiCommon.ReqExecutor() is not null
                      && User.IsInit()
                      && ReqPay.IsInit()
                      && !string.IsNullOrWhiteSpace(SessionID);
            var byType = GetPayByType();
            return ret;
        }

        public bool IsReadyForPayByOptionId()  // For payins/deposits/paymentss/deposits/payments and payouts/withdrawals by Nuvei ID (user who already completed a previous successful paymin)
        {
            var ret = IsInit() && User.IsReadyForPayByOptionId(IsWithdrawal());
            return ret;
        }

        public virtual bool IsReadyForPay()
        {
            var ret = IsReadyForPayByOptionId();
            return ret;
        }

        // Logging

        public VoidTransactionRequest? _VoidTransactionRequest { get; protected set; }
        public VoidTransactionResponse? _VoidTransactionResponse { get; protected set; }

        // Helpers

        protected ResponseStatus_Ext ParseRespError(SafechargeResponse resp, bool bIgnoreSessExpired = false)
        {
            Hint = NuveiCommon.CopyProperty(resp.Hint, Hint);

            var failed = false;
            var errReason = "";
            var errCode = 0;
            if (resp.ErrCode != 0)
            {
                errCode = resp.ErrCode;
                errReason = "Response Error Code: " + errCode;
                failed = true;
            }

            ErrorType? errType = null;
            if (resp.ErrorType is not null)
            {
                errType = (ErrorType) resp.ErrorType;
                if (resp.ErrorType != ErrorType.NoError)
                {
                    errReason = "Response Error Type: " + errType.ToString();
                    failed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(resp.Reason))
            {
                errReason = resp.Reason;
                failed = true;
            }
            ErrReason = NuveiCommon.CopyProperty(errReason, ErrReason, false);

            switch (resp.Status)
            {
                case ResponseStatus.Success:
                    Status = ResponseStatus_Ext.Success;
                    break;

                case ResponseStatus.Approved:
                    Status = ResponseStatus_Ext.Approved;
                    break;

                case ResponseStatus.Redirect:
                    Status = ResponseStatus_Ext.Redirect;
                    break;

                case ResponseStatus.Declined:
                    Status = ResponseStatus_Ext.Declined;
                    break;

                case ResponseStatus.Error:
                default:
                    errReason = NuveiCommon.CopyProperty("Response Status: " + resp.Status.ToString(), errReason, false);
                    if (!bIgnoreSessExpired || (resp.Status != ResponseStatus.Error) || (errCode != 1069) || !errReason.Contains("Session expired", StringComparison.InvariantCultureIgnoreCase))
                    {
                        ErrCode = errCode;
                        ErrType = errType;
                        SetError(errReason);
                    }
                    break;
            }

            return Status;
        }

        protected ResponseStatus_Ext ParsePayMethodError(string? code, string? reason)
        {
            var failed = false;
            var str = "";

            if (!string.IsNullOrWhiteSpace(code))
            {
                code.Trim();
                if (!code.Equals("0", StringComparison.InvariantCultureIgnoreCase))
                {
                    PayMethodErrCode = code;
                    str = PayMethodErrReason = "Pay Method Error Code: " + code;
                    failed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                PayMethodErrReason = reason;
                str = "Pay Method Reason: " + reason;
                failed = true;
            }

            // toDo: Should we fail on Payment Method Error if Status was not Failed
            if (failed)
                ErrReason = NuveiCommon.CopyProperty(str, ErrReason, false);

            return Status;
        }

        protected ResponseStatus_Ext ParseTxStatusError(string? status, int code, string? reason, int extCode)
        {
            var failed = false;
            var str = "";
            if (extCode != 0)
            {
                GwExtErrCode = extCode;
                str = GwErrReason = "Gw Extended Error Code: " + GwExtErrCode;
                failed = true;
            }

            if (code != 0)
            {
                GwErrCode = code;
                str = GwErrReason = "Gw Error Code: " + GwErrCode;
                failed = true;
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                GwErrReason = reason;
                str = "Gw Reason: " + reason;
                failed = true;
            }

            TxStatus = string.IsNullOrWhiteSpace(status) ? "" : status.Trim().ToUpper();
            if (TxStatus.Equals("DECLINED", StringComparison.InvariantCultureIgnoreCase))
                SetError(str, ResponseStatus_Ext.Declined);
            else if (TxStatus.Equals("APPROVED", StringComparison.InvariantCultureIgnoreCase) ||
                     TxStatus.Equals("REDIRECT", StringComparison.InvariantCultureIgnoreCase))
                ErrReason = NuveiCommon.CopyProperty(str, ErrReason, false);
            else
            {
                TxStatus = "ERROR";
                SetError(failed ? str : "Gw Error");
            }

            return Status;
        }

        // SafeCharge payins/deposits/payments and payouts/withdrawals

        protected static async Task<ResponseStatus_Ext> SessionTokenReq(NuveiSession session)
        {
            if (!session.IsInit())
                return session.SetError("Session not initialized");

            var req = new GetSessionTokenRequest(session.Merchant);
            var resp = await NuveiCommon.ReqExecutor().GetSessionToken(req);
            session.ParseRespError(resp);

            session.SessionToken = resp.SessionToken;
            return session.Status;
        }

        protected static async Task<ResponseStatus_Ext> WrapSessionTokenReq(NuveiSession session)
        {
            var status = ResponseStatus_Ext.Error;
            try
            {
                status = await NuveiDeposit.SessionTokenReq(session);
                return status;
            }
            catch (Exception ex)
            {
                status = session.SetError("SessionTokenReq Exception: " + ex.Message, ResponseStatus_Ext.Exception);
                return status;
            }
        }

        protected static async Task<ResponseStatus_Ext> VoidPayReq(NuveiSession session)
        {
            if (!session.IsInit())
                return session.SetError("Session not initialized");

            if (!session.IsReadyForPay())
                return session.SetError("Session not ready for Pay");

            if (string.IsNullOrWhiteSpace(session.SessionToken))
                return session.SetError("Missing SessionToken");

            if (string.IsNullOrWhiteSpace(session.RelTxID))
                return session.SetError("Missing RelTransactionID");

            var pay = session.RespPay is not null ? session.RespPay : session.ReqPay;
            var req = new VoidTransactionRequest(session.Merchant, session.SessionToken, pay.Currency, NuveiCommon.DecimalToString(pay.GetAmount()), session.RelTxID);
            req.ClientUniqueId = session.SessionID;
            req.ClientRequestId = session.GetNewClientRequestID();
            //req.ProductId = session.SessionID;  // toDo: Do we need

            //session.PrintToJsonFile(req, "VoidTransactionRequest");  // toDo: Remove, debugging
            session._VoidTransactionRequest = req;  // Logging

            var resp = await NuveiCommon.ReqExecutor().VoidTransaction(req);
            //session.PrintToJsonFile(resp, "VoidTransactionResponse");  // toDo: Remove, debugging
            session._VoidTransactionResponse = resp;  // Logging

            session.ReqID = resp.InternalRequestId;
            session.ClientReqID = resp.ClientRequestId;
            session.VoidTxID = resp.TransactionId;
            session.LastTxID = NuveiCommon.CopyProperty(resp.TransactionId, session.LastTxID);

            session.ParseRespError(resp);
            session.ParsePayMethodError(resp.PaymentMethodErrorCode.ToString(), resp.PaymentMethodErrorReason);
            session.ParseTxStatusError(resp.TransactionStatus, resp.GwErrorCode, resp.GwErrorReason, resp.GwExtendedErrorCode);
            if (session.IsSuccessful())
            {
                var reason = string.Format("Voided {0} {1}", pay.Amount, pay.Currency);
                session.SetError(reason, ResponseStatus_Ext.Voided);
            }

            return session.Status;
        }

        protected static async Task<ResponseStatus_Ext> WrapVoidPayReq(NuveiSession session)
        {
            var status = ResponseStatus_Ext.Error;
            try
            {
                status = await NuveiDeposit.VoidPayReq(session);
                return status;
            }
            catch (Exception ex)
            {
                status = session.SetError("VoidPayReq Exception: " + ex.Message, ResponseStatus_Ext.Exception);
                return status;
            }
        }

        public virtual string GetFullStatus()
        {
            var str = Status.ToString();
            if (IsError())
                str += ": " + ErrReason;

            if (IsApproved())
                str += ": currency=" + RespPay.Currency + ", amount=" + RespPay.Amount;

            return str;
        }

        // Debugging

        public virtual string? EncodeToJson(bool bFormat = true)
        {
            var json = NuveiCommon.EncodeToJson(this, bFormat);
            return json;
        }

        public virtual bool PrintToJsonFile(string? title = null)
        {
            var txID = SessionID;
            var myStatus = GetFullStatus();
            var ret = NuveiCommon.PrintToJsonFile(this, title, txID, myStatus, false);
            return ret;
        }

        public virtual bool PrintToJsonFile(object? obj, string? title = null, string? info = null)
        {
            var txID = SessionID;
            var ret = NuveiCommon.PrintToJsonFile(obj, title, txID, info, false);
            return ret;
        }
    }
}