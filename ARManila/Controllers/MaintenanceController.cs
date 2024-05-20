using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
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