using ARManila.Models;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Threading.Tasks;
using System.Web.Http;
namespace ARManila.Controllers
{
    public class SMSController : ApiController
    {

        
        [HttpGet]
        public IHttpActionResult SendSMS(string id, string message)
        {
            var db = new ARManila.Models.LetranIntegratedSystemEntities();            
            if (db.Database.Connection.ConnectionString.Contains("172.20.0.10"))
            {
                id = "09494923258";
            }
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
        [Route("SMS/{id}")]
        public async Task<IHttpActionResult> SendSMSAsync(string id, string message)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://devapi.globelabs.com.ph/smsmessaging/v1/outbound/21589099/requests");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var smsLog = new outboundSMSMessageRequest
                    {
                        message = message,
                        address = id.TrimStart('0'),
                        passphrase = "A9undLLc2Q",
                        app_id = "6859HrGyoGhRdcAEy4TyMXhR98dRHyaG",
                        app_secret = "6347762b8166232702b40faa4b99f9222928f163c7cd9987b957cc88d9feffe9"
                    };
                    var response = await client.PostAsJsonAsync("", smsLog);
                    if(response.IsSuccessStatusCode)
                    {
                        var result = await response.Content.ReadAsStringAsync();
                        using (LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities())
                        {
                            SMSLog log = new SMSLog();
                            log.ApplicationNo = null;
                            log.DateSent = DateTime.Now;
                            log.Message = message;
                            log.MobileNo = id.TrimStart('0');
                            log.TypeofSystem = "twofactorauthentication";
                            db.SMSLog.Add(log);
                            db.SaveChanges();
                        }
                        return Ok();
                    }
                    else
                    {
                        return InternalServerError();
                    }
                }
               
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("OldEmail")]
        public IHttpActionResult SendEmail(string recipient, string? sender, string subject, string message)
        {
            try
            {
                var db = new ARManila.Models.LetranIntegratedSystemEntities();                
                if (db.Database.Connection.ConnectionString.Contains("172.20.0.10"))
                {
                    recipient = "christopher.seno@letran.edu.ph";
                }
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
                if(sender != null && sender.Length > 0)
                    mail.CC.Add(sender);
                mail.CC.Add("arletran@letran.edu.ph");
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

        [HttpGet]
        [Route("Email")]
        public async Task<IHttpActionResult> SendEmailAsync(string recipient, string? sender, string subject, string message)
        {
            try
            {
                MailMessage email = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");
                email.From = new MailAddress("admin@letran.edu.ph", "System Admin");
                email.To.Add(new MailAddress(recipient));
                if (sender != null && sender.Length > 0)
                    email.CC.Add(sender);
                email.Subject = subject;
                email.IsBodyHtml = true;
                email.Body = message;
                SmtpServer.Port = 587;
                SmtpServer.UseDefaultCredentials = false;
                SmtpServer.Credentials = new System.Net.NetworkCredential("admin@letran.edu.ph", "dfws xjjr tmng ekkp"); //April 30 2025, New Auth for google Email SMTP
                SmtpServer.EnableSsl = true;
                await SmtpServer.SendMailAsync(email);
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
