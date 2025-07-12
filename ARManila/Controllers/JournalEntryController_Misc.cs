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

namespace ARManila.Controllers
{
    public partial class JournalEntryController : BaseController
    {
        //journal entry
        public ActionResult MiscJournalEntry()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            ViewBag.journalentrytype = new SelectList(db.JournalEntryType, "JournalEntryTypeId", "JounalEntryTypeName");
            return View(new QneJournal());
        }
        [HttpPost]
        public async Task<ActionResult> MiscJournalEntry(QneJournal model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");

            if (model.Action.Equals("Post to QNE") && model.IsQne && (model.description == null || model.description.Trim().Length < 1))
                ModelState.AddModelError("Description", "Description is required!");
            if (model.Action.Equals("Post to QNE") && model.IsQne && (model.docCode == null || model.docCode.Trim().Length < 1))
                ModelState.AddModelError("DocCode", "Journal Code is required!");
            if (model.Action.Equals("Post to QNE") && !model.IsQne)
                ModelState.AddModelError("DocCode", "Posting is for QNE accounts only!");
            
            ViewBag.journalentrytype = new SelectList(db.JournalEntryType, "JournalEntryTypeId", "JounalEntryTypeName");
            if (ModelState.IsValid)
            {
                
                var journalentrytype = db.JournalEntryType.Find(model.JournalEntryTypeId);

                if (model.Action.Equals("Show Data"))
                {
                    if (model.IsQne)
                    {
                        if (model.JournalEntryTypeId < 16)
                            return View(await GetJournalEntryQNEAsync(model));
                        //else if (model.JournalEntryTypeId == 16)
                        //   return View("MiscJournalEntryDcr", await GetJournalEntryQNEAsync(model));
                        return View("MiscJournalEntryAr", await GetJournalEntryQNEAsync(model));
                    }
                    else
                    {                        
                        if (model.JournalEntryTypeId < 16)                            
                            return View(await GetJournalEntryAsync(model));
                        //else if (model.JournalEntryTypeId == 16)
                        //   return View("MiscJournalEntryDcr", await GetJournalEntryAsync(model));
                        return View("MiscJournalEntryAr", await GetJournalEntryAsync(model));
                    }

                }
                else if (model.Action.Equals("Download Report"))
                {
                    //this is not yet working, another option if they want to print a report
                    if (model.IsQne)
                    {
                        return View(await GetJournalEntryQNEAsync(model));
                    }
                    return View(await GetJournalEntryAsync(model));
                }
                var department = model.Action;

                string entrytype = periodid.ToString() + "_" + department + "_" + journalentrytype.JournalEntryTypeId.ToString();
                var log = db.QnePostingLog.Where(m => m.StartDate == model.SDate && m.EndDate == model.EDate && m.IsSuccessful == true && m.JournalEntryType == entrytype).FirstOrDefault();
                if (log != null)
                    throw new Exception("A similar journal entry has already been posted last " + log.DatePosted.ToShortDateString());

                var qnejournalentry = await GetJournalEntryQNEAsync(model);
                QneJournalBase qneJournalBase = new QneJournalBase()
                {
                    currency = "PHP",
                    currencyRate = qnejournalentry.currencyRate,
                    description = qnejournalentry.description,
                    docCode = qnejournalentry.docCode,
                    docDate = qnejournalentry.EDate,
                    isTaxInclusive = qnejournalentry.isTaxInclusive
                };
                if (department.Equals("All"))
                {
                    qneJournalBase.details = qnejournalentry.details;
                }
                else
                {
                    qneJournalBase.details = qnejournalentry.details.Where(m => m.Department.Equals(department)).ToList();
                }
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
                        successlog.JournalEntryType = entrytype;
                        successlog.IsSuccessful = true;
                        db.QnePostingLog.Add(successlog);
                        db.SaveChanges();
                        return RedirectToAction("PostedQneJournalEntry");
                    }
                    else
                    {
                        var error = await httpResponse.Content.ReadAsAsync<QneError>();
                        QnePostingLog errorlog = new QnePostingLog();
                        errorlog.DatePosted = DateTime.Now;
                        errorlog.IsSuccessful = false;
                        errorlog.StartDate = model.SDate;
                        errorlog.EndDate = model.EDate;
                        errorlog.PostedBy = User.Identity.Name;
                        errorlog.JournalEntryType = error.message;
                        db.QnePostingLog.Add(errorlog);
                        db.SaveChanges();
                        throw new Exception(error.message);
                    }
                }
            }
            model.Entries = model.Entries.OrderBy(m=>m.AcaAcronym).OrderByDescending(m => m.IsDebit).ToList();
            model.details = model.details.OrderBy(m => m.IsDebit).ToList();
            return View(model);
        }

        private async Task<QneJournal> GetJournalEntryQNEAsync(QneJournal model)
        
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var journalentrytype = db.JournalEntryType.Find(model.JournalEntryTypeId);
            var departments = db.EducationalLevelDepartment.Where(m => m.EducationalLevelId == period.EducLevelID);
            if (model.JournalEntryTypeId == 16)
            {
                model.docCode = journalentrytype.JournalCode + "-" + model.SDate.ToString("MM") + model.SDate.ToString("dd") + model.SDate.ToString("yyyy");
                model.description = "TO RECORD " + journalentrytype.PostingMessage + " DATED " + model.SDate.ToString("MMMM dd, yyyy").ToUpper();
            }
            else if(model.JournalEntryTypeId > 16)
            {
                model.docCode = QneUtility.GetNextDocNo(journalentrytype.JournalCode + model.EDate.ToString("MM") + model.SDate.ToString("dd") + model.EDate.ToString("yyyy"));
                model.description = "TO RECORD " + journalentrytype.PostingMessage + " FOR THE PERIOD COVERED " + model.SDate.ToString("MMM dd") + "-" + model.EDate.ToString("dd, yyyy");
            }
            else
            {
                model.docCode = QneUtility.GetNextDocNo(journalentrytype.JournalCode + model.EDate.ToString("MM") + model.EDate.ToString("yyyy"));
                model.description = "TO RECORD " + journalentrytype.PostingMessage + " FOR THE PERIOD COVERED " + model.SDate.ToString("MMM dd") + "-" + model.EDate.ToString("dd, yyyy");
            }
            var cashonhand = db.ChartOfAccounts.Where(m => m.AcctNo == "A101.1").FirstOrDefault();
            switch (model.JournalEntryTypeId)
            {
                case (1):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryTALQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.AcaAcronym
                            });
                        }
                    }
                    break;
                case (2):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryMiscQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.AcaAcronym
                            });
                        }
                    }
                    break;
                case (3):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntrySupplementalQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.AcaAcronym
                            });
                        }
                    }
                    break;
                case (4):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryVariousQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.AcaAcronym
                            });
                        }
                    }
                    break;
                case (5):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryDebitFormQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLACA,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.ACRONYMACA
                            });
                        }
                    }
                    break;
                case (6):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryCreditFormQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLACA,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.ACRONYMACA
                            });
                        }
                    }
                    break;
                case (7):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryDebitMemoQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.Dept
                            });
                        }
                    }
                    break;
                case (8):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryCreditMemoQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.Dept
                            });
                        }
                    }
                    break;
                case (9):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryDebitMemoUnvalidatedQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.Dept
                            });
                        }
                    }
                    break;
                case (10):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryCreditMemoUnvalidatedQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.Dept
                            });
                        }
                    }
                    break;
                case (11):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryDiscountQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.AcaAcronym
                            });
                        }
                    }
                    break;
                case (12):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryDiscountAdjustmentQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.AcaAcronym
                            });
                        }
                    }
                    break;
                case (13):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryVoucherQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.AcaAcronym
                            });
                        }
                    }
                    break;
                case (14):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryDeferredIncomeQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.AcctName,
                                account = item.AcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.AcaAcronym
                            });
                        }
                    }
                    break;
                case (15):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryMiscIncomeQNE(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.details.Add(new QneJournalDetail
                            {
                                project = item.GLCode,
                                AccountName = item.Particular,
                                account = item.CRAcctNo,
                                credit = (decimal)(item.Credit ?? 0),
                                debit = (decimal)(item.Debit ?? 0),
                                description = model.description,
                                Department = item.AcaAcronym
                            });
                        }
                    }
                    break;
                case (16):
                    var cashpayments = await Task.Run(() => db.MakeCashReceiptJournalEntryQNE(model.SDate, model.EDate).ToList());
                    decimal totalcashonhand = 0;
                    foreach (var item in cashpayments)
                    {

                        model.details.Add(new QneJournalDetail
                        {
                            project = item.GLCode,
                            AccountName = item.AccountName,
                            account = item.AccountNo,
                            credit = (decimal)(item.Credit ?? 0),
                            debit = 0,
                            description = item.Description,
                            Department = ""
                        });
                        totalcashonhand += (decimal)(item.Credit ?? 0);
                    }
                    model.details.Add(new QneJournalDetail
                    {
                        project = "",
                        AccountName = cashonhand.QNEGLAccount != null ? cashonhand.QNEGLAccount.Description : "NOTSET",
                        account = cashonhand.QNEGLAccount != null ? cashonhand.QNEGLAccount.AccountCode : "NOTSET",
                        credit = 0,
                        debit = totalcashonhand,
                        description = cashonhand.QNEGLAccount != null ? cashonhand.QNEGLAccount.Description : "NOTSET",
                        Department = ""
                    });


                    break;
                case (17):
                    var arbankdeposits = await Task.Run(() => db.MakeCRJournalEntryQNE(model.SDate, model.EDate).ToList());
                    foreach (var item in arbankdeposits)
                    {
                        model.details.Add(new QneJournalDetail
                        {
                            project = item.GLCode,
                            AccountName = item.AccountName,
                            account = item.AccountNo,
                            credit = (decimal)(item.Credit ?? 0),
                            debit = (decimal)(item.Debit ?? 0),
                            description = model.description,
                            Department = item.AcaAcronym
                        });
                    }
                    break;
                case (18):
                    var cashierbankdeposits = await Task.Run(() => db.MakeCRJournalEntryCashierQNE(model.SDate, model.EDate).ToList());
                    foreach (var item in cashierbankdeposits)
                    {
                        model.details.Add(new QneJournalDetail
                        {
                            project = item.GLCode,
                            AccountName = item.AccountName,
                            account = item.AccountNo,
                            credit = (decimal)(item.Credit ?? 0),
                            debit = (decimal)(item.Debit ?? 0),
                            description = model.description,
                            Department = item.AcaAcronym
                        });
                    }
                    break;
                case (19):
                    var fastbills = await Task.Run(() => db.MakeCRJournalEntryFbQNE(model.SDate, model.EDate).ToList());
                    foreach (var item in fastbills)
                    {
                        model.details.Add(new QneJournalDetail
                        {
                            project = item.GLCode,
                            AccountName = item.AcctName,
                            account = item.AcctNo,
                            credit = (decimal)(item.Credit ?? 0),
                            debit = (decimal)(item.Debit ?? 0),
                            description = model.description,
                            Department = item.AcaAcronym
                        });
                    }
                    break;
            }
            return model;
        }

        private async Task<QneJournal> GetJournalEntryAsync(QneJournal model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var journalentrytype = db.JournalEntryType.Find(model.JournalEntryTypeId);
            var departments = db.EducationalLevelDepartment.Where(m => m.EducationalLevelId == period.EducLevelID);
            switch (model.JournalEntryTypeId)
            {
                case (1):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryTAL(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
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
                    }
                    break;
                case (2):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryMisc(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
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
                    }
                    break;
                case (3):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntrySupplemental(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
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
                     }
                    break;
                case (4):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryVarious(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
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
                    }
                    break;
                case (5):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryDebitForm(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.Entries.Add(new QneJournalEntryDTO
                            {
                                AcaAcronym = item.ACRONYMACA,
                                AcctName = item.AcctName,
                                AcctNo = item.AcctNo,
                                Credit = item.Credit ?? 0,
                                Debit = item.Debit ?? 0,
                                GLCode = item.GLACA,
                                Description = model.description
                            });
                        }
                    }
                    break;
                case (6):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryCreditForm(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.Entries.Add(new QneJournalEntryDTO
                            {
                                AcaAcronym = item.ACRONYMACA,
                                AcctName = item.AcctName,
                                AcctNo = item.AcctNo,
                                Credit = item.Credit ?? 0,
                                Debit = item.Debit ?? 0,
                                GLCode = item.GLACA,
                                Description = model.description
                            });
                        }
                    }
                    break;
                case (7):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryDebitMemo(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.Entries.Add(new QneJournalEntryDTO
                            {
                                AcaAcronym = item.ACRONYMACA,
                                AcctName = item.AcctName,
                                AcctNo = item.AcctNo,
                                Credit = item.Credit ?? 0,
                                Debit = item.Debit ?? 0,
                                GLCode = item.GLCode,
                                Description = model.description
                            });
                        }
                    }
                    break;
                case (8):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryCreditMemo(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.Entries.Add(new QneJournalEntryDTO
                            {
                                AcaAcronym = item.ACRONYMACA,
                                AcctName = item.AcctName,
                                AcctNo = item.AcctNo,
                                Credit = item.Credit ?? 0,
                                Debit = item.Debit ?? 0,
                                GLCode = item.GLCode,
                                Description = model.description
                            });
                        }
                    }
                    break;
                case (9):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryDebitMemoUnvalidated(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.Entries.Add(new QneJournalEntryDTO
                            {
                                AcaAcronym = item.Dept,
                                AcctName = item.AcctName,
                                AcctNo = item.AcctNo,
                                Credit = item.Credit ?? 0,
                                Debit = item.Debit ?? 0,
                                GLCode = item.GLCode,
                                Description = model.description
                            });
                        }
                    }
                    break;
                case (10):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryCreditMemoUnvalidated(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.Entries.Add(new QneJournalEntryDTO
                            {
                                AcaAcronym = item.Dept,
                                AcctName = item.AcctName,
                                AcctNo = item.AcctNo,
                                Credit = item.Credit ?? 0,
                                Debit = item.Debit ?? 0,
                                GLCode = item.GLCode,
                                Description = model.description
                            });
                        }
                    }
                    break;
                case (11):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryDiscount(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
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
                    }
                    break;
                case (12):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryDiscountAdjustment(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
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
                    }
                    break;
                case (13):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryVoucher(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
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
                    }
                    break;
                case (14):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryDeferredIncome(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
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
                    }
                    break;
                case (15):
                    foreach (var department in departments)
                    {
                        foreach (var item in await Task.Run(() => db.MakeARJournalEntryMiscIncome(model.SDate, model.EDate, periodid, department.AcademicDepartmentId).ToList()))
                        {
                            model.Entries.Add(new QneJournalEntryDTO
                            {
                                AcaAcronym = item.AcaAcronym,
                                AcctName = item.Particular,
                                AcctNo = item.CRAcctNo,
                                Credit = item.Credit ?? 0,
                                Debit = item.Debit ?? 0,
                                GLCode = item.GLCode,
                                Description = model.description
                            });
                        }
                    }
                    break;
                case (16):
                    var cashpayments = await Task.Run(() => db.MakeCashReceiptJournalEntry(model.SDate, model.EDate).ToList());
                    var cashonhand = db.ChartOfAccounts.Where(m => m.AcctNo == "A101.1").FirstOrDefault();
                    double totalcashonhand = 0;
                    foreach (var item in cashpayments)
                    {
                        model.Entries.Add(new QneJournalEntryDTO
                        {
                            AcaAcronym = "",
                            AcctName = item.AccountName,
                            AcctNo = item.AccountNo,
                            Credit = item.Credit ?? 0,
                            Debit = 0,
                            GLCode = item.GLCode,
                            Description = item.Description
                        });
                        totalcashonhand += item.Credit ?? 0;
                    }
                    model.Entries.Add(new QneJournalEntryDTO
                    {
                        AcaAcronym = "",
                        AcctName = cashonhand.AcctName,
                        AcctNo = cashonhand.AcctNo,
                        Credit = 0,
                        Debit = totalcashonhand,
                        GLCode = "",
                        Description = cashonhand.AcctName
                    });


                    break;
                case (17):
                    var arbankdeposits = await Task.Run(() => db.MakeCRJournalEntry(model.SDate, model.EDate).ToList());
                    foreach (var item in arbankdeposits)
                    {
                        model.Entries.Add(new QneJournalEntryDTO
                        {
                            AcaAcronym = item.AcaAcronym,
                            AcctName = item.AccountName,
                            AcctNo = item.AccountNo,
                            Credit = item.Credit ?? 0,
                            Debit = item.Debit ?? 0,
                            GLCode = item.GLCode,
                            Description = model.description
                        });
                    }
                    break;
                case (18):
                    var cashierbankdeposits = await Task.Run(() => db.MakeCRJournalEntryCashier(model.SDate, model.EDate).ToList());
                    foreach (var item in cashierbankdeposits)
                    {
                        model.Entries.Add(new QneJournalEntryDTO
                        {
                            AcaAcronym = item.AcaAcronym,
                            AcctName = item.AccountName,
                            AcctNo = item.AccountNo,
                            Credit = item.Credit ?? 0,
                            Debit = item.Debit ?? 0,
                            GLCode = item.GLCode,
                            Description = model.description
                        });
                    }
                    break;
                case (19):
                    var arfastbills = await Task.Run(() => db.MakeCRJournalEntryFb(model.SDate, model.EDate).ToList());
                    foreach (var item in arfastbills)
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
                    break;
            }
            return model;
        }
    }
}