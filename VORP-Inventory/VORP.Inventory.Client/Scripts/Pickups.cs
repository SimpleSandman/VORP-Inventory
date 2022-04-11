﻿using CitizenFX.Core;
using CitizenFX.Core.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using VORP.Inventory.Shared;
using VORP.Inventory.Client.Models;

namespace VORP.Inventory.Client.Scripts
{
    public class Pickups : Manager
    {
        public void Init()
        {
            AddEvent("vorpInventory:createPickup", new Action<string, int, int>(OnCreatePickupAsync));
            AddEvent("vorpInventory:createMoneyPickup", new Action<double>(OnCreateMoneyPickupAsync));
            AddEvent("vorpInventory:sharePickupClient", new Action<string, int, int, Vector3, int, int>(OnSharePickupClient));
            AddEvent("vorpInventory:shareMoneyPickupClient", new Action<int, double, Vector3, int>(OnShareMoneyPickupClient));
            AddEvent("vorpInventory:removePickupClient", new Action<int>(OnRemovePickupClientAsync));
            AddEvent("vorpInventory:playerAnim", new Action(OnPlayerExitAnimationAsync));
            AddEvent("vorp:PlayerForceRespawn", new Action(OnDeadActionsAsync));

            AttachTickHandler(PrincipalFunctionPickupsAsync);
            AttachTickHandler(PrincipalFunctionPickupsMoneyAsync);

            SetupPickPrompt();
        }

        private static int PickPrompt;
        public static Dictionary<int, Dictionary<string, dynamic>> pickups = new Dictionary<int, Dictionary<string, dynamic>>();
        public static Dictionary<int, Dictionary<string, dynamic>> pickupsMoney = new Dictionary<int, Dictionary<string, dynamic>>();
        private static bool active = false;
        private static bool active2 = false;
        private static bool dropAll = false;
        private static Vector3 lastCoords = new Vector3();

        private async void OnDeadActionsAsync()
        {
            lastCoords = Function.Call<Vector3>((Hash)0xA86D5F069399F44D, API.PlayerPedId(), true, true);
            dropAll = true;

            if (Configuration.Config.DropOnRespawn.Money)
            {
                TriggerServerEvent("vorpinventory:serverDropAllMoney");
            }

            await DropInventoryAsync();
        }

        public async Task DropInventoryAsync()
        {
            await Delay(200);
            if (Configuration.Config.DropOnRespawn.Items)
            {
                Dictionary<string, ItemClass> items = InventoryAPI.UsersItems.ToDictionary(p => p.Key, p => p.Value);
                foreach (var item in items.Values)
                {
                    TriggerServerEvent("vorpinventory:serverDropItem", item.getName(), item.getCount(), 1);
                    InventoryAPI.UsersItems[item.getName()].quitCount(item.getCount());
                    //Debug.Write(vorp_inventoryClient.useritems[itemname].getCount().ToString());
                    if (InventoryAPI.UsersItems[item.getName()].getCount() == 0)
                    {
                        InventoryAPI.UsersItems.Remove(item.getName());
                    }
                    await Delay(200);
                }
            }

            if (Configuration.Config.DropOnRespawn.Weapons)
            {
                Dictionary<int, WeaponClass> weapons = InventoryAPI.UsersWeapons.ToDictionary(p => p.Key, p => p.Value);
                foreach (var weapon in weapons)
                {
                    TriggerServerEvent("vorpinventory:serverDropWeapon", weapon.Key);
                    if (InventoryAPI.UsersWeapons.ContainsKey(weapon.Key))
                    {
                        WeaponClass wp = InventoryAPI.UsersWeapons[weapon.Key];
                        if (wp.getUsed())
                        {
                            wp.setUsed(false);
                            API.RemoveWeaponFromPed(API.PlayerPedId(), (uint)API.GetHashKey(wp.getName()),
                                true, 0);
                        }
                        InventoryAPI.UsersWeapons.Remove(weapon.Key);
                    }
                    await Delay(200);
                }
            }
            await Delay(800);
            dropAll = false;
        }

