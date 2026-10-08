using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EVHomeAPI.DTOs;
using EVHomeAPI.Tests.Infrastructure;

namespace EVHomeAPI.Tests;

public class AuthTests
{
    [Fact]
    public async Task Register_then_login_returns_token_and_profile_works()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var reg = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Mykola", "Me@Test.Local", "password123"));
        Assert.Equal(HttpStatusCode.OK, reg.StatusCode);

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("me@test.local", "password123"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.False(string.IsNullOrEmpty(auth!.Token));
        Assert.Equal("me@test.local", auth.Email);   // normalized to lower-case

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        var profile = await client.GetFromJsonAsync<ProfileResponse>("/api/auth/profile");
        Assert.Equal("Mykola", profile!.Name);
    }

    [Fact]
    public async Task Register_duplicate_email_case_insensitive_returns_409()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("A", "dup@test.local", "password123"));

        var res = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("B", "DUP@test.local", "password123"));
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Register_short_password_returns_400()
    {
        using var factory = new ApiFactory();
        var res = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new RegisterRequest("A", "a@test.local", "short"));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Login_wrong_password_returns_401()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("A", "a@test.local", "password123"));

        var res = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest("a@test.local", "wrong-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Profile_without_token_returns_401()
    {
        using var factory = new ApiFactory();
        var res = await factory.CreateClient().GetAsync("/api/auth/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Change_password_requires_current_password()
    {
        using var factory = new ApiFactory();
        var client = await factory.CreateUserClientAsync("pw@test.local");

        var bad = await client.PutAsJsonAsync("/api/auth/profile",
            new UpdateProfileRequest("Test User", "pw@test.local", "newpassword1", "wrong"));
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var ok = await client.PutAsJsonAsync("/api/auth/profile",
            new UpdateProfileRequest("Test User", "pw@test.local", "newpassword1", "password123"));
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var login = await factory.CreateClient().PostAsJsonAsync("/api/auth/login", new LoginRequest("pw@test.local", "newpassword1"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }
}
