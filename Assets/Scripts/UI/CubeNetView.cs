using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using RubiksCube.Data;
using RubiksCube.ColorDetection;

namespace RubiksCube.UI
{
    /// <summary>
    /// Interactive unfolded-cube ("net") preview for the Confirm screen.
    /// Shows all 6 scanned faces assembled in a cross layout so the user can
    /// verify their relative orientation.
    ///
    /// Interactions (drag-based to avoid accidental taps):
    ///  - Drag a face (grab any of its stickers) onto another face to swap
    ///    the entire 9-sticker faces — fixes scans done in the wrong order
    ///  - Drag a color from the palette row onto a single sticker to repaint it
    ///  - Tap a face's center letter to rotate that face 90° clockwise
    /// </summary>
    public class CubeNetView : MonoBehaviour
    {
        private const float CellSize = 70f;
        private const float CellGap = 3f;

        internal const string ColorCycle = "WYROBG";

        private CubeState state;
        private ColorDetector detector;
        private Action onRescan;

        private bool built;
        private Image[][] cellImages;
        private TextMeshProUGUI statusText;
        private RectTransform netRoot;
        private Image ghost;

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
            statusRect.anchorMin = new Vector2(0.05f, 0.82f);
            statusRect.anchorMax = new Vector2(0.95f, 0.94f);
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
            hintRect.anchorMin = new Vector2(0.05f, 0.75f);
            hintRect.anchorMax = new Vector2(0.95f, 0.82f);
            hintRect.offsetMin = Vector2.zero;
            hintRect.offsetMax = Vector2.zero;
            var hintTMP = hintGO.AddComponent<TextMeshProUGUI>();
            hintTMP.fontSize = 21;
            hintTMP.alignment = TextAlignmentOptions.Center;
            hintTMP.color = new Color(0.8f, 0.8f, 0.8f);
            hintTMP.text = "Drag a face onto another face to swap them. Drag a palette color onto a sticker to repaint.\nTap a center letter to rotate that face 90.";

            // Net root (center of panel, nudged down a little)
            var rootGO = new GameObject("NetRoot");
            rootGO.transform.SetParent(transform, false);
            netRoot = rootGO.AddComponent<RectTransform>();
            netRoot.anchorMin = new Vector2(0.5f, 0.5f);
            netRoot.anchorMax = new Vector2(0.5f, 0.5f);
            netRoot.pivot = new Vector2(0.5f, 0.5f);
            netRoot.anchoredPosition = new Vector2(0f, -10f);
            netRoot.sizeDelta = Vector2.zero;

            cellImages = new Image[6][];
            for (int f = 0; f < 6; f++)
            {
                cellImages[f] = new Image[9];
                BuildFace(f);
            }

            BuildPalette();

            // Reposition the Confirm button to the bottom-right
            if (confirmButton != null)
            {
                var rect = confirmButton.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.55f, 0.03f);
                rect.anchorMax = new Vector2(0.95f, 0.10f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
            }

            // Create a Rescan button at the bottom-left
            var rescanGO = new GameObject("RescanButton");
            rescanGO.transform.SetParent(transform, false);
            var rescanRect = rescanGO.AddComponent<RectTransform>();
            rescanRect.anchorMin = new Vector2(0.05f, 0.03f);
            rescanRect.anchorMax = new Vector2(0.45f, 0.10f);
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

                // Every sticker (center included) is draggable: dragging moves
                // the WHOLE face. Drag-begin cancels the click, so the center's
                // tap-to-rotate still works when the finger doesn't move.
                var cell = cellGO.AddComponent<CubeNetCell>();
                cell.Init(this, face, i);

