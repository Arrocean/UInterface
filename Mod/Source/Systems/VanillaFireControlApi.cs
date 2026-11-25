using UBOAT.Game.Scene.Items;

namespace UInterface
{
    public static class VanillaFireControlApi
    {
        public const int ApiVersion = 1;

        public static bool TryGetTdc(out TorpedoCalculator calculator)
        {
            return ComponentApi.TryGetUnique(out calculator);
        }

        public static bool TryGetFireControlBoxes(out TorpedoFireControlBoxes boxes)
        {
            return ComponentApi.TryGetUnique(out boxes);
        }
    }
}
