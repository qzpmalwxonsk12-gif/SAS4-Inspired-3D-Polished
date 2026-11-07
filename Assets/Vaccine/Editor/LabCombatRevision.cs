using System.Linq;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;
namespace Vaccine.Editor
{
    public static partial class LabDistrictBuilder
    {
        public static void CombatGeometry()
        {
            if(GameObject.Find("Single flight exit revision"))return;
            RestoreLabBatches();RevisionContext();
            foreach(var t in All().Where(t=>t.name.StartsWith("Exit flight ")||t.name.StartsWith("Exit headhouse ")).ToArray())Object.DestroyImmediate(t.gameObject);
            RemoveNamed("Exit landing one","Exit landing two","Exit top pavement","Stair east enclosure","Stair west enclosure","Stair north enclosure","Stair divider","Pavement north of stair","METRO / RESEARCH","PARKING / SIDE ACCESS");
            parent=Group("Single flight exit revision",district);
            for(int i=0;i<60;i++){float top=-12+(i+1)*.2f;Box("Straight exit step "+i,new Vector3(8,top-.15f,34+(i+.5f)*.4f),new Vector3(4,.3f,.4f),"StationFloor");}
            Floor("Exit to road landing",6,10.2f,58,62,0,"Concrete");Floor("Pavement north of single flight",6,10.2f,62,105,0,"Concrete");
            foreach(float x in new[]{5.7f,10.3f})
            {
                Wall("Single flight enclosure",x,34,x,58,13,-12.5f,"Concrete");
                Wall("Single flight street guard",x,34,x,58,1.1f,0,"Steel");
            }
            Wall("Exit street end guard",5.7f,34,10.3f,34,1.1f,0,"Steel");
            Sign("METRO / RESEARCH",new Vector3(8,2.8f,58.4f),8,180);
            SheetRun(40,24,40,29);
        }
    }
}
