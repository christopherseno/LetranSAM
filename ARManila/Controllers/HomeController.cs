using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
namespace ARManila.Controllers
{
    public class HomeController : BaseController
    {
        readonly LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        public ActionResult Index()
        {
            return View();
        }


    }
}