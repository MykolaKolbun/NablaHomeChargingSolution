namespace EVHomeAPI.Ocpp;

public static class OcppCommandExtensions
{
    /// <summary>
    /// Ask the charger to re-send its StatusNotification so a station that was registered
    /// or claimed AFTER the charger last reported gets a fresh online/connector state
    /// (EVHomeAPI ignores events for stations it does not know yet).
    /// Best effort: a broker failure must not fail the calling request.
    /// </summary>
    public static async Task TryRequestStatusAsync(this IOcppCommandPublisher commands, string ocppId, ILogger logger)
    {
        try
        {
            await commands.RequestStatusAsync(new StatusRequestCommand(ocppId, null));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Status request for {OcppId} not published", ocppId);
        }
    }
}
