using System;
using System.Collections.Generic;
using System.Linq;
using HostileTakeover.Common.Generics;
using HostileTakeover.Common.Interfaces;
using HostileTakeover.Enums;
using HostileTakeover.Models;
using HostileTakeover.References;
using HostileTakeover.References.Settings;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI.Weapons;
using VRage.ModAPI;
using VRageMath;

namespace HostileTakeover.Controllers
{
    internal class HighlightController : ILog
    {
        // Constructors
        public HighlightController(Controller constructController)
        {
            _constructController = constructController;
            HighlightedBlockPool = _constructController.HighlightPool;
        }

        // Fields
        private readonly Controller _constructController;
        internal readonly ObjectPool<HighlightedBlock> HighlightedBlockPool;

        // Props & Expressions

        // Events
        public event Action<string, string> OnWriteToLog;


        // Event Invokers
        public void WriteGeneral(string caller, string message) => OnWriteToLog?.Invoke(caller, message);

        public void TriggerHighlights(IMyAngleGrinder grinder)
        {
            WriteGeneral(nameof(TriggerHighlights), $"Triggering [{_constructController.ImportantBlocks[HighlightType.Control].Count:D3}] highlights for [{_constructController.MasterGridId:D18}] against [{grinder.EntityId:D18}]");
            if (!DefaultSettings.UseHighlights) return;
            if (DefaultSettings.HighlightAllBlocks)
            {
                EnableAllHighlights(grinder);
                return;
            }

            if (DefaultSettings.HighlightAllBlocksInGroup)
            {
                EnableSingleGroupHighlights(grinder);
                return;
            }

            EnableSingleBlockHighlight(grinder);
        }

        private HighlightedBlock GetHighlightedBlock(long playerId, HighlightType type, MyCubeBlock block)
        {
            HighlightedBlock hlb = HighlightedBlockPool.Get();
            hlb.TargetPlayer = playerId;
            hlb.Color = HighlightSettings.GetHighlightColor(type);
            hlb.Block = block;
            return hlb;
        }

        private void EnableAllHighlights(IMyAngleGrinder grinder)
        {
            foreach (var kvp in _constructController.ImportantBlocks)
            {
                WriteGeneral(nameof(EnableAllHighlights), $"Enabling [{_constructController.ImportantBlocks[kvp.Key].Count:D2}] highlights...");
                if (!DefaultSettings.EnableMedicalGroup && kvp.Key == HighlightType.Medical) continue;
                if (!DefaultSettings.EnableWeaponGroup && kvp.Key == HighlightType.Weapon) continue;
                if (!DefaultSettings.EnableTrapGroup && kvp.Key == HighlightType.Trap) continue;
                foreach (var block in kvp.Value)
                {
                    HighlightedBlock hlb = GetHighlightedBlock(grinder.OwnerIdentityId, kvp.Key, block);
                    EnableHighlight(hlb);
                    WriteGeneral(nameof(EnableAllHighlights), $"HLB: {hlb}");
                }
            }
        }

        private void EnableSingleGroupHighlights(IMyAngleGrinder grinder)
        {
            if (_constructController.ImportantBlocks[HighlightType.Control].Count > 0)
            {
                foreach (var block in _constructController.ImportantBlocks[HighlightType.Control])
                {
                    HighlightedBlock hlb = GetHighlightedBlock(grinder.OwnerIdentityId, HighlightType.Control, block);
                    EnableHighlight(hlb);
                }

                return;
            }

            if (_constructController.ImportantBlocks[HighlightType.Medical].Count > 0 &&
                DefaultSettings.UseMedicalGroup)
            {
                foreach (var block in _constructController.ImportantBlocks[HighlightType.Medical])
                {
                    HighlightedBlock hlb = GetHighlightedBlock(grinder.OwnerIdentityId, HighlightType.Medical, block);
                    EnableHighlight(hlb);
                }

                return;
            }

            if (_constructController.ImportantBlocks[HighlightType.Weapon].Count > 0 && DefaultSettings.UseWeaponGroup)
            {
                foreach (var block in _constructController.ImportantBlocks[HighlightType.Weapon])
                {
                    HighlightedBlock hlb = GetHighlightedBlock(grinder.OwnerIdentityId, HighlightType.Weapon, block);
                    EnableHighlight(hlb);
                }

                return;
            }

            if (_constructController.ImportantBlocks[HighlightType.Trap].Count > 0 && DefaultSettings.UseTrapGroup)
            {
                foreach (var block in _constructController.ImportantBlocks[HighlightType.Trap])
                {
                    HighlightedBlock hlb = GetHighlightedBlock(grinder.OwnerIdentityId, HighlightType.Trap, block);
                    EnableHighlight(hlb);
                }
            }
            // If we hit here, something is wrong as there are no blocks to highlight, so the grid should be disowned.
        }

