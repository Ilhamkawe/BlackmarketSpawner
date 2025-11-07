using System;
using Rocket.API;
using Rocket.Unturned.Chat;
using UnityEngine;

namespace BlackmarketNpc.Commands
{
    public class SpawnBlackmarketCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "spawnblackmarket";

        public string Help => "Spawns a Black Market NPC at a random location";

        public string Syntax => "/spawnblackmarket";

        public System.Collections.Generic.List<string> Aliases => new System.Collections.Generic.List<string> { "spawnbm", "bmspawn" };

        public System.Collections.Generic.List<string> Permissions => new System.Collections.Generic.List<string> { "blackmarketnpc.spawn" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            try
            {
                BlackmarketNpcPlugin.Instance.SpawnBlackmarket();
                var location = BlackmarketNpcPlugin.Instance.CurrentBlackmarket;
                if (location != null)
                {
                    var locationText = $"({location.Position.x:F0}, {location.Position.z:F0})";
                    UnturnedChat.Say(caller, BlackmarketNpcPlugin.Instance.Translate("blackmarket_spawned", locationText), Color.yellow);
                    UnturnedChat.Say(caller, BlackmarketNpcPlugin.Instance.Translate("blackmarket_location", locationText), Color.green);
                }
            }
            catch (InvalidOperationException ex)
            {
                UnturnedChat.Say(caller, ex.Message, Color.red);
            }
            catch (Exception ex)
            {
                UnturnedChat.Say(caller, $"Failed to spawn Black Market: {ex.Message}", Color.red);
            }
        }
    }
}

