using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using ARManila.Models;
namespace ARManila.Controllers
{
    [Authorize(Roles = "Finance, SuperAdmin")]
    public class BackaccountAPIController : ApiController
    {

        [HttpGet, Route("api/UnlinkBackaccountPayment/{id}")]
        public IHttpActionResult UnlinkBackaccountPayment(int id)
        {
            try
            {
                using (LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities())
                {
                    var backaccountpayment = db.BackAccountPayment.Find(id);
                    if (backaccountpayment == null) throw new Exception("Invalid backaccount payment.");                    
                    db.BackAccountPayment.Remove(backaccountpayment);
                    db.SaveChanges();
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route("api/UnlinkBackaccountDMCM/{id}")]
        public IHttpActionResult UnlinkBackaccountDMCM(int id)
        {
            try  
            {
                using (LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities())
                {
                    var backaccountDMCM = db.BackaccountDMCM.Find(id);
                    if (backaccountDMCM == null) throw new Exception("Invalid backaccount DMCM.");
                    db.BackaccountDMCM.Remove(backaccountDMCM);
                    db.SaveChanges();
                }
                return Ok();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet, Route("api/Backaccount/{id}")]
        public IHttpActionResult GetBackaccount(string id)
        {
            try
            {
                return Ok();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}
