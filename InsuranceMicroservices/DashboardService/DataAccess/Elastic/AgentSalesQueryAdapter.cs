using System;
using System.Collections.Generic;
using DashboardService.Domain;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace DashboardService.DataAccess.Elastic;

public class AgentSalesQueryAdapter : QueryAdapter<AgentSalesQuery, AgentSalesQueryResult, PolicyDocument>
{
    private AgentSalesQueryAdapter(AgentSalesQuery query) : base(query)
    {
    }

    public static AgentSalesQueryAdapter For(AgentSalesQuery agentSalesQuery)
    {
        return new AgentSalesQueryAdapter(agentSalesQuery);
    }

    public override SearchRequest<PolicyDocument> BuildQuery()
    {
        var filters = new List<Query>();

        if (!string.IsNullOrWhiteSpace(query.FilterByAgentLogin))
        {
            var tq = new TermQuery(new Field("agentLogin.keyword"))
            {
                Value = query.FilterByAgentLogin
            };
            filters.Add(tq);
        }

        if (!string.IsNullOrWhiteSpace(query.FilterByProductCode))
        {
            var tq = new TermQuery(new Field("productCode.keyword"))
            {
                Value = query.FilterByProductCode
            };
            filters.Add(tq);
        }

        if (query.FilterBySalesDateStart != default || query.FilterBySalesDateEnd != default)
        {
            var drq = new DateRangeQuery(new Field("from"))
            {
                Gte = query.FilterBySalesDateStart == default ? null : query.FilterBySalesDateStart,
                Lte = query.FilterBySalesDateEnd == default ? null : query.FilterBySalesDateEnd
            };
            filters.Add(drq);
        }

        if (filters.Count == 0) filters.Add(new MatchAllQuery());

        var filter = new BoolQuery
        {
            Must = filters
        };

        var termAgg = new TermsAggregation("count_by_agent") { Field = "agentLogin.keyword", Size = 10000 };
        termAgg.Aggregations = new Dictionary<string, Aggregation>
        {
            ["total_premium"] = new SumAggregation("total_premium", new Field("totalPremium"))
        };

        return new SearchRequest<PolicyDocument>
        {
            Size = 0,
            Query = filter,
            Aggregations = new Dictionary<string, Aggregation> { ["count_by_agent"] = termAgg }
        };
    }

    public override AgentSalesQueryResult ExtractResult(SearchResponse<PolicyDocument> searchResponse)
    {
        var result = new AgentSalesQueryResult();

        var countByAgent = searchResponse
            .Aggregations?
            .GetStringTerms("count_by_agent") ?? throw new InvalidOperationException("Missing count_by_agent aggregation.");

        foreach (var bucket in countByAgent.Buckets)
        {
            var sum = (bucket["total_premium"] as SumAggregate)?.Value ?? 0;

            result.AgentResult
            (
                bucket.Key.ToString(),
                new SalesResult
                (
                    bucket.DocCount,
                    Convert.ToDecimal(sum)
                )
            );
        }

        return result;
    }
}