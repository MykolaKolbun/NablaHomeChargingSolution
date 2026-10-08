namespace OCPPServer.ChargerAdapters;

public static class ChargerAdapterFactory
{
    public static IChargerAdapter Create(string? vendor)
        => vendor?.Contains("Wall Box", StringComparison.OrdinalIgnoreCase) == true
            ? new WallboxChargerAdapter()
            : new DefaultChargerAdapter();
}
