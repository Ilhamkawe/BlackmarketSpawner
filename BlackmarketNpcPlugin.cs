using System;
using System.Collections.Generic;
using System.Linq;
using System.Timers;
using Rocket.API.Collections;
using Rocket.Core.Logging;
using Rocket.Core.Plugins;
using Rocket.Core.Utils;
using Rocket.Unturned.Chat;
using SDG.Unturned;
using UnityEngine;
using RocketLogger = Rocket.Core.Logging.Logger;

namespace BlackmarketNpc
{
    public class BlackmarketNpcPlugin : RocketPlugin<BlackmarketNpcPluginConfiguration>
    {
        private Timer _autoSpawnTimer;
        private Timer _despawnTimer;
        private BlackmarketLocation _currentBlackmarket;
        private readonly System.Random _random = new System.Random();
        private DateTime? _nextSpawnTimeUtc;

        public static BlackmarketNpcPlugin Instance { get; private set; }

        protected override void Load()
        {
            Instance = this;
            
            if (Configuration.Instance.NpcId == 0)
            {
                RocketLogger.LogWarning("[BlackmarketNpc] ⚠ NpcId is not set! Please configure NpcId in config file.");
            }

            InitializeAutoSpawnTimer();
            
            RocketLogger.Log("[BlackmarketNpc] Plugin loaded.");
        }

        protected override void Unload()
        {
            RemoveCurrentBlackmarket();
            StopAutoSpawnTimer();
            StopDespawnTimer();
            Instance = null;
            RocketLogger.Log("[BlackmarketNpc] Plugin unloaded.");
        }

        public override TranslationList DefaultTranslations => new TranslationList
        {
            { "blackmarket_spawned", "Black Market has appeared at {0}!" },
            { "blackmarket_despawned", "Black Market has closed." },
            { "blackmarket_already_active", "Black Market is already active." },
            { "blackmarket_not_active", "No active Black Market." },
            { "blackmarket_status", "Black Market is currently {0}." },
            { "blackmarket_next", "Next Black Market in {0}." },
            { "blackmarket_location", "Location: {0}" },
            { "blackmarket_no_location", "Unable to find suitable location for Black Market." }
        };

        private void InitializeAutoSpawnTimer()
        {
            if (!Configuration.Instance.AutoSpawnEnabled)
            {
                StopAutoSpawnTimer();
                return;
            }

            if (_autoSpawnTimer == null)
            {
                _autoSpawnTimer = new Timer
                {
                    AutoReset = false
                };
                _autoSpawnTimer.Elapsed += OnAutoSpawnTimerElapsed;
            }

            ScheduleNextAutoSpawn();
        }

        private void ScheduleNextAutoSpawn()
        {
            if (_autoSpawnTimer == null || !Configuration.Instance.AutoSpawnEnabled)
            {
                _nextSpawnTimeUtc = null;
                return;
            }

            var min = Math.Max(0.1, Configuration.Instance.AutoSpawnMinIntervalMinutes);
            var max = Math.Max(min, Configuration.Instance.AutoSpawnMaxIntervalMinutes);
            var minutes = min.Equals(max) ? min : min + _random.NextDouble() * (max - min);
            var interval = TimeSpan.FromMinutes(minutes);

            _nextSpawnTimeUtc = DateTime.UtcNow.Add(interval);
            _autoSpawnTimer.Interval = Math.Max(1000, interval.TotalMilliseconds);
            _autoSpawnTimer.Start();

            RocketLogger.Log($"[BlackmarketNpc] Next Black Market scheduled in {minutes:F1} minutes.");
        }

        private void OnAutoSpawnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            TaskDispatcher.QueueOnMainThread(() =>
            {
                if (_currentBlackmarket != null)
                {
                    ScheduleNextAutoSpawn();
                    return;
                }

                try
                {
                    SpawnBlackmarket();
                }
                catch (Exception ex)
                {
                    RocketLogger.LogWarning($"[BlackmarketNpc] Failed to spawn auto black market: {ex.Message}");
                    ScheduleNextAutoSpawn();
                }
            });
        }

