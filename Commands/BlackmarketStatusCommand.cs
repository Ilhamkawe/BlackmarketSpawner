using System;
using Rocket.API;
using Rocket.Unturned.Chat;
using UnityEngine;

namespace BlackmarketNpc.Commands
{
    public class BlackmarketStatusCommand : IRocketCommand
    {
        public AllowedCaller AllowedCaller => AllowedCaller.Both;

        public string Name => "blackmarketstatus";

        public string Help => "Shows Black Market status";

        public string Syntax => "/blackmarketstatus";

        public System.Collections.Generic.List<string> Aliases => new System.Collections.Generic.List<string> { "bmstatus", "bm" };

        public System.Collections.Generic.List<string> Permissions => new System.Collections.Generic.List<string> { "blackmarketnpc.status" };

        public void Execute(IRocketPlayer caller, string[] command)
        {
            var plugin = BlackmarketNpcPlugin.Instance;
            var state = plugin.IsBlackmarketActive ? "active" : "inactive";
            var message = plugin.Translate("blackmarket_status", state);

            if (plugin.IsBlackmarketActive && plugin.CurrentBlackmarket != null)
            {
                var location = plugin.CurrentBlackmarket;
                var locationText = $"({location.Position.x:F0}, {location.Position.z:F0})";
                message += " " + plugin.Translate("blackmarket_location", locationText);
                
                var elapsed = DateTime.UtcNow - location.SpawnTime;
                var remaining = TimeSpan.FromMinutes(plugin.Configuration.Instance.BlackmarketDurationMinutes) - elapsed;
                if (remaining > TimeSpan.Zero)
                {
                    message += $" (Closes in {FormatDuration(remaining)})";
                }
            }
            else if (!plugin.IsBlackmarketActive && plugin.AutoSpawnEnabled && plugin.NextSpawnTimeUtc.HasValue)
            {
                var remaining = plugin.NextSpawnTimeUtc.Value - DateTime.UtcNow;
                if (remaining > TimeSpan.Zero)
                {
                    message += " " + plugin.Translate("blackmarket_next", FormatDuration(remaining));
                }
            }

            UnturnedChat.Say(caller, message, Color.green);
        }

        private static string FormatDuration(TimeSpan span)
        {
            if (span.TotalHours >= 1)
            {
                return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)span.TotalHours, span.Minutes, span.Seconds);
            }

            return string.Format("{0:D2}:{1:D2}", span.Minutes, span.Seconds);
        }
    }
}

