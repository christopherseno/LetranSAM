using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Net.Http;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;

namespace ARManila.Models
{
  
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit https://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        public string Fullname { get; set; }
        public string BearerToken { get; set; }
        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            
            LetranIntegratedSystemEntities db = new LetranIntegratedSystemEntities();
            var employee = db.Employee.FirstOrDefaultAsync(m => m.EmployeeNo == userIdentity.Name);
            this.Fullname = employee.Result.FullName;
            userIdentity.AddClaim(new Claim("Fullname", this.Fullname));
            var identity = (ClaimsPrincipal)Thread.CurrentPrincipal;
            var claims = identity.Claims;
            
            return userIdentity;
        }
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext()
            : base("DefaultConnection", throwIfV1Schema: false)
        {
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
       
    }
}