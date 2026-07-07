using UnityEngine;
using Tower.Core;

namespace Tower.UI
{
    // Builds a terrain Mesh from a HeightField (75 §4 single-source). The pure
    // HeightField drives vertex displacement; normals come from the built mesh so
    // lighting stays consistent with the same source. Formalized reuse of the
    // execute_code terrain demo.
    public sealed class FloorTerrainMeshBuilder
    {
        // resolution = samples per axis (>= 2). The mesh spans [0, sizeX] x [0, sizeZ]
        // in local space, with Y from the height field. uvScale tiles the UVs.
        public Mesh Build(HeightField heightField, int resolution, float sizeX, float sizeZ, Vector2 uvScale)
        {
            if (heightField == null)
            {
                throw new System.ArgumentNullException(nameof(heightField));
            }

            if (resolution < 2)
            {
                throw new System.ArgumentOutOfRangeException(nameof(resolution), "Resolution must be at least 2.");
            }

            float[,] heights = heightField.Generate(resolution, sizeX, sizeZ, 0f, 0f);

            int vertsPerAxis = resolution;
            int vertexCount = vertsPerAxis * vertsPerAxis;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] triangles = new int[(vertsPerAxis - 1) * (vertsPerAxis - 1) * 6];

            float step = 1f / (vertsPerAxis - 1);

            int v = 0;
            for (int row = 0; row < vertsPerAxis; row++)
            {
                float tz = row * step;
                float z = tz * sizeZ;
                for (int col = 0; col < vertsPerAxis; col++)
                {
                    float tx = col * step;
                    float x = tx * sizeX;

                    vertices[v] = new Vector3(x, heights[row, col], z);
                    uv[v] = new Vector2(tx * uvScale.x, tz * uvScale.y);
                    v++;
                }
            }

            int t = 0;
            for (int row = 0; row < vertsPerAxis - 1; row++)
            {
                for (int col = 0; col < vertsPerAxis - 1; col++)
                {
                    int i0 = row * vertsPerAxis + col;
                    int i1 = i0 + 1;
                    int i2 = i0 + vertsPerAxis;
                    int i3 = i2 + 1;

                    triangles[t++] = i0;
                    triangles[t++] = i2;
                    triangles[t++] = i1;

                    triangles[t++] = i1;
                    triangles[t++] = i2;
                    triangles[t++] = i3;
                }
            }

            Mesh mesh = new Mesh { name = "FloorTerrain" };
            if (vertexCount > 65535)
            {
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
