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
    }
}