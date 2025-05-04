using HarmonyLib;

namespace UsefulTrophiesAgain
{
    [HarmonyPatch(typeof(InventoryGrid), "UpdateGui")]
    public class InventoryGridPatch
    {
        public static bool Prefix(InventoryGrid __instance, [HarmonyArgument(0)] Player player, [HarmonyArgument(1)] ItemDrop.ItemData dragItem)
        {
            if (!UsefulTrophiesAgain.EnableSellingTrophies) return true;

            if (player == null) return true;
        
            foreach (ItemDrop.ItemData itemData in player.GetInventory().GetAllItems())
            {
                string itemName = itemData.m_shared.m_name; 
                if (itemName.Contains("$item_trophy_"))
                {
                    string enemy = itemName.Substring(13);
                    
                    // Give value to any trophies in inventory
                    if (UsefulTrophiesAgain.TrophyGoldValueDict.TryGetValue(enemy, out int value))
                    {
                        itemData.m_shared.m_value = value;
                    }
                }
            }
        
            return true;
        }
    }
}