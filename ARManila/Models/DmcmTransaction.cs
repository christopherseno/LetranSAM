using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace ARManila.Models
{
    public class DmcmTransaction
    {
        public static int PostCreditMemo(string username, int docnumlast, double amount, string particular,DateTime postingdate, int periodid,int studentid,int acadeptid, ChartOfAccounts account, SubChartOfAccounts subaccount, ChartOfAccounts araccount)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();            
            docnumlast++;
            DMCM debit = new DMCM();
            DMCM credit = new DMCM();
            credit.DocNum = docnumlast;
            debit.DocNum = docnumlast;
            credit.Amount =amount;
            debit.Amount = amount;
            credit.ChargeToStudentAr = true;
            debit.ChargeToStudentAr = false;
            credit.DC = "C";
            debit.DC = "D";
            credit.PeriodID = periodid;
            debit.PeriodID = periodid;
            credit.Remarks = particular;
            credit.StudentID = studentid;
            credit.AcaDeptID = acadeptid;
            credit.TransactionDate = postingdate;
            debit.Remarks = particular;
            debit.StudentID = studentid;
            debit.AcaDeptID = acadeptid;
            debit.TransactionDate = postingdate;
            if (subaccount != null)
            {
                debit.AcctID = subaccount.AcctID;
                debit.SubAcctID = subaccount.SubAcctID;
                debit.AccountName = subaccount.SubbAcctName;
                debit.AccountNumber = subaccount.SubAcctNo;
            }
            else
            {
                debit.AcctID = account.AcctID;
                debit.AccountName = account.AcctName;
                debit.AcctID = account.AcctID;
            }
            credit.AcctID = araccount.AcctID;
            credit.AccountName = araccount.AcctName;
            credit.AccountNumber = araccount.AcctNo;
            db.DMCM.Add(debit);
            db.DMCM.Add(credit);
            db.SaveChanges();
            var student = db.Student.Find(debit.StudentID);
            db.InsertDmcmTransactionLog(username, "Batch DMCM - CM - " + debit.AccountNumber + " - " + credit.Amount + " - " + credit.Remarks, student.StudentNo);
            return docnumlast;
        }

        public static int PostDebitMemo(string username, int docnumlast, double amount, string particular, DateTime postingdate, int periodid, int studentid, int acadeptid, ChartOfAccounts account, SubChartOfAccounts subaccount, ChartOfAccounts araccount)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            docnumlast++;
            DMCM debit = new DMCM();
            DMCM credit = new DMCM();
            debit.DocNum = docnumlast;
            credit.DocNum = docnumlast;
            debit.Amount = amount;
            credit.Amount = amount;
            debit.ChargeToStudentAr = true;
            credit.ChargeToStudentAr = false;
            debit.DC = "D";
            credit.DC = "C";
            debit.PeriodID = periodid;
            credit.PeriodID = periodid;
            debit.Remarks = particular;
            debit.StudentID = studentid;
            debit.AcaDeptID = acadeptid;
            debit.TransactionDate = postingdate;
            credit.Remarks = particular;
            credit.StudentID = studentid;
            credit.AcaDeptID = acadeptid;
            credit.TransactionDate = postingdate;
            if (subaccount != null)
            {
                credit.AcctID = subaccount.AcctID;
                credit.SubAcctID = subaccount.SubAcctID;
                credit.AccountName = subaccount.SubbAcctName;
                credit.AccountNumber = subaccount.SubAcctNo;
            }
            else
            {
                credit.AcctID = account.AcctID;
                credit.AccountName = account.AcctName;
                credit.AccountNumber = account.AcctNo;
            }
            debit.AcctID = araccount.AcctID;
            debit.AccountName = araccount.AcctName;
            debit.AccountNumber = araccount.AcctNo;
            db.DMCM.Add(debit);
            db.DMCM.Add(credit);
            db.SaveChanges();
            var student = db.Student.Find(debit.StudentID);
            db.InsertDmcmTransactionLog(username, "Batch DMCM - DM - " + credit.AccountNumber + " - " + debit.Amount + " - " + debit.Remarks, student.StudentNo);
            return docnumlast;
        }

    }
}