using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;

namespace ARManila.Controllers
{
    public class PaymentDefaultPeriodsController : BaseController
    {
        private LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();

        // GET: PaymentDefaultPeriods
        public ActionResult Index()
        {
            var paymentDefaultPeriod = db.PaymentDefaultPeriod.Include(p => p.EducationalLevel).Include(p => p.Period).Include(p => p.Period1);
            return View(paymentDefaultPeriod.ToList());
        }
        // GET: PaymentDefaultPeriods/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            PaymentDefaultPeriod paymentDefaultPeriod = db.PaymentDefaultPeriod.Find(id);
            if (paymentDefaultPeriod == null)
            {
                return HttpNotFound();
            }
            ViewBag.EducationalLevelId = new SelectList(db.EducationalLevel, "EducLevelID", "EducLevelName", paymentDefaultPeriod.EducationalLevelId);
            ViewBag.PeriodId = new SelectList(db.Period.Where(m=>m.EducLevelID== id), "PeriodID", "FullName", paymentDefaultPeriod.PeriodId);
            ViewBag.ReservationPeriodId = new SelectList(db.Period.Where(m=>m.EducLevelID==id), "PeriodID", "FullName", paymentDefaultPeriod.ReservationPeriodId);
            return View(paymentDefaultPeriod);
        }

        // POST: PaymentDefaultPeriods/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit([Bind(Include = "EducationalLevelId,PeriodId,ReservationPeriodId")] PaymentDefaultPeriod paymentDefaultPeriod)
        {
            if (ModelState.IsValid)
            {
                db.Entry(paymentDefaultPeriod).State = EntityState.Modified;
                db.SaveChanges();
                return RedirectToAction("Index");
            }
            ViewBag.EducationalLevelId = new SelectList(db.EducationalLevel, "EducLevelID", "EducLevelName", paymentDefaultPeriod.EducationalLevelId);
            ViewBag.PeriodId = new SelectList(db.Period, "PeriodID", "FullName", paymentDefaultPeriod.PeriodId);
            ViewBag.ReservationPeriodId = new SelectList(db.Period, "PeriodID", "FullName", paymentDefaultPeriod.ReservationPeriodId);
            return View(paymentDefaultPeriod);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
