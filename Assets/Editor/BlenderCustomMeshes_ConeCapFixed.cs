// Fixed-resolution procedural meshes with texture UVs.
// Place in Assets/Editor. Keep only ONE BlenderCustomMeshes class.
using System.Collections.Generic;
using UnityEngine;

public static class BlenderCustomMeshes
{
    public static Mesh Cone()
    {
        const int n = 32;
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        // Unwrapped side: a rectangular strip with a duplicated seam.
        for (int i = 0; i <= n; i++)
        {
            float u = (float)i / n;
            float a = u * Mathf.PI * 2f;
            vertices.Add(new Vector3(Mathf.Cos(a), -1f, Mathf.Sin(a)));
            uv.Add(new Vector2(u, 0f));
            vertices.Add(Vector3.up);
            uv.Add(new Vector2(u, 1f));
        }
        for (int i = 0; i < n; i++)
        {
            int a = i * 2;
            triangles.Add(a); triangles.Add(a + 1); triangles.Add(a + 2);
        }
        // Separate planar-mapped bottom cap, wound downward so the underside renders.
        int center = vertices.Count;
        vertices.Add(Vector3.down);
        uv.Add(new Vector2(0.5f, 0.5f));
        int start = vertices.Count;
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            float x = Mathf.Cos(a), z = Mathf.Sin(a);
            vertices.Add(new Vector3(x, -1f, z));
            uv.Add(new Vector2(0.5f + 0.5f * x, 0.5f + 0.5f * z));
        }
        for (int i = 0; i < n; i++)
        {
            triangles.Add(center);
            triangles.Add(start + i);
            triangles.Add(start + (i + 1) % n);
        }
        return Build(vertices, uv, triangles);
    }

    public static Mesh Torus()
    {
        const int major = 32, minor = 12;
        const float R = 0.8f, r = 0.25f;
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var triangles = new List<int>();
        for (int i = 0; i <= major; i++)
        {
            float u = (float)i / major;
            float a = u * Mathf.PI * 2f;
            for (int j = 0; j <= minor; j++)
            {
                float v = (float)j / minor;
                float b = v * Mathf.PI * 2f;
                float radius = R + r * Mathf.Cos(b);
                vertices.Add(new Vector3(radius * Mathf.Cos(a), r * Mathf.Sin(b), radius * Mathf.Sin(a)));
                uv.Add(new Vector2(u, v));
            }
        }
        for (int i = 0; i < major; i++)
        for (int j = 0; j < minor; j++)
        {
            int a = i * (minor + 1) + j;
            int b = a + minor + 1;
            triangles.Add(a); triangles.Add(a + 1); triangles.Add(b);
            triangles.Add(a + 1); triangles.Add(b + 1); triangles.Add(b);
        }
        return Build(vertices, uv, triangles);
    }

    public static Mesh Icosphere()
    {
        float t = (1f + Mathf.Sqrt(5f)) / 2f;
        var vertices = new List<Vector3> {
            new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),
            new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),
            new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1)
        };
        for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
        var faces = new List<int> {
            0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11,
            1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
            3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9,
            4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1
        };
        for (int level = 0; level < 2; level++)
        {
            var cache = new Dictionary<long, int>();
            var next = new List<int>();
            for (int i = 0; i < faces.Count; i += 3)
            {
                int a = faces[i], b = faces[i + 1], c = faces[i + 2];
                int ab = Mid(a, b, vertices, cache);
                int bc = Mid(b, c, vertices, cache);
                int ca = Mid(c, a, vertices, cache);
                next.AddRange(new[] { a,ab,ca, b,bc,ab, c,ca,bc, ab,bc,ca });
            }
            faces = next;
        }
        // Equirectangular spherical UVs. Split vertices at the 0/1 seam.
        // Per-triangle vertices also prevent interpolation across the seam.
        var finalVertices = new List<Vector3>();
        var finalUV = new List<Vector2>();
        var finalFaces = new List<int>();
        for (int i = 0; i < faces.Count; i += 3)
        {
            Vector3 a = vertices[faces[i]], b = vertices[faces[i + 1]], c = vertices[faces[i + 2]];
            Vector2 ua = SphereUV(a), ub = SphereUV(b), uc = SphereUV(c);
            float min = Mathf.Min(ua.x, Mathf.Min(ub.x, uc.x));
            float max = Mathf.Max(ua.x, Mathf.Max(ub.x, uc.x));
            if (max - min > 0.5f)
            {
                if (ua.x < 0.5f) ua.x += 1f;
                if (ub.x < 0.5f) ub.x += 1f;
                if (uc.x < 0.5f) uc.x += 1f;
            }
            // Keep polar triangles' UVs stable.
            if (Mathf.Abs(a.y) > 0.9999f) ua.x = (ub.x + uc.x) * 0.5f;
            if (Mathf.Abs(b.y) > 0.9999f) ub.x = (ua.x + uc.x) * 0.5f;
            if (Mathf.Abs(c.y) > 0.9999f) uc.x = (ua.x + ub.x) * 0.5f;
            int start = finalVertices.Count;
            finalVertices.Add(a); finalVertices.Add(b); finalVertices.Add(c);
            finalUV.Add(ua); finalUV.Add(ub); finalUV.Add(uc);
            finalFaces.Add(start); finalFaces.Add(start + 1); finalFaces.Add(start + 2);
        }
        Mesh mesh = Build(finalVertices, finalUV, finalFaces);
        // Smooth normals across UV seams (the shape is a unit sphere).
        mesh.SetNormals(finalVertices);
        return mesh;
    }

    private static Vector2 SphereUV(Vector3 p)
    {
        return new Vector2(Mathf.Atan2(p.z, p.x) / (2f * Mathf.PI) + 0.5f,
                           Mathf.Asin(Mathf.Clamp(p.y, -1f, 1f)) / Mathf.PI + 0.5f);
    }

    private static int Mid(int a, int b, List<Vector3> vertices, Dictionary<long, int> cache)
    {
        int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
        long key = ((long)lo << 32) | (uint)hi;
        if (cache.TryGetValue(key, out int existing)) return existing;
        int index = vertices.Count;
        vertices.Add((vertices[a] + vertices[b]).normalized);
        cache[key] = index;
        return index;
    }

    private static Mesh Build(List<Vector3> vertices, List<Vector2> uv, List<int> triangles)
    {
        var mesh = new Mesh();
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();
        return mesh;
    }
}
