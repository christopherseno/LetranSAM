using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;

namespace ARManila.Controllers
{
    public class BackaccountController : BaseController
    {
        private readonly LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();

        public ActionResult FloatingPayment()
        {
            var floatingpayments = db.GetFloatingPayment();
            List<FloatingPayment> floatingPayments = new List<FloatingPayment>();
            foreach (var item in floatingpayments)
            {
                var period = db.Period.Find(item.SemID);
                var student = db.Student.Find(item.StudentID);
                floatingPayments.Add(new Models.FloatingPayment
                {
                    Amount = (decimal)(item.Amount ?? 0),
                    OrNo = item.ORNo,
                    PaymentId = item.PaymentID,
                    PeriodId = item.SemID ?? 0,
                    PeriodName = period.FullName,
                    StudentId = item.StudentID ?? 0,
                    StudentName = student.FullName,
                    StudentNo = student.StudentNo,
                    Date = item.DateReceived
                });
            }
            return View(floatingPayments);
        }
        public ActionResult FloatingDmcm()
        {
            var floatingdmcms = db.GetFloatingDMCM();
            List<FloatingDMCM> dmcms = new List<FloatingDMCM>();

            foreach (var item in floatingdmcms)
            {
                var period = db.Period.Find(item.PeriodID);
                var student = db.Student.Find(item.StudentID);
                dmcms.Add(new FloatingDMCM
                {
                    Amount = (decimal)(item.Amount ?? 0),
                    DmcmId = item.DMCMID,
                    DocNo = item.DocNum.ToString(),
                    PeriodId = item.PeriodID.Value,
                    PeriodName = period.FullName,
                    Remark = item.Remarks,
                    StudentId = item.StudentID.Value,
                    StudentName = student.FullName,
                    StudentNo = student.StudentNo,
                    Date = item.TransactionDate ?? DateTime.Today
                });
            }
            return View(dmcms);
        }
        public ActionResult Index()
        {
            var username = db.AspNetUsers.FirstOrDefault(m => m.UserName == User.Identity.Name);
            ViewBag.session = username.BearerToken;
            return View();
        }

