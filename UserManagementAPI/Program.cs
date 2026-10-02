var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddSingleton<InMemoryUserStore>();

var apiToken = builder.Configuration["Authentication:ApiToken"];
if (string.IsNullOrWhiteSpace(apiToken))
{
    throw new InvalidOperationException("Configure Authentication:ApiToken before starting the API.");
}

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<BearerTokenMiddleware>(apiToken);
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/api/users", (InMemoryUserStore store) => Results.Ok(store.GetAll()))
    .WithName("GetUsers")
    .WithSummary("List users")
    .WithDescription("Returns a snapshot of all users.")
    .Produces<User[]>(StatusCodes.Status200OK);

app.MapGet("/api/users/{id:guid}", (Guid id, InMemoryUserStore store) =>
{
    var user = store.Get(id);
    return user is null ? Results.NotFound() : Results.Ok(user);
})
    .WithName("GetUserById")
    .WithSummary("Get a user by ID")
    .Produces<User>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

app.MapPost("/api/users", (UserRequest? request, InMemoryUserStore store) =>
{
    var errors = UserRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var user = store.Create(request!);
    return Results.Created($"/api/users/{user.Id}", user);
})
    .WithName("CreateUser")
    .WithSummary("Create a user")
    .Produces<User>(StatusCodes.Status201Created)
    .ProducesValidationProblem();

app.MapPut("/api/users/{id:guid}", (Guid id, UserRequest? request, InMemoryUserStore store) =>
{
    var errors = UserRequestValidator.Validate(request);
    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    var user = store.Update(id, request!);
    return user is null ? Results.NotFound() : Results.Ok(user);
})
    .WithName("UpdateUser")
    .WithSummary("Replace a user's editable details")
    .Produces<User>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound)
    .ProducesValidationProblem();

app.MapDelete("/api/users/{id:guid}", (Guid id, InMemoryUserStore store) =>
    store.Delete(id) ? Results.NoContent() : Results.NotFound())
    .WithName("DeleteUser")
    .WithSummary("Delete a user")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

app.Run();

public sealed record User(Guid Id, string Name, string Email, string Department, DateTimeOffset CreatedAt);

public sealed record UserRequest(string? Name, string? Email, string? Department);

internal static class UserRequestValidator
{
    public static Dictionary<string, string[]> Validate(UserRequest? request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request is null)
        {
            errors["request"] = ["A user payload is required."];
            return errors;
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
        {
            errors["name"] = ["Name is required and must be 100 characters or fewer."];
        }

        var email = request.Email?.Trim();
        if (email is null
            || email.Length > 254
            || !System.Net.Mail.MailAddress.TryCreate(email, out var parsedEmail)
            || !string.IsNullOrEmpty(parsedEmail.DisplayName)
            || !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
        {
            errors["email"] = ["A valid email address of 254 characters or fewer is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Department) || request.Department.Trim().Length > 100)
        {
            errors["department"] = ["Department is required and must be 100 characters or fewer."];
        }

        return errors;
    }
}

internal sealed class InMemoryUserStore
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid, User> _users = [];

    public User[] GetAll()
    {
        lock (_sync)
        {
            return _users.Values.ToArray();
        }
    }

    public User? Get(Guid id)
    {
        lock (_sync)
        {
            return _users.GetValueOrDefault(id);
        }
    }

    public User Create(UserRequest request)
    {
        var user = new User(
            Guid.NewGuid(),
            request.Name!.Trim(),
            request.Email!.Trim(),
            request.Department!.Trim(),
            DateTimeOffset.UtcNow);

        lock (_sync)
        {
            _users.Add(user.Id, user);
        }

        return user;
    }

    public User? Update(Guid id, UserRequest request)
    {
        lock (_sync)
        {
            if (!_users.TryGetValue(id, out var existing))
            {
                return null;
            }

            var updated = existing with
            {
                Name = request.Name!.Trim(),
                Email = request.Email!.Trim(),
                Department = request.Department!.Trim()
            };
            _users[id] = updated;
            return updated;
        }
    }

    public bool Delete(Guid id)
    {
        lock (_sync)
        {
            return _users.Remove(id);
        }
    }
}
