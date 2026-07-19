using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BannerlordTwitch.Util;

namespace BannerlordTwitch
{
    /// <summary>
    /// Periodically builds a snapshot via TTVBridgeRegistry.SnapshotProvider (on the
    /// main thread) and, if it changed, POSTs it to the BannerlordTTV Worker.
    /// </summary>
    public class TTVBridgeService : IDisposable
    {
        private const int PushIntervalMs = 3000;

        private readonly HttpClient _http = new();
        private readonly string _url;
        private readonly CancellationTokenSource _cts = new();
        private bool _disposed;
        private string _lastSentJson;

        public TTVBridgeService(string bridgeUrl, string channelId, string secret)
        {
            _url = $"{bridgeUrl.TrimEnd('/')}/state/{channelId}";
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
            _ = RunAsync();
        }

        private async Task RunAsync()
        {
            while (!_disposed)
            {
                try
                {
                    object snapshot = null;
                    await MainThreadSync.RunWaitAsync(() =>
                    {
                        snapshot = TTVBridgeRegistry.SnapshotProvider?.Invoke();
                    });

                    if (snapshot != null)
                    {
                        var json = JsonSerializer.Serialize(snapshot);
                        if (json != _lastSentJson)
                        {
                            await PostAsync(json).ConfigureAwait(false);
                            _lastSentJson = json;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"[TTVBridge] Snapshot cycle failed: {ex.Message}");
                }

                try { await Task.Delay(PushIntervalMs, _cts.Token); }
                catch (OperationCanceledException) { break; }
            }
        }

        private async Task PostAsync(string json)
        {
            try
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _http.PostAsync(_url, content, _cts.Token).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    Log.Error($"[TTVBridge] Push rejected: {(int)response.StatusCode} {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                Log.Error($"[TTVBridge] Push failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _cts.Cancel();
            _cts.Dispose();
            _http.Dispose();
        }
    }
}
