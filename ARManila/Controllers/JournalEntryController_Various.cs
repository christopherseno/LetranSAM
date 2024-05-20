using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
using Newtonsoft.Json;

namespace ARManila.Controllers
{
    public partial class JournalEntryController : BaseController
    {
        public ActionResult VariousJournalEntry()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            return View(new QneJournal());
        }
        [HttpPost]
        public async Task<ActionResult> VariousJournalEntry(QneJournal model)
        {
            if (model.Action.Equals("Post to QNE") && model.IsQne && (model.description == null || model.description.Trim().Length < 1))
                ModelState.AddModelError("Description", "Description is required!");
            if (model.Action.Equals("Post to QNE") && model.IsQne && (model.docCode == null || model.docCode.Trim().Length < 1))
                ModelState.AddModelError("DocCode", "Journal Code is required!");
            if (model.Action.Equals("Post to QNE") && !model.IsQne)
                ModelState.AddModelError("DocCode", "Posting is for QNE accounts only!");
            if (ModelState.IsValid)
            {
                LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
                var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
                var period = db.Period.Find(periodid);
                if (period == null) throw new Exception("Invalid period id.");
                if (model.Action.Equals("Show Data"))
                {
                    if (model.IsQne)
                    {
                        return View(await GetVariousJournalEntryQNEAsync(model));
                    }
                    else
                    {
                        return View(await GetVariousJournalEntryAsync(model));
                    }
                }
                else if (model.Action.Equals("Download Report"))
                {

                    if (model.IsQne)
                    {
                        return View(await GetVariousJournalEntryQNEAsync(model));
                    }
                    else
                    {
                        return View(await GetVariousJournalEntryAsync(model));
                    }
                }
                else
                {
                    string entrytype = periodid.ToString() + "_4";
                    var log = db.QnePostingLog.Where(m => m.StartDate == model.SDate && m.EndDate==model.EDate && m.IsSuccessful==true && m.JournalEntryType==entrytype).FirstOrDefault();
                    if (log != null)
                        throw new Exception("A similar journal entry has already been posted last " + log.DatePosted.ToShortDateString());

                    var qnejournalentry = await GetVariousJournalEntryQNEAsync(model);
                    QneJournalBase qneJournalBase = new QneJournalBase()
                    {
                        currency = "PHP",
                        currencyRate = qnejournalentry.currencyRate,
                        description = qnejournalentry.description,
                        details = qnejournalentry.details,
                        docCode = qnejournalentry.docCode,
                        docDate = DateTime.Now,
                        isTaxInclusive = qnejournalentry.isTaxInclusive
                    };
                    string json = JsonConvert.SerializeObject(qneJournalBase);
                    var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
                    using (var httpClient = new HttpClient())
                    {
                        httpClient.DefaultRequestHeaders.Add("DbCode", "LetranQNEDB");
                        httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        var httpResponse = await httpClient.PostAsync("https://qneapi.letran.edu.ph:5513/api/Journals", httpContent);
                        if (httpResponse.IsSuccessStatusCode)
                        {
                            var content = await httpResponse.Content.ReadAsAsync<PostedQneJournal>();
                            QnePostingLog successlog = new QnePostingLog();
                            successlog.DatePosted = DateTime.Now;
                            successlog.StartDate = model.SDate;
                            successlog.EndDate = model.EDate;
                            successlog.PostedBy = User.Identity.Name;
                            successlog.JournalEntryType = periodid.ToString() + "_4";
                            successlog.IsSuccessful = true;
                            db.QnePostingLog.Add(successlog);
                            db.SaveChanges();
                            return RedirectToAction("PostedQneJournalEntry");
                        }
                        else
                        {
                            QnePostingLog errorlog = new QnePostingLog();
                            errorlog.DatePosted = DateTime.Now;
                            errorlog.IsSuccessful = false;
                            errorlog.StartDate = model.SDate;
                            errorlog.EndDate = model.EDate;
                            errorlog.PostedBy = User.Identity.Name;
                            errorlog.JournalEntryType = httpResponse.ReasonPhrase;
                            db.QnePostingLog.Add(errorlog);
                            db.SaveChanges();
                            throw new Exception(httpResponse.ReasonPhrase);
                        }
                    }                    
                }
            }
            return View(model);
        }

