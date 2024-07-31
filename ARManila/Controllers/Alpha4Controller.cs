using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
namespace ARManila.Controllers
{
    public class Alpha4Controller : Controller
    {
        LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        public ActionResult Index()
        {
            var alpha4 = db.Alpha4;
            return View(alpha4);
        }
        public ActionResult UnpostedAlpha4Payments()
        {
            var paymentids = db.Alpha4Payment.Where(m=>m.PaymentId.HasValue).Select(m => m.PaymentId.Value).ToList();
            var paymentdetails = db.PaymentDetails.Where(m => m.PaycodeID == 221 && !paymentids.Contains(m.PaymentID));
            return View(paymentdetails);
        }
    }
}