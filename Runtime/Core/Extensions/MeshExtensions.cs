using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace Nexus.Core.Extensions
{
    /// <summary>
    /// Extension methods and combination helpers for <see cref="Mesh"/> covering safe CPU
    /// data access, recalculation, index format management, optimization, and mesh combining.
    /// <para>Design rules:</para>
    /// <list type="bullet">
    /// <item>Reading <see cref="Mesh.vertices"/>, <see cref="Mesh.triangles"/>,
    /// <see cref="Mesh.normals"/>, or similar properties on a mesh with
    /// <see cref="Mesh.isReadable"/> <c>false</c> throws — the default for most imported
    /// meshes once "Read/Write Enabled" is disabled to save memory. Every accessor here
    /// checks readability first and fails safely (returning an empty result or a <c>bool</c>)
    /// instead of crashing gameplay code the moment an artist disables that import setting.</item>
    /// <item><see cref="Mesh.vertices"/>, <see cref="Mesh.triangles"/>, and their siblings
    /// each allocate and return a brand-new managed array on every single access — reading
    /// the same property twice in a loop silently doubles the allocation. The <c>*NonAlloc</c>
    /// variants here use <see cref="Mesh.GetVertices(List{Vector3})"/>-style APIs into a
    /// caller-owned <see cref="List{T}"/> instead, which is the only allocation-free way to
    /// read mesh data repeatedly (e.g. per-frame skinned mesh sampling).</item>
    /// <item><see cref="Mesh.RecalculateNormals()"/>, <see cref="Mesh.RecalculateTangents()"/>,
    /// and <see cref="Mesh.RecalculateBounds()"/> are all comparatively expensive full-mesh
    /// passes. They are never called implicitly by any setter in this file; callers must
    /// invoke them explicitly after a batch of edits, exactly as raw <see cref="Mesh"/> usage
    /// requires, to avoid hiding a per-vertex-edit performance cliff behind an innocuous-looking method.</item>
    /// <item>A mesh with more than 65535 vertices requires
    /// <see cref="IndexFormat.UInt32"/>; assigning vertex data past that count while still
    /// using <see cref="IndexFormat.UInt16"/> silently truncates or corrupts the mesh with no
    /// exception. <see cref="EnsureIndexFormatFor"/> exists specifically to catch this before
    /// it happens on large procedurally generated meshes (terrain chunks, marching-cubes output).</item>
    /// </list>
    /// </summary>
    public static class MeshExtensions
    {
        /// <summary>
        /// Largest vertex count representable by <see cref="IndexFormat.UInt16"/>. Meshes at
        /// or below this count can use the smaller, faster 16-bit index buffer; meshes above
        /// it require <see cref="IndexFormat.UInt32"/>.
        /// </summary>
        private const int Max16BitVertexCount = 65535;

        #region Safety & Validation

        /// <summary>
        /// True if vertex and index data can be read from this mesh on the CPU. <br/>
        /// Check this before any of the raw <c>vertices</c>/<c>triangles</c>/<c>normals</c>
        /// properties, or before any method in this class that reads them. A mesh imported
        /// with "Read/Write Enabled" disabled (increasingly the default, to save memory)
        /// throws on every one of those otherwise, and that setting can change after
        /// gameplay code was written and tested against it.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsReadableSafe(this Mesh mesh) => mesh != null && mesh.isReadable;

        /// <summary>
        /// True if the mesh has no vertices, meaning nothing would be rendered. <br/>
        /// Guards against a common procedural-generation bug where an empty vertex buffer is
        /// assigned to a mesh, silently producing an invisible object that logs no error and
        /// looks identical to a correctly hidden one.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEmpty(this Mesh mesh) => mesh == null || mesh.vertexCount == 0;

        /// <summary>
        /// True if the mesh has at least one submesh with a nonzero triangle count. <br/>
        /// A mesh can have vertices assigned but no valid triangle indices — for example a
        /// point-cloud debug mesh, or a half-finished procedural build — which renders
        /// nothing despite <see cref="IsEmpty"/> reporting <c>false</c>. This is the correct
        /// check for "will this mesh actually produce visible triangles."
        /// </summary>
        public static bool HasValidTriangles(this Mesh mesh)
        {
            if (mesh == null) return false;

            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                if (mesh.GetIndexCount(i) > 0) return true;
            }

            return false;
        }

        #endregion

        #region Allocation-Free Data Access

        /// <summary>
        /// Fills a caller-owned <paramref name="buffer"/> with the mesh's vertex positions,
        /// returning <c>false</c> without effect if the mesh is not readable. <br/>
        /// Reusing one <see cref="List{T}"/> across frames — instead of reading the
        /// allocating <see cref="Mesh.vertices"/> property, which returns a fresh array on
        /// every access — removes a per-frame GC allocation from skinned-mesh sampling,
        /// custom deformation, or per-vertex collision systems.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetVerticesNonAlloc(this Mesh mesh, List<Vector3> buffer)
        {
            if (!mesh.IsReadableSafe()) return false;
            mesh.GetVertices(buffer);
            return true;
        }

        /// <summary>
        /// Fills a caller-owned <paramref name="buffer"/> with the mesh's vertex normals,
        /// returning <c>false</c> without effect if the mesh is not readable. <br/>
        /// Allocation-free counterpart to the <see cref="Mesh.normals"/> property, intended
        /// for repeated per-frame reads such as custom lighting calculations or wind-sway
        /// vertex shaders driven from CPU-side data.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetNormalsNonAlloc(this Mesh mesh, List<Vector3> buffer)
        {
            if (!mesh.IsReadableSafe()) return false;
            mesh.GetNormals(buffer);
            return true;
        }

        /// <summary>
        /// Fills a caller-owned <paramref name="buffer"/> with the triangle indices of
        /// submesh <paramref name="submesh"/>, returning <c>false</c> without effect if the
        /// mesh is not readable. <br/>
        /// Allocation-free counterpart to <see cref="Mesh.triangles"/> for tools and runtime
        /// systems (navmesh baking, raycasting against a specific submesh, procedural mesh
        /// slicing) that need repeated index access without repeated array allocation.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetTrianglesNonAlloc(this Mesh mesh, List<int> buffer, int submesh = 0)
        {
            if (!mesh.IsReadableSafe()) return false;
            mesh.GetTriangles(buffer, submesh);
            return true;
        }

        /// <summary>
        /// Fills a caller-owned <paramref name="buffer"/> with the mesh's first UV channel,
        /// returning <c>false</c> without effect if the mesh is not readable. <br/>
        /// Allocation-free counterpart to <see cref="Mesh.uv"/>, useful for runtime texture
        /// baking or decal projection tools that need to read UVs every frame during an
        /// interactive editing session.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool GetUVsNonAlloc(this Mesh mesh, List<Vector2> buffer)
        {
            if (!mesh.IsReadableSafe()) return false;
            mesh.GetUVs(0, buffer);
            return true;
        }

        #endregion

        #region Bounds & Geometry

        /// <summary>
        /// Transforms the mesh's local-space <see cref="Mesh.bounds"/> into a world-space
        /// <see cref="Bounds"/> using <paramref name="transform"/>. <br/>
        /// <see cref="Mesh.bounds"/> alone is in local space and does not account for
        /// rotation or non-uniform scale; a naive <c>transform.TransformPoint(bounds.center)</c>
        /// plus an unscaled size undersizes or oversizes the result whenever the object is
        /// rotated. This method transforms all eight corners of the local AABB and
        /// re-encapsulates them, producing a correct — if conservative — world-space AABB
        /// suitable for frustum culling or spatial partitioning of a rotated, scaled mesh.
        /// </summary>
        public static Bounds GetWorldBounds(this Mesh mesh, Transform transform)
        {
            Bounds localBounds = mesh.bounds;
            Vector3 center = localBounds.center;
            Vector3 extents = localBounds.extents;

            Bounds worldBounds = new Bounds(transform.TransformPoint(center), Vector3.zero);

            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = center + new Vector3(
                    ((i & 1) == 0 ? -extents.x : extents.x),
                    ((i & 2) == 0 ? -extents.y : extents.y),
                    ((i & 4) == 0 ? -extents.z : extents.z));

                worldBounds.Encapsulate(transform.TransformPoint(corner));
            }

            return worldBounds;
        }

        /// <summary>
        /// Recalculates <see cref="Mesh.bounds"/> only if the mesh currently has vertices. <br/>
        /// Calling <see cref="Mesh.RecalculateBounds()"/> on an empty mesh is harmless but
        /// pointless; this guard keeps procedural-generation pipelines from wasting a call on
        /// a mesh that has been cleared but not yet repopulated.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void RecalculateBoundsSafe(this Mesh mesh)
        {
            if (mesh.vertexCount > 0) mesh.RecalculateBounds();
        }

        #endregion

        #region Index Format Management

        /// <summary>
        /// True if a mesh with <paramref name="vertexCount"/> vertices requires
        /// <see cref="IndexFormat.UInt32"/> rather than the default
        /// <see cref="IndexFormat.UInt16"/>. <br/>
        /// The 16-bit format can only address 65535 unique vertices; exceeding that while
        /// still using it silently corrupts the mesh with no exception, one of the most
        /// confusing bugs in procedural mesh generation because the failure mode looks like
        /// random missing triangles rather than an obvious error.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool RequiresWideIndices(int vertexCount) => vertexCount > Max16BitVertexCount;

        /// <summary>
        /// Sets <see cref="Mesh.indexFormat"/> to <see cref="IndexFormat.UInt32"/> if
        /// <paramref name="vertexCount"/> exceeds the 16-bit limit, or leaves it at
        /// <see cref="IndexFormat.UInt16"/> otherwise. <br/>
        /// Call this <b>before</b> assigning vertex data via <see cref="Mesh.SetVertices(List{Vector3})"/>
        /// or the <see cref="Mesh.vertices"/> setter on any procedurally generated mesh whose
        /// final vertex count is not known to be small at compile time — for example
        /// terrain chunks, marching-cubes output, or merged meshes — so a large mesh never
        /// silently corrupts itself for lack of a wide enough index buffer.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void EnsureIndexFormatFor(this Mesh mesh, int vertexCount)
        {
            mesh.indexFormat = RequiresWideIndices(vertexCount) ? IndexFormat.UInt32 : IndexFormat.UInt16;
        }

        #endregion

        #region Optimization & Performance

        /// <summary>
        /// Marks the mesh as frequently modified from script, hinting to Unity's graphics
        /// driver to place it in memory optimized for repeated updates rather than static
        /// rendering. <br/>
        /// Call this once, immediately after creating a <see cref="Mesh"/> that will have
        /// its vertex data rewritten every frame or every few frames — cloth, procedural
        /// terrain deformation, a real-time waveform visualizer — since the driver cannot
        /// safely infer this usage pattern on its own and defaults to assuming the mesh is
        /// static.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void MarkDynamicSafe(this Mesh mesh) => mesh.MarkDynamic();

        /// <summary>
        /// Uploads the mesh's current data to the GPU and, when <paramref name="makeUnreadable"/>
        /// is <c>true</c>, discards Unity's CPU-side copy afterward. <br/>
        /// For a mesh that has finished all of its procedural edits and will only ever be
        /// rendered from this point on, discarding the CPU copy frees the corresponding
        /// system memory — meaningful for large terrain or world-streaming meshes where
        /// keeping a full CPU-readable duplicate of GPU data around is pure waste. Do not
        /// pass <c>true</c> if any later code needs to read or edit this mesh again, since
        /// doing so would then throw exactly the readability errors this file otherwise guards against.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void UploadAndFreeIfDone(this Mesh mesh, bool makeUnreadable)
            => mesh.UploadMeshData(makeUnreadable);

        /// <summary>
        /// Runs Unity's internal mesh optimization pass, reordering vertices and triangles
        /// for better GPU vertex-cache utilization. <br/>
        /// Intended as a one-time finishing step for a procedurally generated mesh once its
        /// geometry is final — for example after a marching-cubes or mesh-combining pass —
        /// not as a per-frame operation, since the optimization itself is a nontrivial CPU
        /// cost that pays off only for a mesh that will subsequently be rendered many times.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void OptimizeSafe(this Mesh mesh) => mesh.Optimize();

        #endregion

        #region Cloning & Combining

        /// <summary>
        /// Creates an independent copy of this mesh via <see cref="Object.Instantiate(Object)"/>,
        /// with the <c>" (Clone)"</c> suffix Unity appends stripped from its name. <br/>
        /// Necessary before editing a mesh obtained from a shared
        /// <see cref="MeshFilter.sharedMesh"/> — editing that reference directly corrupts
        /// the mesh asset (or the shared mesh instance) for every other renderer using it,
        /// the mesh equivalent of the shared-material mutation bug guarded against in
        /// <c>MaterialExtensions</c>.
        /// </summary>
        public static Mesh CloneSafe(this Mesh source)
        {
            Mesh copy = Object.Instantiate(source);
            const string cloneSuffix = " (Clone)";
            if (copy.name.EndsWith(cloneSuffix)) copy.name = copy.name.Substring(0, copy.name.Length - cloneSuffix.Length);
            return copy;
        }

        /// <summary>
        /// Combines <paramref name="meshes"/>, each paired with a local transform matrix,
        /// into a single new <see cref="Mesh"/>, automatically selecting a wide index format
        /// if the combined vertex count requires it. <br/>
        /// This is the standard technique for merging many small static meshes — foliage
        /// instances, modular dungeon tiles, debris fragments — into one draw call to reduce
        /// per-object rendering overhead, without callers needing to manually track whether
        /// the combined result crosses the 65535-vertex 16-bit index limit.
        /// <para>
        /// Matches Unity's own <see cref="Mesh.CombineMeshes(CombineInstance[])"/> semantics:
        /// all input meshes are merged into submesh <c>0</c> of the result. Use
        /// <see cref="Mesh.CombineMeshes(CombineInstance[], bool)"/> directly if separate
        /// submeshes (for multi-material combined meshes) are required instead.
        /// </para>
        /// </summary>
        public static Mesh CombineSafe(IReadOnlyList<(Mesh mesh, Matrix4x4 localToWorld)> meshes)
        {
            int totalVertexCount = 0;
            CombineInstance[] combineInstances = new CombineInstance[meshes.Count];

            for (int i = 0; i < meshes.Count; i++)
            {
                combineInstances[i] = new CombineInstance
                {
                    mesh = meshes[i].mesh,
                    transform = meshes[i].localToWorld
                };
                totalVertexCount += meshes[i].mesh.vertexCount;
            }

            Mesh combined = new Mesh();
            combined.EnsureIndexFormatFor(totalVertexCount);
            combined.CombineMeshes(combineInstances);
            combined.RecalculateBoundsSafe();
            return combined;
        }

        #endregion

        #region Safe Destruction

        /// <summary>
        /// Destroys this mesh, using <see cref="Object.Destroy(Object)"/> during play mode or
        /// <see cref="Object.DestroyImmediate(Object)"/> otherwise. <br/>
        /// Procedurally generated meshes are plain C# heap objects from the CLR's
        /// perspective but hold unmanaged GPU buffers that are not freed by the garbage
        /// collector; failing to explicitly destroy a runtime-created <see cref="Mesh"/>
        /// that is no longer referenced (for example, replaced during LOD regeneration)
        /// leaks native memory for the lifetime of the process. This mirrors
        /// <c>ScriptableObjectExtensions.DestroyRuntimeCopySafe</c> so cleanup code reads
        /// consistently across both types.
        /// </summary>
        public static void DestroySafe(this Mesh mesh)
        {
            if (mesh == null) return;

            if (Application.isPlaying) Object.Destroy(mesh);
            else Object.DestroyImmediate(mesh);
        }

        #endregion
    }
}