        private async Task PrincipalFunctionPickupsAsync()
        {
            int playerPed = API.PlayerPedId();
            Vector3 coords = Function.Call<Vector3>((Hash)0xA86D5F069399F44D, playerPed, true, true);

            if (pickups.Count == 0)
            {
                await BaseScript.Delay(1000);
                return;
            }

            foreach (var pick in pickups)
            {
                float distance = Function.Call<float>((Hash)0x0BE7F4E3CDBAFB28, coords.X, coords.Y, coords.Z,
                    pick.Value["coords"].X,
                    pick.Value["coords"].Y, pick.Value["coords"].Z, false);

                if (distance <= 5.0F && !pick.Value["inRange"])
                {
                    if (pick.Value["weaponid"] == 1)
                    {
                        string name = pick.Value["name"];
                        if (InventoryAPI.citems.ContainsKey(name))
                        {
                            name = InventoryAPI.citems[name]["label"];
                        }
                        Utils.DrawText3DAsync(pick.Value["coords"], name);
                    }
                    else
                    {
                        string name = Function.Call<string>((Hash)0x89CF5FF3D363311E,
                            (uint)API.GetHashKey(pick.Value["name"]));
                        Utils.DrawText3DAsync(pick.Value["coords"], name);
                    }
                }

                if (distance <= 1.2F && !pick.Value["inRange"])
                {
                    Function.Call((Hash)0x69F4BE8C8CC4796C, playerPed, pick.Value["obj"], 3000, 2048, 3);
                    if (active == false)
                    {
                        //Debug.WriteLine("Entro");
                        Function.Call((Hash)0x8A0FB4D03A630D21, PickPrompt, true);
                        Function.Call((Hash)0x71215ACCFDE075EE, PickPrompt, true);
                        active = true;
                    }

                    if (Function.Call<bool>((Hash)0xE0F65F0640EF0617, PickPrompt))
                    {
                        TriggerServerEvent("vorpinventory:onPickup", pick.Value["obj"]);
                        pick.Value["inRange"] = true;
                        Function.Call((Hash)0x8A0FB4D03A630D21, PickPrompt, false);
                        Function.Call((Hash)0x71215ACCFDE075EE, PickPrompt, false);
                    }
                }
                else
                {
                    if (active)
                    {
                        Function.Call((Hash)0x8A0FB4D03A630D21, PickPrompt, false);
                        Function.Call((Hash)0x71215ACCFDE075EE, PickPrompt, false);
                        active = false;
                    }
                }
            }
        }

        private async Task PrincipalFunctionPickupsMoneyAsync()
        {
            int playerPed = API.PlayerPedId();
            Vector3 coords = Function.Call<Vector3>((Hash)0xA86D5F069399F44D, playerPed, true, true);

            if (pickupsMoney.Count == 0)
            {
                await BaseScript.Delay(1000);
                return;
            }

            foreach (var pick in pickupsMoney)
            {
                float distance = Function.Call<float>((Hash)0x0BE7F4E3CDBAFB28, coords.X, coords.Y, coords.Z,
                    pick.Value["coords"].X,
                    pick.Value["coords"].Y, pick.Value["coords"].Z, false);

                if (distance <= 5.0F)
                {
                    string name = pick.Value["name"];
                    Utils.DrawText3DAsync(pick.Value["coords"], name);
                }

                if (distance <= 1.2F && !pick.Value["inRange"])
                {
                    Function.Call((Hash)0x69F4BE8C8CC4796C, playerPed, pick.Value["obj"], 3000, 2048, 3);
                    if (active2 == false)
                    {
                        //Debug.WriteLine("Entro");
                        Function.Call((Hash)0x8A0FB4D03A630D21, PickPrompt, true);
                        Function.Call((Hash)0x71215ACCFDE075EE, PickPrompt, true);
                        active2 = true;
                    }

                    if (Function.Call<bool>((Hash)0xE0F65F0640EF0617, PickPrompt))
                    {
                        TriggerServerEvent("vorpinventory:onPickupMoney", pick.Value["obj"]);
                        pick.Value["inRange"] = true;
                        Function.Call((Hash)0x8A0FB4D03A630D21, PickPrompt, false);
                        Function.Call((Hash)0x71215ACCFDE075EE, PickPrompt, false);
                    }
                }
                else
                {
                    if (active2)
                    {
                        Function.Call((Hash)0x8A0FB4D03A630D21, PickPrompt, false);
                        Function.Call((Hash)0x71215ACCFDE075EE, PickPrompt, false);
                        active2 = false;
                    }
                }
            }
        }

