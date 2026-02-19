using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
using ARManila.Models.OtherDTO;
namespace ARManila.Controllers
{
    public class PermitsController : BaseController
    {
        LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        /*
        public ActionResult Index()
        {
            var periodid =Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            ViewBag.Section = new SelectList(db.Section.Where(m=>m.PeriodID==periodid && m.CurriculumID != null),  "SectionID", "SectionName");            
            ViewBag.PermitTye = new SelectList(db.PermitType,"PermitTypeID" , "PermitName");            
            ViewBag.Paymode = new SelectList(db.Section.Where(m => m.PeriodID == periodid && m.CurriculumID != null), "PaymodeID", "Description");
            return View();
        }
        [HttpPost]
        public ActionResult Index(int? sectionid, int? paymodeid, int? permittypeid, DateTime? duedate, string PrintBulkPermit, string PrintBulkBilling, string LoadStudents )
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            ViewBag.Section = new SelectList(db.Section.Where(m => m.PeriodID == periodid && m.CurriculumID != null), "SectionID", "SectionName");
            var a = new Paymode();
            ViewBag.PermitTye = new SelectList(db.PermitType, "PermitTypeID", "PermitName");
            ViewBag.Paymode = new SelectList(db.Section.Where(m => m.PeriodID == periodid && m.CurriculumID != null), "PaymodeID", "Description");
            var balances = db.GetTotalBalanceByDueDate(sectionid, periodid, duedate);
            List<StudentWithBalancesDTO> studentswithbalances = new List<StudentWithBalancesDTO>();
            foreach (var i in balances)
            {
                if (i.AmountDue > 1)
                {
                    var studentwithbalance = new StudentWithBalancesDTO();
                    studentwithbalance.Amount = i.AmountDue.Value.ToString("#,##0.00");
                    studentwithbalance.StudentName = i.StudentName;
                    studentwithbalance.StudentContactNo = i.StudentCP;
                    studentwithbalance.GuardianContactNo = i.GuardianCP;
                    studentwithbalance.StudentID = i.StudentID;
                    studentwithbalance.StudentNo = i.StudentNo;
                    studentwithbalance.Email = i.Email;
                    studentswithbalances.Add(studentwithbalance);
                }
            }
            return View(studentswithbalances);
        }
        */
       
        public ActionResult Index()
        {
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");          
            ViewBag.EducLevel = period.EducLevelID;

            ViewBag.Sections = db.Section.Where(m => m.PeriodID == periodid).ToList();
            ViewBag.PermitTypes = db.PermitType.OrderBy(m => m.PermitName).ToList();
            ViewBag.PayModes = db.Paymode.ToList();

            return View();
        }

        [HttpPost]
        public ActionResult PrintPermit(int sectionId, int permitTypeId, int educLevel)
        {
            // Logic to generate permit
            return RedirectToAction("PermitPreview", new { sectionId, permitTypeId, educLevel });
        }

        [HttpPost]
        public JsonResult LoadBalanceList(int sectionId, DateTime dueDate)
        {
            var periodId = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodId);
            if (period == null) throw new Exception("Invalid period id.");
            var balances = db.GetTotalBalanceByDueDate(sectionId, periodId, dueDate)
                             .Where(i => i.AmountDue > 1)
                             .Select(i => new {
                                 i.StudentID,
                                 i.StudentNo,
                                 i.StudentName,
                                 i.StudentCP,
                                 i.GuardianCP,
                                 i.Email,
                                 Amount = i.AmountDue.Value.ToString("#,##0.00")
                             }).ToList();

            return Json(balances);
        }
        [HttpPost]
        public JsonResult SendSMS(List<string> studentIds, string message)
        {
            foreach (var id in studentIds)
            {
                var student = db.Student.Find(id);
                if (student != null && !string.IsNullOrEmpty(student.MobileNo))
                {
                    // Send SMS via SendGrid or other service
                    //SendSMSHelper.Send(student.StudentCP, message);
                }
            }
            return Json(new { success = true });
        }

        [HttpPost]
        public JsonResult SendEmail(List<string> studentIds, string subject, string body)
        {
            foreach (var id in studentIds)
            {
                var student = db.Student.Find(id);
                if (student != null && !string.IsNullOrEmpty(student.EmailAddress))
                {
                    //SendGridHelper.Send(student.Email, subject, body);
                }
            }
            return Json(new { success = true });
        }
        [HttpPost]
        public ActionResult PrintBillingBulk(List<string> studentIds, int sectionId, DateTime dueDate)
        {
            // Logic to generate billing documents
            return View("BillingPreview", studentIds); // or redirect to PDF generation
        }

        [HttpPost]
        public JsonResult SetUpPermit(List<string> studentIds, int permitTypeId, DateTime dateIssued)
        {
            foreach (var id in studentIds)
            {
                db.Permit.Add(new Permit
                {
                    StudentID = Convert.ToInt32(id),
                    PermitTypeID = permitTypeId,
                    DateIssued = dateIssued
                });
            }
            db.SaveChanges();
            return Json(new { success = true });
        }

    }
}