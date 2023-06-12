using HostileTakeover.Enums;
using VRageMath;

namespace HostileTakeover.References
{
    internal static class HighlightSettings
    {
        public const long HighlightDuration = Common.References.TicksPerSecond * 60;
        public const int HighlightPulseDuration = 120;
        public const int EnabledThickness = 10;
        public const int DisabledThickness = -1;
        public static readonly Color ControlColor = Color.DodgerBlue;
        public static readonly Color MedicalColor = Color.Red;
        public static readonly Color WeaponColor = Color.Purple;
        public static readonly Color TrapColor = Color.LightSeaGreen;

        public static Color GetHighlightColor(HighlightType type)
        {
            switch (type)
            {
                case HighlightType.Control:
                    return ControlColor;
                case HighlightType.Medical:
                    return MedicalColor;
                case HighlightType.Weapon:
                    return WeaponColor;
                case HighlightType.Trap:
                    return TrapColor;
                default:
                    return Color.BlueViolet;
            }
        }
    }
}