        private async void OnPlayerExitAnimationAsync()
        {
            string dict = "amb_work@world_human_box_pickup@1@male_a@stand_exit_withprop";
            Function.Call((Hash)0xA862A2AD321F94B4, dict);

            while (!Function.Call<bool>((Hash)0x27FF6FE8009B40CA, dict))
            {
                await Delay(10);
            }
            Function.Call((Hash)0xEA47FE3719165B94, API.PlayerPedId(), dict, "exit_front", 1.0, 8.0, -1, 1, 0, false, false, false);
            await Delay(1200);
            Function.Call((Hash)0x67C540AA08E4A6F5, "CHECKPOINT_PERFECT", "HUD_MINI_GAME_SOUNDSET", true, 1);
            await Delay(1000);
            Function.Call((Hash)0xE1EF3C1216AFF2CD, API.PlayerPedId());
        }

        private async void OnRemovePickupClientAsync(int obj)
        {
            Function.Call((Hash)0xDC19C288082E586E, obj, false, true);
            API.NetworkRequestControlOfEntity(obj);
            int timeout = 0;
            while (!API.NetworkHasControlOfEntity(obj) && timeout < 5000)
            {
                timeout += 100;
                if (timeout == 5000)
                {
                    Logger.Error("Control of the entity has not been obtained");
                }

                await Delay(100);
            }
            Function.Call((Hash)0x7D9EFB7AD6B19754, obj, false);
            API.DeleteObject(ref obj);
        }

        private void OnSharePickupClient(string name, int obj, int amount, Vector3 position, int value, int weaponId)
        {
            if (value == 1)
            {
                Debug.WriteLine(obj.ToString());
                pickups.Add(obj, new Dictionary<string, dynamic>
                {
                    ["name"] = name,
                    ["obj"] = obj,
                    ["amount"] = amount,
                    ["weaponid"] = weaponId,
                    ["inRange"] = false,
                    ["coords"] = position
                });
                Debug.WriteLine($"name: {pickups[obj]["name"].ToString()} cuantity: {pickups[obj]["amount"].ToString()},id:{pickups[obj]["weaponid"].ToString()}");
            }
            else
            {
                pickups.Remove(obj);
            }
        }

        private void OnShareMoneyPickupClient(int obj, double amount, Vector3 position, int value)
        {
            if (value == 1)
            {
                Debug.WriteLine(obj.ToString());
                pickupsMoney.Add(obj, new Dictionary<string, dynamic>
                {
                    ["name"] = "money",
                    ["obj"] = obj,
                    ["amount"] = amount,
                    ["inRange"] = false,
                    ["coords"] = position
                });
                Debug.WriteLine($"name: {pickupsMoney[obj]["name"].ToString()} cuantity: {pickupsMoney[obj]["amount"].ToString()},");
            }
            else
            {
                pickupsMoney.Remove(obj);
            }
        }

