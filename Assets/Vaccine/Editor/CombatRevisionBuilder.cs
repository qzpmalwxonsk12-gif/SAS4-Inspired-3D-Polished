using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Vaccine;
public static class CombatRevisionBuilder
{
    static Material white,green;
    static Material Material(string name,Color color)
    {
        string path="Assets/Vaccine/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;return m;
    }
    static void Part(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;g.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(g.GetComponent<Collider>());
    }
    static void Kit(Transform parent,string key,Vector3 position)
    {
        var go=new GameObject("MEDKIT / "+key);go.transform.SetParent(parent,false);go.transform.localPosition=position;
        var item=go.AddComponent<VaccineInteractable>();item.medical=true;item.medicalKey=key;item.action=VaccineUse.Supplies;item.label="MEDKIT / +80 HEALTH";
        var collider=go.AddComponent<BoxCollider>();collider.size=new Vector3(.75f,key=="train"?.48f:.65f,.55f);
        Part(go.transform,"Medical case",Vector3.zero,new Vector3(.72f,.48f,.48f),green);
        Part(go.transform,"Lid",new Vector3(0,.27f,0),new Vector3(.75f,.08f,.51f),white);
        if(key!="train")Part(go.transform,"Support",new Vector3(0,-.55f,0),new Vector3(.45f,.65f,.35f),white);
        for(int sign=-1;sign<=1;sign+=2)
        {
            Part(go.transform,"Cross vertical",new Vector3(0,0,sign*.25f),new Vector3(.075f,.3f,.025f),white);
            Part(go.transform,"Cross horizontal",new Vector3(0,0,sign*.25f),new Vector3(.3f,.075f,.025f),white);
        }
        var label=new GameObject("Medical sign").AddComponent<TextMeshPro>();label.transform.SetParent(go.transform,false);label.transform.localPosition=new Vector3(0,.65f,0);label.font=TMP_Settings.defaultFontAsset;label.text="MEDKIT\n<size=65%>80 HP / ONE USE EACH</size>";label.fontSize=2;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(2.4f,.65f);label.color=new Color(.6f,1,.75f);
    }
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play first.");
        foreach(var name in new[]{"Shambler","Regurgitator","Sprinter"})
        {
            var d=AssetDatabase.LoadAssetAtPath<ShipEnemies.EnemyDefinition>("Assets/Enemies/Definitions/"+name+".asset");d.speed=name=="Sprinter"?6.5f:5;EditorUtility.SetDirty(d);
        }
        System.IO.Directory.CreateDirectory("Assets/Vaccine/Materials");AssetDatabase.Refresh();white=Material("Medical case white",new Color(.8f,.88f,.87f));green=Material("Medical case green",new Color(.04f,.36f,.22f));green.EnableKeyword("_EMISSION");green.SetColor("_EmissionColor",new Color(.02f,.12f,.05f));
        foreach(var sceneName in new[]{"Vaccine_Blockout","Vaccine_LabDistrict"})
        {
            var scene=EditorSceneManager.OpenScene("Assets/Vaccine/Scenes/"+sceneName+".unity");var m=Object.FindAnyObjectByType<VaccineMission>();
            foreach(var i in Object.FindObjectsByType<VaccineInteractable>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(i.medical)Object.DestroyImmediate(i.gameObject);
            if(!m.labDistrict){Kit(null,"station",new Vector3(19,.9f,-26));Kit(null,"waiting-room",new Vector3(74,.9f,-34));}
            else Kit(null,"lab",new Vector3(92,2.78f,65));
            var train=m.labDistrict?m.train.train:m.player.GetComponent<VaccineBoardingTransfer>().train;
            Kit(train,"train",new Vector3(127,-11.17f,25.15f));
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
    }
}
