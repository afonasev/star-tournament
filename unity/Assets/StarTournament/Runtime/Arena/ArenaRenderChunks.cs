using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTournament.ProvingGround
{
    // Presentation only: retain the authored objects/colliders and all triangle attributes.
    // Large material-wide meshes otherwise submit distant rooms to every visible/shadow pass.
    public sealed class ArenaRenderChunks : MonoBehaviour
    {
        const float CellSize = 12f;
        readonly List<Mesh> owned = new List<Mesh>();
        readonly List<MeshRenderer> sources = new List<MeshRenderer>();
        readonly List<MeshRenderer> chunks = new List<MeshRenderer>();
        public double PreparationMilliseconds { get; private set; }
        public int SourceCount => sources.Count;
        public int ChunkCount => chunks.Count;

        public void Build(Transform arena, bool staticBatching = true)
        {
            var preparation = System.Diagnostics.Stopwatch.StartNew();
            var projection = arena.GetComponent<ProvingArena>();
            var solidIds = projection ? new HashSet<string>(projection.Definition.Solids.Select(s => s.Id)) : new HashSet<string>();
            int minimumIndices = projection && projection.Definition.MapId == LunarLaboratoryCatalog.Id ? 300000 : 24000;
            foreach (var source in arena.GetComponentsInChildren<MeshRenderer>())
            {
                var filter = source.GetComponent<MeshFilter>();
                if (!filter) continue;
                var mesh = filter.sharedMesh;
                if (!source.enabled || !mesh || !mesh.isReadable || mesh.blendShapeCount != 0 ||
                    source.GetComponent<TextMesh>() || source.HasPropertyBlock() || source.additionalVertexStreams ||
                    source.GetComponentInParent<LODGroup>() || source.GetComponentInParent<Animator>() || source.GetComponentInParent<Animation>()) continue;
                var materials = source.sharedMaterials;
                if (Enumerable.Range(0, mesh.subMeshCount).Any(i => mesh.GetTopology(i) != MeshTopology.Triangles) ||
                    materials.Length != mesh.subMeshCount || materials.Any(m => !m || m.renderQueue >= 2500)) continue;
                // Authored solids are immutable world geometry. Lifts and animated decoration
                // are deliberately absent from this allowlist; their transforms must stay live.
                bool structural = projection && source.transform.parent == arena &&
                    solidIds.Contains(source.name) && materials.Length == 1;
                if (structural)
                {
                    var part = CreateRenderer(source, mesh, materials[0], ProbeAnchor(source, mesh), "static solid render");
                    sources.Add(source); chunks.Add(part); source.enabled = false; continue;
                }
                if (mesh.vertexCount < 128 || Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z) < CellSize * 2) continue;
                if (mesh.HasVertexAttribute(VertexAttribute.BlendWeight) || mesh.HasVertexAttribute(VertexAttribute.BlendIndices)) continue;
                if (Enumerable.Range(0, mesh.subMeshCount).Sum(i => (long)mesh.GetIndexCount(i)) < minimumIndices) continue;
                float cellSize = Mathf.Max(CellSize, Mathf.Max(mesh.bounds.size.x, mesh.bounds.size.y, mesh.bounds.size.z) / 3f);
                var anchor = ProbeAnchor(source, mesh);
                var vertices = mesh.vertices;
                var normals = mesh.normals;
                var tangents = mesh.tangents;
                var colors = mesh.colors;
                var uv = new List<Vector4>[8];
                for (int channel = 0; channel < uv.Length; channel++)
                { uv[channel] = new List<Vector4>(); mesh.GetUVs(channel, uv[channel]); }
                var parts = new List<MeshRenderer>();
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                {
                    var groups = new Dictionary<Vector3Int, List<int>>();
                    var indices = mesh.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        var center = (vertices[indices[i]] + vertices[indices[i + 1]] + vertices[indices[i + 2]]) / 3;
                        var key = Vector3Int.FloorToInt(center / cellSize);
                        if (!groups.TryGetValue(key, out var group)) groups[key] = group = new List<int>();
                        group.Add(indices[i]); group.Add(indices[i + 1]); group.Add(indices[i + 2]);
                    }
                    foreach (var pair in groups)
                    {
                        var remap = new Dictionary<int, int>();
                        var originals = new List<int>(); var triangles = new List<int>();
                        foreach (int index in pair.Value)
                        {
                            if (!remap.TryGetValue(index, out int mapped))
                            { mapped = originals.Count; remap.Add(index, mapped); originals.Add(index); }
                            triangles.Add(mapped);
                        }
                        var part = new Mesh { name = mesh.name + " / cell " + pair.Key, indexFormat = IndexFormat.UInt32 };
                        part.SetVertices(originals.Select(i => vertices[i]).ToList());
                        if (normals.Length > 0) part.SetNormals(originals.Select(i => normals[i]).ToList());
                        if (tangents.Length > 0) part.SetTangents(originals.Select(i => tangents[i]).ToList());
                        if (colors.Length > 0) part.SetColors(originals.Select(i => colors[i]).ToList());
                        for (int channel = 0; channel < uv.Length; channel++)
                        {
                            if (uv[channel].Count == 0) continue;
                            var values = originals.Select(i => uv[channel][i]).ToList();
                            int dimension = mesh.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0 + channel));
                            if (dimension == 2) part.SetUVs(channel, values.Select(v => new Vector2(v.x, v.y)).ToList());
                            else if (dimension == 3) part.SetUVs(channel, values.Select(v => new Vector3(v.x, v.y, v.z)).ToList());
                            else part.SetUVs(channel, values);
                        }
                        part.SetTriangles(triangles, 0); part.RecalculateBounds(); owned.Add(part);
                        var renderer = CreateRenderer(source, part, materials[sub], anchor, "render-cell " + pair.Key);
                        parts.Add(renderer);
                    }
                }
                if (parts.Count == 0) continue;
                sources.Add(source); chunks.AddRange(parts); source.enabled = false;
            }
            if (staticBatching && chunks.Count > 0)
            {
                var inputs = new HashSet<Mesh>(chunks.Select(r => r.GetComponent<MeshFilter>().sharedMesh));
                StaticBatchingUtility.Combine(chunks.Select(r => r.gameObject).ToArray(), arena.gameObject);
                // Unity replaces the filters with shared combined meshes. Own those too;
                // never retain a native mesh after its arena projection is destroyed.
                var outputs = new HashSet<Mesh>(chunks.Select(r => r.GetComponent<MeshFilter>().sharedMesh));
                foreach (var mesh in outputs)
                    if (mesh && !inputs.Contains(mesh) && !owned.Contains(mesh)) owned.Add(mesh);
                // Intermediate cell meshes replaced by a combined output are no longer used.
                foreach (var mesh in owned.ToArray())
                    if (!outputs.Contains(mesh)) { owned.Remove(mesh); if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); }
            }
            preparation.Stop(); PreparationMilliseconds = preparation.Elapsed.TotalMilliseconds;
        }

        static Transform ProbeAnchor(MeshRenderer source, Mesh mesh)
        {
            if (source.probeAnchor) return source.probeAnchor;
            var anchor = new GameObject("source probe anchor").transform;
            anchor.SetParent(source.transform, false); anchor.localPosition = mesh.bounds.center; return anchor;
        }
        static MeshRenderer CreateRenderer(MeshRenderer source, Mesh mesh, Material material, Transform anchor, string name)
        {
            var go = new GameObject(name) { layer = source.gameObject.layer };
            go.transform.SetParent(source.transform, false); go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = source.shadowCastingMode; renderer.receiveShadows = source.receiveShadows;
            renderer.lightProbeUsage = source.lightProbeUsage; renderer.reflectionProbeUsage = source.reflectionProbeUsage;
            renderer.probeAnchor = anchor; renderer.lightProbeProxyVolumeOverride = source.lightProbeProxyVolumeOverride;
            renderer.renderingLayerMask = source.renderingLayerMask; renderer.motionVectorGenerationMode = source.motionVectorGenerationMode;
            renderer.allowOcclusionWhenDynamic = source.allowOcclusionWhenDynamic;
            renderer.sortingLayerID = source.sortingLayerID; renderer.sortingOrder = source.sortingOrder;
            renderer.lightmapIndex = source.lightmapIndex; renderer.lightmapScaleOffset = source.lightmapScaleOffset;
            renderer.realtimeLightmapIndex = source.realtimeLightmapIndex; renderer.realtimeLightmapScaleOffset = source.realtimeLightmapScaleOffset;
            return renderer;
        }
        public Mesh[] ReadOwnedMeshes() => owned.ToArray();
        // Controlled diagnostic switch: no rebuild, random input or changes to scene settings.
        public void UseChunks(bool value)
        { foreach (var source in sources) if (source) source.enabled = !value; foreach (var chunk in chunks) if (chunk) chunk.enabled = value; }
        void OnDestroy() { foreach (var mesh in owned) if (mesh) { if(Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh); } }
    }
}
