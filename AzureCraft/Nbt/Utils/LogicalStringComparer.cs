using System.Runtime.InteropServices;

namespace AzureCraft.Nbt.Utils
{
    // sort like file explorer does (e.g. 1->2 instead of 1->10)
    public class LogicalStringComparer : IComparer<string>
    {
        public static readonly LogicalStringComparer Instance = new LogicalStringComparer();
        private LogicalStringComparer() { }

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        static extern int StrCmpLogicalW(string x, string y);
        public int Compare(string x, string y)
        {
            return StrCmpLogicalW(x, y);
        }
    }
}