        public void SpawnBlackmarket()
        {
            if (_currentBlackmarket != null)
            {
                throw new InvalidOperationException(Translate("blackmarket_already_active"));
            }

            var location = FindRandomLocation();
            if (location == null)
            {
                throw new InvalidOperationException(Translate("blackmarket_no_location"));
            }

            if (!TrySpawnNpcAtLocation(location))
            {
                throw new InvalidOperationException("Failed to spawn NPC at location.");
            }

            _currentBlackmarket = location;
            StartDespawnTimer();

            var locationText = $"({location.Position.x:F0}, {location.Position.z:F0})";
            if (Configuration.Instance.BroadcastSpawn)
            {
                UnturnedChat.Say(Translate("blackmarket_spawned", locationText), Color.yellow);
            }

            RocketLogger.Log($"[BlackmarketNpc] Black Market spawned at {location.Position} (Source: {location.SourceType} ID: {location.SourceId})");
        }

        public void RemoveCurrentBlackmarket()
        {
            if (_currentBlackmarket == null)
            {
                return;
            }

            try
            {
                DespawnNpcAtLocation(_currentBlackmarket);
                
                if (Configuration.Instance.BroadcastDespawn)
                {
                    UnturnedChat.Say(Translate("blackmarket_despawned"), Color.yellow);
                }

                RocketLogger.Log("[BlackmarketNpc] Black Market removed.");
            }
            catch (Exception ex)
            {
                RocketLogger.LogWarning($"[BlackmarketNpc] Failed to remove black market: {ex.Message}");
            }
            finally
            {
                _currentBlackmarket = null;
                StopDespawnTimer();
            }
        }

        private BlackmarketLocation FindRandomLocation()
        {
            var candidates = new List<BlackmarketLocation>();

            // Collect locations from barricade nodes
            if (Configuration.Instance.UseBarricadeNodes)
            {
                CollectBarricadeLocations(candidates);
            }

            // Collect locations from vehicle nodes
            if (Configuration.Instance.UseVehicleNodes)
            {
                CollectVehicleLocations(candidates);
            }

            if (candidates.Count == 0)
            {
                RocketLogger.LogWarning("[BlackmarketNpc] No suitable locations found from nodes.");
                return null;
            }

            // Filter out locations too close to players or safezones
            var validCandidates = FilterValidLocations(candidates);
            
            if (validCandidates.Count == 0)
            {
                RocketLogger.LogWarning("[BlackmarketNpc] No valid locations after filtering. Using unfiltered candidates.");
                // Fallback to original candidates if filtering removed all
                if (candidates.Count > 0)
                {
                    validCandidates = candidates;
                }
                else
                {
                    return null;
                }
            }

            // Select random location
            var selected = validCandidates[_random.Next(validCandidates.Count)];
            
            // Add random offset within spawn radius
            var angle = (float)(_random.NextDouble() * Math.PI * 2);
            var distance = (float)(_random.NextDouble() * Configuration.Instance.SpawnRadius);
            selected.Position = new Vector3(
                selected.Position.x + Mathf.Cos(angle) * distance,
                selected.Position.y,
                selected.Position.z + Mathf.Sin(angle) * distance
            );
            selected.Yaw = (float)(_random.NextDouble() * 360);

            return selected;
        }

