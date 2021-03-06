namespace ParkingRota
{
    using System;
    using Business;
    using Business.Model;
    using Data;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Identity;
    using Microsoft.AspNetCore.Identity.UI;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.StaticFiles;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;

    public class Startup
    {
        public Startup(IConfiguration configuration) => this.Configuration = configuration;

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.CheckConsentNeeded = context => true;
                options.MinimumSameSitePolicy = SameSiteMode.Strict;
                options.Secure = CookieSecurePolicy.SameAsRequest;
            });

            var connectionString =
                Environment.GetEnvironmentVariable("ParkingRotaConnectionString") ??
                this.Configuration.GetValue<string>("ParkingRotaConnectionString") ??
                this.Configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString));

            services.AddAuthorization(options =>
            {
                options.AddPolicy(UserRole.SiteAdmin, policy => policy.RequireRole(UserRole.SiteAdmin));
                options.AddPolicy(UserRole.TeamLeader, policy => policy.RequireRole(UserRole.TeamLeader));
            });

            services.AddIdentity<ApplicationUser, IdentityRole>(c =>
                {
                    c.Password.RequireDigit = false;
                    c.Password.RequireLowercase = false;
                    c.Password.RequireNonAlphanumeric = false;
                    c.Password.RequireUppercase = false;
                    c.Password.RequiredLength = 10;
                    c.Password.RequiredUniqueChars = 5;

                    c.SignIn.RequireConfirmedEmail = true;

                    c.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultUI(UIFramework.Bootstrap4)
                .AddDefaultTokenProviders();

            ServiceCollectionHelper.RegisterServices(services);

            services.AddMvc()
                .AddRazorPagesOptions(options =>
                {
                    options.Conventions.AuthorizePage("/EditReservations", UserRole.TeamLeader);
                    options.Conventions.AuthorizePage("/OverrideRequests", UserRole.TeamLeader);

                    options.Conventions.AuthorizeFolder("/Users", UserRole.SiteAdmin);

                    options.Conventions.AuthorizeFolder("/");

                    options.Conventions.AllowAnonymousToPage("/Index");
                    options.Conventions.AllowAnonymousToPage("/Error");
                    options.Conventions.AllowAnonymousToPage("/Privacy");
                    options.Conventions.AllowAnonymousToPage("/RegisterSuccess");

                    options.Conventions.AddPageRoute("/OverrideRequests", "OverrideRequests/{id?}");
                })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            services.AddHttpContextAccessor();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
            }

            if (Helpers.IsElasticBeanstalk())
            {
                loggerFactory.AddAWSProvider(this.Configuration.GetAWSLoggingConfigSection());
            }

            app.UseHttpsRedirection();

            var provider = new FileExtensionContentTypeProvider
            {
                Mappings = {[".webmanifest"] = "application/manifest+json"}
            };

            app.UseStaticFiles(new StaticFileOptions {ContentTypeProvider = provider});

            app.UseCookiePolicy();

            app.UseAuthentication();

            app.UseMvc();
        }
    }
}
