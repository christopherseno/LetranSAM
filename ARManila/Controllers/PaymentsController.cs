using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
using OfficeOpenXml;

namespace ARManila.Controllers
{
    public class PaymentsController : BaseController
    {
        private readonly LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
                
        public async Task<ActionResult> Index()
        {
            var payment = db.Payment.Include(p => p.Employee1).Include(p=>p.Period).Include(p=>p.Student).Where(p => p.DateReceived >= DateTime.Today &&  p.StudentID != null && p.ORNo.StartsWith("*")).OrderByDescending(p => p.DateReceived);
            ViewBag.startdate = DateTime.Today.ToString("yyyy-MM-dd");
            ViewBag.enddate = DateTime.Today.ToString("yyyy-MM-dd");
            return View(await payment.ToListAsync());
        }

        [HttpPost]
        public ActionResult Index(DateTime startdate, DateTime enddate)
        {
            var enddateplus = enddate.AddDays(1);
            var payments = db.Payment.Include(p => p.Employee1).Include(p => p.Period).Include(p => p.Student).Where(p => p.ORNo.StartsWith("*") && p.DateReceived >= startdate && p.DateReceived < enddateplus && p.StudentID.HasValue).OrderByDescending(m => m.DateReceived);
            ViewBag.startdate = startdate.ToString("yyyy-MM-dd");
            ViewBag.enddate = enddate.ToString("yyyy-MM-dd");
            return View(payments);
        }

        public void DownloadExcel(DateTime startdate, DateTime enddate)
        {
            var enddateplus = enddate.AddDays(1);
            var payments = db.Payment.Include(p => p.Employee).Include(p => p.Period).Include(p => p.Student).Where(p => p.ORNo.StartsWith("*") && p .DateReceived >= startdate && p .DateReceived < enddateplus && p.StudentID.HasValue).OrderByDescending(m => m.DateReceived);
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            ExcelPackage Ep = new ExcelPackage();

            ExcelWorksheet Sheet = Ep.Workbook.Worksheets.Add("PostedPaymentReport");
            Sheet.Cells["A1"].Value = "OR No";
            Sheet.Cells["B1"].Value = "Student Number";
            Sheet.Cells["C1"].Value = "Student Name";
            Sheet.Cells["D1"].Value = "Posting Date";
            Sheet.Cells["E1"].Value = "Amount";
            Sheet.Cells["F1"].Value = "Bank";
            Sheet.Cells["G1"].Value = "Period";
            Sheet.Cells["H1"].Value = "Level";
            Sheet.Cells["I1"].Value = "Payment For";
            Sheet.Cells["J1"].Value = "Remarks";
            Sheet.Cells["K1"].Value = "Cashier";
            int row = 2;
            foreach (var item in payments)
            {
                Sheet.Cells[string.Format("A{0}", row)].Value = item.ORNo;
                Sheet.Cells[string.Format("B{0}", row)].Value = item.Student.StudentNo;
                Sheet.Cells[string.Format("C{0}", row)].Value = item.Student.FullName;
                Sheet.Cells[string.Format("D{0}", row)].Value = item.DateReceived.ToShortDateString(); ;
                Sheet.Cells[string.Format("E{0}", row)].Value = item.CheckAmount.HasValue ? item.CheckAmount.Value.ToString("#,##0.00") : "";
                Sheet.Cells[string.Format("F{0}", row)].Value = item.Bank;
                if (item.PaymentDetails.FirstOrDefault().PaycodeID == 11)
                {
                    var backaccountpayment = db.BackAccountPayment.Where(m => m.PaymentID == item.PaymentID).FirstOrDefault();
                    if (backaccountpayment != null)
                    {
                        Sheet.Cells[string.Format("H{0}", row)].Value = backaccountpayment.BackAccount.Period.FullName;
                    }
                }
                else
                {
                    Sheet.Cells[string.Format("H{0}", row)].Value = item.Period.FullName;
                }
                Sheet.Cells[string.Format("I{0}", row)].Value = item.Period.EducationalLevel1.EducLevelName;
                Sheet.Cells[string.Format("J{0}", row)].Value = (item.PaymentDetails.FirstOrDefault() == null ? "" : item.PaymentDetails.FirstOrDefault().Paycode.Description);
                Sheet.Cells[string.Format("K{0}", row)].Value = item.Remarks;
                Sheet.Cells[string.Format("L{0}", row)].Value = item.Employee1.FullName;
                row++;
            }


            Sheet.Cells["A:AZ"].AutoFitColumns();
            Response.Clear();
            Response.ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            Response.AddHeader("content-disposition", "attachment: filename=" + "Report.xlsx");
            Response.BinaryWrite(Ep.GetAsByteArray());
            Response.End();
        }
        // GET: Payments/Details/5
        public async Task<ActionResult> Details(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Payment payment = await db.Payment.FindAsync(id);
            if (payment == null)
            {
                return HttpNotFound();
            }
            return View(payment);
        }
        
        public ActionResult Create()
        {   
            ViewBag.Banks = new SelectList(db.Bank.OrderBy(m => m.BankCode), "BankId", "BankCode");
            ViewBag.Paycodes = (db.Paycode.Where(m => m.TuitionRelated == true).OrderBy(m => m.PaycodeID).Concat(db.Paycode.Where(m => m.TuitionRelated == false && m.ForBankPosting==true).OrderBy(m => m.Description))).ToList();
            return View();
        }
        
        public async Task<ActionResult> Delete(long? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Payment payment = await db.Payment.FindAsync(id);
            if (payment == null)
            {
                return HttpNotFound();
            }
            return View(payment);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(long id, string remarks)
        {
            try
            {
                Payment payment = db.Payment.Find(id);
                foreach (var paymentdetail in payment.PaymentDetails)
                {
                    CancelledOR co = new CancelledOR();
                    co.Remarks = "Online: " + remarks;
                    co.Amount = paymentdetail.Amount;
                    co.ORNo = payment.ORNo;
                    co.EducLevel = payment.EducLevel;
                    co.PaycodeID = paymentdetail.PaycodeID;
                    co.SemID = payment.SemID;
                    co.StudentID = payment.StudentID;
                    co.UserID = payment.CashierID;
                    db.CancelledOR.Add(co);
                }
                await db.SaveChangesAsync();
                var paymentfordeletion = db.Payment.Find(payment.PaymentID);
                paymentfordeletion.StudentID = null;
                paymentfordeletion.CheckNo = "CANCELLED";
                await db.SaveChangesAsync();

                var fordeletion = db.PaymentDetails.Where(m => m.PaymentID == id);
                db.PaymentDetails.RemoveRange(fordeletion);
                await db.SaveChangesAsync();

                return RedirectToAction("Index");
            }
            catch(Exception ex)
            {
                throw ex;
            }
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