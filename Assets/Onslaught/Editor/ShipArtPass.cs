using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Onslaught.Editor
{
    // Runs inside the Editor: all scene placement, materials, UVs and saved meshes are real assets.
    public static class ShipArtPass
    {
        const string Root="Assets/Onslaught";
        static Transform art;
        static Material deck, structure, wall, grate, rubber, blue, red, amber, glass, cyan, stripe;
        static Vector3 P(float x,float y,float h=0)=>ShipRefinement.P(x,y,h);

        [MenuItem("Onslaught/Apply Reference Art")]
        public static void Apply()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play before applying art");
            if(GameObject.Find("Ship layout - 130 percent")==null)throw new System.InvalidOperationException("Apply ship refinement first");
            var old=GameObject.Find("07 - Reference ship art");if(old!=null)Object.DestroyImmediate(old);
            art=new GameObject("07 - Reference ship art").transform;
            deck=Mat("Reference deck plates","DeckPlates",new Color(.84f,.85f,.82f));
            wall=Mat("Reference wall atlas","WallPanels",new Color(.78f,.82f,.84f));
            structure=Mat("Reference machinery","MachineryPaint",new Color(.86f,.93f,1));
            grate=Mat("Reference ramp grating","RampGrating",new Color(.73f,.78f,.79f));
            rubber=Mat("Reference dark rubber",null,new Color(.045f,.052f,.056f));
            blue=Mat("Reference blue markers",null,new Color(.21f,.60f,1),true);
            red=Mat("Reference red markers",null,new Color(.85f,.065f,.035f),true);
            amber=Mat("Reference amber markers",null,new Color(1,.51f,.12f),true);
            cyan=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Screen phosphor.mat");
            glass=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Rubber and recesses.mat");
            stripe=Mat("Reference hazard paint",null,new Color(.52f,.32f,.075f));
            Directory.CreateDirectory(Root+"/Geometry/Art");AssetDatabase.Refresh();
            var layout=GameObject.Find("Ship layout - 130 percent");
            foreach(var r in layout.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterial=r.name.Contains("deck")?deck:r.name.Contains("ramp")||r.name.Contains("strip")?grate:structure;
                if(r.name.Contains("deck"))UV(r.GetComponent<MeshFilter>(),10);
                else if(r.name.Contains("ramp")||r.name.Contains("strip"))UV(r.GetComponent<MeshFilter>(),2);
                if(r.name.Contains("seat bank")||r.name.Contains("seat back"))r.sharedMaterial=r.name.Contains("back")?rubber:structure;
            }
            var fittings=GameObject.Find("05 - Ship fittings and doors");
            foreach(var r in fittings.GetComponentsInChildren<MeshRenderer>())
            {
                if(r.sharedMaterial.name.Contains("Worn")||r.sharedMaterial.name.Contains("reinforced"))r.sharedMaterial=structure;
                if(r.name.Contains("Gate hazard"))r.sharedMaterial=r.sharedMaterial.name.Contains("Amber")?stripe:rubber;
            }
            var ceilings=GameObject.Find("06 - Ceilings hidden in overhead view");
            foreach(var r in ceilings.GetComponentsInChildren<MeshRenderer>())
                if(!r.name.Contains("worklight"))r.sharedMaterial=structure;
            // Fit individual panels to each wall, never tile a complete wall atlas across a surface.
            foreach(var r in layout.GetComponentsInChildren<MeshRenderer>().Concat(fittings.GetComponentsInChildren<MeshRenderer>()).ToArray())
            {
                string n=r.name;
                if(n.StartsWith("Hull wall")||n.StartsWith("Jumpseat ")||n.StartsWith("Cargo room wall")||n.Contains("Cargo room northeast")||n.Contains("Cargo room southeast")||n.Contains("projecting bulkhead")||n.StartsWith("Booth ")&&!n.Contains("header"))
                    Clad(r.transform);
            }
            // Replace crude console boxes with the bevelled Blender model; keep collision footprints.
            foreach(var t in fittings.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Forward systems")||t.name=="Ramp operations").ToArray())
            {
                foreach(var r in t.GetComponentsInChildren<MeshRenderer>())r.enabled=false;
                var model=Model("BridgeConsole",art,t.position,t.forward,Vector3.one*(t.name=="Ramp operations"?.74f:1));
                model.name=t.name+" - Blender console";
            }
            // Long central service channel and cable conduits match the reference's continuous aisle.
            var channel=Box("Recessed central service strip",P(438,286,.016f),new Vector3(35.1f,.025f,1.12f),grate);UV(channel.GetComponent<MeshFilter>(),2);
            foreach(float side in new[]{-1f,1f})
            {
                Box("Aisle steel edging",P(438,286,.034f)+Vector3.forward*side*.64f,new Vector3(35.1f,.025f,.065f),structure);
                Box("Aisle thin conduit",P(438,286,.039f)+Vector3.forward*side*.78f,new Vector3(35.1f,.025f,.026f),stripe);
            }
            // Route markers on both sides of the central mouth, pointing toward the boarding bay.
            foreach(float yy in new[]{270f,303f})for(int i=0;i<3;i++)
            {
                var a=P(482+i*8,yy,.05f);var b=a+new Vector3(.28f,0,.18f);var c=a+new Vector3(.28f,0,-.18f);
                Beam("Bay direction marker",a,b,.035f,blue);Beam("Bay direction marker",a,c,.035f,blue);
            }
            // Perimeter inset lighting and beams; red at the nose, blue around the outer passages.
            foreach(var r in layout.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Hull wall")))
            {
                var t=r.transform;var length=t.lossyScale.z;var inward=(P(380,286)-t.position);inward.y=0;
                var normal=Vector3.Dot(t.right,inward)>0?t.right:-t.right;
                int count=Mathf.Max(1,Mathf.FloorToInt(length/3));
                for(int i=0;i<count;i++)
                {
                    var pos=t.position+t.forward*((i+.5f)*length/count-length/2)+normal*(t.lossyScale.x/2+.06f);
                    pos.y=2.7f;
                    var housing=Box("Perimeter lamp bezel",pos,new Vector3(.13f,.64f,.22f),structure);housing.transform.rotation=t.rotation;
                    var lamp=Box("Perimeter inset light",pos+normal*.08f,new Vector3(.035f,.44f,.09f),t.position.x<P(165,286).x?red:blue);lamp.transform.rotation=t.rotation;
                }
                foreach(float h in new[]{.2f,3.85f})
                {
                    var pos=t.position+normal*(t.lossyScale.x/2+.07f);pos.y=h;
                    var rail=Box("Hull reinforcement rail",pos,new Vector3(.14f,.12f,length),structure);rail.transform.rotation=t.rotation;
                }
            }
            // Ceiling ribs stay with the ceiling toggle; restore removes these before rebuilding.
            foreach(var t in ceilings.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Art roof")).ToArray())Object.DestroyImmediate(t.gameObject);
            foreach(float x in new[]{275f,335f,400f,510f,570f})
            {
                var rib=Model("BevelledBeam",ceilings.transform,P(x,286,4.17f),Vector3.forward,new Vector3( .22f,1,1));
                rib.name="Art roof longitudinal brace";rib.transform.localScale=new Vector3(19,.8f,1);rib.transform.rotation=Quaternion.Euler(0,90,0);
            }
            // Avoid black flat lighting while retaining a dim interior and clear walkway silhouettes.
            RenderSettings.ambientLight=new Color(.36f,.40f,.45f);
            GameObject.Find("Single soft blockout light").GetComponent<Light>().intensity=.55f;
            var lights=Object.FindObjectsByType<Light>().Where(l=>l.type==LightType.Point).ToArray();
            foreach(var l in lights){l.intensity=3.3f;l.range=12; if(l.transform.position.x<P(200,286).x)l.color=new Color(1,.16f,.09f);}
            Object.FindAnyObjectByType<ExplorerController>().eye.backgroundColor=new Color(.018f,.025f,.035f);
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
            Debug.Log("Reference art applied: four texture families, four fitted armor variants, Blender consoles, perimeter markers and service aisle.");
        }
        static Material Mat(string name,string texture,Color tint,bool unlit=false)
        {
            string path=Root+"/Materials/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null){m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Simple Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",tint);if(!unlit)m.SetFloat("_Smoothness",.22f);
            if(texture!=null)
            {
                string tp=Root+"/Textures/"+texture+".png";var imp=(TextureImporter)AssetImporter.GetAtPath(tp);
                imp.maxTextureSize=1024;imp.mipmapEnabled=true;imp.wrapMode=texture=="WallPanels"?TextureWrapMode.Clamp:TextureWrapMode.Repeat;
                imp.filterMode=FilterMode.Trilinear;imp.anisoLevel=2;imp.textureCompression=TextureImporterCompression.Compressed;imp.isReadable=false;imp.SaveAndReimport();
                m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
            }
            EditorUtility.SetDirty(m);return m;
        }
        static GameObject Box(string name,Vector3 pos,Vector3 scale,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(art,false);go.transform.position=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;go.isStatic=true;return go;
        }
        static void Beam(string name,Vector3 a,Vector3 b,float width,Material mat)
        {
            var g=Box(name,(a+b)/2,new Vector3(width,.03f,Vector3.Distance(a,b)),mat);g.transform.rotation=Quaternion.LookRotation(b-a);
        }
        static GameObject Model(string name,Transform parent,Vector3 pos,Vector3 forward,Vector3 scale)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/"+name+".fbx");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(source);go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(forward));go.transform.localScale=scale;
            foreach(var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterials=r.sharedMaterials.Select(m=>m.name=="PanelAtlas"?wall:m.name=="Glass"?glass:m.name=="Phosphor"?cyan:m.name=="Amber"?amber:structure).ToArray();r.gameObject.isStatic=true;
            }
            go.isStatic=true;return go;
        }
        static void Clad(Transform t)
        {
            var s=t.lossyScale;
            // Existing collision remains authoritative. Wall art projects at most 22 cm from it.
            if(s.y<2)return;
            bool thinX=s.x<s.z;
            for(int axis=0;axis<(Mathf.Min(s.x,s.z)>1?2:1);axis++)
            {
                bool x=axis==0?thinX:!thinX;float len=x?s.z:s.x,depth=x?s.x:s.z;
                var tangent=x?t.forward:t.right;var normal=x?t.right:t.forward;
                int count=Mathf.Max(1,Mathf.RoundToInt(len/2.8f));float width=len/count;
                foreach(float sign in new[]{-1f,1f})for(int i=0;i<count;i++)
                {
                    var pos=t.position+tangent*((i+.5f)*width-len/2)+normal*sign*(depth/2-.13f);pos.y=.34f;
                    int variant=(i+(int)(Mathf.Abs(t.position.x)*3)+ (sign>0?1:0))%4;
                    var panel=Model("ArmorPanel"+variant,art,pos,normal*sign,new Vector3(width/2.8f,1,1));panel.name=t.name+" / armor "+i+" / "+sign;
                }
            }
        }
        static void UV(MeshFilter f,float metres)
        {
            string path=Root+"/Geometry/Art/"+f.name+" UV.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null){mesh=Object.Instantiate(f.sharedMesh);AssetDatabase.CreateAsset(mesh,path);}
            var verts=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[verts.Length];var s=f.transform.lossyScale;
            for(int i=0;i<verts.Length;i++){var v=Vector3.Scale(verts[i],s);var n=normals[i];uv[i]=Mathf.Abs(n.y)>.5f?new Vector2(v.x,v.z)/metres:Mathf.Abs(n.x)>.5f?new Vector2(v.z,v.y)/metres:new Vector2(v.x,v.y)/metres;}
            mesh.uv=uv;EditorUtility.SetDirty(mesh);f.sharedMesh=mesh;
        }
    }
}
