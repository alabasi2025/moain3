using Hrms.Api.Common;
using Hrms.Api.Security;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHrmsSecurity(builder.Configuration);
builder.Services.AddControllers();
var app = builder.Build();
app.UseProblemExceptions();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
