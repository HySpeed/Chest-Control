using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TShockAPI;
using Terraria;
using System;
using System.Diagnostics;

namespace ChestControl
{
    internal class Chest
    {
        private   string HashedPassword;
        protected int chestId;
        protected bool Locked;
        protected string Owner;
        protected Vector2 Position;
        protected bool Refill;
        protected Item[] RefillItems;
        protected bool RegionLock;
        protected int WorldID;
        protected int RefillDelayLength;
        protected int RefillDelayRemaining;
        protected Stopwatch stopWatch = new Stopwatch();

        public Chest()
        {
            chestId = -1;
            WorldID = Main.worldID;
            Owner = "";
            Position = new Vector2(0, 0);
            Locked = false;
            RegionLock = false;
            Refill = false;
            HashedPassword = "";
            RefillItems = new Item[20];
            RefillDelayLength = 0;
            RefillDelayRemaining = 0;
        }

        public void Reset()
        {
            Owner = "";
            Locked = false;
            RegionLock = false;
            Refill = false;
            HashedPassword = "";
            RefillItems = new Item[20];
            RefillDelayLength = 0;
            RefillDelayRemaining = 0;
        }

        public void SetID(int id)
        {
            chestId = id;
        }

        public int GetID()
        {
            return chestId;
        }

        public void SetOwner(string player)
        {
            Owner = player;
        }

        public void SetOwner(CPlayer player)
        {
            string userAccountName = TShock.Players[player.Index].UserAccountName;
            if (userAccountName != null)
                Owner = userAccountName; //player.Name;
            else
            {
                Owner = TShock.Players[player.Index].Name;
                player.SendMessage("Warning, you are not registered.", Color.Red);
                //player.SendMessage("Please register an account and open the chest again to future-proof your protection.", Color.Red);
            }
        }

        public string GetOwner()
        {
            return Owner;
        }

        public void SetPosition(Vector2 position)
        {
            Position = position;
        }

        public void SetPosition(int x, int y)
        {
            Position = new Vector2(x, y);
        }

        public Vector2 GetPosition()
        {
            return Position;
        }

        public void Lock()
        {
            Locked = true;
        }

        public void UnLock()
        {
            Locked = false;
        }

        public void regionLock(bool locking)
        {
            RegionLock = locking;
        }

        public bool HasOwner()
        {
            return Owner != "";
        }

        public bool IsOwner(CPlayer player)
        {
            return HasOwner() && Owner.Equals(TShock.Players[player.Index].UserAccountName);
        }

        public bool LegacyIsOwner(CPlayer player)
        {
            return HasOwner() && Owner.Equals(TShock.Players[player.Index].Name);
        }

        public bool IsOwnerConvert(CPlayer player)
        {
            if (LegacyIsOwner(player) && !IsOwner(player))
            {
                SetOwner(player);
                return true;
            }
            return IsOwner(player);
        }

        public bool IsLocked()
        {
            return Locked;
        }

        public bool IsRegionLocked()
        {
            return RegionLock;
        }

        public bool IsRefill()
        {
            return Refill;
        }

        public void SetRefill(bool refill)
        {
            Refill = refill;
            if ( refill )
            {
              SetRefillItems();
            } // if
            else
            {
              RefillItems = new Item[20];
            } // else
            RefillDelayLength    = 0;
            RefillDelayRemaining = 0;
            stopWatch.Stop();
            stopWatch.Reset();
        }

        public Item[] GetRefillItems()
        {
            return RefillItems;
        }

        public List<string> GetRefillItemNames()
        {
            List<string> list = (from t in RefillItems
                where t != null
                where !string.IsNullOrEmpty(t.name)
                select t.name + "=" + t.stack).ToList();
            if (list.Count == 0)
                list.Add("");
            return list;
        }

        /* This creates an 'unlinked' copy because simply doing this:
         *  RefillItems = Main.chest[ID].item;
         *  will 'link' the objects. That results in changes 
         *  to Main.chest[ID].item being passed on to RefillItems. :(
         */
        public void SetRefillItems()
        {
          RefillItems = DeepCopyItems( Main.chest[chestId].item );
        }

        // Not used - refill items are pulled from inventory when refill is set
        //public void SetRefillItems( string raw ) {}

        // Not used
        //public void SetChestItems(Terraria.Item[] items) { Terraria.Main.chest[ID].item = items; }

        public void SetRefillDelay( int delay )
        {
          RefillDelayLength    = delay;
          RefillDelayRemaining = delay;
        }

