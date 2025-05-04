using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace UsefulTrophiesAgain
{
    [HarmonyPatch(typeof(Humanoid), "UseItem")]
    class UseItemPatch
    {
        public static bool Prefix(Humanoid __instance, [HarmonyArgument(0)] Inventory inventory, [HarmonyArgument(1)] ItemDrop.ItemData item, 
            [HarmonyArgument(2)] bool fromInventoryGui, Inventory ___m_inventory, ZSyncAnimation ___m_zanim)
        {
            string itemName = item.m_shared.m_name;

            if (UsefulTrophiesAgain.CanConsumeBossSummonItems && UsefulTrophiesAgain.SecondaryPowerDict.TryGetValue(itemName, out string powerName))
            {
                StatusEffect bossPower = ObjectDB.instance.GetStatusEffect(powerName.GetStableHashCode());
                if (!UsefulTrophiesAgain.SecondaryPowerTime.TryGetValue(itemName, out float powerTime))
                {
                    powerTime = 120f;
                }
                
                ApplyStatusEffect(bossPower.Clone(), powerTime, __instance.transform.position);

                if (!itemName.Contains("$item_trophy_"))
                {
                    // Consume Item 
                    Debug.Log($"Consume {itemName} Secondary Boss Item!");
                    inventory.RemoveOneItem(item);
                    __instance.m_consumeItemEffects.Create(Player.m_localPlayer.transform.position, Quaternion.identity, null, 1f, -1);
                    ___m_zanim.SetTrigger("eat");
                    return false;
                }
            }
            
            // Only Override function for Trophy Items
            if (itemName.Contains("$item_trophy_"))
            {
                string enemy = itemName.Substring(13);

                if (UsefulTrophiesAgain.BossEnemies.Contains(enemy))
                {
                    if (!UsefulTrophiesAgain.CanConsumeBosses) return true;
                    
                    if (UsefulTrophiesAgain.BossPowerDict.TryGetValue(enemy, out powerName))
                    {
                        StatusEffect bossPower = ObjectDB.instance.GetStatusEffect(powerName.GetStableHashCode());
                            
                        // Protection against eating boss heads near unactivated boss stones
                        BossStone[] bossStones = GameObject.FindObjectsOfType<BossStone>();
                        
                        // If we are near any boss stones, the find will find some
                        if (bossStones.Length > 1 && bossStones[0].m_itemStand.m_netViewOverride.IsValid())
                        {
                            // There may be duplicate stones, so find at least one active stone to confirm we have it handed in
                            bool bossActived = false;
                            foreach (var stone in bossStones)
                            {
                                if (stone.IsActivated() && bossPower.m_name == stone.m_itemStand.m_guardianPower.m_name)
                                {
                                    bossActived = true;
                                    break;
                                }
                            }

                            if (!bossActived)
                            {
                                Debug.Log("Prevented Player from consuming boss trophy near BossStone");
                                return true;
                            }
                        }
                        
                        // Copy power so we dont effect the original data
                        ApplyStatusEffect(bossPower.Clone(), UsefulTrophiesAgain.BossPowerTime, __instance.transform.position);
                    }
                }
                
                if (inventory == null)
                {
                    inventory = ___m_inventory;
                }
                if (!inventory.ContainsItem(item))
                {
                    return false;
                }
                
                // Prioritize Hover Objects (item stands/altars)
                GameObject hoverObject = __instance.GetHoverObject();
                Hoverable hoverable = hoverObject ? hoverObject.GetComponentInParent<Hoverable>() : null;
                if (hoverable != null && !fromInventoryGui)
                {
                    Interactable componentInParent = hoverObject.GetComponentInParent<Interactable>();
                    if (componentInParent != null && componentInParent.UseItem(__instance, item))
                    {
                        return false;
                    }
                }

                Debug.Log($"Consume {enemy} trophy!");

                // Get a Random Skill from the Player's Skill Pool
                List<Skills.Skill> skills = __instance.GetSkills().GetSkillList();
                Skills.Skill randomSkill = skills[UnityEngine.Random.Range(0, skills.Count)];

                float skillFactor = 10f;
                if (UsefulTrophiesAgain.TrophyXPDict.TryGetValue(enemy, out float dictSkillFactor))
                {
                    skillFactor = dictSkillFactor;
                }
                else
                {
                    Debug.Log($"Unknown trophy for {enemy}!");
                }
                
                Debug.Log($"Raising {randomSkill.m_info.m_skill} by {skillFactor}");

                float req = GetNextLevelRequirement(randomSkill) - randomSkill.m_accumulator;
                
                // Increase Skill by Configured Skill Factor
                __instance.RaiseSkill(randomSkill.m_info.m_skill, skillFactor);
                skillFactor -= req;

                // Handle multi-levelUps
                while (skillFactor > 0f)
                {
                    req = GetNextLevelRequirement(randomSkill);
                    __instance.RaiseSkill(randomSkill.m_info.m_skill, skillFactor);
                    skillFactor -= req;
                }

                // Consume Item 
                inventory.RemoveOneItem(item);
                __instance.m_consumeItemEffects.Create(Player.m_localPlayer.transform.position, Quaternion.identity, null, 1f, -1);
                ___m_zanim.SetTrigger("eat");

                // Notify Player of the Stat Increase
                __instance.Message(MessageHud.MessageType.Center, $"You feel better with {randomSkill.m_info.m_skill}", 0, null);
                return false;
            }

            return true;
        }

        private static void ApplyStatusEffect(StatusEffect statusEffect, float time, Vector3 position)
        {
            statusEffect.m_ttl = time;
            List<Player> players = new List<Player>();
            Player.GetPlayersInRange(position, 10f, players);
            foreach (Player player in players)
            {
                player.GetSEMan().AddStatusEffect(statusEffect, true);
            }
        }
        
        private static float GetNextLevelRequirement(Skills.Skill skill) => (float) ((double) Mathf.Pow(skill.m_level + 1f, 1.5f) * 0.5 + 0.5);
    }
}
