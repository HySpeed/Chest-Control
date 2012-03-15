﻿using System;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using Hooks;
using TShockAPI;
using Terraria;
using System.Reflection;

namespace ChestControl
{
    [APIVersion(1, 11)]
    public class ChestControl : TerrariaPlugin
    {
        private static bool Init;
        private static string datetimeString = string.Format("{0:yyyy-MM-dd_hh-mm-ss}",DateTime.Now);
        public static readonly string ChestLogPath = Path.Combine( TShock.SavePath, "ChestControl_" + datetimeString + ".log" );
        public static CPlayer[] Players = new CPlayer[Main.maxNetPlayers];
        public static ChestDbManager chestDbManager;
        internal static readonly Version VersionNum = Assembly.GetExecutingAssembly().GetName().Version;

        public ChestControl(Main game)
            : base(game)
        {
            Order = 10;
        }

        public override string Name
        {
            get { return "Chest Control"; }
        }

        public override Version Version
        {
            get { return VersionNum; }
        }

        public override string Author
        {
            get { return "Deathmax, Natrim, _Jon"; }
        }

        public override string Description
        {
            get { return "Gives you control over chests."; }
        }

        public override void Initialize()
        {
            GameHooks.Initialize += OnInitialize;
            NetHooks.GetData += NetHooks_GetData;
            ServerHooks.Leave += ServerHooks_Leave;
            GameHooks.Update += OnUpdate;
            WorldHooks.SaveWorld += OnSaveWorld;
        }

        protected override void Dispose(bool disposing)
        {
            GameHooks.Initialize -= OnInitialize;
            NetHooks.GetData -= NetHooks_GetData;
            ServerHooks.Leave -= ServerHooks_Leave;
            GameHooks.Update -= OnUpdate;
            WorldHooks.SaveWorld -= OnSaveWorld;

            base.Dispose(disposing);
        }


        public void OnInitialize()
        {
          Log.Initialize( ChestLogPath, false );
          chestDbManager = new ChestDbManager( TShock.DB );
        }


        private void OnSaveWorld(bool resettime, HandledEventArgs e)
        {
            try
            {
              chestDbManager.SaveChests(); //save chests
            }
            catch (Exception ex) //we don't want the world to fail to save.
            {
                Log.Write(ex.ToString(), LogLevel.Error);
            }
        }

        private void OnUpdate()
        {
            if (Init) return;
            chestDbManager.LoadChests();
            Commands.Load();
            new Thread(UpdateChecker).Start();
            for (int i = 0; i < Players.Length; i++)
                Players[i] = new CPlayer(i);
            Init = true;
        }

        private void ServerHooks_Leave(int obj)
        {
            Players[obj] = new CPlayer(obj);
        }

