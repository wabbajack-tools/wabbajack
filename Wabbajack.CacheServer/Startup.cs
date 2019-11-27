using System;
using System.Collections.Generic;
using System.Text;
using Owin;

namespace Wabbajack.CacheServer
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            app.UseNancy();
        }
    }
}
