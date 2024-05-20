using ARManila.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using System.Web.Security;

namespace ARManila.Controllers
{
    public class PeriodAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if(HttpContext.Current.Request.Cookies["PeriodId"] == null || String.IsNullOrEmpty(HttpContext.Current.Request.Cookies["PeriodId"].Value))
            {
                var controllerName = filterContext.RouteData.Values["controller"];
                var actionName = filterContext.RouteData.Values["action"];
                var parameters = filterContext.ActionParameters.Count();
                RouteValueDictionary routeValues = new RouteValueDictionary(new
                {
                    action = "SetPeriod",
                    controller = "Utility",
                    fromcontroller = parameters > 0 ? "Home" : controllerName,
                    fromaction =  parameters > 0 ? "Index" : actionName
                });
                filterContext.Result = new RedirectToRouteResult(routeValues);
            }
            else
            {
                LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
                var periodid = Convert.ToInt32(HttpContext.Current.Request.Cookies["PeriodId"].Value);
                var period = db.Period.Find(periodid);
                if(period == null)
                {
                    var controllerName = filterContext.RouteData.Values["controller"];
                    var actionName = filterContext.RouteData.Values["action"];
                    var parameters = filterContext.ActionParameters.Count();
                    RouteValueDictionary routeValues = new RouteValueDictionary(new
                    {
                        action = "SetPeriod",
                        controller = "Utility",
                        fromcontroller = parameters > 0 ? "Home" : controllerName,
                        fromaction = parameters > 0 ? "Index" : actionName
                    });
                    filterContext.Result = new RedirectToRouteResult(routeValues);
                }

            }
            base.OnActionExecuting(filterContext);
        }
    }
}