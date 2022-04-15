﻿using CitizenFX.Core;
using CitizenFX.Core.Native;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using VORP.Inventory.Client.Extensions;
using VORP.Inventory.Client.Models;
using VORP.Inventory.Shared;

namespace VORP.Inventory.Client.Scripts
{
    public class InventoryAPI : Manager
    {
        public static Dictionary<string, Dictionary<string, dynamic>> citems =
            new Dictionary<string, Dictionary<string, dynamic>>();

        public static Dictionary<string, ItemClass> UsersItems = new Dictionary<string, ItemClass>();
        public static Dictionary<int, WeaponClass> UsersWeapons = new Dictionary<int, WeaponClass>();
        public static Dictionary<int, string> bulletsHash = new Dictionary<int, string>();

        public void Init()
        {
            AddEvent("vorp:SelectedCharacter", new Action<int>(OnSelectedCharacterAsync));

            AddEvent("vorpCoreClient:addItem", new Action<int, int, string, string, string, bool, bool>(OnAddItem));
            AddEvent("vorpCoreClient:subItem", new Action<string, int>(OnSubtractItem));
            AddEvent("vorpCoreClient:subWeapon", new Action<int>(OnSubtractWeapon));
            AddEvent("vorpCoreClient:addBullets", new Action<int, string, int>(OnAddWeaponBullets));
            AddEvent("vorpCoreClient:subBullets", new Action<int, string, int>(OnSubtractWeaponBullets));
            AddEvent("vorpCoreClient:addComponent", new Action<int, string>(OnAddComponent));
            AddEvent("vorpCoreClient:subComponent", new Action<int, string>(OnSubtractComponent));

            AddEvent("vorpInventory:giveItemsTable", new Action<dynamic>(OnProcessItems));
            AddEvent("vorpInventory:giveInventory", new Action<string>(OnGiveInventory));
            AddEvent("vorpInventory:giveLoadout", new Action<dynamic>(OnGiveUserWeapons));
            AddEvent("vorpinventory:receiveItem", new Action<string, int>(OnReceiveItem));
            AddEvent("vorpinventory:receiveItem2", new Action<string, int>(OnReceiveItemTwo));
            AddEvent("vorpinventory:receiveWeapon",
                new Action<int, string, string, ExpandoObject, List<dynamic>>(OnReceiveWeapon));

            AddEvent("vorp:inventory:ux:update", new Action<double>((cash) =>
            {
                var hudObject = new { action = "updateStatusHud", money = cash };
                NUI.SendMessage(hudObject);
            }));

            AttachTickHandler(UpdateAmmoInWeaponAsync);

            Exports.Add("getWeaponLabelFromHash", new Func<string, string>(hash =>
            {
                if (Configuration.Weapons.ContainsKey(hash))
                    return Configuration.Weapons[hash].Name;
                return hash;
            }));
        }

        private async Task UpdateAmmoInWeaponAsync()
        {
            await Delay(500);
            uint weaponHash = 0;
            if (API.GetCurrentPedWeapon(API.PlayerPedId(), ref weaponHash, false, 0, false))
            {
                string weaponName = Function.Call<string>((Hash)0x89CF5FF3D363311E, weaponHash);
                if (weaponName.Contains("UNARMED")) { return; }

                Dictionary<string, int> ammoDict = new Dictionary<string, int>();
                WeaponClass usedWeapon = null;
                foreach (KeyValuePair<int, WeaponClass> weap in UsersWeapons.ToList())
                {
                    if (weaponName.Contains(weap.Value.Name) && weap.Value.Used)
                    {
                        ammoDict = weap.Value.Ammo;
                        usedWeapon = weap.Value;
                    }
                }

                if (usedWeapon == null) return;
                foreach (var ammo in ammoDict.ToList())
                {
                    int ammoQuantity = Function.Call<int>((Hash)0x39D22031557946C1, API.PlayerPedId(), API.GetHashKey(ammo.Key));
                    if (ammoQuantity != ammo.Value)
                    {
                        usedWeapon.SetAmmo(ammoQuantity, ammo.Key);
                    }
                }
            }
        }

        private void OnReceiveItem(string name, int count)
        {
            if (UsersItems.ContainsKey(name))
            {
                UsersItems[name].Count += count;
            }
            else
            {
                ItemClass itemClass = new ItemClass
                {
                    Count = count,
                    Limit = citems[name]["limit"],
                    Label = citems[name]["label"],
                    Name = name,
                    Type = "item_standard",
                    Usable = true,
                    CanRemove = citems[name]["can_remove"]
                };

                UsersItems.Add(name, itemClass);
            }

            PluginManager.Instance.NUIEvents.LoadInventory();
        }

