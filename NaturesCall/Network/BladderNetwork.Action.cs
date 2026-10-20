using NaturesCall.Bladder;
using NaturesCall.Config;
using NaturesCall.Util;
using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;
using static NaturesCall.Network.PeeMessage;

namespace NaturesCall.Network;

public partial class BladderNetwork
{
    #region Client
    
    private void OnServerPeeMessage(PeeMessage.Response response)
    {
        var entity = _capi.World.Player.Entity;
        //Added an if statement to check for null blockselection so that players can still pee if looking at entity
        if (entity.BlockSelection != null)
        {
            SpawnPeeParticles(entity, response.Position,
            entity.BlockSelection?.HitPosition,
            ConfigSystem.ConfigClient.UrineColor == "default" ? null : ConfigSystem.ConfigClient.UrineColor,
            response.ShouldDieOnContact,
            ConfigSystem.ConfigClient.UrineParticleLifetime,
            ConfigSystem.ConfigClient.StreamArc);
        }
        else if (ConfigSystem.ConfigServer.UrinateOnEntities)
        {
            SpawnPeeParticles(entity, response.Position,
            entity.EntitySelection?.HitPosition,
            ConfigSystem.ConfigClient.UrineColor == "default" ? null : ConfigSystem.ConfigClient.UrineColor,
            response.ShouldDieOnContact,
            ConfigSystem.ConfigClient.UrineParticleLifetime,
            ConfigSystem.ConfigClient.StreamArc);
        }
        else return;
        
        EntityBehaviorBladder.PlayPeeSound(entity);
    }
    
    private void OnEntityAction(EnumEntityAction action, bool on, ref EnumHandling handled)
    {
        if (action != EnumEntityAction.InWorldRightMouseDown)
        {
            // Set up something in a bad way so now need this for it to function smoothly
            _lastPeeMessageTime = null;
            return;
        };
        if (ConfigSystem.ConfigClient.OnlyPeeWithHotkey) return;
        if (!TryTriggerPeeActionOnClient()) return;
        handled = EnumHandling.Handled;
    }
    
    private bool OnPeeKeyPressed(KeyCombination keyCombination)
    {
        return TryTriggerPeeActionOnClient(false, true);
    }

    private bool TryTriggerPeeActionOnClient(bool ignoreOverload = false, bool ignoreExtraKeys = false)
    {
        var world = _capi?.World;
        var player = world?.Player;
        if (world == null || player == null) return false;
        PromptModeChoiceIfNotSet(player);
        if (!CanPlayerPee(player.Entity, ignoreOverload, ignoreExtraKeys))
        {
            //Added this extra bit of code before the return false to produce more particles even if CanPlayerPee says not enough time has passed
            if (CheckPeeControls(player.Entity, ignoreExtraKeys) && player.Entity.WatchedAttributes.GetTreeAttribute($"{Core.ModId}:bladder").GetFloat("currentlevel") > 0.1f) VisualOnlyPee(player);
            
            return false;
        }
        var elapsedTime = world.ElapsedMilliseconds;
        var actionMs = elapsedTime - _lastPeeMessageTime;
        _lastPeeTime = elapsedTime;
        _lastPeeMessageTime = elapsedTime;
        if (!actionMs.HasValue) return false;
        //Added if statement to check if target is block or entity
        if (player.CurrentBlockSelection == null)
        {
            if (!_capi.World.Config.GetBool("UrinateOnEntities"))
            {
                return false;
            }
            _clientChannel.SendPacket(new PeeMessage.Request()
            {
                Position = player.CurrentEntitySelection?.Position,
                HitPostion = player.CurrentEntitySelection?.HitPosition,
                Color = ConfigSystem.ConfigClient.UrineColor == "default" ? null : ConfigSystem.ConfigClient.UrineColor,
                ActionMs = actionMs.Value
            });
            return true;
        }
        _clientChannel.SendPacket(new PeeMessage.Request()
        {
            Position = player.CurrentBlockSelection?.Position.ToVec3d(),
            HitPostion = player.CurrentBlockSelection?.HitPosition,
            Color = ConfigSystem.ConfigClient.UrineColor == "default" ? null : ConfigSystem.ConfigClient.UrineColor,
            ActionMs = actionMs.Value
        });
        return true;
    }

