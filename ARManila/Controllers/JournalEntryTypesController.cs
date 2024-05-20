using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;

namespace ARManila.Controllers
{
    public class JournalEntryTypesController : Controller
    {
        private LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();

        // GET: JournalEntryTypes
        public async Task<ActionResult> Index()
        {
            return View(await db.JournalEntryType.ToListAsync());
        }

        // GET: JournalEntryTypes/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            JournalEntryType journalEntryType = await db.JournalEntryType.FindAsync(id);
            if (journalEntryType == null)
            {
                return HttpNotFound();
            }
            return View(journalEntryType);
        }

        // GET: JournalEntryTypes/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: JournalEntryTypes/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "JournalEntryTypeId,JounalEntryTypeName,PostingMessage,JournalCode")] JournalEntryType journalEntryType)
        {
            if (ModelState.IsValid)
            {
                db.JournalEntryType.Add(journalEntryType);
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }

            return View(journalEntryType);
        }

        // GET: JournalEntryTypes/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            JournalEntryType journalEntryType = await db.JournalEntryType.FindAsync(id);
            if (journalEntryType == null)
            {
                return HttpNotFound();
            }
            return View(journalEntryType);
        }

        // POST: JournalEntryTypes/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "JournalEntryTypeId,JounalEntryTypeName,PostingMessage,JournalCode")] JournalEntryType journalEntryType)
        {
            if (ModelState.IsValid)
            {
                db.Entry(journalEntryType).State = EntityState.Modified;
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(journalEntryType);
        }

        // GET: JournalEntryTypes/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            JournalEntryType journalEntryType = await db.JournalEntryType.FindAsync(id);
            if (journalEntryType == null)
            {
                return HttpNotFound();
            }
            return View(journalEntryType);
        }

        // POST: JournalEntryTypes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            JournalEntryType journalEntryType = await db.JournalEntryType.FindAsync(id);
            db.JournalEntryType.Remove(journalEntryType);
            await db.SaveChangesAsync();
            return RedirectToAction("Index");
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