        private void OnReceiveItemTwo(string name, int count)
        {
            UsersItems[name].Count -= count;
            if (UsersItems[name].Count == 0)
            {
                UsersItems.Remove(name);
            }
            PluginManager.Instance.NUIEvents.LoadInventory();
        }

        private void OnReceiveWeapon(int id, string propietary, string name, ExpandoObject ammo, List<dynamic> components)
        {
            Dictionary<string, int> ammoaux = new Dictionary<string, int>();
            foreach (KeyValuePair<string, object> amo in ammo)
            {
                ammoaux.Add(amo.Key, int.Parse(amo.Value.ToString()));
            }

            List<string> auxcomponents = new List<string>();
            foreach (var comp in components)
            {
                auxcomponents.Add(comp.ToString());
            }

            WeaponClass weapon = new WeaponClass
            {
                Id = id, 
                Propietary = propietary, 
                Name = name, 
                Ammo = ammoaux, 
                Components = auxcomponents, 
                Used = false, 
                Used2 = false
            };

            if (!UsersWeapons.ContainsKey(weapon.Id))
            {
                UsersWeapons.Add(weapon.Id, weapon);
            }

            PluginManager.Instance.NUIEvents.LoadInventory();
        }

        private async void OnSelectedCharacterAsync(int charId)
        {
            Logger.Trace($"OnSelectedCharacterAsync: {charId}");

            DecoratorExtensions.Set(PlayerPedId(), PluginManager.DECOR_SELECTED_CHARACTER_ID, charId);

            PluginManager.Instance.NUIEvents.OnCloseInventory();

            await BaseScript.Delay(1000);

            TriggerServerEvent("vorpinventory:getItemsTable");
            Logger.Trace($"OnSelectedCharacterAsync: vorpinventory:getItemsTable");
            await Delay(300);
            TriggerServerEvent("vorpinventory:getInventory");
            Logger.Trace($"OnSelectedCharacterAsync: vorpinventory:getInventory");
        }

