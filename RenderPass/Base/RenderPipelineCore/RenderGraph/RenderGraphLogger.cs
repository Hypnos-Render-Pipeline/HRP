using System;
using System.Text;
using UnityEngine;

namespace HypnosRenderPipeline.RenderGraph
{
    struct FRGLogIndent : IDisposable
    {
        int                 m_Indentation;
        FRGLogger m_Logger;
        bool                m_Disposed;

        public FRGLogIndent(FRGLogger logger, int indentation = 1)
        {
            m_Disposed = false;
            m_Indentation = indentation;
            m_Logger = logger;

            m_Logger.IncrementIndentation(m_Indentation);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        void Dispose(bool disposing)
        {
            Debug.Assert(m_Logger != null, "RenderGraphLogIndent: logger parameter should not be null.");

            if (m_Disposed)
                return;

            if (disposing && m_Logger != null)
            {
                m_Logger.DecrementIndentation(m_Indentation);
            }

            m_Disposed = true;
        }
    }

    class FRGLogger
    {
        StringBuilder   m_Builder = new StringBuilder();
        int             m_CurrentIndentation;

        public void Initialize()
        {
            m_Builder.Clear();
            m_CurrentIndentation = 0;
        }

        public void IncrementIndentation(int value)
        {
            m_CurrentIndentation += Math.Abs(value);
        }

        public void DecrementIndentation(int value)
        {
            m_CurrentIndentation = Math.Max(0, m_CurrentIndentation - Math.Abs(value));
        }

        public void LogLine(string format, params object[] args)
        {
            for (int i = 0; i < m_CurrentIndentation; ++i)
                m_Builder.Append('\t');
            m_Builder.AppendFormat(format, args);
            m_Builder.AppendLine();
        }

        public string GetLog()
        {
            return m_Builder.ToString();
        }
    }
}
