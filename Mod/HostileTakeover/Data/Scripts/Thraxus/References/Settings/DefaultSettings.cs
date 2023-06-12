using System.Text;
using HostileTakeover.Common.Extensions;
using Sandbox.Game.Localization;

namespace HostileTakeover.References.Settings
{
    public static class DefaultSettings
    {
        public const string SettingsDescription =
            "\n\t\t1) Mirror Easy NPC Takeovers default is FALSE.  Setting this to TRUE will make Hostile Takeover just like Easy Npc Takeovers.  All other settings will be ignored." +
            "\n\t\t2) Use XX Group settings all default to TRUE.  Setting any to FALSE will stop that block group from being required when disowning a ship." +
            "\n\t\t3) UseHighlights defaults to TRUE.  Setting it to FALSE will disable all highlighting and bypass any other highlighting rule." +
            "\n\t\t4) HighlightAllBlocks defaults to FALSE.  Setting it to TRUE will highlight all blocks on a grid that need to be disabled for disownership.  This will disable further highlight options." +
            "\n\t\t5) HighlightAllBlocksInGroup defaults to FALSE.  Setting it to TRUE will highlight all blocks in a group that need to be disabled for disownership and disable single block highlighting." +
            "\n\t\t6) HighlightSingleNearestBlockInActiveGroup defaults to TRUE.  Setting it to FALSE will essentially disable all highlighting.  This is the final highlight option processed." +
            "\n\t";

        // User set settings
        public static bool MirrorEasyNpcTakeovers = false;
        public static bool UseWeaponGroup = true;
        public static bool UseMedicalGroup = true;
        public static bool UseTrapGroup = true;
        public static bool UseHighlights = true;
        public static bool HighlightAllBlocks = false;
        public static bool HighlightAllBlocksInGroup = false;
        public static bool HighlightSingleNearestBlockInActiveGroup = true;

        // Mod hardcoded settings
        public const double DetectionRange = 150;
        public const int HighlightDuration = Common.References.TicksPerSecond * 15;
        public const int EntityAddTickDelay = 10;
        public const int BlockAddTickDelay = 10;
        public const int GrinderTickDelay = 10;

        // Methods be props 'cuz Keen and why not
        public static bool EnableMedicalGroup => UseMedicalGroup && !MirrorEasyNpcTakeovers;

        public static bool EnableWeaponGroup => UseWeaponGroup && !MirrorEasyNpcTakeovers;

        public static bool EnableTrapGroup => UseTrapGroup && !MirrorEasyNpcTakeovers;

        public static bool EnableHighlights => UseHighlights && !MirrorEasyNpcTakeovers;

        public static UserSettings CopyTo(UserSettings userSettings)
        {
            userSettings.SettingsDescription = SettingsDescription;
            userSettings.MirrorEasyNpcTakeovers = MirrorEasyNpcTakeovers.ToString().ToLower();
            userSettings.UseWeaponGroup = UseWeaponGroup.ToString().ToLower();
            userSettings.UseMedicalGroup = UseMedicalGroup.ToString().ToLower();
            userSettings.UseTrapGroup = UseTrapGroup.ToString().ToLower();
            userSettings.UseHighlights = UseHighlights.ToString().ToLower();
            userSettings.HighlightAllBlocks = HighlightAllBlocks.ToString().ToLower();
            userSettings.HighlightAllBlocksInGroup = HighlightAllBlocksInGroup.ToString().ToLower();
            userSettings.HighlightSingleNearestBlockInActiveGroup = HighlightSingleNearestBlockInActiveGroup.ToString().ToLower();
            return userSettings;
        }

        public static StringBuilder PrintSettings(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendFormat("{0, -2}Hostile Takeover Settings", " ");
            sb.AppendLine("__________________________________________________");
            sb.AppendLine();
            sb.AppendFormat("{0, -4}[{1}] Mirror Easy NPC Takeovers", " ", MirrorEasyNpcTakeovers.ToSingleChar());
            sb.AppendLine();
            sb.AppendFormat("{0, -4}[{1}] Use Weapon Group", " ", UseWeaponGroup.ToSingleChar());
            sb.AppendLine();
            sb.AppendFormat("{0, -4}[{1}] Use Medical Group", " ", UseMedicalGroup.ToSingleChar());
            sb.AppendLine();
            sb.AppendFormat("{0, -4}[{1}] Use Trap Group", " ", UseTrapGroup.ToSingleChar());
            sb.AppendLine();
            sb.AppendFormat("{0, -4}[{1}] Use Highlights", " ", UseHighlights.ToSingleChar());
            sb.AppendLine();
            sb.AppendFormat("{0, -4}[{1}] Highlight All Blocks", " ", HighlightAllBlocks.ToSingleChar());
            sb.AppendLine();
            sb.AppendFormat("{0, -4}[{1}] Highlight All Blocks in Group", " ", HighlightAllBlocksInGroup.ToSingleChar());
            sb.AppendLine();
            sb.AppendFormat("{0, -4}[{1}] Highlight Single Nearest Block in Active Group", " ", HighlightSingleNearestBlockInActiveGroup.ToSingleChar());
            sb.AppendLine();
            sb.AppendLine();
            return sb;
        }
    }
}