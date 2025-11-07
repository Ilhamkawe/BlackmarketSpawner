using System.Collections.Generic;
using Rocket.API;

namespace BlackmarketNpc
{
    public class BlackmarketNpcPluginConfiguration : IRocketPluginConfiguration
    {
        public bool AutoSpawnEnabled { get; set; }
        public double AutoSpawnMinIntervalMinutes { get; set; }
        public double AutoSpawnMaxIntervalMinutes { get; set; }
        public double BlackmarketDurationMinutes { get; set; }
        public ushort NpcId { get; set; }
        public bool UseBarricadeNodes { get; set; }
        public bool UseVehicleNodes { get; set; }
        public double SpawnRadius { get; set; }
        public bool BroadcastSpawn { get; set; }
        public bool BroadcastDespawn { get; set; }
        public List<ushort> ExcludedBarricadeIds { get; set; }
        public List<ushort> ExcludedVehicleIds { get; set; }
        public double MinDistanceFromPlayers { get; set; }
        public double MinDistanceFromSafezones { get; set; }

        public void LoadDefaults()
        {
            AutoSpawnEnabled = true;
            AutoSpawnMinIntervalMinutes = 60.0; // Minimal 1 jam
            AutoSpawnMaxIntervalMinutes = 120.0; // Maksimal 2 jam
            BlackmarketDurationMinutes = 30.0; // Black market aktif selama 30 menit
            NpcId = 0; // Set NPC ID sesuai kebutuhan (0 = tidak ada, harus di-set di config)
            UseBarricadeNodes = true; // Gunakan barricade nodes sebagai referensi
            UseVehicleNodes = true; // Gunakan vehicle nodes sebagai referensi
            SpawnRadius = 50.0; // Radius spawn dari node (50 meter)
            BroadcastSpawn = true; // Broadcast saat black market spawn
            BroadcastDespawn = true; // Broadcast saat black market despawn
            ExcludedBarricadeIds = new List<ushort>(); // Barricade ID yang di-exclude
            ExcludedVehicleIds = new List<ushort>(); // Vehicle ID yang di-exclude
            MinDistanceFromPlayers = 100.0; // Minimal jarak dari player (100 meter)
            MinDistanceFromSafezones = 200.0; // Minimal jarak dari safezone (200 meter)
        }
    }
}

