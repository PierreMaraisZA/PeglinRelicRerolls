using UnityEngine;

namespace PeglinRelicRerolls
{
    /// <summary>
    /// Builds a 32x32 pixel-art isometric die (1 pip on top, 2 on the left face, 3 on the right)
    /// so the mod ships without image assets.
    /// </summary>
    internal static class DieSprite
    {
        private const int Size = 32;

        // Cube geometry in pixels, y pointing down: half-width of the top rhombus and side height.
        private const float CentreX = 16f;
        private const float TopY = 1.5f;
        private const float HalfWidth = 14f;
        private const float SideHeight = 15f;

        private static readonly Color TopColor = new Color32(234, 92, 112, 255);
        private static readonly Color LeftColor = new Color32(170, 30, 52, 255);
        private static readonly Color RightColor = new Color32(214, 50, 72, 255);
        private static readonly Color OutlineColor = new Color32(64, 14, 24, 255);
        private static readonly Color PipColor = new Color32(255, 250, 244, 255);

        private static Sprite _sprite;

        private struct Face
        {
            public Vector2 Origin;
            public Vector2 EdgeU;
            public Vector2 EdgeV;
            public Color Color;
            public Vector2[] Pips;
            public float PipRadius;

            /// <summary>Converts a pixel position into this face's (u, v) coordinates, each 0..1 inside the face.</summary>
            public Vector2 ToFace(Vector2 point)
            {
                Vector2 d = point - Origin;
                float det = EdgeU.x * EdgeV.y - EdgeV.x * EdgeU.y;
                return new Vector2((d.x * EdgeV.y - EdgeV.x * d.y) / det, (EdgeU.x * d.y - d.x * EdgeU.y) / det);
            }
        }

        public static Sprite Get()
        {
            if (_sprite != null)
            {
                return _sprite;
            }

            Vector2 top = new Vector2(CentreX, TopY);
            Vector2 upperLeft = new Vector2(CentreX - HalfWidth, TopY + HalfWidth / 2f);
            Vector2 upperRight = new Vector2(CentreX + HalfWidth, TopY + HalfWidth / 2f);
            Vector2 centre = new Vector2(CentreX, TopY + HalfWidth);
            Vector2 down = new Vector2(0f, SideHeight);

            Face[] faces =
            {
                new Face
                {
                    Origin = top, EdgeU = upperRight - top, EdgeV = upperLeft - top, Color = TopColor,
                    Pips = new[] { new Vector2(0.5f, 0.5f) }, PipRadius = 0.17f,
                },
                new Face
                {
                    Origin = upperLeft, EdgeU = centre - upperLeft, EdgeV = down, Color = LeftColor,
                    Pips = new[] { new Vector2(0.28f, 0.28f), new Vector2(0.72f, 0.72f) }, PipRadius = 0.13f,
                },
                new Face
                {
                    Origin = centre, EdgeU = upperRight - centre, EdgeV = down, Color = RightColor,
                    Pips = new[] { new Vector2(0.75f, 0.22f), new Vector2(0.5f, 0.5f), new Vector2(0.25f, 0.78f) }, PipRadius = 0.12f,
                },
            };

            Color clear = new Color(0f, 0f, 0f, 0f);
            Color[,] pixels = new Color[Size, Size];
            bool[,] filled = new bool[Size, Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    pixels[x, y] = clear;
                    Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                    foreach (Face face in faces)
                    {
                        Vector2 uv = face.ToFace(point);
                        if (uv.x < 0f || uv.x > 1f || uv.y < 0f || uv.y > 1f)
                        {
                            continue;
                        }
                        Color color = face.Color;
                        foreach (Vector2 pip in face.Pips)
                        {
                            if ((uv - pip).sqrMagnitude <= face.PipRadius * face.PipRadius)
                            {
                                color = PipColor;
                            }
                        }
                        pixels[x, y] = color;
                        filled[x, y] = true;
                        break;
                    }
                }
            }

            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Color color = pixels[x, y];
                    if (filled[x, y] && IsEdge(filled, x, y))
                    {
                        color = OutlineColor;
                    }
                    // Texture rows start at the bottom; our drawing has y pointing down.
                    texture.SetPixel(x, Size - 1 - y, color);
                }
            }
            texture.Apply();

            _sprite = Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), Size);
            _sprite.name = "PeglinRelicRerollsDie";
            return _sprite;
        }

        private static bool IsEdge(bool[,] filled, int x, int y)
        {
            return x == 0 || y == 0 || x == Size - 1 || y == Size - 1
                || !filled[x - 1, y] || !filled[x + 1, y] || !filled[x, y - 1] || !filled[x, y + 1];
        }
    }
}
