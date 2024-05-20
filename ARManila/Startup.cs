using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(ARManila.Startup))]
namespace ARManila
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
