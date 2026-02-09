using System.Collections.Generic;
using System.Linq;
using TUA.Core.Interfaces;
using TUA.Misc;
using UnityEngine;
using Unity.AI.Navigation;

namespace TUA.Systems
{
    public class MinimapController : SingletonBehaviour<MinimapController>
    {
        #region Serialized Fields
        [Header("NavMesh")]
        [SerializeField] private NavMeshSurface navMeshSurface;
        
        [Header("Rendering")]
        [SerializeField] private RenderTexture targetRenderTexture;
        [SerializeField] private int renderTextureWidth = 512;
        [SerializeField] private int renderTextureHeight = 512;
        
        [Header("Minimap Settings")]
        [SerializeField] private float border = 2f;
        [SerializeField] private int frameGap = 1;
        
        [Header("Colors")]
        [SerializeField] private Color backgroundColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [SerializeField] private Color navMeshFillColor = new Color(0.3f, 0.5f, 0.8f, 0.6f);
        [SerializeField] private Color navMeshOutlineColor = new Color(0.2f, 0.4f, 0.7f, 1f);
        [SerializeField] private float outlineWidth = 1f;
        [SerializeField] private float edgeMatchTolerance = 0.01f;
        
        [Header("Minimap Objects")]
        [SerializeField] private MinimapSpriteEntry[] spriteRegistry = new MinimapSpriteEntry[0];
        #endregion

        #region Fields
        private bool _isInitialized = false;
        private Texture2D _drawTexture;
        private Texture2D _baseMapTexture;
        private Bounds minimapBounds = new Bounds(Vector3.zero, new Vector3(100f, 0f, 100f));
        private Dictionary<string, (Sprite sprite, float size)> _spriteCache = new Dictionary<string, (Sprite, float)>();
        private bool _baseMapDirty = true;
        private int _frameCounter = 0;
        #endregion

        #region Nested Types
        [System.Serializable]
        public class MinimapSpriteEntry
        {
            public string key;
            public Sprite sprite;
            public float size = 1f;
        }
        #endregion

        #region Properties
        public RenderTexture TargetRenderTexture => targetRenderTexture;

        public NavMeshSurface NavMeshSurface
        {
            get => navMeshSurface;
            set => navMeshSurface = value;
        }

        public Bounds MinimapBounds => minimapBounds;

        public float Border
        {
            get => border;
            set => border = Mathf.Max(0f, value);
        }

        public Color BackgroundColor
        {
            get => backgroundColor;
            set => backgroundColor = value;
        }

        public Color NavMeshFillColor
        {
            get => navMeshFillColor;
            set => navMeshFillColor = value;
        }

        public Color NavMeshOutlineColor
        {
            get => navMeshOutlineColor;
            set => navMeshOutlineColor = value;
        }

        public float OutlineWidth
        {
            get => outlineWidth;
            set => outlineWidth = Mathf.Max(0f, value);
        }

        public float EdgeMatchTolerance
        {
            get => edgeMatchTolerance;
            set => edgeMatchTolerance = Mathf.Max(0.001f, value);
        }

        public int FrameGap
        {
            get => frameGap;
            set => frameGap = Mathf.Max(1, value);
        }
        #endregion

        #region Unity Callbacks
        protected override void Awake()
        {
            base.Awake();
            Initialize();
        }

        private void Start()
        {
            Render(true);
        }

        private void Update()
        {
            if (Application.isPlaying)
            {
                _frameCounter++;
                if (_frameCounter >= frameGap)
                {
                    _frameCounter = 0;
                    Render(false);
                }
            }
        }
        #endregion

