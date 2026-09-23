using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Components.Authorization;
using MongoDB.Bson;
using dotCheck.Application.Services;
using dotCheck.Components;
using dotCheck.Domain.Entities;
using dotCheck.Infrastructure.Files;
using dotCheck.Infrastructure.Mongo;
using dotCheck.Application.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using dotCheck.Infrastructure.Security;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "dotCheck.Auth";
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login?denied=1";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Services.AddSingleton<MongoDbContext>();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<ReceiptCryptoService>();
builder.Services.AddSingleton<HttpFingerprintService>();
builder.Services.AddSingleton<UserAsymmetricKeyService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ItemService>();
builder.Services.AddScoped<ApprovalService>();
builder.Services.AddScoped<SettlementService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<PayoutBillService>();
builder.Services.AddScoped<ReceiptService>();
builder.Services.AddScoped<InvoiceFileService>();
builder.Services.AddScoped<FinanceService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IItemRepository, ItemRepository>();
builder.Services.AddScoped<IReasonRepository, ReasonRepository>();
builder.Services.AddScoped<IPaymentBillRepository, PaymentBillRepository>();
builder.Services.AddScoped<IReceiptRepository, ReceiptRepository>();
builder.Services.AddScoped<IPayoutBillRepository, PayoutBillRepository>();

var app = builder.Build();

await app.Services.GetRequiredService<MongoDbContext>().InitializeAsync();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<AuthService>().EnsureAdminAsync();
}


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseAntiforgery();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/auth/login", async (HttpContext httpContext, AuthService authService, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(httpContext);
    var form = await httpContext.Request.ReadFormAsync();
    var userName = form["userName"].ToString();
    var password = form["password"].ToString();

    if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect("/login?error=帳號與密碼不可為空");
    }

    var user = await authService.ValidateCredentialsAsync(userName.Trim(), password);
    if (user is null)
    {
        return Results.Redirect("/login?error=帳號或密碼錯誤");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.UserName),
        new(ClaimTypes.Role, user.Role.ToString())
    };

    var identity = new ClaimsIdentity(
        claims,
        CookieAuthenticationDefaults.AuthenticationScheme);

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

    return Results.Redirect("/items/create");
}).AllowAnonymous();

app.MapPost("/auth/register", async (HttpContext httpContext, AuthService authService, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(httpContext);
    var form = await httpContext.Request.ReadFormAsync();
    var userName = form["userName"].ToString();
    var password = form["password"].ToString();

    if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
    {
        return Results.Redirect("/login?registerError=帳號與密碼不可為空");
    }

    if (password.Length < 6)
    {
        return Results.Redirect("/login?registerError=密碼至少需要6碼");
    }

    var result = await authService.RegisterAsync(userName.Trim(), password);
    if (!result.Success)
    {
        return Results.Redirect($"/login?registerError={Uri.EscapeDataString(result.ErrorMessage ?? "註冊失敗")}");
    }

    var user = result.User!;
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new(ClaimTypes.Name, user.UserName),
        new(ClaimTypes.Role, user.Role.ToString())
    };

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30) });

    return Results.Redirect("/items/create");
}).AllowAnonymous();

app.MapPost("/auth/logout", async (HttpContext httpContext, IAntiforgery antiforgery) =>
{
    await antiforgery.ValidateRequestAsync(httpContext);
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/");
}).RequireAuthorization();

app.MapGet(
    "/invoice/{id}",
    async Task<IResult> (
        string id,
        ItemService itemService,
        InvoiceFileService fileService) =>
    {
        if (!ObjectId.TryParse(id, out var objectId))
        {
            return TypedResults.NotFound();
        }

        var item = await itemService.GetByIdAsync(objectId);

        if (item is null || item.InvoiceFile is null)
        {
            return TypedResults.NotFound();
        }

        var fullPath = fileService.GetFullPath(
            item.InvoiceFile.RelativePath);

        if (fullPath is null || !File.Exists(fullPath))
        {
            return TypedResults.NotFound();
        }

        return TypedResults.PhysicalFile(
            fullPath,
            item.InvoiceFile.ContentType,
            enableRangeProcessing: true);
    })
    .RequireAuthorization();

app.MapGet("/receipts/export", async (
    HttpContext httpContext,
    string? ids,
    ReceiptService receiptService) =>
{
    if (!httpContext.User.Identity?.IsAuthenticated ?? true)
        return Results.Unauthorized();

    if (!ObjectId.TryParse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
        return Results.Unauthorized();

    var objectIds = (ids ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(x => ObjectId.TryParse(x, out _))
        .Select(ObjectId.Parse)
        .Distinct()
        .ToArray();

    if (objectIds.Length == 0)
        return Results.BadRequest("沒有可下載的收據。");

    var text = await receiptService.ExportTextAsync(userId, objectIds);
    var bytes = Encoding.UTF8.GetBytes(text);
    return Results.File(
        bytes,
        "text/plain; charset=utf-8",
        $"dotCheck-receipts-{DateTime.Now:yyyyMMddHHmmss}.txt");
}).RequireAuthorization();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
