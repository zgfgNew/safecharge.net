using System.Diagnostics;
using Safecharge.Model.Common;
using Safecharge.Model.PaymentOptionModels;
using Safecharge.Model.PaymentOptionModels.CardModels;
using Safecharge.Model.PaymentOptionModels.ThreeDModels;
using Safecharge.Model.PaymentOptionModels.InitPayment;
using Safecharge.Request;
using Safecharge.Response;
using Safecharge.Response.Payment;
using Safecharge.Response.Transaction;
using Safecharge.Utils.Enum;


// Keep commented Nuvei namespaces (same namespaces names, ambiguites when compiling)
//using Nuvei.Model.PaymentOptionModels.ThreeDModels;
//using Sfc3DM = Safecharge.Model.PaymentOptionModels.ThreeDModels;


// Task Asana "New PSP: Nuvei": SafeCharge API Wrapper
// See https://docs.nuvei.com/api/main/indexMain_v1_0.html?json#PaymentAPIOverview
namespace EG.SoarPay.PSP.Nuvei.SafeCharge_Wrapper
{
    /*****************************************************************************************************************************/

    public class NuveiDeposit : NuveiSession  // Nuvei session (as they name) for particular user and his particular payin/deposit/payment, state machine keeping all the needed session data
    {
        public string? OrderID { get; protected set; }

        public string TxType { get; protected set; }
        public bool IsAuthThreeD() { return !string.IsNullOrWhiteSpace(TxType) && TxType.Equals("Auth3D", StringComparison.InvariantCultureIgnoreCase); }
        public bool IsAuth() { return !string.IsNullOrWhiteSpace(TxType) && TxType.Equals("Auth", StringComparison.InvariantCultureIgnoreCase); }
        public bool IsSale() { return !string.IsNullOrWhiteSpace(TxType) && TxType.Equals("Sale", StringComparison.InvariantCultureIgnoreCase); }

        public override bool IsApproved(bool bSale = false)
        {
            var bApproved = base.IsApproved();
            if (!bSale || !bApproved)
                return bApproved;

            return IsSale();
        }

        public bool IsIgnoredThreeDError()
        {            
            return IsError(true) && IsAuthThreeD() && !string.IsNullOrWhiteSpace(ErrReason) &&
                   (ErrReason.Contains("Error In 3DSecure Processing", StringComparison.InvariantCultureIgnoreCase) || ErrReason.Contains("Card number is not supported with 3DS", StringComparison.InvariantCultureIgnoreCase));
        }

        public override bool IsDeclined(bool bIncludeIgnoredThreeDError = false)
        {
            var bDeclined = base.IsDeclined();
            if (bDeclined || bZeroAuth || !bIncludeIgnoredThreeDError)
                return bDeclined;

            return IsIgnoredThreeDError();
        }

        public string? InitTxID { get; protected set; }

        public string? RedirectTxID { get; protected set; }

        public string? FinalDecision { get; protected set; }
        public bool IsCustomFraudFilterError() { return IsFilterError() && GwExtErrCode == 1116 && !string.IsNullOrWhiteSpace(FinalDecision) && FinalDecision.Contains("Reject", StringComparison.InvariantCultureIgnoreCase); }

        public string? RedirectURL { get; protected set; }

        public string? Cvv2Reply { get; protected set; }

        public string? AvsCode { get; protected set; }

        public string? ThreeDFlowAndAuthDescription { get; protected set; }

        public ThreeDResponse? ThreeD { get; protected set; }
        protected bool SetThreeD()
        {
            if (ThreeD is null)
                ThreeD = new ThreeDResponse() { Version = "2.1.0", V2supported = "true" };

            return IsThreeDSupported();
        }

        public bool IsThreeDFailed() { return IsFailed() && (ThreeD is not null && 
                                                            (!string.IsNullOrWhiteSpace(ThreeD.ThreeDReason) || (!string.IsNullOrWhiteSpace(ThreeD.ThreeDReasonId) && Convert.ToInt32(ThreeD.ThreeDReasonId) != 0))); }

        public bool IsThreeDSupported() { return ThreeD is not null && !string.IsNullOrWhiteSpace(ThreeD.V2supported) && ThreeD.V2supported.Trim().Equals("true", StringComparison.InvariantCultureIgnoreCase); }
        public bool IsChallenged() { return HasCRes(true) || (IsThreeDSupported() &&
                                            ((!string.IsNullOrWhiteSpace(ThreeD.AcsUrl) && !string.IsNullOrWhiteSpace(ThreeD.CReq)) ||
                                             (!string.IsNullOrWhiteSpace(ThreeD.ThreeDFlow) && ThreeD.ThreeDFlow.Equals("1", StringComparison.InvariantCultureIgnoreCase)) ||
                                             (!string.IsNullOrWhiteSpace(ThreeD.AcsChallengeMandated) && ThreeD.AcsChallengeMandated.Trim().Equals("Y", StringComparison.InvariantCultureIgnoreCase)) ||
                                             (!string.IsNullOrWhiteSpace(ThreeD.Flow) && ThreeD.Flow.Trim().Equals("challenge", StringComparison.InvariantCultureIgnoreCase)) ||
                                             (!string.IsNullOrWhiteSpace(ThreeD.AcquirerDecision) && ThreeD.AcquirerDecision.Trim().Equals("ChallengeRequest", StringComparison.InvariantCultureIgnoreCase)) ||
                                             (!string.IsNullOrWhiteSpace(ThreeD.Result) && 
                                              (ThreeD.Result.Trim().Equals("C", StringComparison.InvariantCultureIgnoreCase) || ThreeD.Result.Trim().Equals("D", StringComparison.InvariantCultureIgnoreCase)))));}
 
        protected ChallengeResult? _CRes { get; set; }
        public bool HasCRes(bool bValid = false)
        {
            if (_CRes is null || !IsThreeDSupported())
                return false;

            if (bValid && !_CRes.IsValid(ThreeD.ServerTransId))
                return false;

            return true;
        }


        public string? DescribeThreeDFlowAndAuth()
        {
            // Mastercard, Visa
            // 0 - No Liability Shift; Transaction processed over secured channel but payment authentication was not performed / 3DS authentication either failed or could not be performed
            // 1 - 3DS authentiacation processed by a stand-in service and classified as successful; Attempted authentication
            // 2 - 3DS authentiacation processed by a stand-in service and classified as successful; Authenticated
            // 6 - No Liability Shift; Attempted authentication with a cryptogram / Exemption from authentication or network token without 3DS / Acquirerer TRA
            // 7 - Non-recurring transactions case: No Liability Shift; 3DS authentication was processed successfully using an SCA exemption, failed or could not be attempted / Internet, not authenticated / Authentication failed or could not be performed / Error  

            // Mastercard
            // 4 - No Liability Shift; Frictionless authentication via the Mastercard Identity Check Data Only service

            // Visa, AMEX, Discover, JCB, Elo, Carte Bancaires, UnionPay International, ITMX, eftpos
            // 5 - 3DS authentiacation was successful, transaction secured by 3DS; Authentication successful

            //https://docs.nuvei.com/documentation/accept-payment/payment-page/output-parameters/?highlight=eci:
            //An Electronic Commerce Indicator (ECI) value is the result of a 3DS authentication request, returned by a Directory Server ("issuer ACS") (namely Visa, Mastercard, JCB, and American Express). Possible ECI values:

            // Visa:
            // 5 – The cardholder was successfully authenticated.
            // 6 – The issuer or cardholder does not participate in a 3D - Secure program.
            // 7 – Payment authentication was not performed.

            // Mastercard:
            // 1 – The issuer or cardholder does not participate in a 3D - Secure program.
            // 2 – The cardholder was successfully authenticated.
            // 6 – Payment authentication was not performed.
            // 7 – The cardholder was successfully authenticated for the initial MIT transaction: https://docs.nuvei.com/?p=2136.

            // https://chargebacks911.com/eci-indicator:
            // https://cardinalcommerce.com/understanding-eci-values-an-important-step-in-your-authentication-strategy/:

            // Status code Y - Authentication SUCCESSFUL– the issuer has verified the cardholder and EMV 3DS and its benefits apply to all parties:
            // 05 - Visa Secure, American Express SafeKey 2.0, Discover ProtectBuy 2.0 (and Diner's Club), JC J/ Secure 2.0, Elo 3DS
            // 02 - Mastercard Identity Check
            // 05/02 - Mastercard Identity Check - Carte Bancaires Fast'r, UnionPay International, ITMX(LSS) Local Switch Secure

            // Status code A - Authentication ATTEMPTED – authentication was not available at the issuer, but the network directory server stands in, which generates proof the merchant attempted authentication:
            // 06 - Visa Secure, American Express SafeKey 2.0, Discover ProtectBuy 2.0 (and Diner's Club), JC J/ Secure 2.0, Elo 3DS,
            // 01 - Mastercard Identity Check
            // 06/01 - Mastercard Identity Check - Carte Bancaires Fast'r, UnionPay International, ITMX(LSS) Local Switch Secure

            // Status code N - Authentication FAILED – the issuer could not authenticate the cardholder for various reasons – the information could have been entered incorrectly, the cardholder cancelled the authentication page, or other reasons

            // Status code U - Authentication NOT PERMITTED - authentication request could not be completed for various reasons – the card type is excluded from attempts, the ACS is not able to handle the authentication request message, or other reasons:
            // 07 - Visa Secure, American Express SafeKey 2.0, Discover ProtectBuy 2.0 (and Diner's Club), JC J/ Secure 2.0, Elo 3DS,
            // 00 - Mastercard Identity Check
            // 07/00 - Mastercard Identity Check - Carte Bancaires Fast'r, UnionPay International, ITMX(LSS) Local Switch Secure

            const string challange = "CHALLENGE";
            const string redirect = "REDIRECT";
            const string successful = "SUCCESSFUL";
            const string attempted = "ATTEMPTED";
            const string notPermitted = "NOT_PERMITTED";

            if (ThreeD is null)
                return null;

            var descript = string.IsNullOrWhiteSpace(ThreeD.Flow) ? null : ThreeD.Flow;

            if (string.IsNullOrWhiteSpace(descript) && HasCRes(true) && IsApproved())
                descript = challange;

            if (string.IsNullOrWhiteSpace(descript) && !string.IsNullOrWhiteSpace(ThreeD.ThreeDFlow) && ThreeD.ThreeDFlow.Equals("1", StringComparison.InvariantCultureIgnoreCase))
                descript = IsApproved() ? challange : redirect;

            if (!string.IsNullOrWhiteSpace(descript))
                descript = descript.ToUpper();

            if (string.IsNullOrWhiteSpace(ThreeD.Eci))
                return descript;

            string? authDescript = null;
            var eciVal = Convert.ToUInt32(ThreeD.Eci);
            switch (eciVal)
            {
                // Visa, American Express, Discover, Diner's Club, JC, Elo
                case 2:
                case 5:
                    authDescript = successful;
                    break;
                case 1:
                case 6:
                    authDescript = attempted;
                    break;
                case 0:
                case 7:
                    authDescript = notPermitted;
                    break;
            }

            if (!string.IsNullOrWhiteSpace(ThreeD.Result))
            {
                if (ThreeD.Result.Equals("Y", StringComparison.InvariantCulture))
                    authDescript = NuveiCommon.CopyProperty(successful, authDescript);
                else if (ThreeD.Result.Equals("A", StringComparison.InvariantCulture))
                    authDescript = NuveiCommon.CopyProperty(attempted, authDescript);
            }

            if (!string.IsNullOrWhiteSpace(descript) && !string.IsNullOrWhiteSpace(authDescript))
                descript += "-" + authDescript;
            else if (!string.IsNullOrWhiteSpace(authDescript))
                descript = authDescript;

            var liability = string.IsNullOrWhiteSpace(ThreeD.IsLiabilityOnIssuer) || Convert.ToInt32(ThreeD.IsLiabilityOnIssuer) != 1 ? null : "LiabilityOnIssuer";
            if (!string.IsNullOrWhiteSpace(descript) && !string.IsNullOrWhiteSpace(liability))
                descript += "-" + liability;
            else if (!string.IsNullOrWhiteSpace(liability))
                descript = liability;

            return descript;
        }

