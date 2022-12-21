using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Middleware.Middlewares
{
  public class CachingMiddleware
  {
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _memoryCache;
    private ILogger<CachingMiddleware> _logger;
    
    public CachingMiddleware(RequestDelegate next, IMemoryCache memoryCache, ILogger<CachingMiddleware> logger)
    {
      _next = next;
      _memoryCache = memoryCache;
      _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
      context.Request.EnableBuffering();
      
      var cacheKey = $"cache_{context.Request.Path.ToString()}";
      var cacheData = _memoryCache.Get<byte[]>(cacheKey);
      if (cacheData != null)
      {
        await GetResponseFromCacheAsync(context, cacheData);
      }
      else
      {
        await SetCacheAndInvokeNextAsync(context, cacheKey);
      }
    }

    private async Task GetResponseFromCacheAsync(HttpContext context, byte[] cacheData)
    {
      _logger.LogInformation("taking data from cache");
      var responseStream = context.Response.Body;
      context.Response.Body = responseStream;
      context.Response.Headers.Append("Content-Type", "text/plain; charset=utf-8");
      await context.Response.Body.WriteAsync(cacheData);
    }
    
    private async Task SetCacheAndInvokeNextAsync(HttpContext context, string key)
    {
      _logger.LogInformation("taking data from action method");
      var responseStream = context.Response.Body;
      await using var ms = new MemoryStream();
      context.Response.Body = ms;
      await _next(context);
      _memoryCache.Set(key, ms.ToArray(), TimeSpan.FromSeconds(5));
      context.Response.Body = responseStream;
      await context.Response.Body.WriteAsync(ms.ToArray());
    }
  }
  
  public static class CachingExtensions
  {
    public static IApplicationBuilder UseCaching(this IApplicationBuilder builder)
    {
      return builder.UseMiddleware<CachingMiddleware>();
    }
  }
}