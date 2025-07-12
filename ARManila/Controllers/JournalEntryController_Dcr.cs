using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
using Newtonsoft.Json;
using ARManila.Models.QneDb;
namespace ARManila.Controllers
{
    public partial class JournalEntryController : BaseController
    {
        public ActionResult ViewReceiptVoucher(DateTime? OrDate)
        {
            QNEDBEntities qNEDB = new QNEDBEntities();
            OrDate = OrDate.HasValue ? OrDate.Value : DateTime.Today;
            ViewBag.OrDate = OrDate.Value.ToString("yyyy-MM-dd");
            var receiptvouchers = qNEDB.Receipts.Where(m => m.ReceiptDate == OrDate);
            return View(receiptvouchers);
        }
        public ActionResult DcrNonTuition()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            ViewBag.IsQne = false;
            return View(new List<Dcr>());
        }
        [HttpPost]
        public async Task<ActionResult> DcrNonTuition(DateTime OrDate, string Action, bool IsQne)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            ViewBag.IsQne = IsQne;
            ViewBag.OrDate = OrDate.ToString("yyyy-MM-dd");
            ViewBag.OrDate2 = OrDate.ToString("MM-dd-yyyy");
            var coh = db.ChartOfAccounts.Where(m => m.AcctName.Equals("CASH ON HAND")).FirstOrDefault();
            var cashonhand = coh == null ? "NOTSET" : (IsQne ? (coh.QNEGLAccount == null ? "NOTSET" : coh.QNEAccountCode) : coh.AcctNo);
            ViewBag.cashonhand = cashonhand;
            if (ModelState.IsValid)
            {
                var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
                var period = db.Period.Find(periodid);
                if (period == null) throw new Exception("Invalid period id.");                
                if (Action.Equals("Show Data"))
                {                    
                    return View(await GetDcrJournalEntryAsync(OrDate, IsQne));
                }
                else if (Action.Equals("Download Report"))
                {
                    return View(await GetDcrJournalEntryAsync(OrDate, IsQne));
                }     
               
                var receiptvouchers = await GetDcrJournalEntryAsync(OrDate, IsQne);
                if (receiptvouchers.Any(m => m.CanBePosted == false)) throw new Exception("One of the items has no QNE Code.");
                if(coh == null) throw new Exception("CASH ON HAND has no QNE Code.");
                QNEDBEntities qnedb = new QNEDBEntities();
                foreach (var item in receiptvouchers)
                {
                    var existingreceipt = qnedb.Receipts.FirstOrDefault(m => m.ReceiptCode.Equals(item.receiptCode));
                    if (existingreceipt == null)
                    {
                        item.depositToAccount = cashonhand;
                        string json = JsonConvert.SerializeObject(item);
                        var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                        using (var httpClient = new HttpClient())
                        {
                            httpClient.DefaultRequestHeaders.Add("DbCode", "LetranQNEDB");
                            httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                            var httpResponse = await httpClient.PostAsync("https://qneapi.letran.edu.ph:5513/api/ReceiptVouchers", httpContent);
                            if (httpResponse.IsSuccessStatusCode)
                            {
                                var content = await httpResponse.Content.ReadAsAsync<Dcr>();
                            }
                            else
                            {
                                var error = await httpResponse.Content.ReadAsAsync<QneError>();
                                throw new Exception(error.message);
                            }
                        }
                    }
                }
                return RedirectToAction("ViewReceiptVoucher", new { OrDate = OrDate});
            }
            return View(new List<Dcr>());
        }

        private async Task<List<Dcr>> GetDcrJournalEntryAsync(DateTime ordate, bool isqne)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);            
            if (period == null) throw new Exception("Invalid period id.");
            List<Dcr> dcrs = new List<Dcr>();
            DateTime enddate = ordate.AddDays(1);
            var payments = await db.PaymentDetails.Where(m => m.PaycodeID > 11 && m.Payment.DateReceived >= ordate && m.Payment.DateReceived < enddate && !m.Payment.ORNo.StartsWith("*")).ToListAsync();
            foreach (var item in payments.Where(m=>m.Payment.StudentID != null).GroupBy(m=> new { m.Payment.Student.FullName, m.Payment.ORNo, m.Payment.DateReceived, m.Payment.StudentID }))
            {
                var curriculum = db.Student_Curriculum.Where(m => m.StudentID == item.Key.StudentID && m.Status == 1).FirstOrDefault();
                var project = "";
                if(curriculum!= null && curriculum.Curriculum.AcaDeptID.HasValue)
                {
                    var department = db.AcademicDepartment.Find(curriculum.Curriculum.AcaDeptID);
                    project = isqne ? (department.QNEProjectCode != null ? department.QNEProjectCode : "NOTSET") : department.GLCode;
                }
                var receiptvoucher = new Dcr();
                receiptvoucher.currency = "PHP";
                receiptvoucher.description = "";
                receiptvoucher.receiptCode = item.Key.ORNo;
                receiptvoucher.receiptDate = item.Key.DateReceived;
                receiptvoucher.receiveFrom = item.Key.FullName;
                receiptvoucher.project = project;
                List<string> description = new List<string>();
                int pos = 1;
                foreach(var paycode in item)
                {
                    description.Add(paycode.Paycode.Description);
                    var receiptdetail = new DcrDetail();
                    if (isqne)
                    {
                        receiptdetail.account = paycode.Paycode.SubCOANo.HasValue ? (paycode.Paycode.SubChartOfAccounts.QNEGLAccount != null ? paycode.Paycode.SubChartOfAccounts.QNEGLAccount.AccountCode : "NOTSET") : (paycode.Paycode.COANo.HasValue ? (paycode.Paycode.ChartOfAccounts.QNEGLAccount != null ? paycode.Paycode.ChartOfAccounts.QNEGLAccount.AccountCode : "NOTSET") : "NOTSET");
                                               
                    }
                    else
                    {
                        receiptdetail.account = paycode.Paycode.SubCOANo.HasValue ? paycode.Paycode.SubChartOfAccounts.SubAcctNo : (paycode.Paycode.COANo.HasValue ? paycode.Paycode.ChartOfAccounts.AcctNo : "NOTSET");
                    }
                    receiptvoucher.CanBePosted = receiptvoucher.CanBePosted = false ? receiptvoucher.CanBePosted : !receiptdetail.account.Equals("NOTSET");
                    receiptdetail.project = project;
                    receiptdetail.amount = (decimal)paycode.Amount;
                    receiptdetail.description = paycode.Paycode.Description;
                    receiptdetail.pos = pos;                    
                    pos++;
                    receiptvoucher.details.Add(receiptdetail);
                }
                var finaldescription = String.Join(";", description.ToArray());
                receiptvoucher.description =finaldescription.Length> 100 ? finaldescription.Substring(0,100) : finaldescription;
                dcrs.Add(receiptvoucher);
            }

            return dcrs;
        }
    }
}