        protected DmnDeposit? _Dmn { get; set; }
        public DmnDeposit? GetDmn() { return _Dmn; }
        public bool HasDmn(bool bValid = false)
        {
            if (_Dmn is null || !IsReadyForPayInByApm())
                return false;

            var apMethodName = User.GetApMethodName();
            string? paymentID = User.PayByOptionId is null ? null : User.PayByOptionId.UserPaymentOptionId;
            if (bValid && !_Dmn.IsValid(apMethodName, SessionID, OrderID, ClientReqID, User.TokenID, paymentID))
                return false;

            return true;
        }

        public string? MerchantURL { get; private set; }
        public string? AcsPrefixURL { get; private set; }
        public override void SetMerchant(string? merchantKey, string? merchantId, string? merchantSiteId, string? serverHostURL = null, string? merchantURL = null, string? acsPrefixURL = null)
        {
            base.SetMerchant(merchantKey, merchantId, merchantSiteId, serverHostURL);

            MerchantURL = NuveiCommon.CopyProperty(merchantURL, MerchantURL);
            AcsPrefixURL = NuveiCommon.CopyProperty(acsPrefixURL, AcsPrefixURL);
        }

        public override bool IsRedirected()
        {
            if (HasCRes() || HasDmn())
                return true;

            var ret = base.IsRedirected();
            if (IsReadyForPayInByApm())
                return ret;

            if (!IsThreeDSupported())
                return false;

            return ret;
        }

        protected ResponseStatus_Ext SetCRes(ChallengeResult? cRes)
        {
            if (!IsRedirected() || cRes is null)
                return Status;

            _CRes = cRes;
            ThreeD.Version = NuveiCommon.CopyProperty(cRes.messageVersion, ThreeD.Version, true);
            if (!cRes.IsValid(ThreeD.ServerTransId))
                SetError("3DSv2 ChallengeResult Error", ResponseStatus_Ext.Error);
            else if (cRes.IsApproved())
                SetApproved();
            else if (cRes.IsCancelled())
                SetError("Deposit cancelled by 3DSv2 ChallengeResult", ResponseStatus_Ext.Declined);
            else
                SetError("Deposit declined by 3DSv2 ChallengeResult", ResponseStatus_Ext.Declined);

            return Status;
        }

        protected ResponseStatus_Ext SetDmn(DmnDeposit? dMN)
        {
            if (!IsRedirected() || dMN is null)
                return Status;

            _Dmn = dMN;
            TxStatus = dMN.GetStatus();

            var apMethodName = User.GetApMethodName();
            string? paymentID = User.PayByOptionId is null ? null : User.PayByOptionId.UserPaymentOptionId;
            if (!dMN.IsValid(apMethodName, SessionID, OrderID, ClientReqID, User.TokenID, paymentID))
            {
                SetError("APM DMN Error: " + dMN.comment);
                return Status;
            }

            var payTxID = dMN.GetTransactionID();
            PayTxID = NuveiCommon.CopyProperty(payTxID, PayTxID, false);
            if (dMN.IsApproved())
            {
                SetApproved();
                var currency = dMN.GetCurrency();
                var strAmmount = dMN.GetTotalAmount();
                RespPay = new NuveiPay(currency, strAmmount);
            }
            else if (dMN.IsCancelledByUser())
                SetError("Deposit cancelled by user: " + dMN.comment, ResponseStatus_Ext.Declined);
            else if (dMN.IsDeclined())
                SetError("Deposit declined by APM DMN: " + dMN.comment, ResponseStatus_Ext.Declined);
            else if (dMN.IsPending())
            {
                // toDo
            }
            else
                SetError("Deposit failed by APM DMN: " + dMN.comment);

            return Status;
        }

        // Zero-Auth per https://docs.nuvei.com/documentation/features/card-operations/zero-authorization/
        public bool bZeroAuth { get; protected set; }
		
        public NuveiDeposit(NuveiUser user, string currency, decimal amount, string? sessionID = null, string? sessionToken = null, bool bZeroAuthentication = false) : base(user, currency, amount, sessionID)
        {
            bZeroAuth = bZeroAuthentication;
            PayType = PayType.Deposit;
            SessionToken = NuveiCommon.CopyProperty(sessionToken, SessionToken);
            SetThreeD();
            SetMerchant(NuveiCommon.Merchant.MerchantKey, NuveiCommon.Merchant.MerchantId, NuveiCommon.Merchant.MerchantSiteId, NuveiCommon.Merchant.ServerHost, NuveiCommon.URLDetails.SuccessUrl, NuveiCommon.AcsPrefixURL);

            _CRes = null;

            var ret = IsInit();
        }

        public bool IsReadyForPayInByTempToken()  // For payins/deposits initiated by Web-SDK sfc.getToken()
        {
            var ret = IsInit() && IsDeposit() && !string.IsNullOrWhiteSpace(SessionToken) && User.IsReadyForPayInByTempToken();
            return ret;
        }

        public bool IsReadyForPayInByCard()  // For payins/deposits by credit card data (we will not use)
        {
            var ret = IsInit() && IsDeposit() && User.IsReadyForPayInByCard();
            return ret;
        }

        public bool IsReadyForPayInByApm()  // APMs - For payins/deposits/payments and payouts/withdrawals by Alternative Payment Methods
        {
            var ret = IsInit() && User.IsReadyForPayByApm();
            return ret;
        }

        public bool IsReadyForPayIn()
        {
            var ret = IsReadyForPayInByTempToken() || IsReadyForPayByOptionId() || IsReadyForPayInByCard() || IsReadyForPayInByApm();
            return ret;
        }

        public override bool IsReadyForPay()
        {
            var ret = IsReadyForPayIn();
            return ret;
        }

        protected ResponseStatus_Ext SetRedirected(string? orderID = null, string? clientReqID = null, string? redirectTxID = null)
        {
            if (IsFailed() || !IsReadyForPayIn())
                return Status;

            OrderID = NuveiCommon.CopyProperty(orderID, OrderID, false);
            ClientReqID = NuveiCommon.CopyProperty(clientReqID, ClientReqID, false);
            RedirectTxID = NuveiCommon.CopyProperty(redirectTxID, RedirectTxID, false);
            RelTxID = NuveiCommon.CopyProperty(redirectTxID, RelTxID, false);
            LastTxID = NuveiCommon.CopyProperty(redirectTxID, LastTxID, false);

            if (!IsApproved() && !IsFailed())
            {
                TxStatus = "REDIRECT";
                Status = ResponseStatus_Ext.Redirect;
            }
            return Status;
        }

