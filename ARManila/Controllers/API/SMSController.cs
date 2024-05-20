using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace ARManila.Controllers
{
    public class SMSController : ApiController
    {
        [HttpGet]
        public IHttpActionResult SendSMS(string id, string message)
        {
            using (HttpClient client = new HttpClient())
            {
                var sms = new SMS
                {
                    address = id,
                    ClientCorrelator = "21589458",
                    outboundSMSMessageRequest = new SMSMessage { message = message },
                    senderAddress = "LETRAN"
                };
                var response = client.PostAsJsonAsync<SMS>("https://api.m360.com.ph/v3/api/globelabs/mt/A9undLLc2Q", sms);
                var result = response.Result;
                if (result.IsSuccessStatusCode)
                {
                    return Ok();
                }
                else
                {
                    return InternalServerError();
                }
            }

        }

    }

    public class SMSMessage
    {
        public string message { get; set; }
    }
    public class SMS
    {
        public SMSMessage outboundSMSMessageRequest { get; set; }
        public string ClientCorrelator { get; set; }
        public string address { get; set; }
        public string senderAddress { get; set; }
    }
}