        private void OnProcessItems(dynamic items)
        {
            try
            {
                citems.Clear();
                foreach (dynamic item in items)
                {
                    citems.Add(item.item, new Dictionary<string, dynamic>
                    {
                        ["item"] = item.item,
                        ["label"] = item.label,
                        ["limit"] = item.limit,
                        ["can_remove"] = item.can_remove,
                        ["type"] = item.type,
                        ["usable"] = item.usable
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"OnProcessItems");
            }
        }

        private void OnGiveUserWeapons(dynamic loadout)
        {
            foreach (var row in loadout)
            {
                JArray componentes = Newtonsoft.Json.JsonConvert.DeserializeObject(row.components.ToString());
                JObject amunitions = Newtonsoft.Json.JsonConvert.DeserializeObject(row.ammo.ToString());
                List<string> components = new List<string>();
                Dictionary<string, int> ammos = new Dictionary<string, int>();
                foreach (JToken componente in componentes)
                {
                    components.Add(componente.ToString());
                }

                foreach (JProperty amunition in amunitions.Properties())
                {
                    ammos.Add(amunition.Name, int.Parse(amunition.Value.ToString()));
                }

                bool auused = false;
                if (row.used == 1)
                {
                    auused = true;
                }

                bool auused2 = false;
                if (row.used2 == 1)
                {
                    auused2 = true;
                }

                WeaponClass auxweapon = new WeaponClass
                {
                    Id = int.Parse(row.id.ToString()), 
                    Propietary = row.identifier.ToString(), 
                    Name = row.name.ToString(), 
                    Ammo = ammos, 
                    Components = components, 
                    Used = auused, 
                    Used2 = auused2
                };

                if (!UsersWeapons.ContainsKey(auxweapon.Id))
                {
                    UsersWeapons.Add(auxweapon.Id, auxweapon);

                    if (auxweapon.Used)
                    {
                        Utils.UseWeapon(auxweapon.Id);
                    }
                }
            }
        }

        private void OnGiveInventory(string inventory)
        {
            UsersItems.Clear();
            if (inventory != null)
            {
                Logger.Trace($"OnGetInventory: {inventory}");

                dynamic items = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(inventory);

                foreach (KeyValuePair<string, Dictionary<string, dynamic>> fitems in citems)
                {
                    if (items[fitems.Key] != null)
                    {
                        int cuantity = int.Parse(items[fitems.Key].ToString());
                        int limit = int.Parse(fitems.Value["limit"].ToString());
                        string label = fitems.Value["label"].ToString();
                        bool canRemove = bool.Parse(fitems.Value["can_remove"].ToString());
                        string type = fitems.Value["type"].ToString();
                        bool usable = bool.Parse(fitems.Value["usable"].ToString());
                        ItemClass item = new ItemClass
                        {
                            Count = cuantity, 
                            Limit = limit, 
                            Label = label, 
                            Name = fitems.Key, 
                            Type = type, 
                            Usable = usable, 
                            CanRemove = canRemove
                        };

                        UsersItems.Add(fitems.Key, item);
                    }
                }
            }
        }

        private void OnSubtractComponent(int weaponId, string component)
        {
            if (UsersWeapons.ContainsKey(weaponId))
            {
                if (!UsersWeapons[weaponId].Components.Contains(component))
                {
                    UsersWeapons[weaponId].QuitComponent(component);
                    if (UsersWeapons[weaponId].Used)
                    {
                        Function.Call((Hash)0x4899CB088EDF59B8, API.PlayerPedId(),
                            (uint)API.GetHashKey(UsersWeapons[weaponId].Name), true, 0);
                        UsersWeapons[weaponId].LoadAmmo();
                        UsersWeapons[weaponId].LoadComponents();
                    }
                }
            }
        }

        private void OnAddComponent(int weaponId, string component)
        {
            if (UsersWeapons.ContainsKey(weaponId))
            {
                if (!UsersWeapons[weaponId].Components.Contains(component))
                {
                    UsersWeapons[weaponId].Components.Add(component);
                    if (UsersWeapons[weaponId].Used)
                    {
                        Function.Call((Hash)0x4899CB088EDF59B8, API.PlayerPedId(),
                            (uint)API.GetHashKey(UsersWeapons[weaponId].Name), true, 0);
                        UsersWeapons[weaponId].LoadAmmo();
                        UsersWeapons[weaponId].LoadComponents();
                    }
                }
            }
        }

        private void OnSubtractWeaponBullets(int weaponId, string bulletType, int cuantity)
        {
            if (UsersWeapons.ContainsKey(weaponId))
            {
                UsersWeapons[weaponId].SubAmmo(cuantity, bulletType);
                if (UsersWeapons[weaponId].Used)
                {
                    API.SetPedAmmoByType(API.PlayerPedId(), API.GetHashKey(bulletType), UsersWeapons[weaponId].GetAmmo(bulletType));
                }
            }
            PluginManager.Instance.NUIEvents.LoadInventory();
        }

        private void OnAddWeaponBullets(int weaponId, string bulletType, int cuantity)
        {
            if (UsersWeapons.ContainsKey(weaponId))
            {
                UsersWeapons[weaponId].AddAmmo(cuantity, bulletType);
                if (UsersWeapons[weaponId].Used)
                {
                    API.SetPedAmmoByType(API.PlayerPedId(), API.GetHashKey(bulletType), UsersWeapons[weaponId].GetAmmo(bulletType));
                }
            }
            PluginManager.Instance.NUIEvents.LoadInventory();
        }


        private void OnSubtractWeapon(int weaponId)
        {
            if (UsersWeapons.ContainsKey(weaponId))
            {
                if (UsersWeapons[weaponId].Used)
                {
                    API.RemoveWeaponFromPed(API.PlayerPedId(),
                        (uint)API.GetHashKey(UsersWeapons[weaponId].Name),
                        true, 0); //Falta revisar que pasa con esto
                }
                UsersWeapons.Remove(weaponId);
            }
            PluginManager.Instance.NUIEvents.LoadInventory();
        }

        private void OnSubtractItem(string name, int cuantity)
        {
            if (UsersItems.ContainsKey(name))
            {
                UsersItems[name].Count = cuantity;
                if (UsersItems[name].Count == 0)
                {
                    UsersItems.Remove(name);
                }
            }
            PluginManager.Instance.NUIEvents.LoadInventory();
        }

        private void OnAddItem(int count, int limit, string label, string name, string type, bool usable, bool canRemove)
        {
            if (UsersItems.ContainsKey(name))
            {
                UsersItems[name].Count += count;
            }
            else
            {
                ItemClass auxitem = new ItemClass 
                { 
                    Count = count, 
                    Limit = limit, 
                    Label = label, 
                    Name = name, 
                    Type = type, 
                    Usable = usable, 
                    CanRemove = canRemove 
                };

                UsersItems.Add(name, auxitem);
            }
            PluginManager.Instance.NUIEvents.LoadInventory();
        }
    }
}