using System.Net;
using System.Text;

namespace Voyager.Common.Proxy.Server.IntegrationTests;

[Collection("Server")]
public class SaleServiceIntegrationTests
{
	private readonly HttpClient _client;

	public SaleServiceIntegrationTests(ServerTestFixture fixture)
	{
		_client = fixture.Client;
	}

	[Fact]
	public async Task HandleCallback_ReturnsTextHtmlContentType()
	{
		var content = new StringContent("\"test-data\"", Encoding.UTF8, "application/json");

		var response = await _client.PostAsync("/sale/callback", content);

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
	}

	[Fact]
	public async Task HandleCallback_ReturnsRawStringWithoutJsonQuotes()
	{
		var content = new StringContent("\"test-data\"", Encoding.UTF8, "application/json");

		var response = await _client.PostAsync("/sale/callback", content);

		var body = await response.Content.ReadAsStringAsync();

		Assert.Equal("OK", body);
	}

	[Fact]
	public async Task GetStatus_WithTextPlainContentType_ReturnsRawString()
	{
		var response = await _client.GetAsync("/get-status");

		Assert.Equal(HttpStatusCode.OK, response.StatusCode);
		Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);

		var body = await response.Content.ReadAsStringAsync();
		Assert.Equal("healthy", body);
	}
}
