using Newtonsoft.Json;
using System.Net;
using System.Security.Cryptography;
using System.Text;


// Task Asana "New PSP: Nuvei": SafeCharge API Wrapper
// See https://docs.nuvei.com/api/main/indexMain_v1_0.html?json#PaymentAPIOverview
namespace EG.SoarPay.PSP.Nuvei.SafeCharge_Wrapper
{
    /*****************************************************************************************************************************/

    public class ChallengeResult  // 3DSv2 ChallengeResults for 3DSv2 Deposits
    {
        public string? comment { get; set; }

        public string? threeDSServerTransID { get; protected set; }
        public string? messageType { get; protected set; }
        public string? messageVersion { get; protected set; }
        public string? transStatus { get; protected set; }

        public string? acsTransID { get; protected set; }
        public string? dsTransID { get; protected set; }
        public string? challengeCompletionInd { get; protected set; }
        public string? acsSignedContent { get; protected set; }

        public string? errorMessageType { get; protected set; }
        public string? errorComponent { get; protected set; }
        public string? errorCode { get; protected set; }
        public string? errorDescription { get; protected set; }
        public string? errorDetail { get; protected set; }

        public string? body { get; protected set; }
        public string? encodedBody { get; protected set; }
        public string? decodedBody { get; protected set; }
        public string? head { get; protected set; }
        public string? tail { get; protected set; }
        protected dynamic? dJson { get; set; }

        public ChallengeResult() {}

        // Helpers

        public bool IsValid(string? threeDSServerTransId = null, bool bIgnoreTransID = false)
        {
            if (string.IsNullOrWhiteSpace(messageType) || !messageType.Trim().Equals("CRes", StringComparison.InvariantCultureIgnoreCase))
            {
                comment = "CRes wrong messageType=" + messageType;
                return false;
            }

            if (!string.IsNullOrWhiteSpace(threeDSServerTransId) && !threeDSServerTransId.Equals(threeDSServerTransID))
            {
                comment = "CRes wrong threeDSServerTransID=" + threeDSServerTransID;

                if (!bIgnoreTransID)
                    return false;
            }

            return true;
        }

        public bool IsApproved(string? threeDSServerTransId = null, bool bIgnoreTransID = false, bool bAttempted = true)
        {
            if (!IsValid(threeDSServerTransId, bIgnoreTransID) || string.IsNullOrWhiteSpace(transStatus))
                return false;

            var status = transStatus.Trim().ToUpper();
            if (status.Equals("Y", StringComparison.InvariantCultureIgnoreCase))
                return true;

            return (bAttempted && status.Equals("A", StringComparison.InvariantCultureIgnoreCase));
        }

        public bool IsCancelled(string? threeDSServerTransId = null, bool bIgnoreTransID = false)
        {
            if (!IsValid(threeDSServerTransId, bIgnoreTransID) || string.IsNullOrWhiteSpace(transStatus))
                return false;

            var status = transStatus.Trim().ToUpper();
            if (!status.Equals("N", StringComparison.InvariantCultureIgnoreCase))
                return false;

            return (!string.IsNullOrWhiteSpace(challengeCompletionInd) && challengeCompletionInd.Equals("Y", StringComparison.InvariantCultureIgnoreCase));
        }

        public bool DecodeFromDJson(dynamic? DJson, string? Body = null, string? EncodedBody = null, string? DecodedBody = null, string? Head = null, string? Tail = null)
        {
            decodedBody = string.IsNullOrWhiteSpace(DecodedBody) ? null : DecodedBody;
            encodedBody = !string.IsNullOrWhiteSpace(decodedBody) || string.IsNullOrWhiteSpace(EncodedBody) ? null : EncodedBody;
            body = !string.IsNullOrWhiteSpace(decodedBody) || !string.IsNullOrWhiteSpace(encodedBody) ? null : Body;
            head = string.IsNullOrWhiteSpace(Head) ? null : Head;
            tail = string.IsNullOrWhiteSpace(Tail) ? null : Tail;
            dJson = DJson;
            if (DJson is null)
            {
                comment = "CRes decoding error";
                return false;
            }

            threeDSServerTransID = DJson.threeDSServerTransID;
            messageType = DJson.messageType;
            messageVersion = DJson.messageVersion;
            transStatus = DJson.transStatus;

            acsTransID = DJson.acsTransID;
            dsTransID = DJson.dsTransID;
            challengeCompletionInd = DJson.challengeCompletionInd;
            acsSignedContent = DJson.acsSignedContent;

            errorMessageType = DJson.errorMessageType;
            errorComponent = DJson.errorComponent;
            errorCode = DJson.errorCode;
            errorDescription = DJson.errorDescription;
            errorDetail = DJson.errorDetail;

            var ret = IsValid();
            if (ret)
                head = tail = body = encodedBody = decodedBody = null;
            else
                comment = "CRes not valid";

            return ret;
        }