        private void EnableSingleBlockHighlight(IMyAngleGrinder grinder)
        {
            if (_constructController.ImportantBlocks[HighlightType.Control].Count > 0)
            {
                MyCubeBlock block =
                    GetNearestBlock(grinder, _constructController.ImportantBlocks[HighlightType.Control]);
                HighlightedBlock hlb = GetHighlightedBlock(grinder.OwnerIdentityId, HighlightType.Control, block);
                EnableHighlight(hlb);
                return;
            }

            if (_constructController.ImportantBlocks[HighlightType.Medical].Count > 0 &&
                DefaultSettings.UseMedicalGroup)
            {
                MyCubeBlock block =
                    GetNearestBlock(grinder, _constructController.ImportantBlocks[HighlightType.Medical]);
                HighlightedBlock hlb = GetHighlightedBlock(grinder.OwnerIdentityId, HighlightType.Medical, block);
                EnableHighlight(hlb);
                return;
            }

            if (_constructController.ImportantBlocks[HighlightType.Weapon].Count > 0 && DefaultSettings.UseWeaponGroup)
            {
                MyCubeBlock block = GetNearestBlock(grinder, _constructController.ImportantBlocks[HighlightType.Weapon]);
                HighlightedBlock hlb = GetHighlightedBlock(grinder.OwnerIdentityId, HighlightType.Weapon, block);
                EnableHighlight(hlb);
                return;
            }

            if (_constructController.ImportantBlocks[HighlightType.Trap].Count > 0 && DefaultSettings.UseTrapGroup)
            {
                MyCubeBlock block = GetNearestBlock(grinder, _constructController.ImportantBlocks[HighlightType.Trap]);
                HighlightedBlock hlb = GetHighlightedBlock(grinder.OwnerIdentityId, HighlightType.Trap, block);
                EnableHighlight(hlb);
                return;
            }
            // If we hit here, something is wrong as there are no blocks to highlight, so the grid should be disowned.
        }

        private static MyCubeBlock GetNearestBlock(IMyAngleGrinder grinder, HashSet<MyCubeBlock> blocks)
        {
            if (blocks.Count == 1) return blocks.FirstOrDefault();
            Vector3D entPos = grinder.GetPosition();
            double closestDistSq = double.MaxValue;
            MyCubeBlock closestBlock = null;
            foreach (var block in blocks)
            {
                double distSq = Vector3D.DistanceSquared(entPos, block.PositionComp.GetPosition());
                if (!(distSq < closestDistSq)) continue;
                closestDistSq = distSq;
                closestBlock = block;
            }

            return closestBlock;
        }

        private void EnableHighlight(HighlightedBlock hlb)
        {
            EnableHighlight(hlb.Block, hlb.TargetPlayer, hlb.Color);
            ScheduleHighlightDisable(hlb);
        }

        private void ScheduleHighlightDisable(HighlightedBlock hlb)
        {
            _constructController.TickDelayedAction(DefaultSettings.HighlightDuration, () => DisableHighlight(hlb));
        }

        private static void EnableHighlight(MyCubeBlock block, long playerId, Color color)
        {
            MyVisualScriptLogicProvider.SetHighlight(block.Name, true, HighlightSettings.EnabledThickness,
                HighlightSettings.HighlightPulseDuration, color, playerId);
        }

        private void DisableHighlight(HighlightedBlock hlb)
        {
            DisableHighlight(hlb.Block, hlb.TargetPlayer, hlb.Color);
            hlb.Reset();
            HighlightedBlockPool.Return(hlb);
        }

        private static void DisableHighlight(MyCubeBlock block, long playerId, Color color)
        {
            MyVisualScriptLogicProvider.SetHighlight(block.Name, false, HighlightSettings.DisabledThickness,
                HighlightSettings.HighlightPulseDuration, color, playerId);
        }

        public void Reset()
        {
            //DisableAllHighlights();
        }
    }
}