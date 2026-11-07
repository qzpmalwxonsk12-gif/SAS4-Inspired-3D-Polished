using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Vaccine.Editor
{
    public static class VaccineSubwayBuilder
    {
        const string Folder = "Assets/Vaccine/Materials/SubwayBlockout";
        const float Level = -4;
        static Transform root, roofs, labelRoot;
        static Material floor, wall, edge, steel, train, dark, debris, pale;
        static VaccineWalkthrough player;
        static Transform Group(string n, Transform p = null)
        { var t = new GameObject(n).transform; t.SetParent(p ? p : root, false); return t; }
        static GameObject Box(string n, Vector3 p, Vector3 s, Material m, Transform parent = null, bool solid = true, bool moving = false)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube); g.name = n;
            g.transform.SetParent(parent ? parent : root, false); g.transform.localPosition = p; g.transform.localScale = s;
            g.GetComponent<MeshRenderer>().sharedMaterial = m; g.isStatic = !moving;
            if (!solid) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
        static Material Mat(string n, Color c)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = n, enableInstancing = true };
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", .12f);
            AssetDatabase.CreateAsset(m, Folder + "/" + n + ".mat"); return m;
        }
        static void Floor(string n, float x0, float x1, float z0, float z1, float top, Material mat, Transform parent = null)
        { Box(n, new Vector3((x0+x1)/2, top-.25f, (z0+z1)/2), new Vector3(x1-x0,.5f,z1-z0),mat,parent); }
        static TextMeshPro Text(string n, string text, Vector3 pos, Vector2 size, Quaternion rotation, Transform parent, float font = 3)
        {
            var t = new GameObject(n).AddComponent<TextMeshPro>(); t.transform.SetParent(parent,false); t.transform.localPosition = pos; t.transform.localRotation = rotation;
            t.rectTransform.sizeDelta = size; t.font = TMP_Settings.defaultFontAsset; t.fontSize = font; t.text = text;
            t.alignment = TextAlignmentOptions.Center; t.color = Color.white; t.textWrappingMode = TextWrappingModes.NoWrap; return t;
        }
        static void Sign(string text, Vector3 pos, float width, float yaw = 0)
        {
            var g = Group(text); g.position = pos; g.rotation = Quaternion.Euler(0,yaw,0);
            Box("Plain sign backing",Vector3.zero,new Vector3(width,.9f,.12f),dark,g,false);
            Text("Wayfinding",text,new Vector3(0,0,-.075f),new Vector2(width-.15f,.8f),Quaternion.identity,g,3.2f);
        }
        [MenuItem("Vaccine/Add Underground Subway Blockout")]
        public static string Build()
        {
            if (Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != VaccineBlockoutBuilder.ScenePath) throw new InvalidOperationException("Open Vaccine outside Play.");
            if (GameObject.Find("14 Underground subway blockout")) throw new InvalidOperationException("Subway already exists; edit it in place.");
            VaccineArtBatcher.Restore();
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            root = new GameObject("14 Underground subway blockout").transform;
            roofs = Group("Subway ceiling and train roofs", GameObject.Find("08 Roofs - hidden for overhead review").transform);
            labelRoot = Group("Subway overhead labels");
            player = Object.FindAnyObjectByType<VaccineWalkthrough>();
            floor=Mat("Platform grey",new Color(.45f,.47f,.49f)); wall=Mat("Underground concrete grey",new Color(.57f,.59f,.60f));
            edge=Mat("Platform edge pale grey",new Color(.76f,.77f,.76f)); steel=Mat("Track and frames charcoal",new Color(.22f,.24f,.26f));
            train=Mat("Train shell neutral grey",new Color(.65f,.68f,.70f)); dark=Mat("Window recess charcoal",new Color(.075f,.09f,.105f));
            debris=Mat("Collapse grey",new Color(.38f,.40f,.42f)); pale=Mat("Ceiling light white",new Color(.86f,.89f,.90f));
            pale.EnableKeyword("_EMISSION"); pale.SetColor("_EmissionColor",new Color(.6f,.65f,.7f));
            ConnectStairs(); Platforms(); Maintenance();
            Train("Platform 1 boarding train",100,19.5f,false); Train("Platform 2 damaged train",126.4f,-32.5f,true);
            Collapse(); Lighting(); Labels();
            var map = player.gameObject.AddComponent<SubwayReviewMap>(); map.player=player; map.undergroundLabels=labelRoot.gameObject;
            labelRoot.gameObject.SetActive(false); Physics.SyncTransforms();
            AssetDatabase.SaveAssets(); var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene(); EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            return "Underground greybox saved: parallel platforms, closed maintenance room, two three-car trains, 12 automatic sliding doorways and sealed far-end collapse.";
        }
        static void ConnectStairs()
        {
            var old=GameObject.Find("Lower landing end boundary"); Object.DestroyImmediate(old);
            var sign=GameObject.Find("PLATFORM / NEXT AREA"); if(sign)Object.DestroyImmediate(sign);
            // Art trims for the removed wall are siblings, so remove only the matching end cap.
            var stairs=GameObject.Find("07 Only open descent - platform layout pending").transform;
            foreach(var t in stairs.Cast<Transform>().Where(t=>t.name=="Dark wall cap"&&Mathf.Abs(t.position.x-88.8f)<.1f).ToArray())Object.DestroyImmediate(t.gameObject);
            Box("Subway entrance lintel",new Vector3(88.8f,.1f,10.65f),new Vector3(.5f,.7f,9.3f),wall);
            Sign("PLATFORM 1  /  BOARD TRAIN",new Vector3(92,-.9f,15.9f),12);
        }
        static void Platforms()
        {
            Floor("Platform 1 floor",88.8f,184,2,17,Level,floor);
            Floor("Central platform connection",88.8f,184,-16,2,Level,floor);
            Floor("Platform 2 floor",88.8f,184,-30,-16,Level,floor);
            Tracks("North track",17,24,19.5f); Tracks("South track",-37,-30,-32.5f);
            Box("Subway north retaining wall",new Vector3(136.4f,-1.7f,24),new Vector3(95.2f,7,.5f),wall);
            Box("Subway south retaining wall",new Vector3(136.4f,-1.7f,-37),new Vector3(95.2f,7,.5f),wall);
            Box("Subway east retaining wall",new Vector3(184,-1.7f,-6.5f),new Vector3(.6f,7,61),wall);
            Box("West wall below stair mouth",new Vector3(88.8f,-1.7f,-15.5f),new Vector3(.5f,7,43),wall);
            Box("West wall above stair mouth",new Vector3(88.8f,-1.7f,19.5f),new Vector3(.5f,7,9),wall);
            Floor("Underground roof slab",88.8f,184,-37,24,1.3f,wall,roofs);
            for(int i=0;i<6;i++)
            {
                float x=97+i*13;
                foreach(float z in new[]{.5f,-14.5f}) Box("Square platform support",new Vector3(x,-1.45f,z),new Vector3(.8f,5.1f,.8f),wall);
                Box("Transverse ceiling beam",new Vector3(x,.65f,-6.5f),new Vector3(.8f,.7f,61),steel,roofs);
            }
            foreach(float z in new[]{16.72f,-29.72f}) Box("Plain platform edge strip",new Vector3(136.4f,-3.98f,z),new Vector3(95.2f,.04f,.5f),edge,null,false);
            // Waist rails guard exposed track drops; openings align exactly with the train doors.
            EdgeRail(17,100); EdgeRail(-30,126.4f);
            Sign("1  /  BOARDING",new Vector3(118,-.65f,15.9f),10);
            Sign("2  /  DAMAGED TRAIN",new Vector3(122,-.65f,-16.5f),13);
            Sign("MAINTENANCE  >     PLATFORM 2  >",new Vector3(110,-.7f,2.8f),17);
        }
        static void Tracks(string n,float z0,float z1,float center)
        {
            var g=Group(n); Floor(n+" bed",88.8f,184,z0,z1,-5.15f,steel,g);
            foreach(float z in new[]{center-.85f,center+.85f})Box("Rail",new Vector3(136.4f,-4.97f,z),new Vector3(95.2f,.2f,.12f),dark,g);
            for(float x=89.5f;x<184;x+=2)Box("Track sleeper",new Vector3(x,-5.05f,center),new Vector3(.25f,.17f,3.1f),dark,g);
            foreach(float x in new[]{89.05f,183.7f})Box("Closed tunnel end",new Vector3(x,-1.7f,center),new Vector3(.4f,6.5f,6.7f),dark,g);
        }
        static void EdgeRail(float z,float start)
        {
            var openings=new List<Vector2>();for(int car=0;car<3;car++)foreach(float d in new[]{4.5f,12.5f})openings.Add(new Vector2(start+car*18.3f+d-1.4f,start+car*18.3f+d+1.4f));
            float cursor=89;foreach(var gap in openings){Rail(cursor,gap.x,z);cursor=gap.y;}Rail(cursor,183.8f,z);
        }
        static void Rail(float a,float b,float z)
        {
            if(b<=a)return;
            Box("Track edge guard rail",new Vector3((a+b)/2,-3.05f,z),new Vector3(b-a,.1f,.1f),steel);
            for(float x=a+.12f;x<b;x+=3)Box("Edge rail post",new Vector3(x,-3.5f,z),new Vector3(.1f,1,.1f),steel);
        }
        static void Maintenance()
        {
            var g=Group("Maintenance room - no access");
            foreach(float z in new[]{-16,2})Box("Maintenance partition",new Vector3(99.8f,-1.45f,z),new Vector3(22,5.1f,.4f),wall,g);
            Box("Maintenance east wall lower",new Vector3(110.8f,-1.45f,-12.1f),new Vector3(.4f,5.1f,7.8f),wall,g);
            Box("Maintenance east wall upper",new Vector3(110.8f,-1.45f,-1.9f),new Vector3(.4f,5.1f,7.8f),wall,g);
            Box("Maintenance door header",new Vector3(110.8f,.05f,-7),new Vector3(.4f,2.1f,2.4f),wall,g);
            Box("Locked maintenance door",new Vector3(110.8f,-2.5f,-7),new Vector3(.22f,3,2.4f),steel,g);
            Sign("MAINTENANCE\nNO ACCESS",new Vector3(111.05f,-1.1f,-7),6,-90);
        }
        static void Train(string name,float start,float z,bool damaged)
        {
            var g=Group(name);
            for(int i=0;i<3;i++)
            {
                float x=start+i*18.3f; bool crushed=damaged&&i>0;
                var car=Group("Carriage "+(i+1)+(crushed?" - crushed":""),g);
                Floor("Boardable carriage floor",x,x+17,z-2.1f,z+2.1f,Level,steel,car);
                Box("Carriage chassis",new Vector3(x+8.5f,-4.35f,z),new Vector3(16.6f,.45f,3.65f),dark,car);
                foreach(float bx in new[]{x+3,x+14})
                {
                    Box("Bogie block",new Vector3(bx,-4.65f,z),new Vector3(2.7f,.55f,2.8f),steel,car);
                    foreach(float wz in new[]{z-1.65f,z+1.65f})
                    {
                        var wheel=GameObject.CreatePrimitive(PrimitiveType.Cylinder);wheel.name="Blockout wheel";wheel.transform.SetParent(car,false);wheel.transform.position=new Vector3(bx,-4.65f,wz);wheel.transform.rotation=Quaternion.Euler(90,0,0);wheel.transform.localScale=new Vector3(.85f,.15f,.85f);wheel.GetComponent<MeshRenderer>().sharedMaterial=dark;wheel.isStatic=true;Object.DestroyImmediate(wheel.GetComponent<Collider>());
                    }
                }
                float front=damaged?z+2.1f:z-2.1f, back=damaged?z-2.1f:z+2.1f;
                // Closed ends and window recesses give each carriage a simple readable silhouette.
                foreach(float end in new[]{x+.12f,x+16.88f})
                {
                    Box("Carriage end",new Vector3(end,-2.5f,z),new Vector3(.24f,3,4.2f),train,car);
                    Box("Cab end window",new Vector3(end+(end<x+1?-.13f:.13f),-1.9f,z),new Vector3(.025f,1,2.8f),dark,car,false);
                }
                Box("Carriage far side",new Vector3(x+8.5f,-2.5f,back),new Vector3(17,3,.18f),train,car);
                for(int w=0;w<6;w++)Box("Far side window recess",new Vector3(x+1.5f+w*2.8f,-1.95f,back+(damaged?.1f:-.1f)),new Vector3(1.9f,1.1f,.025f),dark,car,false);
                var intervals=new[]{new Vector2(0,3.3f),new Vector2(5.7f,11.3f),new Vector2(13.7f,17)};
                foreach(var v in intervals)
                {
                    float mid=x+(v.x+v.y)/2,len=v.y-v.x;
                    Box("Platform side wall",new Vector3(mid,-2.5f,front),new Vector3(len,3,.18f),train,car);
                    Box("Platform side window recess",new Vector3(mid,-1.95f,front+(damaged?.105f:-.105f)),new Vector3(Mathf.Max(.2f,len-.6f),1.1f,.03f),dark,car,false);
                }
                foreach(float dx in new[]{4.5f,12.5f})
                {
                    Box("Doorway header",new Vector3(x+dx,-1.15f,front),new Vector3(2.4f,.3f,.2f),train,car);
                    float edgeZ=damaged?-30:17;
                    Box("Level boarding threshold",new Vector3(x+dx,-4.07f,(front+edgeZ)/2),new Vector3(2.5f,.14f,Mathf.Abs(front-edgeZ)+.2f),steel,car);
                    Door(x+dx,front,car,damaged);
                }
                // Only the exposed section is furnished; rubble seals the damaged half.
                if(!damaged||i==0)
                {
                    for(int s=0;s<4;s++)
                    {
                        float sx=x+2+s*4;
                        Box("Bench seat",new Vector3(sx,-3.5f,back+(damaged?.55f:-.55f)),new Vector3(2.3f,.25f,.75f),wall,car);
                        Box("Bench back",new Vector3(sx,-3.05f,back+(damaged?.24f:-.24f)),new Vector3(2.3f,.8f,.14f),wall,car);
                    }
                }
                var top=Box("Carriage roof",new Vector3(x+8.5f,crushed?(i==1?-1.35f:-2.15f):-.85f,z),new Vector3(17,.3f,4.35f),train,roofs);
                if(crushed)top.transform.rotation=Quaternion.Euler(i==1?7:13,0,i==1?-5:4);
                if(i<2)Box("Train coupler",new Vector3(x+17.65f,-4.1f,z),new Vector3(1.3f,.3f,.4f),dark,car);
            }
        }
        static void Door(float x,float z,Transform parent,bool south)
        {
            var t=Group("Automatic train doorway",parent);t.position=new Vector3(x,-2.65f,z);
            var d=t.gameObject.AddComponent<SubwayTrainDoor>();d.actor=player.transform;
            d.leftPanel=Box("Sliding left leaf",new Vector3(-.6f,0,0),new Vector3(1.18f,2.7f,.16f),wall,t,true,true).transform;
            d.rightPanel=Box("Sliding right leaf",new Vector3(.6f,0,0),new Vector3(1.18f,2.7f,.16f),wall,t,true,true).transform;
            foreach(var leaf in new[]{d.leftPanel,d.rightPanel})
            {
                // Child coordinates compensate for the primitive panel's scale.
                Box("Door window",new Vector3(0,.15f,south?.55f:-.55f),new Vector3(.65f,.32f,.1f),dark,leaf,false,true);
            }
        }
        static void Collapse()
        {
            var g=Group("Far end collapse - half of train 2 buried");
            var rng=new System.Random(3207);
            for(int i=0;i<72;i++)
            {
                float z=-36+(float)rng.NextDouble()*59, start=z< -16?153:z<2?158:167;
                float x=start+2+(float)rng.NextDouble()*(182-start-2);
                float sx=3+(float)rng.NextDouble()*6, sy=1.3f+(float)rng.NextDouble()*2.4f, sz=2+(float)rng.NextDouble()*5;
                var b=Box("Fallen slab and masonry",new Vector3(x,-3.4f+sy*.45f+(i%4)*.5f,z),new Vector3(sx,sy,sz),debris,g);
                b.transform.rotation=Quaternion.Euler((float)rng.NextDouble()*40-20,(float)rng.NextDouble()*160,(float)rng.NextDouble()*48-24);
            }
            // Continuous barriers sit inside visible rubble, including the train floor, so no cracks lead out of the map.
            foreach(var r in new[]{new Vector4(168,184,2,24),new Vector4(159,184,-16,2),new Vector4(154,184,-37,-16)})
            {
                var b=Box("Solid collapse core",new Vector3((r.x+r.y)/2,-1.75f,(r.z+r.w)/2),new Vector3(r.y-r.x,6.5f,r.w-r.z),debris,g);b.GetComponent<MeshRenderer>().enabled=false;
            }
            for(int i=0;i<4;i++)
            {
                var slab=Box("Roof crushing damaged train",new Vector3(155+i*6,-.6f-i*.18f,-32),new Vector3(8,1.2f,8),wall,g);
                slab.transform.rotation=Quaternion.Euler(14+i*3,13*i,-14+i*8);
            }
        }
        static void Lighting()
        {
            var g=Group("Simple subway review lighting");
            foreach(float x in new[]{95,116,137,158})foreach(float z in new[]{9f,-23f})
            {
                Box("Plain ceiling fixture",new Vector3(x,.35f,z),new Vector3(4,.12f,.65f),pale,roofs,false);
                var l=new GameObject("Shadowless review light").AddComponent<Light>();l.transform.SetParent(g,false);l.transform.position=new Vector3(x,-.25f,z);l.type=LightType.Point;l.range=20;l.intensity=65;l.color=new Color(.83f,.89f,1);l.shadows=LightShadows.None;
            }
            foreach(float z in new[]{-5f,-12f})
            {
                var l=new GameObject("Concourse fill").AddComponent<Light>();l.transform.SetParent(g,false);l.transform.position=new Vector3(123,-.2f,z);l.type=LightType.Point;l.range=18;l.intensity=35;l.shadows=LightShadows.None;
            }
        }
        static void Labels()
        {
            foreach(var item in new[]{("PLATFORM 1",new Vector3(128,8,9),35f),("BOARDING TRAIN",new Vector3(125,8,20),43f),("PLATFORM 2",new Vector3(128,8,-23),35f),("DAMAGED TRAIN",new Vector3(151,8,-33),40f),("MAINTENANCE\nNO ACCESS",new Vector3(100,8,-7),23f),("COLLAPSE",new Vector3(174,8,-5),22f),("STAIRS UP",new Vector3(82,8,11),16f)})
                Text(item.Item1,item.Item1,item.Item2,new Vector2(item.Item3,6),Quaternion.Euler(90,0,0),labelRoot,13);
        }
    }
}