        private async Task<QneJournal> GetVariousJournalEntryQNEAsync(QneJournal model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var journalentries = new List<MakeARJournalEntryVariousQNE_Result>();
            model.docCode = QneUtility.GetNextDocNo("ARS" + DateTime.Today.ToString("MM") + DateTime.Today.ToString("yyyy"));
            model.description = "TO RECORD VARIOUS FEES SET-UP FOR THE PERIOD COVERED " + model.SDate.ToString("MMM dd") + "-" + model.EDate.ToString("dd, yyyy");
            switch (period.EducLevelID)
            {
                case 1:
                    journalentries = await Task.Run(() => db.MakeARJournalEntryVariousQNE(model.SDate, model.EDate, periodid, 1).ToList());
                    break;
                case 2:
                    journalentries = await Task.Run(() => db.MakeARJournalEntryVariousQNE(model.SDate, model.EDate, periodid, 58).ToList());
                    break;
                case 3:
                    journalentries = await Task.Run(() => db.MakeARJournalEntryVariousQNE(model.SDate, model.EDate, periodid, 2).ToList());
                    break;
                case 5:
                    journalentries = await Task.Run(() => db.MakeARJournalEntryVariousQNE(model.SDate, model.EDate, periodid, 7).ToList());
                    break;
                case 6:
                    journalentries = await Task.Run(() => db.MakeARJournalEntryVariousQNE(model.SDate, model.EDate, periodid, 7).ToList());
                    break;
                default:
                    journalentries.AddRange(await Task.Run(() => db.MakeARJournalEntryVariousQNE(model.SDate, model.EDate, periodid, 3).ToList()));
                    journalentries.AddRange(await Task.Run(() => db.MakeARJournalEntryVariousQNE(model.SDate, model.EDate, periodid, 4).ToList()));
                    journalentries.AddRange(await Task.Run(() => db.MakeARJournalEntryVariousQNE(model.SDate, model.EDate, periodid, 5).ToList()));
                    journalentries.AddRange(await Task.Run(() => db.MakeARJournalEntryVariousQNE(model.SDate, model.EDate, periodid, 6).ToList()));
                    break;
            }
            model.details = new List<QneJournalDetail>();
            foreach (var item in journalentries)
            {
                model.details.Add(new QneJournalDetail
                {
                    project = item.AcaAcronym,
                    AccountName = item.AcctName,
                    account = item.AcctNo,
                    credit = (decimal)(item.Credit ?? 0),
                    debit = (decimal)(item.Debit ?? 0),
                    description = model.description
                });
            }
            return model;
        }

        private async Task<QneJournal> GetVariousJournalEntryAsync(QneJournal model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var journalentries = new List<MakeARJournalEntryVarious_Result>();
            switch (period.EducLevelID)
            {
                case 1:
                    journalentries = await Task.Run(() => db.MakeARJournalEntryVarious(model.SDate, model.EDate, periodid, 1).ToList());
                    break;
                case 2:
                    journalentries = await Task.Run(() => db.MakeARJournalEntryVarious(model.SDate, model.EDate, periodid, 58).ToList());
                    break;
                case 3:
                    journalentries = await Task.Run(() => db.MakeARJournalEntryVarious(model.SDate, model.EDate, periodid, 2).ToList());
                    break;
                case 5:
                    journalentries = await Task.Run(() => db.MakeARJournalEntryVarious(model.SDate, model.EDate, periodid, 7).ToList());
                    break;
                case 6:
                    journalentries = await Task.Run(() => db.MakeARJournalEntryVarious(model.SDate, model.EDate, periodid, 7).ToList());
                    break;
                default:
                    journalentries.AddRange(await Task.Run(() => db.MakeARJournalEntryVarious(model.SDate, model.EDate, periodid, 3).ToList()));
                    journalentries.AddRange(await Task.Run(() => db.MakeARJournalEntryVarious(model.SDate, model.EDate, periodid, 4).ToList()));
                    journalentries.AddRange(await Task.Run(() => db.MakeARJournalEntryVarious(model.SDate, model.EDate, periodid, 5).ToList()));
                    journalentries.AddRange(await Task.Run(() => db.MakeARJournalEntryVarious(model.SDate, model.EDate, periodid, 6).ToList()));
                    break;
            }
            model.Entries = new List<QneJournalEntryDTO>();
            foreach (var item in journalentries)
            {
                model.Entries.Add(new QneJournalEntryDTO
                {
                    AcaAcronym = item.AcaAcronym,
                    AcctName = item.AcctName,
                    AcctNo = item.AcctNo,
                    Credit = item.Credit ?? 0,
                    Debit = item.Debit ?? 0,
                    GLCode = item.GLCode,
                    Description = model.description
                });
            }
            return model;
        }
    }
}