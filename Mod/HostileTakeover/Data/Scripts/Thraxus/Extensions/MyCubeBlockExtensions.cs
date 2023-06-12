using HostileTakeover.Enums;
using HostileTakeover.References.Settings;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using SpaceEngineers.Game.ModAPI;
using VRage.Utils;

namespace HostileTakeover.Extensions
{
    internal static class MyCubeBlockExtensions
    {
        public static bool IsValidForTakeover(this MyCubeBlock block) => block.IsFunctional && block is IMyTerminalBlock;

        public static HighlightType Classify(this MyCubeBlock block)
        {
            var controller = block as IMyShipController;
            if (controller != null && controller.CanControlShip)
            {
                return HighlightType.Control;
            }

            if (DefaultSettings.EnableMedicalGroup || DefaultSettings.MirrorEasyNpcTakeovers)
            {
                var cryo = block as IMyCryoChamber;
                if (cryo != null)
                {
                    return HighlightType.Medical;
                }
            }

            if (DefaultSettings.EnableMedicalGroup)
            {
                var medical = block as IMyMedicalRoom;
                if (medical != null)
                {
                    return HighlightType.Medical;
                }

                if (block.BlockDefinition.Id.TypeId == typeof(MyObjectBuilder_SurvivalKit))
                {
                    return HighlightType.Medical;
                }
            }

            if (DefaultSettings.EnableWeaponGroup)
            {
                var weapon = block as IMyLargeTurretBase;
                if (weapon != null)
                {
                    return HighlightType.Weapon;
                }

                var sorter = block as MyConveyorSorter;
                if (sorter != null && !sorter.BlockDefinition.Context.IsBaseGame)
                {
                    return HighlightType.Weapon;
                }

                var upgrade = block as IMyUpgradeModule;
                if (upgrade != null && block.BlockDefinition.Id.SubtypeId ==
                    MyStringHash.GetOrCompute("BotSpawner"))
                {
                    return HighlightType.Weapon;
                }
            }

            if (DefaultSettings.EnableTrapGroup)
            {
                var warhead = block as IMyWarhead;
                if (warhead != null)
                {
                    return HighlightType.Trap;
                }
            }

            return HighlightType.None;
        }
    }
}