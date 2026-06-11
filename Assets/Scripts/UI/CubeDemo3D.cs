using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using RubiksCube.Data;
using RubiksCube.ColorDetection;

namespace RubiksCube.UI
{
    /// <summary>
    /// A virtual 3D Rubik's cube shown on the step-guide screen. It is painted
    /// with the user's actual scanned colors and loop-previews the move for the
    /// current step, so the user matches by COLOR (not abstract R/U/F notation)
    /// and just copies whichever layer is spinning. Drag to orbit the view.
    ///
    /// Rendered by a dedicated camera into a RenderTexture (layer-isolated from
    /// the AR scene) and displayed via a RawImage on the step panel.
    /// </summary>
    public class CubeDemo3D : MonoBehaviour
    {
        private const int DemoLayer = 31;
        private const float Spacing = 1.0f;
        private static readonly Vector3 RigPos = new Vector3(0f, 1000f, 0f);

        private ColorDetector detector;
        private Camera demoCamera;
        private Transform cubeRoot;   // orbit container
        private Transform pivot;      // per-move rotation pivot (child of cubeRoot)
        private RawImage display;

        private readonly Dictionary<Vector3Int, Transform> cubies = new Dictionary<Vector3Int, Transform>();
        private readonly Dictionary<char, Material> materialCache = new Dictionary<char, Material>();

        private List<string> stateLetters;   // facelet-letter string before each step
        private Dictionary<char, char> letterToColor;
        private List<MoveStep> moves;

        private Coroutine previewRoutine;
        private bool built;

        // Per-face outward normals (cube-local). Face order: U R F D L B.
        private static readonly Vector3[] FaceNormal =
        {
            new Vector3(0, 1, 0),   // U
            new Vector3(1, 0, 0),   // R
            new Vector3(0, 0, -1),  // F
            new Vector3(0, -1, 0),  // D
            new Vector3(-1, 0, 0),  // L
            new Vector3(0, 0, 1),   // B
        };

        // ----------------------------------------------------------------- setup

        public void Initialize(Transform uiParent)
        {
            if (built) return;
            built = true;

            // Keep the demo cube out of the AR camera's view
            if (Camera.main != null)
                Camera.main.cullingMask &= ~(1 << DemoLayer);

            // Rig + orbit container, parked far from the AR scene
            var rig = new GameObject("CubeDemoRig");
            rig.transform.position = RigPos;
            cubeRoot = new GameObject("CubeRoot").transform;
            cubeRoot.SetParent(rig.transform, false);
            pivot = new GameObject("MovePivot").transform;
            pivot.SetParent(cubeRoot, false);

            // Dedicated camera -> RenderTexture
            var camGO = new GameObject("CubeDemoCamera");
            camGO.transform.SetParent(rig.transform, false);
            camGO.transform.localPosition = new Vector3(3.4f, 2.7f, -4.4f);
            camGO.transform.LookAt(rig.transform.position);
            demoCamera = camGO.AddComponent<Camera>();
            demoCamera.cullingMask = 1 << DemoLayer;
            demoCamera.clearFlags = CameraClearFlags.SolidColor;
            demoCamera.backgroundColor = new Color(0.05f, 0.05f, 0.08f, 0f);
            demoCamera.fieldOfView = 38f;
            demoCamera.nearClipPlane = 0.1f;
            demoCamera.farClipPlane = 50f;

            var rt = new RenderTexture(640, 640, 16, RenderTextureFormat.ARGB32);
            rt.Create();
            demoCamera.targetTexture = rt;

            // RawImage on the step panel (upper area)
            var imgGO = new GameObject("CubeDemoView");
            imgGO.transform.SetParent(uiParent, false);
            var rect = imgGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.52f);
            rect.anchorMax = new Vector2(0.5f, 0.52f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(620, 620);
            display = imgGO.AddComponent<RawImage>();
            display.texture = rt;
            display.raycastTarget = true;

            var dragger = imgGO.AddComponent<CubeDemoDragger>();
            dragger.target = this;
        }

        // ------------------------------------------------------------- public API

