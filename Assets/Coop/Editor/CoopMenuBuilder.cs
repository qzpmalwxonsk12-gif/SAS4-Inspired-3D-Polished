using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using SASCoop;
using System.Linq;

public static class CoopMenuBuilder
{
    static Color paper=new Color(.85f,.91f,.93f),teal=new Color(.2f,.7f,.72f),dark=new Color(.055f,.075f,.1f);
    static Transform root;
    static GameObject Rect(string name,Transform parent,float x,float y,float w,float h)
    {var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);var r=(RectTransform)go.transform;r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return go;}
    static TMP_Text Text(string text,Transform p,float x,float y,float w,float h,int size=22)
    {var go=Rect(text.Split('\n')[0],p,x,y,w,h);var t=go.AddComponent<TextMeshProUGUI>();t.font=TMP_Settings.defaultFontAsset;t.text=text;t.fontSize=size;t.color=paper;t.raycastTarget=false;return t;}
    static Button Button(string text,Transform p,float x,float y,float w,float h)
    {var go=Rect(text,p,x,y,w,h);var img=go.AddComponent<Image>();img.color=new Color(.1f,.18f,.22f);var b=go.AddComponent<Button>();b.targetGraphic=img;var colors=b.colors;colors.highlightedColor=new Color(.65f,.95f,1);colors.selectedColor=Color.white;b.colors=colors;var t=Text(text,go.transform,16,12,w-32,h-16,20);t.alignment=TextAlignmentOptions.MidlineLeft;return b;}
    static GameObject Panel(string name,Transform p,float x,float y,float w,float h,Color c){var g=Rect(name,p,x,y,w,h);g.AddComponent<Image>().color=c;return g;}
    public static string Build()
    {
        System.IO.Directory.CreateDirectory("Assets/Coop/Scenes");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Menu camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=dark;
        var canvas=new GameObject("SAS 4 3D / Opening page",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1366,768);scaler.matchWidthOrHeight=.5f;root=canvas.transform;
        var menu=canvas.AddComponent<MainMenu>();new GameObject("Event system",typeof(EventSystem),typeof(InputSystemUIInputModule));
        Panel("Background",root,0,0,1366,768,dark);Panel("Accent",root,48,50,5,80,teal);
        Text("SAS 4 3D",root,75,43,650,76,60);Text("SPECIAL ASSAULT SQUAD  /  CAMPAIGN",root,78,120,680,35,18);
        Text("CO-OP SURVIVAL",root,1000,65,320,35,22);Text("UP TO FOUR PLAYERS",root,1000,101,320,30,16);
        menu.single=Button("SINGLE PLAYER",root,60,185,285,56);menu.multi=Button("MULTIPLAYER",root,360,185,285,56);
        menu.selectionPanel=Panel("Mission selection",root,60,263,585,285,new Color(.075f,.1f,.13f));
        Text("SELECT MISSION",menu.selectionPanel.transform,22,16,450,32,18);
        menu.missions=new[]{Button("01  ONSLAUGHT",menu.selectionPanel.transform,20,57,265,58),Button("02  VACCINE",menu.selectionPanel.transform,300,57,265,58)};
        menu.missionLabel=Text("",menu.selectionPanel.transform,22,136,540,125,23);
        var right=Panel("Squad selection",root,675,185,631,363,new Color(.075f,.1f,.13f));Text("SELECT YOUR SPECIALIST",right.transform,22,16,570,30,18);
        menu.roles=new Button[4];for(int i=0;i<4;i++){menu.roles[i]=Button(RoleLoadout.Names[i],right.transform,20+i*150,57,140,66);menu.roles[i].GetComponentInChildren<TMP_Text>().fontSize=17;}
        menu.roleDetail=Text(RoleLoadout.Descriptions[0],right.transform,23,145,580,110,23);
        menu.squad=Text("",right.transform,23,260,580,95,17);
        menu.onlinePanel=Panel("Host or join",root,60,567,900,82,new Color(.075f,.1f,.13f));
        menu.host=Button("HOST GAME",menu.onlinePanel.transform,12,12,180,56);
        var input=Panel("Join code",menu.onlinePanel.transform,208,12,370,56,new Color(.13f,.17f,.2f));menu.code=input.AddComponent<TMP_InputField>();var label=Text("",input.transform,14,8,342,42,22);menu.code.textComponent=(TextMeshProUGUI)label;menu.code.textViewport=label.rectTransform;menu.code.placeholder=Text("ENTER ROOM CODE",input.transform,14,8,342,42,18);menu.code.characterLimit=12;
        menu.join=Button("JOIN GAME",menu.onlinePanel.transform,598,12,190,56);
        menu.readyPanel=Panel("Room",root,60,557,900,98,new Color(.075f,.1f,.13f));menu.room=Text("",menu.readyPanel.transform,15,14,470,40,22);menu.ready=Button("READY UP",menu.readyPanel.transform,490,18,175,55);menu.copy=Button("COPY CODE",menu.readyPanel.transform,680,18,200,55);
        menu.deploy=Button("DEPLOY SOLO",root,980,567,326,82);menu.deployLabel=menu.deploy.GetComponentInChildren<TMP_Text>();menu.deploy.image.color=new Color(.17f,.43f,.4f);
        menu.status=Text("",root,60,665,1100,48,18);menu.status.color=new Color(.85f,.7f,.42f);
        menu.leave=Button("LEAVE",root,1160,665,146,48);menu.quit=Button("QUIT",root,1160,718,146,38);
        Text("WASD MOVE  ·  MOUSE AIM  ·  E SWITCH  ·  R RELOAD  ·  F INTERACT  ·  F10 MAIN MENU",root,60,728,1080,25,15);
        EditorSceneManager.SaveScene(scene,"Assets/Coop/Scenes/MainMenu.unity");
        var paths=new[]{"Assets/Coop/Scenes/MainMenu.unity","Assets/Mission/Scenes/Onslaught_Mission.unity","Assets/Vaccine/Scenes/Vaccine_Blockout.unity","Assets/Vaccine/Scenes/Vaccine_LabDistrict.unity"};EditorBuildSettings.scenes=paths.Select(p=>new EditorBuildSettingsScene(p,true)).ToArray();
        PlayerSettings.productName="SAS 4 3D";PlayerSettings.runInBackground=true;PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;PlayerSettings.defaultIsNativeResolution=true;PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;AssetDatabase.SaveAssets();return "Opening page created and set as first build scene.";
    }
    public static string FixSpawnAndHelp()
    {
        foreach(var path in new[]{"Assets/Vaccine/Scenes/Vaccine_Blockout.unity","Assets/Vaccine/Scenes/Vaccine_LabDistrict.unity","Assets/Mission/Scenes/Onslaught_Mission.unity"})
        {
            var scene=EditorSceneManager.OpenScene(path);
            foreach(var marker in Object.FindObjectsByType<Vaccine.VaccineSpawnPoint>())if(marker.zone==1&&Vector3.Distance(marker.transform.position,new Vector3(-33,0,34))<1)marker.transform.position=new Vector3(-30.5f,.05f,34);
            foreach(var t in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))t.text=System.Text.RegularExpressions.Regex.Replace(t.text,@"SHIFT\s+SPRINT","",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        return "Moved the electrical-room spawn clear of the switchgear cabinet; removed sprint help.";
    }
}
