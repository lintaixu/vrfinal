using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RubiksCube.Data;
using RubiksCube.ColorDetection;

namespace RubiksCube.UI
{
    /// <summary>
    /// Interactive unfolded-cube ("net") preview for the Confirm screen.
    /// Shows all 6 scanned faces assembled in a cross layout so the user can
    /// verify their relative orientation. Tap a sticker to cycle its color,
    /// tap a center (marked ↻) to rotate that whole face 90° clockwise —
    /// fixing photos taken at the wrong angle without rescanning.
    /// </summary>
    public class CubeNetView : MonoBehaviour
    {
        private const float CellSize = 70f;
        private const float CellGap = 3f;
        private const float FaceSpacing = 226f; // 3*70 + 2*3 + 10

        private CubeState state;
        private ColorDetector detector;
        private Action onRescan;

        private bool built;
        private Image[][] cellImages;
        private TextMeshProUGUI statusText;
        private RectTransform netRoot;

        // CubeState face order: U R F D L B
        private static readonly string[] FaceLabels = { "U", "R", "F", "D", "L", "B" };
        private static readonly Vector2[] FaceOffsets =
        {
            new Vector2(-113f,  226f), // U above F
            new Vector2( 113f,    0f), // R right of F
            new Vector2(-113f,    0f), // F center
            new Vector2(-113f, -226f), // D below F
            new Vector2(-339f,    0f), // L left of F
            new Vector2( 339f,    0f), // B far right
        };

        // 90° clockwise rotation: new[i] = old[RotCW[i]]
        private static readonly int[] RotCW = { 6, 3, 0, 7, 4, 1, 8, 5, 2 };

        private const string ColorCycle = "WYROBG";

        public void Show(CubeState cubeState, ColorDetector colorDetector,
                         Button confirmButton, Action rescanAction)
        {
            state = cubeState;
            detector = colorDetector;
            onRescan = rescanAction;

            if (!built)
                Build(confirmButton);

            Refresh();
        }

        public void SetStatus(string message, bool isError)
        {
            if (statusText == null) return;
            statusText.text = message;
            statusText.color = isError ? new Color(1f, 0.55f, 0.3f) : new Color(0.4f, 1f, 0.5f);
        }

