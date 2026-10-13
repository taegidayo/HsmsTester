using HsmsTester.Hsms.Define;
using HsmsTester.Hsms.Struct;
using HsmsTester.Manager;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Org.BouncyCastle.Ocsp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
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

            builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

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

        private static void MapEndpoints(WebApplication app)
        {
            app.MapPost("/api/sendMsg",(HsmsJson jsonData) =>
            {

                HsmsManager.Instance.SendMsg(jsonData);      // Header 작성 + 전송 + 로그

                return Results.Ok(new { result = "ok" });
            });

            app.MapPost("/api/defineMsgSave", (List<HsmsMsgDefine> define) =>
            {
                if (HsmsManager.Instance.SaveMsgDefines(define) == false)
                {
                    return Results.Problem("msgDefine.json 저장 실패");
                }
                return Results.Ok(new { result = "ok" });
            });

            // 현재 메시지 정의 전달 (defineMsgSave로 저장한 것과 같은 형식)
            app.MapGet("/api/defineMsgLoad", () => Results.Ok(HsmsManager.Instance.HsmsMsgDefines));

            // 조건 응답 정의 전달 / 저장 (실행폴더/conditionalDefine.json)
            app.MapGet("/api/conditionalDefineLoad", () => Results.Ok(HsmsManager.Instance.HsmsConditionalDefines));

            app.MapPost("/api/conditionalDefineSave", (List<HsmsConditionalDefine> defines) =>
            {
                if (HsmsManager.Instance.SaveConditionalDefines(defines) == false)
                {
                    return Results.Problem("conditionalDefine.json 저장 실패");
                }
                return Results.Ok(new { result = "ok" });
            });

            // 현재 Hsms 설정 전달
            app.MapGet("/api/configLoad", () => Results.Ok(HsmsManager.Instance.Config.ToData()));

            // Hsms 설정 변경 (보낸 항목만 변경, cfg 파일에도 저장됨)
            app.MapPost("/api/configSave", (HsmsConfigData data) =>
            {
                var errors = HsmsManager.Instance.Config.Apply(data);
                if (errors.Count > 0)
                {
                    return Results.BadRequest(new { result = "fail", errors });
                }

                HsmsManager.Instance.Start();

                return Results.Ok(HsmsManager.Instance.Config.ToData());
            });

            app.MapPost("/api/setT3TimeoutFlag", (bool check) =>
            {
                HsmsManager.Instance.T3TimeoutCheckFlag = check;
                return Results.Ok(new { result = "ok" });
            });

            app.MapPost("/api/tcpChange", (bool onOff) =>
            {
                if (onOff == true)
                {
                    HsmsManager.Instance.Start();
                }
                else
                {
                    HsmsManager.Instance.Dispose();
                }
            });

            // 처음 보는 메시지 (정의와 매칭되지 않은 받은 메시지)
            app.MapGet("/api/undefinedMsgLoad", () => Results.Ok(HsmsManager.Instance.UndefinedMsgs));
            app.MapPost("/api/undefinedMsgRemove", (int id) =>
            {
                HsmsManager.Instance.RemoveUndefined(id);
                return Results.Ok(new { result = "ok" });
            });
            app.MapPost("/api/undefinedMsgClear", () =>
            {
                HsmsManager.Instance.ClearUndefined();
                return Results.Ok(new { result = "ok" });
            });

            app.MapGet("/api/status", () =>
            {
                var manager = HsmsManager.Instance;
                return Results.Ok(new { IsConnect = manager.IsConnected(), manager.IsSelected });
            });


            //app.MapPost("/api/echo", (EchoRequest req) =>
            //    Results.Ok(new { echo = req.Text, length = req.Text.Length }));

            // SSE 스트림: 웹이 여기에 연결해서 로그를 실시간으로 받음
            // 로그 기록 지우기 (Clear 버튼)
            app.MapPost("/api/logs/clear", () =>
            {
                LogBroadcaster.Instance.Clear();
                return Results.Ok(new { result = "ok" });
            });

            app.MapGet("/api/logs/stream", async (HttpContext ctx) =>
            {
                ctx.Response.Headers.ContentType = "text/event-stream; charset=utf-8;";
                ctx.Response.Headers.CacheControl = "no-cache";

                var (id, reader, history) = LogBroadcaster.Instance.Subscribe();
                try
                {
                    // 접속 직후 최근 로그부터 전송
                    foreach (var line in history)
                        await ctx.Response.WriteAsync($"{line}\n\n");
                    await ctx.Response.Body.FlushAsync();

                    // 새 로그를 보내고, 15초 동안 로그가 없으면 연결 확인용 heartbeat(":")를 보냄
                    // (웹은 일정 시간 아무것도 못 받으면 끊긴 것으로 보고 다시 연결)
                    Task<bool>? waiting = null;
                    while (true)
                    {
                        waiting ??= reader.WaitToReadAsync(ctx.RequestAborted).AsTask();
                        var heartbeat = Task.Delay(TimeSpan.FromSeconds(15), ctx.RequestAborted);
                        if (await Task.WhenAny(waiting, heartbeat) != waiting)
                        {
                            await ctx.Response.WriteAsync(":\n\n", ctx.RequestAborted);
                            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                            continue;
                        }
                        if (await waiting == false) break;      // 구독 종료
                        waiting = null;

                        while (reader.TryRead(out var line) == true)
                            await ctx.Response.WriteAsync($"{line}\n\n", ctx.RequestAborted);
                        await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                    }
                }
                catch (OperationCanceledException) { }   // 브라우저 탭을 닫으면 여기로 옴
                finally
                {
                    LogBroadcaster.Instance.Unsubscribe(id);
                }
            });


        }

        public void Dispose()
        {
             _app?.DisposeAsync();
        }
    }

    public record EchoRequest(string Text);
    public record CommandRequest(string Name, string? Arg);

}