        public int GetRefillDelay()
        {
          return RefillDelayLength;
        }

        /* If the countdown has not begun, start it */
        public void StartRefillDelay()
        {
          //if ( GetRefillDelayRemaining() == GetRefillDelay() )
          if ( !stopWatch.IsRunning )
          {
            //Log.Write( "Starting Delay (id:" + ID + ")" + "[" + RefillItems[0].name + "]" + "{" + GetRefillDelayRemaining() + "}", LogLevel.Debug );
            stopWatch.Reset();
            stopWatch.Start();
          } // if
        }

        public void SetRefillDelayRemaining( int remaining )
        {
          RefillDelayRemaining = remaining;
        }

        public int GetRefillDelayRemaining()
        {
          int result = 0;
          TimeSpan ts = stopWatch.Elapsed;
          int min = ts.Minutes;
          int sec = ts.Seconds;
          int elapsed = (min * 60) + sec;

          result = RefillDelayRemaining - elapsed; // ? can this be replaced with RefillDelayLength?
          //Log.Write( "RDR (id:" + ID + ")" + "[" + RefillItems[0].name + "]" + "{" + result + "}", LogLevel.Debug );
          return result;
        }


        public bool HasRefillTimerExpired()
        {
          bool result = false;
          if ( GetRefillDelayRemaining() <= 0 )
            result = true;
          return result;
        }


        public void RefillChest()
        {
          if ( RefillItems.Length > 0 ) 
          {
            Log.Write( "Refill (id:" + chestId + ")" + "[" + RefillItems[0].name + "]", LogLevel.Info );
            Main.chest[chestId].item = DeepCopyItems( RefillItems );
            stopWatch.Stop();
            stopWatch.Reset();
            SetRefillDelayRemaining( GetRefillDelay() );  // reset delay
          } // if
        }

        private Item[] DeepCopyItems( Item[] source )
        {
          Item[] result = null;

          if ( source.Length > 0 )
          {
            result = new Item[source.Length];
            for ( int i = 0; i < source.Length; i++ )
            {
              var oldItem = source[i];
              var newItem = new Item();
              newItem.netDefaults( oldItem.netID );
              newItem.Prefix( oldItem.prefix );
              newItem.AffixName();
              //if ( oldItem.type != 0 )
              //  Log.Write( "DCI (id:" + ID + ") " + oldItem.name + "," + newItem.name + "," + oldItem.netID + "," + newItem.netID, LogLevel.Debug );

              result[i] = newItem;
              result[i].stack = oldItem.stack;
            } // for
          } // if

          return result;
        }

        public bool IsOpenFor( CPlayer player )
        {
            if (!IsLocked()) //if chest not locked skip all checks
                return true;

            if (!TShock.Players[player.Index].IsLoggedIn) //if player isn't logged in, and chest is protected, don't allow access
                return false;

            if (IsOwnerConvert(player)) //if player is owner then skip checks
                return true;

            if (HashedPassword != "") //this chest is passworded, so check if user has unlocked this chest
                if (player.HasAccessToChest(chestId)) //has unlocked this chest
                    return true;

            if (IsRegionLocked()) //if region lock then check region
            {
                var x = (int) Position.X;
                var y = (int) Position.Y;

                if (TShock.Regions.InArea(x, y)) //if not in area disable region lock
                {
                    if (TShock.Regions.CanBuild(x, y, TShock.Players[player.Index])) //if can build in area
                        return true;
                }
                else
                    regionLock(false);
            }
            return false;
        }

        public bool CheckPassword(string password)
        {
            return HashedPassword.Equals(Utils.SHA1(password));
        }

        public void SetPassword(string password)
        {
            HashedPassword = password == "" ? "" : Utils.SHA1(password);
        }

        public void SetPassword(string password, bool checkForHash)
        {
            if (checkForHash)
            {
                string pattern = @"^[0-9a-fA-F]{40}$";
                if (Regex.IsMatch(password, pattern)) //is SHA1 string

                    HashedPassword = password;
            }
            else
                SetPassword(password);
        }

        public string GetPassword()
        {
          return HashedPassword;
        }

   
        public static bool TileIsChest(TileData tile)
        {
            return tile.type == 0x15;
        }

        public static bool TileIsChest(Vector2 position)
        {
            var x = (int) position.X;
            var y = (int) position.Y;

            return TileIsChest(x, y);
        }

        public static bool TileIsChest(int x, int y)
        {
            return TileIsChest(Main.tile[x, y].Data);
        }
    }
}