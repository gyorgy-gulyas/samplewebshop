using SrvKit.Net;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddGrpc();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapRestControllers();
app.MapGrpcControllers();


app.Run();
