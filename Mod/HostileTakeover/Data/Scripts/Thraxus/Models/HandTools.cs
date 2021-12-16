using HostileTakeover.Common.BaseClasses;
using HostileTakeover.Common.Enums;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI.Weapons;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.Entity.UseObject;
using VRage.ModAPI;
using VRage.Utils;

namespace HostileTakeover.Models
{
	public class HandTools : BaseLoggingClass
	{
		protected override string Id { get; } = "HandTools";

		public HandTools()
		{
			MyEntities.OnEntityCreate += OnEntityCreate;
			MyEntities.OnEntityAdd += OnEntityAdd;
		}

		private void OnEntityAdd(IMyEntity ent)
		{
			WriteToLog($"{Id} Entity Add", $"{ent.GetType()}", LogType.General);
			
			
			MyHandToolBase handToolBase = ent as MyHandToolBase;
			if (handToolBase != null)
			{
				//WriteToLog($"handToolBase", $"", LogType.General);

				WriteToLog($"handToolBase", $"TypeID: {handToolBase.DefinitionId.TypeId} | SubtypeID: {handToolBase.DefinitionId.SubtypeId} | SubtypeName: {handToolBase.DefinitionId.SubtypeName}", LogType.General);

				foreach (var subPart in handToolBase.Subparts)
				{
					WriteToLog($"handToolBase - Subparts", $"Key: {subPart.Key} | Value: {subPart.Value}", LogType.General);
				}

				// Not sure if this is useful... we'll see.
				MyToolBase myToolBase = handToolBase.GunBase;


				MyPhysicalItemDefinition myPhysicalItemDefinition = handToolBase.PhysicalItemDefinition;
				WriteToLog($"handToolBase - myPhysicalItemDefinition", $"Model: {myPhysicalItemDefinition.Model} | ModContext: {myPhysicalItemDefinition.Context} | Type: {myPhysicalItemDefinition.GetType()}", LogType.General);


				MyToolItemDefinition myToolItemDefinition = (MyToolItemDefinition)handToolBase.PhysicalItemDefinition;
				foreach (var action in myToolItemDefinition.PrimaryActions)
				{
					WriteToLog($"toolBase - myToolItemDefinition- Primary Actions", $"Action: {action} | {action.HitConditions} | {action.Name}", LogType.General);
				}

				foreach (var action in myToolItemDefinition.SecondaryActions)
				{
					WriteToLog($"toolBase - myToolItemDefinition - Secondary Actions", $"Action: {action} | {action.HitConditions} | {action.Name}", LogType.General);
				}
			}

			IMyEngineerToolBase engineerToolBase = ent as IMyEngineerToolBase;
			if (engineerToolBase != null)
			{
				
				//WriteToLog($"engineerToolBase", $"", LogType.General);
				WriteToLog($"engineerToolBase", $"I'm a tool!", LogType.General);
				WriteToLog($"engineerToolBase", $"TypeID: {engineerToolBase.DefinitionId.TypeId} | SubtypeID: {engineerToolBase.DefinitionId.SubtypeId} | SubtypeName: {engineerToolBase.DefinitionId.SubtypeName}", LogType.General);

				MyPhysicalItemDefinition physDef = engineerToolBase.PhysicalItemDefinition;
				if (physDef != null)
				{
					WriteToLog($"engineerToolBase - myPhysicalItemDefinition", $"Model: {physDef.Model} | ModContext: {physDef.Context} | Type: {physDef.GetType()} | IsToolItem: {physDef.GetType() == typeof(MyToolItemDefinition)}", LogType.General);

				}

				//MyToolHitCondition x = new MyToolHitCondition();
				//IMyUseObject y;
				if (((MyEntity)ent).Components != null)
				{
					foreach (var comp in ((MyEntity) ent).Components)
					{
						WriteToLog($"engineerToolBase - Components", $"{comp.GetType()} | {comp}", LogType.General);
						foreach (var compTypes in comp.ContainerBase.GetComponentTypes())
						{
							WriteToLog($"engineerToolBase - CompTypes", $"{compTypes.Name}", LogType.General);
							


							//foreach (var VARIABLE in ((MyAssetModifierComponent)compTypes).AssetModifiers)
							//{
							//	((MyAssetModifierComponent) compTypes).AssetModifiers
							//}
							//WriteToLog($"engineerToolBase - CompTypes", $"{compTypes.Name}", LogType.General);
						}

						//MyResourceSinkComponent x = comp.ContainerBase.Get<MyResourceSinkComponent>();
						////x.


						//MyCasterComponent myComp = comp.ContainerBase.Get<MyCasterComponent>();

						//if (myComp == null) continue;
						//WriteToLog($"MyAssetModifierComponent", $"{myComp.}", LogType.General);
					}

					

					
				}

				if (engineerToolBase.DefinitionId.SubtypeId != MyStringHash.GetOrCompute("BlockFinder")) return;
				//MyAssetModifierComponent
				//MyCasterComponent z;
				//MyToolActionDefinition myToolAction;
				MyToolItemDefinition def = (MyToolItemDefinition)physDef;
				if (def != null)
				{

					foreach (var action in def.PrimaryActions)
					{
						WriteToLog($"toolBase - myToolItemDefinition- Primary Actions", $"Action: {action} | {action.HitConditions} | {action.Name}", LogType.General);
					}

					foreach (var action in def.SecondaryActions)
					{
						WriteToLog($"toolBase - myToolItemDefinition - Secondary Actions", $"Action: {action} | {action.HitConditions} | {action.Name}", LogType.General);
					}
				}
			}
		}

