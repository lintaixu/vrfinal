#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

namespace RubiksCube.Editor
{
    public class SceneSetupWizard : EditorWindow
    {
        [MenuItem("AR Rubik's Solver/自動建立場景結構")]
        public static void ShowWindow()
        {
            GetWindow<SceneSetupWizard>("場景建置精靈");
        }

        private void OnGUI()
        {
            GUILayout.Label("AR 魔術方塊求解器 - 場景建置", EditorStyles.boldLabel);
            GUILayout.Space(10);

            GUILayout.Label("此工具會自動建立完整的場景 Hierarchy。", EditorStyles.wordWrappedLabel);
            GUILayout.Label("請確認已安裝：AR Foundation, ARCore XR Plugin, TextMeshPro", EditorStyles.wordWrappedLabel);
            GUILayout.Space(10);

            if (GUILayout.Button("建立場景結構", GUILayout.Height(40)))
            {
                BuildScene();
            }

            GUILayout.Space(10);

            if (GUILayout.Button("只建立 UI Canvas", GUILayout.Height(30)))
            {
                BuildUICanvas();
            }

            if (GUILayout.Button("只建立箭頭 Prefab 替代物", GUILayout.Height(30)))
            {
                BuildArrowPlaceholders();
            }
        }

        private static void BuildScene()
        {
            // --- AR Session ---
            var arSessionGO = new GameObject("AR Session");
            // Note: ARSession component requires AR Foundation package
            // arSessionGO.AddComponent<UnityEngine.XR.ARFoundation.ARSession>();

            // --- AR Session Origin ---
            var arOriginGO = new GameObject("AR Session Origin");
            // var arOrigin = arOriginGO.AddComponent<UnityEngine.XR.ARFoundation.ARSessionOrigin>();

            // AR Camera
            var arCameraGO = new GameObject("AR Camera");
            arCameraGO.transform.SetParent(arOriginGO.transform);
            var cam = arCameraGO.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            // --- Managers Object ---
            var managersGO = new GameObject("_Managers");

            // Add manager scripts
            var arMgr = managersGO.AddComponent<RubiksCube.AR.ARSessionManager>();
            var solver = managersGO.AddComponent<RubiksCube.Solver.KociembaSolver>();
            var colorDet = managersGO.AddComponent<RubiksCube.ColorDetection.ColorDetector>();

            // --- Build UI ---
            var canvas = BuildUICanvas();

            // --- AR Step Guide ---
            var arGuideGO = new GameObject("ARStepGuide");
            var arGuide = arGuideGO.AddComponent<RubiksCube.AR.ARStepGuide>();
            BuildArrowPlaceholders(arGuideGO.transform);

            // --- Solver Test (Editor Only) ---
            var testGO = new GameObject("_SolverTest");
            testGO.AddComponent<RubiksCube.EditorTest.SolverTest>();

            Debug.Log("[Setup] 場景結構建立完成！請手動添加 AR Foundation 組件。");
            EditorUtility.DisplayDialog("完成",
                "場景結構已建立！\n\n請手動完成以下步驟：\n" +
                "1. 在 AR Session 上添加 ARSession 組件\n" +
                "2. 在 AR Session Origin 上添加 ARSessionOrigin 組件\n" +
                "3. 在 AR Camera 上添加 ARCameraManager 和 ARCameraBackground\n" +
                "4. 在 _Managers 的 ARSessionManager 中拖入參考\n" +
                "5. 在 UIManager 中拖入所有面板和元件參考",
                "了解");
        }

        private static GameObject BuildUICanvas()
        {
            // Canvas
            var canvasGO = new GameObject("Canvas");
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
            canvasGO.AddComponent<GraphicRaycaster>();

            // EventSystem
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            // Panels
            CreatePanel(canvasGO.transform, "StartPanel", "AR 魔術方塊求解器", "開始");
            CreatePanel(canvasGO.transform, "PlaneDetectionPanel", "請對準桌面...", null);
            CreateScanningPanel(canvasGO.transform);
            CreatePanel(canvasGO.transform, "ConfirmPanel", "掃描結果確認", "確認");
            CreatePanel(canvasGO.transform, "SolvingPanel", "求解中...", null);
            CreateStepPanel(canvasGO.transform);
            CreatePanel(canvasGO.transform, "CompletionPanel", "恭喜完成！", "再玩一次");

            // Add UIManager
            var uiMgr = canvasGO.AddComponent<RubiksCube.UI.UIManager>();

            Debug.Log("[Setup] UI Canvas 已建立。");
            return canvasGO;
        }

