using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
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
        [HttpGet]
        [Route("Email")]
        public IHttpActionResult SendEmail(string recipient, string sender, string subject, string message)
        {
            try
            {
                var fromAddress = new MailAddress("admin@letran.edu.ph", "System Admin");
                const string fromPassword = "Boo18!<3";

                SmtpClient smtp = new SmtpClient
                {
                    Host = "smtp.gmail.com",
                    Port = 587,
                    EnableSsl = true,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(fromAddress.Address, fromPassword)
                };
                MailMessage mail = new MailMessage();
                mail.IsBodyHtml = true;
                mail.From = fromAddress;                
                mail.To.Add(recipient);
                mail.CC.Add(sender);
                
                mail.Subject = subject;
                mail.Body = message;
                smtp.Send(mail);
                return Ok();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
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
