using Terraria;
using Terraria.GameContent.Creative;
using TShockAPI;

namespace Punisher;

public class WeaponInformation
{
    private static int defaultAccSlot => 0;
    private Item weaponItem;
    private Player fakePlayer;
    
    public WeaponInformation(int _weaponId, Player plr)
    {
        weaponItem = new Item();
        weaponItem.SetDefaults(_weaponId);

        fakePlayer = plr;
    }
    
    // method to get base damage w/o modifiers
    public int GetBaseDamage()
    {
        return weaponItem.damage;
    }
    
    public void ApplyDamageModifier()
    {
        fakePlayer.UpdateEquips(1);
    }
    
    public int GetModifiedDamage()
    {
        return fakePlayer.GetWeaponDamage(weaponItem);
    }

    public int GetFixedDamageWithDefense(int defense)
    {
        return GetModifiedDamage() - defense;
    }
    
    public bool OverThreshold(int damage)
    {
        TShock.Log.ConsoleInfo($"Threshold: {Punisher.Configuration?.Threshold}");
        var threshold = Punisher.Configuration?.Threshold / 100f;
        
        if (threshold is null) return false;

        var thresholdDmg = GetModifiedDamage() * threshold;
        TShock.Log.ConsoleInfo($"Threshold damage: {thresholdDmg}");
        
        return damage > GetModifiedDamage()*threshold;
    }
}