        private static GameObject CreatePanel(Transform parent, string name, string titleText, string buttonText)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(0, 0, 0, 0.7f);

            // Title
            var titleGO = new GameObject("Title");
            titleGO.transform.SetParent(panel.transform, false);
            var titleRect = titleGO.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.1f, 0.5f);
            titleRect.anchorMax = new Vector2(0.9f, 0.7f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleTMP = titleGO.AddComponent<TextMeshProUGUI>();
            titleTMP.text = titleText;
            titleTMP.fontSize = 48;
            titleTMP.alignment = TextAlignmentOptions.Center;
            titleTMP.color = Color.white;

            // Button
            if (buttonText != null)
            {
                var btnGO = new GameObject("Button");
                btnGO.transform.SetParent(panel.transform, false);
                var btnRect = btnGO.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.25f, 0.25f);
                btnRect.anchorMax = new Vector2(0.75f, 0.35f);
                btnRect.offsetMin = Vector2.zero;
                btnRect.offsetMax = Vector2.zero;

                var btnImg = btnGO.AddComponent<Image>();
                btnImg.color = new Color(0.2f, 0.6f, 1f);
                btnGO.AddComponent<Button>();

                var btnTextGO = new GameObject("Text");
                btnTextGO.transform.SetParent(btnGO.transform, false);
                var btnTextRect = btnTextGO.AddComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.offsetMin = Vector2.zero;
                btnTextRect.offsetMax = Vector2.zero;
                var btnTMP = btnTextGO.AddComponent<TextMeshProUGUI>();
                btnTMP.text = buttonText;
                btnTMP.fontSize = 36;
                btnTMP.alignment = TextAlignmentOptions.Center;
                btnTMP.color = Color.white;
            }

