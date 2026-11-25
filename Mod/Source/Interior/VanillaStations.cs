using UBOAT.Game.Scene.Characters.Actions;

namespace UInterface
{
    internal static class VanillaStations
    {
        internal static void Register()
        {
            StationRegistry.AddStockStation("uinterface.helm", "Helmsman Station",
                "uinterface.helm.work", typeof(WorkAtHelmsmanStation));
            StationRegistry.AddStockStation("uinterface.trim-pump", "Trim Pump",
                "uinterface.trim-pump.toggle", typeof(SwitchPowerAction));

            StationRegistry.AddStockStation("uinterface.command", "Command Spot");
            StationRegistry.AddStockStation("uinterface.navigation", "Navigation Table");
            StationRegistry.AddStockStation("uinterface.dive-planes", "Depth Steers Station");
            StationRegistry.AddStockStation("uinterface.ballast-valves", "Valves");
            StationRegistry.AddStockStation("uinterface.diesel-engine", "Diesel Engines");
            StationRegistry.AddStockStation("uinterface.electric-engine", "Electric Engines");
            StationRegistry.AddStockStation("uinterface.compressors", "Compressor");
            StationRegistry.AddStockStation("uinterface.snorkel", "Snorkel Control Lever");
            StationRegistry.AddStockStation("uinterface.radio", "Radio Room");
            StationRegistry.AddStockStation("uinterface.listening-room", "Listening Room");
            StationRegistry.AddStockStation("uinterface.observation-periscope", "Observation Periscope");
            StationRegistry.AddStockStation("uinterface.attack-periscope", "Attack Periscope");
            StationRegistry.AddStockStation("uinterface.tdc", "Torpedo Calculator");
            StationRegistry.AddStockStation("uinterface.fire-control-boxes", "Torpedo Fire Control Boxes");
            StationRegistry.AddStockStation("uinterface.torpedo-tubes", "Torpedo Launcher");
            StationRegistry.AddStockStation("uinterface.torpedo-breeches", "Torpedo Launcher Breech Door");
            StationRegistry.AddStockStation("uinterface.torpedo-stores", "Torpedo");
            StationRegistry.AddStockStation("uinterface.ventilation", "Ventillation");
            StationRegistry.AddStockStation("uinterface.hatches", "Hatch");
            StationRegistry.AddStockStation("uinterface.ladders", "Ladder");
            StationRegistry.AddStockStation("uinterface.workshop", "Workshop");
            StationRegistry.AddStockStation("uinterface.main-storage", "Main Storage");
            StationRegistry.AddStockStation("uinterface.galley", "Kitchen");
            StationRegistry.AddStockStation("uinterface.officer-beds", "Officer's Bed");
        }
    }
}
