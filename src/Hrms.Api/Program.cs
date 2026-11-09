using System.Text.Json.Serialization;
using Hrms.Api.Common;
using Hrms.Api.Data;
using Hrms.Api.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHrmsSecurity(builder.Configuration);
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never);
builder.Services.AddHealthChecks();

var app = builder.Build();
app.UseProblemExceptions();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

var bootstrap = Type.GetType("Hrms.Api.Features.Admin.DevBootstrap");
if (bootstrap is not null)
{
    using var scope = app.Services.CreateScope();
    var m = bootstrap.GetMethod("EnsureAdminAsync");
    if (m?.Invoke(null, new object[] { scope.ServiceProvider, app.Configuration }) is Task t) await t;
}

app.Run();

public partial class Program { }
