using MassTransit;
using MassTransit.SqlTransport.PostgreSql;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyApp.Components;
using MyApp.Components.Account;
using MyApp.Data;
using Npgsql;
using Testcontainers.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

var postgresContainer = new PostgreSqlBuilder("postgres:16-alpine")
    .WithDatabase("demodb")
    .WithUsername("postgres")
    .WithPassword("postgres")
    .Build();

await postgresContainer.StartAsync();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

var connectionString = postgresContainer.GetConnectionString();

builder.Services.AddNpgsqlDataSource(connectionString, serviceKey: "DefaultConnection");

builder.Services.AddDbContextFactory<ApplicationDbContext>((sp, options) =>
{
    var dataSource = sp.GetRequiredKeyedService<NpgsqlDataSource>("DefaultConnection");
    options.UseNpgsql(dataSource, npgsqlOptions =>
    {
        npgsqlOptions.MigrationsHistoryTable("ef_migrations_history");
    });
});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;
        options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

builder.Services.AddAuthorization(options =>
{
    // Add authorization policy and role
    options.AddPolicy("RequireAuth", policy => policy.RequireAuthenticatedUser());

    options.FallbackPolicy = options.GetPolicy("RequireAuth");
});

builder.Services.Configure<SqlTransportOptions>(sqlOptions =>
{
    sqlOptions.ConnectionString = connectionString;
    sqlOptions.Schema = "transport";
});

builder.Services.AddMassTransit((options) =>
{
    options.AddEntityFrameworkOutbox<ApplicationDbContext>(o =>
           {
               // Configures the outbox to use PG specific features
               o.UsePostgres();

               // Enables the Bus Outbox which intercepts
               // IPublishEndpoint/ISendEndpoint calls automatically
               o.UseBusOutbox();
           });

    options.AddSqlMessageScheduler();

    options.UsingPostgres((context, cfg) =>
    {
        var sqlHostSettings = new PostgresSqlHostSettings(connectionString)
        {
            ConnectionString = connectionString,
            Schema = "transport"
        };

        cfg.Host(sqlHostSettings);

        cfg.UseSqlMessageScheduler();

        cfg.UseJobSagaPartitionKeyFormatters();
    });
});
builder.Services.AddBusMetadataExplorer();
builder.Services.AddMassTransitDashboard(options =>
{
    options.BasePath = "/ops/masstransit";
    options.RequireAuthorization = true;
    options.AuthorizationPolicy = "RequireAuth";
    options.Flow.Enabled = true;
    options.Metrics.Enabled = true;
});

var app = builder.Build();

// Perform the migration into the test container
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

app.UsePathBase("/testbase");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.MapStaticAssets()
    .AllowAnonymous()
    .ShortCircuit();

// Intercept MassTransit's config request before routing & auth
app.Use(async (context, next) =>
{
    if (context.Request.Path.Value?.Equals("/.well-known/masstransit-dashboard-config.js", StringComparison.OrdinalIgnoreCase) == true)
    {
        var options = context.RequestServices
            .GetRequiredService<IOptions<MassTransitDashboardOptions>>().Value;

        // Replicate MassTransit's internal GetFormattedBasePath
        static string GetFormattedBasePath(MassTransitDashboardOptions options)
        {
            string text = options.BasePath.Trim();
            if (text == "/")
            {
                return "/";
            }

            return "/" + text.Trim('/');
        }

        var basePath = GetFormattedBasePath(options);

        context.Response.ContentType = "text/javascript";
        await context.Response.WriteAsync($"globalThis.massTransitConfig = {{\r\n    prefix: \"{basePath}\"\r\n}}");
        
        return; // Short-circuit pipeline
    }

    await next();
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.UseMassTransitDashboard();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AllowAnonymous(); ;

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();

// Ensure clean container teardown when the app stops
app.Lifetime.ApplicationStopping.Register(() => postgresContainer.DisposeAsync().AsTask().Wait());

app.Run();