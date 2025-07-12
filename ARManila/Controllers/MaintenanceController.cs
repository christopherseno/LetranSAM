using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
using DatabaseEncryption;
namespace ARManila.Controllers
{
    public class MaintenanceController : BaseController
    {
        private LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        public ActionResult Index()
        {
            var paycodeforbankposting = db.Paycode.Where(m => m.TuitionRelated == false);
            return View(paycodeforbankposting.ToList());
        }
        
        public ActionResult EncryptedStudentInfo()
        {
            var periodid = HttpContext.Request.Cookies["PeriodId"].Value.ToString();
            var period = int.Parse(periodid);
            var enrolledstudents = db.Student_Section.Where(m => m.Section.PeriodID == period && (m.Student.LastName256 == null || m.Student.LastName256.Length < 1) );            
            foreach(var enrolledstudent in enrolledstudents)
            {
                var student = db.Student.Find(enrolledstudent.StudentID);
                student.LastName256 = Encryption.EncryptStringToBytes_Aes(student.LastName, "13061025", "-951Han5", "172.20.0.7");
                student.FirstName256 = Encryption.EncryptStringToBytes_Aes(student.FirstName, "13061025", "-951Han5", "172.20.0.7");
                student.MiddleName256 = Encryption.EncryptStringToBytes_Aes(student.MiddleName, "13061025", "-951Han5", "172.20.0.7");                
            }
            db.SaveChanges();
            return View(enrolledstudents);
        }
        public JsonResult GetPaycodes()
        {
            return Json(db.Paycode.Where(m => m.TuitionRelated == false).Select(m=>new { PaycodeId = m.PaycodeID, Description = m.Description, ForPosting =m.ForBankPosting}) .ToList(), JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public ActionResult Index(List<Paycode> model)
        {
            foreach(var item in model)
            {
                var paycode = db.Paycode.Find(item.PaycodeID);
                paycode.ForBankPosting = item.ForBankPosting;
                db.SaveChanges();
            }
            
            var paycodeforbankposting = db.Paycode.Where(m => m.TuitionRelated == false);
            return View(paycodeforbankposting.ToList());
        }

        public ActionResult DefaultPeriodForPosting()
        {
            var periods = db.PaymentDefaultPeriod;
            return View(periods);
        }

        [HttpPost]
        public ActionResult DefaultPeriodForPosting(List<PaymentDefaultPeriod> model)
        {
            foreach(var i in model)
            {
                
            }
            var periods = db.PaymentDefaultPeriod;
            return View(periods);
        }
    }
}