                if (i == 4)
                {
                    // Center: tap (without dragging) rotates the face
                    var btn = cellGO.AddComponent<Button>();
                    int f = face;
                    btn.onClick.AddListener(() => RotateFace(f));

                    var labelGO = new GameObject("Label");
                    labelGO.transform.SetParent(cellGO.transform, false);
                    var labelRect = labelGO.AddComponent<RectTransform>();
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = Vector2.zero;
                    labelRect.offsetMax = Vector2.zero;
                    var labelTMP = labelGO.AddComponent<TextMeshProUGUI>();
                    labelTMP.text = FaceLabels[face];
                    labelTMP.fontSize = 28;
                    labelTMP.alignment = TextAlignmentOptions.Center;
                    labelTMP.color = new Color(0f, 0f, 0f, 0.75f);
                    labelTMP.raycastTarget = false;
                }
            }
        }

        private void BuildPalette()
        {
            // A row of 6 reference colors below the net; drag one onto a
            // sticker to repaint that sticker.
            const float palCell = 64f;
            const float palGap = 14f;
            float startX = -(5f * (palCell + palGap)) / 2f;

            var palGO = new GameObject("Palette");
            palGO.transform.SetParent(transform, false);
            var palRect = palGO.AddComponent<RectTransform>();
            palRect.anchorMin = new Vector2(0.5f, 0.135f);
            palRect.anchorMax = new Vector2(0.5f, 0.135f);
            palRect.pivot = new Vector2(0.5f, 0.5f);
            palRect.anchoredPosition = Vector2.zero;
            palRect.sizeDelta = Vector2.zero;

            for (int i = 0; i < ColorCycle.Length; i++)
            {
                var swatchGO = new GameObject($"Swatch_{ColorCycle[i]}");
                swatchGO.transform.SetParent(palGO.transform, false);
                var rect = swatchGO.AddComponent<RectTransform>();
                rect.sizeDelta = new Vector2(palCell, palCell);
                rect.anchoredPosition = new Vector2(startX + i * (palCell + palGap), 0f);

                var img = swatchGO.AddComponent<Image>();
                img.color = detector != null
                    ? detector.GetPreviewColor(ColorCycle[i])
                    : Color.gray;

                var cell = swatchGO.AddComponent<CubeNetCell>();
                cell.InitPalette(this, i);
            }
        }

        private void RotateFace(int face)
        {
            char[] old = (char[])state.faces[face].Clone();
            for (int i = 0; i < 9; i++)
                state.faces[face][i] = old[RotCW[i]];
            Refresh();
        }

        internal void HandleDrop(CubeNetCell source, CubeNetCell target)
        {
            if (state == null || target.IsPalette) return;

            Debug.Log($"[Net] Drop: source(face={source.Face},idx={source.Index},palette={source.IsPalette}) -> target(face={target.Face},idx={target.Index})");

            if (source.IsPalette)
            {
                // Centers define the face — don't allow repainting them
                if (target.Index == 4) return;

                // Paint the single target sticker with the palette color
                state.faces[target.Face][target.Index] = ColorCycle[source.Index];
            }
            else if (source.Face != target.Face)
            {
                // Swap the ENTIRE faces (all 9 stickers) — fixes faces that
                // were scanned in the wrong order
                char[] tmp = state.faces[source.Face];
                state.faces[source.Face] = state.faces[target.Face];
                state.faces[target.Face] = tmp;
            }

            Refresh();
        }

        internal void BeginGhost(CubeNetCell cell, PointerEventData eventData)
        {
            EndGhost();

            var go = new GameObject("DragGhost");
            go.transform.SetParent(transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            if (cell.IsPalette)
            {
                // Single color swatch ghost
                rect.sizeDelta = new Vector2(CellSize, CellSize);
                ghost = go.AddComponent<Image>();
                Color gc = detector != null
                    ? detector.GetPreviewColor(ColorCycle[cell.Index])
                    : Color.gray;
                gc.a = 0.8f;
                ghost.color = gc;
                ghost.raycastTarget = false; // must not block drop detection
            }
            else
            {
                // Whole-face 3x3 mini ghost so the user sees which face moves
                const float mini = 34f;
                const float miniGap = 2f;
                rect.sizeDelta = new Vector2(3 * mini + 2 * miniGap, 3 * mini + 2 * miniGap);

                ghost = go.AddComponent<Image>();
                ghost.color = new Color(0f, 0f, 0f, 0.25f);
                ghost.raycastTarget = false;

                for (int i = 0; i < 9; i++)
                {
                    int row = i / 3;
                    int col = i % 3;

                    var miniGO = new GameObject($"Mini_{i}");
                    miniGO.transform.SetParent(go.transform, false);
                    var miniRect = miniGO.AddComponent<RectTransform>();
                    miniRect.sizeDelta = new Vector2(mini, mini);
                    miniRect.anchoredPosition = new Vector2(
                        (col - 1) * (mini + miniGap),
                        (1 - row) * (mini + miniGap));

                    var miniImg = miniGO.AddComponent<Image>();
                    Color mc = detector != null
                        ? detector.GetPreviewColor(state.faces[cell.Face][i])
                        : Color.gray;
                    mc.a = 0.85f;
                    miniImg.color = mc;
                    miniImg.raycastTarget = false;
                }
            }

            MoveGhost(eventData);
        }

        internal void MoveGhost(PointerEventData eventData)
        {
            if (ghost == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                (RectTransform)transform, eventData.position,
                eventData.pressEventCamera, out Vector2 localPoint);
            ((RectTransform)ghost.transform).anchoredPosition = localPoint;
        }

        internal void EndGhost()
        {
            if (ghost != null)
            {
                Destroy(ghost.gameObject);
                ghost = null;
            }
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

    /// <summary>
    /// Drag/drop behaviour for one net sticker or palette swatch.
    /// EventSystem's drag threshold makes this robust against accidental taps.
    /// </summary>
    public class CubeNetCell : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private CubeNetView net;

        public int Face { get; private set; }   // 0-5, or -1 for palette
        public int Index { get; private set; }  // sticker index, or palette color index
        public bool IsPalette => Face < 0;

        public void Init(CubeNetView view, int face, int index)
        {
            net = view;
            Face = face;
            Index = index;
        }

        public void InitPalette(CubeNetView view, int colorIndex)
        {
            net = view;
            Face = -1;
            Index = colorIndex;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Debug.Log($"[Net] BeginDrag: face={Face}, idx={Index}, palette={IsPalette}");
            net?.BeginGhost(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            net?.MoveGhost(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            net?.EndGhost();
        }

        public void OnDrop(PointerEventData eventData)
        {
            var source = eventData.pointerDrag != null
                ? eventData.pointerDrag.GetComponent<CubeNetCell>()
                : null;
            if (source != null && source != this)
                net?.HandleDrop(source, this);
        }
    }
}
