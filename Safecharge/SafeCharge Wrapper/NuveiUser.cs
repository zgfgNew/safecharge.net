using EG.SoarPay.Model;
using Safecharge.Model.Common;
using Safecharge.Model.PaymentOptionModels;
using Safecharge.Model.PaymentOptionModels.CardModels;
using Safecharge.Utils;

//using sfcm = Safecharge.Model;
//using Nuvei.Model.PaymentOptionModels;  // keep commented Nuvei namespaces (same namespaces names, ambiguites when compiling)


// Task Asana "New PSP: Nuvei": SafeCharge API Wrapper
// See https://docs.nuvei.com/api/main/indexMain_v1_0.html?json#PaymentAPIOverview
namespace EG.SoarPay.PSP.Nuvei.SafeCharge_Wrapper
{
    /*****************************************************************************************************************************/

    public class NuveiUser
    {
        public CardData? PayInCard { get; protected set; }

        public UserAddress? BillAddress { get; protected set; } // Billing address for the user (min. required: email, country)

        public UserPaymentOption? PayByOptionId { get; protected set; }  // Nuvei ID for user who already completed a previous successful payin/deposit/payment

        protected AlternativePaymentMethod? ApMethod { get; set; }  // APMs - Alternative Payment Method credentials
        public AlternativePaymentMethod? GetApMethod()
        {
            if (ApMethod is null || !ApMethod.IsInit())
                return null;

            return ApMethod;
        }
        public string? GetApMethodName()
        {
            var apMethod = GetApMethod();
            if (apMethod is null)
                return null;

            return apMethod.GetName();
        }

        public string? APSubMethod { get; protected set; }  // APMs - Alternative Payment SubMethod
        protected bool SetAPSubMethod(string? apSubMethod)
        {
            if (apSubMethod is not null)
                apSubMethod = apSubMethod.Trim();

            if (!string.IsNullOrWhiteSpace(apSubMethod))
                APSubMethod = apSubMethod;

            var ret = !string.IsNullOrWhiteSpace(APSubMethod);
            return ret;
        }

        public CashierUserDetails? UserDetails { get; protected set; }

        public string? CcCardNum { get; protected set; }  // masked card number, last four digits for payins/deposits/payments and payouts/withdrawals by OptionId

        public string? BIN { get; protected set; }  // Bank Identification Number

        public string? ChallengePref { get; protected set; }  // Preference for 3DSv2 Challenge

        private string? cardBrand = null;
        public string? CardBrand
        {
            get { return cardBrand; }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                    cardBrand = NuveiCommon.CopyProperty(value.Trim(), cardBrand);
            }
        }

        public string? TokenID { get; protected set; }  // our unique ID for the user

        public NuveiUser(string? tokenID, string? firstName, string? lastName, string? country, string? email, string? phone = null, string? dateOfBirth = null, uint challengePref = 0)
        {
            Init(tokenID, firstName, lastName, country, email, phone, dateOfBirth, challengePref);
        }

