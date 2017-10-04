﻿using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Action.Models;
using Action.Services.Scrap;
using Hangfire;
using Hangfire.MemoryStorage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Action
{
    public class Startup
    {
        public IConfigurationRoot Configuration { get; }

        public Startup(IHostingEnvironment env)
        {
	        var builder = new ConfigurationBuilder()
		        .SetBasePath(env.ContentRootPath)
		        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
		        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            Configuration = builder.Build();

            
        }

        public void ConfigureServices(IServiceCollection services)
        {

			services.AddEntityFrameworkMySql();
			services.AddSingleton<IConfigurationRoot>(provider => Configuration);
			
	        // add database context
	        services.AddDbContext<ApplicationDbContext>(options =>
		        options.UseMySql(Configuration.GetConnectionString("DataConnection"),
			        opt => opt.MigrationsAssembly("Action")));
        
            // Add framework services.
            services.AddMvc();
			services.AddCors(o => o.AddPolicy("Default", builder =>
			{
				builder.AllowAnyOrigin()
					.AllowAnyMethod()
					.AllowAnyHeader();
			}));

			services.AddIdentity<User, Role>(config => config.Cookies.ApplicationCookie.Events = new CookieAuthenticationEvents
			{
				OnRedirectToLogin = ctx => {
					if (ctx.Request.Path.StartsWithSegments("/api"))
						ctx.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
					return Task.FromResult(0);
				}
            }).AddEntityFrameworkStores<ApplicationDbContext>()
			.AddDefaultTokenProviders();
	        
	        
	        services.AddHangfire(x => x.UseStorage(new MemoryStorage()));
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();
			
	        app.UseIdentity();
	        app.UseHangfireDashboard("/tasks");
	        app.UseHangfireServer();
			app.UseCors("Default");
			app.UseJwtBearerAuthentication(new JwtBearerOptions()
			{
				AutomaticAuthenticate = true,
				AutomaticChallenge = true,
				TokenValidationParameters = new TokenValidationParameters()
				{
					ValidIssuer = Configuration["JwtSecurityToken:Issuer"],
					ValidAudience = Configuration["JwtSecurityToken:Audience"],
					ValidateIssuerSigningKey = true,
					IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JwtSecurityToken:Key"])),
					ValidateLifetime = true
				}
			});
			app.UseMvc(routes =>
			{

			});

	        var serviceScope = app.ApplicationServices.GetRequiredService<IServiceScopeFactory>()
		        .CreateScope();
            
	        var dbContext = serviceScope.ServiceProvider.GetService<ApplicationDbContext>();

	        EnsureDatabaseCreated(dbContext);
		    StartScraper();

		}

        private void EnsureDatabaseCreated(ApplicationDbContext dbContext){}
	    
	    private  void StartScraper()
	    {
		    MemoryStorage lMemoryStorage = new MemoryStorage();
		    BackgroundJobServerOptions lOptions = new BackgroundJobServerOptions();

		    using (var server = new BackgroundJobServer(lOptions, lMemoryStorage))
		    {
			    JobStorage.Current = new MemoryStorage();
			    RecurringJob.AddOrUpdate(
				    () => new ScrapService().StartScraper(),
				    Cron.MinuteInterval(60));
		    }
	    }
    }
}
