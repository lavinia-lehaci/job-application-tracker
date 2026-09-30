using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using JobAppTrackerApi.DTOs;

namespace JobAppTracker.Tests;

public class CrudTests : IClassFixture<ApiTestFixture>
{
    private readonly HttpClient _httpClient;

    public CrudTests(ApiTestFixture factory)
    {
        _httpClient = factory.CreateClient();
    }

    // --- Helpers ---
    private static string jobTitleOne = "jobTitle1";
    private static string jobTitleTwo = "jobTitle2";
    private static string jobTitleThree = "jobTitle3"; 
    private static string jobCompanyOne = "jobCompany1";
    private static string jobCompanyTwo = "jobCompany2";
    private static string jobCompanyThree = "jobCompany3";

    // Registers a fresh user, logs in, and returns a usable JWT
    private async Task<string> CreateAuthenticatedUserAsync()
    {
        var user = new TestUser($"test-{Guid.NewGuid()}@example.com", "Password123");
        await _httpClient.PostAsJsonAsync("/api/auth/register", user);
        var loginResponse = await _httpClient.PostAsJsonAsync("/api/auth/login", user);
        var body = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.Token;
    }

    // Builds a request with the given token attached
    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string token)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return request;
    }

    private static HttpRequestMessage AuthedJsonRequest<T>(HttpMethod method, string url, string token, T body)
    {
        var request = AuthedRequest(method, url, token);
        request.Content = JsonContent.Create(body);
        return request;
    }

    private static CreateRequest ValidApplication() => new()
    {
        Title = jobTitleOne,
        Company = jobCompanyOne,
        Description = "some job description",
        Link = "https://example.com",
        AppliedDate = DateOnly.FromDateTime(DateTime.UtcNow)
    };

    private async Task<JobAppResponse> CreateApplicationAsync(string token, string title, string company, 
        DateOnly? appliedDate = null)
    {
        var request = new CreateRequest
        {
            Title = title,
            Company = company,
            AppliedDate = appliedDate ?? DateOnly.FromDateTime(DateTime.UtcNow)
        };

        var response = await _httpClient.SendAsync(
            AuthedJsonRequest(HttpMethod.Post, "/api/applications", token, request));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JobAppResponse>())!;
    }

    // Pulls just the titles out of the paginated list response, in server order
    private static async Task<List<string>> GetTitlesAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("applications")
            .EnumerateArray()
            .Select(item => item.GetProperty("title").GetString()!)
            .ToList();
    }

    private static async Task<JsonElement> GetBodyAsync(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<string> InitApplicationsAsync()
    {
        var token = await CreateAuthenticatedUserAsync();

        await CreateApplicationAsync(token, jobTitleOne, jobCompanyOne, new DateOnly(2026, 1, 10));
        await CreateApplicationAsync(token, jobTitleTwo, jobCompanyTwo, new DateOnly(2026, 3, 5));
        await CreateApplicationAsync(token, jobTitleThree, jobCompanyOne, new DateOnly(2026, 2, 20));

        return token;
    }

    // --- Create ---

    /// <summary>
    /// Creating an application with valid data should return 201 and the created item
    /// </summary>
    [Fact]
    public async Task Create_WithValidData()
    {
        var token = await CreateAuthenticatedUserAsync();

        var response = await _httpClient.SendAsync(
            AuthedJsonRequest(HttpMethod.Post, "/api/applications", token, ValidApplication()));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JobAppResponse>();
        Assert.NotNull(body);
        Assert.Equal(jobTitleOne, body!.Title);
        Assert.Equal(jobCompanyOne, body.Company);
    }

    /// <summary>
    /// Creating an application without a token should return 401
    /// </summary>
    [Fact]
    public async Task Create_WithoutToken()
    {
        var response = await _httpClient.PostAsJsonAsync("/api/applications", ValidApplication());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Creating an application with a missing required field should return 400
    /// </summary>
    [Fact]
    public async Task Create_WithMissingTitle()
    {
        var token = await CreateAuthenticatedUserAsync();
        var invalid = ValidApplication();
        invalid.Title = "";

        var response = await _httpClient.SendAsync(
            AuthedJsonRequest(HttpMethod.Post, "/api/applications", token, invalid));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Creating an application with an invalid URL in Link should return 400
    /// </summary>
    [Fact]
    public async Task Create_WithInvalidLink()
    {
        var token = await CreateAuthenticatedUserAsync();
        var invalid = ValidApplication();
        invalid.Link = "not-a-url";

        var response = await _httpClient.SendAsync(
            AuthedJsonRequest(HttpMethod.Post, "/api/applications", token, invalid));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Creating an application without an AppliedDate should default it to current day
    /// </summary>
    [Fact]
    public async Task Create_WithoutAppliedDate()
    {
        var token = await CreateAuthenticatedUserAsync();
        var request = new CreateRequest { Title = jobTitleOne, Company = jobCompanyOne };

        var response = await _httpClient.SendAsync(
            AuthedJsonRequest(HttpMethod.Post, "/api/applications", token, request));

        var body = await response.Content.ReadFromJsonAsync<JobAppResponse>();
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow), body!.AppliedDate);
    }

    // --- Get (list) ---

    /// <summary>
    /// A user with no applications should get an empty list
    /// </summary>
    [Fact]
    public async Task Get_WithNoApplications()
    {
        var token = await CreateAuthenticatedUserAsync();

        var response = await _httpClient.SendAsync(AuthedRequest(HttpMethod.Get, "/api/applications", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var titles = await GetTitlesAsync(response);
        Assert.Empty(titles);
    }

    /// <summary>
    /// A user should never see another user's applications in their list
    /// </summary>
    [Fact]
    public async Task Get_OnlyReturnsCallingUsersOwnApplications()
    {
        var userAToken = await CreateAuthenticatedUserAsync();
        var userBToken = await CreateAuthenticatedUserAsync();

        await CreateApplicationAsync(userAToken, jobTitleOne, jobCompanyOne);

        var response = await _httpClient.SendAsync(AuthedRequest(HttpMethod.Get, "/api/applications", userBToken));
        var titles = await GetTitlesAsync(response);

        Assert.Empty(titles);
    }

    // --- Get (list): Filtering ---

    /// <summary>
    /// Filtering by company should return only applications from that company
    /// </summary>
    [Fact]
    public async Task Get_FilteredByCompany()
    {
        var token = await InitApplicationsAsync();

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/applications?company=jobCompany1", token));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var titles = await GetTitlesAsync(response);

        Assert.Equal(2, titles.Count);
        Assert.Contains(jobTitleOne, titles);
        Assert.Contains(jobTitleThree, titles);
        Assert.DoesNotContain(jobTitleTwo, titles);
    }

    /// <summary>
    /// Filtering by status should return only applications with that exact status
    /// </summary>
    [Fact]
    public async Task Get_FilteredByStatus()
    {
        var token = await CreateAuthenticatedUserAsync();

        await CreateApplicationAsync(token, jobTitleOne, jobCompanyOne);
        var app2 = await CreateApplicationAsync(token, jobTitleTwo, jobCompanyTwo);

        var filterResponse = await _httpClient.SendAsync(
           AuthedRequest(HttpMethod.Get, "/api/applications?status=Interview", token));
        Assert.Equal(HttpStatusCode.OK, filterResponse.StatusCode);
        var titles = await GetTitlesAsync(filterResponse);
        Assert.Empty(titles);

        var statusResponse = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Put, $"/api/applications/{app2.Id}/status?status=interview", token));
        Assert.Equal(HttpStatusCode.NoContent, statusResponse.StatusCode);

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/applications?status=Interview", token));
        titles = await GetTitlesAsync(response);

        Assert.Single(titles);
        Assert.Contains(jobTitleTwo, titles);
    }

    /// <summary>
    /// An invalid status filter value should return 400
    /// </summary>
    [Fact]
    public async Task Get_WithInvalidStatusFilter()
    {
        var token = await CreateAuthenticatedUserAsync();

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/applications?status=NotARealStatus", token));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Filtering by appliedAfter should exclude applications applied on or before that date
    /// </summary>
    [Fact]
    public async Task Get_FilteredByAppliedAfter_ExcludesEarlierApplications()
    {
        var token = await InitApplicationsAsync();

        // job title 1: 2026-01-10, job title 3: 2026-02-20, job title 2: 2026-03-05
        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/applications?appliedAfter=2026-02-01", token));
        var titles = await GetTitlesAsync(response);

        Assert.Equal(2, titles.Count);
        Assert.Contains(jobTitleThree, titles);
        Assert.Contains(jobTitleTwo, titles);
        Assert.DoesNotContain(jobTitleOne, titles);
    }

    /// <summary>
    /// Filtering by appliedBefore should exclude applications applied on or after that date
    /// </summary>
    [Fact]
    public async Task Get_FilteredByAppliedBefore_ExcludesLaterApplications()
    {
        var token = await InitApplicationsAsync();

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/applications?appliedBefore=2026-02-01", token));
        var titles = await GetTitlesAsync(response);

        Assert.Single(titles);
        Assert.Contains(jobTitleOne, titles);
    }

    /// <summary>
    /// Combining filters should apply both conditions together
    /// </summary>
    [Fact]
    public async Task Get_WithMultipleFilters()
    {
        var token = await CreateAuthenticatedUserAsync();

        var app1 = await CreateApplicationAsync(token, jobTitleOne, jobCompanyOne);
        await CreateApplicationAsync(token, jobTitleTwo, jobCompanyOne);
        await CreateApplicationAsync(token, jobTitleThree, jobCompanyTwo);

        await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Put, $"/api/applications/{app1.Id}/status?status=interview", token));

        // Only job title 1 matches both company=jobCompany1 AND status=Interview
        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/applications?company=jobCompany1&status=Interview", token));
        var titles = await GetTitlesAsync(response);

        Assert.Single(titles);
        Assert.Contains(jobTitleOne, titles);
    }

    // --- Get (list): Sorting ---

    /// <summary>
    /// Sorting by company ascending should return applications in alphabetical company order
    /// </summary>
    [Fact]
    public async Task Get_SortedByCompanyAscending()
    {
        var token = await CreateAuthenticatedUserAsync();

        await CreateApplicationAsync(token, jobTitleOne, "zetaCompany");
        await CreateApplicationAsync(token, jobTitleTwo, "alphaCompany");
        await CreateApplicationAsync(token, jobTitleThree, "midCompany");

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/applications?sortBy=company&sortDir=asc", token));
        var titles = await GetTitlesAsync(response);

        Assert.Equal(new[] { jobTitleTwo, jobTitleThree, jobTitleOne }, titles);
    }

    /// <summary>
    /// With no sort parameters given, results should default to newest applied date first
    /// </summary>
    [Fact]
    public async Task Get_DefaultSort()
    {
        var token = await InitApplicationsAsync();

        var response = await _httpClient.SendAsync(AuthedRequest(HttpMethod.Get, "/api/applications", token));
        var titles = await GetTitlesAsync(response);

        // Newest applied date first: job title 2 (Mar 5) > job title 3 (Feb 20) > job title 1 (Jan 10)
        Assert.Equal(new[] { jobTitleTwo, jobTitleThree, jobTitleOne }, titles);
    }

    /// <summary>
    /// Pagination should split results across pages and report accurate counts
    /// </summary>
    [Fact]
    public async Task Get_WithPagination()
    {
        var token = await InitApplicationsAsync();

        var page1Response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/applications?pageSize=2&page=1&sortBy=company&sortDir=asc", token));
        var page1Body = await GetBodyAsync(page1Response);

        Assert.Equal(2, page1Body.GetProperty("applications").GetArrayLength());
        Assert.Equal(3, page1Body.GetProperty("appCount").GetInt32());
        Assert.Equal(2, page1Body.GetProperty("totalPages").GetInt32());
        Assert.Equal(1, page1Body.GetProperty("page").GetInt32());

        var page2Response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/applications?pageSize=2&page=2&sortBy=company&sortDir=asc", token));
        var page2Body = await GetBodyAsync(page2Response);

        Assert.Single(page2Body.GetProperty("applications").EnumerateArray());
    }

    /// <summary>
    /// Requesting a page size above the allowed maximum should be clamped
    /// </summary>
    [Fact]
    public async Task Get_WithOversizedPageSize()
    {
        var token = await CreateAuthenticatedUserAsync();

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/applications?pageSize=99999", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Requesting page 0 or a negative page should be clamped to page 1
    /// </summary>
    [Fact]
    public async Task Get_WithZeroOrNegativePage()
    {
        var token = await InitApplicationsAsync();

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/applications?page=0", token));
        var body = await GetBodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, body.GetProperty("page").GetInt32());
    }

    // --- GetById ---

    /// <summary>
    /// Fetching an existing application by id should return it
    /// </summary>
    [Fact]
    public async Task GetById()
    {
        var token = await CreateAuthenticatedUserAsync();
        var created = await CreateApplicationAsync(token, jobTitleOne, jobCompanyOne);

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, $"/api/applications/{created.Id}", token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JobAppResponse>();
        Assert.Equal(jobTitleOne, body!.Title);
    }

    /// <summary>
    /// Fetching an application id that doesn't exist should return 404
    /// </summary>
    [Fact]
    public async Task GetById_WithNonExistentId()
    {
        var token = await CreateAuthenticatedUserAsync();

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/applications/999999", token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Fetching another user's application by id should return 404
    /// </summary>
    [Fact]
    public async Task GetById_WithAnotherUsersApplication()
    {
        var ownerToken = await CreateAuthenticatedUserAsync();
        var intruderToken = await CreateAuthenticatedUserAsync();

        var created = await CreateApplicationAsync(ownerToken, jobTitleOne, jobCompanyOne);

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, $"/api/applications/{created.Id}", intruderToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Update ---

    /// <summary>
    /// Updating one field should change only that field 
    /// </summary>
    [Fact]
    public async Task UpdateById_WithPartialData()
    {
        var token = await CreateAuthenticatedUserAsync();
        var created = await CreateApplicationAsync(token, jobTitleOne, jobCompanyOne);

        var update = new UpdateRequest { Title = "jobtitle1-updated" };

        var response = await _httpClient.SendAsync(
            AuthedJsonRequest(HttpMethod.Put, $"/api/applications/{created.Id}", token, update));
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var refetched = await _httpClient.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/applications/{created.Id}", token));
        var refetchedBody = await refetched.Content.ReadFromJsonAsync<JobAppResponse>();

        Assert.Equal("jobtitle1-updated", refetchedBody!.Title);
        Assert.Equal(jobCompanyOne, refetchedBody.Company);  
    }

    /// <summary>
    /// Updating a non-existent application should return 404
    /// </summary>
    [Fact]
    public async Task UpdateById_WithNonExistentId()
    {
        var token = await CreateAuthenticatedUserAsync();
        var update = new UpdateRequest { Title = "jobtitle-new" };

        var response = await _httpClient.SendAsync(
            AuthedJsonRequest(HttpMethod.Put, "/api/applications/999999", token, update));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// A user should not be able to update another user's application
    /// </summary>
    [Fact]
    public async Task UpdateById_WithAnotherUsersApplication()
    {
        var ownerToken = await CreateAuthenticatedUserAsync();
        var intruderToken = await CreateAuthenticatedUserAsync();

        var created = await CreateApplicationAsync(ownerToken, jobTitleOne, jobCompanyOne);
        var update = new UpdateRequest { Title = "hijacked" };

        var response = await _httpClient.SendAsync(
            AuthedJsonRequest(HttpMethod.Put, $"/api/applications/{created.Id}", intruderToken, update));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Sending an update with a blank title should fail validation and return 400
    /// </summary>
    [Fact]
    public async Task UpdateById_WithBlankTitle()
    {
        var token = await CreateAuthenticatedUserAsync();
        var created = await CreateApplicationAsync(token, jobTitleOne, jobCompanyOne);

        var update = new UpdateRequest { Title = "" };

        var response = await _httpClient.SendAsync(
            AuthedJsonRequest(HttpMethod.Put, $"/api/applications/{created.Id}", token, update));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Setting a valid status should succeed 
    /// </summary>
    [Fact]
    public async Task UpdateStatus_WithValidStatus()
    {
        var token = await CreateAuthenticatedUserAsync();
        var created = await CreateApplicationAsync(token, jobTitleOne, jobCompanyOne);

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Put, $"/api/applications/{created.Id}/status?status=interview", token));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var refetched = await _httpClient.SendAsync(AuthedRequest(HttpMethod.Get, $"/api/applications/{created.Id}", token));
        var body = await refetched.Content.ReadFromJsonAsync<JobAppResponse>();
        Assert.Equal("Interview", body!.Status);
    }

    /// <summary>
    /// A status value that isn't a defined enum member should return 400
    /// </summary>
    [Fact]
    public async Task UpdateStatus_WithUndefinedEnumValue()
    {
        var token = await CreateAuthenticatedUserAsync();
        var created = await CreateApplicationAsync(token, jobTitleOne, jobCompanyOne);

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Put, $"/api/applications/{created.Id}/status?status=99", token));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// A user should not be able to change another user's application status
    /// </summary>
    [Fact]
    public async Task UpdateStatus_WithAnotherUsersApplication()
    {
        var ownerToken = await CreateAuthenticatedUserAsync();
        var intruderToken = await CreateAuthenticatedUserAsync();

        var created = await CreateApplicationAsync(ownerToken, jobTitleOne, jobCompanyOne);

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Put, $"/api/applications/{created.Id}/status?status=interview", intruderToken));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // --- Delete single ---

    /// <summary>
    /// Deleting an existing application should return 204
    /// </summary>
    [Fact]
    public async Task DeleteById_WithOwnApplication()
    {
        var token = await CreateAuthenticatedUserAsync();
        var created = await CreateApplicationAsync(token, jobTitleOne, jobCompanyOne);

        var deleteResponse = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Delete, $"/api/applications/{created.Id}", token));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, $"/api/applications/{created.Id}", token));
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    /// <summary>
    /// Deleting a non-existent application should return 404
    /// </summary>
    [Fact]
    public async Task DeleteById_WithNonExistentId()
    {
        var token = await CreateAuthenticatedUserAsync();

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Delete, "/api/applications/999999", token));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// A user should not be able to delete another user's application
    /// </summary>
    [Fact]
    public async Task DeleteById_WithAnotherUsersApplication()
    {
        var ownerToken = await CreateAuthenticatedUserAsync();
        var intruderToken = await CreateAuthenticatedUserAsync();

        var created = await CreateApplicationAsync(ownerToken, jobTitleOne, jobCompanyOne);

        var response = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Delete, $"/api/applications/{created.Id}", intruderToken));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var getResponse = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, $"/api/applications/{created.Id}", ownerToken));
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    // --- Delete all ---

    /// <summary>
    /// Deleting all applications should remove only the calling user's own applications
    /// </summary>
    [Fact]
    public async Task DeleteAll_OnlyRemovesCallingUsersApplications()
    {
        var userAToken = await CreateAuthenticatedUserAsync();
        var userBToken = await CreateAuthenticatedUserAsync();

        await CreateApplicationAsync(userAToken, jobTitleOne, jobCompanyOne);
        var userBApp = await CreateApplicationAsync(userBToken, jobTitleTwo, jobCompanyTwo);

        var deleteResponse = await _httpClient.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/applications", userAToken));
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _httpClient.SendAsync(
            AuthedRequest(HttpMethod.Get, $"/api/applications/{userBApp.Id}", userBToken));
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    /// <summary>
    /// Deleting all applications when there are none should still return 204
    /// </summary>
    [Fact]
    public async Task DeleteAll_WithNoApplications_ReturnsNoContent()
    {
        var token = await CreateAuthenticatedUserAsync();

        var response = await _httpClient.SendAsync(AuthedRequest(HttpMethod.Delete, "/api/applications", token));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}