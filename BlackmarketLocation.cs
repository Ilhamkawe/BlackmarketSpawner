using UnityEngine;

namespace BlackmarketNpc
{
    public class BlackmarketLocation
    {
        public Vector3 Position { get; set; }
        public float Yaw { get; set; }
        public string SourceType { get; set; } // "Barricade" or "Vehicle"
        public ushort SourceId { get; set; }
        public System.DateTime SpawnTime { get; set; }
        public BarricadeDrop BarricadeDrop { get; set; }
    }
}

