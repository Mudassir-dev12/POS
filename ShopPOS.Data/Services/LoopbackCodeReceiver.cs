using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;

namespace ShopPOS.Data.Services;

/// <summary>
/// Custom OAuth 2.0 loopback code receiver with PKCE support on a random free port.
/// Handles browser launch, timeout, cancellation, and returns friendly HTML responses.
/// </summary>
public class LoopbackCodeReceiver : ICodeReceiver
{
    private readonly Func<string, Task>? _onBrowserOpen;
    private int _port;
    private string _redirectUri = string.Empty;

    public LoopbackCodeReceiver(Func<string, Task>? onBrowserOpen = null)
    {
        _onBrowserOpen = onBrowserOpen;
        FindFreePort();
    }

    private void FindFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        _port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        _redirectUri = $"http://127.0.0.1:{_port}/authorize/";
    }

    public string RedirectUri => _redirectUri;

    public async Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(
        AuthorizationCodeRequestUrl url,
        CancellationToken taskCancellationToken)
    {
        string authorizationUrl = url.Build().AbsoluteUri;

        using var httpListener = new HttpListener();
        httpListener.Prefixes.Add($"http://127.0.0.1:{_port}/authorize/");
        httpListener.Start();

        try
        {
            // Open system browser or notify callback
            if (_onBrowserOpen != null)
            {
                await _onBrowserOpen(authorizationUrl);
            }
            else
            {
                OpenBrowser(authorizationUrl);
            }

            // Wait for authorization response with cancellation
            using var reg = taskCancellationToken.Register(() =>
            {
                try { httpListener.Stop(); } catch { }
            });

            HttpListenerContext context;
            try
            {
                context = await httpListener.GetContextAsync();
            }
            catch (Exception) when (taskCancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("Authorization was cancelled or timed out.", taskCancellationToken);
            }

            var request = context.Request;
            var response = context.Response;

            string query = request.Url?.Query ?? string.Empty;
            bool isSuccess = query.Contains("code=", StringComparison.OrdinalIgnoreCase);

            string responseHtml = isSuccess
                ? "<html><body style=\"font-family:Segoe UI,sans-serif;text-align:center;padding:40px;\"><h2 style=\"color:#2d6a4f;\">Google Drive Connected</h2><p>ShopPOS has successfully linked to your Google Drive.</p><p>You may now close this browser tab.</p></body></html>"
                : "<html><body style=\"font-family:Segoe UI,sans-serif;text-align:center;padding:40px;\"><h2 style=\"color:#c1121f;\">Connection Cancelled</h2><p>Google Drive authorization was not completed.</p><p>You may close this tab.</p></body></html>";

            byte[] buffer = Encoding.UTF8.GetBytes(responseHtml);
            response.ContentLength64 = buffer.Length;
            response.ContentType = "text/html; charset=utf-8";
            await response.OutputStream.WriteAsync(buffer, taskCancellationToken);
            response.OutputStream.Close();

            return new AuthorizationCodeResponseUrl(query);
        }
        finally
        {
            try { httpListener.Stop(); } catch { }
        }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch
        {
            // Shell execute might fail on some stripped environments
        }
    }
}

