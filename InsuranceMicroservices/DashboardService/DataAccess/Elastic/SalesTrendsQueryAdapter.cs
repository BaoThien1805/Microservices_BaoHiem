using System;
using System.Collections.Generic;
using DashboardService.Domain;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Clients.Elasticsearch.QueryDsl;

namespace DashboardService.DataAccess.Elastic;

public class SalesTrendsQueryAdapter : QueryAdapter<SalesTrendsQuery, SalesTrendsResult, PolicyDocument>
{
    private SalesTrendsQueryAdapter(SalesTrendsQuery query) : base(query)
    {
    }

    public static SalesTrendsQueryAdapter For(SalesTrendsQuery query)
    {
        return new SalesTrendsQueryAdapter(query);
    }

    public override SearchRequest<PolicyDocument> BuildQuery()
    {
        var filters = new List<Query>();

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

        var histogram = new DateHistogramAggregation("sales")
        {
            Field = "from", CalendarInterval = query.AggregationUnit.ToCalendarInterval()
        };
        histogram.Aggregations = new Dictionary<string, Aggregation>
        {
            ["total_premium"] = new SumAggregation("total_premium", new Field("totalPremium"))
        };

        return new SearchRequest<PolicyDocument>
        {
            Size = 0,
            Query = filter,
            Aggregations = new Dictionary<string, Aggregation> { ["sales"] = histogram }
        };
    }

    public override SalesTrendsResult ExtractResult(SearchResponse<PolicyDocument> searchResponse)
    {
        var result = new SalesTrendsResult();

        var histogram = searchResponse
            .Aggregations?
            .GetDateHistogram("sales") ?? throw new InvalidOperationException("Missing sales aggregation.");

        foreach (var bucket in histogram.Buckets)
        {
            var periodStart = DateTime.Parse(bucket.KeyAsString ?? throw new InvalidOperationException("Missing histogram date."), System.Globalization.CultureInfo.InvariantCulture);
            var totalPremium = Convert.ToDecimal((bucket["total_premium"] as SumAggregate)?.Value ?? 0);

            result.PeriodResult
            (
                new PeriodSales
                (
                    periodStart,
                    periodStart.ToShortDateString(),
                    new SalesResult
                    (
                        bucket.DocCount,
                        totalPremium
                    )
                )
            );
        }

        return result;
    }
}