		private void OnEntityCreate(MyEntity ent)
		{
			WriteToLog($"{Id} Entity Create", $"{ent.GetType()}", LogType.General);
			MyHandToolBase handToolBase = ent as MyHandToolBase;
			if (handToolBase != null)
			{
				//WriteToLog($"handToolBase", $"", LogType.General);

				WriteToLog($"handToolBase", $"TypeID: {handToolBase.DefinitionId.TypeId} | SubtypeID: {handToolBase.DefinitionId.SubtypeId} | SubtypeName: {handToolBase.DefinitionId.SubtypeName}", LogType.General);

				foreach (var subPart in handToolBase.Subparts)
				{
					WriteToLog($"handToolBase - Subparts", $"Key: {subPart.Key} | Value: {subPart.Value}", LogType.General);
				}

				// Not sure if this is useful... we'll see.
				MyToolBase myToolBase = handToolBase.GunBase;
				

				MyPhysicalItemDefinition myPhysicalItemDefinition = handToolBase.PhysicalItemDefinition;
				WriteToLog($"handToolBase - myPhysicalItemDefinition", $"Model: {myPhysicalItemDefinition.Model} | ModContext: {myPhysicalItemDefinition.Context} | Type: {myPhysicalItemDefinition.GetType()}", LogType.General);
				

				MyToolItemDefinition myToolItemDefinition = (MyToolItemDefinition)handToolBase.PhysicalItemDefinition;
				foreach (var action in myToolItemDefinition.PrimaryActions)
				{
					WriteToLog($"toolBase - myToolItemDefinition- Primary Actions", $"Action: {action} | {action.HitConditions} | {action.Name}", LogType.General);
				}

				foreach (var action in myToolItemDefinition.SecondaryActions)
				{
					WriteToLog($"toolBase - myToolItemDefinition - Secondary Actions", $"Action: {action} | {action.HitConditions} | {action.Name}", LogType.General);
				}
			}

			IMyEngineerToolBase engineerToolBase = ent as IMyEngineerToolBase;
			if (engineerToolBase != null)
			{
				//WriteToLog($"engineerToolBase", $"", LogType.General);
				WriteToLog($"engineerToolBase", $"I'm a tool!", LogType.General);
				//WriteToLog($"engineerToolBase", $"TypeID: {engineerToolBase.DefinitionId.TypeId} | SubtypeID: {engineerToolBase.DefinitionId.SubtypeId} | SubtypeName: {engineerToolBase.DefinitionId.SubtypeName}", LogType.General);
			}



			//MyHandToolBase toolBase = ent as MyHandToolBase;
			//IMyEngineerToolBase engineerToolBase = ent as IMyEngineerToolBase;
			//MyGunBase gunBase = ent as MyGunBase;
			//MyToolBase myToolBase;
			//MyDeviceBase myDeviceBase;
			//IMyWelder iMyWelder;
			//IMyAngleGrinder iMyAngleGrinder;
			//iMyAngleGrinder.PhysicalItemDefinition.
			//IMyAutomaticRifleGun iMyAutomaticRifleGun;
			//IMyGunBaseUser iMyGunBaseUser;
			//IMyModel iMyModel;
			//MyObjectBuilder_ToolItemDefinition toolItemDefinition;
			//toolItemDefinition.PrimaryActions


			//MyGunBaseUserExtension
			//IMyHandheldGunObject<myToolBase>
			//IMyGunObject<myToolBase>
			//IStoppableAttackingTool
		}

		public override void Close()
		{
			WriteToLog(Id, "Checking out...", LogType.General);
			MyEntities.OnEntityCreate -= OnEntityCreate;
			MyEntities.OnEntityAdd -= OnEntityAdd;
			base.Close();
		}
	}
}