        private async void OnCreatePickupAsync(string name, int amount, int weaponId)
        {
            int ped = API.PlayerPedId();
            Vector3 coords = Function.Call<Vector3>((Hash)0xA86D5F069399F44D, ped, true, true);
            Vector3 forward = Function.Call<Vector3>((Hash)0x2412D9C05BB09B97, ped);
            Vector3 position = (coords + forward * 1.6F);

            if (dropAll)
            {
                Random rnd = new Random();
                float rn1 = (float)rnd.Next(-35, 35);
                float rn2 = (float)rnd.Next(-35, 35);
                position = new Vector3((lastCoords.X + (rn1 / 10.0f)), (lastCoords.Y + (rn2 / 10.0f)), lastCoords.Z);
            }

            if (!Function.Call<bool>((Hash)0x1283B8B89DD5D1B6, (uint)API.GetHashKey("P_COTTONBOX01X")))
            {
                Function.Call((Hash)0xFA28FE3A6246FC30, (uint)API.GetHashKey("P_COTTONBOX01X"));
            }

            while (!Function.Call<bool>((Hash)0x1283B8B89DD5D1B6, (uint)API.GetHashKey("P_COTTONBOX01X")))
            {
                await Delay(1);
            }

            int obj = Function.Call<int>((Hash)0x509D5878EB39E842, (uint)API.GetHashKey("P_COTTONBOX01X"), position.X
                , position.Y, position.Z, true, true, true);
            Function.Call((Hash)0x58A850EAEE20FAA3, obj);
            Function.Call((Hash)0xDC19C288082E586E, obj, true, false);
            Function.Call((Hash)0x7D9EFB7AD6B19754, obj, true);
            Debug.WriteLine(obj.ToString());
            TriggerServerEvent("vorpinventory:sharePickupServer", name, obj, amount, position, weaponId);
            Function.Call((Hash)0x67C540AA08E4A6F5, "show_info", "Study_Sounds", true, 0);
        }

        private async void OnCreateMoneyPickupAsync(double amount)
        {
            int ped = API.PlayerPedId();
            Vector3 coords = Function.Call<Vector3>((Hash)0xA86D5F069399F44D, ped, true, true);
            Vector3 forward = Function.Call<Vector3>((Hash)0x2412D9C05BB09B97, ped);
            Vector3 position = (coords + forward * 1.6F);

            if (dropAll)
            {
                Random rnd = new Random();

                position = new Vector3((lastCoords.X + (float)rnd.Next(-3, 3)), (lastCoords.Y + (float)rnd.Next(-3, 3)), lastCoords.Z);
            }

            if (!Function.Call<bool>((Hash)0x1283B8B89DD5D1B6, (uint)API.GetHashKey("p_moneybag02x")))
            {
                Function.Call((Hash)0xFA28FE3A6246FC30, (uint)API.GetHashKey("p_moneybag02x"));
            }

            while (!Function.Call<bool>((Hash)0x1283B8B89DD5D1B6, (uint)API.GetHashKey("p_moneybag02x")))
            {
                await Delay(1);
            }

            int obj = Function.Call<int>((Hash)0x509D5878EB39E842, (uint)API.GetHashKey("p_moneybag02x"), position.X
                , position.Y, position.Z, true, true, true);
            Function.Call((Hash)0x58A850EAEE20FAA3, obj);
            Function.Call((Hash)0xDC19C288082E586E, obj, true, false);
            Function.Call((Hash)0x7D9EFB7AD6B19754, obj, true);
            Debug.WriteLine(obj.ToString());
            TriggerServerEvent("vorpinventory:shareMoneyPickupServer", obj, amount, position);
            Function.Call((Hash)0x67C540AA08E4A6F5, "show_info", "Study_Sounds", true, 0);
        }

        public static void SetupPickPrompt()
        {
            PickPrompt = Function.Call<int>((Hash)0x04F97DE45A519419);
            long str = Function.Call<long>(Hash._CREATE_VAR_STRING, 10, "LITERAL_STRING", Configuration.GetTranslation("TakeFromFloor"));
            Function.Call((Hash)0x5DD02A8318420DD7, PickPrompt, str);
            Function.Call((Hash)0xB5352B7494A08258, PickPrompt, Configuration.KEY_PICKUP_ITEM);
            Function.Call((Hash)0x8A0FB4D03A630D21, PickPrompt, false);
            Function.Call((Hash)0x71215ACCFDE075EE, PickPrompt, false);
            Function.Call((Hash)0x94073D5CA3F16B7B, PickPrompt, true);
            Function.Call((Hash)0xF7AA2696A22AD8B9, PickPrompt);
        }
    }
}