        private void Init(string? tokenID, string? firstName, string? lastName, string? country, string? email, string? phone = null, string? dateOfBirth = null, uint challengePref = 0)
        {
            Guard.RequiresNotNull(tokenID, "TokenID");
            Guard.RequiresLengthBetween(tokenID?.Length, 5, 30, "TokenID");
            TokenID = tokenID;

            switch (challengePref)
            {
                case 1:  // Challenge requested
                    ChallengePref = "01";
                    break;

                case 2:  // Exemption requested
                    ChallengePref = "02";
                    break;

                case 3:  // No preference
                default:
                    ChallengePref = null;
                    break;
            }

            if (BillAddress is null)
                BillAddress = new UserAddress();

            BillAddress.FirstName = NuveiCommon.CopyProperty(firstName, BillAddress.FirstName);
            BillAddress.LastName = NuveiCommon.CopyProperty(lastName, BillAddress.LastName);
            BillAddress.Country = NuveiCommon.CopyProperty(country, BillAddress.Country);
            BillAddress.Email = NuveiCommon.CopyProperty(email, BillAddress.Email);
            BillAddress.Phone = NuveiCommon.CopyProperty(phone, BillAddress.Phone);

            if (UserDetails is null)
                UserDetails = new CashierUserDetails();

            UserDetails.FirstName = NuveiCommon.CopyProperty(firstName, UserDetails.FirstName);
            UserDetails.LastName = NuveiCommon.CopyProperty(lastName, UserDetails.LastName);
            UserDetails.Country = NuveiCommon.CopyProperty(country, UserDetails.Country);
            UserDetails.Email = NuveiCommon.CopyProperty(email, UserDetails.Email);
            UserDetails.Phone = NuveiCommon.CopyProperty(phone, UserDetails.Phone);
            UserDetails.DateOfBirth = NuveiCommon.CopyProperty(dateOfBirth, UserDetails.DateOfBirth);
        }
        public bool IsInit()
        {
            var ret = !string.IsNullOrWhiteSpace(TokenID)
                      && BillAddress is not null
                      && !string.IsNullOrWhiteSpace(BillAddress.FirstName)
                      && !string.IsNullOrWhiteSpace(BillAddress.LastName)
                      && !string.IsNullOrWhiteSpace(BillAddress.Country)
                      && !string.IsNullOrWhiteSpace(BillAddress.Email)

                      && UserDetails is not null
                      && !string.IsNullOrWhiteSpace(UserDetails.FirstName)
                      && !string.IsNullOrWhiteSpace(UserDetails.LastName)
                      && !string.IsNullOrWhiteSpace(UserDetails.Country)
                      && !string.IsNullOrWhiteSpace(UserDetails.Email);
            return ret;
        }

        public bool SetForPayInByTempToken(string holderName, string ccTempToken, string? cVV = null)  // For payins/deposits/payments initiated by Web-SDK sfc.getToken()
        {
            if (!NuveiCommon.IsCvvValid(cVV))
                cVV = null;

            PayInCard = new CardData
            {
                CardHolderName = holderName,
                CcTempToken = ccTempToken,
                CVV = cVV
            };

            var ret = IsReadyForPayInByTempToken();
            return ret;
        }
        public bool IsReadyForPayInByTempToken()  // For payins/deposits/payments initiated by Web-SDK sfc.getToken()
        {
            var ret = PayInCard is not null
                      && !string.IsNullOrWhiteSpace(PayInCard.CardHolderName)
                      && !string.IsNullOrWhiteSpace(PayInCard.CcTempToken)
                      && !string.IsNullOrWhiteSpace(PayInCard.CVV);
            return ret;
        }

