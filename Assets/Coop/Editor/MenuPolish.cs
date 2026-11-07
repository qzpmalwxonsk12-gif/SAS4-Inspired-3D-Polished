using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using SASCoop;

public static class MenuPolish
{
    static void Place(Component c, float x, float y, float w, float h)
    {
        var r = c.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0, 1);
        r.pivot = new Vector2(0, 1);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
    }

    static GameObject Box(string name, Transform parent, float x, float y, float w, float h, Color color)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        g.transform.SetParent(parent, false);
        Place(g.transform, x, y, w, h);
        var img = g.GetComponent<UnityEngine.UI.Image>();
        img.color = color;
        img.raycastTarget = false;
        return g;
    }

    static TMP_Text Label(string name, string value, Transform parent, float x, float y, float w, float h, int size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal)
    {
        var g = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        g.transform.SetParent(parent, false);
        Place(g.transform, x, y, w, h);
        var t = g.GetComponent<TextMeshProUGUI>();
        t.font = TMP_Settings.defaultFontAsset;
        t.text = value;
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = align;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    static void FramePanel(Transform panel, float w, float h, Color topAccent, Color borderCol)
    {
        Box("HUD_TopAccent", panel, 0, 0, w, 3f, topAccent);
        Box("HUD_BottomBorder", panel, 0, h - 1.5f, w, 1.5f, borderCol);
        Box("HUD_LeftBorder", panel, 0, 0, 1.5f, h, borderCol);
        Box("HUD_RightBorder", panel, w - 1.5f, 0, 1.5f, h, borderCol);
        Box("HUD_CornerBL", panel, 0, h - 12f, 12f, 2.5f, topAccent);
        Box("HUD_CornerBR", panel, w - 12f, h - 12f, 12f, 2.5f, topAccent);
    }

    [MenuItem("Tools/Co-op/Apply Menu Polish")]
    public static void Apply()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Coop/Scenes/MainMenu.unity");
        var m = Object.FindAnyObjectByType<MainMenu>();
        var root = m.transform.Find("Menu content");
        if (!root)
        {
            var frame = new GameObject("Menu content", typeof(RectTransform));
            root = frame.transform;
            var children = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in m.transform) children.Add(child);
            root.SetParent(m.transform, false);
            var r = (RectTransform)root;
            r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            r.sizeDelta = new Vector2(1366, 768);
            foreach (var child in children) child.SetParent(root, false);
        }

        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (!t) continue;
            if (t.name == "Helicopter squad illustration" ||
                t.name == "Specialist divider" ||
                t.name == "01 / SELECT OPERATION" ||
                t.name == "Header rule" ||
                t.name.StartsWith("Operator hotspot ") ||
                t.name.StartsWith("HUD_"))
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }

        m.GetComponent<UnityEngine.UI.CanvasScaler>().screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;

        // Translucent Glassmorphic Tactical Palette so the live 3D Warzone Backdrop shines through
        var backdropTint = new Color(0.008f, 0.015f, 0.022f, 0.20f);
        var panelGlass = new Color(0.018f, 0.032f, 0.044f, 0.72f);
        var subPanelGlass = new Color(0.012f, 0.022f, 0.032f, 0.78f);
        var amber = new Color(0.96f, 0.64f, 0.20f, 1f);
        var cyan = new Color(0.24f, 0.80f, 0.90f, 1f);
        var crimson = new Color(0.94f, 0.28f, 0.22f, 1f);
        var border = new Color(0.18f, 0.38f, 0.45f, 0.70f);
        var muted = new Color(0.62f, 0.76f, 0.80f, 1f);
        var brightText = new Color(0.94f, 0.97f, 0.99f, 1f);

        root.Find("Background").GetComponent<UnityEngine.UI.Image>().color = backdropTint;

        // Top & Bottom Command Bars
        var topBar = Box("HUD_TopCommandBar", root, 0, 0, 1366, 136, new Color(0.010f, 0.018f, 0.026f, 0.76f));
        topBar.transform.SetSiblingIndex(1);
        var botBar = Box("HUD_BottomCommandBar", root, 0, 662, 1366, 106, new Color(0.010f, 0.018f, 0.026f, 0.82f));
        botBar.transform.SetSiblingIndex(2);

        // Header Styling
        Place(root.Find("Accent"), 40, 30, 6, 84);
        root.Find("Accent").GetComponent<UnityEngine.UI.Image>().color = amber;
        Box("HUD_AccentSecondary", root, 50, 30, 2, 84, cyan);

        var titleText = root.Find("SAS 4 3D").GetComponent<TMP_Text>();
        Place(titleText, 66, 24, 620, 74);
        titleText.fontStyle = FontStyles.Bold;
        titleText.fontSize = 56;
        titleText.characterSpacing = 4f;
        titleText.color = brightText;

        var sub = System.Linq.Enumerable.First(root.GetComponentsInChildren<TMP_Text>(true), t => t.name == "SPECIAL ASSAULT SQUAD  /  CAMPAIGN").transform;
        Place(sub, 68, 96, 820, 24);
        var subTmp = sub.GetComponent<TMP_Text>();
        subTmp.text = "SPECIAL ASSAULT SQUAD   //   BIO-CONTAINMENT OPERATIONS COMMAND";
        subTmp.characterSpacing = 3.5f;
        subTmp.fontSize = 14;
        subTmp.color = cyan;

        // Reposition existing top-right labels cleanly inside the Threat Status Badge so nothing overlaps
        var badge = Box("HUD_ThreatBadge", root, 946, 32, 380, 76, subPanelGlass);
        FramePanel(badge.transform, 380, 76, crimson, border);

        var campTitle = root.Find("CO-OP SURVIVAL");
        if (campTitle)
        {
            Place(campTitle, 960, 42, 350, 24);
            var ct = campTitle.GetComponent<TMP_Text>();
            ct.text = "BIO-THREAT LEVEL: CRITICAL";
            ct.fontSize = 15;
            ct.fontStyle = FontStyles.Bold;
            ct.alignment = TextAlignmentOptions.Right;
            ct.color = crimson;
            campTitle.SetAsLastSibling();
        }
        var upToFour = root.Find("UP TO FOUR PLAYERS");
        if (upToFour)
        {
            Place(upToFour, 960, 68, 350, 22);
            var ut = upToFour.GetComponent<TMP_Text>();
            ut.text = "UP TO 4 PLAYERS  //  SOLO OR ONLINE CO-OP";
            ut.fontSize = 12;
            ut.alignment = TextAlignmentOptions.Right;
            ut.color = muted;
            upToFour.SetAsLastSibling();
        }

        Box("Header rule", root, 40, 134, 1286, 2, new Color(0.20f, 0.42f, 0.48f, 0.8f));
        Box("HUD_HeaderRuleAccent", root, 40, 134, 180, 2, amber);

        // Mode Tabs (Singleplayer vs Co-op)
        Place(m.single, 40, 150, 260, 46);
        Place(m.multi, 310, 150, 260, 46);
        m.single.image.color = new Color(0.11f, 0.36f, 0.42f, 0.96f);
        m.multi.image.color = new Color(0.05f, 0.085f, 0.11f, 0.88f);
        Box("HUD_SingleTopLine", m.single.transform, 0, 0, 260, 2.5f, cyan);
        Box("HUD_MultiTopLine", m.multi.transform, 0, 0, 260, 2.5f, amber);

        // Left Column: Operation Briefing Panel
        Place(m.selectionPanel.transform, 40, 210, 530, 350);
        m.selectionPanel.GetComponent<UnityEngine.UI.Image>().color = panelGlass;
        FramePanel(m.selectionPanel.transform, 530, 350, amber, border);

        var selMissionLbl = m.selectionPanel.transform.Find("SELECT MISSION");
        if (selMissionLbl)
        {
            Place(selMissionLbl, 20, 14, 480, 26);
            var smt = selMissionLbl.GetComponent<TMP_Text>();
            smt.text = "01 // SELECT OPERATION & THEATRE";
            smt.fontSize = 15;
            smt.fontStyle = FontStyles.Bold;
            smt.color = amber;
        }

        Place(m.missions[0], 20, 50, 238, 66);
        Place(m.missions[1], 272, 50, 238, 66);
        m.missions[0].image.color = new Color(0.05f, 0.085f, 0.11f, 0.88f);
        m.missions[1].image.color = new Color(0.11f, 0.36f, 0.42f, 0.96f);
        Box("HUD_Mission0Accent", m.missions[0].transform, 0, 0, 4, 66, amber);
        Box("HUD_Mission1Accent", m.missions[1].transform, 0, 0, 4, 66, cyan);

        var briefingBox = Box("HUD_BriefingInset", m.selectionPanel.transform, 18, 132, 494, 164, subPanelGlass);
        briefingBox.transform.SetAsFirstSibling();
        Box("HUD_BriefingLeftBar", briefingBox.transform, 0, 0, 3, 164, cyan);

        Place(m.missionLabel, 32, 144, 466, 140);
        m.missionLabel.fontSize = 20;
        m.missionLabel.lineSpacing = 8;
        m.missionLabel.color = brightText;

        Label("01 / SELECT OPERATION", "INTEL FEED // C4 DEMOLITION & MEDKIT BEACONS ACTIVE", m.selectionPanel.transform, 20, 312, 490, 24, 12, muted, TextAlignmentOptions.Left, FontStyles.Bold);

        // Right Column: 3D Dropship Squad Briefing & Specialist Loadout
        var right = root.Find("Squad selection");
        Place(right, 590, 150, 736, 410);
        right.GetComponent<UnityEngine.UI.Image>().color = panelGlass;
        FramePanel(right, 736, 410, cyan, border);

        var existingSpecHeader = right.Find("SELECT YOUR SPECIALIST");
        if (existingSpecHeader)
        {
            Place(existingSpecHeader, 16, 10, 420, 24);
            var sht = existingSpecHeader.GetComponent<TMP_Text>();
            sht.text = "02 // DROPSHIP SQUAD BRIEFING";
            sht.fontSize = 14;
            sht.fontStyle = FontStyles.Bold;
            sht.color = cyan;
        }
        Label("HUD_SquadHint", "LIVE 3D CABIN // CLICK OR HOVER OPERATOR", right, 440, 11, 280, 22, 11, muted, TextAlignmentOptions.Right);

        var art = Box("Helicopter squad illustration", right, 14, 36, 708, 210, Color.white);
        art.transform.SetSiblingIndex(1);
        Object.DestroyImmediate(art.GetComponent<UnityEngine.UI.Image>());
        var viewport = art.AddComponent<UnityEngine.UI.RawImage>();
        viewport.raycastTarget = false;
        viewport.texture = HelicopterMenuScene.Build();

        // Viewport framing & telemetry tag
        Box("HUD_ViewportTopRule", right, 14, 36, 708, 2, border);
        Box("HUD_ViewportBotRule", right, 14, 244, 708, 2, border);
        var camBadge = Box("HUD_CamBadge", right, 22, 44, 204, 22, new Color(0.01f, 0.02f, 0.03f, 0.80f));
        Label("HUD_CamBadgeText", "● LIVE // VALKYRIE-01 CABIN", camBadge.transform, 8, 2, 192, 18, 11, amber, TextAlignmentOptions.Left, FontStyles.Bold);

        Color[] roleAccents =
        {
            new Color(.90f, .42f, .16f),
            new Color(.18f, .72f, .78f),
            new Color(.88f, .68f, .24f),
            new Color(.84f, .26f, .22f)
        };

        for (int i = 0; i < 4; i++)
        {
            Place(m.roles[i], 14 + i * 179, 254, 171, 34);
            m.roles[i].image.color = i == 0 ? new Color(0.88f, 0.56f, 0.16f, 0.96f) : subPanelGlass;
            Box("HUD_RoleAccent_" + i, m.roles[i].transform, 0, 31, 171, 3, roleAccents[i]);
            var t = m.roles[i].GetComponentInChildren<TMP_Text>();
            Place(t, 8, 2, 155, 28);
            t.fontSize = 16;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = i == 0 ? new Color(0.03f, 0.04f, 0.05f, 1f) : brightText;

            var hot = Box("Operator hotspot " + i, right, 57 + i * 155, 58, 132, 176, new Color(0, 0, 0, 0));
            hot.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            var h = hot.AddComponent<SASCoop.OperatorHotspot>();
            h.role = i;
            h.menu = m;
        }

        var detailInset = Box("HUD_RoleDetailInset", right, 14, 296, 708, 100, subPanelGlass);
        detailInset.transform.SetAsFirstSibling();

        Place(m.roleDetail, 26, 304, 336, 86);
        m.roleDetail.fontSize = 17;
        m.roleDetail.lineSpacing = 3;
        m.roleDetail.color = brightText;

        Place(m.squad, 384, 304, 330, 86);
        m.squad.fontSize = 14.5f;
        m.squad.lineSpacing = 2;
        m.squad.color = muted;

        Box("Specialist divider", right, 368, 302, 1.5f, 86, border);

        // Online / Ready / Deploy Row
        Place(m.onlinePanel.transform, 40, 574, 924, 78);
        m.onlinePanel.GetComponent<UnityEngine.UI.Image>().color = panelGlass;
        FramePanel(m.onlinePanel.transform, 924, 78, cyan, border);

        Place(m.readyPanel.transform, 40, 574, 924, 78);
        m.readyPanel.GetComponent<UnityEngine.UI.Image>().color = panelGlass;
        FramePanel(m.readyPanel.transform, 924, 78, amber, border);

        // Set default Solo initial state so onlinePanel and readyPanel do not overlap in Edit Mode
        m.onlinePanel.SetActive(false);
        m.readyPanel.SetActive(false);
        m.leave.gameObject.SetActive(false);
        m.copy.gameObject.SetActive(false);

        Place(m.deploy, 980, 574, 346, 78);
        m.deploy.image.color = amber;
        Box("HUD_DeployTopEdge", m.deploy.transform, 0, 0, 346, 4, new Color(1f, 0.88f, 0.55f));
        m.deployLabel.color = new Color(0.02f, 0.03f, 0.04f, 1f);
        m.deployLabel.fontStyle = FontStyles.Bold;
        m.deployLabel.fontSize = 24;

        // Bottom Status & Utility Row
        Place(m.status, 40, 674, 1060, 38);
        m.status.fontSize = 15.5f;
        m.status.color = cyan;
        Place(m.leave, 1180, 672, 146, 38);
        Place(m.quit, 1180, 718, 146, 34);

        foreach (var b in m.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            var c = b.colors;
            c.highlightedColor = new Color(1f, 0.88f, 0.62f, 1f);
            c.pressedColor = new Color(0.65f, 0.85f, 0.90f, 1f);
            c.fadeDuration = 0.08f;
            b.colors = c;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
