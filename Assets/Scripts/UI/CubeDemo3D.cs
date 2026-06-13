using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using RubiksCube.Data;
using RubiksCube.ColorDetection;

namespace RubiksCube.UI
{
    /// <summary>
    /// A virtual 3D Rubik's cube anchored in AR world space during the step
    /// guide. It is painted with the user's actual scanned colors and floats
    /// fixed in the air, so the user can physically walk around it 360° to see
    /// every face. For each step the correct layer loop-previews its 90° turn,
    /// so the user matches by COLOR and just copies the spinning face — no need
    /// to know which physical face is "R".
    ///
    /// Rendered by the live AR camera (re-enabled when entering the step guide,
    /// after scanning released it). Drag on screen also spins it in place.
    /// </summary>
    public class CubeDemo3D : MonoBehaviour
    {
        private const float Spacing = 1.0f;
        private const float WorldScale = 0.045f; // ~0.1 m cube
        private const float PlaceDistance = 0.45f;

        private ColorDetector detector;
        private Transform cubeRoot;   // world-anchored container
        private Transform pivot;      // per-move rotation pivot (child of cubeRoot)

        private readonly Dictionary<Vector3Int, Transform> cubies = new Dictionary<Vector3Int, Transform>();
        private readonly Dictionary<char, Material> materialCache = new Dictionary<char, Material>();

        private List<string> stateLetters;   // facelet-letter string before each step
        private Dictionary<char, char> letterToColor;
        private List<MoveStep> moves;

        private Coroutine previewRoutine;
        private bool built;

        // Gyroscope turntable: rotate the phone to view the cube from any side.
        // ARCore 6DoF tracking does not work in this close-up / handheld-cube
        // scenario on this device, so we drive the view from the gyro instead.
        // Use absolute attitude (not rate integration) for a drift-free 1:1 feel.
        private bool gyroReady;
        private bool gyroCalibrated;
        private Quaternion gyroOffset = Quaternion.identity;
        private Quaternion baseRotation = Quaternion.identity;

        // World-anchor mode: when ARCore is tracking, freeze the cube in space
        // so the user can physically walk around it (camera moves via the
        // Tracked Pose Driver). Falls back to the gyro turntable otherwise.
        private bool worldAnchored;
        private GameObject anchorObj;
        private TMPro.TextMeshProUGUI lockButtonLabel;
        private TMPro.TextMeshProUGUI statusLabel;
        private float stateLogTimer;

        // AR components disabled by scanning that we must turn back on
        private ARSession arSession;
        private ARCameraManager arCameraManager;
        private ARCameraBackground arCameraBackground;

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

            // World-space container for the cube (not parented to the camera,
            // so it stays fixed in the room as the user moves around it)
            cubeRoot = new GameObject("ARCubeRoot").transform;
            cubeRoot.localScale = Vector3.one * WorldScale;
            pivot = new GameObject("MovePivot").transform;
            pivot.SetParent(cubeRoot, false);

            // Let the AR feed show through the step panel
            var panelImg = GetComponent<Image>();
            if (panelImg != null)
            {
                var c = panelImg.color;
                panelImg.color = new Color(c.r, c.g, c.b, 0f);
            }

            // Transparent full-panel drag catcher (behind the buttons) to spin
            var dragGO = new GameObject("CubeDragCatcher");
            dragGO.transform.SetParent(uiParent, false);
            var dragRect = dragGO.AddComponent<RectTransform>();
            dragRect.anchorMin = Vector2.zero;
            dragRect.anchorMax = Vector2.one;
            dragRect.offsetMin = Vector2.zero;
            dragRect.offsetMax = Vector2.zero;
            var dragImg = dragGO.AddComponent<Image>();
            dragImg.color = new Color(0, 0, 0, 0f);
            dragImg.raycastTarget = true;
            dragGO.AddComponent<CubeDemoDragger>().target = this;
            dragGO.transform.SetAsFirstSibling();

            // "Recenter" button to re-place the cube in front of the current view
            var btnGO = new GameObject("RecenterButton");
            btnGO.transform.SetParent(uiParent, false);
            var btnRect = btnGO.AddComponent<RectTransform>();
            btnRect.anchorMin = new Vector2(0.35f, 0.17f);
            btnRect.anchorMax = new Vector2(0.65f, 0.23f);
            btnRect.offsetMin = Vector2.zero;
            btnRect.offsetMax = Vector2.zero;
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.2f, 0.5f, 0.9f, 0.85f);
            btnGO.AddComponent<Button>().onClick.AddListener(PlaceInFront);

