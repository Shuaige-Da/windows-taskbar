using System.Net;
using System.Net.Http;

namespace DynamicIslandBar.Tests;

public class NetEaseLyricsProviderTests
{
    [Fact]
    public async Task TryGetLrcAsync_SearchesPlainSongsEndpoint()
    {
        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/search/get")
            {
                return JsonResponse(
                    """
                    {
                      "result": {
                        "songs": [
                          {
                            "id": 1331819951,
                            "name": "像鱼",
                            "duration": 285271,
                            "artists": [{ "name": "王贰浪" }]
                          }
                        ]
                      }
                    }
                    """);
            }

            if (request.RequestUri?.AbsolutePath == "/api/song/lyric")
            {
                return JsonResponse(
                    """
                    {
                      "lrc": { "lyric": "[00:12.30]我在黄昏里等风经过" },
                      "code": 200
                    }
                    """);
            }

            return JsonResponse("{}", HttpStatusCode.NotFound);
        });
        var provider = new NetEaseLyricsProvider(new HttpClient(handler));

        var lrc = await provider.TryGetLrcAsync("像鱼", "王贰浪", TimeSpan.FromMilliseconds(285271));

        Assert.Contains("[00:12.30]我在黄昏里等风经过", lrc);
        Assert.Contains(handler.RequestUris, uri => uri.AbsolutePath == "/api/search/get");
        Assert.DoesNotContain(handler.RequestUris, uri => uri.AbsolutePath == "/api/search/get/web");
    }

    [Fact]
    public async Task TryGetLrcAsync_TriesNextBestSongWhenFirstLyricIsEmpty()
    {
        var lyricRequests = 0;
        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/search/get")
            {
                return JsonResponse(
                    """
                    {
                      "result": {
                        "songs": [
                          {
                            "id": 1,
                            "name": "像鱼",
                            "duration": 285271,
                            "artists": [{ "name": "王贰浪" }]
                          },
                          {
                            "id": 2,
                            "name": "像鱼",
                            "duration": 285271,
                            "artists": [{ "name": "王贰浪" }]
                          }
                        ]
                      }
                    }
                    """);
            }

            if (request.RequestUri?.AbsolutePath == "/api/song/lyric")
            {
                lyricRequests++;
                return lyricRequests == 1
                    ? JsonResponse("""{ "lrc": { "lyric": "" }, "code": 200 }""")
                    : JsonResponse("""{ "lrc": { "lyric": "[00:18.00]下一句歌词" }, "code": 200 }""");
            }

            return JsonResponse("{}", HttpStatusCode.NotFound);
        });
        var provider = new NetEaseLyricsProvider(new HttpClient(handler));

        var lrc = await provider.TryGetLrcAsync("像鱼", "王贰浪", TimeSpan.FromMilliseconds(285271));

        Assert.Contains("[00:18.00]下一句歌词", lrc);
        Assert.Equal(2, lyricRequests);
    }

    [Fact]
    public async Task TryGetLrcAsync_MergesTranslatedLyricWithMatchingTimestamp()
    {
        var handler = new RecordingHandler(request =>
        {
            if (request.RequestUri?.AbsolutePath == "/api/search/get")
            {
                return JsonResponse(
                    """
                    {
                      "result": {
                        "songs": [
                          {
                            "id": 1422134833,
                            "name": "Moments",
                            "duration": 303984,
                            "artists": [
                              { "name": "Leo Stannard" },
                              { "name": "Kidnap" }
                            ]
                          }
                        ]
                      }
                    }
                    """);
            }

            if (request.RequestUri?.AbsolutePath == "/api/song/lyric")
            {
                return JsonResponse(
                    """
                    {
                      "lrc": {
                        "lyric": "[01:41.137]So don't try to stop me now\n[01:45.147]No don't try to stop me now"
                      },
                      "tlyric": {
                        "lyric": "[01:41.137]我已无法止步\n[01:45.147]我已无所可敌"
                      },
                      "code": 200
                    }
                    """);
            }

            return JsonResponse("{}", HttpStatusCode.NotFound);
        });
        var provider = new NetEaseLyricsProvider(new HttpClient(handler));

        var lrc = await provider.TryGetLrcAsync(
            "Moments",
            "Leo Stannard / Kidnap",
            TimeSpan.FromSeconds(304));

        Assert.Contains("[01:41.137]So don't try to stop me now / 我已无法止步", lrc);
        Assert.Contains("[01:45.147]No don't try to stop me now / 我已无所可敌", lrc);
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json)
        };
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }
}
