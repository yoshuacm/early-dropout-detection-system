// Program.cs

using System.Reflection;
using System.Text;
using EDDS_API.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Serialization;
using Swashbuckle.AspNetCore.Filters;

var builder = WebApplication.CreateBuilder(args);
IServiceProvider? serviceProvider = null;

// Add services to the container.
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
        options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    });

// Add and Configure Swagger/OpenAPI Service.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "EDDS API",
        Version = "v1",
        Description = "Early Dropout Detection System",
        Contact = new OpenApiContact
        {
            Name = "Yoshua Mohan",
            Email = "yoshuacm@gmail.com",
        },
        License = new OpenApiLicense
        {
            Name = "Use under LICX",
            Url = new Uri("https://example.com/license"),
        }
    });
    
    options.AddSecurityDefinition("oauth2",
        new OpenApiSecurityScheme
        {
            Description = "Auth Header using Bearer Scheme (\"bearer {token}\")",
            In = ParameterLocation.Header,
            Name = "Authorization",
            Type = SecuritySchemeType.ApiKey
        });

    options.OperationFilter<SecurityRequirementsOperationFilter>();

    // Set the comments path for the Swagger JSON and UI.
    var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
    var commentsFileName = Assembly.GetExecutingAssembly().GetName().Name + ".XML"; 
    var commentsFile = Path.Combine(baseDirectory, commentsFileName);
    options.IncludeXmlComments(commentsFile);
    
    
});

// Add and Instantiate Authentication Service.
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options => {
    options.TokenValidationParameters =
    new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8
            .GetBytes(builder
                .Configuration
                .GetSection("AppSettings:Token")
                .Value ?? throw new InvalidOperationException())),
        ValidateIssuer = false,
        ValidateAudience = false
    };
    
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

            // Default error message
            string errorMessage = "Unauthorized Access or Invalid Token!";

            if (context.Exception is SecurityTokenExpiredException)
            {
                context.Response.Headers.Add("Token-Expired", "true");
                errorMessage = "Expired Token";
                logger.LogError($"Token expired: {context.Exception.Message}");
            }
            else if (context.Exception.GetType().Name == "SecurityTokenInvalidSignatureException" ||
                     context.Exception.GetType().Name == "SecurityTokenDecryptionFailedException" ||
                     context.Exception.GetType().Name == "SecurityTokenInvalidIssuerException")
            {
                context.Response.Headers.Add("Invalid-Token", "true");
                errorMessage = "Invalid Token";
                logger.LogError($"Invalid token: {context.Exception.Message}");
            }

            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            var result = System.Text.Json.JsonSerializer.Serialize(new
            {
                error = errorMessage
            });
            return context.Response.WriteAsync(result);
        }
    };
})
.AddCookie(CookieAuthenticationDefaults
    .AuthenticationScheme, options =>
{
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.LoginPath = "/Login";
    options.LogoutPath = "/Logout";
});

// Define Connection Strings
string connectionString;

connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new Exception("DefaultConnection not found in appsettings.json.");


// Add and Instantiate Database Service.
builder.Services.AddDbContext<DataContext>(options => 
    options.UseSqlServer(connectionString));

// Add ILogger to the container.
builder.Services.AddLogging();

// Add IAuthServices to the container.
builder.Services.AddScoped<IAuthServices, AuthServices>();

// Build
var app = builder.Build();

// Resolve ILogger instance
serviceProvider = app.Services;
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "EDDS API v1");
});
app.UseSwaggerUI();
app.UseHttpsRedirection();

// Authenticate before Authorization
app.UseAuthentication();
app.UseAuthorization();

// Map Controllers
app.MapControllers();

logger.LogInformation("Application started.");

app.Run();