        [HttpPost]
        public ActionResult Index(string studentno)
        {
            var username = db.AspNetUsers.FirstOrDefault(m => m.UserName == User.Identity.Name);
            ViewBag.session = username.BearerToken;
            var student = db.Student.FirstOrDefault(m => m.StudentNo.Equals(studentno));
            if (student == null) throw new Exception("Student Number not found!");
            return RedirectToAction("DisplayBackaccounts", new { studentno });
        }
        public ActionResult Add(int id)
        {
            var student = db.Student.Find(id);
            var assessments = db.Student_Section.Where(m => m.StudentID == id);
            List<SelectListItem> assessmentids = new List<SelectListItem>();
            foreach (var i in assessments)
            {
                assessmentids.Add(new SelectListItem
                {
                    Value = i.Student_SectionID.ToString(),
                    Text = i.Section.Period.FullName
                });
            }
            ViewBag.assessment = assessmentids;
            return View(student);
        }
        [HttpPost]
        public ActionResult Add(int StudentId, decimal Amount, int? AssessmentId)
        {
            var periodid = HttpContext.Request.Cookies["PeriodId"].Value.ToString();
            var period = db.Period.Find(Convert.ToInt32(periodid));
            var student = db.Student.Find(StudentId);
            db.BackAccount.Add(new BackAccount
            {
                SemID = period.PeriodID,
                StudentID = student.StudentID,
                Student_SectionID = AssessmentId,
                Balance = (double)Amount
            });
            db.SaveChanges();
            StringBuilder remarks = new StringBuilder();
            remarks.Append("Added backaccount to " + student.StudentNo + " for PeriodID " + period.PeriodID);
            remarks.Append(" amounting to " + Amount);
            db.InsertBackaccountTransactionLog(User.Identity.Name, remarks.ToString(), student.StudentNo);
            return RedirectToAction("DisplayBackaccounts", new { student.StudentNo });
        }
        public ActionResult DisplayBackaccounts(string studentno)
        {
            var student = db.Student.FirstOrDefault(m => m.StudentNo.Equals(studentno));
            if (student == null) throw new Exception("Student Number not found.");
            var id = student.StudentID;
            var enrollments = db.Student_Section.Where(m => m.StudentID == id && m.ValidationDate != null).OrderBy(m => m.Student_SectionID);
            ViewBag.enrollments = enrollments;

            var backaccounts = db.BackAccount.Where(m => m.StudentID == id);
            List<BackaccountWrapper> backaccountWrappers = new List<BackaccountWrapper>();
            List<int> paymentids = new List<int>();
            List<int> dmcmids = new List<int>();
            if (backaccounts == null || backaccounts.Count() == 0)
            {
                backaccountWrappers.Add(new BackaccountWrapper
                {
                    StudentId = id,
                    StudentName = student.FullName,
                    StudentNo = student.StudentNo
                });
            }
            else
            {
                backaccountWrappers = GetBackaccounts(backaccounts, paymentids, dmcmids);
            }
            var spCheckBackAccount = db.CheckStudentBackAccount(id);
            ViewBag.spbackaccount = spCheckBackAccount.FirstOrDefault();

            List<BackaccountPaymentWrapper> floatingbackaccountpayments = new List<BackaccountPaymentWrapper>();
            var floatingpayments = db.PaymentDetails.Where(m => m.Payment.StudentID == id && m.PaycodeID == 11 && !paymentids.Contains(m.PaymentID));
            foreach (var item in floatingpayments)
            {
                BackaccountPaymentWrapper backaccountPaymentWrapper = new BackaccountPaymentWrapper();
                backaccountPaymentWrapper.Amount = item.Amount;
                backaccountPaymentWrapper.ORNo = item.Payment.ORNo;
                backaccountPaymentWrapper.PaymentDate = item.Payment.DateReceived;
                backaccountPaymentWrapper.PaymentId = item.PaymentID;
                backaccountPaymentWrapper.Remarks = item.Payment.StudentID.HasValue ? item.Payment.Period.FullName : item.Payment.CheckNo;
                backaccountPaymentWrapper.Period = item.Payment.Period.FullName;
                floatingbackaccountpayments.Add(backaccountPaymentWrapper);
            }
            ViewBag.floatingbackaccountpayments = floatingbackaccountpayments;
            var floatingdmcms = db.DMCM.Where(m => m.StudentID == id && !dmcmids.Contains(m.DMCMID) && m.ChargeToStudentAr == true);
            List<BackaccountDMCMWrapper> floatingbackaccountdmcms = new List<BackaccountDMCMWrapper>();
            foreach (var item in floatingdmcms)
            {
                var dmcmforenrollment = enrollments.FirstOrDefault(m => m.Section.PeriodID == item.PeriodID && m.ValidationDate <= item.TransactionDate && m.StudentID == item.StudentID);
                if (dmcmforenrollment == null)
                {
                    BackaccountDMCMWrapper backaccountDMCMWrapper = new BackaccountDMCMWrapper();
                    var dmcm = db.DMCM.Find(item.DMCMID);
                    backaccountDMCMWrapper.DC = dmcm.DC;
                    backaccountDMCMWrapper.Amount = dmcm.Amount;
                    backaccountDMCMWrapper.DMCMId = item.DMCMID;
                    backaccountDMCMWrapper.DocNo = dmcm.DocNum;
                    backaccountDMCMWrapper.TransactionDate = dmcm.TransactionDate;
                    backaccountDMCMWrapper.Remarks = item.Remarks;
                    backaccountDMCMWrapper.Period = dmcm.Period.FullName;
                    floatingbackaccountdmcms.Add(backaccountDMCMWrapper);
                }
            }
            var ba = new BackAccount();
            ViewBag.floatingbackaccountdmcms = floatingbackaccountdmcms;

            ViewBag.backaccountlist = new SelectList(backaccounts, "BankAccountID", "FromPeriod");
            return View("Index", backaccountWrappers.OrderBy(m => m.BackaccountId).ToList());
        }
        public ActionResult Forwarded()
        {
            var periodid = HttpContext.Request.Cookies["PeriodId"].Value.ToString();
            var period = db.Period.Find(Convert.ToInt32(periodid));
            var backaccounts = db.BackAccount.Where(m => m.Student_Section.Section.PeriodID == period.PeriodID);
            return View(backaccounts);
        }

