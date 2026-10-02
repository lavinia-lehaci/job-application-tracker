using System.Net;
using System.Net.Http.Json;
using JobAppTrackerApi.DTOs;

namespace JobAppTracker.Tests;

public class AuthTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _httpClient;

    public AuthTests(ApiTestFixture factory)
    {
        _httpClient = factory.CreateClient();
    }

    /// <summary>
    /// Registering with a valid email and password should succeed and return 201
    /// </summary>
    [Fact]
    public async Task RegisterWithValidData()
    {
        var testUser = new TestUser($"test-{Guid.NewGuid()}@example.com", "Password123");
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", testUser);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    /// <summary>
    /// Registering with a malformed email should fail validation and return 400
    /// </summary>
    [Fact]
    public async Task RegisterWithInvalidData()
    {
        var testUser = new TestUser("invalid-syntax-email", "Password123");
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", testUser);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Registering the same email twice should return 409 on the second attempt
    /// </summary>
    [Fact]
    public async Task RegisterWithDuplicateEmail()
    {
        var testUser = new TestUser($"test-{Guid.NewGuid()}@example.com", "Password123");

        var first = await _httpClient.PostAsJsonAsync("/api/auth/register", testUser);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _httpClient.PostAsJsonAsync("/api/auth/register", testUser);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /// <summary>
    /// Registering with a password shorter than the minimum length should return 400
    /// </summary>
    [Fact]
    public async Task RegisterWithShortPassword()
    {
        var testUser = new TestUser($"test-{Guid.NewGuid()}@example.com", "abc");
        var response = await _httpClient.PostAsJsonAsync("/api/auth/register", testUser);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Logging in with valid credentials should return 200 and a non-empty JWT
    /// </summary>
    [Fact]
    public async Task LoginWithValidCredentials()
    {
        var testUser = new TestUser($"test-{Guid.NewGuid()}@example.com", "Password123");
        await _httpClient.PostAsJsonAsync("/api/auth/register", testUser);
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", testUser);
        
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    /// <summary>
    /// Logging in with a malformed email should fail validation and return 400
    /// </summary>
    [Fact]
    public async Task LoginWithInvalidEmail()
    {
        var testUser = new TestUser("invalid-syntax-email", "Password123");
        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", testUser);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Logging in with the correct email but wrong password should return 401
    /// </summary>
    [Fact]
    public async Task LoginWithInvalidPassword()
    {
        var testUser = new TestUser($"test-{Guid.NewGuid()}@example.com", "Password123");
        await _httpClient.PostAsJsonAsync("/api/auth/register", testUser);

        var response = await _httpClient.PostAsJsonAsync("/api/auth/login", testUser with { Password = "WrongPassword123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
