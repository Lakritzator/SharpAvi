using SharpAvi.Vfw.Enums;

namespace SharpAvi.Vfw
{
    /// <summary>
    /// Extension methods for IcResults
    /// </summary>
    public static class IcResultExtensions
    {
        /// <summary>
        /// Test if the call was successful.
        /// </summary>
        /// <param name="result">IcResults</param>
        /// <returns>bool</returns>
        public static bool IsSuccess(this IcResults result) => result == IcResults.ICERR_OK;

        /// <summary>
        /// Get a description for the IcResults
        /// </summary>
        /// <param name="error">IcResults</param>
        /// <returns>string</returns>
        public static string GetErrorDescription(this IcResults error)
        {
            switch (error)
            {
                case IcResults.ICERR_OK: return "OK";
                case IcResults.ICERR_UNSUPPORTED: return "Unsupported";
                case IcResults.ICERR_BAD_FORMAT: return "Bad format";
                case IcResults.ICERR_MEMORY: return "Memory";
                case IcResults.ICERR_INTERNAL: return "Internal";
                case IcResults.ICERR_BAD_FLAGS: return "Bad flags";
                case IcResults.ICERR_BAD_PARAMETER: return "Bad parameter";
                case IcResults.ICERR_BAD_SIZE: return "Bad size";
                case IcResults.ICERR_BAD_HANDLE: return "Bad handle";
                case IcResults.ICERR_CANNOT_UPDATE: return "Can't update";
                case IcResults.ICERR_ABORT: return "Abort";
                case IcResults.ICERR_ERROR: return "Error";
                case IcResults.ICERR_BAD_BIT_DEPTH: return "Bad bit depth";
                case IcResults.ICERR_BAD_IMAGE_SIZE: return "Bad image size";
                default: return "Unknown " + error;
            }
        }
    }
}
