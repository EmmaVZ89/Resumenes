var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/salud", () => Results.Text("ok"));

app.Run();

public partial class Program { }
