using System;
using System.Collections.Generic;

namespace PoseSystem
{
    [Serializable]
    public struct HandKeypoint
    {
        public int id;
        public float x, y, z, score;
    }

    public interface IHandSource
    {
        bool TryGetHand(out List<HandKeypoint> kps, out float overallScore);
    }
}
