using Microsoft.Maui.Platform;
using Microsoft.Web.WebView2.Core;
using Microsoft.Extensions.Logging;

namespace DataverseDevKit.Host.Platforms.Windows;

/// <summary>
/// Custom HybridWebView handler for Windows to allow localhost dev server access
/// </summary>
public class CustomHybridWebViewHandler : Microsoft.Maui.Handlers.HybridWebViewHandler
{
    private static readonly HttpClient _httpClient = new();

    protected override void ConnectHandler(Microsoft.UI.Xaml.Controls.WebView2 platformView)
    {
        base.ConnectHandler(platformView);

        // Configure WebView2 to allow localhost access for dev servers
        platformView.CoreWebView2Initialized += (sender, args) =>
        {
            if (platformView.CoreWebView2 != null)
            {
#if DEBUG
                var coreWebView2 = platformView.CoreWebView2;
                
                // Add filter for localhost URLs (dev servers)
                coreWebView2.AddWebResourceRequestedFilter("http://localhost:*", CoreWebView2WebResourceContext.All);
                
                // Intercept and proxy localhost requests
                coreWebView2.WebResourceRequested += (s, e) =>
                {
                    var uri = e.Request.Uri;
                    
                    // Don't intercept framework files (handled by HybridWebView internally)
                    if (uri.Contains("/_framework/", StringComparison.OrdinalIgnoreCase) || 
                        uri.Contains("0.0.0.1", StringComparison.Ordinal))
                    {
                        return;
                    }
                    
                    // Get deferral for async operation
                    var deferral = e.GetDeferral();
                    
                    System.Diagnostics.Debug.WriteLine($"Intercepting request for {uri}");
                    
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            // Fetch from actual localhost
                            var response = await _httpClient.GetAsync(new Uri(uri));
                            var content = await response.Content.ReadAsByteArrayAsync();
                            var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
                            
                            System.Diagnostics.Debug.WriteLine($"Response for {uri}: Status={response.StatusCode}, ContentType={contentType}, Size={content.Length}");
                            
                            // If we got HTML when expecting JS, log it
                            if (uri.EndsWith(".js", StringComparison.OrdinalIgnoreCase) && contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
                            {
                                var htmlPreview = System.Text.Encoding.UTF8.GetString(content, 0, Math.Min(200, content.Length));
                                System.Diagnostics.Debug.WriteLine($"WARNING: Got HTML instead of JS for {uri}: {htmlPreview}");
                            }
                            
                            // Must create WebView2 response on UI thread
                            var enqueued = platformView.DispatcherQueue.TryEnqueue(() =>
                            {
                                try
                                {
                                    var stream = new System.IO.MemoryStream(content);
                                    
                                    // Build headers including CORS (use CRLF for HTTP headers)
                                    var headers = $"Content-Type: {contentType}\r\n";
                                    headers += "Access-Control-Allow-Origin: *\r\n";
                                    headers += "Access-Control-Allow-Methods: GET, POST, PUT, DELETE, PATCH, OPTIONS\r\n";
                                    headers += "Access-Control-Allow-Headers: X-Requested-With, content-type, Authorization";
                                    
                                    var webView2Response = coreWebView2.Environment.CreateWebResourceResponse(
                                        stream.AsRandomAccessStream(),
                                        (int)response.StatusCode,
                                        response.ReasonPhrase,
                                        headers);
                                    
                                    e.Response = webView2Response;
                                    System.Diagnostics.Debug.WriteLine($"Successfully proxied {uri}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Failed to create response for {uri}: {ex.Message}");
                                }
                                finally
                                {
                                    deferral.Complete();
                                }
                            });
                            
                            if (!enqueued)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to enqueue response creation for {uri}");
                                deferral.Complete();
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Failed to proxy request to {uri}: {ex.Message}");
                            deferral.Complete();
                        }
                    });
                };
#endif
            }
        };
    }
}
