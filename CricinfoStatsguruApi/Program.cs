using CricinfoStatsguruApi.Models;
using CricinfoStatsguruApi.Options;
using CricinfoStatsguruApi.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<CacheOptions>(
    builder.Configuration.GetSection(CacheOptions.SectionName));

builder.Services.Configure<StatsguruOptions>(
    builder.Configuration.GetSection(StatsguruOptions.SectionName));

builder.Services.AddSingleton<IFilterTemplateRepository, InMemoryFilterTemplateRepository>();
builder.Services.AddSingleton<IStatsCacheRepository, SqliteStatsCacheRepository>();
builder.Services.AddSingleton<IStatsguruUrlBuilder, StatsguruUrlBuilder>();
builder.Services.AddSingleton<IStatsguruHtmlParser, StatsguruHtmlParser>();
builder.Services.AddSingleton<IPlayerLookupCache, FilePlayerLookupCache>();
builder.Services.AddScoped<ICacheWarmupService, CacheWarmupService>();
builder.Services.AddScoped<IStatsService, StatsService>();
builder.Services.AddHttpClient<IStatsguruClient, StatsguruClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<StatsguruOptions>>().Value;
    client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Cricinfo Statsguru API v1");
    options.DocumentTitle = "Cricinfo Statsguru API";
    options.RoutePrefix = "swagger";
});

app.MapGet("/templates", (IFilterTemplateRepository repository) =>
    Results.Ok(new TemplateCatalogResponse
    {
        Count = repository.GetAll().Count,
        Templates = repository.GetAll()
    }));

app.MapGet("/templates/{key}", (string key, IFilterTemplateRepository repository) =>
{
    var template = repository.GetByKey(key);
    return template is null
        ? Results.NotFound(new { error = $"Unknown template key '{key}'." })
        : Results.Ok(template);
});

app.MapPost("/cache/preload", async (
    string templateKey,
    int? limit,
    ICacheWarmupService cacheWarmupService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await cacheWarmupService.WarmLeaderboardAsync(templateKey, limit ?? 100, cancellationToken);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (HttpRequestException exception)
    {
        return Results.Json(
            new
            {
                error = "Unable to fetch Statsguru data",
                detail = exception.Message
            },
            statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/rankings/hammyviz", async (
    string templateKey,
    int? limit,
    string? hostGroup,
    ICacheWarmupService cacheWarmupService,
    CancellationToken cancellationToken) =>
{
    try
    {
        var result = await cacheWarmupService.GetHammyVizRankingsAsync(templateKey, limit ?? 10, hostGroup, cancellationToken);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (ArgumentOutOfRangeException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (HttpRequestException exception)
    {
        return Results.Json(
            new
            {
                error = "Unable to fetch Statsguru data",
                detail = exception.Message
            },
            statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/players/search", async (
    string playerName,
    string templateKey,
    string? host,
    string? opposition,
    string? team,
    string? span,
    string? bowlerType,
    string? hostGroup,
    IStatsService statsService,
    CancellationToken cancellationToken) =>
{
    var query = new StatsQuery
    {
        PlayerName = playerName,
        TemplateKey = templateKey,
        Host = host,
        Opposition = opposition,
        Team = team,
        Span = span,
        BowlerType = bowlerType,
        HostGroup = hostGroup
    };

    try
    {
        var result = await statsService.SearchPlayersAsync(query, cancellationToken);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (HttpRequestException exception)
    {
        return Results.Json(
            new
            {
                error = "Unable to fetch Statsguru data",
                detail = exception.Message
            },
            statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/stats/player", async (
    string playerName,
    string templateKey,
    int? selectedPlayerId,
    string? host,
    string? opposition,
    string? team,
    string? span,
    string? bowlerType,
    string? hostGroup,
    IStatsService statsService,
    CancellationToken cancellationToken) =>
{
    var query = new StatsQuery
    {
        PlayerName = playerName,
        TemplateKey = templateKey,
        SelectedPlayerId = selectedPlayerId,
        Host = host,
        Opposition = opposition,
        Team = team,
        Span = span,
        BowlerType = bowlerType,
        HostGroup = hostGroup
    };

    try
    {
        var result = await statsService.GetPlayerStatsAsync(query, cancellationToken);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (PlayerSearchMissException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (AmbiguousPlayerSearchException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (HttpRequestException exception)
    {
        return Results.Json(
            new
            {
                error = "Unable to fetch Statsguru data",
                detail = exception.Message
            },
            statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/profiles/player", async (
    string playerName,
    string templateKey,
    int? selectedPlayerId,
    string? host,
    string? opposition,
    string? team,
    string? span,
    string? bowlerType,
    string? hostGroup,
    IStatsService statsService,
    CancellationToken cancellationToken) =>
{
    var query = new StatsQuery
    {
        PlayerName = playerName,
        TemplateKey = templateKey,
        SelectedPlayerId = selectedPlayerId,
        Host = host,
        Opposition = opposition,
        Team = team,
        Span = span,
        BowlerType = bowlerType,
        HostGroup = hostGroup
    };

    try
    {
        var result = await statsService.GetPlayerProfileAsync(query, cancellationToken);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (PlayerSearchMissException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (AmbiguousPlayerSearchException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (HttpRequestException exception)
    {
        return Results.Json(
            new
            {
                error = "Unable to fetch Statsguru data",
                detail = exception.Message
            },
            statusCode: StatusCodes.Status502BadGateway);
    }
});

app.MapGet("/compare/players", async (
    string playerAName,
    string playerBName,
    string templateKey,
    int? playerASelectedPlayerId,
    int? playerBSelectedPlayerId,
    string? host,
    string? opposition,
    string? team,
    string? span,
    string? bowlerType,
    string? hostGroup,
    IStatsService statsService,
    CancellationToken cancellationToken) =>
{
    var playerAQuery = new StatsQuery
    {
        PlayerName = playerAName,
        TemplateKey = templateKey,
        SelectedPlayerId = playerASelectedPlayerId,
        Host = host,
        Opposition = opposition,
        Team = team,
        Span = span,
        BowlerType = bowlerType,
        HostGroup = hostGroup
    };

    var playerBQuery = new StatsQuery
    {
        PlayerName = playerBName,
        TemplateKey = templateKey,
        SelectedPlayerId = playerBSelectedPlayerId,
        Host = host,
        Opposition = opposition,
        Team = team,
        Span = span,
        BowlerType = bowlerType,
        HostGroup = hostGroup
    };

    try
    {
        var result = await statsService.ComparePlayersAsync(playerAQuery, playerBQuery, cancellationToken);
        return Results.Ok(result);
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (PlayerSearchMissException exception)
    {
        return Results.NotFound(new { error = exception.Message });
    }
    catch (AmbiguousPlayerSearchException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
    catch (HttpRequestException exception)
    {
        return Results.Json(
            new
            {
                error = "Unable to fetch Statsguru data",
                detail = exception.Message
            },
            statusCode: StatusCodes.Status502BadGateway);
    }
});

app.Run();