            var btnTextGO = new GameObject("Text");
            btnTextGO.transform.SetParent(btnGO.transform, false);
            var btnTextRect = btnTextGO.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.offsetMin = Vector2.zero;
            btnTextRect.offsetMax = Vector2.zero;
            var btnTMP = btnTextGO.AddComponent<TMPro.TextMeshProUGUI>();
            btnTMP.text = "Recenter";
            btnTMP.fontSize = 26;
            btnTMP.alignment = TMPro.TextAlignmentOptions.Center;
            btnTMP.color = Color.white;

            // "Lock in space" toggle — anchors the cube when ARCore is tracking
            lockButtonLabel = CreateButton(uiParent, "LockButton",
                new Vector2(0.04f, 0.17f), new Vector2(0.34f, 0.23f),
                "Lock in space", new Color(0.2f, 0.7f, 0.4f, 0.85f), ToggleLock);

            // Status line above the buttons (tracking hints)
            var statusGO = new GameObject("ARStatus");
            statusGO.transform.SetParent(uiParent, false);
            var sRect = statusGO.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0.04f, 0.24f);
            sRect.anchorMax = new Vector2(0.96f, 0.29f);
            sRect.offsetMin = Vector2.zero;
            sRect.offsetMax = Vector2.zero;
            statusLabel = statusGO.AddComponent<TMPro.TextMeshProUGUI>();
            statusLabel.fontSize = 22;
            statusLabel.alignment = TMPro.TextAlignmentOptions.Center;
            statusLabel.color = new Color(1f, 0.9f, 0.4f);
            statusLabel.text = "Rotate the phone to view the cube";
        }

        private TMPro.TextMeshProUGUI CreateButton(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, string text, Color color, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = color;
            go.AddComponent<Button>().onClick.AddListener(onClick);

            var textGO = new GameObject("Text");
            textGO.transform.SetParent(go.transform, false);
            var tr = textGO.AddComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            var tmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 24;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        // ------------------------------------------------------------- public API

        public void LoadSolution(CubeState scanned, List<MoveStep> solution, ColorDetector colorDetector)
        {
            detector = colorDetector;
            moves = solution;

            // Scanning disabled the AR camera; turn it back on for the AR view
            EnableARCamera();

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

            // Enable the gyroscope so phone rotation orbits the cube
            if (!gyroReady && SystemInfo.supportsGyroscope)
            {
                Input.gyro.enabled = true;
                gyroReady = true;
                Debug.Log("[CubeDemo] Gyroscope enabled — rotate the phone to view the cube");
            }

            if (cubeRoot != null) cubeRoot.gameObject.SetActive(true);
            PlaceInFront();
            ShowStep(0);
        }

        private void Update()
        {
            if (cubeRoot == null || !cubeRoot.gameObject.activeInHierarchy) return;

            LogTrackingState();

            // World-anchored: leave the cube fixed in space; the Tracked Pose
            // Driver moves the camera as the user walks around it.
            if (worldAnchored) return;

            var cam = Camera.main;

            // Otherwise float it in front of the camera and use the gyro
            // turntable (works without 6DoF tracking).
            if (cam != null)
                cubeRoot.position = cam.transform.position + cam.transform.forward * PlaceDistance;

            // Absolute-attitude mapping: cube orientation tracks the phone's
            // orientation 1:1 — no drift, rock-solid when the phone is still.
            if (gyroReady)
            {
                Quaternion att = GyroToUnity(Input.gyro.attitude);
                if (!gyroCalibrated)
                {
                    gyroOffset = baseRotation * Quaternion.Inverse(att);
                    gyroCalibrated = true;
                }
                cubeRoot.rotation = gyroOffset * att;
            }
            else
            {
                cubeRoot.rotation = baseRotation;
            }
        }

        private void LogTrackingState()
        {
            stateLogTimer -= Time.deltaTime;
            if (stateLogTimer > 0f) return;
            stateLogTimer = 1f;

            if (!worldAnchored && statusLabel != null)
            {
                bool tracking = ARSession.state == ARSessionState.SessionTracking;
                statusLabel.text = tracking
                    ? "Tracking ready — tap 'Lock in space' to walk around it"
                    : "Rotate phone to view. For 'Lock', aim at a textured surface & move";
            }
            Debug.Log($"[CubeDemo] ARSession.state = {ARSession.state}");
        }

        /// <summary>
        /// Toggle between gyro turntable and world-anchored AR. Anchoring only
        /// works when ARCore has established tracking.
        /// </summary>
        public void ToggleLock()
        {
            if (worldAnchored)
            {
                // Unlock → back to gyro turntable
                worldAnchored = false;
                if (anchorObj != null) { cubeRoot.SetParent(null, true); Destroy(anchorObj); anchorObj = null; }
                gyroCalibrated = false;
                if (lockButtonLabel != null) lockButtonLabel.text = "Lock in space";
                if (statusLabel != null) statusLabel.text = "Rotate the phone to view the cube";
                return;
            }

            if (ARSession.state != ARSessionState.SessionTracking)
            {
                if (statusLabel != null)
                    statusLabel.text = "Not tracking yet — aim at a textured surface and move the phone slowly";
                Debug.Log($"[CubeDemo] Lock refused, state={ARSession.state}");
                return;
            }

            // Anchor the cube at its current world pose; the camera (driven by
            // the pose driver) now moves around the fixed cube.
            anchorObj = new GameObject("CubeWorldAnchor");
            anchorObj.transform.SetPositionAndRotation(cubeRoot.position, cubeRoot.rotation);
            cubeRoot.SetParent(anchorObj.transform, true);
            worldAnchored = true;

            if (lockButtonLabel != null) lockButtonLabel.text = "Free (gyro)";
            if (statusLabel != null) statusLabel.text = "Locked — walk around the cube!";
            Debug.Log("[CubeDemo] Cube world-anchored");
        }

        // Gyro attitude is right-handed; convert to Unity's left-handed frame.
        private static Quaternion GyroToUnity(Quaternion q)
        {
            return new Quaternion(q.x, q.y, -q.z, -q.w);
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

        /// <summary>Park the cube floating in the air in front of the camera.</summary>
        public void PlaceInFront()
        {
            if (cubeRoot == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            Vector3 fwd = cam.transform.forward;
            cubeRoot.position = cam.transform.position + fwd * PlaceDistance;

            Vector3 flatForward = new Vector3(fwd.x, 0f, fwd.z);
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
            baseRotation = Quaternion.LookRotation(flatForward.normalized, Vector3.up);
            cubeRoot.rotation = baseRotation;

            // Re-sync the gyro so "Recenter" faces the cube at the user again
            gyroCalibrated = false;
        }

        public void Orbit(Vector2 delta)
        {
            if (cubeRoot == null) return;
            Vector3 right = Camera.main != null ? Camera.main.transform.right : Vector3.right;
            Quaternion d = Quaternion.AngleAxis(-delta.x * 0.3f, Vector3.up)
                         * Quaternion.AngleAxis(delta.y * 0.3f, right);
            // Fold drag into the offset so it composes with the gyro tracking
            gyroOffset = d * gyroOffset;
            baseRotation = d * baseRotation;
        }

        private void OnEnable()
        {
            if (cubeRoot != null) cubeRoot.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            StopPreview();
            if (cubeRoot != null) cubeRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------- AR camera

        private void EnableARCamera()
        {
            if (arSession == null) arSession = FindFirstObjectByType<ARSession>();
            if (arCameraManager == null) arCameraManager = FindFirstObjectByType<ARCameraManager>();
            if (arCameraBackground == null) arCameraBackground = FindFirstObjectByType<ARCameraBackground>();

            if (arSession != null) arSession.enabled = true;
            if (arCameraManager != null) arCameraManager.enabled = true;
            if (arCameraBackground != null) arCameraBackground.enabled = true;

            // Make sure the camera actually follows the phone's motion so the
            // user can walk around the anchored cube (not just drag it).
            RubiksCube.AR.ARCameraPoseDriver.Ensure(Camera.main);

            Debug.Log("[CubeDemo] AR camera re-enabled for world-space step guide");
        }

        // ------------------------------------------------------------- cube build

        private void BuildCube(string letters)
        {
            foreach (var kv in cubies)
                if (kv.Value != null) Destroy(kv.Value.gameObject);
            cubies.Clear();

            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        var coord = new Vector3Int(x, y, z);
                        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        body.name = $"Cubie_{x}_{y}_{z}";
                        Destroy(body.GetComponent<Collider>());
                        body.transform.SetParent(cubeRoot, false);
                        body.transform.localPosition = (Vector3)coord * Spacing;
                        body.transform.localScale = Vector3.one * 0.96f;
                        body.GetComponent<Renderer>().sharedMaterial = GetMaterial('K');
                        cubies[coord] = body.transform;
                    }

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
                case 1: coord = new Vector3Int(1, 1 - r, c - 1); break;        // R
                case 2: coord = new Vector3Int(c - 1, 1 - r, -1); break;       // F
                case 3: coord = new Vector3Int(c - 1, -1, r - 1); break;       // D
                case 4: coord = new Vector3Int(-1, 1 - r, 1 - c); break;       // L
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
            if (colorChar == 'K') return new Color(0.05f, 0.05f, 0.05f); // cubie body
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

        /// <summary>Drag handler on the panel that spins the cube in place.</summary>
        public class CubeDemoDragger : MonoBehaviour, IDragHandler
        {
            public CubeDemo3D target;
            public void OnDrag(PointerEventData eventData) => target?.Orbit(eventData.delta);
        }
    }
}
