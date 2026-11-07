using UnityEngine;

namespace ShipHUD
{
    public sealed class HudSafeArea : MonoBehaviour
    {
        Rect last;
        Vector2Int resolution;
        RectTransform target;
        void Awake() => target=(RectTransform)transform;
        void Update()
        {
            if(Screen.width<=0||Screen.height<=0)return;
            var area=Screen.safeArea;var size=new Vector2Int(Screen.width,Screen.height);
            if(area==last&&size==resolution)return;
            last=area;resolution=size;
            target.anchorMin=new Vector2(area.xMin/Screen.width,area.yMin/Screen.height);
            target.anchorMax=new Vector2(area.xMax/Screen.width,area.yMax/Screen.height);
            target.offsetMin=target.offsetMax=Vector2.zero;
        }
    }
}
