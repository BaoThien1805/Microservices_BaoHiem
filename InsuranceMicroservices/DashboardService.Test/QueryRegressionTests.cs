using System.Text;
using DashboardService.DataAccess.Elastic;
using DashboardService.Domain;
using Elastic.Clients.Elasticsearch;
using Elastic.Transport;
using Xunit;

namespace DashboardService.Test;
public class QueryRegressionTests
{
    private static ElasticPolicyRepository Repository(string body, int statusCode = 200)
    {
        var connection = new InMemoryConnection(Encoding.UTF8.GetBytes(body), statusCode,
            headers: new Dictionary<string, IEnumerable<string>> { ["x-elastic-product"] = new[] { "Elasticsearch" } });
        var settings = new ElasticsearchClientSettings(connection).DefaultIndex("policy_lab_stats");
        return new ElasticPolicyRepository(new ElasticsearchClient(settings));
    }

    [Fact]
    public void ProductBucketsProduceTotals()
    {
        var repo = Repository("""
            {"took":1,"timed_out":false,"_shards":{"total":1,"successful":1,"skipped":0,"failed":0},
            "hits":{"total":{"value":5,"relation":"eq"},"max_score":null,"hits":[]},
            "aggregations":{"sterms#count_by_product":{"buckets":[
              {"key":"TRI","doc_count":3,"sum#total_premium":{"value":300}},
              {"key":"HSI","doc_count":2,"sum#total_premium":{"value":150}}]}}}
            """);
        var result = repo.GetTotalSales(new TotalSalesQuery(null, default, default));
        Assert.Equal(5, result.Total.PoliciesCount);
        Assert.Equal(450m, result.Total.PremiumAmount);
        Assert.Equal(150m, result.PerProductTotal["HSI"].PremiumAmount);
    }

    [Fact]
    public void EmptyAgentBucketsReturnEmptySales()
    {
        var repo = Repository("""
            {"took":1,"timed_out":false,"_shards":{"total":1,"successful":1,"skipped":0,"failed":0},
            "hits":{"total":{"value":0,"relation":"eq"},"max_score":null,"hits":[]},
            "aggregations":{"sterms#count_by_agent":{"buckets":[]}}}
            """);
        Assert.Empty(repo.GetAgentSales(new AgentSalesQuery(null, null, default, default)).PerAgentTotal);
    }

    [Fact]
    public void FailedElasticsearchRequestIsNotReportedAsZeroSales()
    {
        var repo = Repository("""{"error":"unavailable","status":503}""", 503);
        Assert.Throws<InvalidOperationException>(() => repo.GetTotalSales(new TotalSalesQuery(null, default, default)));
    }

    [Fact]
    public void ReplacingProductBucketRecalculatesTotals()
    {
        var result = new TotalSalesQueryResult().ProductResult("TRI", new SalesResult(3, 300m))
            .ProductResult("HSI", new SalesResult(2, 150m)).ProductResult("TRI", new SalesResult(1, 50m));
        Assert.Equal(3, result.Total.PoliciesCount);
        Assert.Equal(200m, result.Total.PremiumAmount);
    }
}
