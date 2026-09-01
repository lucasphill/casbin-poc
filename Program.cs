using Casbin;
using Casbin.Persist.Adapter.EFCore;
using casbin_poc;
using casbin_poc.Data;
using casbin_poc.Models;
using casbin_poc.Services.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddOpenApi(options => { options.AddDocumentTransformer<BearerSecuritySchemeTransformer>(); });

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

#region Configuração do CORS
var allowedOrigins = new string[]
{
    "http://localhost:5173"
};

builder.Services.AddCors(options =>
{
    options.AddPolicy("CustomCorsPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins!) // Permite origens especificadas
             .AllowAnyMethod() // Permite qualquer método (GET, POST, PUT, DELETE, etc.)
             .AllowAnyHeader(); // Permite qualquer cabeçalho
    });
});
#endregion

#region Configuração de Autenticação
var authority = builder.Configuration["Authentication:Authority"] ?? throw new InvalidOperationException("Authentication:Authority não configurado.");
var audience = builder.Configuration["Authentication:Audience"] ?? throw new InvalidOperationException("Authentication:Audience não configurado.");
var emailClaimType = builder.Configuration["Authentication:EmailClaim"] ?? throw new InvalidOperationException("Authentication:EmailClaim não configurado.");
var nameClaimType = builder.Configuration["Authentication:NameClaim"] ?? throw new InvalidOperationException("Authentication:NameClaim não configurado.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authority;
        options.Audience = audience;
        options.MapInboundClaims = false;

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var principal = context.Principal;
                var auth0Sub = principal?.FindFirstValue("sub");
                var email = principal?.FindFirstValue(emailClaimType);
                var name = principal?.FindFirstValue(nameClaimType);
                if (string.IsNullOrWhiteSpace(auth0Sub) || string.IsNullOrWhiteSpace(email))
                {
                    context.Fail("Token sem claims obrigatórios.");
                    return;
                }
                var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                // 🔍 Procura por Auth0Sub OU Email (caso tenha sido pré-cadastrado no Seed)
                var user = await db.Users
                    .SingleOrDefaultAsync(
                        item => item.Auth0Sub == auth0Sub || item.Email == email,
                        context.HttpContext.RequestAborted);
                if (user is null)
                {
                    user = new Users
                    {
                        Id = Guid.NewGuid(),
                        Auth0Sub = auth0Sub,
                        Name = name,
                        Email = email
                    };
                    db.Users.Add(user);
                }
                else
                {
                    // Vincula o Auth0Sub ao usuário pré-existente e atualiza dados
                    user.Auth0Sub = auth0Sub;
                    user.Name = name ?? user.Name;
                    user.Email = email;
                }
                await db.SaveChangesAsync(context.HttpContext.RequestAborted);
            }
        };
    });

builder.Services.AddAuthorization();
#endregion

#region Casbin
// Configuração do Casbin (Autorização)
builder.Services.AddScoped<IEnforcer>(provider =>
{
    // Usa o DbContext atual da aplicação (PostgreSQL/SQLite)
    var context = provider.GetRequiredService<AppDbContext>();
    var adapter = new EFCoreAdapter<Guid>(context);
    // Carrega as definições criadas no model.conf
    var enforcer = new Enforcer("model.conf", adapter);
    return enforcer;
});

#endregion

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITasks, TasksService>();

var app = builder.Build();

app.UseCors("CustomCorsPolicy");

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "Casbin POC";

    options
        .AddPreferredSecuritySchemes("Bearer")
        .AddHttpAuthentication("Bearer", bearer =>
        {
            bearer.Token = builder.Configuration["Authentication:Token"] ?? throw new InvalidOperationException("Authentication:Token não configurado.");
        });
});

using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    var db = services.GetRequiredService<AppDbContext>();
    var enforcer = services.GetRequiredService<IEnforcer>();
    // 1. Executa as Migrations
    await db.Database.MigrateAsync();
    // 2. Seed do Super Usuário Admin
    var adminEmail = builder.Configuration["Permissioning:BootstrapAdminEmail"] ?? "email@email.com";
    var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
    if (adminUser is null)
    {
        adminUser = new Users
        {
            Id = Guid.NewGuid(),
            Email = adminEmail,
            Name = "Super Administrador",
            Auth0Sub = string.Empty // Ficará vazio até o 1º login
        };
        db.Users.Add(adminUser);
        await db.SaveChangesAsync();
    }
    // 3. Garante a permissão total no Casbin (*, *)
    var sub = adminUser.Id.ToString();
    var hasPermission = enforcer.HasNamedPolicy("p", sub, "*", "*");
    if (!hasPermission)
    {
        await enforcer.AddNamedPolicyAsync("p", sub, "*", "*");
    }
}

//app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("health", async () =>
{
    return Results.Ok("Healthy");
})
    .WithTags("Health")
    .AllowAnonymous();

app.Urls.Add("http://*:5145");

app.Run();
