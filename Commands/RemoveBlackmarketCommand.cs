using Rocket.API;
using Rocket.Unturned.Chat;
using UnityEngine;

namespace BlackmarketNpc.Commands
{
    public class RemoveBlackmarketCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "removeblackmarket";

        public string Help => "Removes the current Black Market NPC";

        public string Syntax => "/removeblackmarket";

        public System.Collections.Generic.List<string> Aliases => new System.Collections.Generic.List<string> { "removebm", "bmremove" };

        public System.Collections.Generic.List<string> Permissions => new System.Collections.Generic.List<string> { "blackmarketnpc.remove" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            if (!BlackmarketNpcPlugin.Instance.IsBlackmarketActive)
            {
                UnturnedChat.Say(caller, BlackmarketNpcPlugin.Instance.Translate("blackmarket_not_active"), Color.red);
                return;
            }

            try
            {
                BlackmarketNpcPlugin.Instance.RemoveCurrentBlackmarket();
                UnturnedChat.Say(caller, BlackmarketNpcPlugin.Instance.Translate("blackmarket_despawned"), Color.yellow);
            }
            catch (Exception ex)
            {
                UnturnedChat.Say(caller, $"Failed to remove Black Market: {ex.Message}", Color.red);
            }
        }
    }
}

