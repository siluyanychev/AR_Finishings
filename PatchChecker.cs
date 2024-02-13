using System.IO;


namespace AR_Finishings
{
    public class PathChecker
    {
        public bool IsPathAccess()
        {
            return Directory.Exists("K:");
        }
    }
}
