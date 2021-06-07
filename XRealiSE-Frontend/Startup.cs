#region

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XRealiSE_DBConnection;

#endregion

namespace XRealiSE_Frontend
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddRazorPages(options =>
            {
               // options.Conventions.AddPageRoute("/Index", "/{key:int?}/{start:int?}/{repository:long?}");
                options.Conventions.AddPageRoute("/Index", "/");
            }).AddRazorPagesOptions(options => options.Conventions.ConfigureFilter(new IgnoreAntiforgeryTokenAttribute()));
            DatabaseConnection.DatabaseConnectionString = Configuration.GetConnectionString("xrealise");
            services.AddDbContext<DatabaseConnection>();


            // services.AddAntiforgery(options => options.Cookie.Expiration = TimeSpan.Zero);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();
            app.UseRouting();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapRazorPages());
        }
    }
}
