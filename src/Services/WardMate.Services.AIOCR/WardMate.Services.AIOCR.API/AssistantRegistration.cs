using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using WardMate.Services.AIOCR.Application;
using WardMate.Services.AIOCR.Infrastructure;
using WardMate.SharedKernel.Web;

namespace WardMate.Services.AIOCR.API;

public static class AssistantRegistration
{
    public static void AddAssistant(this WebApplicationBuilder builder)
    {
        var services = builder.Services; var config = builder.Configuration;
        services.AddExceptionHandler<AssistantExceptionHandler>();
        services.AddWardMateBrowserCors(config, builder.Environment);
        var key = config["Jwt:Key"] ?? "";
        if (Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("Cần khóa Jwt:Key ít nhất 32 byte, giống IAM.");
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ValidateIssuer = true, ValidIssuer = config["Jwt:Issuer"] ?? "wardmate",
                ValidateAudience = true, ValidAudience = config["Jwt:Audience"] ?? "wardmate-client",
                ValidateLifetime = true, RequireSignedTokens = true, RequireExpirationTime = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ClockSkew = TimeSpan.Zero, NameClaimType = "sub", RoleClaimType = "role"
            };
            options.Events = new JwtBearerEvents { OnTokenValidated = context =>
            {
                if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var id) || id == Guid.Empty) context.Fail("Invalid user id.");
                return Task.CompletedTask;
            } };
        });
        services.AddAuthorization();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("assistant", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("sub")?.Value ?? "anonymous", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
        services.AddDbContext<ChatDbContext>(o => o.UseNpgsql(config.GetConnectionString("AiOcrDatabase")
            ?? "Host=localhost;Database=wardmate_aiocr_db;Username=wardmate_aiocr").UseSnakeCaseNamingConvention());
        services.AddScoped<IChatService, ChatService>();
        // Daily Gemini limits and ai_request_logs (no prompt/answer content is stored).
        services.AddHttpContextAccessor();
        services.AddScoped<IAiUsage, AiUsageService>();
        services.AddHttpClient<GeminiAssistant>(h => { h.Timeout = TimeSpan.FromSeconds(90); h.MaxResponseContentBufferSize = 1024 * 1024; })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<ICccdExtractionService>(s => s.GetRequiredService<GeminiAssistant>());
        services.AddScoped<IDocumentPreValidationService>(s => s.GetRequiredService<GeminiAssistant>());
        services.AddScoped<IAssistantModel>(s => s.GetRequiredService<GeminiAssistant>());
        services.AddHttpClient<IAuthorizedDocumentSource, AuthorizedDocumentSource>(h => { h.Timeout = TimeSpan.FromSeconds(60); h.MaxResponseContentBufferSize = 2 * 1024 * 1024; })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        // Knowledge base (TASK-28): sources, import and chunking; embeddings/vector search come with TASK-29.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IKnowledgeBase, WardMate.Services.AIOCR.Infrastructure.Knowledge.KnowledgeBaseService>();
        services.AddSingleton<IKnowledgePdfReader, WardMate.Services.AIOCR.Infrastructure.Knowledge.KnowledgePdfReader>();
        services.AddHttpClient<IProcedureCatalogSource, WardMate.Services.AIOCR.Infrastructure.Knowledge.ProcedureCatalogSource>(h => { h.Timeout = TimeSpan.FromSeconds(15); h.MaxResponseContentBufferSize = 2 * 1024 * 1024; })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHttpClient("knowledge-iam", h => { h.Timeout = TimeSpan.FromSeconds(5); h.MaxResponseContentBufferSize = 64 * 1024; })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        // TASK-29: embeddings, in-memory vector search and the indexing worker (all inert while Knowledge:EmbeddingEnabled=false).
        services.AddSingleton<WardMate.Services.AIOCR.Infrastructure.Knowledge.KnowledgeSettings>();
        services.AddSingleton<WardMate.Services.AIOCR.Infrastructure.Knowledge.KnowledgeVectorCache>();
        services.AddScoped<IKnowledgeRetriever, WardMate.Services.AIOCR.Infrastructure.Knowledge.KnowledgeRetriever>();
        services.AddHttpClient<IEmbeddingProvider, WardMate.Services.AIOCR.Infrastructure.Knowledge.GeminiEmbeddingProvider>(h => { h.Timeout = TimeSpan.FromSeconds(60); h.MaxResponseContentBufferSize = 16 * 1024 * 1024; })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddHostedService<WardMate.Services.AIOCR.Infrastructure.Knowledge.KnowledgeIndexingWorker>();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(o =>
        {
            o.SwaggerDoc("v1", new() { Title = "WardMate AIOCR", Version = "v1" });
            o.AddSecurityDefinition("Bearer", new() { Type = SecuritySchemeType.Http, Scheme = "bearer" });
            o.AddSecurityRequirement(new() { [new OpenApiSecurityScheme { Reference = new() { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [] });
        });
    }
}
public sealed class AssistantExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception error, CancellationToken ct)
    {
        if (!context.Request.Path.StartsWithSegments("/api/v1/ai") && !context.Request.Path.StartsWithSegments("/api/v1/ai-ocr")) return false;
        // EF Core wraps transient Npgsql errors (lock_not_available 55P03, lost connections) in InvalidOperationException
        // even without a retry strategy; classify the database error itself, not the generic wrapper.
        if (error is InvalidOperationException { InnerException: NpgsqlException inner }) error = inner;
        var failure = error switch
        {
            AiFailure e => e,
            PostgresException { SqlState: "55P03" } => new AiFailure(409, "chat.busy", "Phiên đang trả lời; chờ hoàn tất trước khi gửi tiếp."),
            NpgsqlException or DbUpdateException when context.Request.Path.StartsWithSegments("/api/v1/ai/knowledge") =>
                new AiFailure(503, "knowledge.database_unavailable", "Database AIOCR chưa sẵn sàng; kiểm tra kết nối và migration Knowledge Base."),
            NpgsqlException or DbUpdateException => new AiFailure(503, "chat.database_unavailable", "Database chat chưa sẵn sàng; kiểm tra kết nối và migration."),
            HttpRequestException => new AiFailure(503, "ai.dependency_unavailable", "Không kết nối được dịch vụ phụ thuộc."),
            OperationCanceledException when !context.RequestAborted.IsCancellationRequested => new AiFailure(504, "ai.timeout", "Dịch vụ AI hoặc tài liệu phản hồi quá lâu."),
            JsonException or KeyNotFoundException or InvalidOperationException => new AiFailure(502, "ai.invalid_response", "Dịch vụ trả dữ liệu không hợp lệ."),
            _ => null
        };
        if (failure is null) return false;
        context.Response.StatusCode = failure.Status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails { Status = failure.Status, Title = failure.Message,
            Instance = context.Request.Path, Extensions = { ["code"] = failure.Code, ["traceId"] = context.TraceIdentifier } },
            options: null, contentType: "application/problem+json", cancellationToken: ct);
        return true;
    }
}