        protected ResponseStatus_Ext SetRedirectedForCRes(string redirectTxID, string threeDTransID, string? orderID = null, string? clientReqID = null)
        {
            if (IsFailed() || !IsReadyForPayIn())
                return Status;

            if (ThreeD is null)
                SetThreeD();

            ThreeD.ServerTransId = NuveiCommon.CopyProperty(threeDTransID, ThreeD.ServerTransId, false);
            ThreeD.V2supported = "true";
            ThreeD.ThreeDFlow = "1";

            var status = SetRedirected(orderID, clientReqID, redirectTxID);
            if (string.IsNullOrWhiteSpace(RedirectTxID))
                SetError("Missing RedirectTxID");
            else if (string.IsNullOrWhiteSpace(ThreeD.ServerTransId))
                SetError("Missing ThreeD.ServerTransId");

            return Status;
        }

        protected ResponseStatus_Ext SetRedirectedForApm( string? orderID, string? clientReqID = null)
        {
            if (IsFailed() || !IsReadyForPayInByApm())
                return Status;

            var status = SetRedirected(orderID, clientReqID);
            if (string.IsNullOrWhiteSpace(OrderID))
                SetError("Missing OrderID");
            else if (string.IsNullOrWhiteSpace(ClientReqID))
                SetError("Missing ClientReqID");

            return Status;
        }

        // Logging

        public OpenOrderRequest? _OpenOrderRequest { get; protected set; }
        public OpenOrderResponse? _OpenOrderResponse { get; protected set; }

        public InitPaymentRequest? _InitPaymentRequest { get; protected set; }
        public InitPaymentResponse? _InitPaymentResponse { get; protected set; }

        public PaymentRequest? _PaymentRequest_Auth3D { get; protected set; }
        public PaymentResponse? _PaymentResponse_Auth3D { get; protected set; }

        public PaymentRequest? _PaymentRequest { get; protected set; }
        public PaymentResponse? _PaymentResponse { get; protected set; }

        public GetPaymentStatusRequest? _GetPaymentStatusRequest { get; protected set; }
        public GetPaymentStatusResponse? _GetPaymentStatusResponse { get; protected set; }

        public RefundTransactionRequest? _RefundTransactionRequest { get; protected set; }
        public RefundTransactionResponse? _RefundTransactionResponse { get; protected set; }
        
        // Helpers

