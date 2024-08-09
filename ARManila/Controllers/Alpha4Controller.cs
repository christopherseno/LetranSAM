using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
using ARManila.Reports;
using CrystalDecisions.CrystalReports.Engine;

namespace ARManila.Controllers
{
    public class Alpha4Controller : BaseController
    {
        LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        public ActionResult Index()
        {
            var periodid = HttpContext.Request.Cookies["PeriodId"].Value.ToString();
            var period = db.Period.Find(Convert.ToInt32(periodid));
            var alpha4 = db.Alpha4.Where(m => m.EducLevelId == period.EducLevelID);
            return View(alpha4);
        }
        public ActionResult Detail(int? id)
        {
            if (id.HasValue)
            {
                var alpha4 = db.Alpha4.Find(id);
                return View(alpha4);
            }
            else
            {
                return View();
            }
        }
        public ActionResult AddPayment(int id)
        {
            var alpha4 = db.Alpha4.Find(id);
            var alpha4payment = new Alpha4Payment();
            if (alpha4 != null)
                alpha4payment.Alpha4 = alpha4;
            return View(alpha4payment);
        }
        [HttpPost]
        public ActionResult AddPayment(Alpha4Payment payment)
        {
            var alpha4 = db.Alpha4.Find(payment.Alpha4Id);
            if (alpha4 == null) throw new Exception("Invalid ID.");
            var actualpayment = db.Payment.FirstOrDefault(m => m.ORNo.Equals(payment.OrNo));
            db.Alpha4Payment.Add(new Alpha4Payment
            {
                Alpha4Id = payment.Alpha4Id,
                Amount = payment.Amount,
                IsMigrated = false,
                PaymentDate = actualpayment != null ? actualpayment.DateReceived : payment.PaymentDate,
                PaymentId = actualpayment != null ? actualpayment.PaymentID : (int?)null,
                OrNo = actualpayment != null ? actualpayment.ORNo : null,
                Remarks = payment.Remarks
            });
            db.SaveChanges();
            return RedirectToAction("Detail", new { id = payment.Alpha4Id });
        }
        public ActionResult Delete(int id)
        {
            var alphapayment = db.Alpha4Payment.Find(id);
            int alphaid = alphapayment.Alpha4Id;
            if (alphapayment.IsMigrated) throw new Exception("Cannot delete migrated data.");
            db.Alpha4Payment.Remove(alphapayment);
            db.SaveChanges();
            return RedirectToAction("Detail", new { id = alphaid });
        }
        public ActionResult UnpostedAlpha4Payments()
        {
            var paymentids = db.Alpha4Payment.Where(m => m.PaymentId.HasValue).Select(m => m.PaymentId.Value).ToList();
            var paymentdetails = db.PaymentDetails.Where(m => m.PaycodeID == 221 && !paymentids.Contains(m.PaymentID));
            return View(paymentdetails);
        }
        public ActionResult Print(int id)
        {
            var reportdata = new List<Alpha4Query>();
            var alpha4 = db.Alpha4.Find(id);
            reportdata.Add(new Alpha4Query
            {
                Id = alpha4.Id,
                Level = alpha4.EducationalLevel.EducLevelName,
                FullName = alpha4.FullName,
                StudentNumber = alpha4.StudentId.HasValue ? alpha4.Student.StudentNo : alpha4.StudentNo,
                Balance = alpha4.Balance,
                Remarks = alpha4.Remarks,
                Particular = "Alpha4 Backaccount",
                Debit = alpha4.Amount,
                Credit = 0,
                DocNo = "",
                Date = alpha4.BADate,
            });
            foreach (var payment in alpha4.Alpha4Payment)
            {
                reportdata.Add(new Alpha4Query
                {
                    Id = alpha4.Id,
                    Level = alpha4.EducationalLevel.EducLevelName,
                    FullName = alpha4.FullName,
                    StudentNumber = alpha4.StudentId.HasValue ? alpha4.Student.StudentNo : alpha4.StudentNo,
                    Balance = alpha4.Balance,
                    Remarks = alpha4.Remarks,
                    Particular = payment.PaymentId.HasValue ? payment.Payment.PaymentDetails.FirstOrDefault().Paycode.Description : "Migrated Payment",
                    Debit = 0,
                    Credit = payment.Amount,
                    DocNo = payment.PaymentId.HasValue ? payment.Payment.ORNo : payment.OrNo,
                    Date = payment.PaymentId.HasValue ? payment.Payment.DateReceived.ToShortDateString() : (payment.PaymentDate.HasValue ? payment.PaymentDate.Value.ToShortDateString() : ""),
                });
            }
            using (ReportDocument document = new ARQueryAlpha4())
            {
                document.SetDataSource(reportdata);
                //if (true)
                    return ExportType(1, "AlphaQuery_" + DateTime.Today.ToShortDateString(), document);
                //else
                //    return ExportType(2, "AlphaQuery_" + DateTime.Today.ToShortDateString(), document);
            }
        }

        public ActionResult PrintList(int id)
        {
            var reportdata = new List<Alpha4Dto>();
            var periodid = HttpContext.Request.Cookies["PeriodId"].Value.ToString();
            var period = db.Period.Find(Convert.ToInt32(periodid));
            var alpha4 = db.Alpha4.Where(m => m.EducLevelId == period.EducLevelID);
            foreach (var item in alpha4)
            {
                if (item.Alpha4Payment.Count() > 0)
                {
                    foreach (var payment in item.Alpha4Payment)
                    {
                        reportdata.Add(new Alpha4Dto
                        {
                            Id = item.Id,
                            Amount = item.Amount,
                            BADate = item.BADate,
                            Level = item.EducationalLevel.EducLevelName,
                            FullName = item.FullName,
                            Remarks = item.Remarks,
                            OrNo = payment.PaymentId.HasValue ? payment.Payment.ORNo : payment.OrNo,
                            PaymentDate = payment.PaymentId.HasValue ? payment.Payment.DateReceived.ToShortDateString() : (payment.PaymentDate.HasValue ? payment.PaymentDate.Value.ToShortDateString() : ""),
                            PaymentAmount = payment.Amount,
                            StudentNumber = item.StudentId.HasValue ? item.Student.StudentNo : item.StudentNo,
                            Balance = item.Balance
                        });
                    }
                }
                else
                {
                    reportdata.Add(new Alpha4Dto
                    {
                        Id = item.Id,
                        Amount = item.Amount,
                        BADate = item.BADate,
                        Level = item.EducationalLevel.EducLevelName,
                        FullName = item.FullName,
                        Remarks = item.Remarks,
                        OrNo = "",
                        PaymentDate = "",
                        PaymentAmount = 0,
                        StudentNumber = item.StudentId.HasValue ? item.Student.StudentNo : item.StudentNo,
                        Balance = item.Balance
                    });
                }
            }
            using (ReportDocument document = new AlphaListReport())
            {
                document.SetDataSource(reportdata);
                if (id == 1)
                    return ExportType(1, "AlphaList_" + DateTime.Today.ToShortDateString(), document);
                else
                    return ExportType(2, "AlphaList_" + DateTime.Today.ToShortDateString(), document);

            }
        }
    }

}