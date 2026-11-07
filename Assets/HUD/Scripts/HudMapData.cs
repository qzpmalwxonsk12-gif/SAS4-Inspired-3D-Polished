using UnityEngine;

namespace ShipHUD
{
    // Baked from the scene's real deck meshes and wall colliders by HudBuilder.
    public sealed class HudMapData : ScriptableObject
    {
        public Vector2 minimum, maximum;
        public Vector2[] floorVertices;
        public int[] floorTriangles;
        public Vector2[] wallQuads;
        public Vector2 ToMap(Vector3 world, Rect rect)
        {
            Vector2 size = maximum - minimum;
            float scale = Mathf.Min(rect.width / size.x, rect.height / size.y);
            Vector2 center = (minimum + maximum) * .5f;
            return rect.center + (new Vector2(world.x, world.z) - center) * scale;
        }
        public bool Contains(Vector3 world) => world.x >= minimum.x && world.x <= maximum.x && world.z >= minimum.y && world.z <= maximum.y;
    }
}