        protected static object? DecodeFromBase64(string? encodedBody, ref string? decodedBody)
        {
            if (string.IsNullOrWhiteSpace(encodedBody))
                return null;

            decodedBody = NuveiCommon.DecodeFromBase64(encodedBody);
            if (string.IsNullOrWhiteSpace(decodedBody))
                return null;

            var dJson = JsonConvert.DeserializeObject(decodedBody);
            return dJson;
        }

        public static object? WrapDecodeFromBase64(string? encodedBody, ref string? decodedBody, int nRetry = 0)
        {
            object? dJson = null;
            try
            {
                if (nRetry == 1)
                    encodedBody = WebUtility.UrlDecode(encodedBody);
                else if ((nRetry >= 2) && (nRetry <= 3))
                    encodedBody += "=";

                dJson = DecodeFromBase64(encodedBody, ref decodedBody);
            }
            catch (Exception)
            {
                // Fix for Nuvei 3DS ACS Emulator (Challenge page) - sending incorrect encodedBody for suboptions from Challenge Cancel
                if (nRetry++ < 3)
                    dJson = WrapDecodeFromBase64(encodedBody, ref decodedBody, nRetry);
            }
            return dJson;
        }
    }

    /*****************************************************************************************************************************/

    public class DmnDeposit  // DMN notifications for APM Deposits
    {
        public string? comment { get; set; }
        public dynamic? dJson { get; protected set; }
        public string? body { get; protected set; }

        protected string? secretKey { get; set; }
        protected string? sha256hash { get; set; }

        public DmnDeposit(string? SecretKey = null) { secretKey = SecretKey; }

        public bool IsValid(string? apMethodName = null, string? txID = null, string? orderID = null, string? clientReqID = null, string? userID = null, string? paymentOptionID = null)
        {
            comment = "";
            var check = CheckKeyEqByValue("advanceResponseChecksum", sha256hash);
            check = CheckKeyEqByValue("type", "DEPOSIT", true, true) && check;
            check = CheckKeyEqByValue("payment_method", apMethodName) && check;
            check = CheckKeyEqByValue("clientUniqueId", txID) && check;
            check = CheckKeyEqByValue("PPP_TransactionID", orderID) && check;
            check = CheckKeyEqByValue("clientRequestId", clientReqID) && check;
            check = CheckKeyEqByValue("user_token_id", userID) && check;
            check = CheckKeyEqByValue("userPaymentOptionId", paymentOptionID) && check;
            if (!check)
                comment = "DMN Not valid: " + comment;

            return check;
        }

        public bool IsPending(string? apMethodName = null, string? txID = null, string? orderID = null, string? clientReqID = null, string? userID = null, string? paymentOptionID = null)
        {
            var check = IsError(apMethodName, txID, orderID, clientReqID, userID, paymentOptionID);
            if (check)
                return false;

            comment = "";
            check = CheckKeyEqByValue("ppp_status", "OK", true, true);
            check = CheckKeyEqByValues("Status", "PENDING", "UPDATE", true, true, false, true) && check;
            if (!check)
                return false;

            check = CheckKeyEmpty("errApmDescription", true, true, true);
            check = CheckKeyEmpty("errScDescription", true, true, true) && check;
            check = CheckKeyEqByValue("errScCode", "0", true, true, true) && check;
            check = CheckKeyEqByValue("ExErrCode", "0", true, true, true) && check;
            check = CheckKeyEqByValue("errApmCode", "0", true, true, true) && check;
            CheckKeyEmpty("message", true, true, true);
            //return check;  // toDo

            return true;
        }

        public bool IsApproved(string? apMethodName = null, string? txID = null, string? orderID = null, string? clientReqID = null, string? userID = null, string? paymentOptionID = null)
        {
            var check = IsError(apMethodName, txID, orderID, clientReqID, userID, paymentOptionID);
            check = check || IsDeclined(apMethodName, txID, orderID, clientReqID, userID, paymentOptionID);
            check = check || IsPending(apMethodName, txID, orderID, clientReqID, userID, paymentOptionID);
            if (check)
                return false;

            comment = "";
            check = CheckKeyEqByValue("ppp_status", "OK", true, true);
            check = CheckKeyEqByValue("Status", "APPROVED", true, true, false, true) && check;
            if (!check)
                return false;

            check = !CheckKeyEmpty("currency", true, false, true);
            check = !CheckKeyEmpty("totalAmount", true, false, true) && check;
            CheckKeyEmpty("message", true, true, true);
            //return check;  // toDo

            return true;
        }

