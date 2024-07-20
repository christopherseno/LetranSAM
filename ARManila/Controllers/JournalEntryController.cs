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
using ARManila.Models.QneDb;
namespace ARManila.Controllers
{
    public partial class JournalEntryController : BaseController
    {
        // GET: JournalEntry
        public ActionResult PostedQneJournalEntry()
        {
            QNEDBEntities qnedb = new QNEDBEntities();
            var journals = qnedb.Journals;
            return View(journals);
        }
        public ActionResult Index()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var glaccounts = db.QNEGLAccount;
            return View(glaccounts);
        }
        [HttpPost]
        public async Task<ActionResult> Sync()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            List<QneGlAccountDto> glaccounts = new List<QneGlAccountDto>();
            int skip = 0;
            int take = 1;
            bool morerecords = true;
            while (morerecords)
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("DbCode", "LetranQNEDB");            
                HttpResponseMessage response = await httpClient.GetAsync("https://qneapi.letran.edu.ph:5513/api/GLAccounts?%24skip=" + skip + "&%24top=1000");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsAsync<List<QneGlAccountDto>>();
                    if (content.Count < 1000)
                    {
                        morerecords = false;
                        glaccounts.AddRange(content);
                    }
                    else
                    {
                        glaccounts.AddRange(content);
                        skip = take * 1000;
                        take++;
                    }
                }
                else
                {
                    throw new Exception(response.ReasonPhrase);
                }
            }
            foreach (var item in glaccounts)
            {
                var glaccount = await db.QNEGLAccount.FindAsync(item.GlAccountCode);
                if (glaccount != null)
                {
                    glaccount.Description = item.Description;
                    glaccount.IsActive = item.IsActive == "true" ? true : false;
                    glaccount.IsSubAccount = item.IsSubAccount == "true" ? true : false;
                    await db.SaveChangesAsync();
                }
                else
                {
                    db.QNEGLAccount.Add(new QNEGLAccount
                    {
                        AccountCode = item.GlAccountCode,
                        Description = item.Description,
                        IsActive = item.IsActive == "true" ? true : false,
                        IsSubAccount = item.IsSubAccount == "true" ? true : false
                    });
                    await db.SaveChangesAsync();

                }
            }
            return RedirectToAction("Index");
        }

        public ActionResult Departments()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var projects = db.Project;
            return View(projects);
        }
        [HttpPost]
        public async Task<ActionResult> DepartmentSync()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            List<ProjectCodeDTO> projects = new List<ProjectCodeDTO>();
            int skip = 0;
            int take = 1;
            bool morerecords = true;
            while (morerecords)
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("DbCode", "LetranQNEDB");
                HttpResponseMessage response = await httpClient.GetAsync("https://qneapi.letran.edu.ph:5513/api/Projects?%24skip=" + skip + "&%24top=1000");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsAsync<List<ProjectCodeDTO>>();
                    if (content.Count < 1000)
                    {
                        morerecords = false;
                        projects.AddRange(content);
                    }
                    else
                    {
                        projects.AddRange(content);
                        skip = take * 1000;
                        take++;
                    }
                }
                else
                {
                    throw new Exception(response.ReasonPhrase);
                }
            }
            foreach (var item in projects)
            {
                var project = await db.Project.FindAsync(item.ProjectCode);
                if (project != null)
                {
                    project.Description = item.Description;
                    await db.SaveChangesAsync();
                }
                else
                {
                    db.Project.Add(new Project
                    {
                        ProjectCode = item.ProjectCode,
                        Description = item.Description
                    });
                    await db.SaveChangesAsync();
                }
            }
            return RedirectToAction("Departments");
        }

        public ActionResult DepartmentSetup()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var departments = db.AcademicDepartment.OrderBy(m => m.AcaAcronym);
            return View(departments);
        }

        public ActionResult EditDepartmentSetup(int id)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            ViewBag.qne = new SelectList(db.Project.OrderBy(m => m.Description), "ProjectCode", "Description");
            var department = db.AcademicDepartment.Find(id);
            return View(department);
        }

        [HttpPost]
        public ActionResult EditDepartmentSetup(AcademicDepartment model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            ViewBag.qne = new SelectList(db.Project.OrderBy(m => m.Description), "ProjectCode", "Description");
            var department = db.AcademicDepartment.Find(model.AcaDeptID);
            department.QNEProjectCode = model.QNEProjectCode;
            db.SaveChanges();
            return RedirectToAction("DepartmentSetup");
        }

        public ActionResult ChartOfAccountSetup()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var chartofaccounts = db.ChartOfAccounts.OrderBy(m => m.AcctNo);
            return View(chartofaccounts);
        }

        public ActionResult EditChartOfAccountSetup(int id)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            //ViewBag.qne = new SelectList(db.QNEGLAccount.OrderBy(m => m.Description), "AccountCode", "Description");
            var options = db.QNEGLAccount
               .OrderBy(m => m.Description)
               .Select(m => new
               {
                   id = m.AccountCode,
                   Description = m.AccountCode + " - " + m.Description
               })
               .ToList();
            ViewBag.qne = new SelectList(options, "id", "Description");
            var chartofaccount = db.ChartOfAccounts.Find(id);
            return View(chartofaccount);
        }

        [HttpPost]
        public ActionResult EditChartOfAccountSetup(ChartOfAccounts model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            ViewBag.qne = new SelectList(db.Project.OrderBy(m => m.ProjectCode), "ProjectCode", "Description");
            var chartofaccount = db.ChartOfAccounts.Find(model.AcctID);
            chartofaccount.QNEAccountCode = model.QNEAccountCode;
            db.SaveChanges();
            return RedirectToAction("ChartOfAccountSetup");
        }

        public ActionResult SubLedgerSetup()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var subledgers = db.SubChartOfAccounts.OrderBy(m => m.SubAcctNo);
            return View(subledgers);
        }

        public ActionResult EditSubLedgerSetup(int id)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            //ViewBag.qne = new SelectList(db.QNEGLAccount.OrderBy(m => m.Description), "AccountCode", "Description");
            //3/4/2024 - fernandez - to display account code and description on text field for selectlist
            var options = db.QNEGLAccount
                .OrderBy(m => m.Description)
                .Select(m => new
                {
                    id = m.AccountCode,
                    Description = m.AccountCode + " - " + m.Description
                })
                .ToList();
            ViewBag.qne = new SelectList(options, "id", "Description");
            var subledger = db.SubChartOfAccounts.Find(id);
            return View(subledger);
        }

        [HttpPost]
        public ActionResult EditSubLedgerSetup(SubChartOfAccounts model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            ViewBag.qne = new SelectList(db.QNEGLAccount.OrderBy(m => m.Description), "AccountCode", "Description");
            var subledger = db.SubChartOfAccounts.Find(model.SubAcctID);
            subledger.QNEAccountCode = model.QNEAccountCode;
            db.SaveChanges();
            return RedirectToAction("SubLedgerSetup");
        }

        public ActionResult SchoolYearARSetup()
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            var schoolYears = db.SchoolYear.OrderByDescending(m => m.SequenceNo);
            return View(schoolYears);
        }

        public ActionResult EditSchoolYearARSetup(int id)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            ViewBag.qne = new SelectList(db.QNEGLAccount.OrderBy(m => m.Description), "AccountCode", "FullName");
            var schoolyear = db.SchoolYear.Find(id);
            return View(schoolyear);
        }

        [HttpPost]
        public ActionResult EditSchoolYearARSetup(SchoolYear model)
        {
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var periodid = Convert.ToInt32(HttpContext.Request.Cookies["PeriodId"].Value);
            var period = db.Period.Find(periodid);
            if (period == null) throw new Exception("Invalid period id.");
            ViewBag.qne = new SelectList(db.QNEGLAccount.OrderBy(m => m.Description), "AccountCode", "FullName");
            var schoolyear = db.SchoolYear.Find(model.SYID);
            schoolyear.QNEAccountCode = model.QNEAccountCode;
            db.SaveChanges();
            return RedirectToAction("SchoolYearARSetup");
        }
    }
}