    private void PromptModeChoiceIfNotSet(IClientPlayer player)
    {
        if (ConfigSystem.ConfigClient.PeeMode == EnumPeeMode.None)
        {
            _choiceHud.TryOpen();
            player.IngameError(player, "peemodenotset", Lang.Get(Core.ModId+":peemodenotset") );
        }
    }
    
    private bool CanPlayerPee(EntityPlayer player, bool ignoreOverload = false, bool ignoreExtraKeys = false)
    {
        if (ignoreOverload) return CheckPeeControls(player, ignoreExtraKeys);
        //Old code including IsBladderOverloaded() commented out for reference. Removing it results in peeing being almost uniform whether using the hotkey or rightclick
        //return (player.Player.IsBladderOverloaded() || player.World.ElapsedMilliseconds - _lastPeeTime >= PeeActionMs) &&
               //CheckPeeControls(player, ignoreExtraKeys);
        return player.World.ElapsedMilliseconds - _lastPeeTime >= PeeActionMs &&
               CheckPeeControls(player, ignoreExtraKeys);
    }

    private static bool CheckPeeControls(EntityPlayer player, bool ignoreExtraKeys = false)
    {
        var canPee = player.ActiveHandItemSlot.Empty;
        if (ConfigSystem.ConfigClient.PeeMode.IsStanding())
        {
            canPee = canPee && (ignoreExtraKeys || player.Controls.CtrlKey);
        }
        else if (ConfigSystem.ConfigClient.PeeMode.IsSitting())
        {
            canPee = canPee && player.Controls.FloorSitting;
        }
        else return false;
        return canPee;
    }

    private void VisualOnlyPee(IClientPlayer player)
    {
        if (player.CurrentBlockSelection != null)
        {
            var shouldDieOnContact = _capi.World.Config.GetBool("UrineStains") && !(player.Entity.World.BlockAccessor.GetBlock(player.CurrentBlockSelection.Position).SideIsSolid(player.CurrentBlockSelection.Position, player.CurrentBlockSelection.SelectionBoxIndex));
            SpawnPeeParticles(player.Entity, player.CurrentBlockSelection?.Position.ToVec3d(),
            player.Entity.BlockSelection?.HitPosition,
            ConfigSystem.ConfigClient.UrineColor == "default" ? null : ConfigSystem.ConfigClient.UrineColor,
            shouldDieOnContact,
            ConfigSystem.ConfigClient.UrineParticleLifetime,
            ConfigSystem.ConfigClient.StreamArc);
        }
        else if (_capi.World.Config.GetBool("UrinateOnEntities"))
        {
            SpawnPeeParticles(player.Entity, player.CurrentEntitySelection?.Position,
            player.Entity.EntitySelection?.HitPosition,
            ConfigSystem.ConfigClient.UrineColor == "default" ? null : ConfigSystem.ConfigClient.UrineColor,
            false,
            ConfigSystem.ConfigClient.UrineParticleLifetime,
            ConfigSystem.ConfigClient.StreamArc);
        }
    }

    #endregion
    
    #region Server
    
