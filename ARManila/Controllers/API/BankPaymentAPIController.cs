using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using ARManila.Models;

namespace ARManila.Controllers.API
{
    [Authorize]
    public class BankPaymentAPIController : ApiController
    {
        private LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();

        public HttpResponseMessage Put(PaymentDetailWrapper bankpayment)
        {
            var response = Request.CreateResponse<PaymentDetailWrapper>(HttpStatusCode.OK, bankpayment);
            return response;
        }

        // POST: api/BankPayment
        public async System.Threading.Tasks.Task<HttpResponseMessage> PostAsync([FromBody] BankPaymentWrapper bankpayment)
        {
            try
            {
                if (bankpayment.paymentDetails.Count < 1) throw new Exception("No payment details provided!");
                if (bankpayment.Amount == null || bankpayment.Amount <= 0) throw new Exception("Invalid amount!");
                if (bankpayment.BankDate == null) throw new Exception("Invalid bank date!");
                if (bankpayment.PostingDate == null) throw new Exception("Invalid posting date!");
                if (bankpayment.TransactionNo.Length < 5) throw new Exception("Invalid transaction no!");
                if (bankpayment.BankId == null) throw new Exception("Invalid bank!");
                if (bankpayment.StudentNo == null || bankpayment.StudentNo.Length < 7) throw new Exception("Invalid student number!");

                bankpayment.EmployeeNo = RequestContext.Principal.Identity.Name;
                Employee employee = db.Employee.Where(m => m.EmployeeNo == bankpayment.EmployeeNo).FirstOrDefault();
                if (employee == null) throw new Exception("Employee not found!");
                Student student = db.Student.Where(m => m.StudentNo == bankpayment.StudentNo).FirstOrDefault();
                if (student == null) throw new Exception("Student number not found!");
                Bank bank = db.Bank.Find(bankpayment.BankId);
                if (bank == null) throw new Exception("Bank not found!");

                decimal totalamount = 0;
                foreach (var item in bankpayment.paymentDetails)
                {
                    if (item.PaycodeId == null) throw new Exception("Invalid paycode!");
                    var paycode = db.Paycode.Find(item.PaycodeId);
                    if (paycode == null) throw new Exception("Paycode not found!");
                    if (item.Amount == null || item.Amount <= 0) throw new Exception("Invalid paymet detail amount!");
                    totalamount += item.Amount.Value;
                }
                if (bankpayment.Amount != totalamount) throw new Exception("Invalid total amount.");
                var studentcurriculum = db.Student_Curriculum.Where(m => m.StudentID == student.StudentID && m.Status == 1).FirstOrDefault();
                var educationallevelid = studentcurriculum == null ? 4 : studentcurriculum.Curriculum.EducationalLevel.Value;
                var period = db.PaymentDefaultPeriod.Where(m => m.EducationalLevelId == educationallevelid).FirstOrDefault();
                DateTime datereceived = bankpayment.PostingDate.HasValue ? bankpayment.PostingDate.Value : DateTime.Now;
                string transactionno = bankpayment.TransactionNo;
                var paymentexist = db.Payment.Where(m => m.Student.StudentNo == bankpayment.StudentNo && m.ORNo == "**" + transactionno && m.DateReceived == datereceived).FirstOrDefault();
                if (paymentexist != null)
                {
                    throw new Exception("Reference No/OR No already exists. Cannot post payment!");
                }

                Payment payment = new Payment
                {
                    Bank = bank.BankCode,
                    CashierID = employee.EmployeeID,
                    CheckAmount = (double)bankpayment.Amount.Value,
                    CheckNo = "Bank Deposit " + bank.BankCode,
                    DateReceived = datereceived,
                    Remarks = bankpayment.Remark,
                    BankDate = bankpayment.BankDate,
                    ORNo = "**" + transactionno,
                    SemID = period.PeriodId,
                    StudentID = student.StudentID,
                    EducLevel = educationallevelid,
                    CurriculumID = studentcurriculum == null ? null : studentcurriculum.CurriculumID
                };
                var doubleclickedpayment = db.Payment.Where(m => m.ORNo == payment.ORNo && m.StudentID == payment.StudentID).FirstOrDefault();
                if (doubleclickedpayment == null)
                {
                    db.Payment.Add(payment);
                    db.SaveChanges();
                    bankpayment.PaymentId = payment.PaymentID;
                    bool validatebackaccount = false;
                    bool validateenlistment = false;
                    foreach (var paycode in bankpayment.paymentDetails)
                    {
                        PaymentDetails paymentdetail = new PaymentDetails();
                        paymentdetail.Amount = (double)(paycode.Amount ?? 0);
                        paymentdetail.PaycodeID = paycode.PaycodeId.Value;
                        paymentdetail.PaymentID = payment.PaymentID;
                        db.PaymentDetails.Add(paymentdetail);
                        db.SaveChanges();
                        if (paycode.PaycodeId == 11)
                        {
                            validatebackaccount = true;
                        }
                        if (paycode.PaycodeId == 1)
                        {
                            validateenlistment = true;
                        }
                    }
                    if (validatebackaccount)
                    {
                        var backaccount = db.CheckStudentBackAccount(student.StudentID).FirstOrDefault();
                        if (backaccount != null)
                        {
                            BackAccountPayment backaccountpayment = new BackAccountPayment
                            {
                                BackAccountID = backaccount.BankAccountID,
                                PaymentID = payment.PaymentID
                            };
                            db.BackAccountPayment.Add(backaccountpayment);
                            db.SaveChanges();
                        }
                    }
                    if (validateenlistment)
                    {
                        var studentsection = db.Student_Section.FirstOrDefault(m => m.StudentID == payment.StudentID && m.Section.PeriodID == payment.SemID);
                        if (studentsection != null)
                        {
                            if (studentsection.ValidationDate == null)
                            {
                                studentsection.ValidationDate = bankpayment.PostingDate;
                                db.SaveChanges();
                            }

                        }
                    }
                }
                if (!String.IsNullOrEmpty(bankpayment.Email))
                {
                    var to = new List<string>();
                    to.Add(bankpayment.Email);
                    await Utility.SendMail("The amount of " + payment.CheckAmount.Value.ToString("#,##0.00") + " pesos deposited in " + payment.Bank + " has been posted in your account.", "Letran AR", to, "Payment Posted");
                }
                if (!String.IsNullOrEmpty(bankpayment.MobileNo))
                {
                    await Utility.SendSMS(bankpayment.MobileNo, "The amount of " + payment.CheckAmount.Value.ToString("#,##0.00") + " pesos deposited in " + payment.Bank + " has been posted in your account.", "Payment", payment.PaymentID);
                }
                var response = Request.CreateResponse<BankPaymentWrapper>(HttpStatusCode.OK, bankpayment);
                return response;
            }
            catch (Exception exception)
            {
                var response = Request.CreateErrorResponse(HttpStatusCode.Conflict, exception.Message);
                return response;
            }

        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private bool PaymentExists(int id)
        {
            return db.Payment.Count(e => e.PaymentID == id) > 0;
        }
    }
}
