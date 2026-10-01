using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace HsmsTester.API
{
    //HsmsTest를 웹으로도 가능하게 하기 위해 Rest API를 관리하는 클래스
    public class ApiListener : IDisposable
    {

        public static ApiListener Instance { get; } = new ApiListener();

        private ApiListener() 
        {
            LibraryController.Instance.RegisterDisposable(this);
        }

        private WebApplication? _app;

        public async Task StartAsync(int port = 5000)
        {
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

            _app = builder.Build();
            MapEndpoints(_app);

            await _app.StartAsync();   // 백그라운드에서 실행, 바로 반환됨
        }

        public async Task StopAsync()
        {
            if (_app is null) return;
            await _app.StopAsync();
            await _app.DisposeAsync();
            _app = null;
        }

        // 테스트용으로 구현한 API
        private static void MapEndpoints(WebApplication app)
        {
            app.MapGet("/api/ping", () => Results.Ok(new { message = "pong" }));

            app.MapGet("/api/status", () => Results.Ok(new { status = "running" }));

            //app.MapPost("/api/echo", (EchoRequest req) =>
            //    Results.Ok(new { echo = req.Text, length = req.Text.Length }));

            // SSE 스트림: 웹이 여기에 연결해서 로그를 실시간으로 받음
            app.MapGet("/api/logs/stream", async (HttpContext ctx) =>
            {
                ctx.Response.Headers.ContentType = "text/event-stream; charset=utf-8;";
                ctx.Response.Headers.CacheControl = "no-cache";

                var (id, reader, history) = LogBroadcaster.Instance.Subscribe();
                try
                {
                    // 접속 직후 최근 로그부터 전송
                    foreach (var line in history)
                        await ctx.Response.WriteAsync($"data: {line}\n\n");
                    await ctx.Response.Body.FlushAsync();

                    await foreach (var line in reader.ReadAllAsync(ctx.RequestAborted))
                    {
                        await ctx.Response.WriteAsync($"data: {line}\n\n", ctx.RequestAborted);
                        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                    }
                }
                catch (OperationCanceledException) { }   // 브라우저 탭을 닫으면 여기로 옴
                finally
                {
                    LogBroadcaster.Instance.Unsubscribe(id);
                }
            });

            // 기존 echo에 로그 추가 예시
            app.MapPost("/api/echo", (EchoRequest req) =>
            {
                LogBroadcaster.Instance.Write($"echo 요청: {req.Text}");
                return Results.Ok(new { echo = req.Text, length = req.Text.Length });
            });

        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }

    public record EchoRequest(string Text);
    public record CommandRequest(string Name, string? Arg);

}
