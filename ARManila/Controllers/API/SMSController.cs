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
        [Route("SMS/{id}")]
        public async Task<IHttpActionResult> SendSMSAsync(string id, string message)
        {
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://api.m360.com.ph/v4/sms/send");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var smsLog = new SMSMessageRequest
                    {
                        from = "CSJL-ACCTS",
                        to = new string[] { id },
                        dcs = 0,
                        content = new SMSContent { text = message },
                        request_id = "LOCALSMS12345",
                        app_key = "ZBV00SwA5HgU356n",
                        app_secret = "9iFiaSGAMMiuXEiC7P5jSkWJvucB5YS7"
                    };
                    var response = await client.PostAsJsonAsync("", smsLog);

                    // Read as bytes and decode manually: M360's response Content-Type
                    // carries an invalid charset, which makes ReadAsStringAsync() throw
                    // System.NotSupportedException ("The character set provided in ContentType is invalid").
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var result = System.Text.Encoding.UTF8.GetString(bytes);

                    if(response.IsSuccessStatusCode)
                    {
                        using (LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities())
                        {
                            SMSLog log = new SMSLog();
                            log.ApplicationNo = null;
                            log.DateSent = DateTime.Now;
                            log.Message = message;
                            log.MobileNo = id.TrimStart('0');
                            log.TypeofSystem = "twofactorauthentication";
                            log.Response = result;
                            db.SMSLog.Add(log);
                            db.SaveChanges();
                        }
                        return Ok();
                    }
                    else
                    {
                        // Surface M360's failure reason (e.g. invalid sender mask, bad number)
                        // instead of a blank 500.
                        return Content(response.StatusCode, result);
                    }
                }
               
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        
        [HttpGet]
        [Route("Email")]
        public async Task<IHttpActionResult> SendEmailAsync(string recipient, string sender, string fromname, string subject, string message)
        {
            try
            {
                MailMessage email = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");
                email.From = new MailAddress("letranmailing@letran.edu.ph", fromname);
                email.To.Add(new MailAddress(recipient));
                if (sender != null && sender.Length > 0)
                    email.CC.Add(sender);
                email.Subject = subject;
                email.IsBodyHtml = true;
                email.Body = message;
                SmtpServer.Port = 587;
                SmtpServer.UseDefaultCredentials = false;
                SmtpServer.Credentials = new System.Net.NetworkCredential("letranmailing@letran.edu.ph", "ovrq nplx fjas qfmt"); //April 30 2025, New Auth for google Email SMTP
                SmtpServer.EnableSsl = true;
                await SmtpServer.SendMailAsync(email);
                return Ok();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("EmailWithAttachment")]
        public async Task<IHttpActionResult> SendEmailWithAttachmentAsync(string recipient, string sender, string subject, string message, System.Web.HttpPostedFileBase attachment)
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

                if (attachment != null && attachment.ContentLength > 0)
                {
                    var mailAttachment = new Attachment(attachment.InputStream, attachment.FileName);
                    email.Attachments.Add(mailAttachment);
                }

                SmtpServer.Port = 587;
                SmtpServer.UseDefaultCredentials = false;
                SmtpServer.Credentials = new System.Net.NetworkCredential("letranmailing@letran.edu.ph", "ovrq nplx fjas qfmt"); //April 30 2025, New Auth for google Email SMTP
                SmtpServer.EnableSsl = true;
                await SmtpServer.SendMailAsync(email);

                email.Dispose();

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
