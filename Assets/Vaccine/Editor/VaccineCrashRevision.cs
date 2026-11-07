using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace Vaccine.Editor
{
    public static class VaccineCrashRevision
    {
        const string Root="Assets/Vaccine";
        public const float WingShift=7.2f, CorridorWidth=10.8f;
        static Transform revision, roofs, plane;
        static readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
        static int meshId;
        static GameObject Find(string n)=>GameObject.Find(n)??throw new InvalidOperationException("Missing scene object: "+n);
        static void Save(){AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());}
        [MenuItem("Vaccine/Apply Narrow Station and Crash Revision")]
        public static string Apply()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=VaccineBlockoutBuilder.ScenePath)throw new InvalidOperationException("Open Vaccine outside Play.");
            if(GameObject.Find("13 Narrow station and crash collapse"))throw new InvalidOperationException("Revision already applied.");
            VaccineArtBatcher.Restore();
            Directory.CreateDirectory(Root+"/Geometry/CrashRevision");AssetDatabase.Refresh();
            foreach(var id in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials/Art"})) {var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(id));mats[m.name]=m;}
            revision=new GameObject("13 Narrow station and crash collapse").transform;
            roofs=Find("08 Roofs - hidden for overhead review").transform;plane=Find("04 Crashed aircraft - belly on ground - no landing gear").transform;
            meshId=0;NarrowStation();CargoBoundary();TicketWindow();TornAircraft();UpperStorey();CrashDebris();Route();
            Physics.SyncTransforms();Save();return "Station corridor and passage 10.8m; cargo boundary moved; electrical interior preserved; panoramic window, torn aircraft and upper-storey collapse saved.";
        }
        static Transform Group(string n,Transform parent=null){var t=new GameObject(n).transform;t.SetParent(parent?parent:revision,false);return t;}
        static GameObject Box(string n,Vector3 p,Vector3 size,string mat="Concrete",Transform parent=null,bool solid=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(parent?parent:revision,false);g.transform.localPosition=p;g.transform.localScale=size;g.GetComponent<MeshRenderer>().sharedMaterial=mats[mat];g.isStatic=true;
            if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());if(mats[mat].mainTexture)UV(g.GetComponent<MeshFilter>());return g;
        }
        static void UV(MeshFilter f)
        {
            var m=Object.Instantiate(f.sharedMesh);var vs=m.vertices;var ns=m.normals;var uv=new Vector2[vs.Length];
            for(int i=0;i<vs.Length;i++){var p=Vector3.Scale(vs[i],f.transform.lossyScale);var n=ns[i];uv[i]=(Mathf.Abs(n.y)>.65f?new Vector2(p.x,p.z):Mathf.Abs(n.x)>Mathf.Abs(n.z)?new Vector2(p.z,p.y):new Vector2(p.x,p.y))/2.4f;}
            m.uv=uv;m.name=f.name+" revision UV";AssetDatabase.CreateAsset(m,Root+"/Geometry/CrashRevision/"+(++meshId).ToString("D3")+".asset");f.sharedMesh=m;
        }
        static void Resize(string n,Vector3 p,Vector3 s){var g=Find(n);g.transform.position=p;g.transform.localScale=s;var f=g.GetComponent<MeshFilter>();if(f)UV(f);}
        static void WallX(string n,float x,float z0,float z1,float height=5)
        {var t=Find(n).transform;t.rotation=Quaternion.identity;Resize(n,new Vector3(x,height/2,(z0+z1)/2),new Vector3(.35f,height,z1-z0));}
        static void WallZ(string n,float z,float x0,float x1,float height=5)
        {var t=Find(n).transform;t.rotation=Quaternion.Euler(0,90,0);Resize(n,new Vector3((x0+x1)/2,height/2,z),new Vector3(.35f,height,x1-x0));}
        static void Floor(string n,float x0,float x1,float z0,float z1,float top=0)
        {Resize(n,new Vector3((x0+x1)/2,top-.25f,(z0+z1)/2),new Vector3(x1-x0,.5f,z1-z0));}
        static void NarrowStation()
        {
            Find("06 Three waiting rooms").transform.position+=Vector3.left*WingShift;
            Find("07 Only open descent - platform layout pending").transform.position+=Vector3.left*WingShift;
            WallZ("Station south boundary",-40,0,79.8f);WallZ("Station north boundary",24,50,79.8f);
            WallX("Station corridor west wall",50,-29.2f,14.5f);
            WallZ("Ticket hall south short return",-29.2f,0,11);WallZ("Ticket hall south east return",-29.2f,25,33);
            WallZ("Exterior approach end wall",-29.2f,33,50);
            Floor("East station corridor floor",50,60.8f,-29.2f,24);
            Floor("South passage floor",0,60.8f,-40,-29.2f);
            Floor("Ticket hall floor",0,33,-29.2f,0);
            Floor("Exterior approach to station entrance",33,50,-29.2f,0);
            Floor("Ticket hall roof",0,33,-29.2f,0,5.35f);
            Floor("South passage roof",0,60.8f,-40,-29.2f,5.35f);
            Floor("Corridor roof before crash opening",50,60.8f,-29.2f,6,5.35f);
            foreach(var t in roofs.Cast<Transform>().Where(t=>t.name.StartsWith("Waiting room roof")))t.position+=Vector3.left*WingShift;
            Resize("Enclosed subway descent roof",new Vector3(77.9f,5.18f,10.65f),new Vector3(21.8f,.3f,9.2f));
            // Added art was authored in world coordinates; move room fittings with their rooms.
            var art=Find("10 Vaccine night art").transform;
            foreach(var t in art.Cast<Transform>().ToArray())
            {
                if(t.name=="11 Final surface details")
                {
                    foreach(var child in t.Cast<Transform>())
                    {
                        if(child.position.x>=68&&child.position.z<24)child.position+=Vector3.left*WingShift;
                        else if(child.name=="Discarded paper"&&child.position.x>=50){var p=child.position;p.x=50+(p.x-50)*.6f;child.position=p;}
                    }
                }
                else if(t.position.x>=68&&t.position.z<24)t.position+=Vector3.left*WingShift;
                else if(t.position.x>56&&t.position.x<68&&t.position.z<12&&(t.name.Contains("ceiling")||t.name.StartsWith("Ceiling"))){var p=t.position;p.x=55.4f;t.position=p;}
            }
        }
        static void CargoBoundary()
        {
            WallX("West yard boundary",-22,0,27,4);
            Floor("Courtyard",-22,50,0,50);
            var yard=Find("02 Outdoor crash courtyard").transform;
            foreach(var t in yard.Cast<Transform>().Where(t=>t.name=="Cargo container"||t.name=="Container ribs"))t.position+=Vector3.right*15.05f;
            var art=Find("10 Vaccine night art").transform;
            foreach(var t in art.Cast<Transform>().Where(t=>t.name=="Container locking bars"))t.position+=Vector3.right*15.05f;
            // Relocate only the floodlight beside the moved containers, not electrical-room fittings.
            foreach(var t in art.Cast<Transform>().Where(t=>Mathf.Abs(t.position.x+34)<.2f&&t.position.z<4&&t.position.z>2))t.position+=Vector3.right*15.05f;
        }
        static void TicketWindow()
        {
            Object.DestroyImmediate(Find("Ticket hall north wall"));
            var glass=new Material(Shader.Find("Universal Render Pipeline/Simple Lit")){name="Station panoramic glass",renderQueue=(int)RenderQueue.Transparent,enableInstancing=true};
            glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_SrcBlendAlpha",1);glass.SetFloat("_DstBlendAlpha",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);glass.SetFloat("_Cull",0);glass.SetColor("_BaseColor",new Color(.23f,.39f,.44f,.17f));glass.SetFloat("_Smoothness",.7f);glass.SetColor("_SpecColor",new Color(.16f,.2f,.23f));glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.SetOverrideTag("RenderType","Transparent");
            AssetDatabase.CreateAsset(glass,Root+"/Materials/Art/Station panoramic glass.mat");mats[glass.name]=glass;
            var g=Group("Ticket hall full courtyard glazing");
            Box("Window masonry sill",new Vector3(16.5f,.5f,0),new Vector3(33,1,.35f),"Concrete",g);
            Box("Window top structural beam",new Vector3(16.5f,4.8f,0),new Vector3(33,.4f,.45f),"Steel",g);
            for(int i=0;i<6;i++)Box("Panoramic glass pane "+i,new Vector3(2.75f+i*5.5f,2.8f,0),new Vector3(5.36f,3.6f,.065f),glass.name,g);
            for(int i=0;i<=6;i++)Box("Window vertical mullion",new Vector3(i*5.5f,2.8f,0),new Vector3(.14f,3.7f,.18f),"Steel",g);
            Box("Window bottom frame",new Vector3(16.5f,1.03f,0),new Vector3(33,.12f,.18f),"Steel",g);
        }
        static Vector4 Section(float z)
        {
            var rings=new[]{new Vector4(0,1.3f,3.6f,6.1f),new Vector4(6,2.8f,2,6.6f),new Vector4(13,3.5f,.08f,6.8f),new Vector4(37,3.6f,.08f,6.8f),new Vector4(49,3.3f,.12f,5.1f),new Vector4(57,2.9f,.2f,3.5f),new Vector4(63.25f,.25f,.5f,2.2f)};
            for(int i=0;i<rings.Length-1;i++)if(z<=rings[i+1].x)return Vector4.Lerp(rings[i],rings[i+1],Mathf.InverseLerp(rings[i].x,rings[i+1].x,z));return rings.Last();
        }
        static bool Hole(int row,int side,float[] zs)
        {
            float a=zs[row],b=zs[row+1];bool middle=(side<=3||side>=23)&&a>=41&&b<=51;bool edge=(side==4||side==5||side==21||side==22)&&a>=44&&b<=48;
            return middle||edge;
        }
        static void TornAircraft()
        {
            var fuselage=Find("Grounded fuselage with upswept tail cavity");
            float[] zs={0,6,13,37,41,44,48,51,57,63.25f};const int sides=24;
            var vs=new List<Vector3>();var uv=new List<Vector2>();var outer=new List<int>();var inner=new List<int>();
            foreach(float z in zs)
            {
                var r=Section(z);for(int j=0;j<=sides;j++){float a=j*Mathf.PI*2/sides;vs.Add(new Vector3(Mathf.Cos(a)*r.y,(r.z+r.w)*.5f+Mathf.Sin(a)*(r.w-r.z)*.5f,z));uv.Add(new Vector2(z/4,a*3.4f/4));}
            }
            for(int i=0;i<zs.Length-1;i++)for(int j=0;j<sides;j++)if(!Hole(i,j,zs))
            {int a=i*(sides+1)+j,b=a+1,c=a+sides+1,d=c+1;outer.AddRange(new[]{a,b,d,a,d,c});inner.AddRange(new[]{a,d,b,a,c,d});}
            for(int j=1;j<sides-1;j++){int last=(zs.Length-1)*(sides+1);outer.AddRange(new[]{0,j+1,j,last,last+j,last+j+1});}
            int shellCount=vs.Count;
            var mesh=new Mesh{name="Torn open fuselage and crushed nose"};mesh.SetVertices(vs.Concat(vs).ToList());mesh.SetUVs(0,uv.Concat(uv).ToList());mesh.subMeshCount=2;mesh.SetTriangles(outer,0);mesh.SetTriangles(inner.Select(i=>i+shellCount).ToArray(),1);mesh.RecalculateBounds();
            // Independent inner skin prevents opposite winding from cancelling shell normals.
            var normals=vs.Select(v=>{var r=Section(v.z);return new Vector3(v.x/(r.y*r.y),(v.y-(r.z+r.w)/2)/Mathf.Pow((r.w-r.z)/2,2),.03f).normalized;}).ToArray();mesh.normals=normals.Concat(normals.Select(n=>-n)).ToArray();
            AssetDatabase.CreateAsset(mesh,Root+"/Geometry/CrashRevision/TornFuselage.asset");fuselage.GetComponent<MeshFilter>().sharedMesh=mesh;fuselage.GetComponent<MeshRenderer>().sharedMaterials=new[]{mats["Aircraft"],mats["Steel"]};
            var collision=new Mesh{name="Torn fuselage collision shell"};collision.SetVertices(vs);collision.SetTriangles(outer,0);collision.RecalculateBounds();AssetDatabase.CreateAsset(collision,Root+"/Geometry/CrashRevision/TornFuselageCollider.asset");fuselage.GetComponent<MeshCollider>().sharedMesh=collision;
            var edges=Group("Jagged torn aluminium edges",plane);
            for(int i=0;i<zs.Length-1;i++)for(int j=0;j<sides;j++)if(Hole(i,j,zs))
            {
                int a=i*(sides+1)+j,b=a+1,c=a+sides+1,d=c+1;
                if(i==0||!Hole(i-1,j,zs))Lip(vs[a],vs[b],edges);
                if(i==zs.Length-2||!Hole(i+1,j,zs))Lip(vs[c],vs[d],edges);
                if(!Hole(i,(j+sides-1)%sides,zs))Lip(vs[a],vs[c],edges);
                if(!Hole(i,(j+1)%sides,zs))Lip(vs[b],vs[d],edges);
            }
            foreach(var t in plane.GetComponentsInChildren<Transform>().ToArray())
            {
                var local=plane.InverseTransformPoint(t.position);
                if((t.name.Contains("Passenger window")||t.name=="Dark passenger glass")&&local.x>0&&local.z>=40&&local.z<=52)Object.DestroyImmediate(t.gameObject);
                else if(t.name=="Cockpit glazing"){t.localPosition=new Vector3(t.localPosition.x,2.7f,t.localPosition.z);t.localRotation*=Quaternion.Euler(0,0,12);}
            }
        }
        static void Lip(Vector3 a,Vector3 b,Transform parent)
        {
            var mid=(a+b)/2;var r=Section(mid.z);var normal=new Vector3(mid.x,mid.y-(r.z+r.w)/2,0).normalized;
            var m=new Mesh{name="Bent torn metal lip"};m.vertices=new[]{a,b,mid+normal*.38f+Vector3.forward*.23f};m.triangles=new[]{0,1,2,2,1,0};m.uv=new[]{Vector2.zero,Vector2.right,Vector2.up};m.RecalculateNormals();m.RecalculateBounds();
            AssetDatabase.CreateAsset(m,Root+"/Geometry/CrashRevision/Lip-"+(++meshId)+".asset");var g=new GameObject("Bent fuselage skin",typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.GetComponent<MeshFilter>().sharedMesh=m;g.GetComponent<MeshRenderer>().sharedMaterial=mats["Steel"];g.isStatic=true;
        }
        static void UpperStorey()
        {
            var upper=Group("Station upper storey - hidden with review roofs",roofs);
            // Upper floor is architectural scenery; the mission still routes down to the platform.
            Box("Upper south facade",new Vector3(39.9f,7.9f,-40),new Vector3(79.8f,5.2f,.4f),"Concrete",upper);
            Box("Upper passage courtyard facade",new Vector3(25,7.9f,-29.2f),new Vector3(50,5.2f,.4f),"Concrete",upper);
            Box("Upper west passage return",new Vector3(0,7.9f,-34.6f),new Vector3(.4f,5.2f,10.8f),"Concrete",upper);
            Box("Upper east facade",new Vector3(79.8f,7.9f,-17),new Vector3(.4f,5.2f,46),"Concrete",upper);
            // Tall west facade with genuine glazed openings, ending at the collapsed impact bay.
            Box("Upper west facade sill",new Vector3(50,5.95f,-11.6f),new Vector3(.4f,1.3f,35.2f),"Concrete",upper);
            Box("Upper west facade lintel",new Vector3(50,10.1f,-11.6f),new Vector3(.5f,.8f,35.2f),"Concrete",upper);
            for(int i=0;i<6;i++)
            {
                float z=-29.2f+i*5.8667f;
                Box("Upper concrete pier",new Vector3(50,8.1f,z),new Vector3(.65f,3.1f,.65f),"Concrete",upper);
                Box("Upper dusty glazing",new Vector3(50,8.1f,z+2.93f),new Vector3(.06f,3.1f,5.2f),"Station panoramic glass",upper);
            }
            Box("Upper east wing roof",new Vector3(64.9f,10.65f,-11.6f),new Vector3(29.8f,.4f,35.2f),"Concrete",upper);
            Box("Upper south passage roof",new Vector3(25,10.65f,-34.6f),new Vector3(50,.4f,10.8f),"Concrete",upper);
            Box("Upper room block roof",new Vector3(70.3f,10.65f,-32),new Vector3(19, .4f,16),"Concrete",upper);
            // Ruined storey at the impact: broken piers and hanging slab fragments, no intact box over the nose.
            foreach(var p in new[]{new Vector3(50,6.7f,7),new Vector3(79.8f,7.2f,7),new Vector3(79.8f,6.1f,23)})
                Box("Sheared upper-storey pier",p,new Vector3(.85f,3.8f,.85f),"Concrete",upper).transform.Rotate(0,0,p.x<60?-8:7);
            Box("Broken floor cantilever",new Vector3(71.5f,5.45f,7),new Vector3(13,.5f,3.2f),"Concrete",upper).transform.Rotate(8,0,-5);
            for(int i=0;i<7;i++)Beam("Exposed upper floor reinforcement",new Vector3(53+i*3.5f,5.45f,5.8f),new Vector3(53+i*3.5f,5.1f,10+(i%3)),.045f,"Rust",upper);
        }
        static void Beam(string n,Vector3 a,Vector3 b,float radius,string mat,Transform parent)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name=n;g.transform.SetParent(parent,false);g.transform.localPosition=(a+b)/2;g.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);g.transform.localScale=new Vector3(radius*2,(b-a).magnitude/2,radius*2);Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mats[mat];g.isStatic=true;
        }
        static GameObject Debris(string n,Vector3 p,Vector3 size,Vector3 rot,int index,Transform parent=null)
        {
            var g=new GameObject(n,typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider));g.transform.SetParent(parent?parent:revision,false);g.transform.localPosition=p;g.transform.localScale=size;g.transform.localRotation=Quaternion.Euler(rot);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/ConcreteDebris"+(index%3)+".fbx");g.GetComponent<MeshFilter>().sharedMesh=prefab.GetComponentInChildren<MeshFilter>().sharedMesh;g.GetComponent<MeshRenderer>().sharedMaterial=mats["Concrete"];g.isStatic=true;UV(g.GetComponent<MeshFilter>());return g;
        }
        static void CrashDebris()
        {
            var rear=Group("Far side collapsed masonry",plane);var random=new System.Random(37);
            for(int i=0;i<68;i++)
            {
                float x=-6.8f-(float)random.NextDouble()*11,z=8+(float)random.NextDouble()*52;
                var wp=plane.TransformPoint(new Vector3(x,0,z));if(wp.z>48||wp.x>48)continue;
                float h=1.2f+(float)random.NextDouble()*3.5f;
                Debris("Far-side concrete wreckage",new Vector3(x,h*.44f,z),new Vector3(2.5f+(float)random.NextDouble()*3.5f,h,2.7f+(float)random.NextDouble()*3),new Vector3(random.Next(-20,21),random.Next(180),random.Next(-25,26)),i,rear);
            }
            var boundary=Group("Far-side route boundary and spawn pocket",plane);
            var barrier=new GameObject("Invisible backstop - behind zombie staging pocket",typeof(BoxCollider));barrier.transform.SetParent(boundary,false);barrier.transform.localPosition=new Vector3(-4.8f,5,32);barrier.GetComponent<BoxCollider>().size=new Vector3(.5f,10,68);
            var pocket=Group("Four zombie staging positions - no active spawners",plane);
            foreach(float x in new[]{-3.6f,-2.2f})foreach(float z in new[]{1f,3f})Group("Future sprinter staging",pocket).localPosition=new Vector3(x,0,z);
            // Dense collapsed structure seals the impact breach and visibly crushes the nose.
            var crash=Group("Collapsed second storey over crushed aircraft nose");
            for(int i=0;i<12;i++)
            {
                float x=46+(i%4)*4.6f,z=16.5f+(i/4)*3.1f;
                Debris("Collapsed upper floor slab",new Vector3(x,3.3f+(i/4)*1.5f,z),new Vector3(6.3f,.9f,4.5f),new Vector3(12+i%3*10,i*17,-18+i%4*12),i,crash);
                if(i%2==0)Debris("Crushed wall masonry",new Vector3(x,1.4f,z+1),new Vector3(3.2f,3.3f,3.6f),new Vector3(0,i*13,17),i+1,crash);
            }
            // Continuous collision core removes the old side gap, independent of decorative rubble gaps.
            Box("Impact breach sealed collision core",new Vector3(53.5f,2.9f,17.8f),new Vector3(17,5.8f,5.2f),"Concrete",crash).GetComponent<MeshRenderer>().enabled=false;
            for(int i=0;i<8;i++)Beam("Collapsed structural rebar",new Vector3(47+i*1.8f,4.8f,16),new Vector3(46+i*1.8f,6.2f,19),.055f,"Rust",crash);
        }
        static void Route()
        {
            var route=Find("Review route checkpoints").transform;
            foreach(Transform t in route)
            {
                var p=t.position;
                if(p.x>=68)p.x-=WingShift;else if(p.x>=50)p.x=55.4f;
                if(p.z==-33)p.z=-34.6f;t.position=p;
            }
            var labelRoot=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().First(g=>g.name=="09 Overhead labels");
            foreach(Transform t in labelRoot.transform){if(t.position.x>=68)t.position+=Vector3.left*WingShift;else if(t.position.x>=50){var p=t.position;p.x=55.4f;t.position=p;}}
        }
        public static string CloseRubbleFooting()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
            revision=Find("13 Narrow station and crash collapse").transform;
            if(revision.Find("Packed masonry at impact footing"))return "Footing already closed.";
            foreach(var id in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials/Art"})){var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(id));mats[m.name]=m;}
            meshId=500;
            var g=Group("Packed masonry at impact footing");
            for(int i=0;i<9;i++)Debris("Impact footing rubble",new Vector3(46.8f+i*1.8f,.75f+(i%3)*.25f,14.8f+(i%2)*.7f),new Vector3(2.8f,1.8f+(i%3)*.4f,2.7f),new Vector3(8,i*27,9-i%3*8),i,g);
            Save();return "Impact barrier has visible packed rubble down to the ground.";
        }
    }
}