        protected ResponseStatus_Ext ParsePaymentOptionResp(PaymentOptionResponse option)  // For payins/deposits/payments and payouts/withdrawals by Nuvei ID (user who already completed a previous successful payin/deposit/payment)
        {
            string? ccCardNumber = null; string? expYear = null; string? expMonth = null; string? bin = null;

            if (option.Card is not null)
            {
                //NuveiCommon.PrintToJsonFile(option, "PaymentOptionResponse", "", "", false);  // toDo: Remove, debugging
                User.CardBrand = NuveiCommon.CopyProperty(option.Card.CardBrand, User.CardBrand, true);
                Cvv2Reply = NuveiCommon.CopyProperty(option.Card.Cvv2Reply, Cvv2Reply, true);
                AvsCode = NuveiCommon.CopyProperty(option.Card.AvsCode, AvsCode, true);

                expYear = !string.IsNullOrWhiteSpace(option.Card.CcExpYear) ? option.Card.CcExpYear : null;
                expMonth = !string.IsNullOrWhiteSpace(option.Card.CcExpMonth) ? option.Card.CcExpMonth : null;
                bin = !string.IsNullOrWhiteSpace(option.Card.Bin) ? option.Card.Bin : null;
                ccCardNumber = !string.IsNullOrWhiteSpace(option.Card.CcCardNumber) ? option.Card.CcCardNumber :
                                  !string.IsNullOrWhiteSpace(option.Card.Last4Digits) ? option.Card.Last4Digits : null;

                if (option.Card.ThreeD is not null)
                {
                    var v2Supported = !string.IsNullOrWhiteSpace(option.Card.ThreeD.AcsUrl) && !string.IsNullOrWhiteSpace(option.Card.ThreeD.CReq);

                    const string v2True = "true";
                    var str = option.Card.ThreeD.V2supported;
                    if (!string.IsNullOrWhiteSpace(str))
                        v2Supported = v2Supported || str.Trim().StartsWith(v2True, StringComparison.InvariantCultureIgnoreCase);

                    str = option.Card.ThreeD.Eci;
                    if (!string.IsNullOrWhiteSpace(str))
                        v2Supported = v2Supported || Convert.ToUInt32(str) > 0;

                    str = option.Card.ThreeD.AcsChallengeMandated;
                    if (!string.IsNullOrWhiteSpace(str))
                        v2Supported = v2Supported || str.Trim().StartsWith("Y", StringComparison.InvariantCultureIgnoreCase);

                    str = option.Card.ThreeD.ThreeDFlow;
                    if (!string.IsNullOrWhiteSpace(str))
                        v2Supported = v2Supported || str.Trim().StartsWith("1");

                    str = option.Card.ThreeD.IsLiabilityOnIssuer;
                    if (!string.IsNullOrWhiteSpace(str))
                        v2Supported = v2Supported || str.Trim().StartsWith("1");

                    str = option.Card.ThreeD.Flow;
                    if (!string.IsNullOrWhiteSpace(str))
                        v2Supported = v2Supported || 
                                      str.Trim().StartsWith("challenge", StringComparison.InvariantCultureIgnoreCase) || str.Trim().StartsWith("frictionless", StringComparison.InvariantCultureIgnoreCase);

                    str = option.Card.ThreeD.AcquirerDecision;
                    if (!string.IsNullOrWhiteSpace(str))
                        v2Supported = v2Supported || str.Trim().StartsWith("ChallengeRequest", StringComparison.InvariantCultureIgnoreCase);

                    str = option.Card.ThreeD.Result;
                    if (!string.IsNullOrWhiteSpace(str))
                    {
                        str = str.Trim();
                        v2Supported = v2Supported || 
                                      str.StartsWith("Y", StringComparison.InvariantCultureIgnoreCase) || str.StartsWith("C", StringComparison.InvariantCultureIgnoreCase) || str.StartsWith("D", StringComparison.InvariantCultureIgnoreCase);
                    }

                    if (ThreeD is null)
                        SetThreeD();

                    ThreeD.V2supported = NuveiCommon.CopyProperty(v2Supported ? v2True : option.Card.ThreeD.V2supported, ThreeD.V2supported, true);
                    ThreeD.Version = NuveiCommon.CopyProperty(option.Card.ThreeD.Version, ThreeD.Version, true);

                    ThreeD.MethodPayload = NuveiCommon.CopyProperty(option.Card.ThreeD.MethodPayload, ThreeD.MethodPayload, true);
                    ThreeD.MethodUrl = NuveiCommon.CopyProperty(option.Card.ThreeD.MethodUrl, ThreeD.MethodUrl, true);
                    ThreeD.DsTransId = NuveiCommon.CopyProperty(option.Card.ThreeD.DsTransId, ThreeD.DsTransId, true);
                    ThreeD.ServerTransId = NuveiCommon.CopyProperty(option.Card.ThreeD.ServerTransId, ThreeD.ServerTransId, v2Supported);

                    // First Call 3D-Secure Response Parameters https://docs.nuvei.com/api/main/indexMain_v1_0.html?csharp#threeDOutputClass
                    ThreeD.AcsUrl = NuveiCommon.CopyProperty(option.Card.ThreeD.AcsUrl, ThreeD.AcsUrl, true);
                    ThreeD.AcsChallengeMandated = NuveiCommon.CopyProperty(option.Card.ThreeD.AcsChallengeMandated, ThreeD.AcsChallengeMandated, true);
                    ThreeD.CReq = NuveiCommon.CopyProperty(option.Card.ThreeD.CReq, ThreeD.CReq, true);
                    ThreeD.ThreeDFlow = NuveiCommon.CopyProperty(option.Card.ThreeD.ThreeDFlow, ThreeD.ThreeDFlow, true);
                    ThreeD.ThreeDReason = NuveiCommon.CopyProperty(option.Card.ThreeD.ThreeDReason, ThreeD.ThreeDReason, true);
                    ThreeD.ThreeDReasonId = NuveiCommon.CopyProperty(option.Card.ThreeD.ThreeDReasonId, ThreeD.ThreeDReasonId, true);
                    ThreeD.IsLiabilityOnIssuer = NuveiCommon.CopyProperty(option.Card.ThreeD.IsLiabilityOnIssuer, ThreeD.IsLiabilityOnIssuer, true);

                    // Final Call 3D - Secure Response Parameters https://docs.nuvei.com/api/main/indexMain_v1_0.html?csharp#threeDOutputClass
                    ThreeD.Result = NuveiCommon.CopyProperty(option.Card.ThreeD.Result, ThreeD.Result, true);
                    ThreeD.WhiteListStatus = NuveiCommon.CopyProperty(option.Card.ThreeD.WhiteListStatus, ThreeD.WhiteListStatus, true);
                    ThreeD.Cavv = NuveiCommon.CopyProperty(option.Card.ThreeD.Cavv, ThreeD.Cavv, true);
                    ThreeD.Eci = NuveiCommon.CopyProperty(option.Card.ThreeD.Eci, ThreeD.Eci, true);
                    ThreeD.AuthenticationType = NuveiCommon.CopyProperty(option.Card.ThreeD.AuthenticationType, ThreeD.AuthenticationType, true);
                    ThreeD.CardHolderInfoText = NuveiCommon.CopyProperty(option.Card.ThreeD.CardHolderInfoText, ThreeD.CardHolderInfoText, true);

                    // Extending Safecharge properties per https://docs.nuvei.com/api/main/indexMain_v1_0.html?csharp#threeDOutputClass
                    ThreeD.ChallengePreferenceReason = NuveiCommon.CopyProperty(option.Card.ThreeD.ChallengePreferenceReason, ThreeD.ChallengePreferenceReason, true);
                    ThreeD.ChallengeCancelReason = NuveiCommon.CopyProperty(option.Card.ThreeD.ChallengeCancelReason, ThreeD.ChallengeCancelReason, true);
                    ThreeD.IsExemptionRequestInAuthentication = NuveiCommon.CopyProperty(option.Card.ThreeD.IsExemptionRequestInAuthentication, ThreeD.IsExemptionRequestInAuthentication, true);
                    ThreeD.Flow = NuveiCommon.CopyProperty(option.Card.ThreeD.Flow, ThreeD.Flow, true);
                    ThreeD.AcquirerDecision = NuveiCommon.CopyProperty(option.Card.ThreeD.AcquirerDecision, ThreeD.AcquirerDecision, true);
                    ThreeD.DecisionReason = NuveiCommon.CopyProperty(option.Card.ThreeD.DecisionReason, ThreeD.DecisionReason, true);

                    if (IsFailed() && !string.IsNullOrWhiteSpace(ThreeD.ThreeDReasonId) && Convert.ToInt32(ThreeD.ThreeDReasonId) != 0)
                        ErrReason = NuveiCommon.CopyProperty("ThreeD Reason ID: " + ThreeD.ThreeDReasonId, ErrReason);

                    if (IsFailed() && !string.IsNullOrWhiteSpace(ThreeD.ThreeDReason))
                        ErrReason = NuveiCommon.CopyProperty("ThreeD Reason: " + ThreeD.ThreeDReason, ErrReason);

                    if (IsAuthThreeD()
                        && !string.IsNullOrWhiteSpace(TxStatus) && TxStatus.Equals("REDIRECT", StringComparison.InvariantCultureIgnoreCase)
                        && !string.IsNullOrWhiteSpace(option.Card.ThreeD.ThreeDFlow) && option.Card.ThreeD.ThreeDFlow.Equals("1", StringComparison.InvariantCultureIgnoreCase)
                        && !string.IsNullOrWhiteSpace(option.Card.ThreeD.AcsUrl) && !string.IsNullOrWhiteSpace(option.Card.ThreeD.CReq))
                    {
                        RedirectURL = string.IsNullOrWhiteSpace(AcsPrefixURL) ? "" : AcsPrefixURL + "?acsUrl=";
                        RedirectURL += option.Card.ThreeD.AcsUrl;
                        RedirectURL += RedirectURL.Contains("?") ? "&" : "?";
                        RedirectURL += "creq=" + option.Card.ThreeD.CReq;

                        if (!IsFailed())
                            Status = ResponseStatus_Ext.Redirect;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(expYear) || string.IsNullOrWhiteSpace(expMonth))
                expYear = expMonth = null;
            else
            {
                var nExpMonth = Convert.ToInt32(expMonth);
                var nExpYear = Convert.ToInt32(expYear);
                var utcNow = DateTime.UtcNow;
                var utcMonth = utcNow.ToString("MM");
                var nUtcMonth = Convert.ToInt32(utcMonth);
                var utcYear = utcNow.ToString("yy");
                var nUtcYear = Convert.ToInt32(utcYear);
                if (nUtcYear > nExpYear || 
                    (nUtcYear == nExpYear && nUtcMonth > nExpMonth))
                    expYear = expMonth = null;
            }

            var ret = false;
            if (!string.IsNullOrWhiteSpace(option.UserPaymentOptionId))
                ret = User.ExtendForPayByOptionId(option.UserPaymentOptionId, ccCardNumber, expYear, expMonth, bin);

            if (!string.IsNullOrWhiteSpace(TxStatus) && TxStatus.Equals("REDIRECT", StringComparison.InvariantCultureIgnoreCase)
                        && !string.IsNullOrWhiteSpace(option.RedirectUrl))
            {
                RedirectURL = option.RedirectUrl;
                if (!IsFailed())
                    Status = ResponseStatus_Ext.Redirect;
            }

            return Status;
        }

        // SafeCharge payins/deposits/payments

        protected static async Task<ResponseStatus_Ext> OrderPayInReq(NuveiDeposit deposit)
        {
            if (!deposit.IsInit())
                return deposit.SetError("Deposit not initialized");

            if (string.IsNullOrWhiteSpace(deposit.SessionToken))
                return deposit.SetError("Missing SessionToken");

            var amount = deposit.ReqPay.GetAmount();

            // toDo: Testing Zero-Auth with Web SDK
            var bZeroAuthWeb = false;
            //bZeroAuthWeb = true;
            if (bZeroAuthWeb)
                amount = 0M;

            var req = new OpenOrderRequest(deposit.Merchant, deposit.SessionToken, deposit.ReqPay.Currency, NuveiCommon.DecimalToString(amount));
            req.UserTokenId = deposit.User.TokenID;
            req.ClientUniqueId = deposit.SessionID;
            req.ClientRequestId = deposit.GetNewClientRequestID();

            // Extending Safecharge properties per https://docs.nuvei.com/api/main/indexMain_v1_0.html?csharp#openOrder
            req.IsPartialApproval = "0";  // toDo: Testing

            // toDo: Testing Zero-Auth with Web SDK
            if (bZeroAuthWeb)
            {
                req.TransactionType = "Auth";

                //Extending Safecharge properties per https://docs.nuvei.com/api/main/indexMain_v1_0.html?csharp#openOrder
                req.AuthenticationOnlyType = "ADDCARD";
            }
            else
                req.PreventOverride = "1";

            //deposit.PrintToJsonFile(req, "OpenOrderRequest");  // toDo: Remove, debugging
            deposit._OpenOrderRequest = req;  // Logging

            var resp = await NuveiCommon.ReqExecutor().OpenOrder(req);
            //deposit.PrintToJsonFile(resp, "OpenOrderResponse");  // toDo: Remove, debugging
            deposit._OpenOrderResponse = resp;  // Logging

            deposit.ReqID = resp.InternalRequestId;
            deposit.ClientReqID = resp.ClientRequestId;
            deposit.OrderID = resp.OrderId;

            deposit.ParseRespError(resp);
            return deposit.Status;
        }

        protected static async Task<ResponseStatus_Ext> WrapOrderPayInReq(NuveiDeposit deposit)
        {
            var status = ResponseStatus_Ext.Error;
            try
            {
                status = await NuveiDeposit.OrderPayInReq(deposit);
                return status;
            }
            catch (Exception ex)
            {
                status = deposit.SetError("OrderPayInReq Exception: " + ex.Message, ResponseStatus_Ext.Exception);
                return status;
            }
        }

        protected static async Task<ResponseStatus_Ext> InitPayInReq(NuveiDeposit deposit)
        {
            if (!deposit.IsInit())
                return deposit.SetError("Deposit not initialized");

            if (!deposit.IsReadyForPayIn())
                return deposit.SetError("Deposit not ready for InitPayIn");

            if (string.IsNullOrWhiteSpace(deposit.SessionToken))
                return deposit.SetError("Missing SessionToken");

            var payInOption = new InitPaymentPaymentOption();
            if (deposit.IsReadyForPayByOptionId())
            {
                payInOption.UserPaymentOptionId = deposit.User.PayByOptionId.UserPaymentOptionId;
                payInOption.Card = new InitPaymentCard() { CVV = deposit.User.PayByOptionId.CVV };
                if (deposit.User.PayInCard is not null)
                {
                    payInOption.Card.ExpirationYear = deposit.User.PayInCard.ExpirationYear;
                    payInOption.Card.ExpirationMonth = deposit.User.PayInCard.ExpirationMonth;
                }
            }
            else if (deposit.IsReadyForPayInByCard())
                payInOption.Card = new InitPaymentCard()
                {
                    CardHolderName = deposit.User.PayInCard.CardHolderName,
                    CardNumber = deposit.User.PayInCard.CardNumber,
                    ExpirationYear = deposit.User.PayInCard.ExpirationYear,
                    ExpirationMonth = deposit.User.PayInCard.ExpirationMonth,
                    CVV = deposit.User.PayInCard.CVV
                };
            else
                //if (deposit.IsReadyForPayInByTempToken())
                payInOption.Card = new InitPaymentCard()
                {
                    CardHolderName = deposit.User.PayInCard.CardHolderName,
                    CcTempToken = deposit.User.PayInCard.CcTempToken,
                    CVV = deposit.User.PayInCard.CVV
                };

            var amount = deposit.bZeroAuth ? 0M : deposit.ReqPay.GetAmount();
            var req = new InitPaymentRequest(deposit.Merchant, deposit.SessionToken, deposit.ReqPay.Currency, NuveiCommon.DecimalToString(amount), payInOption);
            req.UserTokenId = deposit.User.TokenID;
            req.ClientUniqueId = deposit.SessionID;
            req.ClientRequestId = deposit.GetNewClientRequestID();

            req.DeviceDetails = deposit.Device;
            req.BillingAddress = deposit.User.BillAddress;
            req.UrlDetails = new UrlDetails()
            {
                SuccessUrl = deposit.URLDetails.SuccessUrl,
                FailureUrl = deposit.URLDetails.FailureUrl,
                PendingUrl = deposit.URLDetails.PendingUrl
            };

            if (deposit.bZeroAuth)
            {
                // Extending Safecharge properties per https://docs.nuvei.com/documentation/features/card-operations/zero-authorization/
                req.TransactionType = "Auth";
            }

            //deposit.PrintToJsonFile(req, "InitPaymentRequest");  // toDo: Remove, debugging
            deposit._InitPaymentRequest = req;  // Logging

            var resp = await NuveiCommon.ReqExecutor().InitPayment(req);
            //deposit.PrintToJsonFile(resp, "InitPaymentResponse");  // toDo: Remove, debugging
            deposit._InitPaymentResponse = resp;  // Logging

            deposit.ReqID = resp.InternalRequestId;
            deposit.ClientReqID = resp.ClientRequestId;
            deposit.OrderID = resp.OrderId;
            deposit.InitTxID = deposit.LastTxID = resp.TransactionId;
            deposit.TxType = resp.TransactionType;

            deposit.ParseRespError(resp);
            deposit.ParseTxStatusError(resp.TransactionStatus, resp.GwErrorCode, resp.GwErrorReason, resp.GwExtendedErrorCode);
            if (resp.PaymentOption is not null)
                deposit.ParsePaymentOptionResp(resp.PaymentOption);

            if (deposit.IsSuccessful())
                deposit.RelTxID = deposit.LastTxID;

            return deposit.Status;
        }

        protected static async Task<ResponseStatus_Ext> WrapInitPayInReq(NuveiDeposit deposit)
        {
            var status = ResponseStatus_Ext.Error;
            try
            {
                status = await NuveiDeposit.InitPayInReq(deposit);
                return status;
            }
            catch (Exception ex)
            {
                status = deposit.SetError("InitPayInReq Exception: " + ex.Message, ResponseStatus_Ext.Exception);
                return status;
            }
        }

        protected static async Task<ResponseStatus_Ext> PayInReq(NuveiDeposit deposit, bool bThreeD = false)
        {
            if (!deposit.IsInit())
                return deposit.SetError("Deposit not initialized");

            if (!deposit.IsReadyForPayIn())
                return deposit.SetError("Deposit not ready for PayIn");

            if (string.IsNullOrWhiteSpace(deposit.SessionToken))
                return deposit.SetError("Missing SessionToken");

            if (string.IsNullOrWhiteSpace(deposit.RelTxID) && !deposit.IsReadyForPayInByTempToken() && !deposit.IsReadyForPayInByApm())
                return deposit.SetError("Missing RelTxID or Session not ready for PayInByTempToken");

            var payInOption = new PaymentOption();
            if (deposit.IsReadyForPayByOptionId())
            {
                payInOption.UserPaymentOptionId = deposit.User.PayByOptionId.UserPaymentOptionId;
                payInOption.Card = new Card() { CVV = deposit.User.PayByOptionId.CVV };
                if (deposit.User.PayInCard is not null)
                {
                    payInOption.Card.ExpirationYear = deposit.User.PayInCard.ExpirationYear;
                    payInOption.Card.ExpirationMonth = deposit.User.PayInCard.ExpirationMonth;
                }
                if (!string.IsNullOrWhiteSpace(deposit.User.APSubMethod))
                    payInOption.Submethod = new SubMethod { Submethod = deposit.User.APSubMethod };
            }
            else if (deposit.IsReadyForPayInByCard())
                payInOption.Card = new Card()
                {
                    CardHolderName = deposit.User.PayInCard.CardHolderName,
                    CardNumber = deposit.User.PayInCard.CardNumber,
                    ExpirationYear = deposit.User.PayInCard.ExpirationYear,
                    ExpirationMonth = deposit.User.PayInCard.ExpirationMonth,
                    CVV = deposit.User.PayInCard.CVV
                };
            else if (deposit.IsReadyForPayInByApm())
            {
                if (deposit.User.IsReadyForPayByOptionId(true))
                    payInOption.UserPaymentOptionId = deposit.User.PayByOptionId.UserPaymentOptionId;
                else
                    payInOption.AlternativePaymentMethod = deposit.User.GetApMethod().APM;

                if (!string.IsNullOrWhiteSpace(deposit.User.APSubMethod))
                    payInOption.Submethod = new SubMethod { Submethod = deposit.User.APSubMethod };
            }
            else
            //if (deposit.IsReadyForPayInByTempToken())
                payInOption.Card = new Card()
                {
                    CardHolderName = deposit.User.PayInCard.CardHolderName,
                    CcTempToken = deposit.User.PayInCard.CcTempToken,
                    CVV = deposit.User.PayInCard.CVV
                };

            if (bThreeD && payInOption.Card is not null)
            {
                payInOption.Card.ThreeD = new ThreeD
                {
                    Version = deposit.ThreeD.Version,
                    MethodCompletionInd = "U",  // Optional 3D-Secure Device Fingerprinting for Web Browsers, not using
                    PlatformType = "02",
                    MerchantURL = deposit.MerchantURL,
                    NotificationURL = deposit.URLDetails.BackUrl,
                    BrowserDetails = new BrowserDetails
                    {
                        AcceptHeader = "text/html,application/xhtml+xml",
                        Ip = deposit.Device.IpAddress,
                        JavaEnabled = "TRUE",
                        JavaScriptEnabled = "TRUE",
                        Language = "EN",  // toDo: User locale
                        ColorDepth = "48",
                        ScreenHeight = "400",
                        ScreenWidth = "600",
                        TimeZone = "0",  // toDo: User timezone
                        UserAgent = "Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47)"
                    },
                    V2AdditionalParams = new V2AdditionalParams
                    {
                        ChallengeWindowSize = "05",
                        ChallengePreference = deposit.User.ChallengePref 
                    }
                };
                if (deposit.bZeroAuth)
                    payInOption.Card.ThreeD.V2AdditionalParams.ExceptionPayment3DAuth = true;  // toDo: Test (not ducumented in API)
            }

            var bZeroAuth = deposit.bZeroAuth && !deposit.HasCRes(true);
            var amount = bZeroAuth ? 0M : deposit.ReqPay.GetAmount();
            var req = new PaymentRequest(deposit.Merchant, deposit.SessionToken, deposit.ReqPay.Currency, NuveiCommon.DecimalToString(amount), payInOption);
            req.RelatedTransactionId = deposit.RelTxID;
            if (bZeroAuth)
            {
                req.TransactionType = "Auth";

                // Extending Safecharge properties per https://docs.nuvei.com/api/main/indexMain_v1_0.html?json#payment
                req.AuthenticationOnlyType = "ADDCARD";
                //req.PaymentFlow = "direct";
            }

            req.IsPartialApproval = "0";
            req.UserTokenId = deposit.User.TokenID;
            req.ClientUniqueId = deposit.SessionID;
            req.ClientRequestId = deposit.GetNewClientRequestID();
            req.ProductId = deposit.SessionID;

            req.DeviceDetails = deposit.Device;
            req.BillingAddress = deposit.User.BillAddress;
            req.UserDetails = deposit.User.UserDetails;
            req.UrlDetails = new UrlDetails()
            {
                SuccessUrl = deposit.URLDetails.SuccessUrl,
                FailureUrl = deposit.URLDetails.FailureUrl,
                PendingUrl = deposit.URLDetails.PendingUrl,
                NotificationUrl = deposit.IsReadyForPayInByApm() ? deposit.URLDetails.NotificationUrl : null
            };

            //deposit.PrintToJsonFile(req, "PaymentRequest" + (bThreeD ? "-Auth" : ""));  // toDo: Remove, debugging
            if (bThreeD)
                deposit._PaymentRequest_Auth3D = req;  // Logging
            else
                deposit._PaymentRequest = req;  // Logging

            var resp = await NuveiCommon.ReqExecutor().Payment(req);
            //deposit.PrintToJsonFile(resp, "PaymentResponse" + (bThreeD ? "-Auth" : ""));  // toDo: Remove, debugging
            if (bThreeD)
                deposit._PaymentResponse_Auth3D = resp;  // Logging
            else
                deposit._PaymentResponse = resp;  // Logging

            deposit.ReqID = resp.InternalRequestId;
            deposit.ClientReqID = resp.ClientRequestId;
            deposit.OrderID = resp.OrderId;
            deposit.LastTxID = NuveiCommon.CopyProperty(resp.TransactionId, deposit.LastTxID);
            deposit.TxType = NuveiCommon.CopyProperty(resp.TransactionType, deposit.TxType);

            deposit.ParseRespError(resp);
            deposit.ParsePayMethodError(resp.PaymentMethodErrorCode, resp.PaymentMethodErrorReason);
            deposit.ParseTxStatusError(resp.TransactionStatus, resp.GwErrorCode, resp.GwErrorReason, resp.GwExtendedErrorCode);
            if (resp.PaymentOption is not null)
                deposit.ParsePaymentOptionResp(resp.PaymentOption);

            if (resp.FraudDetails is not null)
                deposit.FinalDecision = NuveiCommon.CopyProperty(resp.FraudDetails.FinalDecision, deposit.FinalDecision);

            if ((deposit.IsAuth() || deposit.IsSale())
                && !string.IsNullOrWhiteSpace(deposit.TxStatus) && deposit.TxStatus.Equals("APPROVED", StringComparison.InvariantCultureIgnoreCase)
                && !string.IsNullOrWhiteSpace(deposit.FinalDecision) && deposit.FinalDecision.Equals("Accept", StringComparison.InvariantCultureIgnoreCase))
            {
                deposit.PayTxID = resp.TransactionId;
                deposit.SetApproved();
                if (resp.PartialApproval is not null)
                    deposit.RespPay = new NuveiPay(resp.PartialApproval.ProcessedCurrency, resp.PartialApproval.ProcessedAmount);

                if (deposit.ThreeD is not null)
                    deposit.ThreeDFlowAndAuthDescription = deposit.DescribeThreeDFlowAndAuth();
            }

            if (deposit.IsSuccessful())
            {
                deposit.RelTxID = resp.TransactionId;

                // Extending Safecharge properties per https://docs.nuvei.com/api/main/indexMain_v1_0.html?csharp#payment
                if (resp.PartialApproval is not null)
                    deposit.RespPay = new NuveiPay(resp.PartialApproval.ProcessedCurrency, resp.PartialApproval.ProcessedAmount);
            }
            else if (deposit.IsDeclined())
                deposit.PayTxID = resp.TransactionId;

            if (deposit.IsRedirected())
                deposit.RedirectTxID = NuveiCommon.CopyProperty(resp.TransactionId, deposit.RedirectTxID, false);         

            return deposit.Status;
        }

        protected static async Task<ResponseStatus_Ext> WrapPayInReq(NuveiDeposit deposit, bool threeD = false)
        {
            var status = ResponseStatus_Ext.Error;
            try
            {
                status = await NuveiDeposit.PayInReq(deposit, threeD);
                return status;
            }
            catch (Exception ex)
            {
                status = deposit.SetError("PayInReq Exception: " + ex.Message, ResponseStatus_Ext.Exception);
                return status;
            }
        }

        protected static async Task<ResponseStatus_Ext> PayInStatusReq(NuveiDeposit deposit)
        {
            if (!deposit.IsInit())
                return deposit.SetError("Deposit not initialized");

            if (!deposit.IsReadyForPayIn())
                return deposit.SetError("Deposit not ready for PayIn");

            if (string.IsNullOrWhiteSpace(deposit.SessionToken))
                return deposit.SetError("Missing SessionToken");

            var req = new GetPaymentStatusRequest(deposit.Merchant, deposit.SessionToken);
            req.ClientRequestId = deposit.GetNewClientRequestID();

            //deposit.PrintToJsonFile(req, "PaymentStatusRequest");  // toDo: Remove, debugging
            deposit._GetPaymentStatusRequest = req;  // Logging

            var resp = await NuveiCommon.ReqExecutor().GetPaymentStatus(req);
            //deposit.PrintToJsonFile(resp, "PaymentStatusResponse");  // toDo: Remove, debugging
            deposit._GetPaymentStatusResponse = resp;  // Logging

            deposit.LastTxID = NuveiCommon.CopyProperty(resp.TransactionId, deposit.LastTxID);
            deposit.TxType = NuveiCommon.CopyProperty(resp.TransactionType, deposit.TxType);
            deposit.ReqID = resp.InternalRequestId;
            deposit.ClientReqID = resp.ClientRequestId;

            deposit.ParseRespError(resp, deposit.HasDmn() || deposit.HasCRes());
            deposit.ParsePayMethodError(resp.PaymentMethodErrorCode.ToString(), resp.PaymentMethodErrorReason);
            deposit.ParseTxStatusError(resp.TransactionStatus, resp.GwErrorCode, resp.GwErrorReason, resp.GwExtendedErrorCode);
            if (resp.PaymentOption is not null)
                deposit.ParsePaymentOptionResp(resp.PaymentOption);

            if ((deposit.IsAuth() || deposit.IsSale())
                && !string.IsNullOrWhiteSpace(deposit.TxStatus) && deposit.TxStatus.Equals("APPROVED", StringComparison.InvariantCultureIgnoreCase))
            {
                deposit.SetApproved();
                deposit.RespPay = new NuveiPay(resp.Currency, resp.Amount);
                deposit.PayTxID = NuveiCommon.CopyProperty(resp.TransactionId, deposit.PayTxID, false);

                if (deposit.ThreeD is not null)
                    deposit.ThreeDFlowAndAuthDescription = NuveiCommon.CopyProperty(deposit.DescribeThreeDFlowAndAuth(), deposit.ThreeDFlowAndAuthDescription, true);
            }

            return deposit.Status;
        }

        protected static async Task<ResponseStatus_Ext> WrapPayInStatusReq(NuveiDeposit deposit)
        {
            var status = deposit.Status;
            try
            {
                status = await NuveiDeposit.PayInStatusReq(deposit);
                return status;
            }
            catch (Exception ex)
            {
                if (!deposit.IsApproved())
                    status = deposit.SetError("PayInStatusReq Exception: " + ex.Message, ResponseStatus_Ext.Exception);

                return deposit.Status;
            }
        }

        protected static async Task<ResponseStatus_Ext> RefundPayInReq(NuveiDeposit deposit)
        {
            if (!deposit.IsInit())
                return deposit.SetError("Deposit not initialized");

            if (!deposit.IsReadyForPayIn())
                return deposit.SetError("Deposit not ready for PayIn");

            if (string.IsNullOrWhiteSpace(deposit.RelTxID) && !deposit.User.IsReadyForPayByOptionId(true))
                return deposit.SetError("Deposit not ready for Refund");

            if (string.IsNullOrWhiteSpace(deposit.SessionToken))
                return deposit.SetError("Missing SessionToken");

            if (!"Sale".Equals(deposit.TxType, StringComparison.InvariantCultureIgnoreCase))
                return deposit.SetError("Deposit not Sale type");

            var pay = deposit.RespPay is not null ? deposit.RespPay : deposit.ReqPay;
            var relTxID = string.IsNullOrWhiteSpace(deposit.RelTxID) ? "" : deposit.RelTxID;

            var req = new RefundTransactionRequest(deposit.Merchant, deposit.SessionToken, pay.Currency, NuveiCommon.DecimalToString(pay.GetAmount()), relTxID);
            req.ClientUniqueId = deposit.SessionID;
            req.ClientRequestId = deposit.GetNewClientRequestID();
            //req.ProductId = deposit.SessionID;  // toDo: Do we need

            if (string.IsNullOrWhiteSpace(relTxID))
            {
                // Extending Safecharge properties per https://docs.nuvei.com/documentation/features/financial-operations/refund/
                req.UserTokenId = deposit.User.TokenID;
                var payByOptionId = new PaymentOption
                {
                    UserPaymentOptionId = deposit.User.PayByOptionId.UserPaymentOptionId
                };
                req.PaymentOption = payByOptionId;
            }

            //session.PrintToJsonFile(req, "RefundTransactionRequest");  // toDo: Remove, debugging
            deposit._RefundTransactionRequest = req;  // Logging

            var resp = await NuveiCommon.ReqExecutor().RefundTransaction(req);
            //deposit.PrintToJsonFile(resp, "RefundTransactionResponse");  // toDo: Remove, debugging
            deposit._RefundTransactionResponse = resp;  // Logging

            deposit.ReqID = resp.InternalRequestId;
            deposit.ClientReqID = resp.ClientRequestId;
            deposit.VoidTxID = resp.TransactionId;
            deposit.LastTxID = NuveiCommon.CopyProperty(resp.TransactionId, deposit.LastTxID);

            deposit.ParseRespError(resp);
            deposit.ParsePayMethodError(resp.PaymentMethodErrorCode.ToString(), resp.PaymentMethodErrorReason);
            deposit.ParseTxStatusError(resp.TransactionStatus, resp.GwErrorCode, resp.GwErrorReason, resp.GwExtendedErrorCode);
            if (deposit.IsSuccessful())
            {
                var reason = string.Format("Refunded {0} {1}", pay.Amount, pay.Currency);
                deposit.SetError(reason, ResponseStatus_Ext.Voided);
            }

            return deposit.Status;
        }

        protected static async Task<ResponseStatus_Ext> WrapRefundPayInReq(NuveiDeposit deposit)
        {
            var status = ResponseStatus_Ext.Error;
            try
            {
                status = await NuveiDeposit.RefundPayInReq(deposit);
                return status;
            }
            catch (Exception ex)
            {
                status = deposit.SetError("RefundPayInReq Exception: " + ex.Message, ResponseStatus_Ext.Exception);
                return status;
            }
        }

        // Public PayIn/Deposit/Payment methods

        public static async Task<ResponseStatus_Ext> PrepareDeposit(NuveiDeposit deposit)  // PayIn = Deposit = Payment
        {
            ResponseStatus_Ext status;
            do
            {
                if (!deposit.IsInit())
                {
                    deposit.SetError("Deposit not initialized");
                    break;
                }

                // SessionTokenReq
                status = await NuveiDeposit.WrapSessionTokenReq(deposit);
                if (!deposit.IsSuccessful())
                {
                    deposit.SetError("SessionTokenReq failed");
                    break;
                }

                if (!deposit.bZeroAuth)
                {
                    // OrderPayInReq
                    status = await NuveiDeposit.WrapOrderPayInReq(deposit);
                    if (!deposit.IsSuccessful())
                        deposit.SetError("OrderPayInReq failed");
                }
            } while (false);

            //deposit.PrintToJsonFile("PrepareDeposit-Result");  // toDo: Remove, debugging
            var ret = deposit.IsSuccessful();
            return deposit.Status;
        }

        public static async Task<ResponseStatus_Ext> StartDeposit(NuveiDeposit deposit, string? InitTxID = null, string? threeDSrvTxID = null, string? LastTxID = null, bool bDebugRedirect = false)  // PayIn = Deposit = Payment
        {
            if (!string.IsNullOrWhiteSpace(InitTxID))
                deposit.LastTxID = deposit.RelTxID = deposit.InitTxID = InitTxID;

            if (!string.IsNullOrWhiteSpace(LastTxID))
                deposit.LastTxID = LastTxID;

            if (!string.IsNullOrWhiteSpace(threeDSrvTxID))
            {
                if (deposit.ThreeD is null)
                    deposit.SetThreeD();

                deposit.ThreeD.ServerTransId = threeDSrvTxID;
            }

            //deposit.PrintToJsonFile("StartDeposit");  // toDo: Remove, debugging
            ResponseStatus_Ext status;
            do
            {
                if (!deposit.IsReadyForPayIn() || deposit.IsReadyForPayInByApm())
                {
                    deposit.SetError("Deposit not ready for PayIn");
                    //deposit.PrintToJsonFile("StartDeposit-1");  // toDo: Remove, debugging
                    break;
                }

               if (!deposit.IsReadyForPayInByTempToken())
                {
                    // SessionTokenReq
                    status = await NuveiDeposit.WrapSessionTokenReq(deposit);
                    if (!deposit.IsSuccessful())
                    {
                        deposit.SetError("SessionTokenReq failed");
                        //deposit.PrintToJsonFile("StartDeposit-2");  // toDo: Remove, debugging
                        break;
                    }
                }

                // InitPayInReq
                status = await NuveiDeposit.WrapInitPayInReq(deposit);
                if (!deposit.IsSuccessful())
                {
                    deposit.SetError("InitPayInReq failed");
                    //deposit.PrintToJsonFile("StartDeposit-3");  // toDo: Remove, debugging
                    break;
                }

                // PayInReq ThreeD
                var bThreeD = !deposit.bZeroAuth && (deposit.IsThreeDSupported() || deposit.IsStaging());
                status = await NuveiDeposit.WrapPayInReq(deposit, bThreeD);

                var bIgnoreThreeDError = bThreeD && deposit.IsStaging() && deposit.IsIgnoredThreeDError();
                if (bIgnoreThreeDError)
                {
                    deposit.RelTxID = deposit.LastTxID;
                    deposit.Status = ResponseStatus_Ext.Approved;
                    deposit.ErrType = ErrorType.NoError;
                    deposit.ErrReason = deposit.GwErrReason = null;
                    deposit.GwErrCode = deposit.GwExtErrCode = null;
                    deposit.FinalDecision = null;
                    if (deposit.ThreeD is not null)
                    {
                        deposit.ThreeD.ThreeDReason = null;

                        // Extending Safecharge properties per https://docs.nuvei.com/api/main/indexMain_v1_0.html?csharp#threeDOutputClass
                        deposit.ThreeD.ThreeDReasonId = null;
                    }
                }

                if (!deposit.IsSuccessful())
                    deposit.SetError("PayInReq " + (deposit.bZeroAuth ? "Zero Auth" : "ThreeD") + " failed");
                else if (deposit.IsRedirected() && NuveiPay.IsInit(deposit.RespPay) && !deposit.ReqPay.Equals(deposit.RespPay) 
                         //&& false  // Fail Deposit if not fully pre-approved
                        )
                {
                    var reason = string.Format("Pre-approved only {0} {1}", deposit.RespPay.Amount, deposit.RespPay.Currency);
                    deposit.SetError(reason);
                    //deposit.PrintToJsonFile("StartDeposit-4");  // toDo: Remove, debugging
                }
            } while (false);

            // toDo: Testing, Remove
            // toDo: TxId 7854237322927583, Nuvei TransactionID 2130000005051346182, PayInReq Exception: The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.
            //deposit.SetError("PayInReq Exception: The request was canceled due to the configured HttpClient.Timeout of 90 seconds elapsing", ResponseStatus_Ext.Exception);

            if (!deposit.IsError())
            {
                if (deposit.bZeroAuth)
                    status = deposit.Status;
                else if (!deposit.IsRedirected())
                    status = await ConfirmDeposit(deposit);
                else if (bDebugRedirect)
                {
                    var cmd = "microsoft-edge:" + deposit.RedirectURL;
                    var processInfo = new ProcessStartInfo(cmd);
                    processInfo.UseShellExecute = true;
                    Process.Start(processInfo);

                    var cRes = new ChallengeResult();
                    var fName = "\\\\192.168.210.19\\Temp\\NuveiNotifications\\CRes\\" + deposit.SessionID + ".txt";
                    var body = NuveiCommon.ReadStringFromFile(fName);
                    var encodedBody = body.Replace("CRES=", "cres=", StringComparison.InvariantCultureIgnoreCase);
                    var list = encodedBody.Split("cres=");
                    if (list is not null && list.Length > 1)
                        encodedBody = list[1];

                    list = encodedBody.Split("&");
                    if (list is not null && list.Length > 0)
                        encodedBody = list[0];

                    string? decodedBody = null;
                    var dJson = ChallengeResult.WrapDecodeFromBase64(encodedBody, ref decodedBody);
                    var ret = cRes.DecodeFromDJson(dJson, body, encodedBody, decodedBody);
                    status = await ConfirmDeposit(deposit, cRes);
                }
            }

            // toDo: TxId 7854237322927583, Nuvei TransactionID 2130000005051346182, PayInReq Exception: The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.
            else if (deposit.Status == ResponseStatus_Ext.Exception)
            {
                status = await CheckDeposit(deposit);
                if (!deposit.IsApproved(true))
                {
                    deposit.SetError("PayInStatusReq failed");
                    //deposit.PrintToJsonFile("ConfirmDeposit-4");  // toDo: Remove, debugging
                }
            }

            //deposit.PrintToJsonFile("StartDeposit-Result");  // toDo: Remove, debugging
            return deposit.Status;
        }

        public static async Task<ResponseStatus_Ext> StartDepositByApm(NuveiDeposit deposit)  // PayIn = Deposit = Payment by Alternative Payment methods
        {
            ResponseStatus_Ext status;
            do
            {
                if (!deposit.IsReadyForPayInByApm())
                {
                    deposit.SetError("Deposit not ready for PayIn by APM");
                    break;
                }

                // SessionTokenReq
                status = await NuveiDeposit.WrapSessionTokenReq(deposit);
                if (!deposit.IsSuccessful())
                {
                    deposit.SetError("SessionTokenReq failed");
                    break;
                }

                // PayInReq
                status = await NuveiDeposit.WrapPayInReq(deposit);
                if (!deposit.IsSuccessful())
                    deposit.SetError("PayInReq by APM failed");
                else if (deposit.IsRedirected() && NuveiPay.IsInit(deposit.RespPay) && !deposit.ReqPay.Equals(deposit.RespPay)
                        //&& false  // Fail Deposit if not fully pre-approved
                        )
                {
                    var reason = string.Format("Pre-approved only {0} {1}", deposit.RespPay.Amount, deposit.RespPay.Currency);
                    deposit.SetError(reason);
                }
            } while (false);

            //deposit.PrintToJsonFile("StartDepositByApm-Result");  // toDo: Remove, debugging
            return deposit.Status;
        }

        public static async Task<ResponseStatus_Ext> ConfirmDeposit(NuveiDeposit deposit, ChallengeResult? cRes = null, string? redirectTxID = null, string? threeDTransID = null, string? orderID = null, string? clientReqID = null)
        {
            //deposit.PrintToJsonFile("ConfirmDeposit");  // toDo: Remove, debugging
            ResponseStatus_Ext status;
            do
            {
                if (!deposit.IsReadyForPayIn())
                {
                    deposit.SetError("Deposit not ready for PayIn");
                    //deposit.PrintToJsonFile("ConfirmDeposit-1");  // toDo: Remove, debugging
                    break;
                }
                if (cRes is not null)
                    status = deposit.SetRedirectedForCRes(redirectTxID, threeDTransID, orderID, clientReqID);

                if (deposit.IsRedirected())
                {
                    if (cRes is null)
                        cRes = deposit._CRes;

                    status = deposit.SetCRes(cRes);
                }

                if (!deposit.IsFailed() && (!deposit.IsApproved(true) || deposit.HasCRes()))
                {
                    // SessionTokenReq
                    status = await NuveiDeposit.WrapSessionTokenReq(deposit);
                    if (!deposit.IsSuccessful())
                    {
                        deposit.SetError("SessionTokenReq failed");
                        //deposit.PrintToJsonFile("ConfirmDeposit-2");  // toDo: Remove, debugging
                        break;
                    }

                    // PayInReq
                    status = await NuveiDeposit.WrapPayInReq(deposit);
                    if (!deposit.IsApproved(true))
                    {
                        deposit.SetError("PayInReq failed");
                        //deposit.PrintToJsonFile("ConfirmDeposit-3");  // toDo: Remove, debugging
                        break;
                    }
                }

                if (!deposit.IsFailed() || deposit.HasCRes())
                {
                    status = await CheckDeposit(deposit);
                    if (!deposit.IsApproved(true))
                    {
                        deposit.SetError("PayInStatusReq failed");
                        //deposit.PrintToJsonFile("ConfirmDeposit-4");  // toDo: Remove, debugging
                        break;
                    }
                }

                if (!deposit.bZeroAuth)
                {
                    if (deposit.IsApproved() && !deposit.IsFullyApproved()
                       //&& false  // Void Deposit if not fully approved
                       )
                    {
                        if ("Sale".Equals(deposit.TxType, StringComparison.InvariantCultureIgnoreCase)) 
                            status = await RefundDeposit(deposit);
                        else 
                            status = await VoidDeposit(deposit);

                        //deposit.PrintToJsonFile("ConfirmDeposit-5");  // toDo: Remove, debugging
                        return deposit.Status;
                    }
                }
                //else
                //    deposit.RespPay = new NuveiPay(deposit.RespPay.Currency, 0M);  // toDo

            } while (false);

            // toDo: TxId 7854237322927583, Nuvei TransactionID 2130000005051346182, PayInReq Exception: The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.
            if (deposit.Status == ResponseStatus_Ext.Exception)
            {
                status = await CheckDeposit(deposit);
                if (!deposit.IsApproved(true))
                {
                    deposit.SetError("PayInStatusReq failed");
                    //deposit.PrintToJsonFile("ConfirmDeposit-4");  // toDo: Remove, debugging
                }
            }

            //deposit.PrintToJsonFile("ConfirmDeposit-Result");  // toDo: Remove, debugging
            var ret = deposit.IsFullyApproved();
            return deposit.Status;
        }

        public static async Task<ResponseStatus_Ext> ConfirmDepositByApm(NuveiDeposit deposit, DmnDeposit? dMN = null, string? orderID = null, string? clientReqID = null)
        {
            //deposit.PrintToJsonFile("ConfirmDeposit");  // toDo: Remove, debugging
            ResponseStatus_Ext status;
            do
            {
                if (!deposit.IsReadyForPayInByApm ())
                {
                    deposit.SetError("Deposit not ready for PayIn by APM");
                    break;
                }
                status = deposit.SetRedirectedForApm(orderID, clientReqID);

                if (deposit.IsRedirected())
                {
                    if (dMN is null)
                        dMN = deposit._Dmn;

                    status = deposit.SetDmn(dMN);
                }

                if (!deposit.IsFailed() || deposit.HasDmn())
                {
                    status = await CheckDeposit(deposit);
                    if (!deposit.IsApproved(true))
                    {
                        deposit.SetError("PayInStatusReq failed");
                        //deposit.PrintToJsonFile("ConfirmDeposit-4");  // toDo: Remove, debugging
                        break;
                    }
                }

            } while (false);

            // toDo: TxId 7854237322927583, Nuvei TransactionID 2130000005051346182, PayInReq Exception: The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.
            if (deposit.Status == ResponseStatus_Ext.Exception)
            {
                status = await CheckDeposit(deposit);
                if (!deposit.IsApproved(true))
                {
                    deposit.SetError("PayInStatusReq failed");
                    //deposit.PrintToJsonFile("ConfirmDeposit-4");  // toDo: Remove, debugging
                }
            }

            //deposit.PrintToJsonFile("ConfirmDeposit-Result");  // toDo: Remove, debugging
            var ret = deposit.IsFullyApproved();
            return deposit.Status;
        }

        public static async Task<ResponseStatus_Ext> CheckDeposit(NuveiDeposit deposit, string? orderID = null, string? clientReqID = null, string? redirectTxID = null)
        {
            //deposit.PrintToJsonFile("CheckDeposit");  // toDo: Remove, debugging
            ResponseStatus_Ext status;
            do
            {
                if (!deposit.IsReadyForPayIn())
                {
                    deposit.SetError("Deposit not ready for PayIn");
                    break;
                }
                status = deposit.SetRedirected(orderID, clientReqID, redirectTxID);

                // toDo: TxId 7854237322927583, Nuvei TransactionID 2130000005051346182, PayInReq Exception: The request was canceled due to the configured HttpClient.Timeout of 30 seconds elapsing.
                if (deposit.Status == ResponseStatus_Ext.Exception || !deposit.IsFailed())
                {
                    // PayInStatusReq
                    status = await NuveiDeposit.WrapPayInStatusReq(deposit);
                    if (!deposit.IsApproved())
                    {
                        deposit.SetError("PayInStatusReq failed");
                        break;
                    }
                }

                // ErrCode: 1125, ErrReason: UPO Payment method is not supported by this method
                //if (deposit.IsApproved() && !deposit.IsFullyApproved()
                //      && false  // Refund Deposit if not fully approved
                //    )
                //{
                //    status = await RefundDeposit(deposit);
                //    return deposit.Status;
                //}
            } while (false);

            //deposit.PrintToJsonFile("CheckDeposit-Result");  // toDo: Remove, debugging
            var ret = deposit.IsFullyApproved();
            return deposit.Status;
        }

        public static async Task<ResponseStatus_Ext> VoidDeposit(NuveiDeposit deposit, string relTxID = "")
        {
            //deposit.PrintToJsonFile("VoidDeposit");  // toDo: Remove, debugging
            deposit.PayTxID = deposit.RelTxID = NuveiCommon.CopyProperty(relTxID, deposit.RelTxID);
            if (deposit.RespPay is null || !deposit.RespPay.IsInit())
                deposit.RespPay = deposit.ReqPay;

            ResponseStatus_Ext status;
            do
            {
                if (!deposit.IsReadyForPayIn())
                {
                    deposit.SetError("Deposit not ready for PayIn");
                    break;
                }

                if (string.IsNullOrWhiteSpace(deposit.RelTxID))
                {
                    deposit.SetError("Missing RelTransactionID");
                    break;
                }

                // SessionTokenReq
                status = await NuveiDeposit.WrapSessionTokenReq(deposit);
                if (!deposit.IsSuccessful())
                {
                    deposit.SetError("SessionTokenReq failed");
                    break;
                }

                // VoidPayReq
                status = await NuveiDeposit.WrapVoidPayReq(deposit);
                if (!deposit.IsVoided())
                    deposit.SetError("VoidPayInReq failed");
            } while (false);

            //deposit.PrintToJsonFile("VoidDeposit-Result");  // toDo: Remove, debugging
            return deposit.Status;
        }

        public static async Task<ResponseStatus_Ext> RefundDeposit(NuveiDeposit deposit, string relTxID = "")
        {
            //deposit.PrintToJsonFile("RefundDeposit");  // toDo: Remove, debugging
            deposit.PayTxID = deposit.RelTxID = NuveiCommon.CopyProperty(relTxID, deposit.RelTxID);
            if (deposit.RespPay is null || !deposit.RespPay.IsInit())
                deposit.RespPay = deposit.ReqPay;

            ResponseStatus_Ext status;
            do
            {
                if (!deposit.IsReadyForPayIn())
                {
                    deposit.SetError("Deposit not ready for PayIn");
                    break;
                }

                if (string.IsNullOrWhiteSpace(deposit.RelTxID) && !deposit.User.IsReadyForPayByOptionId(true))
                {
                    deposit.SetError("Deposit not ready for Refund");
                    break;
                }

                if (!"Sale".Equals(deposit.TxType, StringComparison.InvariantCultureIgnoreCase))
                {
                    deposit.SetError("Deposit not Sale type");
                    break;
                }

                // SessionTokenReq
                status = await NuveiDeposit.WrapSessionTokenReq(deposit);
                if (!deposit.IsSuccessful())
                {
                    deposit.SetError("SessionTokenReq failed");
                    break;
                }

                // RefundPayInReq
                status = await NuveiDeposit.WrapRefundPayInReq(deposit);
                if (!deposit.IsVoided())
                    deposit.SetError("RefundPayInReq failed");
            } while (false);

            //deposit.PrintToJsonFile("RefundDeposit-Result");  // toDo: Remove, debugging
            return deposit.Status;
        }
    }
}