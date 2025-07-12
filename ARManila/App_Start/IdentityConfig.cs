using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.AspNet.Identity.Owin;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using ARManila.Models;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mail;

namespace ARManila
{
    public class EmailService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
            return Task.Run(() => {
                MailMessage email = new MailMessage();
                SmtpClient SmtpServer = new SmtpClient("smtp.gmail.com");
                //email.From = new MailAddress("itms@letranbataan.edu.ph", "ITSD");
                email.From = new MailAddress("admin@letran.edu.ph", "LETRAN");
                email.To.Add(message.Destination);                
                email.Subject = message.Subject;
                email.IsBodyHtml = true;
                email.Body = message.Body;
                //email.Bcc.Add("admin@letranbataan.edu.ph");
                SmtpServer.Port = 587;
                SmtpServer.UseDefaultCredentials = false;
                SmtpServer.Credentials = new System.Net.NetworkCredential("admin@letran.edu.ph", "dfws xjjr tmng ekkp");
                //SmtpServer.Credentials = new System.Net.NetworkCredential("itms@letranbataan.edu.ph", "1T3@mWoRk0");
                //SmtpServer.Credentials = new System.Net.NetworkCredential("christopher.seno@letran.edu.ph", "-951Han5");
                SmtpServer.EnableSsl = true;
                SmtpServer.Send(email);
            });
        }
    }

    public class SmsService : IIdentityMessageService
    {
        public Task SendAsync(IdentityMessage message)
        {
             return Task.Run(() => {
                HttpClient client = new HttpClient();
                client.BaseAddress = new Uri("https://devapi.globelabs.com.ph/smsmessaging/v1/outbound/21589099/requests");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                var student = new outboundSMSMessageRequest
                {
                    message = message.Body,
                    address = message.Destination.TrimStart('0'),
                    passphrase = "A9undLLc2Q",
                    app_id = "6859HrGyoGhRdcAEy4TyMXhR98dRHyaG",
                    app_secret = "6347762b8166232702b40faa4b99f9222928f163c7cd9987b957cc88d9feffe9"
                };
                client.PostAsJsonAsync("", student);
                using (LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities())
                {
                    SMSLog log = new SMSLog();
                    log.ApplicationNo = null;
                    log.DateSent = DateTime.Now;
                    log.Message = message.Body;
                    log.MobileNo = message.Destination.TrimStart('0');
                    log.TypeofSystem = "twofactorauthentication";
                    db.SMSLog.Add(log);
                    db.SaveChanges();
                }
            });
        }
    }

    // Configure the application user manager used in this application. UserManager is defined in ASP.NET Identity and is used by the application.
    public class ApplicationUserManager : UserManager<ApplicationUser>
    {
        public ApplicationUserManager(IUserStore<ApplicationUser> store)
            : base(store)
        {
        }

        public static ApplicationUserManager Create(IdentityFactoryOptions<ApplicationUserManager> options, IOwinContext context) 
        {
            var manager = new ApplicationUserManager(new UserStore<ApplicationUser>(context.Get<ApplicationDbContext>()));
            // Configure validation logic for usernames
            manager.UserValidator = new UserValidator<ApplicationUser>(manager)
            {
                AllowOnlyAlphanumericUserNames = false,
                RequireUniqueEmail = true
            };

            // Configure validation logic for passwords
            manager.PasswordValidator = new PasswordValidator
            {
                RequiredLength = 6,
                RequireNonLetterOrDigit = true,
                RequireDigit = true,
                RequireLowercase = true,
                RequireUppercase = true,
            };

            // Configure user lockout defaults
            manager.UserLockoutEnabledByDefault = true;
            manager.DefaultAccountLockoutTimeSpan = TimeSpan.FromMinutes(5);
            manager.MaxFailedAccessAttemptsBeforeLockout = 5;

            // Register two factor authentication providers. This application uses Phone and Emails as a step of receiving a code for verifying the user
            // You can write your own provider and plug it in here.
            manager.RegisterTwoFactorProvider("Phone Code", new PhoneNumberTokenProvider<ApplicationUser>
            {
                MessageFormat = "Your security code is {0}"
            });
            manager.RegisterTwoFactorProvider("Email Code", new EmailTokenProvider<ApplicationUser>
            {
                Subject = "Security Code",
                BodyFormat = "Your security code is {0}"
            });
            manager.EmailService = new EmailService();
            manager.SmsService = new SmsService();
            var dataProtectionProvider = options.DataProtectionProvider;
            if (dataProtectionProvider != null)
            {
                manager.UserTokenProvider = 
                    new DataProtectorTokenProvider<ApplicationUser>(dataProtectionProvider.Create("ASP.NET Identity"));
            }
            return manager;
        }
    }

    // Configure the application sign-in manager which is used in this application.
    public class ApplicationSignInManager : SignInManager<ApplicationUser, string>
    {
        public ApplicationSignInManager(ApplicationUserManager userManager, IAuthenticationManager authenticationManager)
            : base(userManager, authenticationManager)
        {
        }

        public override Task<ClaimsIdentity> CreateUserIdentityAsync(ApplicationUser user)
        {
            return user.GenerateUserIdentityAsync((ApplicationUserManager)UserManager);
        }

        public static ApplicationSignInManager Create(IdentityFactoryOptions<ApplicationSignInManager> options, IOwinContext context)
        {
            return new ApplicationSignInManager(context.GetUserManager<ApplicationUserManager>(), context.Authentication);
        }
    }
}
