using TheSpanishWay.Application.Infrastructure;
using TheSpanishWay.Infrastructure;

// Composition root. Endpoints are deliberately out of scope for the bootstrap change: the
// domain model and the state-serialisation boundary come first, and an HTTP surface designed
// before them would be designed around the wrong thing.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure();

// A request with no handler, or with two, is a deployment mistake. Finding it at startup
// means finding it at deployment time rather than on whichever request first needs it.
var problems = ApplicationRegistration.ValidateHandlers(
    typeof(ApplicationRegistration).Assembly);

if (problems.Count > 0)
{
    throw new InvalidOperationException(
        "Handler registration is invalid:" + Environment.NewLine
        + string.Join(Environment.NewLine, problems));
}

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

await app.RunAsync();

/// <summary>Exposed so the functional test suite can drive the host.</summary>
public partial class Program;