        public ActionResult FromPeriod()
        {
            var periodid = HttpContext.Request.Cookies["PeriodId"].Value.ToString();
            var period = db.Period.Find(Convert.ToInt32(periodid));
            var backaccounts = db.BackAccount.Where(m => m.SemID == period.PeriodID);
            return View(backaccounts);
        }

        public ActionResult TransactionLog()
        {
            var transactions = db.BackaccountTransactionLog.OrderByDescending(m => m.TransactionDate);
            return View(transactions);
        }
        public ActionResult Edit(int id)
        {
            var backaccount = db.BackAccount.Find(id);
            var backaccountwrapper = new BackaccountWrapper
            {
                Amount = backaccount.Balance,
                AssessmentId = backaccount.Student_SectionID,
                BackaccountId = backaccount.BankAccountID,
                PeriodId = backaccount.SemID,
                StudentId = backaccount.StudentID,
                StudentNo = backaccount.Student.StudentNo,
                StudentName = backaccount.Student.FullName,
                Period = backaccount.Period.FullName,
                ForwardedAmount = 0
            };

            var assessments = db.Student_Section.Where(m => m.StudentID == backaccount.StudentID);
            List<SelectListItem> assessmentids = new List<SelectListItem>();
            foreach (var i in assessments)
            {
                assessmentids.Add(new SelectListItem
                {
                    Value = i.Student_SectionID.ToString(),
                    Text = i.Section.Period.FullName,
                    Selected = backaccount.Student_SectionID.HasValue && backaccount.Student_SectionID == i.Student_SectionID
                });
            }
            ViewBag.assessment = assessmentids;
            return View(backaccountwrapper);
        }

        [HttpPost]
        public ActionResult Edit(BackaccountWrapper model)
        {
            var backaccount = db.BackAccount.Find(model.BackaccountId);
            if (backaccount == null) throw new Exception("Invalid backaccount.");
            StringBuilder remarks = new StringBuilder();
            remarks.Append("Updated backaccount of " + backaccount.Student.StudentNo + " with Period ID " + backaccount.SemID + ". ");
            remarks.Append("Amount changed from " + backaccount.Balance + " to " + model.Amount + ". ");
            remarks.Append("StudentSectionID from " + backaccount.Student_SectionID + " to " + model.AssessmentId + ".");
            backaccount.Balance = model.Amount;
            backaccount.Student_SectionID = model.AssessmentId == 0 || model.AssessmentId == null ? null : model.AssessmentId;
            db.SaveChanges();
            var assessment = db.Student_Section.Find(model.AssessmentId);
            if (assessment != null)
            {
                assessment.Credit = 0 - model.ForwardedAmount;
                db.SaveChanges();
                remarks.Append(" Forwarded amount is " + (0 - model.ForwardedAmount));
            }
            db.InsertBackaccountTransactionLog(User.Identity.Name, remarks.ToString(), backaccount.Student.StudentNo);
            return RedirectToAction("DisplayBackaccounts", new { backaccount.Student.StudentNo });
        }

