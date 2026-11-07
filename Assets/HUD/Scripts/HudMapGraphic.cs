using UnityEngine;

namespace ShipHUD
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudMapGraphic : UnityEngine.UI.MaskableGraphic
    {
        public HudMapData data;
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear(); if (data == null) return;
            Rect r = rectTransform.rect;
            for (float x=r.xMin;x<r.xMax;x+=22) HudDrawing.Line(vh,new Vector2(x,r.yMin),new Vector2(x,r.yMax),.5f,new Color(.12f,.19f,.19f,.35f));
            for (float y=r.yMin;y<r.yMax;y+=22) HudDrawing.Line(vh,new Vector2(r.xMin,y),new Vector2(r.xMax,y),.5f,new Color(.12f,.19f,.19f,.35f));
            Vector2 Map(Vector2 p) => data.ToMap(new Vector3(p.x,0,p.y),r);
            for (int i=0;i<data.floorTriangles.Length;i+=3)
                HudDrawing.Triangle(vh,Map(data.floorVertices[data.floorTriangles[i]]),Map(data.floorVertices[data.floorTriangles[i+1]]),Map(data.floorVertices[data.floorTriangles[i+2]]),new Color(.15f,.23f,.24f));
            for (int i=0;i<data.wallQuads.Length;i+=4)
            {
                Vector2 a=Map(data.wallQuads[i]),b=Map(data.wallQuads[i+1]),c=Map(data.wallQuads[i+2]),d=Map(data.wallQuads[i+3]);
                HudDrawing.Quad(vh,a,b,c,d,HudDrawing.Ink);
                HudDrawing.Line(vh,a,b,.7f,HudDrawing.Muted);HudDrawing.Line(vh,b,c,.7f,HudDrawing.Muted);
                HudDrawing.Line(vh,c,d,.7f,HudDrawing.Muted);HudDrawing.Line(vh,d,a,.7f,HudDrawing.Muted);
            }
        }
    }
}
