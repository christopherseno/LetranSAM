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
        public ActionResult DisplayBackaccounts(string studentno)
        {
            var id = db.Student.FirstOrDefault(m => m.StudentNo.Equals(studentno)).StudentID;
            var enrollments = db.Student_Section.Where(m => m.StudentID==id && m.ValidationDate != null).OrderBy(m => m.Student_SectionID);
            ViewBag.enrollments = enrollments;
            List<int> periodids = enrollments.Select(m => m.Section.PeriodID).ToList();
            var backaccounts = db.BackAccount.Where(m => m.StudentID == id);
            List<int> paymentids = new List<int>();
            List<int> dmcmids = new List<int>();
            List<BackaccountWrapper> backaccountWrappers = GetBackaccounts(backaccounts, paymentids, dmcmids);
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
                floatingbackaccountpayments.Add(backaccountPaymentWrapper);
            }
            ViewBag.floatingbackaccountpayments = floatingbackaccountpayments;
            var floatingdmcms = db.DMCM.Where(m => m.StudentID == id && !periodids.Contains(m.PeriodID.Value) && !dmcmids.Contains(m.DMCMID) && m.ChargeToStudentAr == true);
            List<BackaccountDMCMWrapper> floatingbackaccountdmcms = new List<BackaccountDMCMWrapper>();
            foreach(var item in floatingdmcms)
            {
                BackaccountDMCMWrapper backaccountDMCMWrapper = new BackaccountDMCMWrapper();
                var dmcm = db.DMCM.Find(item.DMCMID);
                backaccountDMCMWrapper.DC = dmcm.DC;
                backaccountDMCMWrapper.Amount = dmcm.Amount;                                
                backaccountDMCMWrapper.DMCMId = item.DMCMID;
                backaccountDMCMWrapper.DocNo = dmcm.DocNum;
                backaccountDMCMWrapper.TransactionDate = dmcm.TransactionDate;
                backaccountDMCMWrapper.Remarks = item.Remarks;
                floatingbackaccountdmcms.Add(backaccountDMCMWrapper);
            }
            var ba = new BackAccount();
            ViewBag.floatingbackaccountdmcms = floatingbackaccountdmcms;
            
            ViewBag.backaccountlist = new SelectList(backaccounts, "BankAccountID", "FromPeriod");
            return View("Index",backaccountWrappers.OrderBy(m => m.BackaccountId).ToList());
        }
        //public ActionResult Forwarded()
        //{
        //    var periodwraper = (PeriodWrapper)Session["PeriodId"];
        //    var period = db.Period.Find(periodwraper.PeriodId);
        //    var backaccounts = db.BackAccount.Where(m => m.Student_Section.Section.PeriodID== period.PeriodID);
        //    List<BackaccountWrapper> backaccountWrappers = GetBackaccounts(backaccounts);
        //    return View(backaccountWrappers);
        //}

        /*
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
        */
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
                    db.BackAccount.Remove(backaccount);
                    db.SaveChanges();
                    //var backaccounts = db.BackAccount.Where(m => m.StudentID == studentid);
                    //List<BackaccountWrapper> backaccountWrappers = GetBackaccounts(backaccounts);
                    //return View("Index", backaccountWrappers);
                    return RedirectToAction("DisplayBackaccounts", new { studentno = studentno});
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
            return RedirectToAction("DisplayBackaccounts", new { studentno = backaccount.Student.StudentNo });
        }
        public ActionResult UnlinkBackaccountPayment(int id)
        {
            var backaccountpayment = db.BackAccountPayment.Find(id);
            if (backaccountpayment == null) throw new Exception("Invalid backaccount payment id");
            var studentno = backaccountpayment.BackAccount.Student.StudentNo;
            db.BackAccountPayment.Remove(backaccountpayment);
            db.SaveChanges();
            var backaccounts = db.BackAccount.Where(m => m.StudentID == backaccountpayment.BackAccount.StudentID);
           
            //List<BackaccountWrapper> backaccountWrappers = GetBackaccounts(backaccounts, paymentids);
            //return View("Index", backaccountWrappers);
            return RedirectToAction("DisplayBackaccounts", new { studentno = studentno });
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
                backaccountPaymentWrapper.Amount = paymentdetail.Amount;
                backaccountPaymentWrapper.BackaccountId = backaccount.BankAccountID;
                backaccountPaymentWrapper.BackaccountPaymentId = item.BackAccountPaymentID;
                backaccountPaymentWrapper.ORNo = item.Payment.ORNo;
                backaccountPaymentWrapper.PaymentDate = item.Payment.DateReceived;
                backaccountPaymentWrapper.PaymentId = item.PaymentID;
                backaccountPaymentWrapper.Remarks = item.Payment.StudentID.HasValue ? item.Payment.Period.FullName : item.Payment.CheckNo;
                backaccountwrapper.BackaccountPaymentWrappers.Add(backaccountPaymentWrapper);
                paymentids.Add(item.PaymentID.Value);
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
                backaccountwrapper.BackaccountDMCMWrappers.Add(backaccountDMCMWrapper);
                dmcmids.Add(item.DMCMID.Value);
            }            
        }
    }
}