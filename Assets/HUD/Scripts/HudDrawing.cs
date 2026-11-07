using UnityEngine;

namespace ShipHUD
{
    public static class HudDrawing
    {
        public static readonly Color Ink = new Color(.035f, .047f, .052f, .94f);
        public static readonly Color Paper = new Color(.9f, .92f, .86f);
        public static readonly Color Muted = new Color(.55f, .63f, .62f);
        public static readonly Color Amber = new Color(.94f, .66f, .28f);
        public static readonly Color Red = new Color(1f, .27f, .22f);
        public static readonly Color Green = new Color(.51f, .78f, .47f);
        public static readonly Color Cyan = new Color(.4f, .93f, .91f);
        public static void Triangle(UnityEngine.UI.VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
        }
        public static void Quad(UnityEngine.UI.VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int i = vh.currentVertCount;
            vh.AddVert(a, color, Vector2.zero); vh.AddVert(b, color, Vector2.zero); vh.AddVert(c, color, Vector2.zero); vh.AddVert(d, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2); vh.AddTriangle(i, i + 2, i + 3);
        }
        public static void Rect(UnityEngine.UI.VertexHelper vh, Rect r, Color color) => Quad(vh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax), new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin), color);
        public static void Line(UnityEngine.UI.VertexHelper vh, Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width * .5f;
            Quad(vh, a + n, b + n, b - n, a - n, color);
        }
        public static void Diamond(UnityEngine.UI.VertexHelper vh, Vector2 p, float r, Color color)
            => Quad(vh, p + Vector2.up * r, p + Vector2.right * r, p + Vector2.down * r, p + Vector2.left * r, color);
        public static void Ring(UnityEngine.UI.VertexHelper vh, Vector2 p, float r, Color color)
        {
            for (int i = 0; i < 16; i++)
            {
                float a = i * Mathf.PI / 8, b = (i + 1) * Mathf.PI / 8;
                Line(vh, p + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, p + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * r, 1.1f, color);
            }
        }
    }

}
