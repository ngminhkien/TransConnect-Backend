using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Xunit;
using TransConnect.Common.ExceptionHandling;
using System.Data.Common;
using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using TransConnect.Modules.Identity.Entities;
using TransConnect.Modules.Identity.Persistence;
using TransConnect.Modules.Identity.Services;
using TransConnect.Host.Configuration;

namespace TransConnect.Tests;

public class RequestCancellationTests
{
    [Theory]
    [InlineData("/locations/admin/shared", true, 0, 499)]
    [InlineData("/locations/provinces", true, 0, 499)]
    [InlineData("/locations/admin/shared", false, 0, 500)]
    [InlineData("/locations/admin/shared", true, -2, 500)]
    [InlineData("/locations/admin/shared", false, 208, 500)]
    public async Task Sql_failure_during_live_claims_is_only_cancellation_when_request_aborted(
        string path, bool disconnected, int number, int expectedStatus)
    {
        var interceptor = new PermissionQueryFailure(disconnected, number);
        var sink = new Events();
        using var baseFactory = new ApiFactory(interceptor: interceptor);
        using var factory = baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(
            services => services.AddSingleton<ILogEventSink>(sink)));
        using var client = factory.CreateClient();
        interceptor.Accessor = factory.Services.GetRequiredService<IHttpContextAccessor>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            await db.Database.EnsureCreatedAsync();
            var user = new User { Email = "cancel-test@example.com", FullName = "Cancellation test" };
            db.Users.Add(user);
            await db.SaveChangesAsync();
            var token = scope.ServiceProvider.GetRequiredService<ITokenService>()
                .GenerateAccessToken(user, [], []).AccessToken;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        sink.Items.Clear();
        using var response = await client.GetAsync(path);
        Assert.True(interceptor.Triggered);
        Assert.Equal(disconnected ? 499 : expectedStatus, (int)response.StatusCode);
        if (disconnected) Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        if (number == 0 && disconnected)
        {
            // SQLite's exception detector does not classify the injected SqlException.
            // Assert on the real HTTP/auth/error-handler logs, excluding provider logs.
            Assert.DoesNotContain(sink.Items, e => e.Level >= LogEventLevel.Error
                && (!e.Properties.TryGetValue("SourceContext", out var source)
                    || !source.ToString().Contains("Microsoft.EntityFrameworkCore")));
        }
        else Assert.Contains(sink.Items, e => e.Level >= LogEventLevel.Error && e.Exception is SqlException);
    }

    [Theory]
    [InlineData(false, 0, false)]
    [InlineData(true, 0, true)]
    [InlineData(true, -2, false)]
    [InlineData(true, 208, false)]
    public async Task Provider_cancellation_outside_auth_uses_same_rules(bool disconnected, int number, bool handled)
    {
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(disconnected) };
        var failure = SqlFailure(number);
        var middleware = new ClientRequestCancellationMiddleware(_ => throw failure,
            RequestCancellation.IsClientCancellation);
        if (handled)
        {
            await middleware.InvokeAsync(context);
            Assert.Equal(499, context.Response.StatusCode);
        }
        else Assert.Same(failure, await Assert.ThrowsAsync<SqlException>(() => middleware.InvokeAsync(context)));
    }

    [Fact]
    public async Task Error_handler_does_not_write_to_disconnected_response()
    {
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(true) };
        var body = new MemoryStream();
        context.Response.Body = body;
        var handler = new AppExceptionHandler(NullLogger<AppExceptionHandler>.Instance);
        Assert.True(await handler.TryHandleAsync(context, new InvalidOperationException("Real failure"), context.RequestAborted));
        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, body.Length);
    }

    [Fact]
    public async Task Disconnected_request_is_not_logged_as_server_failure()
    {
        var sink = new Events();
        using var logger = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        using var services = new ServiceCollection().AddLogging()
            .AddSingleton(new Serilog.Extensions.Hosting.DiagnosticContext(logger)).BuildServiceProvider();
        var app = new ApplicationBuilder(services);
        app.UseSerilogRequestLogging(options => options.Logger = logger);
        app.UseMiddleware<ClientRequestCancellationMiddleware>();
        app.Run(_ => throw new TaskCanceledException());
        var context = new DefaultHttpContext { RequestServices = services, RequestAborted = new CancellationToken(true) };
        await app.Build()(context);
        Assert.DoesNotContain(sink.Items, e => e.Level >= LogEventLevel.Error);
        Assert.Equal(499, context.Response.StatusCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Internal_cancellation_and_real_errors_still_propagate(bool disconnected)
    {
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(disconnected) };
        var failure = new InvalidOperationException("Server failure");
        var middleware = new ClientRequestCancellationMiddleware(_ => throw failure);
        Assert.Same(failure, await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context)));
        if (!disconnected)
        {
            var canceled = new ClientRequestCancellationMiddleware(_ => throw new TaskCanceledException());
            await Assert.ThrowsAsync<TaskCanceledException>(() => canceled.InvokeAsync(context));
        }
    }

    private sealed class Events : ILogEventSink
    {
        public List<LogEvent> Items { get; } = [];
        public void Emit(LogEvent logEvent) => Items.Add(logEvent);
    }

    private sealed class PermissionQueryFailure(bool disconnected, int number) : DbCommandInterceptor
    {
        public IHttpContextAccessor? Accessor { get; set; }
        public bool Triggered { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (Accessor?.HttpContext is { } context && command.CommandText.Contains("UserRoles"))
            {
                Triggered = true;
                if (disconnected) context.RequestAborted = new CancellationToken(true);
                throw SqlFailure(number);
            }
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    // SqlClient has no public exception factory. Construct the actual provider exception
    // only in tests so the Host pipeline can be exercised without a live SQL Server.
    private static SqlException SqlFailure(int number)
    {
        var errors = (SqlErrorCollection)Activator.CreateInstance(typeof(SqlErrorCollection), true)!;
        var constructor = typeof(SqlError).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .First(c => c.GetParameters().Length == 9);
        var error = (SqlError)constructor.Invoke(constructor.GetParameters().Select(p => p.Name switch
        {
            "infoNumber" => (object)number,
            "errorState" => (byte)0,
            "errorClass" => (byte)11,
            "server" => "test-server",
            "errorMessage" => number == 0 ? "Operation cancelled by user." : "SQL failure",
            "procedure" => "",
            "lineNumber" => 0,
            "win32ErrorCode" => Activator.CreateInstance(p.ParameterType),
            "exception" => null,
            "batchIndex" => -1,
            _ => throw new InvalidOperationException(p.Name)
        }).ToArray());
        typeof(SqlErrorCollection).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(errors, [error]);
        return (SqlException)typeof(SqlException).GetMethod("CreateException", BindingFlags.Static | BindingFlags.NonPublic,
            null, [typeof(SqlErrorCollection), typeof(string)], null)!.Invoke(null, [errors, "16.0"])!;
    }
}