        #region Public Methods
        public void Initialize()
        {
            if (navMeshSurface == null)
                navMeshSurface = FindFirstObjectByType<NavMeshSurface>();

            var needsRenderTexture = targetRenderTexture == null ||
                                     targetRenderTexture.width != renderTextureWidth ||
                                     targetRenderTexture.height != renderTextureHeight;

            var needsDrawTexture = _drawTexture == null ||
                                   _drawTexture.width != renderTextureWidth ||
                                   _drawTexture.height != renderTextureHeight;

            var needsBaseMapTexture = _baseMapTexture == null ||
                                      _baseMapTexture.width != renderTextureWidth ||
                                      _baseMapTexture.height != renderTextureHeight;

            if (_isInitialized && !needsRenderTexture && !needsDrawTexture && !needsBaseMapTexture)
                return;

            if (needsRenderTexture)
            {
                CreateRenderTexture();
                _baseMapDirty = true;
            }

            if (needsDrawTexture)
            {
                if (_drawTexture != null)
                    DestroyUnityObject(_drawTexture);
                _drawTexture = new Texture2D(renderTextureWidth, renderTextureHeight, TextureFormat.RGBA32, false);
                _drawTexture.filterMode = FilterMode.Bilinear;
            }

            if (needsBaseMapTexture)
            {
                if (_baseMapTexture != null)
                    DestroyUnityObject(_baseMapTexture);
                _baseMapTexture = new Texture2D(renderTextureWidth, renderTextureHeight, TextureFormat.RGBA32, false);
                _baseMapTexture.filterMode = FilterMode.Bilinear;
                _baseMapDirty = true;
            }

            _isInitialized = true;
        }

        public void Refresh()
        {
            Render(true);
        }
        public void BuildSpriteCache()
        {
            _spriteCache.Clear();
            foreach (var entry in spriteRegistry)
            {
                if (entry != null && !string.IsNullOrEmpty(entry.key) && entry.sprite != null)
                {
                    _spriteCache[entry.key] = (entry.sprite, entry.size);
                }
            }
        }
        public void Render(bool forceRebuild)
        {
            if (forceRebuild)
                ClearBuffers();

            Initialize();

            if (targetRenderTexture == null || _drawTexture == null || _baseMapTexture == null)
                return;

            if (_spriteCache.Count == 0)
                BuildSpriteCache();

            if (forceRebuild)
            {
                _baseMapDirty = true;
                _frameCounter = 0;
            }

            var previousBounds = minimapBounds;

            if (navMeshSurface != null)
            {
                var triangulation = UnityEngine.AI.NavMesh.CalculateTriangulation();
                if (triangulation.indices == null || triangulation.indices.Length == 0)
                {
                    navMeshSurface.BuildNavMesh();
                    _baseMapDirty = true;
                }
            }

            CalculateBoundsFromNavMesh();
            if (!BoundsApproximatelyEqual(previousBounds, minimapBounds))
                _baseMapDirty = true;

            if (_baseMapDirty)
            {
                var rebuiltOk = RebuildBaseMap();
                _baseMapDirty = !rebuiltOk;
            }

            if (_baseMapTexture != null)
                Graphics.CopyTexture(_baseMapTexture, _drawTexture);
            else
            {
                var pixels = new Color32[renderTextureWidth * renderTextureHeight];
                for (int i = 0; i < pixels.Length; i++)
                {
                    pixels[i] = backgroundColor;
                }
                _drawTexture.SetPixels32(pixels);
            }

            DrawMinimapObjects();

            _drawTexture.Apply();
            Graphics.Blit(_drawTexture, targetRenderTexture);
        }

        private static bool BoundsApproximatelyEqual(Bounds a, Bounds b)
        {
            const float eps = 0.01f;
            return (a.center - b.center).sqrMagnitude <= eps * eps && (a.size - b.size).sqrMagnitude <= eps * eps;
        }

        private bool RebuildBaseMap()
        {
            if (_baseMapTexture == null)
                return false;

            var pixels = new Color32[renderTextureWidth * renderTextureHeight];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = backgroundColor;
            }
            _baseMapTexture.SetPixels32(pixels);

            var drewNavMesh = false;
            if (navMeshSurface != null)
            {
                drewNavMesh = DrawNavMeshToTexture(_baseMapTexture);
            }

