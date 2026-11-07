using UnityEngine;
using ShipEnemies;
using Onslaught;

namespace ShipHUD
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HudRadar : UnityEngine.UI.MaskableGraphic
    {
        public HudMapData data;
        public Transform player, objective;
        public float maximumHeightDifference=10000;
        public ShipDoor[] doors;
        public Onslaught.Mission.DefenseGate[] barricades;
        public TMPro.TMP_Text countText;
        public const int Capacity = 128;
        public int ContactCount { get; private set; }
        public int DisplayedCount { get; private set; }
        public int OpenGates { get; private set; }
        public Vector2 PlayerPoint { get; private set; }
        public float PlayerYaw { get; private set; }
        readonly Vector3[] positions = new Vector3[Capacity];
        readonly bool[] bosses = new bool[Capacity];
        readonly float[] distances = new float[Capacity];
        readonly Transform[] actors = new Transform[Capacity];
        float nextRefresh;
        int lastCount = -1;
        public Vector2 MapPoint(Vector3 world) => data.ToMap(world, rectTransform.rect);
        public bool HasContact(Transform actor) { for(int i=0;i<DisplayedCount;i++) if(actors[i]==actor) return true; return false; }
        public bool TryContactPoint(Transform actor, out Vector2 point)
        {
            for(int i=0;i<DisplayedCount;i++) if(actors[i]==actor){point=MapPoint(positions[i]);return true;}
            point=default;return false;
        }
        void Update() { if (Time.unscaledTime >= nextRefresh) { nextRefresh = Time.unscaledTime + .1f; RefreshNow(); } }
        public void RefreshNow()
        {
            if (data == null) return;
            ContactCount = DisplayedCount = OpenGates = 0;
            // Nearest-first fixed buffer; bosses take priority. Excess contacts never allocate GameObjects.
            foreach (var e in EnemyBrain.Living)
                if(e != null && e.isActiveAndEnabled && e.Health != null && e.Health.Alive) Add(e.transform, e.definition != null && e.definition.kind == EnemyKind.Regurgitator);
            foreach (var e in MinimapContact.Active)
                if(e != null && e.Visible)
                {
                    var brain=e.GetComponent<EnemyBrain>();
                    if(brain != null && EnemyBrain.Living.Contains(brain)) continue;
                    Add(e.transform,e.boss);
                }
            if (doors != null) foreach (var d in doors) if(d != null && !d.automatic && d.IsOpen) OpenGates++;
            if(barricades!=null)foreach(var b in barricades)if(b!=null&&b.Broken&&!b.Sealed)OpenGates++;
            if(player != null) { PlayerPoint=MapPoint(player.position); PlayerYaw=player.eulerAngles.y; }
            if(countText != null && lastCount != ContactCount) { countText.SetText("{0:00} CONTACTS", ContactCount); lastCount=ContactCount; }
            SetVerticesDirty();
        }
        void Add(Transform actor, bool boss)
        {
            if(!data.Contains(actor.position)||player&&Mathf.Abs(actor.position.y-player.position.y)>maximumHeightDifference)return;
            ContactCount++;
            float d = (player != null ? (actor.position-player.position).sqrMagnitude : 0) - (boss ? 1000000 : 0);
            int index = DisplayedCount;
            if(index == Capacity) { if(d >= distances[Capacity-1]) return; index=Capacity-1; }
            else DisplayedCount++;
            while(index>0 && distances[index-1]>d) { positions[index]=positions[index-1];bosses[index]=bosses[index-1];distances[index]=distances[index-1];actors[index]=actors[index-1];index--; }
            positions[index]=actor.position;bosses[index]=boss;distances[index]=d;actors[index]=actor;
        }
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
        {
            vh.Clear(); if(data == null) return;
            if(doors != null)foreach(var door in doors)
            {
                if(door==null)continue;
                Vector3 side=door.transform.right*door.width*.5f;
                HudDrawing.Line(vh,MapPoint(door.transform.position-side),MapPoint(door.transform.position+side),door.automatic?1.5f:3,door.IsOpen?HudDrawing.Cyan:HudDrawing.Amber);
            }
            if(objective != null && data.Contains(objective.position))
            { var p=MapPoint(objective.position);HudDrawing.Diamond(vh,p,5.5f,HudDrawing.Amber);HudDrawing.Diamond(vh,p,3,HudDrawing.Ink); }
            if(barricades!=null)foreach(var gate in barricades)
            {
                if(gate==null)continue;Vector3 side=gate.transform.right*gate.width*.5f;
                HudDrawing.Line(vh,MapPoint(gate.transform.position-side),MapPoint(gate.transform.position+side),2.5f,gate.Sealed?HudDrawing.Cyan:gate.Broken?HudDrawing.Red:HudDrawing.Amber);
            }
            for(int i=0;i<DisplayedCount;i++)
            {
                var p=MapPoint(positions[i]);
                if(bosses[i]) { HudDrawing.Diamond(vh,p,6.5f,HudDrawing.Red);HudDrawing.Diamond(vh,p,3,HudDrawing.Ink); }
                else { HudDrawing.Diamond(vh,p,4,HudDrawing.Ink);HudDrawing.Diamond(vh,p,2.7f,HudDrawing.Red); }
            }
            if(player == null || !data.Contains(player.position))return;
            Vector2 point=PlayerPoint;
            float angle=PlayerYaw*Mathf.Deg2Rad;Vector2 forward=new Vector2(Mathf.Sin(angle),Mathf.Cos(angle)),right=new Vector2(forward.y,-forward.x);
            HudDrawing.Triangle(vh,point,point+forward*25-right*16,point+forward*25+right*16,new Color(.3f,.85f,.82f,.12f));
            HudDrawing.Triangle(vh,point+forward*7,point-forward*5+right*5,point-forward*5-right*5,HudDrawing.Ink);
            HudDrawing.Triangle(vh,point+forward*5,point-forward*3+right*3,point-forward*3-right*3,HudDrawing.Cyan);
        }
    }
}