        public bool SetForPayByOptionId(string optionId, string? cVV = null, string? maskedCardNum = null, string? holderName = null, bool bWithdrawal = false)  // For payins/deposits/payments and payouts/withdrawals by Nuvei ID (user who already completed a previous successful payin/deposit/payment)
        {
            if (string.IsNullOrWhiteSpace(optionId))
                return false;

            if (!bWithdrawal && !NuveiCommon.IsCvvValid(cVV))
                return false;

            if (PayByOptionId is not null)
                cVV = NuveiCommon.CopyProperty(PayByOptionId.CVV, cVV, false);

            if (PayInCard is not null)
            {
                holderName = NuveiCommon.CopyProperty(PayInCard.CardHolderName, holderName, false);
                cVV = NuveiCommon.CopyProperty(PayInCard.CVV, cVV, false);
            }

            CcCardNum = NuveiCommon.CopyProperty(maskedCardNum, CcCardNum);

            if (PayByOptionId is null)
                PayByOptionId = new UserPaymentOption();

            PayByOptionId.UserPaymentOptionId = NuveiCommon.CopyProperty(optionId, PayByOptionId.UserPaymentOptionId);
            PayByOptionId.CVV = NuveiCommon.CopyProperty(cVV, PayByOptionId.CVV);

            if (PayInCard is null)
                PayInCard = new CardData();

            PayInCard.CardHolderName = holderName;
            PayInCard.CVV = cVV;

            var ret = IsReadyForPayByOptionId(bWithdrawal);
            return ret;
        }
        public bool ExtendForPayByOptionId(string optionId, string? maskedCardNum, string? expYear, string? expMonth, string? bin)  // For payins/deposits/payments and payouts/withdrawals by Nuvei ID (user who already completed a previous successful payin/deposit/payment)
        {
            if (string.IsNullOrWhiteSpace(optionId))
                return false;

            CcCardNum = NuveiCommon.CopyProperty(maskedCardNum, CcCardNum);
            BIN = NuveiCommon.CopyProperty(bin, BIN);

            if (PayByOptionId is null)
                PayByOptionId = new UserPaymentOption();

            PayByOptionId.UserPaymentOptionId = NuveiCommon.CopyProperty(optionId, PayByOptionId.UserPaymentOptionId);

            if (PayInCard is not null)
                PayByOptionId.CVV = NuveiCommon.CopyProperty(PayInCard.CVV, PayByOptionId.CVV);

            if (!string.IsNullOrWhiteSpace(expYear) || !string.IsNullOrWhiteSpace(expMonth))
            {
                if (PayInCard is null)
                    PayInCard = new CardData();

                PayInCard.ExpirationYear = NuveiCommon.CopyProperty(expYear, PayInCard.ExpirationYear);
                PayInCard.ExpirationMonth = NuveiCommon.CopyProperty(expMonth, PayInCard.ExpirationMonth);
            }

            var ret = IsReadyForPayByOptionId();
            return ret;
        }
        public bool IsReadyForPayByOptionId(bool bWithdrawal = false)  // For payins/deposits/payments and payouts/withdrawals by Nuvei ID (user who already completed a previous successful payin/deposit/payment)
        {
            var ret = PayByOptionId is not null
                      && !string.IsNullOrWhiteSpace(PayByOptionId.UserPaymentOptionId)
                      && (bWithdrawal || NuveiCommon.IsCvvValid(PayByOptionId.CVV));

            return ret;
        }

        public bool SetForPayByApm(AlternativePaymentMethod? apMethod, string? apSubMethod = null, string optionId = null)  // APMs - For payins/deposits/payments by Alternative Payment Methods
        {
            ApMethod = apMethod;
            var ret = SetAPSubMethod(apSubMethod);
            ret = SetForPayByOptionId(optionId, null, null, null, true);

            ret = IsReadyForPayByApm();
            return ret;
        }
        public bool IsReadyForPayByApm()  // APMs - For payins/deposits/payments by Alternative Payment Methods
        {
            var ret = GetApMethod() is not null;
            ret = ret && IsReadyForPayByOptionId(true);
            return ret;
        }

        public bool SetForPayInByCard(string holderName, string cardNumber, string expYear, string expMonth, string cVV)  // For payins/deposits/payments by credit card data
        {
            if (!NuveiCommon.IsCvvValid(cVV))
                return false;

            PayInCard = new CardData
            {
                CardHolderName = holderName,
                CardNumber = cardNumber,
                ExpirationYear = expYear,
                ExpirationMonth = expMonth,
                CVV = cVV
            };

            var ret = IsReadyForPayInByCard();
            return ret;
        }
        public bool IsReadyForPayInByCard()  // For payins/deposits/payments by credit card data
        {
            var ret = PayInCard is not null
                       && !string.IsNullOrWhiteSpace(PayInCard.CardHolderName)
                       && !string.IsNullOrWhiteSpace(PayInCard.CardNumber)
                       && !string.IsNullOrWhiteSpace(PayInCard.ExpirationYear)
                       && !string.IsNullOrWhiteSpace(PayInCard.ExpirationYear)
                       && NuveiCommon.IsCvvValid(PayInCard.CVV);
            return ret;
        }
    }
}