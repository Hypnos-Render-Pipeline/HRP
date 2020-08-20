using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HypnosRenderPipeline
{
    class IESParser
    {
        public enum PhotometryType
        {
            C = 1,
            B = 2,
            A = 3
        }

        enum CubeFace
        {
            Right,
            Left,
            Top,
            Bottom,
            Front,
            Back
        }

        #region Properties

        public string IESversion;
        public string Manufac;
        public string Lumcat;
        public string Luminaire;
        public string Lamp;
        public string Tilt;
        public int NumOfLamps;
        public int LumensPerLamp;
        public int CDMultiplier;
        public int NumVertAngles;
        public int NumHorizAngles;
        public PhotometryType photometryType;
        public int units;
        public double ballastFactor;
        public double inputWatts;
        public List<double> VertAnglesList = new List<double>();
        public List<double> HorizAnglesList = new List<double>();
        public float[,] candelasMatrix;
        public float maxCandelas;

        #endregion

        public IESParser(string iesFile)
        {
            FileStream fs = new FileStream(iesFile, FileMode.Open);
            StreamReader sr = new StreamReader(fs);
            int count = 0;
            int res = 0;
            string readLine = "";
            bool data = false;
            int v = 0, h = 0, d = 0;
            maxCandelas = 0;
            while (!sr.EndOfStream)
            {
                readLine = sr.ReadLine();

                if (!data)
                {
                    while (readLine.Length != 0 && readLine[0] == ' ')
                        readLine = readLine.Substring(1);
                    if (readLine.Contains("IESNA"))
                    {
                        if (readLine.Contains("IESNA:"))
                        {
                            readLine = readLine.Substring(6);
                        }
                        IESversion = readLine;
                    }
                    if (readLine.Contains("[MANUFAC]"))
                    {
                        Manufac = readLine;
                    }
                    if (readLine.Contains("[LUMCAT]"))
                    {
                        Lumcat = readLine;
                    }
                    if (readLine.Contains("[LUMINAIRE]"))
                    {
                        Luminaire = readLine;
                    }
                    if (readLine.Contains("[LAMP]"))
                    {
                        Lamp = readLine;
                    }
                    if (readLine.Contains("TILT="))
                    {
                        Tilt = readLine;
                    }

                    if (Tilt != null && res == 0 && int.TryParse(readLine.Substring(0, 1), out res))
                    {
                        try
                        {
                            List<string> firstLine = new List<string>(readLine.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                            while (firstLine.Count < 13)
                            {
                                readLine = sr.ReadLine();
                                firstLine.AddRange(readLine.Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                            }
                            int.TryParse(firstLine[0], out NumOfLamps);
                            int.TryParse(firstLine[1], out LumensPerLamp);
                            int.TryParse(firstLine[2], out CDMultiplier);
                            int.TryParse(firstLine[3], out NumVertAngles);
                            int.TryParse(firstLine[4], out NumHorizAngles);
                            int a = 0;
                            int.TryParse(firstLine[5], out a); photometryType = (PhotometryType)a;
                            int.TryParse(firstLine[6], out units);

                            double.TryParse(firstLine[10], out ballastFactor);
                            double.TryParse(firstLine[12], out inputWatts);
                            data = true;

                            candelasMatrix = new float[NumVertAngles, NumHorizAngles];
                        }
                        catch { }
                    }
                }
                else
                {
                    string[] line = readLine.TrimEnd().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var num in line)
                    {
                        float k;
                        if (float.TryParse(num, out k))
                        {
                            if (v++ < NumVertAngles)
                            {
                                VertAnglesList.Add(k);
                            }
                            else if (h++ < NumHorizAngles)
                            {
                                HorizAnglesList.Add(k);
                            }
                            else
                            {
                                candelasMatrix[d % NumVertAngles, d / NumVertAngles] = k;
                                maxCandelas = maxCandelas > k ? maxCandelas : k;
                                d++;
                            }
                        }
                    }

                }
                count++;
            }

            sr.Close();
            fs.Close();
        }

        public Cubemap RenderCubemap(int resolution)
        {
            try
            {
                float fr = resolution;
                Cubemap cubemap = new Cubemap(resolution, TextureFormat.RFloat, true);

                for (int face = 0; face < 6; face++)
                {
                    float offset = (1 / fr) / 2;
                    Color[] colors = new Color[resolution * resolution];
                    for (int y = 0; y < resolution; y++)
                    {
                        float yNorm = (y / fr) + offset;
                        for (int x = 0; x < resolution; x++)
                        {
                            float xNorm = (x / fr) + offset;
                            Vector3 cubeP = Vector3.zero;
                            switch ((CubemapFace)face)
                            {
                                case CubemapFace.PositiveZ:
                                    cubeP = new Vector3(xNorm - 0.5f, yNorm - 0.5f, 0.5f);
                                    break;
                                case CubemapFace.NegativeZ:
                                    cubeP = new Vector3(0.5f - xNorm, yNorm - 0.5f, -0.5f);
                                    break;
                                case CubemapFace.NegativeX:
                                    cubeP = new Vector3(-0.5f, yNorm - 0.5f, xNorm - 0.5f);
                                    break;
                                case CubemapFace.PositiveX:
                                    cubeP = new Vector3(0.5f, yNorm - 0.5f, 0.5f - xNorm);
                                    break;
                                case CubemapFace.NegativeY:
                                    cubeP = new Vector3(xNorm - 0.5f, 0.5f, yNorm - 0.5f);
                                    break;
                                case CubemapFace.PositiveY:
                                    cubeP = new Vector3(xNorm - 0.5f, -0.5f, 0.5f - yNorm);
                                    break;
                            }
                            cubeP /= cubeP.magnitude;

                            Vector2 latLongPoint = new Vector2();
                            latLongPoint.y = (-cubeP.y + 1) / 2 * 180;
                            latLongPoint.x = Mathf.Abs(Mathf.Atan2(cubeP.z, cubeP.x) / Mathf.PI) * 180;
                            float candela = InterpolatedCandelaFromData(latLongPoint);
                            colors[x + y * resolution] = Color.red * candela;
                        }
                    }
                    cubemap.SetPixels(colors, (CubemapFace)face);
                }
                cubemap.Apply();

                return cubemap;
            }
            catch
            {
                Debug.LogError("Unsupported IES File!");
                return null;
            }
        }

        static void BinarySearch(List<double> data, double value, out int i, out int j, out float t)
        {
            int low = 0, high = data.Count - 1;
            while (low < high - 1)
            {
                int mid = (low + high) / 2;
                var v = data[mid];
                if (v < value)
                {
                    low = mid;
                }
                else if (v > value)
                {
                    high = mid;
                }
                else
                {
                    i = mid;
                    j = mid;
                    t = 0;
                    return;
                }
            }

            i = low;
            j = Mathf.Min(data.Count - 1, low + 1);

            var d1 = data[i];
            var d2 = data[j];
            if (i == j) t = 0;
            else t = (float)(value - d1) / (float)(d2 - d1);
            return;
        }

        float InterpolatedCandelaFromData(Vector2 position)
        {
            int i, j;
            float t1;
            BinarySearch(VertAnglesList, position.y, out i, out j, out t1);

            int k, w;
            float t2;
            BinarySearch(HorizAnglesList, position.x, out k, out w, out t2);

            return (float)(Mathf.Lerp(Mathf.Lerp(candelasMatrix[i, k], candelasMatrix[i, w], t2), Mathf.Lerp(candelasMatrix[j, k], candelasMatrix[j, w], t2), t1) / maxCandelas);
        }
    }
}
