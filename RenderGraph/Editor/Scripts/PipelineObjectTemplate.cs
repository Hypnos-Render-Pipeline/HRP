using UnityEngine;
using UnityEditor;
using System.Collections;
using UnityEngine.Rendering;
using System.Collections.Generic;

namespace HypnosRenderPipeline.RenderPass
{
    public class PipelineObjectTemplate : ScriptableObject
    {
        private bool IsInitDrity = true;

        private List<BaseRenderPass> RenderPassArray = new List<BaseRenderPass>(64);


        //SRP CullbackFunction
        public void Submit(RenderContext RenderingContext) 
        {
            //Init RenderPass
            if (IsInitDrity) 
            {
                foreach (BaseRenderPass RenderPass in RenderPassArray) {
                    RenderPass.Init(RenderingContext);
                }

                IsInitDrity = false;
            }

            //Render Loop
            foreach (BaseRenderPass RenderPass in RenderPassArray)
            {
                RenderPass.OnRender(RenderingContext);
            }
        }

        public void Release(RenderContext RenderingContext) 
        {
            //Release RenderPass
            foreach (BaseRenderPass RenderPass in RenderPassArray)
            {
                RenderPass.Release(RenderingContext);
            }
        }


        //if Drity Init Pipeline
        public void MakeInitDrity()
        {
            IsInitDrity = true;
        }
    }
}