        private void Build(Button confirmButton)
        {
            built = true;

            // Hide the old static title so it doesn't overlap the net
            var oldTitle = transform.Find("Title");
            if (oldTitle != null) oldTitle.gameObject.SetActive(false);

            // Status text at the top
            var statusGO = new GameObject("NetStatusText");
            statusGO.transform.SetParent(transform, false);
            var statusRect = statusGO.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0.05f, 0.80f);
            statusRect.anchorMax = new Vector2(0.95f, 0.93f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            statusText = statusGO.AddComponent<TextMeshProUGUI>();
            statusText.fontSize = 30;
            statusText.alignment = TextAlignmentOptions.Center;
            statusText.color = Color.white;
            statusText.text = "Check the scan result";

            // Hint text under the status
            var hintGO = new GameObject("NetHintText");
            hintGO.transform.SetParent(transform, false);
            var hintRect = hintGO.AddComponent<RectTransform>();
            hintRect.anchorMin = new Vector2(0.05f, 0.74f);
            hintRect.anchorMax = new Vector2(0.95f, 0.80f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;
            var hintTMP = hintGO.AddComponent<TextMeshProUGUI>();
            hintTMP.fontSize = 22;
            hintTMP.alignment = TextAlignmentOptions.Center;
            hintTMP.color = new Color(0.8f, 0.8f, 0.8f);
            hintTMP.text = "Tap a sticker to fix its color. Tap a center letter to rotate that face 90.";

            // Net root (center of panel, nudged down a little)
            var rootGO = new GameObject("NetRoot");
            rootGO.transform.SetParent(transform, false);
            netRoot = rootGO.AddComponent<RectTransform>();
            netRoot.anchorMin = new Vector2(0.5f, 0.5f);
            netRoot.anchorMax = new Vector2(0.5f, 0.5f);
            netRoot.pivot = new Vector2(0.5f, 0.5f);
            netRoot.anchoredPosition = new Vector2(0f, -40f);
            netRoot.sizeDelta = Vector2.zero;

            cellImages = new Image[6][];
            for (int f = 0; f < 6; f++)
            {
                cellImages[f] = new Image[9];
                BuildFace(f);
            }

            // Reposition the Confirm button to the bottom-right
            if (confirmButton != null)
            {
                var rect = confirmButton.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.55f, 0.04f);
                rect.anchorMax = new Vector2(0.95f, 0.12f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            // Create a Rescan button at the bottom-left
            var rescanGO = new GameObject("RescanButton");
            rescanGO.transform.SetParent(transform, false);
            var rescanRect = rescanGO.AddComponent<RectTransform>();
            rescanRect.anchorMin = new Vector2(0.05f, 0.04f);
            rescanRect.anchorMax = new Vector2(0.45f, 0.12f);
            rescanRect.offsetMin = Vector2.zero;
            rescanRect.offsetMax = Vector2.zero;
            var rescanImg = rescanGO.AddComponent<Image>();
            rescanImg.color = new Color(0.8f, 0.45f, 0.2f);
            var rescanBtn = rescanGO.AddComponent<Button>();
            rescanBtn.onClick.AddListener(() => onRescan?.Invoke());

            var rescanTextGO = new GameObject("Text");
            rescanTextGO.transform.SetParent(rescanGO.transform, false);
            var rtr = rescanTextGO.AddComponent<RectTransform>();
            rtr.anchorMin = Vector2.zero;
            rtr.anchorMax = Vector2.one;
            rtr.offsetMin = Vector2.zero;
            rtr.offsetMax = Vector2.zero;
            var rescanTMP = rescanTextGO.AddComponent<TextMeshProUGUI>();
            rescanTMP.text = "Rescan";
            rescanTMP.fontSize = 32;
            rescanTMP.alignment = TextAlignmentOptions.Center;
            rescanTMP.color = Color.white;
        }

        private void BuildFace(int face)
        {
            var faceGO = new GameObject($"Face_{FaceLabels[face]}");
            faceGO.transform.SetParent(netRoot, false);
            var faceRect = faceGO.AddComponent<RectTransform>();
            faceRect.anchoredPosition = FaceOffsets[face];
            faceRect.sizeDelta = Vector2.zero;

            for (int i = 0; i < 9; i++)
            {
                int row = i / 3;
                int col = i % 3;

                var cellGO = new GameObject($"Cell_{i}");
                cellGO.transform.SetParent(faceGO.transform, false);
                var cellRect = cellGO.AddComponent<RectTransform>();
                cellRect.sizeDelta = new Vector2(CellSize, CellSize);
                cellRect.anchoredPosition = new Vector2(
                    (col - 1) * (CellSize + CellGap),
                    (1 - row) * (CellSize + CellGap));

                var img = cellGO.AddComponent<Image>();
                img.color = Color.gray;
                cellImages[face][i] = img;

                var btn = cellGO.AddComponent<Button>();
                int f = face, idx = i;
                if (i == 4)
                {
                    btn.onClick.AddListener(() => RotateFace(f));

                    // Label the center with its face letter + rotate hint
                    var labelGO = new GameObject("Label");
                    labelGO.transform.SetParent(cellGO.transform, false);
                    var labelRect = labelGO.AddComponent<RectTransform>();
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = Vector2.zero;
                    labelRect.offsetMax = Vector2.zero;
                    var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
                    labelTMP.text = FaceLabels[face];
                    labelTMP.fontSize = 26;
                    labelTMP.alignment = TextAlignmentOptions.Center;
                    labelTMP.color = new Color(0f, 0f, 0f, 0.75f);
                    labelTMP.raycastTarget = false;
                }
                else
                {
                    btn.onClick.AddListener(() => CycleCell(f, idx));
                }
            }
        }

        private void CycleCell(int face, int index)
        {
            char current = state.faces[face][index];
            int pos = ColorCycle.IndexOf(current);
            char next = ColorCycle[(pos + 1 + ColorCycle.Length) % ColorCycle.Length];
            state.faces[face][index] = next;
            Refresh();
        }

        private void RotateFace(int face)
        {
            char[] old = (char[])state.faces[face].Clone();
            for (int i = 0; i < 9; i++)
                state.faces[face][i] = old[RotCW[i]];
            Refresh();
        }

        private void Refresh()
        {
            if (state == null || cellImages == null) return;

            for (int f = 0; f < 6; f++)
            {
                for (int i = 0; i < 9; i++)
                {
                    char c = state.faces[f][i];
                    Color color = detector != null ? detector.GetPreviewColor(c) : Color.gray;
                    if (cellImages[f][i] != null)
                        cellImages[f][i].color = color;
                }
            }

            // Live validation feedback
            if (state.Validate(out string error))
                SetStatus("Looks good — press Confirm to solve!", false);
            else
                SetStatus(error, true);
        }
    }
}
