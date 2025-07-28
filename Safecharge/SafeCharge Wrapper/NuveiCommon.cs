using AnyAscii;
using Newtonsoft.Json;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Safecharge;
using Safecharge.Model.Common;
using Safecharge.Utils.Enum;


// Task Asana "New PSP: Nuvei": SafeCharge API Wrapper
// See https://docs.nuvei.com/api/main/indexMain_v1_0.html?json#PaymentAPIOverview
namespace EG.SoarPay.PSP.Nuvei.SafeCharge_Wrapper
{
    /*****************************************************************************************************************************/

    public enum ResponseStatus_Ext
    {
        NotInit,
        Success,
        Error,
        Approved,
        Declined,
        Redirect,
        Voided,
        Exception
    }

    public enum PayType
    {
        NotDefined,
        Deposit,
        Withdrawal
    }

    public enum ByType
    {
        NotDefined,
        ByTempToken,
        ByOptionId,
        ByCard,
        ByApm
    }

    /*****************************************************************************************************************************/

    public class NuveiPay
    {
        public string? Currency { get; set; }

        public decimal? Amount { get; set; }
        public decimal GetAmount() { return Amount is null ? 0M : (decimal) Amount; }

        public NuveiPay(string? currency, decimal amount) { Set(currency, amount); }
        public NuveiPay(string? currency = null, string? strAmmount = null)
        {
            if (!string.IsNullOrWhiteSpace(strAmmount))
                strAmmount = strAmmount.Trim();

            if (string.IsNullOrWhiteSpace(strAmmount))
                return;

            var amount = NuveiCommon.StringToDecimal(strAmmount);
            Set(currency, amount);
        }

        protected bool Set(string? currency, decimal? amount)
        {
            if (!string.IsNullOrWhiteSpace(currency))
                currency = currency.Trim();

            if (string.IsNullOrWhiteSpace(currency) || amount is null)
                return false;

            Currency = currency.ToUpper();
            Amount = amount;

            var ret = IsInit();
            return ret;
        }
        public bool IsInit() { return !string.IsNullOrWhiteSpace(Currency) && Amount is not null && Amount > 0; }

        public bool Equals(NuveiPay? pay)
        {
            if (!IsInit() || !IsInit(pay))
                return false;

            return Currency.Equals(pay.Currency, StringComparison.InvariantCultureIgnoreCase) && Amount == pay.Amount;
        }

        public static bool IsInit(NuveiPay? pay) { return pay is not null && pay.IsInit(); }
    }

    /*****************************************************************************************************************************/

    public class AlternativePaymentMethod
    {
        public Dictionary<string, string>? APM { get; protected set; }
        public AlternativePaymentMethod(Dictionary<string, string>? apm = null)
        {
            APM = apm;
            var ret = IsInit();
        }

        public AlternativePaymentMethod(string? key, string? value)
        {
            Add(key, value);
        }

        public bool Add(string? key, string? value)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return false;

            if (APM is null)
                APM = new Dictionary<string, string>();

            key = key.Trim();
            APM[key] = value;

            var ret = IsInit();
            return ret;
        }

        public string? GetName()
        {
            string? name = GetParameter("paymentMethod");
            return name;
        }

        public string? GetParameter(string? label)
        {
            string? value = null;
            try
            {
                if (APM is not null && !string.IsNullOrWhiteSpace(label))
                    value = APM[label];
            }
            catch (Exception)
            {
            }
            return value;
        }