    private void HandlePeeAction(IServerPlayer player, PeeMessage.Request request)
    {
        if (!player.Entity.HasBehavior<EntityBehaviorBladder>()) return;
        if (request.Position == null) return;
        var bh = player.Entity.GetBehavior<EntityBehaviorBladder>();
        var urinationDs = request.ActionMs / 100f;
        var drainAmount = ConfigSystem.ConfigServer.UrineDrainRate * urinationDs * 4;
        if (!(bh?.Drain(drainAmount) ?? false)) return;
        EntityBehaviorBladder.PlayPeeSound(player.Entity);
        var blockSelection = player.CurrentBlockSelection;
        var world = player.Entity.World;
        //Added a check to make sure the target is a block to fix crash when peeing on entities
        if (blockSelection == null)
        {
            var entitySelection = player.CurrentEntitySelection;
            if (entitySelection != null && ConfigSystem.ConfigServer.UrinateOnEntities)
            {
                _serverChannel.SendPacket(new PeeMessage.Response() { Position = entitySelection.Position, ShouldDieOnContact = false }, player);
                DamageSource damageSource = new() { HitPosition = entitySelection.HitPosition, IgnoreInvFrames = false, KnockbackStrength = 0, Source = EnumDamageSource.Player, SourceEntity = player.Entity, SourcePos = player.Entity.Pos.XYZ, Type = EnumDamageType.Injury};
                entitySelection.Entity.ReceiveDamage(damageSource, 0f);
                return;
            }
            else return;
        }
        var block = world.BlockAccessor.GetBlock(blockSelection.Position);
        var sideIsSolid = block.SideIsSolid(blockSelection.Position, blockSelection.SelectionBoxIndex);
        var shouldDieOnContact = ConfigSystem.ConfigServer.UrineStains && !sideIsSolid;
        _serverChannel.SendPacket(new PeeMessage.Response() { Position = blockSelection.Position.ToVec3d(), ShouldDieOnContact = shouldDieOnContact}, player);
        if (block is BlockLiquidContainerBase container)
        {
            var waterStack = new ItemStack(world.GetItem(Core.ModId + ":urineportion"))
            {
                StackSize = 128
            };
            var desiredLiters = drainAmount * 3f / 1800; // 3 liters per 1800 hydration
            var storedLitres = player.Entity.Attributes.GetFloat($"{Core.ModId}:urineVirtualLiters");
            var totalLiters = desiredLiters + storedLitres;
            var virtualLiters = totalLiters % 0.01f;
            var physicalLiters = totalLiters - virtualLiters;
            var stackSizeIn = container.TryPutLiquid(blockSelection.Position, waterStack, physicalLiters);
            container.DoLiquidMovedEffects(player, waterStack, stackSizeIn, BlockLiquidContainerBase.EnumLiquidDirection.Fill);
        } 
        else if (block is BlockLiquidContainerTopOpened container2)
        {
            var waterStack = new ItemStack(world.GetItem(Core.ModId + ":urineportion"))
            {
                StackSize = 128
            };
            var desiredLiters = drainAmount * 3f / 1800; // 3 liters per 1800 hydration
            var storedLitres = player.Entity.Attributes.GetFloat($"{Core.ModId}:urineVirtualLiters");
            var totalLiters = desiredLiters + storedLitres;
            var virtualLiters = totalLiters % 0.01f;
            var physicalLiters = totalLiters - virtualLiters;
            var stackSizeIn = container2.TryPutLiquid(blockSelection.Position, waterStack, physicalLiters);
            container2.DoLiquidMovedEffects(player, waterStack, stackSizeIn, BlockLiquidContainerBase.EnumLiquidDirection.Fill);
        }
        else if (world.BlockAccessor.GetBlockEntity(blockSelection.Position) is BlockEntityGroundStorage beg &&
                    beg.GetSlotAt(blockSelection) is ItemSlot peeIntoSlot &&
                    peeIntoSlot.Itemstack?.Block is BlockLiquidContainerTopOpened begCnt)
        {
            var waterStack = new ItemStack(world.GetItem(Core.ModId + ":urineportion"))
            {
                StackSize = 128
            };
            var desiredLiters = drainAmount * 3f / 1800; // 3 liters per 1800 hydration
            var storedLitres = player.Entity.Attributes.GetFloat($"{Core.ModId}:urineVirtualLiters");
            var totalLiters = desiredLiters + storedLitres;
            var virtualLiters = totalLiters % 0.01f;
            var physicalLiters = totalLiters - virtualLiters;
            if (begCnt.TryPutLiquid(peeIntoSlot.Itemstack, waterStack, physicalLiters) == 0) return;
            beg.MarkDirty(true);
        }
        else if (block is BlockFarmland)
        {
            FertiliseFarmland(world, blockSelection.Position);
        } 
        else if (world.BlockAccessor.GetBlock(blockSelection.Position.DownCopy()) is BlockFarmland)
        {
            FertiliseFarmland(world, blockSelection.Position.DownCopy());
        }
        else if (world.BlockAccessor.GetBlockEntity(blockSelection.Position) is IFarmlandBlockEntity)
        {
            FertiliseIFarmland(world, blockSelection.Position);
        }
        else if (world.BlockAccessor.GetBlockEntity(blockSelection.Position.DownCopy()) is IFarmlandBlockEntity)
        {
            FertiliseIFarmland(world, blockSelection.Position.DownCopy());
        }
        else if (block is BlockToolMold)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSelection.Position) as BlockEntityToolMold; 
            be?.CoolWithWater();
        }
        else if (block is BlockIngotMold)
        {
            var be = world.BlockAccessor.GetBlockEntity(blockSelection.Position) as BlockEntityIngotMold; 
            be?.CoolWithWater();
        }
        if (!ConfigSystem.ConfigServer.UrineStains) return;
        var rand = world.Rand.Next(0, 24);
        var x = rand % 6 + 1;
        var y = rand / 6 + 1;
        var stain = world.GetBlock(new AssetLocation(Core.ModId, $"caveart-stain-urine-1-{x}-{y}"));
        if (SuitableStainPosition(world.BlockAccessor, blockSelection))
            world.BlockAccessor.SetDecor(stain, blockSelection.Position,
                blockSelection.ToDecorIndex());
    }

    private static void FertiliseIFarmland(IWorldAccessor world, BlockPos position)
    {
        if (position == null) return;
        var be = world.BlockAccessor.GetBlockEntity(position);
        var tree = new TreeAttribute();
        be.ToTreeAttributes(tree);
        if (!tree.HasAttribute("moistureLevel")) return;
        var moistureLevel = tree.GetFloat("moistureLevel");
        moistureLevel = Math.Min(1f, moistureLevel+ 0.05f / 2f);
        tree.SetFloat("moistureLevel", moistureLevel);
        if (ConfigSystem.ConfigServer.UrineNutrientChance > world.Rand.NextDouble())
        {
            var nutrients = ConfigSystem.ConfigServer.UrineNutrientLevels;
            var n = tree.GetFloat("n") + nutrients[0];
            var p = tree.GetFloat("p") + nutrients[(EnumSoilNutrient)1];
            var k = tree.GetFloat("k") + nutrients[(EnumSoilNutrient)2];
            tree.SetFloat("n", n);
            tree.SetFloat("p", p);
            tree.SetFloat("k", k);
        }
        be.FromTreeAttributes(tree, world);
    }

    private static void FertiliseFarmland(IWorldAccessor world, BlockPos position)
    {
        if (position == null) return;
        var be = world.BlockAccessor.GetBlockEntity(position) as BlockEntityFarmland; 
        be?.WaterFarmland(0.05f);
        if (ConfigSystem.ConfigServer.UrineNutrientChance > world.Rand.NextDouble())
        {
            be?.IncreaseNutrients(ConfigSystem.ConfigServer.UrineNutrientLevels);
        }
    }

    //Added a calculation which will change the height the particles spawn from to be roughly half height, particleSpeed is how long it takes to reach the target point
    private static void SpawnPeeParticles(Entity byEntity, Vec3d pos, Vec3d hitPos, string color = null, bool shouldDieOnContact = true, float particleLifetime = 1f, float particleSpeed = 0.5f)
    {
        if (hitPos == null || pos == null) return;
        var entityPos = byEntity.Pos.XYZ.AddCopy(byEntity.LocalEyePos.SubCopy(0, byEntity.LocalEyePos.Y/2, 0));
        var posVec = pos.AddCopy(hitPos);
        var dist = (posVec - entityPos);
        var velocity = new Vec3f((float)dist.X / particleSpeed, ((float)dist.Y / particleSpeed) + 0.5f*(6.0f*particleSpeed), (float)dist.Z / particleSpeed);
        //var addVertical = new Vec3f(0, (float)(0.5f*Math.Sqrt(dist.NoY().LengthSq())), 0);
        //var velocity = 2.5f * dist.ToVec3f().AddCopy(addVertical).Normalize();
        var one = new Vec3f(1, 1, 1);
        var xyz = entityPos.AddCopy(0.5 * one.ToVec3d());

        _waterParticles = new SimpleParticleProperties(1f, 1f, -1, entityPos, entityPos, velocity.AddCopy(0.2f * one), velocity.AddCopy(-0.2f * one), minSize: 0.33f, maxSize: 0.75f)
        {
            AddPos = new Vec3d(),
            SizeEvolve = new EvolvingNatFloat(EnumTransformFunction.LINEAR, -1f),
            ClimateColorMap = "climateWaterTint",
            AddQuantity = 5f,
            GravityEffect = 0.6f,
            ShouldDieInLiquid = true,
            Bounciness = 0,
            LifeLength = particleLifetime,
            WithTerrainCollision = !shouldDieOnContact,
            WindAffected = true
        };
        
        if (color != null)
        {
            switch (color)
            {
                case "gaymer":
                {
                    // Make it change color over time using a sine wave in hsv space
                    var hue = (Math.Sin(byEntity.World.ElapsedMilliseconds / 1000.0) * 0.5 + 0.5) * 255;
                    var rgbaColor = ColorUtil.HsvToRgba((int)hue, 255, 255);
                    _waterParticles.Color = (rgbaColor & 0x00FFFFFF) | (120 << 24); // Ensure alpha is set to 160
                    _waterParticles.ClimateColorMap = null;
                    break;
                }
                case "blahaj":
                {
                    // Set the color to the trans flag colors
                    var transFlagColors = new[] { "#5BCEFA", "#F5A9B8", "#FFFFFF", "#F5A9B8", "#5BCEFA" }; // Light blue, pink, white
                    var colorIndex = (int)((byEntity.World.ElapsedMilliseconds / 1000.0) % transFlagColors.Length);
                    var colors = ColorUtil.Hex2Doubles(transFlagColors[colorIndex]);
                    _waterParticles.Color = ColorUtil.ToRgba(160, (int)(colors[0] * 255), (int)(colors[1] * 255), (int)(colors[2] * 255));
                    _waterParticles.ClimateColorMap = null;
                    break;
                }
                case "enbee":
                {
                    // Set the color to the nonbinary flag colors
                    var nonBinaryFlagColors = new[] { "#FFF430", "#FFFFFF", "#9C59D1", "#000000" }; // Yellow, white, purple, black
                    var colorIndex = (int)((byEntity.World.ElapsedMilliseconds / 1000.0) % nonBinaryFlagColors.Length);
                    var colors = ColorUtil.Hex2Doubles(nonBinaryFlagColors[colorIndex]);
                    _waterParticles.Color = ColorUtil.ToRgba(160, (int)(colors[0] * 255), (int)(colors[1] * 255), (int)(colors[2] * 255));
                    _waterParticles.ClimateColorMap = null;
                    break;
                }
                default:
                {
                    var colors = ColorUtil.Hex2Doubles(color);
                    _waterParticles.Color = ColorUtil.ToRgba(160, (int)(colors[0]*255), (int)(colors[1]*255), (int)(colors[2]*255));
                    _waterParticles.ClimateColorMap = null;
                    break;
                }
            }
        }
        
        byEntity.World.SpawnParticles(_waterParticles, byEntity is EntityPlayer entityPlayer ? entityPlayer.Player : null);
    }
    
    private bool SuitableStainPosition(IBlockAccessor blockAccessor, BlockSelection blockSel)
    {
        var block = blockAccessor.GetBlock(blockSel.Position);
        if (block.decorBehaviorFlags != 0) return false;
        if (block.SideSolid[blockSel.Face.Index] || block is BlockMicroBlock && (blockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityMicroBlock blockEntity ? (blockEntity.sideAlmostSolid[blockSel.Face.Index] ? 1 : 0) : 0) != 0)
        {
            var blockMaterial = block.GetBlockMaterial(blockAccessor, blockSel.Position);
            return ConfigSystem.ConfigServer.UrineStainableMaterials.Any(t => blockMaterial == t);
        }
        return false;
    }
    
    #endregion
}