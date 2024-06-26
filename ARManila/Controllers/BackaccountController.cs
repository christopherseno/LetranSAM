using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;

namespace ARManila.Controllers
{
    public class BackaccountController : BaseController
    {
        private readonly LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public ActionResult Index(string id)
        {
            var backaccounts = db.BackAccount.Where(m => m.Student.StudentNo == id);           
            List<BackaccountWrapper> backaccountWrappers = GetBackaccounts(backaccounts);
            return View(backaccountWrappers);
        }

        public ActionResult Forwarded()
        {
            var periodwraper = (PeriodWrapper)Session["PeriodId"];
            var period = db.Period.Find(periodwraper.PeriodId);
            var backaccounts = db.BackAccount.Where(m => m.Student_Section.Section.PeriodID== period.PeriodID);
            List<BackaccountWrapper> backaccountWrappers = GetBackaccounts(backaccounts);
            return View(backaccountWrappers);
        }

        public ActionResult Edit(int id)
        {
            var backaccounts = db.BackAccount.Where(m=>m.BankAccountID ==id);
            List<BackaccountWrapper> backaccountWrappers = GetBackaccounts(backaccounts);
            var assessments = db.Student_Section.Where(m => m.StudentID == backaccounts.FirstOrDefault().StudentID);
            List<SelectListItem> assessmentids = new List<SelectListItem>();
            foreach(var i in assessments)
            {
                assessmentids.Add(new SelectListItem
                {
                    Value = i.Student_SectionID.ToString(),
                    Text = i.Section.Period.FullName,
                    Selected = backaccounts.FirstOrDefault().Student_SectionID.HasValue && backaccounts.FirstOrDefault().Student_SectionID == i.Student_SectionID
                });
            }
            ViewBag.assessment = assessmentids;
            return View(backaccountWrappers.FirstOrDefault());
        }

        [HttpPost]
        public ActionResult Edit(BackaccountWrapper model)
        {
            var backaccount = db.BackAccount.Where(m => m.BankAccountID == model.BackaccountId).FirstOrDefault();
            if (backaccount == null) throw new Exception("Invalid backaccount.");
            backaccount.Balance = model.Amount;
            backaccount.Student_SectionID = model.AssessmentId == 0 || model.AssessmentId == null ? null : model.AssessmentId;
            db.SaveChanges();
            var backaccounts = db.BackAccount.Where(m => m.StudentID == model.StudentId);
            List<BackaccountWrapper> backaccountWrappers = GetBackaccounts(backaccounts);
            return View("Index",backaccountWrappers);
        }
        public ActionResult DeleteBackaccount(int id)
        {
            try
            {
                using (LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities())
                {
                    var backaccount = db.BackAccount.Find(id);
                    var studentid = backaccount.StudentID;
                    if (backaccount == null) throw new Exception("Invalid backaccount.");
                    if (backaccount.Student_SectionID.HasValue) throw new Exception("Backaccount is already forwarded!");
                    var backpayment = db.BackAccountPayment.Where(m => m.BackAccountID == id).FirstOrDefault();
                    if (backpayment != null) throw new Exception("Backaccount has an existing payment.");
                    var backaccountdmcm = db.BackaccountDMCM.Where(m => m.BackaccountID == id).FirstOrDefault();
                    if (backaccountdmcm != null) throw new Exception("Backaccount has an existing DMCM transaction.");
                    db.BackAccount.Remove(backaccount);
                    db.SaveChanges();
                    var backaccounts = db.BackAccount.Where(m => m.StudentID == studentid);
                    List<BackaccountWrapper> backaccountWrappers = GetBackaccounts(backaccounts);
                    return View("Index", backaccountWrappers);
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public ActionResult UnlinkBackaccountPayment(int id)
        {
            var backaccountpayment = db.BackAccountPayment.Find(id);
            if (backaccountpayment == null) throw new Exception("Invalid backaccount payment id");
            var studentid = backaccountpayment.BackAccount.StudentID;
            db.BackAccountPayment.Remove(backaccountpayment);
            db.SaveChanges();
            var backaccounts = db.BackAccount.Where(m => m.StudentID == studentid);
            List<BackaccountWrapper> backaccountWrappers = GetBackaccounts(backaccounts);
            return View("Index", backaccountWrappers);           
        }

        private List<BackaccountWrapper> GetBackaccounts(IQueryable<BackAccount> backaccounts)
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

                GetBackaccountPaymentDMCM(backaccount, backaccountwrapper);

                backaccountWrappers.Add(backaccountwrapper);
            }

            return backaccountWrappers;
        }

        private void GetBackaccountPaymentDMCM(BackAccount backaccount, BackaccountWrapper backaccountwrapper)
        {
            backaccountwrapper.BackaccountPaymentWrappers = new List<BackaccountPaymentWrapper>();
            var backaccountpayments = db.BackAccountPayment.Where(m => m.BackAccountID == backaccount.BankAccountID);
            foreach (var item in backaccountpayments)
            {
                BackaccountPaymentWrapper backaccountPaymentWrapper = new BackaccountPaymentWrapper();
                var paymentdetail = db.PaymentDetails.Where(m => m.PaymentID == item.PaymentID && m.PaycodeID <= 11).FirstOrDefault();
                backaccountPaymentWrapper.Amount = paymentdetail.Amount;
                backaccountPaymentWrapper.BackaccountId = backaccount.BankAccountID;
                backaccountPaymentWrapper.BackaccountPaymentId = item.BackAccountPaymentID;
                backaccountPaymentWrapper.ORNo = item.Payment.ORNo;
                backaccountPaymentWrapper.PaymentDate = item.Payment.DateReceived;
                backaccountPaymentWrapper.PaymentId = item.PaymentID;
                backaccountPaymentWrapper.Remarks = item.Payment.StudentID.HasValue ? item.Payment.Period.FullName : item.Payment.CheckNo;
                backaccountwrapper.BackaccountPaymentWrappers.Add(backaccountPaymentWrapper);
            }
            /* for future use
            backaccountwrapper.BackaccountDMCMWrappers = new List<BackaccountDMCMWrapper>();
            var backaccountdmcms = db.BackaccountDMCM.Where(m => m.BackaccountID == backaccount.BankAccountID);
            foreach (var item in backaccountdmcms)
            {
                BackaccountDMCMWrapper backaccountDMCMWrapper = new BackaccountDMCMWrapper();
                var dmcm = db.DMCM.Find(item.DMCMID);
                backaccountDMCMWrapper.Amount = dmcm.DC == "D" ? dmcm.Amount : 0 - dmcm.Amount;
                backaccountDMCMWrapper.BackaccountDMCMId = item.BackaccountDMCMID;
                backaccountDMCMWrapper.BackaccountId = backaccount.BankAccountID;
                backaccountDMCMWrapper.DMCMId = item.DMCMID;
                backaccountDMCMWrapper.DocNo = dmcm.DocNum;
                backaccountDMCMWrapper.TransactionDate = dmcm.TransactionDate;
                backaccountwrapper.BackaccountDMCMWrappers.Add(backaccountDMCMWrapper);
            }
            */
        }
    }
}