        private void NetHooks_GetData(GetDataEventArgs e)
        {
            switch (e.MsgID)
            {
                case PacketTypes.ChestGetContents:
                    if (!e.Handled)
                        using (var data = new MemoryStream(e.Msg.readBuffer, e.Index, e.Length))
                         try {
                            var reader = new BinaryReader(data);
                            int x = reader.ReadInt32();
                            int y = reader.ReadInt32();
                            reader.Close();
                            int id = Terraria.Chest.FindChest(x, y);
                            CPlayer player = Players[e.Msg.whoAmI];
                            TSPlayer tplayer = TShock.Players[e.Msg.whoAmI];
                            if (id != -1)
                            {
                                Chest chest = chestDbManager.GetChest( id );
                                bool naggedAboutLock = false;

                                switch (player.GetState())
                                {
                                    case SettingState.Setting:
                                        if (chest.HasOwner())
                                            if (chest.IsOwnerConvert(player))
                                                player.SendMessage("You already own this chest!", Color.Red);
                                            else
                                            {
                                                player.SendMessage("This chest is already owned by someone!", Color.Red);
                                                naggedAboutLock = true;
                                            }
                                        else
                                        {
                                            chest.SetID(id);
                                            chest.SetPosition(x, y);
                                            chest.SetOwner(player);
                                            chest.Lock();

                                            player.SendMessage("This chest is now yours, and yours only.", Color.Red);
                                        }

                                        //end player setting
                                        player.SetState(SettingState.None);
                                        break;

                                    case SettingState.RegionSetting:
                                        if (chest.HasOwner())
                                            if (chest.IsOwnerConvert(player))
                                                if (chest.IsRegionLocked())
                                                {
                                                    chest.regionLock(false);

                                                    player.SendMessage(
                                                        "Region share disabled. This chest is now only yours. To fully remove protection use \"cunset\".",
                                                        Color.Red);
                                                }
                                                else if (TShock.Regions.InArea(x, y))
                                                {
                                                    chest.regionLock(true);

                                                    player.SendMessage(
                                                        "This chest is now shared between region users. Use this command again to disable it.",
                                                        Color.Red);
                                                }
                                                else
                                                    player.SendMessage(
                                                        "You can region share chest only if the chest is inside region!",
                                                        Color.Red);
                                            else
                                            {
                                                player.SendMessage("This chest isn't yours!", Color.Red);
                                                naggedAboutLock = true;
                                            }
                                        else if (TShock.Regions.InArea(x, y))
                                        {
                                            chest.SetID(id);
                                            chest.SetPosition(x, y);
                                            chest.SetOwner(player);
                                            chest.Lock();
                                            chest.regionLock(true);

                                            player.SendMessage(
                                                "This chest is now shared between region users with you as owner. Use this command again to disable region sharing (You will still be owner).",
                                                Color.Red);
                                        }
                                        else
                                            player.SendMessage(
                                                "You can region share chest only if the chest is inside region!",
                                                Color.Red);

                                        //end player setting
                                        player.SetState(SettingState.None);
                                        break;

                                    case SettingState.PublicSetting:
                                        if (chest.HasOwner())
                                            if (chest.IsOwnerConvert(player))
                                                if (chest.IsLocked())
                                                {
                                                    chest.UnLock();
                                                    player.SendMessage(
                                                        "This chest is now public! Use \"/cpset\" to set it private.",
                                                        Color.Red);
                                                }
                                                else
                                                {
                                                    chest.Lock();
                                                    player.SendMessage(
                                                        "This chest is now private! Use \"/cunset\" to set it public.",
                                                        Color.Red);
                                                }
                                            else
                                            {
                                                player.SendMessage("This chest isn't yours!", Color.Red);
                                                naggedAboutLock = true;
                                            }
                                        else
                                        {
                                            chest.SetID(id);
                                            chest.SetPosition(x, y);
                                            chest.SetOwner(player);

                                            player.SendMessage(
                                                "This chest is now yours. This chest is public. Use \"/cpset\" to set it private.",
                                                Color.Red);
                                        }
                                        break;

                                    case SettingState.Deleting:
                                        if (chest.HasOwner())
                                            if (chest.IsOwnerConvert(player) ||
                                                tplayer.Group.HasPermission("removechestprotection"))
                                            {
                                                chest.Reset();
                                                player.SendMessage("This chest is no longer yours!", Color.Red);
                                            }
                                            else
                                            {
                                                player.SendMessage("This chest isn't yours!", Color.Red);
                                                naggedAboutLock = true;
                                            }
                                        else
                                            player.SendMessage("This chest is not protected!", Color.Red);

                                        //end player setting
                                        player.SetState(SettingState.None);
                                        break;

                                    case SettingState.PasswordSetting:
                                        if (chest.HasOwner())
                                            if (chest.IsOwnerConvert(player))
                                            {
                                                chest.SetPassword(player.PasswordForChest);
                                                player.SendMessage("This chest is now protected with password.",
                                                    Color.Red);
                                            }
                                            else
                                            {
                                                player.SendMessage("This chest isn't yours!", Color.Red);
                                                naggedAboutLock = true;
                                            }
                                        else
                                        {
                                            chest.SetID(id);
                                            chest.SetPosition(x, y);
                                            chest.SetOwner(player);
                                            chest.Lock();
                                            chest.SetPassword(player.PasswordForChest);

                                            player.SendMessage(
                                                "This chest is now protected with password, with you as owner.",
                                                Color.Red);
                                        }

                                        //end player setting
                                        player.SetState(SettingState.None);
                                        break;

                                    case SettingState.PasswordUnSetting:
                                        if (chest.HasOwner())
                                            if (chest.IsOwnerConvert(player))
                                            {
                                                chest.SetPassword("");
                                                player.SendMessage("This chest password has been removed.", Color.Red);
                                            }
                                            else
                                            {
                                                player.SendMessage("This chest isn't yours!", Color.Red);
                                                naggedAboutLock = true;
                                            }
                                        else
                                            player.SendMessage("This chest is not protected!", Color.Red);

                                        //end player setting
                                        player.SetState(SettingState.None);
                                        break;

                                    case SettingState.RefillSetting:
                                        if (chest.HasOwner())
                                            if (chest.IsOwnerConvert(player))
                                            {
                                              chest.SetRefill( true );
                                              if ( player.RefillDelay == 0 )
                                              {
                                                player.SendMessage( "This chest will now refill with items.", Color.Red );
                                              } // if
                                              else
                                              {
                                                chest.SetRefillDelay( player.RefillDelay );
                                                player.SendMessage( "This chest will now refill with items after a " + player.RefillDelay + " second delay.", Color.Red );
                                                player.RefillDelay = 0;
                                              } // else
                                            }
                                            else
                                            {
                                                player.SendMessage("This chest isn't yours!", Color.Red);
                                                naggedAboutLock = true;
                                            }
                                        else
                                        {
                                          chest.SetID( id );
                                          chest.SetPosition( x, y );
                                          chest.SetOwner( player );
                                          chest.SetRefill( true );
                                          if ( player.RefillDelay == 0 )
                                            player.SendMessage( "This chest will now refill with items, with you as owner.", Color.Red );
                                          else
                                          {
                                            chest.SetRefillDelay( player.RefillDelay );
                                            player.SendMessage( "This chest will now refill with items after a " + player.RefillDelay + " second delay, with you as owner.", Color.Red );
                                            player.RefillDelay = 0;
                                          } // else
                                        } // else

                                        // end player setting
                                        player.SetState(SettingState.None);
                                        break;

                                    case SettingState.RefillUnSetting:
                                        if (chest.IsRefill())
                                            if (chest.HasOwner())
                                                if (chest.IsOwnerConvert(player))
                                                {
                                                    chest.SetRefill(false);
                                                    player.SendMessage(
                                                        "This chest will no longer refill with items.", Color.Red);
                                                }
                                                else
                                                {
                                                    player.SendMessage("This chest isn't yours!", Color.Red);
                                                    naggedAboutLock = true;
                                                }
                                            else
                                            {
                                                chest.SetID(id);
                                                chest.SetPosition(x, y);
                                                chest.SetOwner(player);
                                                chest.SetRefill(false);

                                                player.SendMessage("This chest will no longer refill with items", Color.Red);
                                            }
                                        else
                                            player.SendMessage("This chest is not refilling!", Color.Red);

                                        //end player setting
                                        player.SetState(SettingState.None);
                                        break;

                                    case SettingState.UnLocking:
                                        if (chest.HasOwner())
                                            if (chest.IsLocked())
                                                if (chest.GetPassword() == "")
                                                {
                                                    player.SendMessage("This chest can't be unlocked with password!", Color.Red);
                                                    naggedAboutLock = true;
                                                }
                                                else if (chest.IsOwnerConvert(player))
                                                    player.SendMessage(
                                                        "You are owner of this chest, you dont need to unlock it. If you want to remove password use \"/lockchest remove\".",
                                                        Color.Red);
                                                else if (player.HasAccessToChest(chest.GetID()))
                                                    player.SendMessage("You already have access to this chest!",
                                                        Color.Red);
                                                else if (chest.CheckPassword(player.PasswordForChest))
                                                {
                                                    player.UnlockedChest(chest.GetID());
                                                    player.SendMessage(
                                                        "Chest unlocked! When you leave game you must unlock it again.",
                                                        Color.Red);
                                                }
                                                else
                                                {
                                                    player.SendMessage("Wrong password for chest!", Color.Red);
                                                    naggedAboutLock = true;
                                                }
                                            else
                                                player.SendMessage("This chest is not locked!", Color.Red);
                                        else
                                            player.SendMessage("This chest is not protected!", Color.Red);

                                        //end player setting
                                        player.SetState(SettingState.None);
                                        break;
                                } // switch - (player.GetState())

                                if (tplayer.Group.HasPermission("showchestinfo")) //if player should see chest info
                                    player.SendMessage(
                                        string.Format(
                                            "Chest Owner: {0} || Public: {1} || RegionShare: {2} || Password: {3} || Refill: {4} || Delay: {5}",
                                            chest.GetOwner() == "" ? "-None-" : chest.GetOwner(),
                                            chest.IsLocked() ? "No" : "Yes", 
                                            chest.IsRegionLocked() ? "Yes" : "No",
                                            chest.GetPassword() == "" ? "No" : "Yes",
                                            chest.IsRefill() ? "Yes" : "No",
                                            chest.GetRefillDelay() ), Color.Yellow );

                                if (!tplayer.Group.HasPermission("openallchests") && !chest.IsOpenFor(player))
                                { // if player doesnt has permission to see inside chest, then send message and break
                                    e.Handled = true;
                                    if (!naggedAboutLock)
                                        player.SendMessage( chest.GetPassword() != ""
                                                ? "This chest is magically locked with password. ( Use \"/cunlock PASSWORD\" to unlock it. )"
                                                : "This chest is magically locked.", Color.IndianRed);
                                    return;
                                } // if

// +++++++++++++++++++++++++++++++++++++++++++++++++++
                                if ( chest.IsOpenFor( player ) )
                                  if ( chest.IsRefill() )
                                    if ( chest.GetRefillDelay() > 0 )
                                    {
                                      //player.SendMessage( "This chest has a refill delay of " + chest.GetRefillDelay() + " seconds", Color.Gold );
                                      if ( chest.HasRefillTimerExpired() ) // delay has elapsed, refill chest
                                      {
                                        chest.RefillChest();
                                        //Players[e.Msg.whoAmI].SendMessage( "Refilled", Color.Green );
                                      } // if
                                      else
                                        if ( chest.GetRefillDelayRemaining() == chest.GetRefillDelay() )
                                          Players[e.Msg.whoAmI].SendMessage( "This chest has a refill delay of " + chest.GetRefillDelay() + " seconds", Color.Blue );
                                        else
                                          Players[e.Msg.whoAmI].SendMessage( "Refill will occur in " + chest.GetRefillDelayRemaining() + " seconds", Color.Orange );
                                    } // if
                                    //else // immediate refill
                                      //player.SendMessage( "This chest refills immediately", Color.Green );
                                  //else
                                    //player.SendMessage( "You can open this chest", Color.White );

                            } // if (id != -1)

                            if (player.GetState() != SettingState.None)
                                //if player is still setting something - end his setting
                                player.SetState(SettingState.None);
                      } // try using MemoryStream
                      catch ( Exception ex )
                      {
                        Log.Write( ex.ToString(), LogLevel.Error );
                      }
                    break;
// ---------------------------------------------------------------------------------------
                case PacketTypes.ChestItem: // this occurs when a player grabs item from or puts item into chest
                    using (var data = new MemoryStream(e.Msg.readBuffer, e.Index, e.Length))
                      try {
                        var reader = new BinaryReader(data);
                        short id = reader.ReadInt16();
                        byte slot = reader.ReadByte();
                        byte stack = reader.ReadByte();
                        byte prefix = reader.ReadByte();
                        short type = reader.ReadInt16(); // formerly ReadByte()
                        reader.Close();

                        if (id != -1)
                        {
                            Chest chest = chestDbManager.GetChest( id );
                            if ( chest.IsRefill() )  
                            {
                              if ( chest.GetRefillDelay() > 0 )
                              {
                                //Players[e.Msg.whoAmI].SendMessage( "This chest refills after a delay of " + chest.GetRefillDelay() + " seconds", Color.Violet );
                                chest.StartRefillDelay();
                              } // if
                              else // no refill delay - ignore action
                              {
                                /* Setting e.Handled prevents TShock / Terraria from processing this message
                                 * In this case, it causes Terraria to ignore chest interactions (takes from & adds to)
                                 */
                                //Players[e.Msg.whoAmI].SendMessage( "Chest refilled", Color.Thistle );
                                e.Handled = true;
                              } // else
                            } // if ( chest.GetRefillDelay() > 0 )

                            //if (!e.Handled)
                            //{
                            //    var item = Main.chest[id].item[slot];
                            //    var newitem = new Item();
                            //    newitem.netDefaults(type);
                            //    newitem.Prefix(prefix);
                            //    newitem.AffixName();
                            //    String playerName = (string.IsNullOrEmpty( TShock.Players[e.Msg.whoAmI].UserAccountName )) ? Players[e.Msg.whoAmI].Name : TShock.Players[e.Msg.whoAmI].UserAccountName;
                            //    Log.Write( string.Format( "Item {0}({1}) in slot {2} in chest at {3}x{4} was modified to {5}({6}) by {7}",
                            //        item.name, item.stack, slot, Main.chest[id].x, Main.chest[id].y, newitem.name, stack, playerName ),
                            //        LogLevel.Info, false );
                            //} // if (!e.Handled)

                        } // if (id != -1)
                      } // try using MemoryStream
                      catch ( Exception ex )
                      {
                        Log.Write( ex.ToString(), LogLevel.Error );
                      }
                    break;
// ===========================================================
                case PacketTypes.TileKill:
                case PacketTypes.Tile:
                    using ( var data = new MemoryStream( e.Msg.readBuffer, e.Index, e.Length ) )
                      try
                      {
                        var reader = new BinaryReader( data );
                        if ( e.MsgID == PacketTypes.Tile )
                        {
                          byte type = reader.ReadByte();
                          if ( !(type == 0 || type == 4) )
                            return;
                        }
                        int x = reader.ReadInt32();
                        int y = reader.ReadInt32();
                        reader.Close();

                        if ( Chest.TileIsChest( x, y ) ) //if is Chest
                        {
                          int id = Terraria.Chest.FindChest( x, y );
                          CPlayer player = Players[e.Msg.whoAmI];
                          TSPlayer tplayer = TShock.Players[e.Msg.whoAmI];

                          //dirty fix for finding chest, try to find chest point around
                          if ( id == -1 )
                            try
                            {
                              id = Terraria.Chest.FindChest( x - 1, y ); //search one tile left
                              if ( id == -1 )
                              {
                                id = Terraria.Chest.FindChest( x - 1, y - 1 );
                                //search one tile left and one tile up
                                if ( id == -1 )
                                  id = Terraria.Chest.FindChest( x, y - 1 ); //search one tile up
                              }
                            }
                            catch ( Exception ex )
                            {
                              Log.Write( ex.ToString(), LogLevel.Error );
                            }

                          if ( id != -1 ) //if have found chest
                          {
                            Chest chest = chestDbManager.GetChest( id );
                            if ( chest.HasOwner() ) //if owned stop removing
                            {
                              if ( tplayer.Group.HasPermission( "removechestprotection" ) ||
                                  chest.IsOwnerConvert( player ) )
                                //display more verbose info to player who has permission to remove protection on this chest
                                player.SendMessage( "This chest is protected. To remove it, first remove protection using \"/cunset\" command.", Color.Red );
                              else
                                player.SendMessage( "This chest is protected!", Color.Red );

                              player.SendTileSquare( x, y );
                              e.Handled = true;
                            }
                          }
                        }
                      }
                      catch ( Exception ex )
                      {
                        Log.Write( ex.ToString(), LogLevel.Error );
                      }
                    break;
            } // switch
        }


        //private void RefillChest( Chest chest, int chestId )
        //{
        //  //Players[playerId].SendMessage( "Refilled", Color.Green );
        //  Main.chest[chestId].item = chest.GetRefillItems();
        //  chest.SetRefillDelayRemaining( chest.GetRefillDelay() );  // reset delay
        //} // ResetChest


        private void UpdateChecker()
        {
            string raw;
            try
            {
                raw = new WebClient().DownloadString("https://github.com/Deathmax/Chest-Control/raw/master/version.txt");
            }
            catch (Exception)
            {
                return;
            }
            string[] list = raw.Split('\n');
            Version version;
            if (!Version.TryParse(list[0], out version)) return;
            if (Version.CompareTo(version) >= 0) return;
            TShock.Utils.Broadcast(string.Format("New Chest-Control version : {0}", version), Color.Yellow);
            if (list.Length > 1)
                for (int i = 1; i < list.Length; i++)
                    TShock.Utils.Broadcast(list[i], Color.Yellow);
            TShock.Utils.Broadcast("Get the CC download at bit.ly/chestcontroldl", Color.Yellow);
        }
    }
}