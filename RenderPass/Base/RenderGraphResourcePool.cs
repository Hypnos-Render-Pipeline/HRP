using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using HypnosRenderPipeline.RenderPass;

namespace HypnosRenderPipeline.RenderGraph
{
    public struct FRGBufferDesc
    {
        public int count;
        public int stride;
        public ComputeBufferType type;
        public string name;

        public FRGBufferDesc(int count, int stride) : this()
        {
            this.count = count;
            this.stride = stride;
            type = ComputeBufferType.Default;
        }

        public FRGBufferDesc(int count, int stride, ComputeBufferType type) : this()
        {
            this.count = count;
            this.stride = stride;
            this.type = type;
        }

        public override int GetHashCode()
        {
            int hashCode = 17;

            hashCode = hashCode * 23 + count;
            hashCode = hashCode * 23 + stride;
            hashCode = hashCode * 23 + (int)type;

            return hashCode;
        }
    }

    abstract class FRGResourcePool<Type> where Type : class
    {
        protected Dictionary<int, List<(Type resource, int frameIndex)>> m_ResourcePool = new Dictionary<int, List<(Type resource, int frameIndex)>>();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        List<(int, Type)> m_FrameAllocatedResources = new List<(int, Type)>();
#endif

        protected static int s_CurrentFrameIndex;

        abstract protected void ReleaseInternalResource(Type res);
        abstract protected string GetResourceName(Type res);
        abstract protected string GetResourceTypeName();

        public void ReleaseResource(int hash, Type resource, int currentFrameIndex)
        {
            if (!m_ResourcePool.TryGetValue(hash, out var list))
            {
                list = new List<(Type resource, int frameIndex)>();
                m_ResourcePool.Add(hash, list);
            }

            list.Add((resource, currentFrameIndex));
        }

        public bool TryGetResource(int hashCode, out Type resource)
        {
            if (m_ResourcePool.TryGetValue(hashCode, out var list) && list.Count > 0)
            {
                resource = list[list.Count - 1].resource;
                list.RemoveAt(list.Count - 1); 
                return true;
            }

            resource = null;
            return false;
        }

        abstract public void PurgeUnusedResources(int currentFrameIndex);

        public void Cleanup()
        {
            foreach (var kvp in m_ResourcePool)
            {
                foreach (var res in kvp.Value)
                {
                    ReleaseInternalResource(res.resource);
                }
            }
        }

        public void RegisterFrameAllocation(int hash, Type value)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (hash != -1)
                m_FrameAllocatedResources.Add((hash, value));
#endif
        }

        public void UnregisterFrameAllocation(int hash, Type value)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (hash != -1)
                m_FrameAllocatedResources.Remove((hash, value));
#endif
        }

        public void CheckFrameAllocation(bool onException, int frameIndex)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (m_FrameAllocatedResources.Count != 0 && !onException)
            {
                string logMessage = $"RenderGraph: Not all resources of type {GetResourceTypeName()} were released. This can be caused by a resources being allocated but never read by any pass.";

                foreach (var value in m_FrameAllocatedResources)
                {
                    logMessage = $"{logMessage}\n\t{GetResourceName(value.Item2)}";
                    ReleaseResource(value.Item1, value.Item2, frameIndex);
                }

                Debug.LogWarning(logMessage);
            }

            m_FrameAllocatedResources.Clear();
#endif
        }
    }

    class FRGTexturePool : FRGResourcePool<FRTHandle>
    {
        protected override void ReleaseInternalResource(FRTHandle res)
        {
            res.Release();
        }

        protected override string GetResourceName(FRTHandle res)
        {
            return res.rt.name;
        }

        override protected string GetResourceTypeName()
        {
            return "Texture";
        }

        override public void PurgeUnusedResources(int currentFrameIndex)
        {
            s_CurrentFrameIndex = currentFrameIndex;

            foreach (var kvp in m_ResourcePool)
            {
                var list = kvp.Value;
                list.RemoveAll(obj =>
                {
                    if (obj.frameIndex < s_CurrentFrameIndex)
                    {
                        obj.resource.Release();
                        return true;
                    }
                    return false;
                });
            }
        }
    }

    class FRGBufferPool : FRGResourcePool<ComputeBuffer>
    {
        protected override void ReleaseInternalResource(ComputeBuffer res)
        {
            res.Release();
        }

        protected override string GetResourceName(ComputeBuffer res)
        {
            return "BufferNameNotAvailable";
        }

        override protected string GetResourceTypeName()
        {
            return "Buffer";
        }

        override public void PurgeUnusedResources(int currentFrameIndex)
        {
            s_CurrentFrameIndex = currentFrameIndex;

            foreach (var kvp in m_ResourcePool)
            {
                var list = kvp.Value;
                list.RemoveAll(obj =>
                {
                    if (obj.frameIndex < s_CurrentFrameIndex)
                    {
                        obj.resource.Release();
                        return true;
                    }
                    return false;
                });
            }
        }
    }

    public class RenderGraphResourcePool
    {
        FRGBufferPool m_BufferPool = new FRGBufferPool();

        FRGTexturePool m_TexturePool = new FRGTexturePool();

        public void CreateBuffer(RenderContext Context, FRGBufferDesc bufferDesc, int index)
        {
            /*var resource = m_Resources[(int)FRGResourceType.Buffer][index] as BufferResource;

            if (resource.resource != null)
                throw new InvalidOperationException(string.Format("Trying to create an already created Compute Buffer ({0}). Buffer was probably declared for writing more than once in the same pass.", resource.desc.name));

            resource.resource = null;
            if (!m_BufferPool.TryGetResource(hashCode, out resource.resource))
            {
                resource.resource = new ComputeBuffer(resource.desc.count, resource.desc.stride, resource.desc.type);
                resource.resource.name = m_RenderGraphDebug.tagResourceNamesWithRG ? $"RenderGraph_{resource.desc.name}" : resource.desc.name;
            }
            resource.cachedHash = hashCode;

            m_BufferPool.RegisterFrameAllocation(hashCode, resource.resource);*/
        }

    }
}
