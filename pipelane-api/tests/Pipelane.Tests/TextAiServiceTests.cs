using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Pipelane.Application.Ai;
using Pipelane.Domain.Enums;

using Xunit;

namespace Pipelane.Tests;

public class TextAiServiceTests
{
    [Fact]
    public async Task GenerateMessage_Trims_To_120_Words()
    {
        // Arrange
        var responseContent = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = JsonSerializer.Serialize(new
                        {
                            subject = "Test",
                            text = string.Join(' ', Enumerable.Repeat("word", 250)),
                            html = "",
                            languageDetected = "en"
                        })
                    }
                }
            },
            usage = new { prompt_tokens = 50, completion_tokens = 100 }
        });

        var handler = new StubHttpMessageHandler(responseContent);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake-openai.test/") };
        var factory = new StubHttpClientFactory(client);

        var options = Options.Create(new TextAiOptions
        {
            ApiKey = "test-key",
            Model = "test-model",
            DailyBudgetEur = 10m
        });

        var service = new TextAiService(factory, options, NullLogger<TextAiService>.Instance, new MemoryCache(new MemoryCacheOptions()));

        var command = new GenerateMessageCommand(
            Guid.NewGuid(),
            Channel.Email,
            "en",
            new AiMessageContext("Jamie", "Doe", "Acme", "COO", new[] { "manual reporting", "slow follow-up" }, "We help teams automate outreach.", "https://cal.com/demo", null));

        // Act
        var result = await service.GenerateMessageAsync(Guid.NewGuid(), command, CancellationToken.None);

        // Assert
        result.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.Should().BeLessOrEqualTo(121);
        result.Source.Should().Be(AiContentSource.OpenAi);
    }

    [Fact]
    public async Task SuggestFollowup_Adjusts_To_QuietHours()
    {
        var json = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = JsonSerializer.Serialize(new
                        {
                            scheduledAtIso = "2025-03-10T22:45:00+01:00",
                            angle = "value",
                            previewText = "Some preview"
                        })
                    }
                }
            },
            usage = new { prompt_tokens = 10, completion_tokens = 15 }
        });

        var handler = new StubHttpMessageHandler(json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake-openai.test/") };
        var factory = new StubHttpClientFactory(client);

        var options = Options.Create(new TextAiOptions
        {
            ApiKey = "test-key",
            Model = "test-model",
            DailyBudgetEur = 10m
        });

        var service = new TextAiService(factory, options, NullLogger<TextAiService>.Instance, new MemoryCache(new MemoryCacheOptions()));

        var command = new SuggestFollowupCommand(
            Channel.Email,
            "Europe/Paris",
            new DateTime(2025, 3, 9, 17, 0, 0, DateTimeKind.Utc),
            true,
            "fr",
            "Merci pour votre message",
            new AiPerformanceHints(new[] { 9, 11 }, new[] { "Sat", "Sun" }));

        var result = await service.SuggestFollowupAsync(Guid.NewGuid(), command, CancellationToken.None);

        var local = TimeZoneInfo.ConvertTimeFromUtc(result.ScheduledAtUtc, TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris"));
        local.Hour.Should().BeGreaterThanOrEqualTo(8);
        local.Hour.Should().Be(10);
        local.Minute.Should().Be(30);
        result.Angle.Should().Be(AiFollowupAngle.Value);
        result.Source.Should().Be(AiContentSource.OpenAi);
    }

    [Fact]
    public async Task Pipeline_Generate_Classify_Suggest_HappyPath()
    {
        var responses = new[]
        {
            JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = JsonSerializer.Serialize(new
                            {
                                subject = "Hello",
                                text = "Short text",
                                html = "<p>Short text</p>",
                                languageDetected = "en"
                            })
                        }
                    }
                },
                usage = new { prompt_tokens = 5, completion_tokens = 15 }
            }),
            JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = JsonSerializer.Serialize(new
                            {
                                intent = "Interested",
                                confidence = 0.92
                            })
                        }
                    }
                },
                usage = new { prompt_tokens = 2, completion_tokens = 3 }
            }),
            JsonSerializer.Serialize(new
            {
                choices = new[]
                {
                    new
                    {
                        message = new
                        {
                            content = JsonSerializer.Serialize(new
                            {
                                scheduledAtIso = DateTime.UtcNow.AddHours(30).ToString("o"),
                                angle = "reminder",
                                previewText = "Following up soon"
                            })
                        }
                    }
                },
                usage = new { prompt_tokens = 3, completion_tokens = 4 }
            })
        };

        var handler = new StubHttpMessageHandler(responses);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://fake-openai.test/") };
        var factory = new StubHttpClientFactory(client);
        var options = Options.Create(new TextAiOptions { ApiKey = "test", Model = "test", DailyBudgetEur = 10m });
        var cache = new MemoryCache(new MemoryCacheOptions());

        var service = new TextAiService(factory, options, NullLogger<TextAiService>.Instance, cache);
        var tenantId = Guid.NewGuid();

        var generate = await service.GenerateMessageAsync(tenantId, new GenerateMessageCommand(null, Channel.Email, "en", new AiMessageContext(null, null, "Acme", "Founder", null, "Test pitch", null, null)), CancellationToken.None);
        var classify = await service.ClassifyReplyAsync(tenantId, new ClassifyReplyCommand("Sounds good! Let's talk.", "en"), CancellationToken.None);
        var followup = await service.SuggestFollowupAsync(tenantId, new SuggestFollowupCommand(Channel.Email, "UTC", DateTime.UtcNow, false, "en", "Chat summary", null), CancellationToken.None);

        generate.Subject.Should().Be("Hello");
        classify.Intent.Should().Be(AiReplyIntent.Interested);
        followup.Angle.Should().Be(AiFollowupAngle.Reminder);
    }

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        private readonly HttpClient _client = client;

        public HttpClient CreateClient(string name) => _client;
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<string> _payloads;

        public StubHttpMessageHandler(params string[] payloads)
        {
            _payloads = new Queue<string>(payloads);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = _payloads.Count > 0 ? _payloads.Dequeue() : "{}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
