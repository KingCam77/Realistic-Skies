using System.Collections.Generic;
using UnityEngine;

namespace RealisticSkies
{
    /// <summary>
    /// Where the Sea Legends scroll lies in each of the mod's shops. A spot is a pose in the shop's own
    /// space (the shopPrefab transform from ShopInfo), keyed by the island index. Shops without a spot
    /// get the scroll next to the Star Chart. The spots were placed by hand in game (shop-local pose of
    /// a scroll put down on the stall) and copied here.
    /// </summary>
    internal static class LoreShopSpots
    {
        internal struct Spot
        {
            public Vector3 pos;
            public Quaternion rot;
        }

        /// <summary>Hand-placed spots, by island index.</summary>
        private static readonly Dictionary<int, Spot> Baked = new Dictionary<int, Spot>
        {
            { 1, S(-0.912f, 0.770f, 0.611f, -0.0093f, -0.7703f, 0.6375f, -0.0115f) },
            { 9, S(-0.889f, 0.908f, 0.048f, 0.5550f, 0.4904f, -0.5269f, 0.4169f) },
            { 15, S(-0.878f, 0.781f, 0.437f, -0.0040f, -0.7739f, 0.6333f, -0.0031f) },
        };

        private static Spot S(float x, float y, float z, float qx, float qy, float qz, float qw)
        {
            return new Spot { pos = new Vector3(x, y, z), rot = new Quaternion(qx, qy, qz, qw) };
        }

        internal static bool TryGet(int island, out Spot spot)
        {
            return Baked.TryGetValue(island, out spot);
        }

        /// <summary>Moves a scroll spawner onto a spot of the given shop (world pose from shop space).</summary>
        internal static void Apply(Transform spawner, Transform shop, Spot spot)
        {
            spawner.position = shop.TransformPoint(spot.pos);
            spawner.rotation = shop.rotation * spot.rot;
        }
    }
}