        public bool IsDeclined(string? apMethodName = null, string? txID = null, string? orderID = null, string? clientReqID = null, string? userID = null, string? paymentOptionID = null)
        {
            var check = IsError(apMethodName, txID, orderID, clientReqID, userID, paymentOptionID);
            if (check)
                return false;

            comment = "";
            check = CheckKeyEqByValue("ppp_status", "FAIL", true, true);
            check = CheckKeyEqByValue("Status", "DECLINED", true, true, false, true) && check;
            if (!check)
                return false;

            check = !CheckKeyEmpty("errApmDescription", true, true, true);
            check = !CheckKeyEmpty("errScDescription", true, true, true) || check;
            check = !CheckKeyEqByValue("errApmCode", "0", true, true, true) || check;
            check = !CheckKeyEqByValue("errScCode", "0", true, true, true) || check;
            check = !CheckKeyEqByValue("ExErrCode", "0", true, true, true) || check;
            CheckKeyEmpty("message", true, true, true);
            //return check;  // toDo

            return true;
        }

        public bool IsCancelledByUser(string? apMethodName = null, string? txID = null, string? orderID = null, string? clientReqID = null, string? userID = null, string? paymentOptionID = null)
        {
            var check = IsDeclined(apMethodName, txID, orderID, clientReqID, userID, paymentOptionID);
            if (!check)
                return false;

            comment = "";
            check = CheckKeyEqByValue("ppp_status", "FAIL", true, true);
            check = CheckKeyEqByValue("Status", "DECLINED", true, true, false, true) && check;
            if (!check)
                return false;

            check = CheckKeyEqByValue("errApmDescription", "User Cancelation", true, true, false, true);
            check = CheckKeyEqByValue("errScDescription", "Default", true, true, true, true) && check;
            check = CheckKeyEqByValue("errScCode", "9999", true, true, true, true) && check;
            if (check)
                return true;

            check = CheckKeyEqByValue("errApmCode", "0", true, true, true);
            check = CheckKeyEqByValue("ExErrCode", "0", true, true, true) && check;
            CheckKeyEmpty("message", true, true, true);
            //return check;  // toDo

            return true;
        }

        public bool IsError(string? apMethodName = null, string? txID = null, string? orderID = null, string? clientReqID = null, string? userID = null, string? paymentOptionID = null)
        {
            var check = IsValid(apMethodName, txID, orderID, clientReqID, userID, paymentOptionID);
            if (!check)
                return true;

            check = CheckKeyEqByValue("Status", "DECLINED", true, true);
            if (check)
                return false;

            comment = "";
            check = CheckKeyEqByValue("ppp_status", "FAIL", true, true);
            check = CheckKeyEqByValue("Status", "ERROR", true, true, false, true) && check;
            check = !CheckKeyEqByValue("ErrCode", "0", true, true, true) || check;
            check = !CheckKeyEqByValue("ReasonCode", "0", true, true, true) || check;
            check = !CheckKeyEmpty("Reason", true, true, true) || check;
            if (!check)
                return false;

            check = !CheckKeyEmpty("errApmDescription", true, true, true);
            check = !CheckKeyEmpty("errScDescription", true, true, true) || check;
            check = !CheckKeyEqByValue("errApmCode", "0", true, true, true) || check;
            check = !CheckKeyEqByValue("errScCode", "0", true, true, true) || check;
            check = !CheckKeyEqByValue("ExErrCode", "0", true, true, true) || check;
            CheckKeyEmpty("message", true, true, true);
            //return check;  // toDo

            return true;
        }

        public string? GetStatus()
        {
            var value = GetValueByKey("Status");
            return value;
        }

        public string? GetMessage()
        {
            var value = GetValueByKey("message");
            return value;
        }

        public string? GetTransactionID()
        {
            var value = GetValueByKey("TransactionID");
            return value;
        }

        public string? GetCurrency()
        {
            var value = GetValueByKey("currency");
            return value;
        }

        public string? GetTotalAmount()
        {
            var value = GetValueByKey("totalAmount");
            return value;
        }

        // Helpers

        protected bool CheckKeyEmpty(string? key, bool bTrim = false, bool bAllowMissing = false, bool bAlwaysPrint = false)
        {
            if (string.IsNullOrWhiteSpace(key))
                return true;

            var prop = GetValueByKey(key);
            if (string.IsNullOrWhiteSpace(prop))
            {
                if (bAllowMissing)
                    return true;

                AddComment("missing " + key);
                return false;
            }

            if (bTrim)
                prop = prop.Trim();

            if (bAlwaysPrint)
                AddComment(key + "=" + prop, true, false);

            return false;
        }

