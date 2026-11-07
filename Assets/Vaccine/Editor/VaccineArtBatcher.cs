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
    public static class VaccineArtBatcher
    {
        const string Folder="Assets/Vaccine/Geometry/Art/Batches";
        [MenuItem("Vaccine/Restore Editable Art Renderers")]
        public static void Restore()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play first.");
            var old=Object.FindAnyObjectByType<VaccineStaticArtCache>();if(!old)return;
            foreach(var r in old.sources)if(r)r.enabled=true;
            foreach(var g in old.generatedGroups)if(g)Object.DestroyImmediate(g);
            Object.DestroyImmediate(old.gameObject);
            foreach(var guid in AssetDatabase.FindAssets("t:Mesh",new[]{Folder}))AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(guid));
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        }
        [MenuItem("Vaccine/Rebuild Static Art Batches")]
        public static string Bake()
        {
            if(Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=VaccineBlockoutBuilder.ScenePath)throw new InvalidOperationException("Open Vaccine outside Play.");
            Restore();Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var roofs=GameObject.Find("08 Roofs - hidden for overhead review").transform;
            var candidates=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include).Where(r=>r.enabled&&r.gameObject.isStatic&&r.GetComponent<MeshFilter>()&&r.GetComponent<MeshFilter>().sharedMesh&&!r.GetComponent<TMPro.TMP_Text>()&&!r.GetComponentInParent<ReviewDoor>()&&!r.sharedMaterials.Any(m=>m&&m.renderQueue>=3000)).ToArray();
            var manifest=new GameObject("12 Static art cache - restore via Vaccine menu").AddComponent<VaccineStaticArtCache>();
            var main=new GameObject("Batched fixed scenery");var roofGroup=new GameObject("Batched removable review roofs");roofGroup.transform.SetParent(roofs,false);
            var groups=new Dictionary<string,List<CombineInstance>>();var materials=new Dictionary<string,Material>();var isRoof=new Dictionary<string,bool>();
            foreach(var r in candidates)
            {
                var mesh=r.GetComponent<MeshFilter>().sharedMesh;
                for(int j=0;j<mesh.subMeshCount;j++)
                {
                    if(j>=r.sharedMaterials.Length||!r.sharedMaterials[j])continue;
                    var m=r.sharedMaterials[j];var p=r.bounds.center;bool roof=r.transform.IsChildOf(roofs);
                    string k=(roof?"Roof":"World")+"/"+Mathf.FloorToInt(p.x/24)+"/"+Mathf.FloorToInt(p.z/24)+"/"+AssetDatabase.GetAssetPath(m);
                    if(!groups.ContainsKey(k)){groups[k]=new List<CombineInstance>();materials[k]=m;isRoof[k]=roof;}
                    groups[k].Add(new CombineInstance{mesh=mesh,subMeshIndex=j,transform=r.transform.localToWorldMatrix});
                }
            }
            int id=0;
            foreach(var kv in groups)
            {
                var mesh=new Mesh{name="Vaccine static chunk "+(++id),indexFormat=IndexFormat.UInt32};
                mesh.CombineMeshes(kv.Value.ToArray(),true,true);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Folder+"/Chunk-"+id.ToString("D3")+".asset");
                var g=new GameObject(materials[kv.Key].name+" chunk "+id,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(isRoof[kv.Key]?roofGroup.transform:main.transform,false);g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=materials[kv.Key];g.isStatic=true;
            }
            foreach(var r in candidates)r.enabled=false;
            manifest.sources=candidates;manifest.generatedGroups=new[]{main,roofGroup};
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            return candidates.Length+" static source renderers grouped into "+id+" spatial/material batches. Interactive doors and map roof toggling retained.";
        }
    }
}
