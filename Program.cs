using EkycInquiry.Models;
using EkycInquiry.Models.ViewModel;
using EkycInquiry.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Security.Policy;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var dbBuilderOperations = new NpgsqlDataSourceBuilder(builder.Configuration.GetConnectionString("WidStaging"));
dbBuilderOperations.EnableDynamicJson();
dbBuilderOperations.UseJsonNet();

var dbBuilder = dbBuilderOperations.Build();

builder.Services.AddDbContext<WidContext>(x => {
    #if DEBUG
    x.LogTo(Console.WriteLine);
    #endif
    x.UseNpgsql(dbBuilder);
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.ConsentCookie.IsEssential = true;
    options.CheckConsentNeeded = context => false;
    options.MinimumSameSitePolicy = SameSiteMode.None;
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.IsEssential = true;
        options.Cookie.SameSite = Microsoft.AspNetCore.Http.SameSiteMode.Strict;
        options.Cookie.HttpOnly = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
        options.AccessDeniedPath = "/Login/Index";
        options.LoginPath = "/Login/Index";
    });

builder.Services.AddHttpClient<EkycClient>(client =>
{
    client.BaseAddress = new Uri("https://079.jo/");
});

builder.Services.Configure<EkycClientOptions>(builder.Configuration.GetSection("EkycSettings"));


builder.Services.AddScoped<EkycClient>();


var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
//    app.UseHsts();
//}


app.UseHttpsRedirection();
app.UseDeveloperExceptionPage();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
