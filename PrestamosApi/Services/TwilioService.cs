using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace PrestamosApi.Services;

public interface ITwilioService
{
    Task<bool> SendSmsAsync(string toNumber, string message);
}

public class TwilioService : ITwilioService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TwilioService> _logger;

    public TwilioService(IConfiguration configuration, ILogger<TwilioService> logger)
    {
        _configuration = configuration;
        _logger = logger;
        
        var accountSid = _configuration["Twilio:AccountSid"];
        var authToken = _configuration["Twilio:AuthToken"];
        TwilioClient.Init(accountSid, authToken);
    }

    public async Task<bool> SendSmsAsync(string toNumber, string message)
    {
        try
        {
            var fromNumber = _configuration["Twilio:FromNumber"];
            
            var messageResource = await MessageResource.CreateAsync(
                to: new PhoneNumber(toNumber),
                from: new PhoneNumber(fromNumber),
                body: message
            );

            _logger.LogInformation("SMS enviado a {ToNumber}: SID={Sid}", toNumber, messageResource.Sid);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando SMS a {ToNumber}", toNumber);
            return false;
        }
    }
}
