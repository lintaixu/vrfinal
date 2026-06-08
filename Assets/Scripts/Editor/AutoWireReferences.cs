#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using TMPro;

namespace RubiksCube.Editor
{
    public class AutoWireReferences : EditorWindow
    {
        [MenuItem("AR Rubik's Solver/自動連接所有參考")]
        public static void Wire()
        {
            int wired = 0;

            // === 1. ARSessionManager on _Managers ===
            var arMgr = Object.FindFirstObjectByType<AR.ARSessionManager>();
            if (arMgr != null)
            {
                var so = new SerializedObject(arMgr);

                var arSession = Object.FindFirstObjectByType<ARSession>();
                if (arSession != null) { so.FindProperty("arSession").objectReferenceValue = arSession; wired++; }

                var planeMgr = Object.FindFirstObjectByType<ARPlaneManager>();
                if (planeMgr != null) { so.FindProperty("planeManager").objectReferenceValue = planeMgr; wired++; }

                var rayMgr = Object.FindFirstObjectByType<ARRaycastManager>();
                if (rayMgr != null) { so.FindProperty("raycastManager").objectReferenceValue = rayMgr; wired++; }

                var anchorMgr = Object.FindFirstObjectByType<ARAnchorManager>();
                if (anchorMgr != null) { so.FindProperty("anchorManager").objectReferenceValue = anchorMgr; wired++; }

                so.ApplyModifiedProperties();
                Debug.Log($"[AutoWire] ARSessionManager: done");
            }
            else Debug.LogWarning("[AutoWire] ARSessionManager not found!");

            // === 2. UIManager on Canvas ===
            var uiMgr = Object.FindFirstObjectByType<UI.UIManager>(FindObjectsInactive.Include);
            if (uiMgr != null)
            {
                var so = new SerializedObject(uiMgr);

                // Panels - find by name
                SetGO(so, "startPanel", "StartPanel", ref wired);
                SetGO(so, "planeDetectionPanel", "PlaneDetectionPanel", ref wired);
                SetGO(so, "scanningPanel", "ScanningPanel", ref wired);
                SetGO(so, "confirmPanel", "ConfirmPanel", ref wired);
                SetGO(so, "solvingPanel", "SolvingPanel", ref wired);
                SetGO(so, "stepPanel", "StepPanel", ref wired);
                SetGO(so, "completionPanel", "CompletionPanel", ref wired);

                // Start button
                var startPanel = GameObject.Find("StartPanel");
                if (startPanel != null)
                {
                    var btn = startPanel.GetComponentInChildren<Button>(true);
                    if (btn != null) { so.FindProperty("startButton").objectReferenceValue = btn; wired++; }
                }

                // Plane hint text
                var planePanel = GameObject.Find("PlaneDetectionPanel");
                if (planePanel != null)
                {
                    var txt = planePanel.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (txt != null) { so.FindProperty("planeHintText").objectReferenceValue = txt; wired++; }
                }

                // Confirm panel buttons
                var confirmPanel = GameObject.Find("ConfirmPanel");
                if (confirmPanel != null)
                {
                    var btns = confirmPanel.GetComponentsInChildren<Button>(true);
                    if (btns.Length > 0) { so.FindProperty("confirmOkButton").objectReferenceValue = btns[0]; wired++; }
                    // We'll add a retake button to confirm panel if needed later
                }

                // Completion panel
                var completionPanel = GameObject.Find("CompletionPanel");
                if (completionPanel != null)
                {
                    var txt = completionPanel.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (txt != null) { so.FindProperty("completionText").objectReferenceValue = txt; wired++; }
                    var btn = completionPanel.GetComponentInChildren<Button>(true);
                    if (btn != null) { so.FindProperty("restartButton").objectReferenceValue = btn; wired++; }
                }

                // Component references
                var scanUI = Object.FindFirstObjectByType<UI.ScanningUI>(FindObjectsInactive.Include);
                if (scanUI != null) { so.FindProperty("scanningUI").objectReferenceValue = scanUI; wired++; }

                var stepMgr = Object.FindFirstObjectByType<UI.StepManager>(FindObjectsInactive.Include);
                if (stepMgr != null) { so.FindProperty("stepManager").objectReferenceValue = stepMgr; wired++; }

                var solver = Object.FindFirstObjectByType<Solver.KociembaSolver>();
                if (solver != null) { so.FindProperty("solver").objectReferenceValue = solver; wired++; }

                if (arMgr != null) { so.FindProperty("arSessionManager").objectReferenceValue = arMgr; wired++; }

                var arGuide = Object.FindFirstObjectByType<AR.ARStepGuide>();
                if (arGuide != null) { so.FindProperty("arStepGuide").objectReferenceValue = arGuide; wired++; }

                var colorDet = Object.FindFirstObjectByType<ColorDetection.ColorDetector>();
                if (colorDet != null) { so.FindProperty("colorDetector").objectReferenceValue = colorDet; wired++; }

                so.ApplyModifiedProperties();
                Debug.Log($"[AutoWire] UIManager: done");
            }
            else Debug.LogWarning("[AutoWire] UIManager not found!");

            // === 3. ScanningUI on ScanningPanel ===
            var scanningUI = Object.FindFirstObjectByType<UI.ScanningUI>(FindObjectsInactive.Include);
            if (scanningUI != null)
            {
                var so = new SerializedObject(scanningUI);
                var scanPanel = scanningUI.gameObject;

                // Find children by name
                var progressText = FindChildTMP(scanPanel.transform, "ProgressText");
                if (progressText != null) { so.FindProperty("progressText").objectReferenceValue = progressText; wired++; }

                var hintText = FindChildTMP(scanPanel.transform, "HintText");
                if (hintText != null) { so.FindProperty("hintText").objectReferenceValue = hintText; wired++; }

                var captureBtn = FindChildButton(scanPanel.transform, "CaptureButton");
                if (captureBtn != null) { so.FindProperty("captureButton").objectReferenceValue = captureBtn; wired++; }

                var retakeBtn = FindChildButton(scanPanel.transform, "RetakeButton");
                if (retakeBtn != null) { so.FindProperty("retakeButton").objectReferenceValue = retakeBtn; wired++; }

                var gridOverlay = FindChild(scanPanel.transform, "GridOverlay");
                if (gridOverlay != null) { so.FindProperty("gridOverlay").objectReferenceValue = gridOverlay.GetComponent<RectTransform>(); wired++; }

                var cameraPreview = FindChild(scanPanel.transform, "CameraPreview");
                if (cameraPreview != null)
                {
                    var rawImg = cameraPreview.GetComponent<RawImage>();
                    if (rawImg != null) { so.FindProperty("cameraPreview").objectReferenceValue = rawImg; wired++; }
                }

                // ColorDetector reference
                var cd = Object.FindFirstObjectByType<ColorDetection.ColorDetector>();
                if (cd != null) { so.FindProperty("colorDetector").objectReferenceValue = cd; wired++; }

                so.ApplyModifiedProperties();
                Debug.Log($"[AutoWire] ScanningUI: done");
            }

            // === 4. StepManager on StepPanel ===
            var stepManager = Object.FindFirstObjectByType<UI.StepManager>(FindObjectsInactive.Include);
            if (stepManager != null)
            {
                var so = new SerializedObject(stepManager);
                var stepPanel = stepManager.gameObject;

                var countText = FindChildTMP(stepPanel.transform, "StepCountText");
                if (countText != null) { so.FindProperty("stepCountText").objectReferenceValue = countText; wired++; }

                var descText = FindChildTMP(stepPanel.transform, "StepDescText");
                if (descText != null) { so.FindProperty("stepDescText").objectReferenceValue = descText; wired++; }

                var notationText = FindChildTMP(stepPanel.transform, "MoveNotationText");
                if (notationText != null) { so.FindProperty("moveNotationText").objectReferenceValue = notationText; wired++; }

                var nextBtn = FindChildButton(stepPanel.transform, "NextButton");
                if (nextBtn != null) { so.FindProperty("nextButton").objectReferenceValue = nextBtn; wired++; }

                var prevBtn = FindChildButton(stepPanel.transform, "PrevButton");
                if (prevBtn != null) { so.FindProperty("prevButton").objectReferenceValue = prevBtn; wired++; }

                var guide = Object.FindFirstObjectByType<AR.ARStepGuide>();
                if (guide != null) { so.FindProperty("arStepGuide").objectReferenceValue = guide; wired++; }

                so.ApplyModifiedProperties();
                Debug.Log($"[AutoWire] StepManager: done");
            }

            // === 5. ARStepGuide arrows ===
            var arStepGuide = Object.FindFirstObjectByType<AR.ARStepGuide>();
            if (arStepGuide != null)
            {
                var so = new SerializedObject(arStepGuide);
                string[] arrowNames = { "Arrow_U", "Arrow_D", "Arrow_F", "Arrow_B", "Arrow_L", "Arrow_R" };
                string[] propNames = { "arrowU", "arrowD", "arrowF", "arrowB", "arrowL", "arrowR" };

                for (int i = 0; i < 6; i++)
                {
                    var arrow = FindChild(arStepGuide.transform, arrowNames[i]);
                    var prop = so.FindProperty(propNames[i]);
                    if (arrow != null && prop != null)
                    {
                        prop.objectReferenceValue = arrow;
                        wired++;
                    }
                }
                so.ApplyModifiedProperties();
                Debug.Log($"[AutoWire] ARStepGuide: done");
            }

            // Save scene
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

            Debug.Log($"[AutoWire] 完成！共連接 {wired} 個參考。");
            EditorUtility.DisplayDialog("自動連接完成",
                $"成功連接 {wired} 個 SerializeField 參考！\n\n請按 Ctrl+S 儲存場景。",
                "OK");
        }

        private static void SetGO(SerializedObject so, string propName, string goName, ref int count)
        {
            var go = GameObject.Find(goName);
            if (go == null)
            {
                // Also search inactive objects
                var all = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var g in all)
                {
                    if (g.name == goName && g.scene.isLoaded)
                    {
                        go = g;
                        break;
                    }
                }
            }
            if (go != null)
            {
                so.FindProperty(propName).objectReferenceValue = go;
                count++;
            }
            else
            {
                Debug.LogWarning($"[AutoWire] GameObject '{goName}' not found for property '{propName}'");
            }
        }

        private static Transform FindChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static TextMeshProUGUI FindChildTMP(Transform parent, string name)
        {
            var t = FindChild(parent, name);
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }

        private static Button FindChildButton(Transform parent, string name)
        {
            var t = FindChild(parent, name);
            return t != null ? t.GetComponent<Button>() : null;
        }
    }
}
#endif
