using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using ARManila.Models;
using System.Net.Http;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Web.Http.Results;

namespace ARManila.Controllers
{
    public class ARQueryController : BaseController
    {
        private readonly LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult Index2()
        {
            return View();
        }
        [HttpPost]
        public ActionResult Index2(string studentno, string usedefault)
        {
            ARWrapper model = new ARWrapper();
            var periodid = usedefault != null && usedefault.Equals("on") ? "0" : HttpContext.Request.Cookies["PeriodId"].Value.ToString();
            //using (var httpClient = new HttpClient())
            //{
            //    LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            //    var aspnetuser = db.AspNetUsers.Where(m => m.UserName == User.Identity.Name).FirstOrDefault();
            //    httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + aspnetuser.BearerToken);
            //    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            //    HttpResponseMessage response = httpClient.GetAsync("https://api.letran.edu.ph/Assessment/ARQueryByFinance/" + studentno + "/" + periodid).Result;
            //    var statuscode = response.StatusCode;
            //    if (!response.IsSuccessStatusCode) throw new Exception("Expired token. Please try to login again.");
            //    var reason = response.ReasonPhrase;
            //    model = response.Content.ReadAsAsync<ARWrapper>().Result;
            //}
            var period = int.Parse(periodid);
            var apiController = new ARManila.Controllers.FinanceController();
            // Call the action method directly
            System.Web.Http.IHttpActionResult actionResult = apiController.GetARQueryByFinance(studentno, period);

            // Handle the result as needed
            var contentResult = actionResult as OkNegotiatedContentResult<ARWrapper>;

            if (contentResult != null)
            {
                var result = contentResult.Content;
                return View(result);
            }
            return View(model);            
        }
    }
}