        public void LoadSolution(CubeState scanned, List<MoveStep> solution, ColorDetector colorDetector)
        {
            detector = colorDetector;
            moves = solution;

            // Map facelet letters (U R F D L B) back to the scanned center colors
            letterToColor = new Dictionary<char, char>
            {
                ['U'] = scanned.faces[0][4],
                ['R'] = scanned.faces[1][4],
                ['F'] = scanned.faces[2][4],
                ['D'] = scanned.faces[3][4],
                ['L'] = scanned.faces[4][4],
                ['B'] = scanned.faces[5][4],
            };

            // Precompute the cube state before every step using the cubie model
            stateLetters = new List<string>();
            string k0 = scanned.ToKociembaString();
            var cc = new Kociemba.FaceCube(k0).ToCubieCube();
            stateLetters.Add(cc.ToFaceCube().ToFaceletString());
            foreach (var m in moves)
            {
                ApplyMove(cc, m);
                stateLetters.Add(cc.ToFaceCube().ToFaceletString());
            }

            ShowStep(0);
        }

        public void ShowStep(int index)
        {
            if (stateLetters == null || index < 0 || index >= stateLetters.Count) return;

            StopPreview();
            ResetPivot();
            BuildCube(stateLetters[index]);

            // Preview the move to perform now (no move on the final solved state)
            if (moves != null && index < moves.Count)
                previewRoutine = StartCoroutine(PreviewLoop(moves[index]));
        }

        public void Orbit(Vector2 delta)
        {
            if (cubeRoot == null) return;
            cubeRoot.Rotate(Vector3.up, -delta.x * 0.4f, Space.World);
            cubeRoot.Rotate(Vector3.right, delta.y * 0.4f, Space.World);
        }

        private void OnDisable() => StopPreview();

        // ------------------------------------------------------------- cube build

        private void BuildCube(string letters)
        {
            // Clear previous cubies
            foreach (var kv in cubies)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            cubies.Clear();

            // 27 cubie bodies
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        var coord = new Vector3Int(x, y, z);
                        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        body.name = $"Cubie_{x}_{y}_{z}";
                        SetLayerRecursive(body, DemoLayer);
                        Destroy(body.GetComponent<Collider>());
                        body.transform.SetParent(cubeRoot, false);
                        body.transform.localPosition = (Vector3)coord * Spacing;
                        body.transform.localScale = Vector3.one * 0.96f;
                        body.GetComponent<Renderer>().sharedMaterial = GetMaterial('K'); // black body
                        cubies[coord] = body.transform;
                    }

            // 54 stickers (children of their cubie so they move with it)
            for (int f = 0; f < 6; f++)
            {
                for (int idx = 0; idx < 9; idx++)
                {
                    int r = idx / 3, c = idx % 3;
                    GetFaceletPlacement(f, r, c, out Vector3Int coord, out Vector3 normal);

                    char letter = letters[f * 9 + idx];
                    char colorChar = letterToColor.TryGetValue(letter, out char cc) ? cc : '?';

                    var sticker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    sticker.name = $"Sticker_{f}_{idx}";
                    SetLayerRecursive(sticker, DemoLayer);
                    Destroy(sticker.GetComponent<Collider>());
                    sticker.transform.SetParent(cubies[coord], false);
                    sticker.transform.localPosition = normal * 0.5f;
                    sticker.transform.localRotation = Quaternion.LookRotation(normal);
                    sticker.transform.localScale = new Vector3(0.82f, 0.82f, 0.06f);
                    sticker.GetComponent<Renderer>().sharedMaterial = GetMaterial(colorChar);
                }
            }
        }

        private void GetFaceletPlacement(int face, int r, int c, out Vector3Int coord, out Vector3 normal)
        {
            normal = FaceNormal[face];
            switch (face)
            {
                case 0: coord = new Vector3Int(c - 1, 1, 1 - r); break;        // U
                case 1: coord = new Vector3Int(1, 1 - r, 1 - c); break;        // R
                case 2: coord = new Vector3Int(c - 1, 1 - r, -1); break;       // F
                case 3: coord = new Vector3Int(c - 1, -1, r - 1); break;       // D
                case 4: coord = new Vector3Int(-1, 1 - r, c - 1); break;       // L
                default: coord = new Vector3Int(1 - c, 1 - r, 1); break;       // B
            }
        }