        public bool IsInit() { return (APM is not null && APM.Count > 0); }
    }

    ///*****************************************************************************************************************************/

    public class NuveiCommon
    {
        //private static HttpClientHandler _httpHandler = new HttpClientHandler();
        //private static HttpClient? _httpClient = new HttpClient(_httpHandler) { /*BaseAddress = new Uri(MerchantURL),*/ };
        private static SafechargeRequestExecutor? _reqExecutor = null;
        public static SafechargeRequestExecutor ReqExecutor()
        {
            if (_reqExecutor is null)
                _reqExecutor = new SafechargeRequestExecutor(/*_httpClient*/);  // Use SafeCharge's own HTTP Client

            return _reqExecutor;
        }

        public static MerchantInfo Merchant = new MerchantInfo
        {
            //MerchantKey = "FSZo1ogg1hGJjvAEYwMRii6t58ZDSMNtetmMaQxqVsQxjMDeopGRPbkGYy9UzRFK",  // (Test)
            //MerchantId = "707400637033335853",  // (Test)
            //MerchantSiteId = "221378",  // (Test)
            //MerchantKey = "2nljyzlSD9c4HaGlhSLi8jKZYBGSYaGSltnP7XsO9coNBcoFxQnzPy9kW9VaMUJw",  // (FundaloreMX)
            //MerchantId = "7999094040990337272",  // (FundaloreMX)
            //MerchantSiteId = "224948",  // (FundaloreMX)

            //ServerHost = ApiConstants.IntegrationHost,  // Nuvei Test environment, "https://ppp-test.safecharge.com/ppp/"
            //ServerHost = "https://secure.safecharge.com/ppp/",  // Nuvei Production

            HashAlgorithm = HashAlgorithmType.SHA256
        };

        public static UrlDetails URLDetails = new UrlDetails
        {
            //SuccessUrl = "https://4sg-test.eagle-gaming.com/payment/deposit-success",  // (Test)
            //FailureUrl = "https://4sg-test.eagle-gaming.com/payment/deposit-failure",  // (Test)
            //PendingUrl = "https://4sg-test.eagle-gaming.com/payment/deposit-success",  // (Test)
            //SuccessUrl = "https://www.fundalor.mx/payment/deposit-success",  // (FundaloreMX)
            //FailureUrl = "https://www.fundalor.mx/payment/deposit-failure",  // (FundaloreMX)
            //PendingUrl = "https://www.fundalor.mx/payment/deposit-success",  // (FundaloreMX)

            //NotificationUrl = "https://sandbox.soargaming.net/SoarPay.Nuvei/Api/CallbackDepositDMN/",  // DMNs for APM payments (Test)
            //BackUrl = "https://sandbox.soargaming.net/SoarPay.Nuvei/Api/CallbackDepositCRes/"  // Base64 encoded ChallengeResults for 3DSv2 payments (Test)
            //NotificationUrl = "https://soarpay.soargaming.net/PSP.Nuvei/Api/CallbackDepositDMN/",  // DMNs for APM payments (FundaloreMX)
            //BackUrl = "https://soarpay.soargaming.net/PSP.Nuvei/Api/CallbackDepositCRes/"  // Base64 encoded ChallengeResults for 3DSv2 payments (FundaloreMX)
        };

        public static string? AcsPrefixURL = null;
        //public static string? AcsPrefixURL = "https://docs.nuvei.com/3Dsimulator/simulator.php";  // Test AcsUrl prefix

        public static bool IsCvvValid(string? cvv)
        {
            var ret = !string.IsNullOrWhiteSpace(cvv)
                      && cvv.Length >= 3
                      && cvv.Length <= 4
                      && cvv.All(Char.IsNumber);
            return ret;
        }

        public static string CreateSessionID()
        {
            var randNum = GetNextRandom();
            var sessionID = HashToMD5(randNum.ToString());
            return sessionID;
        }

        public static string CreateTokennID()
        {
            var randNum = GetNextRandom();
            var tokenID = EncodeToBase64(randNum.ToString());
            return tokenID;
        }
        public static string? CopyProperty(string? src, string? dest, bool bOverwrite = true)
        {
            if (string.IsNullOrWhiteSpace(src))
                return dest;

            if (!bOverwrite && !string.IsNullOrWhiteSpace(dest))
                return dest;

            return src;
        }

        public static string? CopyLongerProperty(string? src, string? dest)
        {
            src = string.IsNullOrWhiteSpace(src) ? "" : src.Trim();
            dest = string.IsNullOrWhiteSpace(dest) ? "" : dest.Trim();
            var bOverwrite = src.Length > dest.Length;

            return CopyProperty(src, dest, bOverwrite);
        }

        public static string NormalizeName(string? src, bool bClean = true)
        {
            var name = "";
            try
            {
                if (string.IsNullOrWhiteSpace(src))
                    return name;

                name = TrimName(src);
                if (bClean)
                {
                    name = name.Replace('-', ' ');
                    name = name.Replace('+', ' ');
                    name = name.Replace(".", "");
                    name = name.Replace(",", "");
                }

                // AnyASCII: https://github.com/anyascii/anyascii/blob/master/README.md
                name = name.Transliterate();

                name = TrimName(name);
            }
            catch (Exception) { }
            return name;
        }

        public static string TrimName(string? src)
        {
            var name = "";
            if (string.IsNullOrWhiteSpace(src))
                return name;

            name = src.Trim();

            const string doubleSpace = "  ";
            const string singleSpace = " ";
            while (name.Contains(doubleSpace))
                name = name.Replace(doubleSpace, singleSpace);

            return name;
        }

        public static string JoinNames(string? first, string? last)
        {
            var name = TrimName(first);
            if (!string.IsNullOrWhiteSpace(last))
            {
                last = TrimName(last);
                if (!string.IsNullOrWhiteSpace(name))
                    name += " " + last;
                else
                    name = last;
            }
            return name;
        }

        public enum eMATCH
        {
            HOLDER_MISMATCH = 0,
            PARTIAL_MATCH = 1,
            MATCH = 2
        }

        public static bool CheckIfNameIncluded(string? fullName, string? checkFullName, ref string? currMatching, ref eMATCH currMatch)
        {
            fullName = TrimName(fullName);
            checkFullName = TrimName(checkFullName);
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(checkFullName))
                return currMatch >= eMATCH.PARTIAL_MATCH;

            if (currMatch >= eMATCH.MATCH)
                return true;

            var list = checkFullName.Split(" ");
            var checkList = list.Distinct().ToList();
            var check = false;
            string? matching = null;
            foreach (var name in checkList)
            {
                var checkName = NormalizeName(name);
                check = fullName.Contains(checkName, StringComparison.InvariantCultureIgnoreCase);
                if (!check)
                    break;

                matching = JoinNames(matching, checkName);
            }

            if (check)
            {
                currMatch = eMATCH.PARTIAL_MATCH;
                currMatching = CopyLongerProperty(currMatching, matching);
            }

            return currMatch >= eMATCH.PARTIAL_MATCH;
        }

        public static bool CheckAreNamesMatching(string? fullNameA, string? fullNameB, ref string? currMatching, ref eMATCH currMatch)
        {
            fullNameA = TrimName(fullNameA);
            fullNameB = TrimName(fullNameB);
            if (string.IsNullOrWhiteSpace(fullNameA) || string.IsNullOrWhiteSpace(fullNameB))
                return currMatch >= eMATCH.PARTIAL_MATCH;

            if (currMatch >= eMATCH.MATCH)
                return true;

            if (fullNameA.Equals(fullNameB, StringComparison.InvariantCultureIgnoreCase))
            {
                currMatch = eMATCH.MATCH;
                currMatching = fullNameA;
            }
            else if (fullNameA.Contains(fullNameB, StringComparison.InvariantCultureIgnoreCase))
            {
                currMatch = eMATCH.PARTIAL_MATCH;
                currMatching = CopyLongerProperty(currMatching, fullNameB);
            }
            else if (fullNameB.Contains(fullNameA, StringComparison.InvariantCultureIgnoreCase))
            {
                currMatch = eMATCH.PARTIAL_MATCH;
                currMatching = CopyLongerProperty(currMatching, fullNameA);
            }

            var check = CheckIfNameIncluded(fullNameA, fullNameB, ref currMatching, ref currMatch);
            check = CheckIfNameIncluded(fullNameB, fullNameA, ref currMatching, ref currMatch) || check;

            var list = string.IsNullOrWhiteSpace(currMatching) ? null : currMatching.Split(" ");
            var n = list is null ? 0 : list.Length;
            if (n <= 0)
                currMatch = eMATCH.HOLDER_MISMATCH;
            else if (n <= 1)
                currMatch = eMATCH.PARTIAL_MATCH;

            return currMatch >= eMATCH.PARTIAL_MATCH;
        }

        // Helpers

        private static Random? random = null;
        public static int GetNextRandom()
        {
            if (random == null)
                random = new Random();

            var randNum = random.Next();
            return randNum;
        }

        public static string? HashToMD5(string? src)
        {
            if (string.IsNullOrWhiteSpace(src))
                return null;

            var bytes = Encoding.UTF8.GetBytes(src);
            var md5 = MD5.Create();
            var str = BitConverter.ToString(md5.ComputeHash(bytes));
            return str.Replace("-", "");
        }

        public static string? EncodeToBase64(string? src)
        {
            if (string.IsNullOrWhiteSpace(src))
                return null;

            var bytes = Encoding.UTF8.GetBytes(src);
            var str = Convert.ToBase64String(bytes);
            return str;
        }

        public static string DecodeFromBase64(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            var json = Encoding.UTF8.GetString(base64EncodedBytes);
            return json;
        }

        public static dynamic? ReadFromJson(string? json)
        {
            if (json is null)
                return null;

            json = json.Trim();
            if (string.IsNullOrWhiteSpace(json))
                return null;

            var dJson = JsonConvert.DeserializeObject(json);
            return dJson;
        }

        public static string? EncodeToJson(object? src, bool bFormat = true)
        {
            string? str = bFormat ? "Null object" : null;
            if (src is null)
                return str;

            var format = bFormat ? Formatting.Indented : Formatting.None;
            str = JsonConvert.SerializeObject(src, format, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });

            if (bFormat)
            {
                if (str is null)
                    str = "Json error";
                else if (string.IsNullOrWhiteSpace(str))
                    str = "Empty Json";
            }
            else if (str is not null)
                str = str.Replace(" ", "");

            return str;
        }

        public static bool PrintToJsonFile(object? src, string? title = null, string? txID = null, string? info = null, bool bType = true)
        {
            var str = EncodeToJson(src);

            var objType = "";
            if (bType && src is not null)
                objType = src.GetType().ToString();

            var ret = PrintToFile(str, objType, title, txID, info);
            return ret;
        }

        public static bool PrintToFile(object? src, string fName, string? title = null, string? txID = null, string? info = null)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(fName) && !string.IsNullOrWhiteSpace(title))
                    fName += "-";

                fName += title;
                if (!string.IsNullOrWhiteSpace(fName) && !string.IsNullOrWhiteSpace(txID))
                    fName += "-";

                fName += txID + ".txt";
                var fPath = "C:\\Temp\\Nuvei\\" + fName;
                if (string.IsNullOrWhiteSpace(fPath))
                    return false;

                string? str = null;
                if (src is not null)
                    str = src.ToString();

                StreamWriter file = new StreamWriter(fPath);
                if (!string.IsNullOrWhiteSpace(info))
                    file.WriteLine(info);

                file.WriteLine(str);
                file.Close();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string? ReadStringFromFile(string fName)
        {
            if (string.IsNullOrWhiteSpace(fName) || !File.Exists(fName))
                return null;

            var file = new StreamReader(fName);
            var str = file.ReadToEnd();
            file.Close();

            return str;
        }

        public static decimal RoundDecimal(decimal value)
        {
            var round = Math.Round(value, 2, MidpointRounding.AwayFromZero);
            return round;
        }

        public static string DecimalToString(decimal value)
        {
            var round = RoundDecimal(value);
            var strValue = round.ToString("0.00", CultureInfo.InvariantCulture);
            return strValue;
        }

        public static decimal StringToDecimal(string strVvalue)
        {
            var value = Convert.ToDecimal(strVvalue, CultureInfo.InvariantCulture);
            return value;
        }
    }
}