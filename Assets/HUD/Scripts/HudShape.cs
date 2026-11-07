using UnityEngine;
namespace ShipHUD
{
    // Original vector geometry: no downloaded UI art, textures, shaders or per-frame texture uploads.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudShape : UnityEngine.UI.MaskableGraphic
    {
        public enum Style { Panel, Bar, Crosshair, Medical, Grenade, Skill, Weapon, Pistol }
        public Style style;
        [Range(0, 1)] public float fill = 1;
        public int segments = 1;
        public void SetFill(float value) { value = Mathf.Clamp01(value); if (Mathf.Approximately(fill, value)) return; fill = value; SetVerticesDirty(); }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear(); Rect r = rectTransform.rect; Vector2 c = r.center;
            if (style == Style.Panel)
            {
                float cut = Mathf.Min(12, r.height / 4);
                Vector2[] p = {new Vector2(r.xMin + cut,r.yMin),new Vector2(r.xMin,r.yMin + cut),new Vector2(r.xMin,r.yMax),new Vector2(r.xMax - cut,r.yMax),new Vector2(r.xMax,r.yMax - cut),new Vector2(r.xMax,r.yMin)};
                for (int i = 0; i < p.Length; i++) { HudDrawing.Triangle(vh, c, p[i], p[(i + 1) % p.Length], color); HudDrawing.Line(vh, p[i], p[(i + 1) % p.Length], 1, new Color(.3f,.36f,.36f,.9f)); }
                HudDrawing.Line(vh, new Vector2(r.xMin + 12,r.yMax),new Vector2(r.xMin + 65,r.yMax),2,HudDrawing.Amber);
            }
            else if (style == Style.Bar)
            {
                int n = Mathf.Max(1, segments); float w = r.width / n;
                for (int i = 0; i < n; i++)
                {
                    var cell = new Rect(r.xMin + i * w, r.yMin, w - (n > 1 ? 2 : 0), r.height);
                    HudDrawing.Rect(vh, cell, new Color(.14f,.19f,.19f,.95f));
                    cell.width *= Mathf.Clamp01(fill * n - i); if (cell.width > 0) HudDrawing.Rect(vh, cell, color);
                }
            }
            else if (style == Style.Crosshair)
            {
                HudDrawing.Rect(vh,new Rect(c.x-1,c.y-1,2,2),color);
                for (int i=0;i<4;i++) { float a=i*Mathf.PI/2; Vector2 v=new Vector2(Mathf.Cos(a),Mathf.Sin(a)); HudDrawing.Line(vh,c+v*7,c+v*13,1.2f,color); }
            }
            else if (style == Style.Medical)
            {
                HudDrawing.Rect(vh,new Rect(c.x-3,c.y-10,6,20),color); HudDrawing.Rect(vh,new Rect(c.x-10,c.y-3,20,6),color);
            }
            else if (style == Style.Grenade)
            {
                HudDrawing.Quad(vh,c+new Vector2(-7,-9),c+new Vector2(-8,4),c+new Vector2(7,6),c+new Vector2(8,-9),color);
                HudDrawing.Line(vh,c+new Vector2(-2,8),c+new Vector2(7,8),3,color); HudDrawing.Line(vh,c+new Vector2(7,8),c+new Vector2(10,-1),2,color);
            }
            else if (style == Style.Skill)
            {
                HudDrawing.Diamond(vh,c,12,color); HudDrawing.Diamond(vh,c,8,HudDrawing.Ink); HudDrawing.Rect(vh,new Rect(c.x-2,c.y-5,4,10),color);
            }
            else if (style == Style.Pistol)
            {
                HudDrawing.Rect(vh,new Rect(c.x-23,c.y+3,48,10),color);
                HudDrawing.Rect(vh,new Rect(c.x-22,c.y,32,6),color);
                HudDrawing.Quad(vh,c+new Vector2(-22,3),c+new Vector2(-9,3),c+new Vector2(-14,-15),c+new Vector2(-27,-15),color);
                HudDrawing.Line(vh,c+new Vector2(-10,-5),c+new Vector2(2,-5),2,color);
                HudDrawing.Line(vh,c+new Vector2(2,-5),c+new Vector2(4,2),2,color);
            }
            else
            {
                HudDrawing.Rect(vh,new Rect(c.x-32,c.y,43,9),color); HudDrawing.Rect(vh,new Rect(c.x+11,c.y+3,25,3),color);
                HudDrawing.Quad(vh,c+new Vector2(-20,0),c+new Vector2(-16,0),c+new Vector2(-20,-10),c+new Vector2(-26,-10),color);
                HudDrawing.Quad(vh,c+new Vector2(-5,0),c+new Vector2(2,0),c+new Vector2(5,-12),c+new Vector2(-2,-12),color);
                HudDrawing.Quad(vh,c+new Vector2(-32,3),c+new Vector2(-43,-2),c+new Vector2(-43,10),c+new Vector2(-32,9),color);
            }
        }
    }
}