        // --------------------------------------------------------- move animation

        private IEnumerator PreviewLoop(MoveStep m)
        {
            int face = (int)m.face;
            Vector3 normal = FaceNormal[face];
            float target = m.turns == 2 ? 180f : (m.turns == 1 ? 90f : -90f);

            var layer = GetLayer(face);

            while (true)
            {
                ResetPivot();
                foreach (var t in layer) t.SetParent(pivot, true);

                yield return RotatePivot(normal, 0f, target, 0.45f);
                pivot.localRotation = Quaternion.AngleAxis(target, normal);
                yield return new WaitForSeconds(0.4f);
                yield return RotatePivot(normal, target, 0f, 0.3f);

                foreach (var t in layer)
                    if (t != null) t.SetParent(cubeRoot, true);
                ResetPivot();

                yield return new WaitForSeconds(0.55f);
            }
        }

        private IEnumerator RotatePivot(Vector3 normal, float from, float to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / dur));
                pivot.localRotation = Quaternion.AngleAxis(a, normal);
                yield return null;
            }
        }

        private List<Transform> GetLayer(int face)
        {
            var list = new List<Transform>();
            foreach (var kv in cubies)
            {
                var p = kv.Key;
                bool inLayer = face switch
                {
                    0 => p.y == 1,   // U
                    1 => p.x == 1,   // R
                    2 => p.z == -1,  // F
                    3 => p.y == -1,  // D
                    4 => p.x == -1,  // L
                    _ => p.z == 1,   // B
                };
                if (inLayer) list.Add(kv.Value);
            }
            return list;
        }

        private void ResetPivot()
        {
            if (pivot != null) pivot.localRotation = Quaternion.identity;
        }

        private void StopPreview()
        {
            if (previewRoutine != null)
            {
                StopCoroutine(previewRoutine);
                previewRoutine = null;
            }
            // Re-home any cubies still parented to the pivot
            if (pivot != null && cubeRoot != null)
            {
                var stragglers = new List<Transform>();
                foreach (Transform child in pivot) stragglers.Add(child);
                foreach (var t in stragglers) t.SetParent(cubeRoot, true);
                ResetPivot();
            }
        }

        // ------------------------------------------------------------- utilities

        private void ApplyMove(Kociemba.CubieCube cc, MoveStep m)
        {
            int axis = (int)m.face; // CubeFace U R F D L B == moveCube order
            int power = m.turns == 2 ? 2 : (m.turns == 1 ? 1 : 3);
            for (int p = 0; p < power; p++)
                cc.Multiply(Kociemba.CubieCube.moveCube[axis]);
        }

        private Material GetMaterial(char colorChar)
        {
            if (materialCache.TryGetValue(colorChar, out Material cached))
                return cached;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader);
            Color col = ColorFor(colorChar);
            mat.color = col;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            materialCache[colorChar] = mat;
            return mat;
        }

        private Color ColorFor(char colorChar)
        {
            if (colorChar == 'K') return new Color(0.08f, 0.08f, 0.08f); // cubie body
            if (detector != null && colorChar != '?')
                return detector.GetPreviewColor(colorChar);

            return colorChar switch
            {
                'W' => Color.white,
                'Y' => Color.yellow,
                'R' => new Color(1f, 0.2f, 0.4f),
                'O' => new Color(1f, 0.5f, 0f),
                'B' => new Color(0.2f, 0.4f, 1f),
                'G' => new Color(0.2f, 0.9f, 0.3f),
                _ => Color.gray,
            };
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        /// <summary>Drag handler on the RawImage that orbits the cube view.</summary>
        public class CubeDemoDragger : MonoBehaviour, IDragHandler
        {
            public CubeDemo3D target;
            public void OnDrag(PointerEventData eventData) => target?.Orbit(eventData.delta);
        }
    }
}
