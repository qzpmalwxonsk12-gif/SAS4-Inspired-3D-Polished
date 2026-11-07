using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

namespace Onslaught.Mission.Editor
{
    public static class MissionArt
    {
        public static Material dark,steel,grip,accent,glove,flash;
        const string Root="Assets/Mission";
        public static void Prepare()
        {
            foreach(string d in new[]{"Materials","Audio","Weapons","Prefabs","Data","Navigation","Scenes"})Directory.CreateDirectory(Root+"/"+d);
            AssetDatabase.Refresh();
            dark=Mat("Gun receiver",new Color(.075f,.1f,.11f),.6f);steel=Mat("Gun steel",new Color(.31f,.36f,.37f),.72f);
            grip=Mat("Grip polymer",new Color(.045f,.05f,.045f));accent=Mat("Identification amber",new Color(.7f,.35f,.07f));
            glove=Mat("Combat gloves",new Color(.12f,.14f,.11f));flash=Mat("Muzzle and tracer",new Color(1,.62f,.16f),0,true);
            WriteSound("PistolShot",.23f,0);WriteSound("RifleShot",.17f,1);WriteSound("Reload",1.3f,2);WriteSound("GateClang",.3f,3);WriteSound("GateCollapse",.7f,4);WriteSound("Engine",3,5);WriteSound("Alert",.6f,6);
            AssetDatabase.Refresh();
            foreach(string guid in AssetDatabase.FindAssets("t:AudioClip",new[]{Root+"/Audio"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var importer=(AudioImporter)AssetImporter.GetAtPath(path);importer.forceToMono=true;
                var settings=importer.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.Vorbis;settings.quality=.65f;importer.defaultSampleSettings=settings;importer.SaveAndReimport();
            }
        }
        public static AudioClip Clip(string name)=>AssetDatabase.LoadAssetAtPath<AudioClip>(Root+"/Audio/"+name+".wav");
        public static Material Mat(string name,Color color,float metal=0,bool unlit=false)
        {
            var mat=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Simple Lit"));mat.name=name;mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",.22f);
            AssetDatabase.CreateAsset(mat,Root+"/Materials/"+name+".mat");return mat;
        }
        public static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 size,Material mat,bool collision=false)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=mat;go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static GameObject Tube(string name,Transform parent,Vector3 pos,float radius,float length,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localRotation=Quaternion.Euler(90,0,0);go.transform.localScale=new Vector3(radius*2,length*.5f,radius*2);
            go.GetComponent<Renderer>().sharedMaterial=mat;go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        public static GameObject Weapon(Transform parent,bool rifle,out Transform slide,out Transform magazine,out Transform muzzle)
        {
            var root=new GameObject(rifle?"AR-24 service rifle":"P-34 service pistol").transform;root.SetParent(parent,false);
            Box("Receiver",root,new Vector3(0,0,.16f),new Vector3(.13f,.13f,rifle?.38f:.25f),dark);
            slide=new GameObject("Moving bolt and slide").transform;slide.SetParent(root,false);
            Box("Slide",slide,new Vector3(0,.07f,rifle?.12f:.17f),new Vector3(rifle?.11f:.14f,.065f,rifle?.18f:.3f),steel);
            var hand=Box("Pistol grip",root,new Vector3(0,-.14f,.06f),new Vector3(.095f,.22f,.105f),grip);hand.transform.localRotation=Quaternion.Euler(-16,0,0);
            Box("Trigger guard lower",root,new Vector3(0,-.11f,.17f),new Vector3(.022f,.022f,.11f),steel);
            Box("Trigger guard forward",root,new Vector3(0,-.065f,.22f),new Vector3(.022f,.1f,.022f),steel);
            Box("Trigger",root,new Vector3(0,-.055f,.15f),new Vector3(.018f,.055f,.018f),steel);
            magazine=new GameObject("Magazine").transform;magazine.SetParent(root,false);
            if(rifle)
            {
                Box("Box magazine",magazine,new Vector3(0,-.16f,.21f),new Vector3(.09f,.23f,.15f),steel).transform.localRotation=Quaternion.Euler(-8,0,0);
                Box("Stock neck",root,new Vector3(0,-.01f,-.12f),new Vector3(.07f,.07f,.2f),steel);
                Box("Stock",root,new Vector3(0,-.035f,-.23f),new Vector3(.1f,.18f,.16f),grip);
                Box("Handguard",root,new Vector3(0,.01f,.41f),new Vector3(.14f,.12f,.28f),grip);
                for(int i=0;i<5;i++){Box("Cooling slot",root,new Vector3(.071f,.025f,.31f+i*.045f),new Vector3(.006f,.034f,.026f),dark);Box("Rail tooth",root,new Vector3(0,.09f,.29f+i*.05f),new Vector3(.085f,.025f,.018f),steel);}
                Tube("Barrel",root,new Vector3(0,.018f,.62f),.023f,.28f,steel);Tube("Muzzle brake",root,new Vector3(0,.018f,.775f),.032f,.07f,dark);
                Box("Forehand glove",root,new Vector3(-.02f,-.085f,.4f),new Vector3(.16f,.12f,.17f),glove);
                Box("Supporting sleeve",root,new Vector3(-.11f,-.21f,.25f),new Vector3(.14f,.15f,.32f),grip).transform.localRotation=Quaternion.Euler(-35,-14,0);
                Box("Rear sight",root,new Vector3(0,.12f,-.01f),new Vector3(.07f,.055f,.025f),dark);
                Box("Front sight",root,new Vector3(0,.11f,.57f),new Vector3(.035f,.07f,.024f),dark);
            }
            else
            {
                Box("Magazine base",magazine,new Vector3(0,-.25f,.09f),new Vector3(.105f,.027f,.12f),steel);
                Tube("Barrel",root,new Vector3(0,.05f,.345f),.026f,.12f,dark);
                Box("Rear sight",slide,new Vector3(0,.112f,.035f),new Vector3(.065f,.03f,.025f),dark);
                Box("Front sight",slide,new Vector3(0,.11f,.29f),new Vector3(.018f,.026f,.02f),accent);
                for(int i=0;i<5;i++)Box("Slide serration",slide,new Vector3(.071f,.068f,.03f+i*.02f),new Vector3(.005f,.045f,.009f),grip);
            }
            Box("Amber identification",root,new Vector3(.067f,.025f,.17f),new Vector3(.006f,.028f,.09f),accent);
            Box("Firing hand glove",root,new Vector3(.033f,-.13f,.03f),new Vector3(.14f,.13f,.15f),glove);
            Box("Right sleeve",root,new Vector3(.055f,-.23f,-.09f),new Vector3(.14f,.15f,.26f),grip).transform.localRotation=Quaternion.Euler(-25,0,0);
            muzzle=new GameObject("Muzzle socket").transform;muzzle.SetParent(root,false);muzzle.localPosition=new Vector3(0,rifle?.018f:.05f,rifle?.82f:.41f);
            foreach(var t in root.GetComponentsInChildren<Transform>())t.gameObject.layer=2;
            PrefabUtility.SaveAsPrefabAsset(root.gameObject,Root+"/Weapons/"+(rifle?"Rifle":"Pistol")+".prefab");return root.gameObject;
        }
        static void WriteSound(string name,float seconds,int kind)
        {
            const int rate=22050;int count=(int)(rate*seconds);var random=new System.Random(kind+102);var data=new short[count];
            for(int i=0;i<count;i++)
            {
                float t=(float)i/rate,noise=(float)(random.NextDouble()*2-1),s;
                if(kind<2)s=(noise*.5f+Mathf.Sin(t*(kind==0?580:740))*.5f)*Mathf.Exp(-t*(kind==0?24:32));
                else if(kind==2){float a=Mathf.Abs(t-.08f),b=Mathf.Abs(t-1.08f);s=noise*(Mathf.Exp(-a*100)+Mathf.Exp(-b*90))*.55f;}
                else if(kind==3)s=(Mathf.Sin(t*2100)+Mathf.Sin(t*3700)*.4f+noise*.3f)*Mathf.Exp(-t*16)*.35f;
                else if(kind==4)s=noise*Mathf.Exp(-t*5)*.7f+Mathf.Sin(t*180)*Mathf.Exp(-t*7)*.2f;
                else if(kind==5)s=(Mathf.Sin(t*Mathf.PI*2*65)*.45f+Mathf.Sin(t*Mathf.PI*2*130)*.2f+noise*.15f)*.5f;
                else s=Mathf.Sin(t*Mathf.PI*2*(t<.25f?660:880))*Mathf.Clamp01((seconds-t)*15)*.25f;
                data[i]=(short)(Mathf.Clamp(s,-.9f,.9f)*short.MaxValue);
            }
            using(var stream=File.Create(Root+"/Audio/"+name+".wav"))using(var writer=new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+count*2);writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));writer.Write(count*2);foreach(short sample in data)writer.Write(sample);
            }
        }
    }
}