        public ActionResult DeleteBackaccount(int id)
        {
            try
            {
                using (LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities())
                {
                    var backaccount = db.BackAccount.Find(id);
                    var studentno = backaccount.Student.StudentNo;
                    if (backaccount == null) throw new Exception("Invalid backaccount.");
                    if (backaccount.Student_SectionID.HasValue) throw new Exception("Backaccount is already forwarded!");
                    var backpayment = db.BackAccountPayment.Where(m => m.BackAccountID == id).FirstOrDefault();
                    if (backpayment != null) throw new Exception("Backaccount has an existing payment.");
                    var backaccountdmcm = db.BackaccountDMCM.Where(m => m.BackaccountID == id).FirstOrDefault();
                    if (backaccountdmcm != null) throw new Exception("Backaccount has an existing DMCM transaction.");
                    StringBuilder remarks = new StringBuilder();
                    remarks.Append("Deleted backaccount of " + studentno + " with PeriodID " + backaccount.SemID);
                    remarks.Append(" and amount of " + backaccount.Balance + ".");
                    db.BackAccount.Remove(backaccount);
                    db.SaveChanges();
                    db.InsertBackaccountTransactionLog(User.Identity.Name, remarks.ToString(), studentno);
                    return RedirectToAction("DisplayBackaccounts", new { studentno = studentno });
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        [HttpPost]
        public ActionResult LinkPayment(int PaymentId, int BackaccountId)
        {
            db.BackAccountPayment.Add(new BackAccountPayment
            {
                BackAccountID = BackaccountId,
                PaymentID = PaymentId
            });
            var backaccount = db.BackAccount.Find(BackaccountId);
            db.SaveChanges();
            StringBuilder remarks = new StringBuilder();
            remarks.Append("Linked payment " + PaymentId + " for student bankaccount with PeriodID " + backaccount.SemID + ".");
            db.InsertBackaccountTransactionLog(User.Identity.Name, remarks.ToString(), backaccount.Student.StudentNo);
            return RedirectToAction("DisplayBackaccounts", new { studentno = backaccount.Student.StudentNo });
        }

        [HttpPost]
        public ActionResult LinkDmcm(int DmcmId, int BackaccountId)
        {
            db.BackaccountDMCM.Add(new BackaccountDMCM
            {
                BackaccountID = BackaccountId,
                DMCMID = DmcmId
            });
            var backaccount = db.BackAccount.Find(BackaccountId);
            db.SaveChanges();
            StringBuilder remarks = new StringBuilder();
            remarks.Append("Linked DMCM " + DmcmId + " for student bankaccount with PeriodID " + backaccount.SemID + ".");
            db.InsertBackaccountTransactionLog(User.Identity.Name, remarks.ToString(), backaccount.Student.StudentNo);
            return RedirectToAction("DisplayBackaccounts", new { studentno = backaccount.Student.StudentNo });
        }
        public ActionResult UnlinkBackaccountPayment(int id)
        {
            var backaccountpayment = db.BackAccountPayment.Find(id);
            if (backaccountpayment == null) throw new Exception("Invalid backaccount payment id");
            var student = db.Student.Find(backaccountpayment.BackAccount.StudentID);
            var studentno = student.StudentNo;
            db.BackAccountPayment.Remove(backaccountpayment);
            db.SaveChanges();
            var backaccount = db.BackAccount.FirstOrDefault(m => m.StudentID == student.StudentID);
            StringBuilder remarks = new StringBuilder();
            remarks.Append("Unlinked payment " + backaccountpayment.PaymentID + " for student bankaccount with PeriodID " + backaccount.SemID + ".");
            db.InsertBackaccountTransactionLog(User.Identity.Name, remarks.ToString(), backaccount.Student.StudentNo);
            return RedirectToAction("DisplayBackaccounts", new { studentno = studentno });
        }

        public ActionResult UnlinkDmcm(int id)
        {
            var backaccountdmcm = db.BackaccountDMCM.Find(id);
            if (backaccountdmcm == null) throw new Exception("Invalid backaccount DMCM id");
            var student = db.Student.FirstOrDefault(m => m.StudentNo.Equals(backaccountdmcm.BackAccount.Student.StudentNo));
            db.BackaccountDMCM.Remove(backaccountdmcm);
            db.SaveChanges();
            var backaccount = db.BackAccount.FirstOrDefault(m => m.StudentID == student.StudentID);
            StringBuilder remarks = new StringBuilder();
            remarks.Append("Unlinked DMCM " + backaccountdmcm.DMCMID + " for student bankaccount with PeriodID " + backaccount.SemID + ".");
            db.InsertBackaccountTransactionLog(User.Identity.Name, remarks.ToString(), backaccount.Student.StudentNo);
            return RedirectToAction("DisplayBackaccounts", new { studentno = student.StudentNo });
        }
        private List<BackaccountWrapper> GetBackaccounts(IQueryable<BackAccount> backaccounts, List<int> paymentids, List<int> dmcmids)
        {
            List<BackaccountWrapper> backaccountWrappers = new List<BackaccountWrapper>();
            foreach (var backaccount in backaccounts)
            {
                var backaccountwrapper = new BackaccountWrapper
                {
                    Amount = backaccount.Balance,
                    AssessmentId = backaccount.Student_SectionID,
                    BackaccountId = backaccount.BankAccountID,
                    PeriodId = backaccount.SemID,
                    StudentId = backaccount.StudentID
                };
                var student = db.Student.Find(backaccount.StudentID);
                var fromenrollment = db.Student_Section.FirstOrDefault(m => m.StudentID == backaccount.StudentID && m.Section.PeriodID == backaccount.SemID);
                backaccountwrapper.FromAssessmentId = fromenrollment?.Student_SectionID;
                backaccountwrapper.StudentNo = student.StudentNo;
                backaccountwrapper.StudentName = student.FullName;
                if (backaccount.Student_SectionID.HasValue)
                {
                    var assessment = db.Student_Section.Find(backaccount.Student_SectionID);
                    backaccountwrapper.AssessmentId = assessment.Student_SectionID;
                    backaccountwrapper.AssessmentPeriodId = assessment.Section.PeriodID;
                    backaccountwrapper.AssessmentPeriod = assessment.Section.Period.FullName;
                }
                var period = db.Period.Find(backaccount.SemID);
                backaccountwrapper.Period = period.FullName;

                GetBackaccountPaymentDMCM(backaccount, backaccountwrapper, paymentids, dmcmids);
                backaccountWrappers.Add(backaccountwrapper);
            }

            return backaccountWrappers;
        }

        private void GetBackaccountPaymentDMCM(BackAccount backaccount, BackaccountWrapper backaccountwrapper, List<int> paymentids, List<int> dmcmids)
        {
            backaccountwrapper.BackaccountPaymentWrappers = new List<BackaccountPaymentWrapper>();
            var backaccountpayments = db.BackAccountPayment.Where(m => m.BackAccountID == backaccount.BankAccountID);
            foreach (var item in backaccountpayments)
            {
                BackaccountPaymentWrapper backaccountPaymentWrapper = new BackaccountPaymentWrapper();
                var paymentdetail = db.PaymentDetails.Where(m => m.PaymentID == item.PaymentID && m.PaycodeID <= 11).FirstOrDefault();
                backaccountPaymentWrapper.Amount = paymentdetail == null ? 0 : paymentdetail.Amount;
                backaccountPaymentWrapper.BackaccountId = backaccount.BankAccountID;
                backaccountPaymentWrapper.BackaccountPaymentId = item.BackAccountPaymentID;
                backaccountPaymentWrapper.ORNo = paymentdetail == null ? "CANCELLED" : item.Payment.ORNo;
                backaccountPaymentWrapper.PaymentDate = item.Payment.DateReceived;
                backaccountPaymentWrapper.PaymentId = item.PaymentID;
                backaccountPaymentWrapper.Remarks = item.Payment.StudentID.HasValue ? item.Payment.Period.FullName : item.Payment.CheckNo;
                backaccountPaymentWrapper.Period = item.Payment.Period.FullName;
                backaccountwrapper.BackaccountPaymentWrappers.Add(backaccountPaymentWrapper);
                paymentids.Add(item.PaymentID);
            }

            backaccountwrapper.BackaccountDMCMWrappers = new List<BackaccountDMCMWrapper>();
            var backaccountdmcms = db.BackaccountDMCM.Where(m => m.BackaccountID == backaccount.BankAccountID);
            foreach (var item in backaccountdmcms)
            {
                BackaccountDMCMWrapper backaccountDMCMWrapper = new BackaccountDMCMWrapper();
                var dmcm = db.DMCM.Find(item.DMCMID);
                backaccountDMCMWrapper.DC = dmcm.DC;
                backaccountDMCMWrapper.Amount = dmcm.Amount;
                backaccountDMCMWrapper.BackaccountDMCMId = item.BackAccountDMCMID;
                backaccountDMCMWrapper.BackaccountId = backaccount.BankAccountID;
                backaccountDMCMWrapper.DMCMId = item.DMCMID;
                backaccountDMCMWrapper.DocNo = dmcm.DocNum;
                backaccountDMCMWrapper.TransactionDate = dmcm.TransactionDate;
                backaccountDMCMWrapper.Remarks = dmcm.Remarks;
                backaccountDMCMWrapper.Period = item.DMCM.Period.FullName;
                backaccountwrapper.BackaccountDMCMWrappers.Add(backaccountDMCMWrapper);
                dmcmids.Add(item.DMCMID);
            }
        }
    }
}