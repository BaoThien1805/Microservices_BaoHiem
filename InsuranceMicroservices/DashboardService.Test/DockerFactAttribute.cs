using Xunit;
namespace DashboardService.Test;
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_DOCKER_TESTS") != "1")
            Skip = "Set RUN_DOCKER_TESTS=1 and start Docker to run Elasticsearch integration tests.";
    }
}