        private void CollectBarricadeLocations(List<BlackmarketLocation> candidates)
        {
            try
            {
                var regions = BarricadeManager.regions;
                if (regions == null)
                {
                    return;
                }

                var lengthX = regions.GetLength(0);
                var lengthY = regions.GetLength(1);

                for (var x = 0; x < lengthX; x++)
                {
                    for (var y = 0; y < lengthY; y++)
                    {
                        var region = regions[x, y];
                        if (region == null || region.drops == null)
                        {
                            continue;
                        }

                        foreach (var drop in region.drops)
                        {
                            if (drop == null || drop.model == null)
                            {
                                continue;
                            }

                            var asset = drop.asset;
                            if (asset == null)
                            {
                                continue;
                            }

                            // Skip excluded barricades
                            if (Configuration.Instance.ExcludedBarricadeIds.Contains(asset.id))
                            {
                                continue;
                            }

                            var position = drop.model.position;
                            candidates.Add(new BlackmarketLocation
                            {
                                Position = position,
                                Yaw = drop.model.rotation.eulerAngles.y,
                                SourceType = "Barricade",
                                SourceId = asset.id,
                                BarricadeDrop = drop
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RocketLogger.LogWarning($"[BlackmarketNpc] Failed to collect barricade locations: {ex.Message}");
            }
        }

        private void CollectVehicleLocations(List<BlackmarketLocation> candidates)
        {
            try
            {
                var vehicles = VehicleManager.vehicles;
                if (vehicles == null)
                {
                    return;
                }

                foreach (var vehicle in vehicles)
                {
                    if (vehicle == null || vehicle.transform == null)
                    {
                        continue;
                    }

                    var asset = vehicle.asset;
                    if (asset == null)
                    {
                        continue;
                    }

                    // Skip excluded vehicles
                    if (Configuration.Instance.ExcludedVehicleIds.Contains(asset.id))
                    {
                        continue;
                    }

                    var position = vehicle.transform.position;
                    candidates.Add(new BlackmarketLocation
                    {
                        Position = position,
                        Yaw = vehicle.transform.rotation.eulerAngles.y,
                        SourceType = "Vehicle",
                        SourceId = asset.id
                    });
                }
            }
            catch (Exception ex)
            {
                RocketLogger.LogWarning($"[BlackmarketNpc] Failed to collect vehicle locations: {ex.Message}");
            }
        }

        private List<BlackmarketLocation> FilterValidLocations(List<BlackmarketLocation> candidates)
        {
            var valid = new List<BlackmarketLocation>();
            var minPlayerDistSqr = Configuration.Instance.MinDistanceFromPlayers * Configuration.Instance.MinDistanceFromPlayers;
            var minSafezoneDistSqr = Configuration.Instance.MinDistanceFromSafezones * Configuration.Instance.MinDistanceFromSafezones;

            foreach (var location in candidates)
            {
                // Check distance from players
                bool tooCloseToPlayer = false;
                foreach (var steamPlayer in Provider.clients)
                {
                    if (steamPlayer?.player?.transform == null)
                    {
                        continue;
                    }

                    var playerPos = steamPlayer.player.transform.position;
                    var distSqr = (location.Position - playerPos).sqrMagnitude;
                    if (distSqr < minPlayerDistSqr)
                    {
                        tooCloseToPlayer = true;
                        break;
                    }
                }

                if (tooCloseToPlayer)
                {
                    continue;
                }

                // Check distance from safezones (simplified check)
                // Note: Full safezone check would require more complex logic
                valid.Add(location);
            }

            return valid;
        }

        private bool TrySpawnNpcAtLocation(BlackmarketLocation location)
        {
            if (Configuration.Instance.NpcId == 0)
            {
                RocketLogger.LogWarning("[BlackmarketNpc] Cannot spawn: NpcId is not configured.");
                return false;
            }

            try
            {
                // Get NPC asset
                var npcAsset = Assets.find(EAssetType.NPC, Configuration.Instance.NpcId) as Asset;
                if (npcAsset == null)
                {
                    RocketLogger.LogWarning($"[BlackmarketNpc] NPC asset {Configuration.Instance.NpcId} not found.");
                    return false;
                }

                // Spawn NPC as barricade
                var item = new Item(Configuration.Instance.NpcId, 1, 100, npcAsset.getState());
                var rotation = Quaternion.Euler(0, location.Yaw, 0);
                var point = new TransformPoint(location.Position, rotation, ushort.MaxValue, ushort.MaxValue);

                BarricadeManager.dropBarricade(
                    item,
                    null, // owner
                    null, // group
                    point.transform,
                    point.point);

                location.SpawnTime = DateTime.UtcNow;
                RocketLogger.Log($"[BlackmarketNpc] NPC spawned successfully at {location.Position}.");
                return true;
            }
            catch (Exception ex)
            {
                RocketLogger.LogWarning($"[BlackmarketNpc] Failed to spawn NPC: {ex.Message}");
                return false;
            }
        }

        private void DespawnNpcAtLocation(BlackmarketLocation location)
        {
            try
            {
                // Find and remove NPC barricade near the location
                var regions = BarricadeManager.regions;
                if (regions == null)
                {
                    return;
                }

                var searchRadiusSqr = 10f * 10f; // 10 meter search radius
                var lengthX = regions.GetLength(0);
                var lengthY = regions.GetLength(1);

                for (var x = 0; x < lengthX; x++)
                {
                    for (var y = 0; y < lengthY; y++)
                    {
                        var region = regions[x, y];
                        if (region == null || region.drops == null)
                        {
                            continue;
                        }

                        for (var i = region.drops.Count - 1; i >= 0; i--)
                        {
                            var drop = region.drops[i];
                            if (drop == null || drop.model == null)
                            {
                                continue;
                            }

                            var asset = drop.asset;
                            if (asset == null || asset.id != Configuration.Instance.NpcId)
                            {
                                continue;
                            }

                            var distSqr = (drop.model.position - location.Position).sqrMagnitude;
                            if (distSqr <= searchRadiusSqr)
                            {
                                BarricadeManager.destroyBarricade(region, x, y, (ushort)i);
                                RocketLogger.Log($"[BlackmarketNpc] NPC removed at {drop.model.position}.");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                RocketLogger.LogWarning($"[BlackmarketNpc] Failed to despawn NPC: {ex.Message}");
            }
        }

        private void StartDespawnTimer()
        {
            StopDespawnTimer();

            var durationSeconds = Math.Max(0, Configuration.Instance.BlackmarketDurationMinutes * 60);
            if (durationSeconds <= 0)
            {
                return;
            }

            _despawnTimer = new Timer(durationSeconds * 1000)
            {
                AutoReset = false
            };
            _despawnTimer.Elapsed += OnDespawnTimerElapsed;
            _despawnTimer.Start();
        }

        private void OnDespawnTimerElapsed(object sender, ElapsedEventArgs e)
        {
            TaskDispatcher.QueueOnMainThread(() =>
            {
                if (_currentBlackmarket == null)
                {
                    return;
                }

                try
                {
                    RemoveCurrentBlackmarket();
                    ScheduleNextAutoSpawn();
                }
                catch (Exception ex)
                {
                    RocketLogger.LogWarning($"[BlackmarketNpc] Failed to despawn black market: {ex.Message}");
                }
            });
        }

        private void StopDespawnTimer()
        {
            if (_despawnTimer == null)
            {
                return;
            }

            _despawnTimer.Elapsed -= OnDespawnTimerElapsed;
            _despawnTimer.Stop();
            _despawnTimer.Dispose();
            _despawnTimer = null;
        }

        private void StopAutoSpawnTimer()
        {
            if (_autoSpawnTimer == null)
            {
                return;
            }

            _autoSpawnTimer.Elapsed -= OnAutoSpawnTimerElapsed;
            _autoSpawnTimer.Stop();
            _autoSpawnTimer.Dispose();
            _autoSpawnTimer = null;
        }

        public bool IsBlackmarketActive => _currentBlackmarket != null;
        public BlackmarketLocation CurrentBlackmarket => _currentBlackmarket;
        public DateTime? NextSpawnTimeUtc => _nextSpawnTimeUtc;
        public bool AutoSpawnEnabled => Configuration.Instance.AutoSpawnEnabled;
    }
}

