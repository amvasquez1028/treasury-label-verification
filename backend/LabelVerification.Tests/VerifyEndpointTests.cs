using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using LabelVerification.Core.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LabelVerification.Tests;

[Collection(nameof(OcrSequentialCollection))]
public class VerifyEndpointTests : IClassFixture<LabelVerifyWebFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly LabelVerifyWebFactory _factory;

    public VerifyEndpointTests(LabelVerifyWebFactory factory) => _factory = factory;

    [Fact]
    public async Task Verify_accepts_camelCase_labelPresentation_enum_in_multipart_expected()
    {
        var client = await CreateAuthenticatedClientAsync();
        var manifest = await LoadManifestAsync();
        var mismatch = manifest.First(i => i.File == "01-mismatch-act-of-treason.png");
        Assert.NotNull(mismatch.ExpectedLabelFields);

        using var content = BuildVerifyMultipart(
            SamplePath(mismatch.File),
            mismatch.ExpectedLabelFields);

        var response = await client.PostAsync("/api/v1/verify", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<VerificationResultDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.RawOcrText));
        Assert.Equal("fail", body.OverallStatus, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verify_uses_submitted_expected_values_not_cola_cache()
    {
        var client = await CreateAuthenticatedClientAsync();
        var manifest = await LoadManifestAsync();
        var jack = manifest.First(i => i.File == "05-odp-jack-daniels-old-no7.png");
        Assert.NotNull(jack.ExpectedLabelFields);

        var wrongExpected = jack.ExpectedLabelFields with { BrandName = "INTENTIONALLY WRONG BRAND" };
        using var content = BuildVerifyMultipart(SamplePath(jack.File), wrongExpected);

        var response = await client.PostAsync("/api/v1/verify", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<VerificationResultDto>(JsonOptions);
        Assert.NotNull(body);
        var brand = body.Fields.FirstOrDefault(f => f.FieldName == "BrandName");
        Assert.NotNull(brand);
        Assert.False(brand.IsMatch);
        Assert.Equal("INTENTIONALLY WRONG BRAND", brand.ExpectedValue);
    }

    [Fact]
    public async Task Verify_jack_odp_passes_against_manifest_application_values()
    {
        var client = await CreateAuthenticatedClientAsync();
        var manifest = await LoadManifestAsync();
        var jack = manifest.First(i => i.File == "05-odp-jack-daniels-old-no7.png");
        Assert.NotNull(jack.ExpectedLabelFields);

        var expected = jack.ExpectedLabelFields with { LabelPresentation = LabelPresentation.FullLabel };
        using var content = BuildVerifyMultipart(SamplePath(jack.File), expected);

        var response = await client.PostAsync("/api/v1/verify", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<VerificationResultDto>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal("pass", body.OverallStatus, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Approval_post_rejects_missing_token_body()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/approve", new { token = "", action = "approve" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        var login = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = TestCredentials.DemoAgentEmail, password = TestCredentials.DemoAgentPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return client;
    }

    private static MultipartFormDataContent BuildVerifyMultipart(string imagePath, ExpectedLabelFields expected)
    {
        var content = new MultipartFormDataContent();
        var imageBytes = File.ReadAllBytes(imagePath);
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(imageContent, "image", Path.GetFileName(imagePath));
        content.Add(new StringContent(JsonSerializer.Serialize(expected, JsonOptions)), "expected");
        return content;
    }

    private static string SamplePath(string fileName) =>
        Path.Combine(LabelVerifyWebFactory.FindPlanRepoRoot(), "public", "samples", fileName);

    private static async Task<List<ManifestItem>> LoadManifestAsync()
    {
        var path = Path.Combine(LabelVerifyWebFactory.FindPlanRepoRoot(), "public", "samples", "manifest.json");
        var json = await File.ReadAllTextAsync(path);
        return JsonSerializer.Deserialize<List<ManifestItem>>(json, JsonOptions)
            ?? throw new InvalidOperationException("Invalid sample manifest.");
    }

    private sealed record ManifestItem
    {
        public required string File { get; init; }
        public ExpectedLabelFields? ExpectedLabelFields { get; init; }
    }

    private sealed record VerificationResultDto
    {
        public string OverallStatus { get; init; } = "";
        public bool IsVerified { get; init; }
        public string RawOcrText { get; init; } = "";
        public long ProcessingTimeMs { get; init; }
        public List<FieldDto> Fields { get; init; } = [];
    }

    private sealed record FieldDto
    {
        public string FieldName { get; init; } = "";
        public bool IsMatch { get; init; }
        public string? ExpectedValue { get; init; }
    }
}
