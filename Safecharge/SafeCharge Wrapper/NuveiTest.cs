using System.Diagnostics;


// Task Asana "New PSP: Nuvei": SafeCharge API Wrapper
// See https://docs.nuvei.com/api/main/indexMain_v1_0.html?json#PaymentAPIOverview
namespace EG.SoarPay.PSP.Nuvei.SafeCharge_Wrapper
{
    /*****************************************************************************************************************************/

    public class NuveiTest
    {
        public static async Task Test()  // Test Nuvei Deposits and Withdrawals via SafeCharge_Wrapper - method can be used as main() in the ConsoleApp
        {
            NuveiCommon.PrintToConsole = true;

            NuveiUser user;
            NuveiDeposit deposit;
            NuveiWithdrawal withdrawal;
            ResponseStatus_Ext status;
            string fName = "";

            //if (false)
            {
                user = new NuveiUser("monster72-MTM2NzMyNTEz", "Jane", "Swift", "US", "jane.swift@abc.uk.com");
                //user.SetForPayInByCard("FL-BRW1-EXMPT", "5333302221254276", "29", "12", "217");  // Non 3D Secure MC
                user.SetForPayByOptionId("96528988", "217", "5****4276", "29", "12", "533330");

                deposit = new NuveiDeposit(user, "PEN", 107.02M);
                status = await NuveiDeposit.Deposit(deposit, true);
                //fName = "C:\\Temp\\NuveiDeposit_" + deposit.SessionID + ".txt";
                //NuveiCommon.EncodeToJsonFile(deposit, fName);
                Debug.Assert(deposit.IsFullyApproved());

                withdrawal = new NuveiWithdrawal(user, "CHF", new decimal(77.03));
                status = await NuveiWithdrawal.Withdrawal(withdrawal);
                //fName = "C:\\Temp\\NuveiWithdrawal_" + withdrawal.SessionID + ".txt";
                //NuveiCommon.EncodeToJsonFile(withdrawal, fName);
                Debug.Assert(withdrawal.IsApproved());
            }

            if (false)
            {
                user = new NuveiUser("CL-BRW1-MTk2MTc2OTQ1", "John", "Smith", "US", "john.smith@safecharge.com");
                //user.SetForPayInByCard("CL-BRW1", "5333302221254276", "29", "12", "217");  // 3D Secure MC
                user.SetForPayByOptionId("96457798", "217", "5****4276", "29", "12", "533330");

                // Debugging ThreeDs ACS Emulator: Challenge Cancel / Transaction Timed out at ACS - First CReq not received by ACS
                for (var i = 0; i++ < 3;)
                {
                    deposit = new NuveiDeposit(user, "MXN", 100 + 0.1M * i);  // Redirect for amount > 100
                    status = await NuveiDeposit.Deposit(deposit, true);
                    //fName = "C:\\Temp\\NuveiDeposit_" + deposit.SessionID + ".txt";
                    //NuveiCommon.EncodeToJsonFile(deposit, fName);
                    Debug.Assert(!deposit.IsError());
                }
            }

            if (false)
            {
                user = new NuveiUser("CL-BRW2-NjU0MjMxMzY5", "Not", "Available", "PE", "peru@safecharge.com");
                //user.SetForPayInByCard("CL-BRW2", "4000027891380961", "29", "12", "234");  // 3D Secure Visa
                user.SetForPayByOptionId("96456598", "234", "4****0961", "29", "12", "400002");

                deposit = new NuveiDeposit(user, "DKK", 30);
                status = await NuveiDeposit.Deposit(deposit, true);
                Debug.Assert(deposit.IsFullyApproved());

                deposit = new NuveiDeposit(user, "CHF", 100.1M);
                status = await NuveiDeposit.Deposit(deposit, true);
                //fName = "C:\\Temp\\NuveiDeposit_" + deposit.SessionID + ".txt";
                //NuveiCommon.EncodeToJsonFile(deposit, fName);
                Debug.Assert(deposit.IsFullyApproved());

                withdrawal = new NuveiWithdrawal(user, "USD", 500);
                status = await NuveiWithdrawal.Withdrawal(withdrawal);
                //fName = "C:\\Temp\\NuveiWithdrawal_" + withdrawal.SessionID + ".txt";
                //NuveiCommon.EncodeToJsonFile(withdrawal, fName);
                Debug.Assert(withdrawal.IsApproved());
                return;
            }

            if (false)
            {
                user = new NuveiUser("CL-BRW3-MTQ1MzIxNDYxMg==", "Jane", "Smith", "US", "john.smith@safecharge.com");
                //user.SetForPayInByCard("CL-BRW3", "2221008123677736", "29", "12", "217");  // 3D Secure MC
                user.SetForPayByOptionId("96457618", "217", "2****7736", "29", "12", "222100");

                deposit = new NuveiDeposit(user, "HUF", 15);
                status = await NuveiDeposit.Deposit(deposit, true);
                Debug.Assert(deposit.IsFullyApproved());

                deposit = new NuveiDeposit(user, "PEN", NuveiCommon.StringToDecimal("250.12876"));  // Redirect for amount > 100
                status = await NuveiDeposit.Deposit(deposit, true);
                //fName = "C:\\Temp\\NuveiDeposit_" + deposit.SessionID + ".txt";
                //NuveiCommon.EncodeToJsonFile(deposit, fName);
                Debug.Assert(deposit.IsFullyApproved());

                //"TxStatus": "ERROR",
                //"ErrReason": "Pay Method Error: Country does not support the CFT program",, see https://www.jfdbrokers.com/documents/CFT-Program.pdf
                //"GwErrReason": "Country does not support the CFT program",
                //"GwErrCode": -1100,
                //"GwExtErrCode": 1187,
                //"PayMethodErrReason": "Country does not support the CFT program",
                //withdrawal = new NuveiWithdrawal(user, "GBP", new decimal(50.3345));
                //status = await NuveiWithdrawal.Withdrawal(withdrawal);
                //fName = "C:\\Temp\\NuveiWithdrawal_" + withdrawal.SessionID + ".txt";
                //NuveiCommon.EncodeToJsonFile(withdrawal, fName);
                //Debug.Assert(withdrawal.IsApproved());
            }

            if (false)
            {
                user = new NuveiUser("CL BRWA-MTE2NjY3ODMxOA==", "John", "Smith", "US", "john.smith@safecharge.com");
                //user.SetForPayInByCard("CL BRWA", "4000020951595032", "29", "12", "217");  // 3D Secure Visa
                user.SetForPayByOptionId("96457898", "217", "4****5032", "29", "12", "400002");

                deposit = new NuveiDeposit(user, "GBP", 1.7M);
                status = await NuveiDeposit.Deposit(deposit, true);
                Debug.Assert(deposit.IsFullyApproved());

                deposit = new NuveiDeposit(user, "PEN", 171.71M);
                status = await NuveiDeposit.Deposit(deposit, true);
                //fName = "C:\\Temp\\NuveiDeposit_" + deposit.SessionID + ".txt";
                //NuveiCommon.EncodeToJsonFile(deposit, fName);
                Debug.Assert(deposit.IsFullyApproved());

                withdrawal = new NuveiWithdrawal(user, "CHF", new decimal(71.71));
                status = await NuveiWithdrawal.Withdrawal(withdrawal);
                //fName = "C:\\Temp\\NuveiWithdrawal_" + withdrawal.SessionID + ".txt";
                //NuveiCommon.EncodeToJsonFile(withdrawal, fName);
                Debug.Assert(withdrawal.IsApproved());
            }
        }
    }
}