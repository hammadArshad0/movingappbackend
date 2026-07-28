using System.Web.Http;
using System.Web.Http.Cors;

namespace task_full_stack
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Global CORS
            var cors = new EnableCorsAttribute(
                "http://localhost:5173",
                "*",
                "*"
            );

            config.EnableCors(cors);

            // Attribute Routing
            config.MapHttpAttributeRoutes();

            // Conventional Routing
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );
        }
    }
}