        protected bool CheckKeyEqByValue(string? key, string? value, bool bTrim = false, bool bInvariantCheck = false, bool bAllowMissing = false, bool bAlwaysPrint = false, bool bPrintWrong = true)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return true;

            var prop = GetValueByKey(key);
            if (string.IsNullOrWhiteSpace(prop))
            {
                if (bAllowMissing) 
                    return true;

                AddComment("missing " + key);
                return false;
            }

            if (bTrim)
            {
                value = value.Trim();
                prop = prop.Trim();
            }

            var check = bInvariantCheck ? value.Equals(prop, StringComparison.InvariantCultureIgnoreCase) : value.Equals(prop);
            bPrintWrong = bPrintWrong && !check;
            if (bAlwaysPrint || !check)
            {
                if (bPrintWrong)
                    AddComment("wrong ");

                AddComment(key + "=" + prop, !bPrintWrong, false);
            }
            return check;
        }

        protected bool CheckKeyEqByValues(string? key, string? value1, string? value2, bool bTrim = false, bool bInvariantCheck = false, bool bAllowMissing = false, bool bAlwaysPrint = false, bool bPrintWrong = true)
        {
            if (string.IsNullOrWhiteSpace(key))
                return true;

            if (string.IsNullOrWhiteSpace(value1))
                return CheckKeyEqByValue(key, value2, bTrim, bInvariantCheck, bAllowMissing, bAlwaysPrint, bPrintWrong);
            else if (string.IsNullOrWhiteSpace(value2))
                return CheckKeyEqByValue(key, value1, bTrim, bInvariantCheck, bAllowMissing, bAlwaysPrint, bPrintWrong);

            var prop = GetValueByKey(key);
            if (string.IsNullOrWhiteSpace(prop))
            {
                AddComment("missing " + key);
                return false;
            }

            if (bTrim)
            {
                value1 = value1.Trim();
                value2 = value2.Trim();
                prop = prop.Trim();
            }

            var check = bInvariantCheck ? value1.Equals(prop, StringComparison.InvariantCultureIgnoreCase) : value1.Equals(prop);
            check = check || (bInvariantCheck ? value2.Equals(prop, StringComparison.InvariantCultureIgnoreCase) : value2.Equals(prop));
            if (!check)
            {
                if (bPrintWrong)
                    AddComment("wrong ");

                AddComment(key + "=" + prop, !bPrintWrong, false);
                return false;
            }
            return check;
        }

        protected void AddComment(string? str, bool bComma = true, bool bCapital = true)
        {
            if (string.IsNullOrWhiteSpace(str))
                return;

            if (bComma && !string.IsNullOrWhiteSpace(comment))
                comment += ", ";

            if (bCapital)
                str = char.ToUpper(str[0]) + str.Substring(1).ToLower();

            comment += str;
            return;
        }

        protected string? GetTimeStamp()
        {
            var value = GetValueByKey("responseTimeStamp");
            return value;
        }

        protected string? GetPppTransactionID()
        {
            var value = GetValueByKey("PPP_TransactionID");
            return value;
        }

        public string? GetProductID()
        {
            var value = GetValueByKey("productId");
            return value;
        }

        protected string? GetValueByKey(string? key)
        {
            string? value = null;
            try
            {
                if (dJson is not null && !string.IsNullOrWhiteSpace(key))
                    value = dJson[key];
            }
            catch (Exception)
            {
            }
            return value;
        }

        public bool DecodeFromBody(string? Body)
        {
            body = Body;
            if (string.IsNullOrWhiteSpace(Body))
                return false;

            var json = WebUtility.UrlDecode(Body);
            json = "{\"" + json + "\"}";
            json = json.Replace("=", "\": \"");
            json = json.Replace("&", "\", \"");
            var ret = DecodeFromDJson(json);
            if (!ret)
            {
                comment = "DMN decoding error" + comment;
                return false;
            }

            var str = "" + secretKey + GetTotalAmount() + GetCurrency() +
                      GetTimeStamp() + GetPppTransactionID() +
                      GetStatus() + GetProductID();
            var bytesIn = ASCIIEncoding.ASCII.GetBytes(str.Replace("+", " "));
            var bytesOut = new SHA256CryptoServiceProvider().ComputeHash(bytesIn);
            sha256hash = BitConverter.ToString(bytesOut).Replace("-", "").ToLower();

            ret = IsValid();
            return ret;
        }

        protected bool DecodeFromDJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                dJson = JsonConvert.DeserializeObject(json);
                return dJson is not null;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}