            panel.SetActive(false);
            return panel;
        }

        private static void CreateScanningPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "ScanningPanel", "", null);
            panel.GetComponent<Image>().color = new Color(0, 0, 0, 0.3f);

            // Camera preview
            var previewGO = new GameObject("CameraPreview");
            previewGO.transform.SetParent(panel.transform, false);
            var previewRect = previewGO.AddComponent<RectTransform>();
            previewRect.anchorMin = Vector2.zero;
            previewRect.anchorMax = Vector2.one;
            previewRect.offsetMin = Vector2.zero;
            previewRect.offsetMax = Vector2.zero;
            previewGO.AddComponent<RawImage>();

            // Grid overlay
            var gridGO = new GameObject("GridOverlay");
            gridGO.transform.SetParent(panel.transform, false);
            var gridRect = gridGO.AddComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0.2f, 0.3f);
            gridRect.anchorMax = new Vector2(0.8f, 0.7f);
            gridRect.offsetMin = Vector2.zero;
            gridRect.offsetMax = Vector2.zero;
            var gridImg = gridGO.AddComponent<Image>();
            gridImg.color = new Color(1, 1, 1, 0.3f);

            // Draw 3x3 grid lines (using child images)
            for (int i = 1; i < 3; i++)
            {
                // Vertical lines
                var vLine = new GameObject($"VLine_{i}");
                vLine.transform.SetParent(gridGO.transform, false);
                var vRect = vLine.AddComponent<RectTransform>();
                float xPos = i / 3f;
                vRect.anchorMin = new Vector2(xPos, 0);
                vRect.anchorMax = new Vector2(xPos, 1);
                vRect.sizeDelta = new Vector2(3, 0);
                var vImg = vLine.AddComponent<Image>();
                vImg.color = Color.white;

                // Horizontal lines
                var hLine = new GameObject($"HLine_{i}");
                hLine.transform.SetParent(gridGO.transform, false);
                var hRect = hLine.AddComponent<RectTransform>();
                float yPos = i / 3f;
                hRect.anchorMin = new Vector2(0, yPos);
                hRect.anchorMax = new Vector2(1, yPos);
                hRect.sizeDelta = new Vector2(0, 3);
                var hImg = hLine.AddComponent<Image>();
                hImg.color = Color.white;
            }

            // Progress text
            var progressGO = new GameObject("ProgressText");
            progressGO.transform.SetParent(panel.transform, false);
            var progRect = progressGO.AddComponent<RectTransform>();
            progRect.anchorMin = new Vector2(0.1f, 0.85f);
            progRect.anchorMax = new Vector2(0.9f, 0.95f);
            progRect.offsetMin = Vector2.zero;
            progRect.offsetMax = Vector2.zero;
            var progTMP = progressGO.AddComponent<TextMeshProUGUI>();
            progTMP.text = "已掃描 0 / 6 面";
            progTMP.fontSize = 32;
            progTMP.alignment = TextAlignmentOptions.Center;
            progTMP.color = Color.white;

            // Hint text
            var hintGO = new GameObject("HintText");
            hintGO.transform.SetParent(panel.transform, false);
            var hintRect = hintGO.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.1f, 0.75f);
            hintRect.anchorMax = new Vector2(0.9f, 0.85f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;
            var hintTMP = hintGO.AddComponent<TextMeshProUGUI>();
            hintTMP.text = "請對準上面 (U)";
            hintTMP.fontSize = 28;
            hintTMP.alignment = TextAlignmentOptions.Center;
            hintTMP.color = Color.yellow;

            // Capture button
            var capBtnGO = new GameObject("CaptureButton");
            capBtnGO.transform.SetParent(panel.transform, false);
            var capRect = capBtnGO.AddComponent<RectTransform>();
            capRect.anchorMin = new Vector2(0.3f, 0.05f);
            capRect.anchorMax = new Vector2(0.7f, 0.12f);
            capRect.offsetMin = Vector2.zero;
            capRect.offsetMax = Vector2.zero;
            var capImg = capBtnGO.AddComponent<Image>();
            capImg.color = new Color(0.2f, 0.8f, 0.2f);
            capBtnGO.AddComponent<Button>();

            var capTextGO = new GameObject("Text");
            capTextGO.transform.SetParent(capBtnGO.transform, false);
            var capTextRect = capTextGO.AddComponent<RectTransform>();
            capTextRect.anchorMin = Vector2.zero;
            capTextRect.anchorMax = Vector2.one;
            capTextRect.offsetMin = Vector2.zero;
            capTextRect.offsetMax = Vector2.zero;
            var capTMP = capTextGO.AddComponent<TextMeshProUGUI>();
            capTMP.text = "拍攝";
            capTMP.fontSize = 36;
            capTMP.alignment = TextAlignmentOptions.Center;
            capTMP.color = Color.white;

            // Retake button
            var retakeBtnGO = new GameObject("RetakeButton");
            retakeBtnGO.transform.SetParent(panel.transform, false);
            var retakeRect = retakeBtnGO.AddComponent<RectTransform>();
            retakeRect.anchorMin = new Vector2(0.3f, 0.13f);
            retakeRect.anchorMax = new Vector2(0.7f, 0.20f);
            retakeRect.offsetMin = Vector2.zero;
            retakeRect.offsetMax = Vector2.zero;
            var retakeImg = retakeBtnGO.AddComponent<Image>();
            retakeImg.color = new Color(0.8f, 0.4f, 0.2f);
            retakeBtnGO.AddComponent<Button>();

            var retakeTextGO = new GameObject("Text");
            retakeTextGO.transform.SetParent(retakeBtnGO.transform, false);
            var retakeTextRect = retakeTextGO.AddComponent<RectTransform>();
            retakeTextRect.anchorMin = Vector2.zero;
            retakeTextRect.anchorMax = Vector2.one;
            retakeTextRect.offsetMin = Vector2.zero;
            retakeTextRect.offsetMax = Vector2.zero;
            var retakeTMP = retakeTextGO.AddComponent<TextMeshProUGUI>();
            retakeTMP.text = "重拍上一面";
            retakeTMP.fontSize = 28;
            retakeTMP.alignment = TextAlignmentOptions.Center;
            retakeTMP.color = Color.white;

            // Color preview grid (9 small squares)
            var previewGrid = new GameObject("ColorPreviewGrid");
            previewGrid.transform.SetParent(panel.transform, false);
            var pgRect = previewGrid.AddComponent<RectTransform>();
            pgRect.anchorMin = new Vector2(0.35f, 0.21f);
            pgRect.anchorMax = new Vector2(0.65f, 0.29f);
            pgRect.offsetMin = Vector2.zero;
            pgRect.offsetMax = Vector2.zero;

            // Add ScanningUI component
            panel.AddComponent<RubiksCube.UI.ScanningUI>();
        }

        private static void CreateStepPanel(Transform parent)
        {
            var panel = CreatePanel(parent, "StepPanel", "", null);
            panel.GetComponent<Image>().color = new Color(0, 0, 0, 0.5f);

            // Step count
            var countGO = new GameObject("StepCountText");
            countGO.transform.SetParent(panel.transform, false);
            var countRect = countGO.AddComponent<RectTransform>();
            countRect.anchorMin = new Vector2(0.1f, 0.85f);
            countRect.anchorMax = new Vector2(0.9f, 0.95f);
            countRect.offsetMin = Vector2.zero;
            countRect.offsetMax = Vector2.zero;
            var countTMP = countGO.AddComponent<TextMeshProUGUI>();
            countTMP.text = "步驟 1 / 20";
            countTMP.fontSize = 36;
            countTMP.alignment = TextAlignmentOptions.Center;
            countTMP.color = Color.white;

            // Move notation (big)
            var notationGO = new GameObject("MoveNotationText");
            notationGO.transform.SetParent(panel.transform, false);
            var notRect = notationGO.AddComponent<RectTransform>();
            notRect.anchorMin = new Vector2(0.2f, 0.65f);
            notRect.anchorMax = new Vector2(0.8f, 0.85f);
            notRect.offsetMin = Vector2.zero;
            notRect.offsetMax = Vector2.zero;
            var notTMP = notationGO.AddComponent<TextMeshProUGUI>();
            notTMP.text = "R";
            notTMP.fontSize = 96;
            notTMP.alignment = TextAlignmentOptions.Center;
            notTMP.color = Color.green;

            // Step description
            var descGO = new GameObject("StepDescText");
            descGO.transform.SetParent(panel.transform, false);
            var descRect = descGO.AddComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.1f, 0.55f);
            descRect.anchorMax = new Vector2(0.9f, 0.65f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;
            var descTMP = descGO.AddComponent<TextMeshProUGUI>();
            descTMP.text = "右面 (Right) 順時針 90°";
            descTMP.fontSize = 28;
            descTMP.alignment = TextAlignmentOptions.Center;
            descTMP.color = Color.white;

            // Prev button
            var prevBtnGO = CreateSimpleButton(panel.transform, "PrevButton",
                new Vector2(0.05f, 0.05f), new Vector2(0.45f, 0.15f),
                "上一步", new Color(0.5f, 0.5f, 0.5f));

            // Next button
            var nextBtnGO = CreateSimpleButton(panel.transform, "NextButton",
                new Vector2(0.55f, 0.05f), new Vector2(0.95f, 0.15f),
                "下一步", new Color(0.2f, 0.6f, 1f));

            // Add StepManager
            panel.AddComponent<RubiksCube.UI.StepManager>();
        }

        private static GameObject CreateSimpleButton(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, string text, Color color)
        {
            var btnGO = new GameObject(name);
            btnGO.transform.SetParent(parent, false);
            var rect = btnGO.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var img = btnGO.AddComponent<Image>();
            img.color = color;
            btnGO.AddComponent<Button>();

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(btnGO.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 32;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;

            return btnGO;
        }

        private static void BuildArrowPlaceholders(Transform parent = null)
        {
            if (parent == null)
            {
                var go = new GameObject("ARStepGuide");
                go.AddComponent<RubiksCube.AR.ARStepGuide>();
                parent = go.transform;
            }

            string[] faces = { "U", "D", "F", "B", "L", "R" };
            Color[] colors = {
                Color.white, Color.yellow, Color.green,
                Color.blue, new Color(1f, 0.5f, 0f), Color.red
            };
            Vector3[] positions = {
                new Vector3(0, 0.05f, 0),    // U - top
                new Vector3(0, -0.05f, 0),   // D - bottom
                new Vector3(0, 0, 0.05f),    // F - front
                new Vector3(0, 0, -0.05f),   // B - back
                new Vector3(-0.05f, 0, 0),   // L - left
                new Vector3(0.05f, 0, 0)     // R - right
            };

            for (int i = 0; i < 6; i++)
            {
                var arrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
                arrow.name = $"Arrow_{faces[i]}";
                arrow.transform.SetParent(parent, false);
                arrow.transform.localPosition = positions[i];
                arrow.transform.localScale = new Vector3(0.03f, 0.03f, 0.03f);

                var renderer = arrow.GetComponent<Renderer>();
                var mat = new Material(Shader.Find("Standard"));
                mat.color = colors[i];
                renderer.material = mat;

                arrow.AddComponent<RubiksCube.AR.ArrowRotator>();
                arrow.SetActive(false);
            }

            Debug.Log("[Setup] 箭頭替代物已建立（使用 Cube 代替正式箭頭）。");
        }
    }
}
#endif
