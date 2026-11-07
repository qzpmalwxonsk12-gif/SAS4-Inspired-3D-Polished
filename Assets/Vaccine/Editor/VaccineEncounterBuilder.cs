using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Vaccine.EditorTools
{
    public static class VaccineEncounterBuilder
    {
        static Bounds Box(float x0,float x1,float y0,float y1,float z0,float z1) =>
            new Bounds(new Vector3((x0+x1)/2,(y0+y1)/2,(z0+z1)/2),new Vector3(x1-x0,y1-y0,z1-z0));
        static VaccineMission mission;static Transform root;
        static void Area(int zone,string name,Bounds[] entry,Bounds[] placement,Vector3[] anchors,float scatter=5,bool populate=true)
        {
            var g=new GameObject("Encounter "+zone+" / "+name);g.transform.SetParent(root);
            var a=g.AddComponent<VaccineEncounterArea>();a.zone=zone;a.displayName=name;
            a.entry=entry;a.placement=placement;a.reachableAnchors=anchors;a.scatter=scatter;a.enterToPopulate=populate;
        }
        public static void ConfigureCurrent()
        {
            mission=Object.FindAnyObjectByType<VaccineMission>();
            if(!mission||EditorApplication.isPlaying)throw new System.InvalidOperationException("Open a Vaccine scene outside Play mode first.");
            var old=GameObject.Find("Vaccine encounter boundaries");if(old)Object.DestroyImmediate(old);
            root=new GameObject("Vaccine encounter boundaries").transform;
            mission.baseDensity=2.5f;mission.maximumAlive=48;
            if(mission.labDistrict)District();else Surface();
            mission.encounterAreas=root.GetComponentsInChildren<VaccineEncounterArea>();
            mission.labDoors=Object.FindObjectsByType<ReviewDoor>();
            EditorUtility.SetDirty(mission);EditorSceneManager.MarkSceneDirty(mission.gameObject.scene);
            EditorSceneManager.SaveScene(mission.gameObject.scene);
        }
        static void Surface()
        {
            mission.forbiddenSpawns=new[]{Box(88,111.65f,-13,-6,-16.65f,2.65f),Box(-67,0,-2,6,-40,0)};
            // This marker was on the sealed room's southern wall. Keep it on the
            // public Platform 2 walkway, with a generous margin from that wall.
            foreach(var p in mission.spawns.Where(p=>p.zone==6&&p.name.EndsWith(" 7")))p.transform.position=new Vector3(100,-12,-23);
            Area(0,"crash courtyard",new[]{Box(-36,50,-.6f,6,.05f,27)},new[]{Box(-32,44,-.6f,.65f,1,32)},new[]{new Vector3(0,0,8)},6);
            Area(1,"electrical room",new[]{Box(-36,-8,-.6f,6,27.1f,47)},new[]{Box(-35,-9,-.6f,.65f,28.2f,45.5f)},new[]{new Vector3(-24,0,32)},3);
            var station=new[]{Box(.5f,34.5f,-.6f,6,-29,-.2f),Box(.5f,60.8f,-.6f,6,-40,-29),Box(50,60.8f,-.6f,6,-29,6)};
            Area(2,"station hall and corridor",station,station,new[]{new Vector3(20,0,-18),new Vector3(54,0,-22)},4);
            for(int i=0;i<3;i++)
            {
                float z=-31+i*15;
                var room=new[]{Box(61.5f,79,-.6f,5,z-7,z+6)};
                Area(3+i,"waiting room "+(i+1),room,room,new[]{new Vector3(70,0,z)},3,false);
            }
            Area(6,"platforms and both trains",new[]{Box(88,176,-12.7f,-9.5f,-39,26)},
                new[]{Box(90,173,-12.6f,-11.35f,3.2f,15.7f),Box(100,153,-12.6f,-11.35f,18,25.2f),
                    Box(112,173,-12.6f,-11.35f,-16.5f,3.2f),Box(90,173,-12.6f,-11.35f,-29.7f,-17),Box(127,150,-12.6f,-11.35f,-38,-31.3f)},
                new[]{new Vector3(95,-12,8)},5);
            Area(7,"tail breach",new Bounds[0],new[]{Box(-12,5,-.6f,.65f,22,32)},new[]{new Vector3(-5,0,25)},1,false);
        }
        static void District()
        {
            mission.forbiddenSpawns=new Bounds[0];
            foreach(var p in mission.spawns.Where(p=>p.zone==9&&p.transform.position.x>83))p.zone=16;
            var final=new[]{Box(-23,42,-12.6f,-9,27.6f,34.5f)};
            Area(8,"final station",final,final,new[]{new Vector3(8,-12,30)},3);
            Area(9,"subway exit street",new[]{Box(-3,83,-.6f,6,34,60),Box(-3,39,-.6f,6,-2,108)},new[]{Box(12,79,-.6f,.65f,42,59)},new[]{new Vector3(30,0,52)},5);
            Area(16,"road beyond the truck",new[]{Box(83,159,-.6f,6,34,60.2f)},new[]{Box(84,154,-.6f,.65f,35,59)},new[]{new Vector3(110,0,48)},5);
            var lab=new[]{Box(59.5f,125,1.3f,7,60,105)};
            Area(10,"laboratory",lab,new[]{Box(60.5f,124,1.25f,2.5f,61,104)},new[]{new Vector3(68,1.88f,69),new Vector3(69,1.88f,79),new Vector3(68,1.88f,94),new Vector3(87,1.88f,95),new Vector3(115,1.88f,95),new Vector3(90,1.88f,65)},3);
            var parking=new[]{Box(40.5f,106,-.6f,6,0,34)};
            Area(11,"walled parking enclosure",parking,new[]{Box(42,104,-.6f,.65f,2,32)},new[]{new Vector3(63,0,20)},5);
            var ship1=new[]{Box(106,159,-.6f,6,0,34)};
            Area(12,"ship zone 1",ship1,new[]{Box(108,156,-.6f,.65f,2,33)},new[]{new Vector3(120,0,17)},5);
            var ship3=new[]{Box(125,159,-.6f,6,60.2f,108)};
            Area(13,"ship zone 3",ship3,new[]{Box(127,156,-.6f,.65f,62,104)},new[]{new Vector3(138,0,80)},5);
        }
    }
}
