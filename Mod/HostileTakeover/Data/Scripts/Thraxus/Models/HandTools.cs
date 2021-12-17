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
		protected string Id = nameof(HandTools);

		public HandTools()
		{
			MyEntities.OnEntityCreate += OnEntityCreate;
			MyEntities.OnEntityAdd += OnEntityAdd;
		}

		private void OnEntityAdd(IMyEntity ent)
		{
			WriteToLog($"{Id} Entity Add", $"{ent.GetType()}");
			
			MyHandToolBase handToolBase = ent as MyHandToolBase;
			if (handToolBase != null)
			{
				//WriteToLog($"handToolBase", $"");

				WriteToLog($"handToolBase", $"TypeID: {handToolBase.DefinitionId.TypeId} | SubtypeID: {handToolBase.DefinitionId.SubtypeId} | SubtypeName: {handToolBase.DefinitionId.SubtypeName}");

				foreach (var subPart in handToolBase.Subparts)
				{
					WriteToLog($"handToolBase - Subparts", $"Key: {subPart.Key} | Value: {subPart.Value}");
				}

				// Not sure if this is useful... we'll see.
				MyToolBase myToolBase = handToolBase.GunBase;


				MyPhysicalItemDefinition myPhysicalItemDefinition = handToolBase.PhysicalItemDefinition;
				WriteToLog($"handToolBase - myPhysicalItemDefinition", $"Model: {myPhysicalItemDefinition.Model} | ModContext: {myPhysicalItemDefinition.Context} | Type: {myPhysicalItemDefinition.GetType()}");


				MyToolItemDefinition myToolItemDefinition = (MyToolItemDefinition)handToolBase.PhysicalItemDefinition;
				foreach (var action in myToolItemDefinition.PrimaryActions)
				{
					WriteToLog($"toolBase - myToolItemDefinition- Primary Actions", $"Action: {action} | {action.HitConditions} | {action.Name}");
				}

				foreach (var action in myToolItemDefinition.SecondaryActions)
				{
					WriteToLog($"toolBase - myToolItemDefinition - Secondary Actions", $"Action: {action} | {action.HitConditions} | {action.Name}");
				}
			}

			IMyEngineerToolBase engineerToolBase = ent as IMyEngineerToolBase;
			if (engineerToolBase != null)
			{
				
				//WriteToLog($"engineerToolBase", $"");
				WriteToLog($"engineerToolBase", $"I'm a tool!");
				WriteToLog($"engineerToolBase", $"TypeID: {engineerToolBase.DefinitionId.TypeId} | SubtypeID: {engineerToolBase.DefinitionId.SubtypeId} | SubtypeName: {engineerToolBase.DefinitionId.SubtypeName}");

				MyPhysicalItemDefinition physDef = engineerToolBase.PhysicalItemDefinition;
				if (physDef != null)
				{
					WriteToLog($"engineerToolBase - myPhysicalItemDefinition", $"Model: {physDef.Model} | ModContext: {physDef.Context} | Type: {physDef.GetType()} | IsToolItem: {physDef.GetType() == typeof(MyToolItemDefinition)}");

				}

				//MyToolHitCondition x = new MyToolHitCondition();
				//IMyUseObject y;
				if (((MyEntity)ent).Components != null)
				{
					foreach (var comp in ((MyEntity) ent).Components)
					{
						WriteToLog($"engineerToolBase - Components", $"{comp.GetType()} | {comp}");
						foreach (var compTypes in comp.ContainerBase.GetComponentTypes())
						{
							WriteToLog($"engineerToolBase - CompTypes", $"{compTypes.Name}");
							


							//foreach (var VARIABLE in ((MyAssetModifierComponent)compTypes).AssetModifiers)
							//{
							//	((MyAssetModifierComponent) compTypes).AssetModifiers
							//}
							//WriteToLog($"engineerToolBase - CompTypes", $"{compTypes.Name}");
						}

						//MyResourceSinkComponent x = comp.ContainerBase.Get<MyResourceSinkComponent>();
						////x.


						//MyCasterComponent myComp = comp.ContainerBase.Get<MyCasterComponent>();

						//if (myComp == null) continue;
						//WriteToLog($"MyAssetModifierComponent", $"{myComp.}");
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
						WriteToLog($"toolBase - myToolItemDefinition- Primary Actions", $"Action: {action} | {action.HitConditions} | {action.Name}");
					}

					foreach (var action in def.SecondaryActions)
					{
						WriteToLog($"toolBase - myToolItemDefinition - Secondary Actions", $"Action: {action} | {action.HitConditions} | {action.Name}");
					}
				}
			}
		}

		private void OnEntityCreate(MyEntity ent)
		{
			WriteToLog($"{Id} Entity Create", $"{ent.GetType()}");
			MyHandToolBase handToolBase = ent as MyHandToolBase;
			if (handToolBase != null)
			{
				//WriteToLog($"handToolBase", $"");

				WriteToLog($"handToolBase", $"TypeID: {handToolBase.DefinitionId.TypeId} | SubtypeID: {handToolBase.DefinitionId.SubtypeId} | SubtypeName: {handToolBase.DefinitionId.SubtypeName}");

				foreach (var subPart in handToolBase.Subparts)
				{
					WriteToLog($"handToolBase - Subparts", $"Key: {subPart.Key} | Value: {subPart.Value}");
				}

				// Not sure if this is useful... we'll see.
				MyToolBase myToolBase = handToolBase.GunBase;
				

				MyPhysicalItemDefinition myPhysicalItemDefinition = handToolBase.PhysicalItemDefinition;
				WriteToLog($"handToolBase - myPhysicalItemDefinition", $"Model: {myPhysicalItemDefinition.Model} | ModContext: {myPhysicalItemDefinition.Context} | Type: {myPhysicalItemDefinition.GetType()}");
				

				MyToolItemDefinition myToolItemDefinition = (MyToolItemDefinition)handToolBase.PhysicalItemDefinition;
				foreach (var action in myToolItemDefinition.PrimaryActions)
				{
					WriteToLog($"toolBase - myToolItemDefinition- Primary Actions", $"Action: {action} | {action.HitConditions} | {action.Name}");
				}

				foreach (var action in myToolItemDefinition.SecondaryActions)
				{
					WriteToLog($"toolBase - myToolItemDefinition - Secondary Actions", $"Action: {action} | {action.HitConditions} | {action.Name}");
				}
			}

			IMyEngineerToolBase engineerToolBase = ent as IMyEngineerToolBase;
			if (engineerToolBase != null)
			{
				//WriteToLog($"engineerToolBase", $"");
				WriteToLog($"engineerToolBase", $"I'm a tool!");
				//WriteToLog($"engineerToolBase", $"TypeID: {engineerToolBase.DefinitionId.TypeId} | SubtypeID: {engineerToolBase.DefinitionId.SubtypeId} | SubtypeName: {engineerToolBase.DefinitionId.SubtypeName}");
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

        public override void WriteToLog(string caller, string message)
        {
            base.WriteToLog($"[{Id}] {caller}", message);
        }

        public override void Close()
		{
			WriteToLog(Id, "Checking out...");
			MyEntities.OnEntityCreate -= OnEntityCreate;
			MyEntities.OnEntityAdd -= OnEntityAdd;
			base.Close();
		}
	}
}
