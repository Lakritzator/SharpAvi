using System;

namespace SharpAvi.Vfw.Enums
{
    /// <summary>
    /// This enum is a result of the call to ICCompress
    /// </summary>
    [Flags]
    public enum AviIndexFlags
    {
        /// <summary>
        /// Default frame
        /// </summary>
        AVIIF_NONE = 0,
        /// <summary>
        /// Current frame is a key frame.
        /// </summary>
        AVIIF_KEYFRAME = 0x00000001
    }
}