            _baseMapTexture.Apply();
            return navMeshSurface == null || drewNavMesh;
        }
        #endregion

        #region Private Methods
        private void CalculateBoundsFromNavMesh()
        {
            if (navMeshSurface == null)
            {
                minimapBounds = new Bounds(Vector3.zero, new Vector3(100f, 0f, 100f));
                return;
            }

            var triangulation = UnityEngine.AI.NavMesh.CalculateTriangulation();
            
            if (triangulation.vertices == null || triangulation.vertices.Length == 0)
            {
                minimapBounds = new Bounds(Vector3.zero, new Vector3(100f, 0f, 100f));
                return;
            }

            var vertices = triangulation.vertices;
            
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;

            foreach (var vertex in vertices)
            {
                minX = Mathf.Min(minX, vertex.x);
                maxX = Mathf.Max(maxX, vertex.x);
                minZ = Mathf.Min(minZ, vertex.z);
                maxZ = Mathf.Max(maxZ, vertex.z);
            }
            
            minX -= border;
            maxX += border;
            minZ -= border;
            maxZ += border;

            var center = new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f);
            var size = new Vector3(maxX - minX, 0f, maxZ - minZ);

            if (size.x < 1f) size.x = 1f;
            if (size.z < 1f) size.z = 1f;

            minimapBounds = new Bounds(center, size);
        }

        private void CreateRenderTexture()
        {
            if (targetRenderTexture != null)
            {
                targetRenderTexture.Release();
                Object.DestroyImmediate(targetRenderTexture);
            }

            targetRenderTexture = new RenderTexture(renderTextureWidth, renderTextureHeight, 0, RenderTextureFormat.ARGB32);
            targetRenderTexture.name = "MinimapRenderTexture";
            targetRenderTexture.Create();
        }

        public Vector2 WorldToMinimapPixel(Vector3 worldPos)
        {
            return WorldToTextureSpace(worldPos);
        }

        private Vector2 WorldToTextureSpace(Vector3 worldPos)
        {
            var localPos = worldPos - minimapBounds.min;
            var normalizedX = minimapBounds.size.x > 0 ? localPos.x / minimapBounds.size.x : 0f;
            var normalizedZ = minimapBounds.size.z > 0 ? localPos.z / minimapBounds.size.z : 0f;

            var texX = Mathf.Clamp01(normalizedX) * renderTextureWidth;
            var texY = Mathf.Clamp01(normalizedZ) * renderTextureHeight;

            return new Vector2(texX, texY);
        }

        private bool DrawNavMeshToTexture(Texture2D targetTexture)
        {
            if (navMeshSurface == null || targetTexture == null)
                return false;

            var triangulation = UnityEngine.AI.NavMesh.CalculateTriangulation();
            
            if (triangulation.vertices == null || triangulation.indices == null || 
                triangulation.vertices.Length == 0 || triangulation.indices.Length == 0)
                return false;

            var vertices = triangulation.vertices;
            var indices = triangulation.indices;

            for (int i = 0; i < indices.Length; i += 3)
            {
                if (i + 2 >= indices.Length)
                    break;

                var v1 = vertices[indices[i]];
                var v2 = vertices[indices[i + 1]];
                var v3 = vertices[indices[i + 2]];

                var p1 = new Vector3(v1.x, 0f, v1.z);
                var p2 = new Vector3(v2.x, 0f, v2.z);
                var p3 = new Vector3(v3.x, 0f, v3.z);

                FillTriangleToTexture(
                    targetTexture,
                    WorldToTextureSpace(p1),
                    WorldToTextureSpace(p2),
                    WorldToTextureSpace(p3),
                    navMeshFillColor
                );
            }

            if (outlineWidth > 0f)
            {
                var edgeCount = new Dictionary<Edge, int>();

                for (int i = 0; i < indices.Length; i += 3)
                {
                    if (i + 2 >= indices.Length)
                        break;

                    var v1 = vertices[indices[i]];
                    var v2 = vertices[indices[i + 1]];
                    var v3 = vertices[indices[i + 2]];

                    var p1 = new Vector2(v1.x, v1.z);
                    var p2 = new Vector2(v2.x, v2.z);
                    var p3 = new Vector2(v3.x, v3.z);

                    AddEdgeByPosition(edgeCount, p1, p2);
                    AddEdgeByPosition(edgeCount, p2, p3);
                    AddEdgeByPosition(edgeCount, p3, p1);
                }

                foreach (var kvp in edgeCount)
                {
                    if (kvp.Value == 1)
                    {
                        var edge = kvp.Key;
                        var p1 = new Vector3(edge.p1.x, 0f, edge.p1.y);
                        var p2 = new Vector3(edge.p2.x, 0f, edge.p2.y);

                        var t1 = WorldToTextureSpace(p1);
                        var t2 = WorldToTextureSpace(p2);

                        DrawThickLineToTexture(targetTexture, t1, t2, outlineWidth * 0.5f, navMeshOutlineColor);
                    }
                }
            }

            return true;
        }

        private void DrawMinimapObjects()
        {
            var minimapObjects = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .OfType<IMinimapObject>()
                .ToList();

            foreach (var minimapObject in minimapObjects)
            {
                if (minimapObject == null)
                    continue;

                var monoBehaviour = minimapObject as MonoBehaviour;
                if (monoBehaviour == null || !monoBehaviour.gameObject.activeInHierarchy)
                    continue;

                if (minimapObject.GetMinimapTarget(out var worldPos, out var color, out var spriteKey, out var scale, out var rotationY))
                {
                    DrawMinimapIcon(worldPos, color, spriteKey, scale, rotationY);
                }
            }
        }

        private void DrawMinimapIcon(Vector3 worldPos, Color color, string spriteKey, float scale, float rotationY)
        {
            var texPos = WorldToTextureSpace(worldPos);

            if (!string.IsNullOrEmpty(spriteKey) && _spriteCache.TryGetValue(spriteKey, out var spriteData))
            {
                var sprite = spriteData.sprite;
                var spriteSize = spriteData.size;
                DrawSprite(texPos, sprite, color, scale * spriteSize, rotationY);
            }
        }

        private void DrawSprite(Vector2 texPos, Sprite sprite, Color color, float scale, float rotationY)
        {
            if (sprite == null || sprite.texture == null)
                return;

            var texture = sprite.texture;
            var rect = sprite.textureRect;
            Color[] pixels = texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);

            var halfWidth = rect.width * scale * 0.5f;
            var halfHeight = rect.height * scale * 0.5f;

            var spriteWidth = (int)rect.width;
            var spriteHeight = (int)rect.height;

            var angleRad = -rotationY * Mathf.Deg2Rad;
            var cosA = Mathf.Cos(angleRad);
            var sinA = Mathf.Sin(angleRad);
            var absCos = Mathf.Abs(cosA);
            var absSin = Mathf.Abs(sinA);

            var destHalfWidth = absCos * halfWidth + absSin * halfHeight;
            var destHalfHeight = absSin * halfWidth + absCos * halfHeight;

            var startX = Mathf.RoundToInt(texPos.x - destHalfWidth);
            var startY = Mathf.RoundToInt(texPos.y - destHalfHeight);
            var endX = Mathf.RoundToInt(texPos.x + destHalfWidth);
            var endY = Mathf.RoundToInt(texPos.y + destHalfHeight);

            var denomSpriteX = Mathf.Max(0.0001f, rect.width * scale);
            var denomSpriteY = Mathf.Max(0.0001f, rect.height * scale);

            for (int y = startY; y <= endY; y++)
            {
                if (y < 0 || y >= renderTextureHeight)
                    continue;

                for (int x = startX; x <= endX; x++)
                {
                    if (x < 0 || x >= renderTextureWidth)
                        continue;

                    var dx = (x + 0.5f) - texPos.x;
                    var dy = (y + 0.5f) - texPos.y;

                    var srcDx = (cosA * dx) + (sinA * dy);
                    var srcDy = (-sinA * dx) + (cosA * dy);

                    var u = (srcDx / denomSpriteX) + 0.5f;
                    var v = (srcDy / denomSpriteY) + 0.5f;

                    if (u < 0f || u > 1f || v < 0f || v > 1f)
                        continue;

                    var spriteX = Mathf.Clamp(Mathf.FloorToInt(u * spriteWidth), 0, spriteWidth - 1);
                    var spriteY = Mathf.Clamp(Mathf.FloorToInt(v * spriteHeight), 0, spriteHeight - 1);

                    if (spriteX < 0 || spriteX >= spriteWidth || spriteY < 0 || spriteY >= spriteHeight)
                        continue;

                    var pixelIndex = spriteY * spriteWidth + spriteX;
                    if (pixelIndex < 0 || pixelIndex >= pixels.Length)
                        continue;

                    var spriteColor = pixels[pixelIndex];
                    if (spriteColor.a < 0.01f)
                        continue;

                    var blendedColor = BlendColors(_drawTexture.GetPixel(x, y), color * spriteColor);
                    _drawTexture.SetPixel(x, y, blendedColor);
                }
            }
        }

        private void ClearBuffers()
        {
            _isInitialized = false;
            _baseMapDirty = true;

            if (_drawTexture != null)
            {
                DestroyUnityObject(_drawTexture);
                _drawTexture = null;
            }

            if (_baseMapTexture != null)
            {
                DestroyUnityObject(_baseMapTexture);
                _baseMapTexture = null;
            }

            _spriteCache.Clear();
        }

        private void DestroyUnityObject(Object obj)
        {
            if (obj == null)
                return;

            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }
        private struct Edge
        {
            public Vector2 p1;
            public Vector2 p2;
            private float _tolerance;

            public Edge(Vector2 p1, Vector2 p2, float tolerance)
            {
                _tolerance = tolerance;
                if (p1.x < p2.x || (Mathf.Approximately(p1.x, p2.x) && p1.y < p2.y))
                {
                    this.p1 = p1;
                    this.p2 = p2;
                }
                else
                {
                    this.p1 = p2;
                    this.p2 = p1;
                }
            }

            public override int GetHashCode()
            {
                var quantizedP1 = new Vector2(
                    Mathf.RoundToInt(p1.x / _tolerance) * _tolerance,
                    Mathf.RoundToInt(p1.y / _tolerance) * _tolerance
                );
                var quantizedP2 = new Vector2(
                    Mathf.RoundToInt(p2.x / _tolerance) * _tolerance,
                    Mathf.RoundToInt(p2.y / _tolerance) * _tolerance
                );
                return quantizedP1.x.GetHashCode() ^ quantizedP1.y.GetHashCode() ^ 
                       quantizedP2.x.GetHashCode() ^ quantizedP2.y.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj is Edge other)
                {
                    return (Vector2.Distance(p1, other.p1) < _tolerance && 
                            Vector2.Distance(p2, other.p2) < _tolerance) ||
                           (Vector2.Distance(p1, other.p2) < _tolerance && 
                            Vector2.Distance(p2, other.p1) < _tolerance);
                }
                return false;
            }
        }

        private void AddEdgeByPosition(Dictionary<Edge, int> edgeCount, Vector2 p1, Vector2 p2)
        {
            var edge = new Edge(p1, p2, edgeMatchTolerance);
            
            Edge? matchingEdge = null;
            foreach (var existingEdge in edgeCount.Keys)
            {
                if (edge.Equals(existingEdge))
                {
                    matchingEdge = existingEdge;
                    break;
                }
            }

            if (matchingEdge.HasValue)
            {
                edgeCount[matchingEdge.Value]++;
            }
            else
            {
                edgeCount[edge] = 1;
            }
        }

        private void DrawThickLineToTexture(Texture2D targetTexture, Vector2 start, Vector2 end, float halfWidth, Color color)
        {
            if (halfWidth <= 0f)
            {
                var x = Mathf.RoundToInt(start.x);
                var y = Mathf.RoundToInt(start.y);
                if (x >= 0 && x < renderTextureWidth && y >= 0 && y < renderTextureHeight)
                {
                    var currentColor = targetTexture.GetPixel(x, y);
                    var blendedColor = BlendColors(currentColor, color);
                    targetTexture.SetPixel(x, y, blendedColor);
                }
                return;
            }

            var dir = (end - start).normalized;
            if (dir.sqrMagnitude < 0.0001f)
                return;

            var perp = new Vector2(-dir.y, dir.x) * halfWidth;

            var p1 = start - perp;
            var p2 = end - perp;
            var p3 = end + perp;
            var p4 = start + perp;

            FillTriangleToTexture(targetTexture, p1, p2, p3, color);
            FillTriangleToTexture(targetTexture, p1, p3, p4, color);
        }

        private void FillTriangleToTexture(Texture2D targetTexture, Vector2 v1, Vector2 v2, Vector2 v3, Color color)
        {
            var minX = Mathf.FloorToInt(Mathf.Min(v1.x, Mathf.Min(v2.x, v3.x)));
            var maxX = Mathf.CeilToInt(Mathf.Max(v1.x, Mathf.Max(v2.x, v3.x)));
            var minY = Mathf.FloorToInt(Mathf.Min(v1.y, Mathf.Min(v2.y, v3.y)));
            var maxY = Mathf.CeilToInt(Mathf.Max(v1.y, Mathf.Max(v2.y, v3.y)));

            minX = Mathf.Clamp(minX, 0, renderTextureWidth - 1);
            maxX = Mathf.Clamp(maxX, 0, renderTextureWidth - 1);
            minY = Mathf.Clamp(minY, 0, renderTextureHeight - 1);
            maxY = Mathf.Clamp(maxY, 0, renderTextureHeight - 1);

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    var p = new Vector2(x, y);
                    if (IsPointInTriangle(p, v1, v2, v3))
                    {
                        var currentColor = targetTexture.GetPixel(x, y);
                        var blendedColor = BlendColors(currentColor, color);
                        targetTexture.SetPixel(x, y, blendedColor);
                    }
                }
            }
        }

        private bool IsPointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            var v0 = c - a;
            var v1 = b - a;
            var v2 = p - a;

            var dot00 = Vector2.Dot(v0, v0);
            var dot01 = Vector2.Dot(v0, v1);
            var dot02 = Vector2.Dot(v0, v2);
            var dot11 = Vector2.Dot(v1, v1);
            var dot12 = Vector2.Dot(v1, v2);

            var invDenom = 1f / (dot00 * dot11 - dot01 * dot01);
            var u = (dot11 * dot02 - dot01 * dot12) * invDenom;
            var v = (dot00 * dot12 - dot01 * dot02) * invDenom;

            return (u >= 0) && (v >= 0) && (u + v <= 1);
        }

        private Color BlendColors(Color background, Color foreground)
        {
            var alpha = foreground.a;
            var invAlpha = 1f - alpha;
            return new Color(
                foreground.r * alpha + background.r * invAlpha,
                foreground.g * alpha + background.g * invAlpha,
                foreground.b * alpha + background.b * invAlpha,
                Mathf.Min(1f, background.a + foreground.a)
            );
        }
        #endregion

        #region Cleanup
        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (targetRenderTexture != null)
            {
                targetRenderTexture.Release();
            }

            if (_drawTexture != null)
            {
                Object.Destroy(_drawTexture);
            }

            if (_baseMapTexture != null)
            {
                Object.Destroy(_baseMapTexture);
            }
        }
        #endregion
    }
}
