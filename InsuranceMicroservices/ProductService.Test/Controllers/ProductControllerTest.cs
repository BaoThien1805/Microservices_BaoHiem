using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using ProductService.Api.Queries.Dtos;
using ProductService.Test.TestData;
using Xunit;
using static Xunit.Assert;

namespace ProductService.Test.Controllers;

public class ProductsControllerTest : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public ProductsControllerTest(WebApplicationFactory<Program> factory)
    {
        this.factory = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Eureka:Client:Enabled"] = "false",
                ["Eureka:Client:ShouldRegisterWithEureka"] = "false",
                ["Eureka:Client:ShouldFetchRegistry"] = "false",
                ["Settings:UseInMemoryDatabase"] = "true"
            })));
    }

    [Fact]
    public async Task GetAll_ReturnsJsonResult_WithListOfProducts()
    {
        var client = factory.CreateClient();

        var response = await client.DoGetAsync<List<ProductDto>>("/api/Products");

        True(response.Count > 1);
    }


    [Fact]
    public async Task GetByCode_ReturnsJsonResult_WithOneProductOfCorrectType()
    {
        var productTravel = TestProductDtoFactory.Travel();

        var client = factory.CreateClient();

        var response = await client.DoGetAsync<ProductDto>("/api/Products/" + productTravel.Code);

        Equal(productTravel.Code, response.Code);
        Equal(productTravel.Name, response.Name);
        Equal(productTravel.Description, response.Description);
    }

    [Fact]
    public async Task DraftActivationAndDiscontinuationPersistAcrossRequests()
    {
        var client = factory.CreateClient();
        var code = "TEST_" + Guid.NewGuid().ToString("N");
        var created = await client.DoPostAsync<ProductService.Api.Commands.CreateProductDraftResult>("/api/Products", new
        {
            productDraft = new
            {
                code, name = "Test Product", image = "test.png", description = "TH7 regression", icon = "shield",
                maxNumberOfInsured = 1, covers = new object[0], questions = new object[]
                {
                    new { questionCode = "A", index = 1, text = "First", questionType = "Choice",
                        choices = new[] { new { code = "YES", label = "Yes" }, new { code = "NO", label = "No" } } },
                    new { questionCode = "B", index = 2, text = "Second", questionType = "Choice",
                        choices = new[] { new { code = "YES", label = "Yes" }, new { code = "NO", label = "No" } } }
                }
            }
        });
        True(created.Success);
        var activate = await client.DoPostAsync<ProductService.Api.Commands.ActivateProductResult>(
            "/api/Products/activate", new { productId = created.Data.ProductId });
        True(activate.Success);
        var products = await client.DoGetAsync<List<ProductDto>>("/api/Products");
        Contains(products, p => p.Code == code);
        var details = await client.DoGetAsync<ProductDto>("/api/Products/" + code.ToLowerInvariant());
        Equal(2, details.Questions.Count);
        var discontinued = await client.DoPostAsync<ProductService.Api.Commands.DiscontinueProductResult>(
            "/api/Products/discontinue", new { productId = created.Data.ProductId });
        True(discontinued.Success);
        products = await client.DoGetAsync<List<ProductDto>>("/api/Products");
        DoesNotContain(products, p => p.Code == code);
    }

    [Fact]
    public async Task UnknownProductReturnsNotFound()
    {
        var response = await factory.CreateClient().GetAsync("/api/Products/UNKNOWN");
        Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task MissingDraftReturnsBadRequest()
    {
        var response = await factory.CreateClient().PostAsync("/api/Products",
            new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json"));
        Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
}