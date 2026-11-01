using System;
using System.Threading.Tasks;
using PaymentService.Domain;

namespace PaymentService.Jobs;

public class InPaymentRegistrationJob
{
    private readonly IDataStore dataStore;
    private readonly Microsoft.Extensions.Logging.ILogger<InPaymentRegistrationJob> logger;
    private readonly BackgroundJobsConfig jobConfig;

    public InPaymentRegistrationJob(IDataStore dataStore, BackgroundJobsConfig jobConfig, Microsoft.Extensions.Logging.ILogger<InPaymentRegistrationJob> logger)
    {
        this.dataStore = dataStore;
        this.logger = logger;
        this.jobConfig = jobConfig;
    }

    public async Task Run()
    {
        logger.LogInformation("InPayment import started from {Folder}", jobConfig.InPaymentFileFolder);

        var importService = new InPaymentRegistrationService(dataStore);
        await importService.RegisterInPayments(jobConfig.InPaymentFileFolder, DateTimeOffset.Now);

        logger.LogInformation("InPayment import finished.");
    }
}