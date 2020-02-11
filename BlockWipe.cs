using UnityEngine;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using System;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("BlockWipe", "Kaidoz", "1.0.4")]
    [Description("Блокировка некоторых возможностей в первое время вайпа")]

    class BlockWipe : HurtworldPlugin
    {
        Configuration _config;

        public class Configuration
        {
            [JsonProperty("Plugin enabled")]
            public bool enable;

            [JsonProperty("Kit list")]
            public List<Kit_Block> kitlist = new List<Kit_Block>();

            [JsonProperty("Item List")]
            public List<Item_Block> itemlist; // = new List<Item_Block>();

            [JsonProperty("Exclude admins")]
            public bool excadmins;

            [JsonProperty("Exclude players")]
            public List<ulong> exclude = new List<ulong>();

            public class Item_Block
            {
                [JsonProperty("Name")]
                public string ItemName { get; set; }

                [JsonProperty("Time To End")]
                public string Time { get; set; }

                public Item_Block(string item, string t)
                {
                    this.ItemName = item;
                    this.Time = t;
                }
            }

            public class Kit_Block
            {
                [JsonProperty("Name")]
                public string KitName { get; set; }

                [JsonProperty("Time To End")]
                public string Time { get; set; }

                public Kit_Block(string kit, string t)
                {
                    this.KitName = kit;
                    this.Time = t;
                }
            }
        }

        [HookMethod("LoadDefaultMessages")]
        private void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"timeblock","物品 {item} 被锁定需要等待 {timemsg}"},
                {"timeleft","<color=lime>[❀]</color> 礼包 <color=red>{kitname}</color> 被锁定需要等待 <color=red>{timemsg}</color>"},
                {"timemsg_hours","需要等待 {duration_hours} 小时 {duration_min} 分钟"},
                {"timemsg_minutes","需要等待 {duration} 分钟"},
                {"timemsg_seconds","还需要等待不到一分钟"}
            };

            lang.RegisterMessages(messages, this);
        }

        private new void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);


        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating config file for BlockWipe...");
            _config = new Configuration()
            {
                enable = true,
                excadmins = true,
                kitlist = new List<Configuration.Kit_Block>()
                {
                    new Configuration.Kit_Block("vip","09.02.19 18:00")
                },
                itemlist = new List<Configuration.Item_Block>
                {
                    new Configuration.Item_Block("C4","12.02.20 17:00"),
                    new Configuration.Item_Block("Beancangrenade","12.02.20 17:00"),
					new Configuration.Item_Block("Explosive Device","12.02.20 17:00"),
                    new Configuration.Item_Block("RaidDrill","12.02.20 17:00"),
                    new Configuration.Item_Block("RaidLauncher","12.02.20 17:00"),
                    new Configuration.Item_Block("DynamiteBomb","12.02.20 17:00"),
                    new Configuration.Item_Block("AR15","11.02.20 12:00"),
                    new Configuration.Item_Block("AK47","11.02.20 12:00"),
                    new Configuration.Item_Block("Shotgun","11.02.20 12:00"),
                    new Configuration.Item_Block("AWM","11.02.20 12:00"),
					new Configuration.Item_Block("MP5","10.02.20 20:00"),
					new Configuration.Item_Block("Mac10","10.02.20 20:00")
                },
                exclude = new List<ulong>()
                {
                    76561198874111111
                }
            };
            SaveConfig();
        }

        [HookMethod("Loaded")]
        private void Loaded()
        {
            LoadConfig();
        }

        [HookMethod("OnItemSelected")]
        private void OnItemSelected(ItemObject item, EquippedHandlerServer qhs)
        {
            if (!_config.enable)
                return;

            PlayerSession session = GameManager.Instance.GetSession(qhs.HNetworkView().owner);

            if (_config.exclude.Contains((ulong)session.SteamId))
                return;

            Inventory inv = qhs.GetStorage();
            var find = (from x in _config.itemlist where x.ItemName == item.Generator.name select x);
            if (find.Count() == 0)
                return;

            int duration = getDuration(find.First().Time);
            if (duration <= 0)
                return;

            int slot = qhs.GetEquippedSlotNumber();

            string timemsg = string.Empty;
            switch (duration)
            {
                case 0:
                    timemsg = lang.GetMessage("timemsg_seconds", this, getsteamid(session));
                    break;
                default:
                    if (duration > 60)
                    {
                        timemsg = Msg("timemsg_hours", getsteamid(session)).Replace("{duration_hours}", (duration / 60).ToString()).Replace("{duration_min}", (duration % 60).ToString());
                    }
                    else
                        timemsg = Msg("timemsg_minutes", getsteamid(session)).Replace("{duration}", duration.ToString());
                    break;
            }

            if (!ReturnItemInInventory(session, slot))
                inv.DropSlot(slot, Vector3.up);
            Alert(Msg("timeblock", getsteamid(session)).Replace("{timemsg}", timemsg).Replace("{item}", find.FirstOrDefault().ItemName), session);
        }

        [HookMethod("CanRedeemKit")]
        object CanRedeemKit(PlayerSession session, string kitName)
        {
            if (!_config.enable)
                return null;

            if (_config.exclude.Contains((ulong)session.SteamId))
                return null;

            var find = (from x in _config.kitlist where x.KitName.ToLower() == kitName.ToLower() select x);
            if (find.Count() == 0)
                return null;

            int duration = getDuration(find.First().Time);
            if (duration <= 0)
                return null;


            string timemsg = string.Empty;
            switch (duration)
            {
                case 0:
                    timemsg = lang.GetMessage("timemsg_seconds", this, getsteamid(session));
                    break;
                default:
                    if (duration > 60)
                    {
                        timemsg = Msg("timemsg_hours", getsteamid(session)).Replace("{duration_hours}", (duration / 60).ToString()).Replace("{duration_min}", (duration % 60).ToString());
                    }
                    else
                        timemsg = Msg("timemsg_minutes", getsteamid(session)).Replace("{duration}", duration.ToString());
                    break;
            }

            Alert(Msg("timeleft", getsteamid(session)).Replace("{timemsg}", timemsg).Replace("{item}", find.FirstOrDefault().KitName), session);

            return false;
        }

        [ConsoleCommand("blockwipe")]
        private void command(string command, string[] args)
        {
            if (args.Length == 1)
            {
                switch (args[0].ToLower())
                {
                    // для защиты
                    case "{DarkPluginsId}":
                    case "off":
                    case "false":
                        _config.enable = false;
                        break;
                    case "on":
                    case "true":
                        _config.enable = true;
                        break;
                }
                SaveConfig();
                LoadConfig();
            }
            else
                _config.enable = !_config.enable;
            Puts("Set status enable: " + _config.enable);
        }


        bool ReturnItemInInventory(PlayerSession session, int slot)
        {
            var inventory = session.WorldPlayerEntity.GetComponent<Inventory>();
            var item = inventory.GetSlot(slot);
            inventory.AutoMoveItem(inventory, slot, 16, 100);
            if (inventory.GetSlot(slot) == item)
                return false;

            return true;
        }

        #region Helper

        int getDuration(string date)
        {
            DateTime dateTime = DateTime.ParseExact(date, "dd.MM.yy HH:mm", null);
            TimeSpan ts = dateTime.Subtract(DateTime.Now);
            int duration = (int)ts.TotalMinutes;
            return duration;
        }

        void Alert(string msg, PlayerSession session)
        {
            AlertManager.Instance.GenericTextNotificationServer(msg, session.Player);
        }

        void sendchatmessage(PlayerSession session, string msg)
        {
            hurt.SendChatMessage(session, null, msg);
        }

        string getdate()
        {
            return DateTime.Now.ToString("dd.MM.yy");
        }

        string gettime()
        {
            return DateTime.Now.ToShortTimeString();
        }

        string getsteamid(PlayerSession session)
        {
            return session.SteamId.ToString();
        }

        string Msg(string msg, string SteamId = null)
        {
            return lang.GetMessage(msg, this, SteamId);
        }

        #endregion

    }
}
